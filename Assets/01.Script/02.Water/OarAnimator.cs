using UnityEngine;

/// <summary>
/// 노 젓기 애니메이션 + 노를 저을 때의 순간 가속(서지).
///
/// 구조 전제: 배 아래 OarGroup / Left, Right 가 있고 그 자식 하나하나가 노다.
/// 노의 로컬 X 회전만 구동하고 Y/Z 는 원래 값을 그대로 둔다.
/// (우현 노는 보통 Y=180 으로 뒤집혀 있어서 함께 건드리면 방향이 망가진다)
///
/// 각도 범위는 "앞 ~ 뒤" 순서다. 앞(catch) 이 노를 앞으로 뻗은 상태,
/// 뒤(release) 가 다 젓고 난 상태다.
///
/// 한 주기 = 드라이브(앞→뒤, 물을 젓는 구간) + 리커버리(뒤→앞, 노를 빼서 되돌리는 구간)
/// 젓는 속도는 배의 실제 속도를 따라간다.
/// </summary>
[DefaultExecutionOrder(130)]
public class OarAnimator : MonoBehaviour
{
    [Header("노 그룹 (비우면 OarGroup/Left, OarGroup/Right 자동 탐색)")]
    public Transform leftGroup;
    public Transform rightGroup;

    [Header("좌현 각도 (앞 ~ 뒤)")]
    public float leftForwardAngle = 0f;
    public float leftBackAngle = 10f;

    [Header("우현 각도 (앞 ~ 뒤)")]
    public float rightForwardAngle = 0f;
    public float rightBackAngle = 10f;

    [Header("젓는 속도")]
    [Tooltip("멈춰 있을 때의 초당 젓는 횟수.")]
    public float idleStrokeRate = 0.25f;
    [Tooltip("최고 속도에서의 초당 젓는 횟수.")]
    public float maxStrokeRate = 0.9f;
    [Tooltip("이 속도(m/s)에서 최대가 된다. 0이면 MoveControl 의 Max Speed 를 쓴다.")]
    public float referenceSpeed = 0f;
    [Tooltip("한 주기에서 물을 젓는 구간의 비율. 나머지는 노를 되돌리는 구간.")]
    [Range(0.15f, 0.85f)]
    public float driveFraction = 0.4f;
    [Tooltip("후진할 때 노 젓는 방향을 뒤집는다.")]
    public bool reverseWhenBackward = true;

    [Header("연출")]
    [Tooltip("노마다 위상을 조금씩 어긋나게 해서 물결치듯 보이게 한다. 0이면 완전히 같이 젓는다.")]
    [Range(0f, 0.3f)]
    public float phaseOffsetPerOar = 0f;
    [Tooltip("선회할 때 바깥쪽 노를 더 빨리 젓는다. 0이면 사용 안 함.")]
    [Range(0f, 1f)]
    public float turnDifferential = 0f;

    [Header("서지 (노를 저을 때 순간 가속)")]
    [Tooltip("물리로 움직이는 배에서만 동작한다.")]
    public bool enableSurge = true;
    [Tooltip("젓는 순간의 최대 추가 가속도(m/s^2).")]
    public float surgeAccel = 2.2f;
    [Tooltip("켜면 리커버리 구간에 같은 양을 덜어내 평균 속도는 그대로 두고 '출렁이는 느낌'만 만든다. 끄면 실제로 조금 빨라진다.")]
    public bool zeroMeanSurge = true;

    [Header("상태 (읽기 전용)")]
    public float currentStrokeRate;
    public float currentPhase;
    public float debugSurge;
    public float debugSpeed;

    private Transform[] _left;
    private Transform[] _right;
    private Vector3[] _leftBaseEuler;
    private Vector3[] _rightBaseEuler;

    private Rigidbody _rb;
    private MoveControl _move;
    private Vector3 _lastPos;
    private float _speed;        // 부호 있는 전진 속도
    private float _phase;        // 0~1
    private float _phaseL, _phaseR;

    void OnEnable()
    {
        _rb = GetComponent<Rigidbody>();
        _move = GetComponent<MoveControl>();
        _lastPos = transform.position;
        Collect();
    }

    void Collect()
    {
        if (leftGroup == null || rightGroup == null)
        {
            Transform og = null;
            foreach (var tr in GetComponentsInChildren<Transform>(true))
                if (tr.name == "OarGroup") { og = tr; break; }
            if (og != null)
            {
                if (leftGroup == null) leftGroup = og.Find("Left");
                if (rightGroup == null) rightGroup = og.Find("Right");
            }
        }

        _left = Grab(leftGroup, out _leftBaseEuler);
        _right = Grab(rightGroup, out _rightBaseEuler);
    }

