using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 피해를 입은 배가 타들어가는 모습.
///
/// 표면은 전용 셰이더(Haesin/ShipBurn)가 두 갈래로 나눠 그린다.
///   숯   - 그을음 텍스처의 R 채널(넓은 얼룩)을 따라 표면이 검게 탄다.
///   불씨 - B 채널(가는 갈라진 틈)을 따라 벌겋게 빛난다.
///          숯이 된 자리에서만 빛나므로, 배가 통째로 붉어지지 않고
///          숯 사이 갈라진 틈만 달아오른 숯덩이처럼 보인다.
///
/// 불 파티클은 런타임에 만들지 않는다. 배 안에 미리 넣어 두고 꺼 놓은 것을
/// 단계에 맞춰 켜기만 한다. (World 시뮬레이션 파티클을 생성하면 프리팹이
/// 원래 있던 좌표에서 한 프레임 뿜은 뒤 날아오는 문제가 있었다)
/// </summary>
[RequireComponent(typeof(ShipHealth))]
public class ShipBurnVisual : MonoBehaviour
{
    public enum Mode
    {
        /// <summary>전용 셰이더로 갈아끼운다.</summary>
        BurnShader,
        /// <summary>원본 HDRP/Lit 을 유지한 채 디테일 맵으로만 그을린다.</summary>
        LitDetailOverlay
    }

    [Header("방식")]
    public Mode mode = Mode.BurnShader;
    [Tooltip("전용 셰이더. 비우면 Haesin/ShipBurn 을 찾는다.")]
    public Shader burnShader;

    [Header("숯 (넓은 그을음)")]
    [Tooltip("그을음 텍스처. 비우면 코드가 하나 만들어 쓴다. R=그을음 얼룩, B=갈라진 틈.")]
    public Texture2D burnMap;
    [Tooltip("무늬 반복 횟수. 낮출수록 얼룩이 커져 반복되는 티가 줄어든다.")]
    public float burnTiling = 3f;
    [Tooltip("숯이 번지기 시작하는 문턱. 낮출수록 조금만 맞아도 검어진다.")]
    public float burnBias = 0.82f;
    [Tooltip("숯 경계의 선명함.")]
    public float burnSharpness = 3f;
    [Tooltip("숯 색. 기본 텍스처에 곱해진다. 어두울수록 새까맣게 탄다.")]
    public Color charColor = new Color(0.05f, 0.043f, 0.04f, 1f);
    [Tooltip("디테일 맵 방식에서만 쓰는 어두워지는 세기.")]
    [Range(0f, 2f)] public float charStrength = 1.5f;

    [Header("불씨 (갈라진 틈)")]
    [Tooltip("불씨 색조.")]
    public Color emberColor = new Color(0.749f, 0.149f, 0.039f, 1f);
    [Tooltip("불씨 밝기. 낮 하늘(노출 14EV)에 묻히지 않으려면 수천 단위가 필요하다.")]
    public float emberIntensity = 3969f;
    [Tooltip("불씨가 나타나기 시작하는 문턱. 높을수록 아주 깊은 틈만 달아오른다.")]
    public float emberBias = 1.02f;
    [Tooltip("불씨 가장자리의 선명함. 높을수록 틈이 가늘고 또렷해진다.")]
    public float emberSharpness = 6f;
    [Tooltip("불씨 세기 배수.")]
    public float emberBoost = 3929.45f;
    [Tooltip("불씨가 일렁이는 속도.")]
    public float emberFlickerSpeed = 4.5f;
    [Tooltip("숯 경계가 살아 움직이는 폭. 0이면 무늬가 고정된다.")]
    public float burnCreep = 0.04f;
    [Tooltip("숯 경계가 움직이는 속도.")]
    public float burnCreepSpeed = 0.3f;

    [Header("불 파티클 (배 안에 미리 넣어 둔 것)")]
    [Tooltip("배 안에 꺼 둔 불 오브젝트들. 손상 단계만큼 앞에서부터 켠다. 비우면 DamageFX 자식에서 찾는다.")]
    public GameObject[] fireObjects;

