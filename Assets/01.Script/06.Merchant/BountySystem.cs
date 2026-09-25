using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 현상금. 플레이어가 상선을 약탈할 때마다 한 점씩 붙고, 점수만큼 관군 함대가 더 나온다.
///
/// 한 번의 약탈은 bountyDuration 초 뒤에 저절로 지워진다. 그래서 연달아 털면
/// 관군이 겹겹이 몰려오고, 한동안 얌전히 있으면 수배가 풀린다.
///
/// 왜군이 상선을 털어도 상선은 가라앉지만, 그건 플레이어의 죄가 아니므로
/// 현상금은 붙지 않는다. (관군이 왜군을 쫓는 것은 진영 규칙이 알아서 한다)
/// </summary>
[DisallowMultipleComponent]
public class BountySystem : MonoBehaviour
{
    public static BountySystem Instance { get; private set; }

    [Header("연결")]
    [Tooltip("현상금만큼 관군 함대를 늘릴 스포너. 비우면 씬에서 찾는다.")]
    public EnemyFleetSpawner spawner;

    [Header("현상금")]
    [Tooltip("약탈 한 번이 유지되는 시간(초).")]
    public float bountyDuration = 120f;
    [Tooltip("약탈 한 번에 붙는 점수.")]
    public int bountyPerPlunder = 1;
    [Tooltip("현상금으로 늘릴 수 있는 관군 함대 수의 최대치.")]
    public int maxBounty = 5;

    [Header("관군 함대")]
    [Tooltip("현상금이 0일 때의 관군 함대 수. 시작할 때 스포너 값에서 자동으로 읽는다.")]
    public int baseNavyFleets = 3;
    [Tooltip("현상금이 아무리 높아도 이 이상은 나오지 않는다.")]
    public int hardMaxNavyFleets = 8;

    [Header("상태 (읽기 전용)")]
    [Tooltip("지금 걸려 있는 현상금.")]
    public int bounty;
    [Tooltip("지금 관군 함대 상한.")]
    public int navyFleetLimit;
    [Tooltip("가장 오래된 현상금이 풀리기까지 남은 시간(초).")]
    public float nextExpiresIn;
    [Tooltip("플레이어가 지금까지 약탈한 상선 수.")]
    public int plunderedByPlayer;
    [Tooltip("왜군이 털어간 상선 수.")]
    public int plunderedByJapanese;

    /// <summary>현상금이 바뀔 때. (현재 현상금, 관군 함대 상한)</summary>
    public System.Action<int, int> OnBountyChanged;

    private readonly List<float> _expiry = new List<float>();

    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        if (spawner == null) spawner = FindFirstObjectByType<EnemyFleetSpawner>();
        if (spawner != null) baseNavyFleets = spawner.maxNavyFleets;
        Apply();
    }

    /// <summary>상선이 약탈당했음을 알린다. 어디서든 부를 수 있게 정적으로 열어 둔다.</summary>
    public static void ReportPlunder(Faction raider, bool raiderKnown, Vector3 where)
    {
        if (Instance != null) Instance.Report(raider, raiderKnown, where);
    }

    void Report(Faction raider, bool raiderKnown, Vector3 where)
    {
        if (!raiderKnown)
        {
            // 누가 털었는지 모르면(충돌로 침몰 등) 아무 일도 없던 것으로 둔다.
            return;
        }

        if (raider == Faction.Japanese) { plunderedByJapanese++; return; }
        if (raider != Faction.Pirate) return;   // 관군이 자기편 상선을 침몰시킨 경우

        plunderedByPlayer++;
        for (int i = 0; i < Mathf.Max(1, bountyPerPlunder); i++)
            _expiry.Add(Time.time + bountyDuration);

        Apply();
    }

    void Update()
    {
        bool changed = false;
        for (int i = _expiry.Count - 1; i >= 0; i--)
        {
            if (Time.time >= _expiry[i]) { _expiry.RemoveAt(i); changed = true; }
        }
        if (changed) Apply();

        // 가장 먼저 풀리는 것까지 남은 시간
        if (_expiry.Count == 0) nextExpiresIn = 0f;
        else
        {
            float soonest = float.MaxValue;
            for (int i = 0; i < _expiry.Count; i++) soonest = Mathf.Min(soonest, _expiry[i]);
            nextExpiresIn = Mathf.Max(0f, soonest - Time.time);
        }
    }

    void Apply()
    {
        bounty = Mathf.Min(_expiry.Count, Mathf.Max(0, maxBounty));
        navyFleetLimit = Mathf.Clamp(baseNavyFleets + bounty, baseNavyFleets, hardMaxNavyFleets);

        if (spawner != null) spawner.maxNavyFleets = navyFleetLimit;
        if (OnBountyChanged != null) OnBountyChanged(bounty, navyFleetLimit);
    }

    /// <summary>수배를 즉시 푼다. (디버그·아이템용)</summary>
    public void ClearBounty()
    {
        _expiry.Clear();
        Apply();
    }
}
