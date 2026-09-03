using UnityEngine;

/// <summary>
/// 관군 함선의 항해 토대. 플레이어 쪽 스크립트(MoveControl 등)와는 완전히 분리되어 있고,
/// 왜군 쪽 EnemyShipBase 와도 별개다. 판단은 NavyPanokAI 가 상속해서 구현한다.
///
/// 축 분담은 다른 배들과 같다. 이 클래스는 수평 이동과 Y축 회전만 담당하고,
/// 높이·기울기는 ShipBuoyancy 가 부력으로 처리한다.
///
/// 왜군 AI 와 다른 점은 표적을 고르는 방식이다. 왜군은 원래 플레이어만 쫓았지만
/// 관군은 해적(플레이어)과 왜군 둘 다 적이므로, ShipFaction 레지스트리에서
/// 가장 가까운 적대 진영의 배를 주기적으로 다시 고른다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(ShipFaction))]
public abstract class NavyShipBase : MonoBehaviour
{
    [Header("항해 성능")]
    [Tooltip("최고 속도(m/s).")]
    public float maxSpeed = 6.5f;
    [Tooltip("순찰 시 순항 속도(m/s).")]
    public float patrolSpeed = 2.5f;
    [Tooltip("전진 가속도(m/s^2).")]
    public float forwardAccel = 5f;
    [Tooltip("선회 가속도(도/s^2). 판옥선은 무거우므로 안택선보다 약간 둔하다.")]
    public float turnAccel = 55f;
    [Tooltip("최대 선회 속도(도/s).")]
    public float maxTurnRate = 20f;
    [Tooltip("목표가 이 각도 이내면 방향타를 비례로 줄인다(도).")]
    public float steerSoftAngle = 25f;

    [Header("물의 저항")]
    [Tooltip("전후 방향 감속.")]
    public float linearDrag = 0.6f;
    [Tooltip("선회 감속.")]
    public float angularDrag = 2.2f;
    [Tooltip("옆미끄러짐 억제. 당파로 밀려난 뒤 서서히 잡아준다.")]
    public float lateralGrip = 3.5f;

    [Header("순찰")]
    [Tooltip("시작 지점을 중심으로 이 반지름 안에서 순찰한다(m).")]
    public float patrolRadius = 70f;
    [Tooltip("목적지에 이 거리까지 가까워지면 도착으로 본다(m).")]
    public float arriveDistance = 9f;
    [Tooltip("도착 후 다음 목적지로 떠나기 전 대기 시간 범위(초).")]
    public Vector2 waitRange = new Vector2(2f, 6f);

    [Header("표적 선택")]
    [Tooltip("표적을 다시 고르는 간격(초). 매 프레임 전체를 훑지 않기 위한 값.")]
    public float retargetInterval = 0.5f;
    [Tooltip("이 거리보다 먼 배는 표적 후보에서 제외한다(m). 0이면 제한 없음.")]
    public float targetSearchRange = 600f;

    [Header("공통 상태 (읽기 전용)")]
    public float currentSpeed;
    [Tooltip("현재 표적과의 거리(m). 표적이 없으면 매우 큰 값.")]
    public float distanceToTarget = float.MaxValue;
    [Tooltip("현재 표적의 진영.")]
    public string targetFactionName = "-";

    protected Rigidbody _rb;
    protected ShipFaction _self;
    protected ShipFaction _target;
    protected Vector3 _anchor;
    protected Vector3 _patrolTarget;

    private float _nextRetargetTime;
    private float _waitUntil;
    private bool _waiting;

    /// <summary>지금 쫓고 있는 표적. 포격 스크립트가 읽는다. 없으면 null.</summary>
    public ShipFaction Target { get { return _target; } }

    protected virtual void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _self = GetComponent<ShipFaction>();

