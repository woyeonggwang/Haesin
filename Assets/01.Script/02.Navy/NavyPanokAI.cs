using UnityEngine;

/// <summary>
/// 관군 판옥선 AI. 항해 물리는 NavyShipBase 가 담당한다.
///
/// 상태 기계는 안택선(EnemyAI)과 같은 뼈대를 쓰되, 표적이 플레이어로 고정되어 있지 않다.
///   Patrol    - 초계 구역을 천천히 항해.
///   Chase     - 적(해적 또는 왜선)을 발견하면 요격 지점으로 전속 항해.
///   Broadside - 사거리 안에서 현측을 표적 쪽으로 돌린 채 천천히 주위를 돈다.
///               이 상태에서 NavyCannonControl 이 포격을 한다.
///
/// 탐지/이탈 거리에 히스테리시스를 두어 경계에서 상태가 떨리지 않는다.
/// </summary>
public class NavyPanokAI : NavyShipBase
{
    public enum State { Patrol, Chase, Broadside }

    [Header("탐지")]
    [Tooltip("이 거리 안에 적이 들어오면 추격을 시작한다(m). 관군 주력함이라 시야가 넓다.")]
    public float detectionRange = 160f;
    [Tooltip("추격 중 적이 이 거리보다 멀어지면 포기하고 초계로 돌아간다(m).")]
    public float loseRange = 220f;
    [Tooltip("이 거리 안이면 현측을 돌리고 교전 자세를 잡는다(m).")]
    public float attackRange = 70f;
    [Tooltip("교전 중 적이 이 거리보다 멀어지면 다시 추격한다(m).")]
    public float breakOffRange = 95f;

    [Header("교전")]
    [Tooltip("교전(현측 유지) 중 유지 속도(m/s). 표적 주위를 천천히 돈다.")]
    public float broadsideSpeed = 1.8f;
    [Tooltip("표적에게 이보다 가까워지면 붙지 않고 거리를 벌린다(m). 판옥선은 접현전을 피한다.")]
    public float keepAwayDistance = 28f;

    [Header("아군 지원")]
    [Tooltip("교전을 시작하면 이 반지름 안의 아군 관군함을 불러 모은다(m).")]
    public float callRadius = 300f;
    [Tooltip("지원 요청을 다시 보내는 간격(초).")]
    public float callInterval = 5f;
    [Tooltip("지원 요청 한 번으로 아군이 달려오는 시간(초).")]
    public float callDuration = 20f;

    [Header("상태 (읽기 전용)")]
    public State state = State.Patrol;
    [Tooltip("아군의 지원 요청을 받아 달려가는 중이면 true.")]
    public bool responding;

    private float _respondUntil = -999f;
    private float _nextCallTime;
    private Vector3 _rallyPoint;

    void OnValidate()
    {
        loseRange = Mathf.Max(loseRange, detectionRange * 1.2f);
        breakOffRange = Mathf.Max(breakOffRange, attackRange * 1.2f);
    }

    /// <summary>아군 관군함이 교전 중임을 알린다. duration 동안 그 지점으로 달려간다.</summary>
    public void CallToBattle(Vector3 point, float duration)
    {
        _rallyPoint = point;
        _respondUntil = Mathf.Max(_respondUntil, Time.time + duration);
        if (state == State.Patrol) state = State.Chase;
    }

    protected override void TickBehaviour()
    {
        responding = Time.time < _respondUntil;
        UpdateState();

        switch (state)
        {
            case State.Patrol: TickPatrol(); break;
            case State.Chase: TickChase(); break;
            case State.Broadside: TickBroadside(); break;
        }
    }

    void UpdateState()
    {
        if (_target == null)
        {
            // 표적이 없어도 지원 요청이 살아 있으면 집결 지점으로 계속 간다.
            state = responding ? State.Chase : State.Patrol;
            return;
        }

        switch (state)
        {
            case State.Patrol:
                if (distanceToTarget <= detectionRange || responding) state = State.Chase;
                break;

            case State.Chase:
                if (distanceToTarget <= attackRange) state = State.Broadside;
                else if (distanceToTarget > loseRange && !responding)
                {
                    state = State.Patrol;
                    PickPatrolTarget();
                }
                break;

            case State.Broadside:
                if (distanceToTarget > breakOffRange) state = State.Chase;
                break;
        }
    }

    void TickChase()
    {
        if (_target == null)
        {
            // 지원 요청만 받은 상태 - 집결 지점으로.
            if (responding) SailTowards(_rallyPoint, maxSpeed);
            return;
        }

        // 표적의 현재 속도로 조금 앞을 예측해 요격 지점으로 향한다.
        Vector3 aim = _target.transform.position;
        Rigidbody trb = _target.Body;
        if (trb != null)
        {
            float lead = Mathf.Clamp(distanceToTarget / Mathf.Max(maxSpeed, 1f), 0f, 2.5f);
            Vector3 pv = trb.linearVelocity;
            pv.y = 0f;
            aim += pv * lead;
        }
        SailTowards(aim, maxSpeed);
    }

    void TickBroadside()
    {
        if (_target == null) return;

        // 교전을 시작했으면 주변 아군을 부른다.
        if (Time.time >= _nextCallTime)
        {
            _nextCallTime = Time.time + callInterval;
            CallNearbyAllies();
        }

        Vector3 toTarget = _target.transform.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.01f) return;
        toTarget.Normalize();

        // 너무 붙었으면 현측을 유지한 채 바깥쪽으로 빠진다.
        if (distanceToTarget < keepAwayDistance)
        {
            SailTowards(transform.position - toTarget * 50f, broadsideSpeed * 2f);
            return;
        }

        // 좌현/우현 중 덜 돌아도 되는 쪽을 표적에게 내민다.
        Vector3 fwd = FlatForward();
        Vector3 tangentA = Vector3.Cross(Vector3.up, toTarget);
        Vector3 tangentB = -tangentA;
        Vector3 desired = Vector3.Dot(fwd, tangentA) >= Vector3.Dot(fwd, tangentB) ? tangentA : tangentB;

        float angle = Vector3.SignedAngle(fwd, desired, Vector3.up);
        ApplySteer(angle);

        // 현측을 유지한 채 천천히 전진 - 표적 주위를 도는 움직임이 된다.
        float align = Mathf.Clamp01(1f - Mathf.Abs(angle) / 90f);
        ApplyThrust(broadsideSpeed * align);
    }

    void CallNearbyAllies()
    {
        NavyPanokAI[] allies = Object.FindObjectsByType<NavyPanokAI>(FindObjectsSortMode.None);
        for (int i = 0; i < allies.Length; i++)
        {
            if (allies[i] == this) continue;
            if (Vector3.Distance(transform.position, allies[i].transform.position) > callRadius) continue;
            allies[i].CallToBattle(transform.position, callDuration);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = new Color(1f, 0.9f, 0.3f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, keepAwayDistance);

        Vector3 center = Application.isPlaying ? _anchor : transform.position;
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.4f);
        Gizmos.DrawWireSphere(center, patrolRadius);

        if (Application.isPlaying && _target != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, _target.transform.position);
        }
    }
}
