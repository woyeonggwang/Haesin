using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// 배 모양(직사각형) 거품을 선체에 붙여 두고, 속도에 따라 세기를 조절한다.
///
/// 원형 파문을 여러 개 뿌리는 방식과 달리, HDRP 의 Water Foam Generator 는
/// 수면의 거품 버퍼에 계속 그려 넣기 때문에 배가 지나간 자리에 거품이 그대로 남는다.
/// 즉 항적이 저절로 선체 모양으로 뒤에 깔린다. HDRP 물 샘플의 배가 쓰는 방식이다.
/// </summary>
[ExecuteAlways]
[DefaultExecutionOrder(120)]
public class ShipWakeFoam : MonoBehaviour
{
    [Header("선체 크기 (0이면 렌더러에서 자동 계산)")]
    [Tooltip("선체 폭(m).")]
    public float hullWidth = 0f;
    [Tooltip("선체 길이(m).")]
    public float hullLength = 0f;
    [Tooltip("계산된 크기에 곱하는 배수. 1보다 크면 선체보다 넓게 거품이 난다.")]
    public float widthScale = 1.15f;
    public float lengthScale = 1.1f;

    [Header("속도에 따른 세기")]
    [Tooltip("이 속도(m/s) 이하에서는 정박 중 세기를 쓴다.")]
    public float minSpeed = 0.5f;
    [Tooltip("이 속도에서 최대 세기가 된다.")]
    public float fullSpeed = 12f;
    [Tooltip("멈춰 있을 때의 거품 세기. 0이면 정박 중엔 거품이 없다.")]
    public float amplitudeIdle = 0.15f;
    [Tooltip("전속 항해 시 거품 세기.")]
    public float amplitudeFull = 2.2f;

    [Header("항적 늘어짐")]
    [Tooltip("빠를수록 거품 영역이 뒤로 얼마나 길어지는지(배수).")]
    public float lengthStretchAtFullSpeed = 1.8f;
    [Tooltip("거품 영역을 선체 중심에서 뒤로 얼마나 밀지(선체 길이 대비 비율).")]
    public float aftOffsetAtFullSpeed = 0.35f;

    [Header("해상도")]
    public int resolution = 128;

    private WaterFoamGenerator _gen;
    private Transform _genTr;
    private Vector3 _lastPos;
    private float _speed;

    void Reset()
    {
        hullWidth = 0f;
        hullLength = 0f;
    }

    void OnEnable()
    {
        _lastPos = transform.position;
        EnsureGenerator();
    }

    void EnsureGenerator()
    {
        if (_gen != null) return;

        Transform existing = transform.Find("WakeFoam");
        GameObject go;
        if (existing != null) go = existing.gameObject;
        else
        {
            go = new GameObject("WakeFoam");
            go.transform.SetParent(transform, false);
        }
        _genTr = go.transform;

        _gen = go.GetComponent<WaterFoamGenerator>();
        if (_gen == null) _gen = go.AddComponent<WaterFoamGenerator>();

        MeasureHull();

        _gen.type = WaterFoamGeneratorType.Rectangle;   // 원형(Disk)이 아니라 선체 모양
        _gen.scaleMode = DecalScaleMode.ScaleInvariant; // 배의 큰 localScale 에 끌려가지 않게
        _gen.resolution = new Vector2Int(resolution, resolution);
        _gen.regionSize = new Vector2(hullWidth * widthScale, hullLength * lengthScale);
        _gen.amplitude = amplitudeIdle;
        _gen.surfaceFoamDimmer = 1f;
        _gen.deepFoamDimmer = 1f;
    }

    void MeasureHull()
    {
        if (hullWidth > 0f && hullLength > 0f) return;

        bool first = true;
        Bounds local = new Bounds();
        Renderer[] rs = GetComponentsInChildren<Renderer>();
        for (int i = 0; i < rs.Length; i++)
        {
            Bounds wb = rs[i].bounds;
            for (int c = 0; c < 8; c++)
            {
                Vector3 corner = new Vector3(
                    (c & 1) == 0 ? wb.min.x : wb.max.x,
                    (c & 2) == 0 ? wb.min.y : wb.max.y,
                    (c & 4) == 0 ? wb.min.z : wb.max.z);
                Vector3 lp = transform.InverseTransformPoint(corner);
                if (first) { local = new Bounds(lp, Vector3.zero); first = false; }
                else local.Encapsulate(lp);
            }
        }

        float s = Mathf.Abs(transform.localScale.x) < 0.0001f ? 1f : transform.localScale.x;
        if (first)
        {
            if (hullWidth <= 0f) hullWidth = 6f;
            if (hullLength <= 0f) hullLength = 20f;
        }
        else
        {
            if (hullWidth <= 0f) hullWidth = local.size.x * s;
            if (hullLength <= 0f) hullLength = local.size.z * s;
        }
    }

    void LateUpdate()
    {
        if (_gen == null) EnsureGenerator();
        if (_gen == null) return;

        if (Time.deltaTime > 0f)
        {
            Vector3 d = transform.position - _lastPos;
            d.y = 0f;
            _speed = d.magnitude / Time.deltaTime;
        }
        _lastPos = transform.position;

        float k = Mathf.Clamp01(Mathf.InverseLerp(minSpeed, fullSpeed, _speed));

        _gen.amplitude = Mathf.Lerp(amplitudeIdle, amplitudeFull, k);

        float stretch = Mathf.Lerp(1f, lengthStretchAtFullSpeed, k);
        _gen.regionSize = new Vector2(hullWidth * widthScale, hullLength * lengthScale * stretch);

        // 배가 기울어도 거품 영역은 수평을 유지하고, 빠를수록 뒤로 밀어 항적처럼 보이게 한다.
        float yaw = transform.eulerAngles.y;
        Quaternion flat = Quaternion.Euler(0f, yaw, 0f);
        Vector3 aft = flat * Vector3.back * (hullLength * aftOffsetAtFullSpeed * k);

        _genTr.position = new Vector3(transform.position.x + aft.x, transform.position.y, transform.position.z + aft.z);
        _genTr.rotation = flat;
    }

    void OnDrawGizmosSelected()
    {
        if (_genTr == null) return;
        Gizmos.color = new Color(1f, 1f, 1f, 0.5f);
        Gizmos.matrix = Matrix4x4.TRS(_genTr.position, _genTr.rotation, Vector3.one);
        Vector2 r = _gen != null ? _gen.regionSize : new Vector2(hullWidth, hullLength);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(r.x, 0.1f, r.y));
    }
}
