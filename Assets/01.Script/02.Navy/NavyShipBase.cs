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

    [Header("부스트 (플레이어 MoveControl 과 같은 방식)")]
    [Tooltip("부스트 최대일 때 최고 속도·가속도 배수. 1 이면 부스트 없음.\n"
           + "판단(언제 켜는지)은 파생 AI 가 wantBoost 로 정한다. 상선은 켜지 않는다.")]
    public float boostMultiplier = 1.6f;
    [Tooltip("부스트가 최대까지 오르는 시간(초).")]
    public float boostRiseTime = 2f;
    [Tooltip("부스트가 풀리는 시간(초).")]
    public float boostFallTime = 1.5f;

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
    [Tooltip("다음 순찰 지점을 뱃머리 쪽으로 치우치게 고르는 정도. 0이면 완전 무작위, 1이면 늘 정면.")]
    [Range(0f, 1f)]
    public float patrolForwardBias = 0.45f;
    [Tooltip("순찰 구역의 중심이 배를 따라오는 속도(m/s). 0이면 처음 자리에 고정된다.\n"
           + "고정해 두면 구역 밖으로 나갈 때마다 뒤로 끌려가서 앞으로 나아가는 느낌이 사라진다.")]
    public float patrolAnchorDrift = 1.5f;

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
    [Range(0f, 1f)] public float boostLevel;

    /// <summary>파생 AI 가 매 틱 정한다. true 면 부스트 게이지가 오른다.</summary>
    protected bool wantBoost;

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

        DriftAnchor();
        TickBehaviour();

        float boostTarget = wantBoost ? 1f : 0f;
        float boostRate = boostTarget > boostLevel
            ? (boostRiseTime > 0f ? Time.fixedDeltaTime / boostRiseTime : 1f)
            : (boostFallTime > 0f ? Time.fixedDeltaTime / boostFallTime : 1f);
        boostLevel = Mathf.MoveTowards(boostLevel, boostTarget, boostRate);

        ApplyWaterResistance(fwd, flatVel);
    }


    /// <summary>
    /// 순찰 구역의 중심을 배 쪽으로 아주 천천히 옮긴다.
    /// 중심이 고정되어 있으면 배가 구역을 벗어날 때마다 목적지가 뒤쪽으로만 잡혀서
    /// 앞으로 나아가려는 성향이 죽는다. 구역이 같이 흘러가면 바다를 가로지르면서도
    /// 제 구역을 크게 벗어나지 않는다.
    /// </summary>
    void DriftAnchor()
    {
        if (patrolAnchorDrift <= 0f) return;
        Vector3 p = transform.position; p.y = 0f;
        Vector3 a = _anchor; a.y = 0f;
        _anchor = Vector3.MoveTowards(a, p, patrolAnchorDrift * Time.fixedDeltaTime);
    }

    /// <summary>파생 클래스가 매 물리 프레임 판단·조종을 구현한다.</summary>
    protected abstract void TickBehaviour();

    // ---------- 표적 ----------

    /// <summary>가장 가까운 적대 진영의 배를 표적으로 다시 고른다.</summary>
    protected virtual void Retarget()
    {
        if (_self == null) { _target = null; targetFactionName = "-"; return; }

        // 이미 놓친(파괴되거나 비활성) 표적을 붙들고 있지 않게 먼저 정리한다.
        // 놓친 표적, 침몰 중인 표적, 베이스 안전지대로 들어간 표적은 놓아준다.
        if (_target != null && (!_target.isActiveAndEnabled || !_self.IsHostileTo(_target) || !ShipFaction.IsTargetable(_target)))
            _target = null;

        ShipFaction nearest = ShipFaction.FindNearestHostile(_self, targetSearchRange);
        if (nearest != null) _target = nearest;

        targetFactionName = _target != null ? _target.faction.ToString() : "-";
    }

    // ---------- 순찰 ----------

    protected void PickPatrolTarget()
    {
        // 완전히 무작위인 지점을 고르면 배가 제자리에서 뱅뱅 도는 것처럼 보인다.
        // 뱃머리 쪽으로 조금 치우친 방향을 골라 앞으로 나아가는 느낌을 준다.
        Vector2 r = Random.insideUnitCircle;
        Vector3 dir = new Vector3(r.x, 0f, r.y);
        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
        dir.Normalize();

        Vector3 biased = Vector3.Slerp(dir, FlatForward(), Mathf.Clamp01(patrolForwardBias));
        if (biased.sqrMagnitude < 0.0001f) biased = FlatForward();
        biased.Normalize();

        float dist = Random.Range(patrolRadius * 0.4f, patrolRadius);
        Vector3 candidate = transform.position + biased * dist;

        // 그래도 순찰 구역은 벗어나지 않게 앵커 반경 안으로 당겨 둔다.
        Vector3 fromAnchor = candidate - _anchor;
        fromAnchor.y = 0f;
        if (fromAnchor.magnitude > patrolRadius)
            candidate = _anchor + fromAnchor.normalized * patrolRadius;

        candidate.y = 0f;
        candidate = BaseSafeZone.PushOutside(candidate, 25f); // 순찰 목적지도 베이스 안전지대 밖으로
        _patrolTarget = candidate;
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
        // 베이스 안전지대 안으로는 들어가지 않는다: 목적지가 안이면 경계 밖으로 밀어낸다.
        // 어쩌다 안에 들어와 있으면(표류 등) 밖으로 나가는 쪽을 목적지로 잡는다.
        target = BaseSafeZone.PushOutside(target, 25f);
        if (BaseSafeZone.Contains(transform.position))
        {
            Vector3 outDir = transform.position - BaseSafeZone.Center; outDir.y = 0f;
            if (outDir.sqrMagnitude < 0.01f) outDir = FlatForward();
            target = BaseSafeZone.Center + outDir.normalized * (BaseSafeZone.Radius + 60f);
        }

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
        float boostFactor = 1f + (boostMultiplier - 1f) * boostLevel;
        if (currentSpeed < speedCap * boostFactor)
            _rb.AddForce(FlatForward() * forwardAccel * boostFactor, ForceMode.Acceleration);
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
