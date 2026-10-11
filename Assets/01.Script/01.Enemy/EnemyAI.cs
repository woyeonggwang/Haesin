using UnityEngine;

/// <summary>
/// 안택선(전투함) AI. 항해 물리는 EnemyShipBase 가 담당한다.
///
/// 상태 기계:
///   Patrol    - 시작 지점 주변을 천천히 항해.
///   Chase     - 플레이어를 발견하거나 정찰선(세키부네)의 경보를 받으면 전속 추격.
///   Broadside - 사거리 안에서 현측(포문)을 플레이어에게 돌린 채 천천히 주위를 돈다.
///               이 상태에서 EnemyCannonControl 이 포격을 한다.
/// 탐지/이탈 거리에 히스테리시스를 두어 경계에서 상태가 떨리지 않는다.
/// 2026-10-06(사용자 지적 "멈춘 배 주위를 돌며 멀어졌다 가까워져서야 공격 — 가까우면 측면을 보이며 공격"): Broadside 는 표적 둘레를 돌지 않고
/// standoffDistance 근처에 멈춰 현측을 표적에 돌린 채 쏜다. 거리 오차가 holdBand 밖일 때만 표적 쪽(멀면)/바깥쪽(가까우면)으로 기울여 천천히 움직인다.
/// </summary>
public class EnemyAI : EnemyShipBase
{
    public enum State { Patrol, Chase, Broadside }

    [Header("탐지")]
    [Tooltip("이 거리 안에 플레이어가 들어오면 추격을 시작한다(m).")]
    public float detectionRange = 30f;
    [Tooltip("추격 중 플레이어가 이 거리보다 멀어지면 포기하고 순찰로 돌아간다(m). detectionRange 보다 크게.")]
    public float loseRange = 45f;
    [Tooltip("이 거리 안이면 현측을 돌리고 교전 자세를 잡는다(m).")]
    public float attackRange = 10f;
    [Tooltip("교전 중 플레이어가 이 거리보다 멀어지면 다시 추격한다(m). attackRange 보다 크게.")]
    public float breakOffRange = 15f;

    [Header("교전")]
    [Tooltip("교전(현측 유지) 중 유지 속도(m/s). 플레이어 주위를 천천히 돈다.")]
    public float broadsideSpeed = 1.5f;
    [Tooltip("교전 중 머물 거리(m). 0 이면 attackRange × 0.75.")]
    public float standoffDistance = 0f;
    [Tooltip("머물 거리에서 이만큼(m) 안이면 멈춰 현측만 유지한다.")]
    public float holdBand = 12f;

    [Header("상태 (읽기 전용)")]
    public State state = State.Patrol;
    [Tooltip("정찰선 경보가 유지되는 동안 true.")]
    public bool alerted;

    private float _alertUntil = -999f;

    void OnValidate()
    {
        // 히스테리시스 거리가 기준 거리보다 작으면 경계에서 상태가 떨린다. 항상 더 크게 유지.
        loseRange = Mathf.Max(loseRange, detectionRange * 1.2f);
        breakOffRange = Mathf.Max(breakOffRange, attackRange * 1.2f);
    }

    /// <summary>정찰선 등 외부에서 경보를 보내 추격을 강제한다. duration 동안 거리와 무관하게 쫓는다.</summary>
    public void Alert(float duration)
    {
        _alertUntil = Mathf.Max(_alertUntil, Time.time + duration);
        if (state == State.Patrol) state = State.Chase;
    }

    protected override void TickBehaviour()
    {
        alerted = Time.time < _alertUntil;
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
        if (_player == null) { state = State.Patrol; return; }

        switch (state)
        {
            case State.Patrol:
                if (distanceToPlayer <= detectionRange || alerted) state = State.Chase;
                break;
            case State.Chase:
                if (distanceToPlayer <= attackRange) state = State.Broadside;
                else if (distanceToPlayer > loseRange && !alerted)
                {
                    state = State.Patrol;
                    PickPatrolTarget();
                }
                break;
            case State.Broadside:
                if (distanceToPlayer > breakOffRange) state = State.Chase;
                break;
        }
    }

    void TickChase()
    {
        // 플레이어의 현재 속도로 조금 앞을 예측해 요격 지점으로 향한다.
        Vector3 aim = _player.position;
        if (_playerRb != null)
        {
            float lead = Mathf.Clamp(distanceToPlayer / Mathf.Max(maxSpeed, 1f), 0f, 2.5f);
            Vector3 pv = _playerRb.linearVelocity;
            pv.y = 0f;
            aim += pv * lead;
        }
        SailTowards(aim, maxSpeed);
    }

    void TickBroadside()
    {
        Vector3 toPlayer = _player.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.01f) return;
        toPlayer.Normalize();

        // 좌현/우현 중 덜 돌아도 되는 쪽을 향한다.
        Vector3 fwd = FlatForward();
        Vector3 tangentA = Vector3.Cross(Vector3.up, toPlayer);   // 플레이어를 오른쪽에 두는 방향
        Vector3 tangentB = -tangentA;                              // 왼쪽에 두는 방향
        Vector3 desired = Vector3.Dot(fwd, tangentA) >= Vector3.Dot(fwd, tangentB) ? tangentA : tangentB;

        // 거리 오차: 멀면 표적 쪽으로, 가까우면 바깥쪽으로 침로를 최대 35° 기울임. 오차가 holdBand 안이면 멈춰 현측만 유지.
        float standoff = standoffDistance > 0f ? standoffDistance : attackRange * 0.75f;
        float err = distanceToPlayer - standoff;
        bool hold = Mathf.Abs(err) <= holdBand;
        if (!hold)
        {
            float bend = Mathf.Clamp(err / 20f, -1f, 1f) * 35f;
            float side = Mathf.Sign(Vector3.SignedAngle(desired, toPlayer, Vector3.up)); // + 면 표적 쪽으로 돌리는 방향 // desired 를 표적 쪽으로 돌리는 부호
            desired = Quaternion.AngleAxis(side * bend, Vector3.up) * desired;
        }

        float angle = Vector3.SignedAngle(fwd, desired, Vector3.up);
        ApplySteer(angle);

        float align = Mathf.Clamp01(1f - Mathf.Abs(angle) / 90f);
        if (!hold) ApplyThrust(broadsideSpeed * align);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Vector3 center = Application.isPlaying ? _anchor : transform.position;
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.5f);
        Gizmos.DrawWireSphere(center, patrolRadius);

        if (Application.isPlaying && state == State.Patrol)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, _patrolTarget);
            Gizmos.DrawWireSphere(_patrolTarget, 1.5f);
        }
    }
}

