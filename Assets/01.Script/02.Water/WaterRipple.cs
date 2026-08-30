using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// 파문 한 발. 바깥으로 퍼지면서 진폭이 줄고 거품이 뒤따라 남는다.
///
/// 원형이 강제는 아니다. aspect 로 세로:가로 비율을 주면 선체처럼 길쭉한 타원이 되고,
/// forward 를 주면 그 방향으로 정렬된다. aspect = 1 이면 기존처럼 원형이다.
/// </summary>
[RequireComponent(typeof(WaterDeformer))]
public class WaterRipple : MonoBehaviour
{
    [Tooltip("파문이 최종적으로 퍼지는 반지름(m). 가로 기준.")]
    public float maxRadius = 30f;
    [Tooltip("퍼지는 데 걸리는 시간(초).")]
    public float duration = 2.2f;
    [Tooltip("솟아오르는 높이(m).")]
    public float amplitude = 0.6f;
    [Tooltip("거품이 남는 정도.")]
    public float foamStrength = 0.5f;

    [Tooltip("세로(진행 방향) 대 가로 비율. 1이면 원, 2면 진행 방향으로 두 배 길쭉해진다.")]
    public float aspect = 1f;

    [Tooltip("퍼져나가는 속도 곡선.")]
    public AnimationCurve radiusCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("진폭 감쇠 곡선.")]
    public AnimationCurve amplitudeCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    private WaterDeformer _deformer;
    private float _t;

    void Awake()
    {
        _deformer = GetComponent<WaterDeformer>();
    }

    /// <summary>원형 파문(기존 호출과 호환).</summary>
    public void Play(Vector3 worldPos, float strength, float radius)
    {
        Play(worldPos, strength, radius, 1f, Vector3.forward);
    }

    /// <summary>
    /// 방향과 비율을 지정한 파문.
    /// aspect 가 1보다 크면 forward 방향으로 길쭉해진다.
    /// </summary>
    public void Play(Vector3 worldPos, float strength, float radius, float rippleAspect, Vector3 forward)
    {
        transform.position = new Vector3(worldPos.x, transform.position.y, worldPos.z);

        Vector3 flat = new Vector3(forward.x, 0f, forward.z);
        if (flat.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
        else
            transform.rotation = Quaternion.identity;

        amplitude = strength;
        maxRadius = radius;
        aspect = Mathf.Max(0.05f, rippleAspect);
        _t = 0f;
        gameObject.SetActive(true);
        Apply(0f);
    }

    void OnEnable()
    {
        _t = 0f;
    }

    void Update()
    {
        _t += Time.deltaTime;
        float n = duration <= 0f ? 1f : Mathf.Clamp01(_t / duration);
        Apply(n);
        if (n >= 1f) gameObject.SetActive(false);
    }

    void Apply(float n)
    {
        if (_deformer == null) return;

        float r = Mathf.Max(0.01f, radiusCurve.Evaluate(n) * maxRadius);

        // regionSize.x = 가로(좌우), regionSize.y = 세로(진행 방향)
        _deformer.regionSize = new Vector2(r * 2f, r * 2f * aspect);
        _deformer.amplitude = amplitudeCurve.Evaluate(n) * amplitude;

        // 거품은 진폭보다 늦게 사라지게 해서 흰 자국이 남도록 한다.
        float foamFade = Mathf.Clamp01(1f - n * 0.75f) * foamStrength;
        _deformer.surfaceFoamDimmer = foamFade;
        _deformer.deepFoamDimmer = foamFade * 0.6f;
    }
}
