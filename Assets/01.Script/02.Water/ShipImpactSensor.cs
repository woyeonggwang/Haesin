using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 배가 무언가와 부딪히는 순간을 잡아 파문/물보라를 만들고,
/// 항해 중에는 선체 둘레를 따라 항적 거품을 만든다.
///
/// 이동을 transform 으로 직접 처리하는 코드(MoveControl 등)에서는
/// Rigidbody 의 velocity 가 0 이라 collision.impulse / relativeVelocity 를 믿을 수 없다.
/// 그래서 프레임마다 자기 속도를 직접 재고, 부딪히기 "직전" 속도를 기억해 두었다가
/// 그 값으로 충돌 세기를 만든다. 충돌 후에는 두 배가 같이 밀려나 상대속도가 0 이 되기 때문이다.
/// </summary>
public class ShipImpactSensor : MonoBehaviour
{
    [Header("충돌 파문")]
    public bool rippleOnCollision = true;
    [Tooltip("한 번 충돌 파문을 만든 뒤 다음 파문까지의 최소 간격(초).")]
    public float impactCooldown = 0.8f;
    [Tooltip("충돌 세기를 계산할 때 몇 프레임 전 속도를 쓸지. 접촉 직전 속도를 잡기 위한 값.")]
    public int velocityLookback = 6;

    [Header("항적 - 켜고 끄기")]
    public bool wakeWhileMoving = true;
    [Tooltip("이 속도(m/s)를 넘어야 항적이 나온다. 멈춰 있을 때 나오면 값을 올린다.")]
    public float wakeMinSpeed = 2f;
    [Tooltip("이 속도에서 항적이 최대 크기가 된다.")]
    public float wakeFullSpeed = 12f;
    [Tooltip("항적 파문 생성 간격(초). 짧을수록 촘촘하지만 풀을 많이 쓴다.")]
    public float wakeInterval = 0.25f;

    [Header("항적 - 크기")]
    [Tooltip("최소 속도에서의 항적 반지름(m).")]
    public float wakeRadiusMin = 14f;
    [Tooltip("최대 속도에서의 항적 반지름(m).")]
    public float wakeRadiusMax = 30f;
    [Tooltip("최소 속도에서의 솟아오름(m).")]
    public float wakeAmplitudeMin = 0.12f;
    [Tooltip("최대 속도에서의 솟아오름(m).")]
    public float wakeAmplitudeMax = 0.4f;

    [Header("항적 - 생성 위치")]
    [Tooltip("항적이 나올 지점들. 비워두면 콜라이더 크기로 뱃머리/좌현/우현/선미 네 곳을 자동 계산한다.")]
    public Transform[] wakePoints;
    [Tooltip("자동 계산 시 선체 크기 대비 얼마나 바깥에서 거품을 낼지.")]
    [Range(0.4f, 1.5f)]
    public float wakePointSpread = 0.85f;
    [Tooltip("자동 계산 지점 중 뱃머리만 쓰지 않고 선체 둘레 전체에 돌아가며 만든다.")]
    public bool cycleWakePoints = true;

    [Header("파문 모양")]
    [Tooltip("항적 파문의 세로:가로 비율. 1이면 원형, 2면 선체처럼 길쭉하다.")]
    public float wakeAspect = 2f;
    [Tooltip("충돌 파문의 세로:가로 비율. 충돌 방향으로 길어진다. 1이면 원형.")]
    public float impactAspect = 1.3f;

    [Header("디버그")]
    public int debugEnterCount;
    public int debugStayCount;
    public float debugLastImpactSpeed;
    public float debugPeakImpactSpeed;
    public int debugSpawnCount;
    public float debugFlatSpeed;

    /// <summary>이 프레임 기준 월드 속도(m/s).</summary>
    public Vector3 Velocity { get { return _velocity; } }

    /// <summary>접촉 직전(= velocityLookback 프레임 전) 속도.</summary>
    public Vector3 ApproachVelocity
    {
        get
        {
            if (_history.Count == 0) return _velocity;
            int idx = _history.Count - 1 - Mathf.Clamp(velocityLookback, 0, _history.Count - 1);
            return _history[idx];
        }
    }

    private static readonly Dictionary<Collider, ShipImpactSensor> _lookup = new Dictionary<Collider, ShipImpactSensor>();

    private readonly List<Vector3> _history = new List<Vector3>();
    private const int HistoryMax = 20;

    private Vector3 _lastPos;
    private Vector3 _velocity;
    private float _lastImpactTime = -999f;
    private float _lastWakeTime;
    private int _wakeCursor;

    // 자동 계산된 선체 로컬 오프셋 (뱃머리 / 선미 / 우현 / 좌현)
    private Vector3[] _autoOffsets;

    void OnEnable()
    {
        _lastPos = transform.position;
        _history.Clear();

        Collider[] cols = GetComponentsInChildren<Collider>();
        for (int i = 0; i < cols.Length; i++) _lookup[cols[i]] = this;

        BuildAutoOffsets();
    }

    void OnDisable()
    {
        Collider[] cols = GetComponentsInChildren<Collider>();
        for (int i = 0; i < cols.Length; i++) _lookup.Remove(cols[i]);
    }

