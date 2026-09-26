using UnityEngine;

/// <summary>
/// 상선 AI. 관군과 같은 진영이지만 대포가 없다. 싸우지 않고 도망만 친다.
///
/// 항해 물리는 NavyShipBase 를 그대로 쓴다. 관군과 같은 편이므로 표적 탐색
/// (가장 가까운 적대 진영 찾기)도 그대로 쓰이는데, 상선은 그 표적을 쫓는 대신
/// 반대로 달아나는 데 쓴다.
///
/// 호송대: 여러 척이 몰려다니도록 첫 배가 선도선이 되고 나머지는 그 옆을 따라간다.
/// 적이 가까이 붙으면 각자 알아서 흩어져 도망친다.
/// </summary>
public class MerchantAI : NavyShipBase
{
    public enum State { Sail, Follow, Flee }

    [Header("도주")]
    [Tooltip("적이 이 거리 안에 들어오면 도망친다(m).")]
    public float fleeRange = 140f;
    [Tooltip("이 거리보다 멀어지면 다시 평소 항해로 돌아간다(m). fleeRange 보다 크게.")]
    public float calmRange = 200f;
    [Tooltip("도망칠 때 내는 속도 배수. 짐을 실어 무거우므로 1보다 조금 크게 쳐 준다.")]
    public float fleeSpeedMultiplier = 1.15f;

    [Header("호송대")]
    [Tooltip("따라갈 선도선. 비어 있으면 이 배가 선도선이다.")]
    public MerchantAI convoyLeader;
    [Tooltip("선도선 기준 대열 위치(오른쪽, 뒤쪽 미터).")]
    public Vector2 formationOffset = Vector2.zero;
    [Tooltip("대열 위치에 이만큼 가까우면 다 따라잡은 것으로 본다(m).")]
    public float formationArrive = 12f;

    [Header("상태 (읽기 전용)")]
    public State state = State.Sail;
    [Tooltip("이 상선이 이미 약탈당했는지.")]
    public bool plundered;

    private ShipHealth _health;

    void OnValidate()
    {
        calmRange = Mathf.Max(calmRange, fleeRange * 1.2f);
    }

    protected override void Awake()
    {
        base.Awake();
        _health = GetComponent<ShipHealth>();
    }

    void OnEnable()
    {
        if (_health != null) _health.OnDied += OnDied;
    }

    void OnDisable()
    {
        if (_health != null) _health.OnDied -= OnDied;
    }

    /// <summary>
    /// 격침 = 약탈. 우호도 변화는 ShipHealth.TakeDamage 가 처리하고 여기서는 기록만 남긴다.
    /// 플레이어(해적)가 털었으면 수배가 붙고, 왜군이 털었으면 기록만 남는다.
    /// </summary>
    void OnDied()
    {
        if (plundered) return;
        plundered = true;

        Faction raider = _health != null ? _health.lastAttackerFaction : Faction.Pirate;
        bool known = _health != null && _health.hasLastAttacker;
        // 예전 현상금 시스템 대신 우호도: ShipHealth.TakeDamage 가 플레이어의 격침을 ReputationSystem 에 알린다.
        if (known && raider == Faction.Pirate) Debug.Log("[Merchant] 플레이어가 상선을 약탈");
    }

    protected override void TickBehaviour()
    {
        UpdateState();

        switch (state)
        {
            case State.Flee: TickFlee(); break;
            case State.Follow: TickFollow(); break;
            default: TickPatrol(); break;
        }
    }

    void UpdateState()
    {
        bool leaderAlive = convoyLeader != null && convoyLeader.isActiveAndEnabled && !convoyLeader.plundered;

        // 적이 붙었는지부터 본다. 대열이고 뭐고 일단 도망친다.
        if (_target != null)
        {
            if (state == State.Flee)
            {
                if (distanceToTarget > calmRange) state = leaderAlive ? State.Follow : State.Sail;
                return;
            }
            if (distanceToTarget <= fleeRange) { state = State.Flee; return; }
        }
        else if (state == State.Flee)
        {
            state = leaderAlive ? State.Follow : State.Sail;
            return;
        }

        state = leaderAlive ? State.Follow : State.Sail;
    }

    void TickFlee()
    {
        if (_target == null) return;

        Vector3 away = transform.position - _target.transform.position;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = FlatForward();
        away.Normalize();

        // 정면으로만 달아나면 벽에 박히듯 뻔해진다. 살짝 비스듬히 튼다.
        Vector3 side = Vector3.Cross(Vector3.up, away);
        Vector3 heading = (away + side * 0.25f).normalized;

        SailTowards(transform.position + heading * 120f, maxSpeed * fleeSpeedMultiplier);
    }

    void TickFollow()
    {
        Vector3 lead = convoyLeader.transform.position;
        Vector3 fwd = convoyLeader.FlatForwardPublic();
        Vector3 right = Vector3.Cross(Vector3.up, fwd);

        // 선도선 기준으로 오른쪽·뒤쪽 만큼 떨어진 자리
        Vector3 slot = lead + right * formationOffset.x - fwd * formationOffset.y;

        float d = Vector3.Distance(transform.position, slot);
        if (d <= formationArrive)
        {
            // 자리에 붙었으면 선도선과 같은 속도로만 간다
            ApplySteer(Vector3.SignedAngle(FlatForward(), fwd, Vector3.up));
            ApplyThrust(Mathf.Max(0f, convoyLeader.currentSpeed));
            return;
        }

        // 뒤처졌으면 조금 더 내서 따라붙는다
        float cap = Mathf.Min(maxSpeed, Mathf.Max(patrolSpeed, convoyLeader.currentSpeed + 1.5f));
        SailTowards(slot, cap);
    }

    /// <summary>대열 계산을 위해 밖에서도 뱃머리 방향을 볼 수 있게 열어 둔다.</summary>
    public Vector3 FlatForwardPublic() { return FlatForward(); }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.7f);
        Gizmos.DrawWireSphere(transform.position, fleeRange);
        if (convoyLeader != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, convoyLeader.transform.position);
        }
    }
}
