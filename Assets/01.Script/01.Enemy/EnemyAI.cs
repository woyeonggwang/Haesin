using UnityEngine;

/// <summary>
/// 힘 기반 적함 AI.
///
/// 예전 버전은 transform.Translate 로 배를 직접 옮겼기 때문에 Rigidbody 속도가 항상 0 이었고,
/// 판옥선으로 들이받아도 적함이 밀려나지 않았다. 이제는 MoveControl 과 같은 방식으로
/// 추진력·조타 토크를 Rigidbody 에 넣어 물리로 움직인다. 당파 시 질량 차이만큼 밀려난다.
///
/// 상태 기계:
///   Patrol    - 시작 지점 주변 patrolRadius 안에서 무작위 목적지를 골라 천천히 항해.
///   Chase     - 플레이어를 발견하면 예측 지점(리드)을 향해 전속 추격.
///   Broadside - 사거리 안에 들어오면 속도를 줄이고 현측(포문)을 플레이어에게 돌린 채
///               천천히 주위를 돈다.
/// 탐지/이탈 거리에 히스테리시스를 두어 경계에서 상태가 떨리지 않는다.
///
/// 축 분담은 MoveControl 과 동일: 수평 이동과 Y축 회전만 담당하고,
/// 높이·기울기는 ShipBuoyancy 가 부력(힘)으로 처리한다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class EnemyAI : MonoBehaviour
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

    [Header("항해 성능")]
    [Tooltip("추격 시 최고 속도(m/s).")]
    public float maxSpeed = 7f;
    [Tooltip("순찰 시 순항 속도(m/s).")]
    public float patrolSpeed = 2.5f;
    [Tooltip("교전(현측 유지) 중 유지 속도(m/s). 플레이어 주위를 천천히 돈다.")]
    public float broadsideSpeed = 1.5f;
    [Tooltip("전진 가속도(m/s^2).")]
    public float forwardAccel = 5f;
    [Tooltip("선회 가속도(도/s^2).")]
    public float turnAccel = 60f;
    [Tooltip("최대 선회 속도(도/s).")]
    public float maxTurnRate = 22f;
    [Tooltip("목표가 이 각도 이내면 방향타를 비례로 줄인다(도). 작을수록 급하게 튼다.")]
    public float steerSoftAngle = 25f;

    [Header("물의 저항")]
    [Tooltip("전후 방향 감속. 클수록 빨리 멈춘다.")]
    public float linearDrag = 0.6f;
    [Tooltip("선회 감속.")]
    public float angularDrag = 2.2f;
    [Tooltip("옆미끄러짐 억제. 당파로 밀려난 뒤 서서히 잡아준다.")]
    public float lateralGrip = 3f;

    [Header("순찰")]
    [Tooltip("시작 지점을 중심으로 이 반지름 안에서 순찰한다(m).")]
    public float patrolRadius = 60f;
    [Tooltip("목적지에 이 거리까지 가까워지면 도착으로 본다(m).")]
    public float arriveDistance = 8f;
    [Tooltip("도착 후 다음 목적지로 떠나기 전 대기 시간 범위(초).")]
    public Vector2 waitRange = new Vector2(2f, 6f);

    [Header("상태 (읽기 전용)")]
    public State state = State.Patrol;
    public float currentSpeed;
    public float distanceToPlayer;

    private Rigidbody _rb;
    private Transform _player;
    private Rigidbody _playerRb;
    private Vector3 _anchor;          // 순찰 중심
    private Vector3 _patrolTarget;
    private float _waitUntil;
    private bool _waiting;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        // 저항은 아래에서 직접 계산하므로 내장 감쇠는 끈다 (MoveControl 과 동일한 구조).
        _rb.linearDamping = 0f;
        _rb.angularDamping = 0f;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        _rb.constraints = RigidbodyConstraints.None;
    }

    void OnValidate()
    {
        // 히스테리시스 거리가 기준 거리보다 작으면 경계에서 상태가 떨린다. 항상 더 크게 유지.
        loseRange = Mathf.Max(loseRange, detectionRange * 1.2f);
        breakOffRange = Mathf.Max(breakOffRange, attackRange * 1.2f);
    }


    void Start()
    {
        GameObject target = GameObject.FindGameObjectWithTag("Player");
        if (target != null)
        {
            _player = target.transform;
            _playerRb = target.GetComponent<Rigidbody>();
        }
        _anchor = transform.position;
        PickPatrolTarget();
    }

    void FixedUpdate()
    {
        if (_rb == null) return;

        Vector3 fwd = FlatForward();
        Vector3 flatVel = _rb.linearVelocity;
        flatVel.y = 0f;
        currentSpeed = Vector3.Dot(flatVel, fwd);

        UpdateState();

        switch (state)
        {
            case State.Patrol: TickPatrol(); break;
            case State.Chase: TickChase(); break;
            case State.Broadside: TickBroadside(); break;
        }

        ApplyWaterResistance(fwd, flatVel);
    }

    // ---------- 상태 전환 ----------

    void UpdateState()
    {
        if (_player == null) { state = State.Patrol; return; }

        distanceToPlayer = Vector3.Distance(transform.position, _player.position);

        switch (state)
        {
            case State.Patrol:
                if (distanceToPlayer <= detectionRange) state = State.Chase;
                break;
            case State.Chase:
                if (distanceToPlayer <= attackRange) state = State.Broadside;
                else if (distanceToPlayer > loseRange)
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

    // ---------- 순찰 ----------

    void PickPatrolTarget()
    {
        Vector2 r = Random.insideUnitCircle * patrolRadius;
        _patrolTarget = _anchor + new Vector3(r.x, 0f, r.y);
        _waiting = false;
    }

    void TickPatrol()
    {
        if (_waiting)
        {
            if (Time.time >= _waitUntil) PickPatrolTarget();
            return; // 대기 중에는 저항만 받으며 자연히 멈춘다
        }

        Vector3 to = _patrolTarget - transform.position;
        to.y = 0f;
        if (to.magnitude <= arriveDistance)
        {
            _waiting = true;
            _waitUntil = Time.time + Random.Range(waitRange.x, waitRange.y);
            return;
        }

        SailTowards(_patrolTarget, patrolSpeed);
    }

    // ---------- 추격 ----------

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

    // ---------- 교전 (현측 유지) ----------

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

        float angle = Vector3.SignedAngle(fwd, desired, Vector3.up);
        ApplySteer(angle);

        // 현측을 유지한 채 천천히 전진 - 플레이어 주위를 도는 움직임이 된다.
        float align = Mathf.Clamp01(1f - Mathf.Abs(angle) / 90f);
        ApplyThrust(broadsideSpeed * align);
    }

    // ---------- 공통 조종 ----------

    /// <summary>목표 지점을 향해 침로를 잡고 전진한다. 크게 틀어야 하면 속도를 줄인다.</summary>
    void SailTowards(Vector3 target, float speedCap)
    {
        Vector3 to = target - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude < 0.01f) return;

        float angle = Vector3.SignedAngle(FlatForward(), to, Vector3.up);
        ApplySteer(angle);

        // 정면에서 벗어날수록 감속: 급선회 시 반경이 줄어 자연스럽다.
        float throttle = Mathf.Lerp(1f, 0.35f, Mathf.Clamp01((Mathf.Abs(angle) - 30f) / 90f));
        ApplyThrust(speedCap * throttle);
    }

    void ApplySteer(float signedAngleDeg)
    {
        float steer = Mathf.Clamp(signedAngleDeg / Mathf.Max(steerSoftAngle, 1f), -1f, 1f);
        float turnRate = _rb.angularVelocity.y * Mathf.Rad2Deg;
        if (Mathf.Abs(steer) > 0.01f && Mathf.Abs(turnRate) < maxTurnRate)
            _rb.AddTorque(Vector3.up * turnAccel * Mathf.Deg2Rad * steer, ForceMode.Acceleration);
    }

    void ApplyThrust(float speedCap)
    {
        if (speedCap <= 0.01f) return;
        if (currentSpeed < speedCap)
            _rb.AddForce(FlatForward() * forwardAccel, ForceMode.Acceleration);
    }

    void ApplyWaterResistance(Vector3 fwd, Vector3 flatVel)
    {
        // 전후 저항
        _rb.AddForce(-fwd * currentSpeed * linearDrag, ForceMode.Acceleration);

        // 옆미끄러짐 억제 (당파로 밀린 속도를 서서히 죽인다)
        Vector3 right = transform.right;
        right.y = 0f;
        right.Normalize();
        float lateral = Vector3.Dot(flatVel, right);
        _rb.AddForce(-right * lateral * lateralGrip, ForceMode.Acceleration);

        // 선회 저항
        _rb.AddTorque(-Vector3.up * _rb.angularVelocity.y * angularDrag, ForceMode.Acceleration);
    }

    Vector3 FlatForward()
    {
        Vector3 f = transform.forward;
        f.y = 0f;
        return f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;
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

        if (Application.isPlaying && state == State.Patrol && !_waiting)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, _patrolTarget);
            Gizmos.DrawWireSphere(_patrolTarget, 1.5f);
        }
    }
}