    void BuildAutoOffsets()
    {
        float halfLength = 6f, halfWidth = 3f;

        Collider c = GetComponent<Collider>();
        if (c != null)
        {
            // 배의 Y 회전만 반영한 기준축에서 앞뒤/좌우 반경을 구한다.
            Quaternion flat = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            Vector3 fwd = flat * Vector3.forward;
            Vector3 right = flat * Vector3.right;
            Vector3 ext = c.bounds.extents;

            halfLength = Mathf.Abs(fwd.x) * ext.x + Mathf.Abs(fwd.z) * ext.z;
            halfWidth = Mathf.Abs(right.x) * ext.x + Mathf.Abs(right.z) * ext.z;
        }

        halfLength *= wakePointSpread;
        halfWidth *= wakePointSpread;

        // 로컬 방향(앞/뒤/우/좌) 기준 오프셋. 월드 변환은 매 프레임 한다.
        _autoOffsets = new Vector3[]
        {
            new Vector3(0f, 0f, halfLength),          // 뱃머리
            new Vector3(halfWidth, 0f, 0f),           // 우현
            new Vector3(-halfWidth, 0f, 0f),          // 좌현
            new Vector3(0f, 0f, -halfLength)          // 선미
        };
    }

    void Update()
    {
        if (Time.deltaTime > 0f)
            _velocity = (transform.position - _lastPos) / Time.deltaTime;
        _lastPos = transform.position;

        _history.Add(_velocity);
        if (_history.Count > HistoryMax) _history.RemoveAt(0);

        if (wakeWhileMoving) TryWake();
    }

    void TryWake()
    {
        if (WaterRippleSpawner.Instance == null) return;

        // 파도 때문에 배가 위아래로 흔들리는 성분은 빼고, 수평 이동 속도만 본다.
        Vector3 flat = new Vector3(_velocity.x, 0f, _velocity.z);
        float speed = flat.magnitude;
        debugFlatSpeed = speed;

        if (speed < wakeMinSpeed) return;
        if (Time.time - _lastWakeTime < wakeInterval) return;
        _lastWakeTime = Time.time;

        float k = Mathf.Clamp01(Mathf.InverseLerp(wakeMinSpeed, wakeFullSpeed, speed));
        float radius = Mathf.Lerp(wakeRadiusMin, wakeRadiusMax, k);
        float amplitude = Mathf.Lerp(wakeAmplitudeMin, wakeAmplitudeMax, k);

        Quaternion flatRot = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        WaterRippleSpawner.Instance.SpawnWake(NextWakePosition(), amplitude, radius, flatRot * Vector3.forward, wakeAspect);
    }

    Vector3 NextWakePosition()
    {
        // 직접 지정한 지점이 있으면 그것을 우선 사용한다.
        if (wakePoints != null && wakePoints.Length > 0)
        {
            Transform t = wakePoints[_wakeCursor % wakePoints.Length];
            _wakeCursor++;
            if (t != null) return t.position;
        }

        if (_autoOffsets == null || _autoOffsets.Length == 0) BuildAutoOffsets();

        Quaternion flatRot = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        Vector3 off = cycleWakePoints
            ? _autoOffsets[_wakeCursor % _autoOffsets.Length]
            : _autoOffsets[0];
        _wakeCursor++;

        return transform.position + flatRot * off;
    }

    void OnCollisionEnter(Collision collision)
    {
        debugEnterCount++;
        HandleImpact(collision);
    }

    // transform 으로 배를 옮기는 구조에서는 Enter 가 한 번만 들어오고
    // 이후 계속 붙어 있는 상태가 되므로 Stay 도 함께 본다.
    // 실제로 다시 부딪히는 경우(접근 속도가 살아있는 경우)만 통과한다.
    void OnCollisionStay(Collision collision)
    {
        debugStayCount++;
        HandleImpact(collision);
    }

    void HandleImpact(Collision collision)
    {
        if (!rippleOnCollision) return;
        if (WaterRippleSpawner.Instance == null) return;
        if (Time.time - _lastImpactTime < impactCooldown) return;
        if (collision.contactCount == 0) return;

        ContactPoint contact = collision.GetContact(0);

        Vector3 myVel = ApproachVelocity;
        Vector3 otherVel = Vector3.zero;
        ShipImpactSensor other;
        if (collision.collider != null && _lookup.TryGetValue(collision.collider, out other) && other != null)
            otherVel = other.ApproachVelocity;
        else if (collision.rigidbody != null)
            otherVel = collision.rigidbody.linearVelocity;

        Vector3 relative = myVel - otherVel;

        // 접촉면 법선 방향으로 얼마나 파고들었는가 = 실제 충돌 세기
        float closing = Mathf.Abs(Vector3.Dot(relative, contact.normal));
        closing = Mathf.Max(closing, collision.relativeVelocity.magnitude);

        debugLastImpactSpeed = closing;
        if (closing > debugPeakImpactSpeed) debugPeakImpactSpeed = closing;

        if (closing < WaterRippleSpawner.Instance.minImpactSpeed) return;

        // 실제로 파문을 만들 때만 쿨다운을 건다.
        _lastImpactTime = Time.time;
        debugSpawnCount++;
        Vector3 impactDir = relative.sqrMagnitude > 0.0001f ? relative.normalized : transform.forward;
        WaterRippleSpawner.Instance.SpawnFromImpact(contact.point, closing, impactDir, impactAspect);
    }

    /// <summary>포탄 명중 등, 외부에서 충격을 직접 넣고 싶을 때.</summary>
    public void ReportImpact(Vector3 worldPos, float impactSpeed)
    {
        if (WaterRippleSpawner.Instance == null) return;
        WaterRippleSpawner.Instance.SpawnFromImpact(worldPos, impactSpeed);
    }

    void OnDrawGizmosSelected()
    {
        if (_autoOffsets == null) return;
        Quaternion flatRot = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        Gizmos.color = new Color(1f, 1f, 1f, 0.6f);
        for (int i = 0; i < _autoOffsets.Length; i++)
            Gizmos.DrawWireSphere(transform.position + flatRot * _autoOffsets[i], wakeRadiusMin * 0.5f);
    }
}
