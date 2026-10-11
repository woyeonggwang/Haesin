using UnityEngine;

/// <summary>
/// 선착장 앞바다를 원을 그리며 천천히 도는 배(HarborMooring 이 정박선 복제본에 붙임).
/// 사용자 지시(2026-10-05): 예전 정박선은 kinematic 으로 선착장 바로 옆에 멈춰 있어 하차를 막고 밀리지도 않았음 →
/// 선착장에서 떨어진 깊은 물의 원(center, radius)을 돌게 하고, 물리(힘)로 움직여 부딪히면 밀려나게 함.
/// 움직임은 EnemyShipBase 와 같은 방식(추진·조타 힘 + 직접 계산하는 물 저항) — Rigidbody 는 kinematic 아님,
/// 높이·기울기는 ShipBuoyancy(Physics 모드). 원 위 lookAhead 앞 점을 따라가므로 밀려나도 원으로 돌아옴.
/// 앞쪽(yieldAngle 안) yieldDistance 안에 다른 배가 있으면 추진을 멈추고 비켜 기다림(플레이어 배 포함).
/// 침몰 중·완파면 아무것도 안 함.
/// 표적(2026-10-05 사용자 지시 "선착장 배가 대포를 안 쏨"): 0.5 s 마다 engageRange 안 가장 가까운 적대 배(우호도·영해 규칙 —
/// ShipFaction.FindNearestHostile)를 Target 에 둠 → 이 배의 EnemyCannonControl / NavyCannonControl 이 그 표적을 쏨(현측 정렬은 도는 동안 맞을 때).
/// 표적이 있어도 원을 계속 돎(쫓지 않음).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class HarborPatrol : MonoBehaviour
{
    [Header("도는 원 (월드)")]
    public Vector3 center;
    public float radius = 45f;
    [Tooltip("위에서 보아 시계 방향으로 돌지.")] public bool clockwise = false;
    [Tooltip("원 위에서 이만큼(m) 앞의 점을 향해 감.")] public float lookAhead = 30f;

    [Header("항해")]
    public float cruiseSpeed = 2.5f;
    public float forwardAccel = 3f;
    [Tooltip("선회 가속도(도/s²).")] public float turnAccel = 45f;
    [Tooltip("최대 선회 속도(도/s).")] public float maxTurnRate = 15f;
    public float steerSoftAngle = 25f;

    [Header("물의 저항")]
    public float linearDrag = 0.6f;
    public float angularDrag = 2.2f;
    [Tooltip("옆미끄러짐 억제. 작을수록 옆에서 받으면 더 멀리 밀려남(AI 배 3).")] public float lateralGrip = 1.2f;

    [Header("양보")]
    [Tooltip("앞쪽 이 거리(m, 배 중심끼리) 안에 다른 배가 있으면 추진을 멈춤.")] public float yieldDistance = 45f;
    [Tooltip("앞쪽으로 보는 각도(±°).")] public float yieldAngle = 40f;

    [Header("포격 표적")]
    [Tooltip("이 거리(m, 배 중심끼리) 안의 적대 배를 표적으로 삼음. 대포 사거리(관군 95 · 왜군 90)는 대포 쪽 값.")] public float engageRange = 110f;
    [Tooltip("표적을 다시 찾는 간격(s).")] public float scanInterval = 0.5f;

    [Header("상태 (읽기 전용)")]
    public float currentSpeed;
    public bool yielding;
    [SerializeField] ShipFaction _target;
    /// <summary>지금 표적(없으면 null). 대포 스크립트가 읽음.</summary>
    public ShipFaction Target { get { return _target; } }
    float _nextScan;

    Rigidbody _rb;
    ShipHealth _hp;
    ShipFaction _self;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _hp = GetComponent<ShipHealth>();
        _self = GetComponent<ShipFaction>();
        _rb.isKinematic = false;
        _rb.linearDamping = 0f;
        _rb.angularDamping = 0f;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    Vector3 FlatForward()
    {
        Vector3 f = transform.forward; f.y = 0f;
        return f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward;
    }

    /// <summary>원 위 각도 a(라디안, atan2(z, x))의 접선 방향(도는 쪽).</summary>
    public Vector3 Tangent(float a)
    {
        return clockwise ? new Vector3(Mathf.Sin(a), 0f, -Mathf.Cos(a)) : new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
    }

    void FixedUpdate()
    {
        if (_rb == null || _rb.isKinematic) return;
        if (_hp != null && (_hp.isDead || _hp.isSinking)) { _target = null; return; }

        if (Time.time >= _nextScan)
        {
            _nextScan = Time.time + scanInterval;
            _target = ShipFaction.FindNearestHostile(_self, engageRange);
        }

        Vector3 fwd = FlatForward();
        Vector3 v = _rb.linearVelocity; v.y = 0f;
        currentSpeed = Vector3.Dot(v, fwd);

        // 원 위 앞 점
        Vector3 rel = transform.position - center; rel.y = 0f;
        if (rel.sqrMagnitude < 1f) rel = -fwd;
        float a = Mathf.Atan2(rel.z, rel.x);
        float da = lookAhead / Mathf.Max(radius, 1f) * (clockwise ? -1f : 1f);
        Vector3 target = center + new Vector3(Mathf.Cos(a + da), 0f, Mathf.Sin(a + da)) * radius;

        Vector3 to = target - transform.position; to.y = 0f;
        float angle = Vector3.SignedAngle(fwd, to, Vector3.up);
        float steer = Mathf.Clamp(angle / Mathf.Max(steerSoftAngle, 1f), -1f, 1f);
        float turnRate = _rb.angularVelocity.y * Mathf.Rad2Deg;
        if (Mathf.Abs(steer) > 0.01f && Mathf.Abs(turnRate) < maxTurnRate)
            _rb.AddTorque(Vector3.up * turnAccel * Mathf.Deg2Rad * steer, ForceMode.Acceleration);

        // 앞에 다른 배가 있으면 양보
        yielding = false;
        var all = ShipFaction.All;
        for (int i = 0; i < all.Count; i++)
        {
            var s = all[i];
            if (s == null || s == _self) continue;
            Vector3 d = s.transform.position - transform.position; d.y = 0f;
            float m = d.magnitude;
            if (m > yieldDistance || m < 0.01f) continue;
            if (Vector3.Angle(fwd, d) <= yieldAngle) { yielding = true; break; }
        }

        float speedCap = yielding ? 0f : cruiseSpeed * Mathf.Lerp(1f, 0.4f, Mathf.Clamp01((Mathf.Abs(angle) - 30f) / 90f));
        if (speedCap > 0.01f && currentSpeed < speedCap)
            _rb.AddForce(fwd * forwardAccel, ForceMode.Acceleration);

        // 물 저항(EnemyShipBase 와 같은 식)
        _rb.AddForce(-fwd * currentSpeed * linearDrag, ForceMode.Acceleration);
        Vector3 right = transform.right; right.y = 0f; right.Normalize();
        _rb.AddForce(-right * Vector3.Dot(v, right) * lateralGrip, ForceMode.Acceleration);
        _rb.AddTorque(-Vector3.up * _rb.angularVelocity.y * angularDrag, ForceMode.Acceleration);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
        Vector3 prev = center + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= 48; i++)
        {
            float a = i / 48f * Mathf.PI * 2f;
            Vector3 p = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
            Gizmos.DrawLine(prev, p); prev = p;
        }
    }
}