    [Header("상태 (읽기 전용)")]
    public float burnAmount;
    public int materialsInstanced;
    public int firesOn;
    public string activeMode = "-";

    private ShipHealth _health;
    private readonly List<Material> _instances = new List<Material>();
    private readonly List<Color> _baseColors = new List<Color>();
    private bool _built;
    private int _shownStage = -1;
    private bool _usingShader;
    private float _creep;

    static readonly int IdBaseColor    = Shader.PropertyToID("_BaseColor");
    static readonly int IdBaseMap      = Shader.PropertyToID("_BaseColorMap");
    static readonly int IdBurnMap      = Shader.PropertyToID("_BurnMap");
    static readonly int IdBurnAmount   = Shader.PropertyToID("_BurnAmount");
    static readonly int IdBurnBias     = Shader.PropertyToID("_BurnBias");
    static readonly int IdBurnSharp    = Shader.PropertyToID("_BurnSharpness");
    static readonly int IdBurnTiling   = Shader.PropertyToID("_BurnTiling");
    static readonly int IdCharColor    = Shader.PropertyToID("_CharColor");
    static readonly int IdEmberColor   = Shader.PropertyToID("_EmberColor");
    static readonly int IdEmberBias    = Shader.PropertyToID("_EmberBias");
    static readonly int IdEmberSharp   = Shader.PropertyToID("_EmberSharpness");
    static readonly int IdEmberBoost   = Shader.PropertyToID("_EmberBoost");
    static readonly int IdSmoothness   = Shader.PropertyToID("_Smoothness");
    static readonly int IdMetallic     = Shader.PropertyToID("_Metallic");
    static readonly int IdDetailMap    = Shader.PropertyToID("_DetailMap");
    static readonly int IdDetailAlbedo = Shader.PropertyToID("_DetailAlbedoScale");
    static readonly int IdDetailSmooth = Shader.PropertyToID("_DetailSmoothnessScale");
    static readonly int IdEmissive     = Shader.PropertyToID("_EmissiveColor");
    static readonly int IdEmissiveExp  = Shader.PropertyToID("_EmissiveExposureWeight");

    void Awake()
    {
        _health = GetComponent<ShipHealth>();
        if (burnShader == null) burnShader = Shader.Find("Haesin/ShipBurn");
        CollectFires();
        SetFireCount(0);
    }

    void OnEnable() { if (_health != null) _health.OnStageChanged += OnStageChanged; }
    void OnDisable() { if (_health != null) _health.OnStageChanged -= OnStageChanged; }

    void OnDestroy()
    {
        for (int i = 0; i < _instances.Count; i++)
            if (_instances[i] != null) Destroy(_instances[i]);
        _instances.Clear();
    }

    void OnStageChanged(int stage, float damage01)
    {
        burnAmount = damage01;
        if (damage01 <= 0.001f && !_built) { SetFireCount(0); return; }

        EnsureInstances();
        ApplyBurn(damage01);

        if (stage != _shownStage)
        {
            _shownStage = stage;
            SetFireCount(stage);
        }
    }

    void Update()
    {
        if (!_built || burnAmount <= 0.02f) return;
        // 문턱을 아주 느리게 흔들면 숯 경계가 조금씩 번져 나가는 것처럼 보인다.
        _creep = (Mathf.PerlinNoise(Time.time * burnCreepSpeed, 11.3f) - 0.5f) * 2f * burnCreep;
        ApplyBurn(burnAmount);
        ApplyFlicker();
    }

    // ---------- 불 파티클: 켜고 끄기만 ----------

    void CollectFires()
    {
        if (fireObjects != null && fireObjects.Length > 0) return;

        Transform holder = transform.Find("DamageFX");
        if (holder == null) return;

        var list = new List<GameObject>();
        for (int i = 0; i < holder.childCount; i++) list.Add(holder.GetChild(i).gameObject);
        fireObjects = list.ToArray();
    }

