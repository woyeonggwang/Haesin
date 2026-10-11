using UnityEngine;

/// <summary>
/// 현상금(2026-10-07 사용자 지시 "어쌔신크리드 오디세이처럼 관군 현상금 아이콘, 일본 현상금 아이콘 — 기존 게이지를 교체").
/// 예전 우호도 게이지(standing −100 관군 쪽 … +100 일본 쪽, 하나)를 진영별 현상금 두 개로 바꿨다. 클래스 이름은 씬 참조(스크립트 GUID)를 지키려고 그대로 둠.
///
///   관군 현상금 navyBounty, 일본 현상금 japanBounty (각 0~100 점)
///   - 단계(아이콘 수) = levelThresholds(25 / 40 / 55 / 70 / 85) 를 넘은 개수, 0~5. **1단계부터 그 진영이 플레이어를 공격**한다.
///   - 플레이어가 관군 배(판옥선·상선)를 때리면 관군 현상금이 오르고, 일본 현상금은 같은 양만큼 내린다(그 반대도 같음).
///     예전 게이지의 "한쪽이 오르면 다른 쪽이 내려감"(사용자 지정)을 그대로 살린 것 — 1막 1-4 "관군과 싸워 일본 적대를 낮춘다"가 이 규칙에 기대어 있다.
///   - 가만히 있으면 decayPerSecond 로 0 으로 돌아온다. 단 1단계 이상(적대)이 되면 돌아오지 않고 그대로 머문다
///     (2026-10-06 사용자 지시 "적대가 되면 그 상태로 멈추게, 적대를 내리지 마라"). 반대 진영을 쳐서 내리는 것은 됨.
///   - 왜구(Faction.Wokou)는 현상금과 상관없이 늘 적. 왜구를 쳐도 현상금은 안 움직임(격침 수만 셈).
///   - 관군 ↔ 일본군은 늘 적(ShipFaction.IsHostileTo).
/// 씬에 하나. 없으면 예전처럼 모든 진영이 플레이어의 적이다.
/// 단계 수·문턱·증감량은 Claude 가 정한 값(예전 게이지 문턱 25·증감량 그대로) — 손맛은 사용자 확인 필요.
/// </summary>
[DisallowMultipleComponent]
public class ReputationSystem : MonoBehaviour
{
    public static ReputationSystem Instance { get; private set; }

    public const int MaxLevel = 5;

    [Header("현상금 (점, 0~100)")]
    [Range(0f, 100f)] public float navyBounty = 0f;
    [Range(0f, 100f)] public float japanBounty = 0f;
    [Tooltip("아이콘 단계 문턱(점, 오름차순 5개). 첫 문턱을 넘으면(1단계) 그 진영이 공격한다.")]
    public float[] levelThresholds = { 25f, 40f, 55f, 70f, 85f };
    [Tooltip("적대가 아닐 때(0단계) 초당 0 으로 돌아오는 양. 0 이면 돌아오지 않는다.")]
    public float decayPerSecond = 0.1f;

    [Header("변화량")]
    [Tooltip("포탄 한 발을 맞힐 때.")] public float perHit = 2f;
    [Tooltip("군선(판옥선·안택선·세키부네)을 격침할 때 추가로.")] public float perWarshipSunk = 15f;
    [Tooltip("상선을 격침(약탈)할 때 추가로.")] public float perMerchantSunk = 10f;
    [Tooltip("한 진영을 칠 때 다른 진영 현상금이 내려가는 비율(1 = 오른 만큼).")] public float otherSideRelief = 1f;

    [Header("상태 (읽기 전용)")]
    public int navyLevel;
    public int japanLevel;
    public bool navyHostile;
    public bool japaneseHostile;
    public int navySunkByPlayer, japaneseSunkByPlayer, wokouSunkByPlayer, merchantsPlundered;

    /// <summary>현상금이 바뀔 때.</summary>
    public System.Action OnChanged;
    /// <summary>단계가 바뀔 때(진영, 예전 단계, 새 단계) — 화면 알림용.</summary>
    public System.Action<Faction, int, int> OnLevelChanged;

    void Awake() { if (Instance == null) Instance = this; Refresh(); }
    void OnDestroy() { if (Instance == this) Instance = null; }

    void Update()
    {
        if (decayPerSecond <= 0f) return;
        bool changed = false;
        float step = decayPerSecond * Time.deltaTime;
        if (navyBounty > 0f && navyLevel == 0) { navyBounty = Mathf.MoveTowards(navyBounty, 0f, step); changed = true; }
        if (japanBounty > 0f && japanLevel == 0) { japanBounty = Mathf.MoveTowards(japanBounty, 0f, step); changed = true; }
        if (changed) Refresh();
    }

