using UnityEngine;

/// <summary>
/// 적 배들의 공통 토대. 힘 기반 항해(추진·조타·물 저항)와 순찰 로직을 담당한다.
/// 전투 판단은 EnemyAI, 정찰 판단은 SekiScoutAI 가 이 클래스를 상속해서 구현한다.
///
/// 축 분담은 MoveControl 과 동일: 수평 이동과 Y축 회전만 담당하고,
/// 높이·기울기는 ShipBuoyancy 가 부력(힘)으로 처리한다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public abstract class EnemyShipBase : MonoBehaviour
{
    [Header("항해 성능")]
    [Tooltip("최고 속도(m/s).")]
    public float maxSpeed = 7f;
    [Tooltip("순찰 시 순항 속도(m/s).")]
    public float patrolSpeed = 2.5f;
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

    [Header("표적 선택")]
    [Tooltip("표적을 다시 고르는 간격(초). 매 프레임 전체를 훑지 않기 위한 값.")]
    public float retargetInterval = 0.5f;
    [Tooltip("이 거리보다 먼 배는 표적 후보에서 제외한다(m). 0이면 제한 없음.")]
    public float targetSearchRange = 600f;
    [Header("공통 상태 (읽기 전용)")]
    public float currentSpeed;
    public float distanceToPlayer;    [Tooltip("현재 표적의 진영.")]
    public string targetFactionName = "-";


    protected Rigidbody _rb;
    protected Transform _player;
    protected Rigidbody _playerRb;
    protected ShipFaction _self;
    protected ShipFaction _targetShip;
    private float _nextRetargetTime;
    protected Vector3 _anchor;          // 순찰 중심

    /// <summary>지금 쫓는 표적의 진영표. 포격 스크립트가 읽는다. 없으면 null.</summary>
    public ShipFaction TargetShip { get { return _targetShip; } }

    /// <summary>지금 쫓는 표적. 예전에는 항상 플레이어였지만 이제는 관군함일 수도 있다.</summary>
    public Transform CurrentTarget { get { return _player; } }
    protected Vector3 _patrolTarget;
    private float _waitUntil;
    private bool _waiting;

    protected virtual void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _self = GetComponent<ShipFaction>();
        // 저항은 아래에서 직접 계산하므로 내장 감쇠는 끈다 (MoveControl 과 동일한 구조).
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

    /// <summary>
    /// 가장 가까운 적대 진영의 배를 표적으로 다시 고른다.
    /// 예전에는 Player 태그만 찾았지만, 관군이 생기면서 왜군의 적이 둘이 되었다.
    /// ShipFaction 이 붙어 있지 않은 배는 예전처럼 플레이어만 쫓는다.
    /// </summary>
    protected virtual void Retarget()
    {
        if (_self != null)
        {
            if (_targetShip != null && (!_targetShip.isActiveAndEnabled || !_self.IsHostileTo(_targetShip)))
                _targetShip = null;

            ShipFaction nearest = ShipFaction.FindNearestHostile(_self, targetSearchRange);
            if (nearest != null) _targetShip = nearest;

            if (_targetShip != null)
            {
                _player = _targetShip.transform;
                _playerRb = _targetShip.Body;
                targetFactionName = _targetShip.faction.ToString();
                return;
            }

            _player = null;
            _playerRb = null;
            targetFactionName = "-";
            return;
        }

        // 진영표가 없는 예전 구성 - 플레이어만 쫓는다.
        if (_player == null)
        {
            GameObject t = GameObject.FindGameObjectWithTag("Player");
            if (t != null)
            {
                _player = t.transform;
                _playerRb = t.GetComponent<Rigidbody>();
                targetFactionName = "Player";
            }
        }
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

        if (_player != null)
            distanceToPlayer = Vector3.Distance(transform.position, _player.position);
        else
            distanceToPlayer = float.MaxValue;

        TickBehaviour();

        ApplyWaterResistance(fwd, flatVel);
    }

    /// <summary>파생 클래스가 매 물리 프레임 판단·조종을 구현한다.</summary>
    protected abstract void TickBehaviour();

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

    // ---------- 공통 조종 ----------

    /// <summary>목표 지점을 향해 침로를 잡고 전진한다. 크게 틀어야 하면 속도를 줄인다.</summary>
    protected void SailTowards(Vector3 target, float speedCap)
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

    protected Vector3 FlatForward()
    {
        Vector3 f = transform.forward;
        f.y = 0f;
        return f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;
    }
}