    /// <summary>앞에서부터 count 개만 켠다.</summary>
    void SetFireCount(int count)
    {
        if (fireObjects == null) { firesOn = 0; return; }

        int on = 0;
        for (int i = 0; i < fireObjects.Length; i++)
        {
            if (fireObjects[i] == null) continue;
            bool want = i < count;
            if (fireObjects[i].activeSelf != want) fireObjects[i].SetActive(want);
            if (want) on++;
        }
        firesOn = on;
    }

    // ---------- 머티리얼 ----------

    void EnsureInstances()
    {
        if (_built) return;
        _built = true;

        if (burnMap == null) burnMap = BuildDefaultBurnMap();
        _usingShader = mode == Mode.BurnShader && burnShader != null;
        activeMode = _usingShader ? "BurnShader" : "LitDetailOverlay";

        var map = new Dictionary<Material, Material>();
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer) continue;

            var mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                Material m = mats[i];
                if (m == null) continue;

                string n = m.name.ToLower();
                if (n.Contains("flag") || n.Contains("flg")) continue;   // 깃발은 태우지 않는다
                if (!m.HasProperty(IdBaseColor)) continue;

                Material inst;
                if (!map.TryGetValue(m, out inst))
                {
                    inst = _usingShader ? MakeBurnMaterial(m) : MakeDetailMaterial(m);
                    if (inst == null) continue;
                    map[m] = inst;
                    _instances.Add(inst);
                    _baseColors.Add(m.GetColor(IdBaseColor));
                }
                mats[i] = inst;
                changed = true;
            }
            if (changed) r.sharedMaterials = mats;
        }
        materialsInstanced = _instances.Count;
    }

    Material MakeBurnMaterial(Material src)
    {
        var m = new Material(burnShader);
        m.name = src.name + " (Burn)";

        if (src.HasProperty(IdBaseMap)) m.SetTexture(IdBaseMap, src.GetTexture(IdBaseMap));
        m.SetColor(IdBaseColor, src.GetColor(IdBaseColor));
        if (src.HasProperty(IdSmoothness)) m.SetFloat(IdSmoothness, src.GetFloat(IdSmoothness));
        if (src.HasProperty(IdMetallic)) m.SetFloat(IdMetallic, src.GetFloat(IdMetallic));

        m.SetTexture(IdBurnMap, burnMap);
        m.SetFloat(IdBurnAmount, 0f);

        // new Material() 은 HDRP 의 머티리얼 검증을 안 거쳐서 컬링이 Back 으로 남는다.
        if (m.HasProperty("_CullMode")) m.SetFloat("_CullMode", 0f);
        if (m.HasProperty("_CullModeForward")) m.SetFloat("_CullModeForward", 0f);
        if (m.HasProperty("_DoubleSidedEnable")) m.SetFloat("_DoubleSidedEnable", 1f);
        m.EnableKeyword("_DOUBLESIDED_ON");
        m.doubleSidedGI = true;
        return m;
    }

    Material MakeDetailMaterial(Material src)
    {
        if (!src.HasProperty(IdDetailMap)) return null;

        var m = new Material(src);
        m.name = src.name + " (Burn)";
        m.EnableKeyword("_DETAIL_MAP");
        m.SetTexture(IdDetailMap, burnMap);
        m.SetTextureScale("_DetailMap", new Vector2(burnTiling, burnTiling));
        if (m.HasProperty("_UVDetail")) m.SetFloat("_UVDetail", 0f);
        if (m.HasProperty("_UVDetailsMappingMask")) m.SetVector("_UVDetailsMappingMask", new Vector4(1f, 0f, 0f, 0f));
        if (m.HasProperty("_LinkDetailsWithBase")) m.SetFloat("_LinkDetailsWithBase", 0f);
        if (m.HasProperty(IdEmissiveExp)) m.SetFloat(IdEmissiveExp, 0f);
        return m;
    }

    void ApplyBurn(float t)
    {
        float k = Mathf.Clamp01(t);
        for (int i = 0; i < _instances.Count; i++)
        {
            Material m = _instances[i];
            if (m == null) continue;

            if (_usingShader)
            {
                // 매번 다시 넣는다. 그래야 플레이 중 인스펙터에서 만진 값이 바로 반영된다.
                m.SetFloat(IdBurnAmount, k);
                m.SetFloat(IdBurnBias, burnBias - _creep);
                m.SetFloat(IdBurnSharp, burnSharpness);
                m.SetFloat(IdBurnTiling, burnTiling);
                m.SetColor(IdCharColor, charColor);
                m.SetFloat(IdEmberBias, emberBias);
                m.SetFloat(IdEmberSharp, emberSharpness);
                m.SetFloat(IdEmberBoost, emberBoost);
            }
            else
            {
                m.SetFloat(IdDetailAlbedo, -charStrength * k);
                if (m.HasProperty(IdDetailSmooth)) m.SetFloat(IdDetailSmooth, -Mathf.Clamp01(k * 1.2f));
                Color b = _baseColors[i];
                Color target = new Color(b.r * charColor.r, b.g * charColor.g, b.b * charColor.b, b.a);
                m.SetColor(IdBaseColor, Color.Lerp(b, target, k * 0.7f));
            }
        }
    }

    void ApplyFlicker()
    {
        float flicker = 0.78f + 0.22f * Mathf.PerlinNoise(Time.time * emberFlickerSpeed, 0.37f);
        Color e = emberColor * (emberIntensity * flicker);

        for (int i = 0; i < _instances.Count; i++)
        {
            Material m = _instances[i];
            if (m == null) continue;

            if (_usingShader) m.SetColor(IdEmberColor, e);
            else if (m.HasProperty(IdEmissive))
            {
                float k = Mathf.InverseLerp(0.45f, 1f, burnAmount);
                m.SetColor(IdEmissive, emberColor * (emberIntensity * k * flicker));
            }
        }
    }

    // ---------- 그을음 텍스처 ----------

    /// <summary>
    /// 그을음 텍스처를 만든다. 채널마다 역할이 다르다.
    ///   R - 넓은 그을음 얼룩. 낮은 주파수라 타일링을 낮추면 크게 번진다.
    ///   B - 가는 갈라진 틈. 능선 노이즈를 높이 세워 얇은 금만 남긴다.
    ///       불씨는 이 금을 따라서만 빛난다.
    /// </summary>
    static Texture2D BuildDefaultBurnMap()
    {
        const int S = 512;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, true);
        tex.name = "BurnSoot (generated)";
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;

        var px = new Color32[S * S];
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                float u = x / (float)S, v = y / (float)S;

                // R: 넓은 그을음 얼룩. 주파수를 낮게 잡아야 크게 번진다.
                float f = Mathf.PerlinNoise(u * 2f, v * 2f) * 0.55f
                        + Mathf.PerlinNoise(u * 5f, v * 5f) * 0.30f
                        + Mathf.PerlinNoise(u * 13f, v * 13f) * 0.15f;
                float soot = Mathf.Clamp01((f - 0.16f) * 2.3f);

                // B: 갈라진 틈. 능선의 꼭대기만 남겨 가는 금을 만든다.
                // 여기서 값이 굵으면 배가 통째로 붉어지므로, 면적의 60% 이상은 0 이어야 한다.
                float r1 = 1f - Mathf.Abs(Mathf.PerlinNoise(u * 14f, v * 14f) * 2f - 1f);
                float r2 = 1f - Mathf.Abs(Mathf.PerlinNoise(u * 31f, v * 31f) * 2f - 1f);
                float c1 = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.86f, 0.995f, r1));
                float c2 = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.90f, 0.998f, r2));
                // 큰 무늬로 한 번 더 눌러, 배 전체가 고르게 달아오르지 않고 부위마다 다르게 탄다.
                float big = 0.55f + 0.45f * Mathf.PerlinNoise(u * 3f + 17f, v * 3f + 17f);
                float crack = Mathf.Clamp01(Mathf.Max(c1, c2 * 0.8f) * big);

                px[y * S + x] = new Color32((byte)(soot * 255f), 128, (byte)(crack * 255f), 128);
            }
        }
        tex.SetPixels32(px);
        tex.Apply(true);
        return tex;
    }
}