        // 저항은 아래에서 직접 계산하므로 내장 감쇠는 끈다.
        _rb.linearDamping = 0f;
        _rb.angularDamping = 0f;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        _rb.constraints = RigidbodyConstraints.None;
    }

    protected virtual void Start()
    {
        _anchor = transform.position;
        PickPatrolTarget();
        Retarget();
    }

    void FixedUpdate()
    {
        if (_rb == null) return;

        Vector3 fwd = FlatForward();
        Vector3 flatVel = _rb.linearVelocity;
        flatVel.y = 0f;
        currentSpeed = Vector3.Dot(flatVel, fwd);

        if (Time.time >= _nextRetargetTime)
        {
            _nextRetargetTime = Time.time + Mathf.Max(0.1f, retargetInterval);
            Retarget();
        }

        if (_target != null)
            distanceToTarget = Vector3.Distance(transform.position, _target.transform.position);
        else
            distanceToTarget = float.MaxValue;

        TickBehaviour();

        ApplyWaterResistance(fwd, flatVel);
    }

    /// <summary>파생 클래스가 매 물리 프레임 판단·조종을 구현한다.</summary>
    protected abstract void TickBehaviour();

    // ---------- 표적 ----------

    /// <summary>가장 가까운 적대 진영의 배를 표적으로 다시 고른다.</summary>
    protected virtual void Retarget()
    {
        if (_self == null) { _target = null; targetFactionName = "-"; return; }

        // 이미 놓친(파괴되거나 비활성) 표적을 붙들고 있지 않게 먼저 정리한다.
        if (_target != null && (!_target.isActiveAndEnabled || !_self.IsHostileTo(_target)))
            _target = null;

        ShipFaction nearest = ShipFaction.FindNearestHostile(_self, targetSearchRange);
        if (nearest != null) _target = nearest;

        targetFactionName = _target != null ? _target.faction.ToString() : "-";
    }

    // ---------- 순찰 ----------

    protected void PickPatrolTarget()
    {
        Vector2 r = Random.insideUnitCircle * patrolRadius;
        _patrolTarget = _anchor + new Vector3(r.x, 0f, r.y);
        _waiting = false;
    }

    /// <summary>순찰 한 틱. 목적지로 항해하고 도착하면 잠시 표류한 뒤 새 목적지를 고른다.</summary>
    protected void TickPatrol()
    {
        if (_waiting)
        {
            if (Time.time >= _waitUntil) PickPatrolTarget();
            return;
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

    // ---------- 조종 ----------

    /// <summary>목표 지점을 향해 침로를 잡고 전진한다. 크게 틀어야 하면 속도를 줄인다.</summary>
    protected void SailTowards(Vector3 target, float speedCap)
    {
        Vector3 to = target - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude < 0.01f) return;

        float angle = Vector3.SignedAngle(FlatForward(), to, Vector3.up);
        ApplySteer(angle);

        float throttle = Mathf.Lerp(1f, 0.35f, Mathf.Clamp01((Mathf.Abs(angle) - 30f) / 90f));
        ApplyThrust(speedCap * throttle);
    }

    protected void ApplySteer(float signedAngleDeg)
    {
        float steer = Mathf.Clamp(signedAngleDeg / Mathf.Max(steerSoftAngle, 1f), -1f, 1f);
        float turnRate = _rb.angularVelocity.y * Mathf.Rad2Deg;
        if (Mathf.Abs(steer) > 0.01f && Mathf.Abs(turnRate) < maxTurnRate)
            _rb.AddTorque(Vector3.up * turnAccel * Mathf.Deg2Rad * steer, ForceMode.Acceleration);
    }

    protected void ApplyThrust(float speedCap)
    {
        if (speedCap <= 0.01f) return;
        if (currentSpeed < speedCap)
            _rb.AddForce(FlatForward() * forwardAccel, ForceMode.Acceleration);
    }

    void ApplyWaterResistance(Vector3 fwd, Vector3 flatVel)
    {
        _rb.AddForce(-fwd * currentSpeed * linearDrag, ForceMode.Acceleration);

        Vector3 right = transform.right;
        right.y = 0f;
        right.Normalize();
        float lateral = Vector3.Dot(flatVel, right);
        _rb.AddForce(-right * lateral * lateralGrip, ForceMode.Acceleration);

        _rb.AddTorque(-Vector3.up * _rb.angularVelocity.y * angularDrag, ForceMode.Acceleration);
    }

    protected Vector3 FlatForward()
    {
        Vector3 f = transform.forward;
        f.y = 0f;
        return f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;
    }
}