    Transform[] Grab(Transform group, out Vector3[] baseEuler)
    {
        if (group == null) { baseEuler = new Vector3[0]; return new Transform[0]; }
        int n = group.childCount;
        Transform[] arr = new Transform[n];
        baseEuler = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            arr[i] = group.GetChild(i);
            // 원래 Y/Z 를 기억해 둔다. X 만 우리가 굴린다.
            baseEuler[i] = arr[i].localEulerAngles;
        }
        return arr;
    }

    void Update()
    {
        // ---- 배 속도 측정 ----
        if (_move != null)
        {
            _speed = _move.currentSpeed;
        }
        else if (Time.deltaTime > 0f)
        {
            Vector3 d = transform.position - _lastPos;
            d.y = 0f;
            Vector3 fwd = transform.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude > 0.0001f) fwd.Normalize();
            _speed = Vector3.Dot(d / Time.deltaTime, fwd);
        }
        _lastPos = transform.position;
        debugSpeed = _speed;

        // ---- 젓는 속도 ----
        float refSpeed = referenceSpeed > 0.01f ? referenceSpeed
                       : (_move != null ? _move.maxSpeed : 6f);
        float k = Mathf.Clamp01(Mathf.Abs(_speed) / Mathf.Max(0.01f, refSpeed));
        currentStrokeRate = Mathf.Lerp(idleStrokeRate, maxStrokeRate, k);

        float dir = (reverseWhenBackward && _speed < -0.05f) ? -1f : 1f;

        // 선회 시 좌우 차등
        float steer = 0f;
        if (turnDifferential > 0f && _rb != null)
            steer = Mathf.Clamp(_rb.angularVelocity.y * Mathf.Rad2Deg / 25f, -1f, 1f);

        float rateL = currentStrokeRate * (1f + steer * turnDifferential);
        float rateR = currentStrokeRate * (1f - steer * turnDifferential);

        _phase = Mathf.Repeat(_phase + currentStrokeRate * dir * Time.deltaTime, 1f);
        _phaseL = Mathf.Repeat(_phaseL + rateL * dir * Time.deltaTime, 1f);
        _phaseR = Mathf.Repeat(_phaseR + rateR * dir * Time.deltaTime, 1f);
        currentPhase = _phase;

        ApplySide(_left, _leftBaseEuler, _phaseL, leftForwardAngle, leftBackAngle);
        ApplySide(_right, _rightBaseEuler, _phaseR, rightForwardAngle, rightBackAngle);
    }

    void ApplySide(Transform[] oars, Vector3[] baseEuler, float phase, float forwardAngle, float backAngle)
    {
        if (oars == null) return;
        for (int i = 0; i < oars.Length; i++)
        {
            if (oars[i] == null) continue;
            float p = Mathf.Repeat(phase + i * phaseOffsetPerOar, 1f);
            float t = StrokeCurve(p);                       // 0 = 앞, 1 = 뒤
            float angle = Mathf.Lerp(forwardAngle, backAngle, t);
            Vector3 e = baseEuler[i];
            oars[i].localEulerAngles = new Vector3(angle, e.y, e.z);
        }
    }

    /// <summary>주기 위상(0~1)을 0(앞)~1(뒤) 진행도로 바꾼다.</summary>
    float StrokeCurve(float p)
    {
        float d = Mathf.Clamp(driveFraction, 0.05f, 0.95f);
        if (p < d)
        {
            // 드라이브: 앞 -> 뒤 (물을 젓는 구간, 상대적으로 빠르다)
            return Mathf.SmoothStep(0f, 1f, p / d);
        }
        // 리커버리: 뒤 -> 앞 (노를 빼서 되돌리는 구간)
        return Mathf.SmoothStep(1f, 0f, (p - d) / (1f - d));
    }

    void FixedUpdate()
    {
        debugSurge = 0f;
        if (!enableSurge) return;
        if (_rb == null || _rb.isKinematic) return;
        if (_move == null) return;      // 물리로 움직이는 배에서만

        float d = Mathf.Clamp(driveFraction, 0.05f, 0.95f);
        float pulse;

        if (_phase < d)
        {
            // 드라이브 구간: 반주기 사인 모양으로 밀어 준다
            float u = _phase / d;
            pulse = Mathf.Sin(u * Mathf.PI);
        }
        else
        {
            // 리커버리 구간: 평균을 0으로 맞추기 위해 조금 덜어낸다
            if (!zeroMeanSurge) { return; }
            float meanDrive = (2f / Mathf.PI) * d;          // 드라이브 구간의 평균 기여
            pulse = -meanDrive / (1f - d);
        }

        // 후진 중이면 반대로
        float sign = (_speed < -0.05f) ? -1f : 1f;

        Vector3 fwd = transform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) return;
        fwd.Normalize();

        float a = surgeAccel * pulse * sign;
        debugSurge = a;
        _rb.AddForce(fwd * a, ForceMode.Acceleration);
    }
}