    int LevelOf(float points)
    {
        int lv = 0;
        if (levelThresholds != null)
            for (int i = 0; i < levelThresholds.Length && i < MaxLevel; i++) if (points >= levelThresholds[i]) lv = i + 1;
        return lv;
    }

    void Refresh()
    {
        navyBounty = Mathf.Clamp(navyBounty, 0f, 100f);
        japanBounty = Mathf.Clamp(japanBounty, 0f, 100f);
        int n = LevelOf(navyBounty), j = LevelOf(japanBounty);
        int pn = navyLevel, pj = japanLevel;
        navyLevel = n; japanLevel = j;
        navyHostile = n > 0; japaneseHostile = j > 0;
        if (OnChanged != null) OnChanged();
        if (n != pn) { if (OnLevelChanged != null) OnLevelChanged(Faction.Navy, pn, n); Debug.Log("[Bounty] 관군 현상금 " + navyBounty.ToString("F0") + " → " + n + "단계"); }
        if (j != pj) { if (OnLevelChanged != null) OnLevelChanged(Faction.Japanese, pj, j); Debug.Log("[Bounty] 일본 현상금 " + japanBounty.ToString("F0") + " → " + j + "단계"); }
    }

    /// <summary>이 진영이 플레이어를 공격하는가(= 플레이어와 적대인가).</summary>
    public static bool PlayerHostileWith(Faction f)
    {
        if (f == Faction.Pirate) return false;
        if (f == Faction.Wokou) return true;
        if (Instance == null) return true; // 현상금 시스템이 없는 씬: 예전 규칙
        return f == Faction.Navy ? Instance.navyHostile : Instance.japaneseHostile;
    }

    /// <summary>진영 현상금 단계(0~5). 관군·일본 말고는 0.</summary>
    public static int Level(Faction f)
    {
        if (Instance == null) return 0;
        return f == Faction.Navy ? Instance.navyLevel : f == Faction.Japanese ? Instance.japanLevel : 0;
    }

    /// <summary>진영 현상금 점수(0~100).</summary>
    public float Bounty(Faction f) { return f == Faction.Navy ? navyBounty : f == Faction.Japanese ? japanBounty : 0f; }

    /// <summary>지금 단계의 문턱 ~ 다음 단계 문턱 사이에서 얼마나 왔는지(0~1, 아이콘 아래 막대용). 5단계면 1.</summary>
    public float ProgressToNext(Faction f)
    {
        float p = Bounty(f); int lv = f == Faction.Navy ? navyLevel : japanLevel;
        if (lv >= MaxLevel || levelThresholds == null || lv >= levelThresholds.Length) return 1f;
        float lo = lv == 0 ? 0f : levelThresholds[lv - 1], hi = levelThresholds[lv];
        return Mathf.Clamp01(Mathf.InverseLerp(lo, hi, p));
    }

    /// <summary>플레이어가 victim 진영 배를 때렸다(ShipHealth 가 부름). sunk = 이 한 방으로 격침.</summary>
    public static void ReportPlayerAttack(Faction victim, bool sunk, bool merchant)
    {
        if (Instance == null || victim == Faction.Pirate) return;
        Instance.Apply(victim, sunk, merchant);
    }

    void Apply(Faction victim, bool sunk, bool merchant)
    {
        if (victim == Faction.Wokou) { if (sunk) wokouSunkByPlayer++; return; }
        float amount = perHit;
        if (sunk)
        {
            amount += merchant ? perMerchantSunk : perWarshipSunk;
            if (merchant) merchantsPlundered++;
            else if (victim == Faction.Navy) navySunkByPlayer++;
            else japaneseSunkByPlayer++;
        }
        // 친 진영은 오르고, 그 반대 진영은 내린다
        if (victim == Faction.Navy) { navyBounty += amount; japanBounty -= amount * otherSideRelief; }
        else { japanBounty += amount; navyBounty -= amount * otherSideRelief; }
        Refresh();
    }

    /// <summary>현상금을 직접 정한다(퀘스트·디버그용, 점 0~100).</summary>
    public void SetBounty(Faction f, float points)
    {
        if (f == Faction.Navy) navyBounty = points; else if (f == Faction.Japanese) japanBounty = points; else return;
        Refresh();
    }

    /// <summary>현상금을 단계로 정한다(그 단계의 문턱 점수로). 0 이면 0 점.</summary>
    public void SetLevel(Faction f, int level)
    {
        level = Mathf.Clamp(level, 0, MaxLevel);
        SetBounty(f, level == 0 || levelThresholds == null || levelThresholds.Length < level ? 0f : levelThresholds[level - 1]);
    }

    /// <summary>현상금을 더한다(음수면 뺌).</summary>
    public void AddBounty(Faction f, float points)
    {
        if (f == Faction.Navy) navyBounty += points; else if (f == Faction.Japanese) japanBounty += points; else return;
        Refresh();
    }
}
