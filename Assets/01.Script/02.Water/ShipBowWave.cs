using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// 뱃머리가 물을 가르며 좌우로 밀어내는 V자 파도.
///
/// 주의: WaterDeformer 컴포넌트는 Unity 6 에서 deprecated 이고,
/// Awake() 에서 한 번만 내부 머티리얼로 변환된다. 그래서 AddComponent 뒤에
/// type 을 BowWave 로 바꿔도 이미 늦어서 반영되지 않는다.
/// 여기서는 HDRP 가 내부적으로 쓰는 "HDRP/Water/Water Decal Sample" 셰이더를
/// 직접 머티리얼로 만들어 WaterDecal 에 물린다. 덕분에 HDRP 기본 변환에서는
/// 꺼져 있는 BowWave 의 거품(_AffectFoam)까지 켤 수 있다.
/// </summary>
[ExecuteAlways]
[DefaultExecutionOrder(121)]
public class ShipBowWave : MonoBehaviour
{
    // HDRP Water Decal Sample 셰이더의 _TYPE 값
    private const float TYPE_SPHERE = 0f;
    private const float TYPE_BOX = 1f;
    private const float TYPE_BOWWAVE = 2f;
    private const float TYPE_SHOREWAVE = 3f;
    private const float TYPE_TEXTURE = 4f;

    [Header("선체 크기 (0이면 렌더러에서 자동 계산)")]
    public float hullWidth = 0f;
    public float hullLength = 0f;

    [Header("V자 크기")]
    [Tooltip("선체 폭 대비 V가 얼마나 넓게 벌어질지.")]
    public float widthScale = 3.2f;
    [Tooltip("선체 길이 대비 V가 얼마나 길게 뻗을지.")]
    public float lengthScale = 2f;
    [Tooltip("뱃머리 위치. 선체 길이 대비 앞뒤 오프셋.")]
    [Range(-1f, 1f)]
    public float bowOffset = 0.25f;
    [Tooltip("V자 방향이 반대로 나오면 체크한다. BowWave 는 꼭짓점이 뒤쪽에서 앞으로 벌어지는 모양이라 배에는 뒤집어 써야 한다.")]
    public bool flip = true;
    [Tooltip("V자의 꼭짓점이 뱃머리에 오도록 위치를 자동 계산한다. 끄면 Bow Offset 을 직접 조절한다.")]
    public bool autoPlaceApexAtBow = true;

    [Header("속도에 따른 세기")]
    public float minSpeed = 1f;
    public float fullSpeed = 12f;
    [Tooltip("멈춰 있을 때의 파고(m).")]
    public float amplitudeIdle = 0f;
    [Tooltip("전속에서의 파고(m).")]
    public float amplitudeFull = 2.2f;
    [Tooltip("V자 바깥쪽이 솟는 높이 비율. 클수록 좌우로 갈라지는 물마루가 뚜렷해진다.")]
    [Range(0f, 2f)]
    public float elevation = 1.4f;

    [Header("거품")]
    [Tooltip("HDRP 기본 BowWave 는 거품이 없다. 켜면 갈라지는 물마루에 흰 거품이 생긴다.")]
    public bool affectFoam = true;
    [Tooltip("거품이 생기는 구간(0~1).")]
    public Vector2 deepFoamRange = new Vector2(0.1f, 0.6f);
    public Vector2 breakingRange = new Vector2(0.4f, 0.9f);
    [Range(0f, 1f)] public float surfaceFoam = 1f;
    [Range(0f, 1f)] public float deepFoam = 1f;

    [Header("해상도")]
    public int resolution = 128;

    private WaterDecal _decal;
    private Transform _tr;
    private Material _mat;
    private Vector3 _lastPos;
    private float _speed;

    void OnEnable()
    {
        _lastPos = transform.position;
        EnsureDecal();
    }

    void OnDisable()
    {
        if (_mat != null)
        {
            if (Application.isPlaying) Destroy(_mat);
            else DestroyImmediate(_mat);
            _mat = null;
        }
    }

