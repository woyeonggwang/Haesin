using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 힘 기반 배 조종.
///
/// 예전 버전은 transform.Translate / eulerAngles 로 배를 직접 옮겼기 때문에
/// Rigidbody 의 속도가 항상 0 이었고, 그래서 충돌 시 반작용도 충격량도 없었다.
/// 이제는 추진력과 조타 토크를 Rigidbody 에 넣어 물리로 움직인다.
/// 당파(충각) 시 질량 차이에 따라 서로 밀리고 선체가 돌아간다.
///
/// 축 분담:
///   물리   - 수평(XZ) 위치, Y축 회전(선수 방향)
///   부력   - Y 높이, 앞뒤/좌우 기울기 (ShipBuoyancy 가 담당)
/// 부력도 힘으로 주기 때문에 축을 잠글 필요가 없다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class MoveControl : MonoBehaviour
{
    [Header("선체")]
    [Tooltip("배의 질량(kg). 당파 시 어느 쪽이 밀리는지를 결정한다.")]
    public float shipMass = 1000f;

    [Header("추진")]
    [Tooltip("전진 가속도(m/s^2).")]
    public float forwardAccel = 6f;
    [Tooltip("후진 가속도(m/s^2). 노를 거꾸로 젓는 것이라 전진보다 약하다.")]
    public float reverseAccel = 2.5f;
    [Tooltip("전진 최고 속도(m/s).")]
    public float maxSpeed = 10f;
    [Tooltip("후진 최고 속도(m/s).")]
    public float maxReverseSpeed = 4f;

    [Header("조타")]
    [Tooltip("선회 가속도(도/s^2).")]
    public float turnAccel = 70f;
    [Tooltip("최대 선회 속도(도/s).")]
    public float maxTurnRate = 26f;
    [Tooltip("이 속도 미만이면 방향타가 거의 듣지 않는다(m/s). 배는 멈춰 있으면 못 돈다.")]
    public float rudderMinSpeed = 0.4f;
    [Tooltip("이 속도에서 방향타가 100% 듣는다(m/s).")]
    public float rudderFullSpeed = 4f;
    [Tooltip("후진 중에는 조타 방향이 반대가 된다.")]
    public bool invertSteerWhenReversing = true;

    [Header("물의 저항")]
    [Tooltip("전후 방향 감속. 키를 놓았을 때 얼마나 빨리 멈추는지.")]
    public float linearDrag = 0.6f;
    [Tooltip("선회 감속. 클수록 방향타를 놓았을 때 빨리 멈춘다.")]
    public float angularDrag = 2.2f;
    [Tooltip("옆미끄러짐 억제. 배는 옆으로 잘 밀리지 않으므로 크게 준다.")]
    public float lateralGrip = 4f;

    [Header("부스트")]
    public float boostMultiplier = 1.6f;
    [Tooltip("부스트가 최대까지 오르는 시간(초).")]
    public float boostRiseTime = 2f;
    [Tooltip("부스트가 풀리는 시간(초).")]
    public float boostFallTime = 1.5f;
    [Tooltip("부스트 게이지 UI. 없어도 된다.")]
    public Image boostGauge;

    [Header("상태 (읽기 전용)")]
    public bool boost = false;
    [Tooltip("현재 전진 속도(m/s). 뒤로 가면 음수.")]
    public float currentSpeed;
    [Tooltip("현재 선회 속도(도/s).")]
    public float currentTurnRate;
    [Range(0f, 1f)] public float boostLevel;

    [Header("정박 (하차 중)")]
    [Tooltip("정박 중: 입력을 무시하고, 정박한 자리·방향으로 약하게 끌어당겨 파도에 떠내려가지 않게 한다. BoardingSystem 이 SetDocked 로 켜고 끈다.")]
    public bool docked;
    [Tooltip("정박 위치로 끌어당기는 세기(1/s²). 파도·충돌로 밀려도 천천히 돌아온다.")]
    public float anchorStiffness = 0.6f;
    [Tooltip("정박 중 추가 수평 감속(1/s).")]
    public float anchorDamping = 1.5f;
    [Tooltip("정박 방향으로 되돌리는 세기(1/s²).")]
    public float anchorYawStiffness = 0.8f;

    private Rigidbody _rb;
    private float _inputThrottle;   // -1 ~ 1
    private float _inputSteer;      // -1 ~ 1
    private Vector3 _anchorPos; private float _anchorYaw;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        SetupRigidbody();
    }

    void SetupRigidbody()
    {
        _rb.mass = shipMass;
        _rb.linearDamping = 0f;                 // 저항은 아래에서 직접 계산한다
        _rb.angularDamping = 0f;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        // 높이와 기울기는 ShipBuoyancy 가 부력(힘)으로 처리한다.
        // 축을 잠그거나 위치를 직접 쓰면 추진력과 충돌 물리가 상쇄되므로 여기서는 아무것도 잠그지 않는다.
        _rb.constraints = RigidbodyConstraints.None;
    }

    void OnValidate()
    {
        if (_rb != null) _rb.mass = shipMass;
    }

    void Update()
    {
        // 부스트 게이지
        float target = (boost && _inputThrottle > 0f) ? 1f : 0f;
        float rate = target > boostLevel
            ? (boostRiseTime > 0f ? Time.deltaTime / boostRiseTime : 1f)
            : (boostFallTime > 0f ? Time.deltaTime / boostFallTime : 1f);
        boostLevel = Mathf.MoveTowards(boostLevel, target, rate);

        if (boostGauge != null) boostGauge.fillAmount = boostLevel;
    }

    void FixedUpdate()
    {
        if (_rb == null) return;

        Vector3 fwd = transform.forward;
        fwd.y = 0f;
        fwd.Normalize();
        Vector3 right = transform.right;
        right.y = 0f;
        right.Normalize();

        Vector3 vel = _rb.linearVelocity;
        Vector3 flatVel = new Vector3(vel.x, 0f, vel.z);

        currentSpeed = Vector3.Dot(flatVel, fwd);
        currentTurnRate = _rb.angularVelocity.y * Mathf.Rad2Deg;

        // ---- 추진 ----
        float boostFactor = 1f + (boostMultiplier - 1f) * boostLevel;
        float speedCap = _inputThrottle >= 0f ? maxSpeed * boostFactor : maxReverseSpeed;

        if (Mathf.Abs(_inputThrottle) > 0.01f)
        {
            float accel = _inputThrottle > 0f ? forwardAccel * boostFactor : reverseAccel;
            // 이미 최고 속도면 더 밀지 않는다
            bool canPush = _inputThrottle > 0f ? currentSpeed < speedCap : currentSpeed > -maxReverseSpeed;
            if (canPush)
                _rb.AddForce(fwd * accel * _inputThrottle, ForceMode.Acceleration);
        }

        // ---- 전후 저항 ----
        _rb.AddForce(-fwd * currentSpeed * linearDrag, ForceMode.Acceleration);

        // ---- 옆미끄러짐 억제 ----
        float lateral = Vector3.Dot(flatVel, right);
        _rb.AddForce(-right * lateral * lateralGrip, ForceMode.Acceleration);

        // ---- 조타 ----
        // 배는 물을 밀어야 방향타가 듣는다. 속도가 없으면 거의 못 돈다.
        float speedForRudder = Mathf.Abs(currentSpeed);
        float authority = Mathf.Clamp01(Mathf.InverseLerp(rudderMinSpeed, rudderFullSpeed, speedForRudder));

        float steer = _inputSteer;
        if (invertSteerWhenReversing && currentSpeed < -0.1f) steer = -steer;

        if (Mathf.Abs(steer) > 0.01f && authority > 0f)
        {
            if (Mathf.Abs(currentTurnRate) < maxTurnRate)
                _rb.AddTorque(Vector3.up * turnAccel * Mathf.Deg2Rad * steer * authority, ForceMode.Acceleration);
        }

        // ---- 선회 저항 ----
        _rb.AddTorque(-Vector3.up * _rb.angularVelocity.y * angularDrag, ForceMode.Acceleration);

        // ---- 정박: 제자리로 끌어당김 ----
        if (docked)
        {
            Vector3 d = _anchorPos - _rb.position; d.y = 0f;
            _rb.AddForce(d * anchorStiffness - flatVel * anchorDamping, ForceMode.Acceleration);
            float dyaw = Mathf.DeltaAngle(_rb.rotation.eulerAngles.y, _anchorYaw) * Mathf.Deg2Rad;
            _rb.AddTorque(Vector3.up * dyaw * anchorYawStiffness, ForceMode.Acceleration);
        }
    }

    /// <summary>
    /// 정박 켜기/끄기. 하차 중에는 MoveControl 을 끄지 않고 이것을 켠다.
    /// (예전엔 하차 때 MoveControl 을 꺼서 물 저항 계산까지 멈춰(Rigidbody 저항 0) 배가 파도에 떠내려갔고,
    ///  currentSpeed 가 끄기 직전 값으로 남아 OarAnimator 가 계속 노를 저었음 — 2026-09-25 확인.
    ///  또 PlayerInput(Send Messages) 은 꺼진 컴포넌트에도 OnMove 를 보내서, 걸을 때 누른 WASD 가 _inputThrottle 에 들어갔음.)
    /// </summary>
    public void SetDocked(bool on)
    {
        docked = on;
        Halt();
        if (on && _rb != null) { _anchorPos = _rb.position; _anchorYaw = _rb.rotation.eulerAngles.y; }
    }

    // ===== Input System (PlayerInput / Send Messages) =====

    private void OnMove(InputValue value)
    {
        Vector2 input = value.Get<Vector2>();
        if (docked || !enabled) input = Vector2.zero; // 하차 중 걷기 WASD 가 배로 들어오지 않게
        _inputThrottle = input.y;
        _inputSteer = input.x;
    }

    private void OnBoost(InputValue value)
    {
        boost = value.isPressed && !docked && enabled;
    }

    /// <summary>외부에서 강제로 멈추고 싶을 때.</summary>
    public void Halt()
    {
        _inputThrottle = 0f;
        _inputSteer = 0f;
        boost = false;
        currentSpeed = 0f; currentTurnRate = 0f; // OarAnimator 가 읽는 값 — 안 지우면 멈춘 뒤에도 노를 저음
        if (_rb != null)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }
    }
}
