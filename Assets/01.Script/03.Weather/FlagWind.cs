using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// 깃발(Cloth)을 날씨 바람에 맞춰 휘날리게 하고, 배가 회전할 때 폴대에 감기는 것을 막는다.
///
/// 감기는 원인: Cloth 의 worldVelocityScale / worldAccelerationScale 이 켜져 있으면
/// 배의 월드 이동·회전이 천에 그대로 누적된다. damping 이 0 이면 그 회전이 풀리지 않고
/// 계속 쌓여서 깃발이 폴대를 칭칭 감는다.
/// 그래서 worldAccelerationScale 만 낮추고(회전이 감김의 주범),
/// 약한 damping 과 bendingStiffness 로 감긴 것이 풀리게 하며,
/// 방향이 뚜렷한 바람(externalAcceleration)으로 깃발이 바람 아래로 뻗게 만든다.
/// 펄럭임은 randomAcceleration(돌풍)이 담당한다.
/// </summary>
[DefaultExecutionOrder(140)]
public class FlagWind : MonoBehaviour
{
    [Header("대상 (비우면 자식에서 자동 수집)")]
    public Cloth[] cloths;

    [Header("참조 (비우면 자동 탐색)")]
    public WeatherSystem weather;
    public WaterSurface ocean;

    [Header("감김 방지")]
    [Tooltip("배의 월드 이동·회전을 천에서 분리한다. 끄면 원래처럼 감길 수 있다.")]
    public bool decoupleFromShipMotion = true;
    [Range(0f, 1f)] public float worldVelocityScale = 0.5f;
    [Range(0f, 1f)] public float worldAccelerationScale = 0.3f;
    [Tooltip("천의 감쇠. 0이면 한 번 생긴 회전이 풀리지 않는다.")]
    [Range(0f, 1f)] public float damping = 0.1f;
    [Tooltip("접히는 것에 대한 저항. 0이면 쉽게 말린다.")]
    [Range(0f, 1f)] public float bendingStiffness = 0.08f;
    [Range(0f, 1f)] public float stretchingStiffness = 1f;
    [Range(0f, 1f)] public float friction = 0.5f;

    [Header("바람 세기")]
    [Tooltip("바람이 거의 없을 때의 가속도(m/s^2). 중력 9.81 보다 작으면 축 늘어진다.")]
    public float windAtCalm = 14f;
    [Tooltip("최대 바람일 때의 가속도. 크게 줄수록 격렬하게 휘날린다.")]
    public float windAtStorm = 90f;
    [Tooltip("이 바람 세기에서 최대가 된다. 폭풍우 프리셋의 windSpeed 와 맞추면 된다.")]
    public float referenceWindSpeed = 140f;
    [Tooltip("바람 세기 곡선. 1이면 직선, 2~3이면 약한 바람에서 더 약하게 나와 깃발이 축 처진다.")]
    [Range(1f, 4f)]
    public float windCurvePower = 2.5f;
    [Tooltip("배가 빠르게 달릴 때 생기는 맞바람의 비중.")]
    public float shipSpeedContribution = 1.6f;

    [Header("돌풍 (펄럭임)")]
    [Tooltip("바람 세기 대비 불규칙한 흔들림의 비율.")]
    [Range(0f, 3f)] public float gustStrength = 1.3f;
    [Tooltip("돌풍이 바뀌는 빠르기.")]
    public float gustFrequency = 2.5f;

    [Header("바람 방향")]
    [Tooltip("바다의 바람 방향(Distant Wind Orientation)을 따라간다.")]
    public bool useOceanWindDirection = true;
    [Tooltip("위 옵션이 꺼져 있을 때 쓸 월드 방향.")]
    public Vector3 fixedWindDirection = Vector3.forward;

    [Header("상태 (읽기 전용)")]
    public float debugWindSpeed;
    public float debugAccel;
    public Vector3 debugWindDir;

    private Rigidbody _rb;
    private Vector3 _lastPos;
    private float _noiseSeed;