    static Shader FindDecalShader()
    {
        // HDRP 내부 리소스에서 Water Decal Sample 셰이더를 가져온다.
        System.Type resT = null;
        foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = a.GetType("UnityEngine.Rendering.HighDefinition.WaterSystemRuntimeResources");
            if (t != null) { resT = t; break; }
        }
        if (resT != null)
        {
            var m = typeof(GraphicsSettings).GetMethod("GetRenderPipelineSettings", System.Type.EmptyTypes);
            if (m != null)
            {
                var res = m.MakeGenericMethod(resT).Invoke(null, null);
                var pr = resT.GetProperty("waterDecalMigrationShader");
                if (pr != null)
                {
                    var sh = pr.GetValue(res, null) as Shader;
                    if (sh != null) return sh;
                }
            }
        }
        return Shader.Find("HDRP/Water/Water Decal Sample");
    }

    void EnsureDecal()
    {
        if (_decal != null && _mat != null) return;

        Transform existing = transform.Find("BowWave");
        GameObject go;
        if (existing != null) go = existing.gameObject;
        else
        {
            go = new GameObject("BowWave");
            go.transform.SetParent(transform, false);
        }
        _tr = go.transform;

        // 예전에 붙어 있던 deprecated 컴포넌트가 있으면 치운다.
        var legacy = go.GetComponent<WaterDeformer>();
        if (legacy != null)
        {
            if (Application.isPlaying) Destroy(legacy);
            else DestroyImmediate(legacy, true);
        }

        _decal = go.GetComponent<WaterDecal>();
        if (_decal == null) _decal = go.AddComponent<WaterDecal>();

        MeasureHull();

        Shader sh = FindDecalShader();
        if (sh == null)
        {
            Debug.LogError("[ShipBowWave] Water Decal Sample 셰이더를 찾지 못했습니다.");
            return;
        }

        _mat = new Material(sh);
        _mat.name = "BowWave_" + gameObject.name;
        _mat.hideFlags = HideFlags.DontSave;
        ApplyMaterial();

        _decal.scaleMode = DecalScaleMode.ScaleInvariant;
        _decal.resolution = new Vector2Int(resolution, resolution);
        _decal.updateMode = CustomRenderTextureUpdateMode.Realtime;
        _decal.material = _mat;
        _decal.regionSize = new Vector2(hullWidth * widthScale, hullLength * lengthScale);
        _decal.amplitude = amplitudeIdle;
        _decal.surfaceFoamDimmer = surfaceFoam;
        _decal.deepFoamDimmer = deepFoam;
    }

    void ApplyMaterial()
    {
        if (_mat == null) return;
        _mat.SetFloat("_TYPE", TYPE_BOWWAVE);
        _mat.SetFloat("_AffectDeformation", 1f);
        _mat.SetFloat("_AffectFoam", affectFoam ? 1f : 0f);
        _mat.SetFloat("_Elevation", elevation);
        _mat.SetVector("_Deep_Foam_Range", deepFoamRange);
        _mat.SetVector("_Breaking_Range", breakingRange);

        // 이 셰이더는 _TYPE 값이 아니라 키워드로 분기한다. 키워드를 켜지 않으면
        // _TYPE 을 아무리 넣어도 기본값(Sphere)으로 그려진다.
        _mat.DisableKeyword("_TYPE_SPHERE");
        _mat.DisableKeyword("_TYPE_BOX");
        _mat.DisableKeyword("_TYPE_SHORE_WAVE");
        _mat.DisableKeyword("_TYPE_TEXTURE");
        _mat.EnableKeyword("_TYPE_BOW_WAVE");
        _mat.EnableKeyword("_AFFECTS_DEFORMATION");
        if (affectFoam) _mat.EnableKeyword("_AFFECTS_FOAM");
        else _mat.DisableKeyword("_AFFECTS_FOAM");
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
        if (_decal == null || _mat == null) EnsureDecal();
        if (_decal == null || _mat == null) return;

        if (Time.deltaTime > 0f)
        {
            Vector3 d = transform.position - _lastPos;
            d.y = 0f;
            _speed = d.magnitude / Time.deltaTime;
        }
        _lastPos = transform.position;

        float k = Mathf.Clamp01(Mathf.InverseLerp(minSpeed, fullSpeed, _speed));

        if (autoPlaceApexAtBow && hullLength > 0.001f)
        {
            // 뒤집힌 상태에서 꼭짓점은 영역의 앞쪽 끝에 온다.
            float halfRegion = hullLength * lengthScale * 0.5f;
            bowOffset = (hullLength * 0.5f - halfRegion) / hullLength;
        }

        ApplyMaterial();
        _decal.regionSize = new Vector2(hullWidth * widthScale, hullLength * lengthScale);
        _decal.amplitude = Mathf.Lerp(amplitudeIdle, amplitudeFull, k);
        _decal.surfaceFoamDimmer = surfaceFoam * k;
        _decal.deepFoamDimmer = deepFoam * k;

        // 배가 파도에 기울어도 V자는 수면과 평행하게 유지한다.
        float yaw = transform.eulerAngles.y + (flip ? 180f : 0f);
        Quaternion flat = Quaternion.Euler(0f, yaw, 0f);
        Vector3 fwd = Quaternion.Euler(0f, transform.eulerAngles.y, 0f) * Vector3.forward;
        Vector3 bow = fwd * (hullLength * bowOffset);

        _tr.position = new Vector3(transform.position.x + bow.x, transform.position.y, transform.position.z + bow.z);
        _tr.rotation = flat;
    }

    void OnDrawGizmosSelected()
    {
        if (_tr == null || _decal == null) return;
        Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.7f);
        Gizmos.matrix = Matrix4x4.TRS(_tr.position, _tr.rotation, Vector3.one);
        Vector2 r = _decal.regionSize;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(r.x, 0.1f, r.y));
    }
}
