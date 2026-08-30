using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// 배를 수면에 띄우고 파도를 타게 한다. 두 가지 방식을 지원한다.
///
/// Physics 모드 (MoveControl 처럼 힘으로 움직이는 배)
///   높이는 수면을 목표로 하는 스프링-댐퍼 힘,
///   기울기는 파도 기울기를 목표로 하는 토크로 맞춘다.
///   위치나 회전을 직접 쓰지 않으므로 추진력·충돌 물리와 싸우지 않는다.
///   가속도에 상한을 두어 어떤 파도에서도 배가 솟구치지 않는다.
///
/// Transform 모드 (EnemyAI 처럼 transform 으로 직접 움직이는 배)
///   예전처럼 Y 높이와 기울기를 직접 써 준다.
/// </summary>
[DefaultExecutionOrder(100)]
public class ShipBuoyancy : MonoBehaviour
{
    public enum Mode { Auto, Physics, Transform }

    [Header("동작 방식")]
    [Tooltip("Auto: 움직이는(비 kinematic) Rigidbody 가 있으면 Physics, 없으면 Transform.")]
    public Mode mode = Mode.Auto;

    [Header("수면")]
    [Tooltip("비워두면 씬에서 자동으로 찾는다.")]
    public WaterSurface targetSurface;

    [Header("선체 샘플 지점")]
    [Tooltip("앞뒤 샘플 거리(m). 0이면 콜라이더 크기에서 자동 계산.")]
    public float lengthSpan = 0f;
    [Tooltip("좌우 샘플 거리(m). 0이면 콜라이더 크기에서 자동 계산.")]
    public float widthSpan = 0f;
    [Tooltip("흘수 - 수면보다 이만큼 아래에 뜬다(m).")]
    public float draft = 0.5f;

    [Header("Physics 모드 - 높이")]
    [Tooltip("수면으로 끌어당기는 힘(1/s^2). 클수록 딱딱하게 수면에 붙는다.")]
    public float heaveStiffness = 20f;
    [Tooltip("위아래 출렁임 감쇠(1/s). 대략 2*sqrt(heaveStiffness) 가 임계 감쇠다.")]
    public float heaveDamping = 8f;
    [Tooltip("높이 보정 가속도 상한(m/s^2). 배가 솟구치지 않게 하는 안전장치.")]
    public float maxHeaveAccel = 30f;
    [Tooltip("수직 속도 상한(m/s).")]
    public float maxVerticalSpeed = 8f;

    [Header("Physics 모드 - 기울기")]
    [Tooltip("파도 기울기를 따라가는 힘.")]
    public float tiltStiffness = 8f;
    [Tooltip("기울기 흔들림 감쇠.")]
    public float tiltDamping = 4f;
    [Tooltip("최대 기울기(도).")]
    public float maxTiltPhysics = 14f;
    [Range(0f, 1f)]
    [Tooltip("0이면 파도를 무시하고 평평, 1이면 파도 기울기를 그대로 따른다.")]
    public float tiltStrengthPhysics = 0.7f;

    [Header("Transform 모드")]
    public float heightLerp = 6f;
    public float rotationLerp = 3f;
    public float maxTilt = 12f;
    [Range(0f, 1f)] public float tiltStrength = 0.7f;

    [Header("디버그 (읽기 전용)")]
    public float debugWaterY;
    public float debugHeightError;
    public float debugTiltDeg;

    private Rigidbody _rb;
    private bool _usePhysics;
    private bool _initialised;
    private float _smoothedY;
    private bool _hasY;

    void Start()
    {
        _rb = GetComponent<Rigidbody>();
        if (targetSurface == null) targetSurface = WaterHeightUtil.FindSurface();
        ComputeSpans();

        _usePhysics = mode == Mode.Physics
            || (mode == Mode.Auto && _rb != null && !_rb.isKinematic);
        if (_rb == null) _usePhysics = false;

        if (_rb != null)
        {
            // 중력은 쓰지 않는다. 수면을 목표로 하는 스프링이 높이를 완전히 책임진다.
            _rb.useGravity = false;
            if (_usePhysics) _rb.constraints = RigidbodyConstraints.None;
        }
    }

    void ComputeSpans()
    {
        if (lengthSpan > 0f && widthSpan > 0f) { _initialised = true; return; }

        Bounds b = new Bounds(transform.position, Vector3.zero);
        bool found = false;
        Collider col = GetComponent<Collider>();
        if (col != null) { b = col.bounds; found = true; }
        else
        {
            Renderer[] rs = GetComponentsInChildren<Renderer>();
            for (int i = 0; i < rs.Length; i++)
            {
                if (!found) { b = rs[i].bounds; found = true; }
                else b.Encapsulate(rs[i].bounds);
            }
        }

        if (found)
        {
            if (lengthSpan <= 0f) lengthSpan = Mathf.Max(b.size.x, b.size.z) * 0.4f;
            if (widthSpan <= 0f) widthSpan = Mathf.Min(b.size.x, b.size.z) * 0.4f;
        }
        else
        {
            if (lengthSpan <= 0f) lengthSpan = 5f;
            if (widthSpan <= 0f) widthSpan = 2f;
        }
        _initialised = true;
    }

