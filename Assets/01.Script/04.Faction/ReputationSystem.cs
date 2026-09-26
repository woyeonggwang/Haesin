using UnityEngine;

/// <summary>
/// 우호도(예전 현상금 시스템을 대신함). 게이지 하나로 관군 ↔ 일본군 사이 플레이어의 위치를 나타낸다.
///
///   standing −100 = 관군 쪽 끝 … 0 = 중립 … +100 = 일본 쪽 끝
///   - 관군 배(판옥선·상선)를 때리거나 격침하면 일본 쪽(+)으로, 일본 배를 때리거나 격침하면 관군 쪽(−)으로 움직인다.
///     (사용자 지정: 한쪽 우호도가 오르면 다른 쪽은 내려감 = 게이지 하나)
///   - standing ≥ +hostileThreshold 이면 관군이, standing ≤ −hostileThreshold 이면 일본군이 플레이어를 공격한다.
///     그 사이(중립 구간)에서는 둘 다 공격하지 않는다. 관군과 일본군끼리는 늘 적이다(ShipFaction.IsHostileTo).
///   - 가만히 있으면 decayPerSecond 로 천천히 0(중립)으로 돌아온다(0 이면 돌아오지 않음).
/// 씬에 하나. 없으면 예전처럼 모든 진영이 플레이어의 적이다.
/// </summary>
[DisallowMultipleComponent]
public class ReputationSystem : MonoBehaviour
{
    public static ReputationSystem Instance { get; private set; }

    [Header("게이지")]
    [Tooltip("−100 관군 쪽 … 0 중립 … +100 일본 쪽.")]
    [Range(-100f, 100f)] public float standing = 0f;
    [Tooltip("이 값 이상 한쪽으로 기울면 반대 진영이 공격한다.")]
    public float hostileThreshold = 25f;
    [Tooltip("초당 중립(0)으로 돌아오는 양. 0 이면 돌아오지 않는다.")]
    public float decayPerSecond = 0.1f;

    [Header("변화량")]
    [Tooltip("포탄 한 발을 맞힐 때.")] public float perHit = 2f;
    [Tooltip("군선(판옥선·안택선·세키부네)을 격침할 때 추가로.")] public float perWarshipSunk = 15f;
    [Tooltip("상선을 격침(약탈)할 때 추가로.")] public float perMerchantSunk = 10f;

    [Header("상태 (읽기 전용)")]
    public bool navyHostile;
    public bool japaneseHostile;
    public int navySunkByPlayer, japaneseSunkByPlayer, merchantsPlundered;

    /// <summary>게이지가 바뀔 때. (standing)</summary>
    public System.Action<float> OnChanged;

    void Awake() { if (Instance == null) Instance = this; Refresh(); }
    void OnDestroy() { if (Instance == this) Instance = null; }

    void Update()
    {
        if (decayPerSecond > 0f && standing != 0f)
        {
            standing = Mathf.MoveTowards(standing, 0f, decayPerSecond * Time.deltaTime);
            Refresh();
        }
    }

    void Refresh()
    {
        standing = Mathf.Clamp(standing, -100f, 100f);
        bool n = standing >= hostileThreshold, j = standing <= -hostileThreshold;
        bool changed = n != navyHostile || j != japaneseHostile;
        navyHostile = n; japaneseHostile = j;
        if (OnChanged != null) OnChanged(standing);
        if (changed) Debug.Log("[Reputation] " + standing.ToString("F0") + " 관군 적대 " + navyHostile + " / 일본 적대 " + japaneseHostile);
    }

    /// <summary>이 진영이 플레이어를 공격하는가(= 플레이어와 적대인가).</summary>
    public static bool PlayerHostileWith(Faction f)
    {
        if (f == Faction.Pirate) return false;
        if (Instance == null) return true; // 우호도 시스템이 없는 씬: 예전 규칙
        return f == Faction.Navy ? Instance.navyHostile : Instance.japaneseHostile;
    }

    /// <summary>플레이어가 victim 진영 배를 때렸다(ShipHealth 가 부름). sunk = 이 한 방으로 격침.</summary>
    public static void ReportPlayerAttack(Faction victim, bool sunk, bool merchant)
    {
        if (Instance == null || victim == Faction.Pirate) return;
        Instance.Apply(victim, sunk, merchant);
    }

    void Apply(Faction victim, bool sunk, bool merchant)
    {
        float amount = perHit;
        if (sunk)
        {
            amount += merchant ? perMerchantSunk : perWarshipSunk;
            if (merchant) merchantsPlundered++;
            else if (victim == Faction.Navy) navySunkByPlayer++;
            else japaneseSunkByPlayer++;
        }
        // 관군을 치면 일본 쪽(+), 일본을 치면 관군 쪽(−)
        standing += victim == Faction.Navy ? amount : -amount;
        Refresh();
    }

    /// <summary>게이지를 직접 정한다(디버그·퀘스트용).</summary>
    public void SetStanding(float v) { standing = v; Refresh(); }
}
