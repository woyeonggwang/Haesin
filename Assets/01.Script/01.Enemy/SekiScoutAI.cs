using UnityEngine;

/// <summary>
/// 세키부네(정찰선) AI. 무기가 없는 대신 눈이 좋다.
///
/// 상태 기계:
///   Patrol - 시작 지점 주변을 천천히 항해.
///   Shadow - 플레이어를 발견하면 안전 거리(standOffDistance)를 유지하며 따라붙고,
///            일정 간격으로 주변 안택선(EnemyAI)에게 경보를 보내 몰려오게 한다.
///            플레이어가 너무 다가오면 전속으로 거리를 벌린다.
/// </summary>
public class SekiScoutAI : EnemyShipBase
{
    public enum State { Patrol, Shadow }

    [Header("탐지 (정찰선은 눈이 좋다)")]
    [Tooltip("이 거리 안에 플레이어가 들어오면 미행을 시작한다(m).")]
    public float detectionRange = 130f;
    [Tooltip("플레이어가 이 거리보다 멀어지면 놓친 것으로 보고 순찰로 돌아간다(m).")]
    public float loseRange = 180f;

    [Header("미행")]
    [Tooltip("플레이어와 유지하려는 안전 거리(m). 이보다 가까우면 도망친다.")]
    public float standOffDistance = 55f;
    [Tooltip("안전 거리의 이 배율보다 멀어지면 다시 다가간다.")]
    public float approachFactor = 1.4f;

    [Header("경보")]
    [Tooltip("이 반지름 안의 안택선들에게 경보를 보낸다(m).")]
    public float alertRadius = 350f;
    [Tooltip("경보를 다시 보내는 간격(초).")]
    public float alertInterval = 4f;
    [Tooltip("경보 한 번으로 안택선이 추격을 유지하는 시간(초).")]
    public float alertDuration = 25f;

    [Header("상태 (읽기 전용)")]
    public State state = State.Patrol;
    public int lastAlertCount;

    private float _nextAlertTime;

    void OnValidate()
    {
        loseRange = Mathf.Max(loseRange, detectionRange * 1.2f);
    }

    protected override void TickBehaviour()
    {
        UpdateState();

        switch (state)
        {
            case State.Patrol: TickPatrol(); break;
            case State.Shadow: TickShadow(); break;
        }
    }

    void UpdateState()
    {
        if (_player == null) { state = State.Patrol; return; }

        switch (state)
        {
            case State.Patrol:
                if (distanceToPlayer <= detectionRange)
                {
                    state = State.Shadow;
                    _nextAlertTime = Time.time; // 발견 즉시 첫 경보
                }
                break;
            case State.Shadow:
                if (distanceToPlayer > loseRange)
                {
                    state = State.Patrol;
                    PickPatrolTarget();
                }
                break;
        }
    }

    void TickShadow()
    {
        // 주기적으로 주변 안택선에게 경보
        if (Time.time >= _nextAlertTime)
        {
            _nextAlertTime = Time.time + alertInterval;
            SendAlert();
        }

        Vector3 away = transform.position - _player.position;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = FlatForward();
        away.Normalize();

        if (distanceToPlayer < standOffDistance)
        {
            // 너무 가깝다 - 전속으로 도망
            SailTowards(transform.position + away * 60f, maxSpeed);
        }
        else if (distanceToPlayer > standOffDistance * approachFactor)
        {
            // 너무 멀다 - 다시 다가간다
            SailTowards(_player.position, maxSpeed);
        }
        else
        {
            // 적당한 거리 - 플레이어 주위를 크게 돌며 시야를 유지한다
            Vector3 tangent = Vector3.Cross(Vector3.up, away); // 궤도 방향
            Vector3 orbitPoint = _player.position + away * standOffDistance + tangent * 25f;
            SailTowards(orbitPoint, patrolSpeed * 1.6f);
        }
    }

    void SendAlert()
    {
        EnemyAI[] fighters = Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        int count = 0;
        for (int i = 0; i < fighters.Length; i++)
        {
            float d = Vector3.Distance(transform.position, fighters[i].transform.position);
            if (d <= alertRadius)
            {
                fighters[i].Alert(alertDuration);
                count++;
            }
        }
        lastAlertCount = count;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 1f, 0.4f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, standOffDistance);
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, alertRadius);
    }
}