    /// <summary>선체 앞뒤/좌우 네 지점의 수면 높이를 잰다.</summary>
    void SampleWater(Vector3 pos, float yaw, out float hF, out float hB, out float hR, out float hL)
    {
        Quaternion flat = Quaternion.Euler(0f, yaw, 0f);
        Vector3 f = flat * Vector3.forward;
        Vector3 r = flat * Vector3.right;
        hF = WaterHeightUtil.SampleHeight(targetSurface, pos + f * lengthSpan);
        hB = WaterHeightUtil.SampleHeight(targetSurface, pos - f * lengthSpan);
        hR = WaterHeightUtil.SampleHeight(targetSurface, pos + r * widthSpan);
        hL = WaterHeightUtil.SampleHeight(targetSurface, pos - r * widthSpan);
    }

    void FixedUpdate()
    {
        if (!_usePhysics) return;
        if (!EnsureSurface()) return;
        if (!_initialised) ComputeSpans();

        Vector3 pos = _rb.position;
        float yaw = _rb.rotation.eulerAngles.y;

        float hF, hB, hR, hL;
        SampleWater(pos, yaw, out hF, out hB, out hR, out hL);
        float avg = (hF + hB + hR + hL) * 0.25f;
        debugWaterY = avg;

        // ---- 높이: 수면을 목표로 하는 스프링-댐퍼 ----
        float targetY = avg - draft;
        float error = targetY - pos.y;
        debugHeightError = error;

        float vy = _rb.linearVelocity.y;
        float accel = error * heaveStiffness - vy * heaveDamping;
        accel = Mathf.Clamp(accel, -maxHeaveAccel, maxHeaveAccel);
        _rb.AddForce(Vector3.up * accel, ForceMode.Acceleration);

        // 안전장치
        Vector3 v = _rb.linearVelocity;
        if (Mathf.Abs(v.y) > maxVerticalSpeed)
        {
            v.y = Mathf.Sign(v.y) * maxVerticalSpeed;
            _rb.linearVelocity = v;
        }

        // ---- 기울기: 파도 기울기를 목표로 하는 토크 ----
        float pitch = Mathf.Atan2(hB - hF, lengthSpan * 2f) * Mathf.Rad2Deg;
        float roll = Mathf.Atan2(hR - hL, widthSpan * 2f) * Mathf.Rad2Deg;
        pitch = Mathf.Clamp(pitch * tiltStrengthPhysics, -maxTiltPhysics, maxTiltPhysics);
        roll = Mathf.Clamp(roll * tiltStrengthPhysics, -maxTiltPhysics, maxTiltPhysics);

        Quaternion target = Quaternion.Euler(pitch, yaw, roll);
        Quaternion delta = target * Quaternion.Inverse(_rb.rotation);

        float angle;
        Vector3 axis;
        delta.ToAngleAxis(out angle, out axis);
        if (angle > 180f) angle -= 360f;

        if (!float.IsInfinity(axis.x) && !float.IsNaN(axis.x) && Mathf.Abs(angle) > 0.01f)
        {
            Vector3 torque = axis.normalized * (angle * Mathf.Deg2Rad * tiltStiffness);
            torque -= _rb.angularVelocity * tiltDamping;
            // Y축 선회는 MoveControl 이 담당하므로 건드리지 않는다.
            torque.y = 0f;
            _rb.AddTorque(torque, ForceMode.Acceleration);
        }

        debugTiltDeg = Vector3.Angle(transform.up, Vector3.up);
    }

    void LateUpdate()
    {
        if (_usePhysics) return;
        if (!EnsureSurface()) return;
        if (!_initialised) ComputeSpans();

        float yaw = transform.eulerAngles.y;
        Vector3 pos = transform.position;

        float hF, hB, hR, hL;
        SampleWater(pos, yaw, out hF, out hB, out hR, out hL);
        float avg = (hF + hB + hR + hL) * 0.25f;
        debugWaterY = avg;

        float targetY = avg - draft;
        if (!_hasY) { _smoothedY = targetY; _hasY = true; }
        _smoothedY = Mathf.Lerp(_smoothedY, targetY, 1f - Mathf.Exp(-heightLerp * Time.deltaTime));

        float pitch = Mathf.Clamp(Mathf.Atan2(hB - hF, lengthSpan * 2f) * Mathf.Rad2Deg * tiltStrength, -maxTilt, maxTilt);
        float roll = Mathf.Clamp(Mathf.Atan2(hR - hL, widthSpan * 2f) * Mathf.Rad2Deg * tiltStrength, -maxTilt, maxTilt);

        pos.y = _smoothedY;
        transform.position = pos;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(pitch, yaw, roll),
                                              1f - Mathf.Exp(-rotationLerp * Time.deltaTime));
        debugTiltDeg = Vector3.Angle(transform.up, Vector3.up);
    }

    bool EnsureSurface()
    {
        if (targetSurface != null) return true;
        targetSurface = WaterHeightUtil.FindSurface();
        return targetSurface != null;
    }
}