    void OnEnable()
    {
        _rb = GetComponent<Rigidbody>();
        _lastPos = transform.position;
        _noiseSeed = Random.value * 100f;

        // 지정한 깃발만 건드린다. 비어 있으면 아무것도 하지 않는다.
        if (cloths == null || cloths.Length == 0)
            Debug.LogWarning("[FlagWind] Cloths 가 비어 있습니다. 제어할 깃발을 지정하세요.");

        if (weather == null) weather = Object.FindFirstObjectByType<WeatherSystem>();
        if (ocean == null) ocean = Object.FindFirstObjectByType<WaterSurface>();

        ApplyClothSettings();
    }

    /// <summary>감김 방지용 기본 설정을 천에 적용한다.</summary>
    public void ApplyClothSettings()
    {
        if (cloths == null) return;
        for (int i = 0; i < cloths.Length; i++)
        {
            var c = cloths[i];
            if (c == null) continue;

            if (decoupleFromShipMotion)
            {
                c.worldVelocityScale = worldVelocityScale;
                c.worldAccelerationScale = worldAccelerationScale;
            }
            c.damping = damping;
            c.bendingStiffness = bendingStiffness;
            c.stretchingStiffness = stretchingStiffness;
            c.friction = friction;
            c.useGravity = true;

            // 이미 쌓여 있던 회전을 한 번 털어 준다.
            c.ClearTransformMotion();
        }
    }

    /// <summary>이미 감긴 깃발을 즉시 초기화한다.</summary>
    public void ResetFlags()
    {
        if (cloths == null) return;
        for (int i = 0; i < cloths.Length; i++)
            if (cloths[i] != null) cloths[i].ClearTransformMotion();
    }

    void Update()
    {
        if (cloths == null || cloths.Length == 0) return;

        // ---- 바람 세기 ----
        float windSpeed = 0f;
        if (ocean != null) windSpeed = ocean.largeWindSpeed;   // 날씨 시스템이 매 프레임 여기에 써 준다
        debugWindSpeed = windSpeed;

        // 직선 보간이면 약한 바람에서도 값이 커서 깃발이 계속 뻗어 있다.
        // 거듭제곱을 씌워 바람이 약할 때는 중력이 이기도록 한다.
        float k = Mathf.Clamp01(windSpeed / Mathf.Max(1f, referenceWindSpeed));
        k = Mathf.Pow(k, windCurvePower);
        float accel = Mathf.Lerp(windAtCalm, windAtStorm, k);

        // ---- 바람 방향 ----
        Vector3 dir;
        if (useOceanWindDirection && ocean != null)
        {
            float deg = ocean.largeOrientationValue;
            dir = Quaternion.Euler(0f, deg, 0f) * Vector3.forward;
        }
        else
        {
            dir = fixedWindDirection.sqrMagnitude > 0.0001f ? fixedWindDirection.normalized : Vector3.forward;
        }

        // ---- 배가 달리면서 생기는 맞바람 ----
        Vector3 shipVel;
        if (_rb != null) shipVel = _rb.linearVelocity;
        else
        {
            shipVel = Time.deltaTime > 0f ? (transform.position - _lastPos) / Time.deltaTime : Vector3.zero;
        }
        _lastPos = transform.position;
        shipVel.y = 0f;

        Vector3 wind = dir * accel - shipVel * shipSpeedContribution;
        debugWindDir = wind.normalized;
        debugAccel = wind.magnitude;

        // ---- 돌풍 ----
        float t = Time.time * gustFrequency + _noiseSeed;
        float gx = (Mathf.PerlinNoise(t, 0.13f) - 0.5f) * 2f;
        float gy = (Mathf.PerlinNoise(0.37f, t) - 0.5f) * 2f;
        float gz = (Mathf.PerlinNoise(t * 0.7f, t * 0.5f) - 0.5f) * 2f;
        Vector3 gust = new Vector3(gx, gy * 0.5f, gz) * (accel * gustStrength);

        for (int i = 0; i < cloths.Length; i++)
        {
            var c = cloths[i];
            if (c == null) continue;
            c.externalAcceleration = wind;
            c.randomAcceleration = new Vector3(Mathf.Abs(gust.x), Mathf.Abs(gust.y), Mathf.Abs(gust.z));
        }
    }
}
