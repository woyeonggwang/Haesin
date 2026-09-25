using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.UI;

/// <summary>
/// 메뉴 안의 3D 뷰어. 전용 카메라가 하늘 높이(stagePosition) 에 둔 전시용 복제본을 RenderTexture 로 그리고,
/// RawImage 위에서 좌우 드래그하면 복제본이 Y 축으로 돈다. 게임이 멈춰 있어도(timeScale 0) 동작한다.
///
/// 2026-09-19 (함선 메뉴 개편):
///   - fullScreen = true 면 RenderTexture 를 **화면 해상도·비율에 맞춰** 만든다. 정사각 RT 를 전체 화면으로
///     늘리면 배가 가로로 늘어나 보이므로. 화면 크기가 바뀌면 RT 와 프레이밍을 다시 잡는다.
///   - backdrop(Background.png) 은 **카메라 자식 쿼드**로 3D 안에 세운다. HDRP 카메라는 알파 0 으로 지워도
///     RenderTexture 알파가 1 로 나와(확인됨) 뒤의 UI 이미지가 비치지 않기 때문에, 배경을 RT 안에 함께 그린다.
///     쿼드는 절두체를 꽉 채우고 텍스처는 비율을 유지한 채 잘라 채운다(cover).
///   - horizontalFreeFraction / verticalFreeFraction: 화면에서 UI 에 가리지 않는 영역 비율. 그 안에 대상이
///     들어가도록 카메라 거리를 잡아 좌측 그리드·우측 패널과 배가 겹치지 않게 한다.
///   - 드래그는 마우스 아래 가장 위 UI 가 뷰어일 때만 시작한다(버튼·패널 조작 중 배가 같이 돌지 않게).
/// </summary>
public class ShowcaseViewer : MonoBehaviour
{
    public Vector3 stagePosition = new Vector3(20000f, 600f, 20000f); // 월드에서 멀리, 안개 최대 높이(350) 위
    [Tooltip("fullScreen 이 false 일 때 쓰는 정사각 RT 한 변 길이.")]
    public int textureSize = 1024;
    [Tooltip("true 면 화면 해상도·비율에 맞춘 RT 를 만든다(전체 화면 배경 합성용).")]
    public bool fullScreen;
    [Tooltip("전체 화면 배경 그림. 비우면 단색 배경.")]
    public Texture2D backdrop;
    [Tooltip("RT 가로 최대 픽셀(성능 상한).")]
    public int maxTextureWidth = 2560;
    [Tooltip("화면 가로 중 UI 에 가리지 않는 비율. 이 폭 안에 대상이 들어가도록 거리를 잡는다.")]
    [Range(0.2f, 1f)] public float horizontalFreeFraction = 1f;
    [Tooltip("화면 세로 중 UI 에 가리지 않는 비율.")]
    [Range(0.2f, 1f)] public float verticalFreeFraction = 1f;
    [Tooltip("대상을 화면 가로 어느 지점에 놓을지(0=왼쪽 끝, 0.5=가운데, 1=오른쪽 끝). 좌·우 UI 폭이 다를 때 쓴다.")]
    [Range(0f, 1f)] public float viewportCenterX = 0.5f;

    [Tooltip("드래그 1px 당 회전 각도.")] public float dragDegreesPerPixel = 0.4f;
    [Tooltip("카메라가 대상을 보는 높이 각도(도).")] public float cameraElevation = 12f;
    [Tooltip("대상 크기 대비 카메라 거리 배율.")] public float distanceScale = 0.9f;
    [Tooltip("뷰어 카메라 고정 노출(EV). 햇빛 아래 야외 ≈ 13~14.")] public float fixedExposure = 13.5f;
    [Tooltip("배경 그림 밝기 배수. HDRP/Unlit 은 이 카메라 설정에서 원본보다 어둡게 나와 1.7 로 보정한다(측정값).")] public float backdropBrightness = 1.7f;

    [Header("전시용 조명 (캐릭터 뷰어용)")]
    [Tooltip("켜면 스테이지에 전용 스포트라이트 3개(키·보조·역광)를 둔다. 햇빛만으로는 날씨·해 방향에 따라 캐릭터가 거의 검게 나왔음(2026-09-25, 인벤토리).")]
    public bool stageLights;
    [Tooltip("대상 위치에서의 조도(lux). 고정 노출 13.5 EV 기준.")] public float keyLux = 30000f;
    public float fillLux = 10000f;
    public float rimLux = 15000f;

    public RenderTexture Texture { get; private set; }
    public RawImage view;   // 드래그 판정용 (메뉴가 지정)

    Camera _cam;
    Transform _stage;      // 회전하는 부모
    GameObject _subject;   // 전시 복제본
    Transform _backdropQuad;
    Material _backdropMat;
    float _yaw;
    bool _dragging;
    Vector2 _lastMouse;
    int _rtW, _rtH;
    float _radius;
    Light _key, _fill, _rim;

    static readonly string[] StripChildren = { "WakeFoam", "BowWave", "DamageFX", "Splashes", "CameraPivot", "HullWaterAudio", "OarAudio" };

    void Awake()
    {
        var stageGo = new GameObject("ShowcaseStage_" + name);
        _stage = stageGo.transform; _stage.position = stagePosition;

        var camGo = new GameObject("ShowcaseCamera_" + name);
        _cam = camGo.AddComponent<Camera>();
        _cam.nearClipPlane = 0.3f; _cam.farClipPlane = 600f;
        _cam.fieldOfView = 35f;
        _cam.enabled = false;
        var hd = camGo.AddComponent<HDAdditionalCameraData>();
        hd.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
        hd.backgroundColorHDR = new Color(0.02f, 0.02f, 0.03f, 1f);
        // 전시 레이어만 그리고, 이 카메라에서는 물·안개·구름을 끈다 (HDRP 물은 farClip 과 무관하게 그려져 바다가 뷰어를 덮었음 — 확인됨)
        int layer = LayerMask.NameToLayer("Showcase");
        if (layer >= 0) _cam.cullingMask = 1 << layer;
        hd.customRenderingSettings = true;
        var mask = hd.renderingPathCustomFrameSettingsOverrideMask;
        var fs = hd.renderingPathCustomFrameSettings;
        FrameSettingsField[] off = { FrameSettingsField.Water, FrameSettingsField.VolumetricClouds, FrameSettingsField.AtmosphericScattering, FrameSettingsField.Volumetrics, FrameSettingsField.MotionBlur, FrameSettingsField.LensFlareDataDriven };
        foreach (var f in off) { mask.mask[(uint)f] = true; fs.SetEnabled(f, false); }
        hd.renderingPathCustomFrameSettingsOverrideMask = mask;
        hd.renderingPathCustomFrameSettings = fs;

        // 노출: 게임이 멈춘 상태(timeScale 0)에서는 자동 노출이 적응하지 못해 하얗게 타 버림(확인됨) → 이 카메라 전용 볼륨에 고정 노출.
        if (layer >= 0)
        {
            hd.volumeLayerMask = 1 << layer;
            stageGo.layer = layer;
            var vol = stageGo.AddComponent<UnityEngine.Rendering.Volume>();
            vol.isGlobal = true; vol.priority = 100f;
            var profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
            var ex = profile.Add<Exposure>(true);
            ex.mode.overrideState = true; ex.mode.value = ExposureMode.Fixed;
            ex.fixedExposure.overrideState = true; ex.fixedExposure.value = fixedExposure;
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.overrideState = true; bloom.intensity.value = 0.05f;
            var env = profile.Add<VisualEnvironment>(true);
            env.skyType.overrideState = true; env.skyType.value = (int)SkyType.Gradient;
            env.skyAmbientMode.overrideState = true; env.skyAmbientMode.value = SkyAmbientMode.Dynamic;
            var sky = profile.Add<GradientSky>(true);
            sky.top.overrideState = true; sky.top.value = new Color(0.35f, 0.45f, 0.6f);
            sky.middle.overrideState = true; sky.middle.value = new Color(0.25f, 0.3f, 0.4f);
            sky.bottom.overrideState = true; sky.bottom.value = new Color(0.15f, 0.15f, 0.18f);
            sky.exposure.overrideState = true; sky.exposure.value = fixedExposure;
            vol.sharedProfile = profile;
        }

        EnsureTexture();
    }

    void OnDestroy()
    {
        if (Texture != null) Texture.Release();
    }

    // ================= RenderTexture =================

    void WantedSize(out int w, out int h)
    {
        if (fullScreen)
        {
            w = Mathf.Max(320, Screen.width); h = Mathf.Max(240, Screen.height);
            if (w > maxTextureWidth) { float k = maxTextureWidth / (float)w; w = maxTextureWidth; h = Mathf.Max(240, Mathf.RoundToInt(h * k)); }
        }
        else { w = textureSize; h = textureSize; }
    }

    /// <summary>화면 비율에 맞는 크기로 RT 를 만든다(이미 같은 크기면 아무것도 하지 않는다).</summary>
    void EnsureTexture()
    {
        int w, h; WantedSize(out w, out h);
        if (Texture != null && _rtW == w && _rtH == h) return;

        var old = Texture;
        Texture = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        Texture.name = "ShowcaseRT_" + name;
        _rtW = w; _rtH = h;
        if (_cam != null) _cam.targetTexture = Texture;
        if (view != null) view.texture = Texture;
        if (old != null) old.Release();
    }

    float Aspect { get { return _rtH > 0 ? _rtW / (float)_rtH : 1f; } }

    // ================= 배경 쿼드 =================

    /// <summary>
    /// 카메라 자식으로 배경 쿼드를 세운다. Unlit 색은 노출(EV)의 영향을 받으므로 1.2·2^EV 를 곱해 원본 밝기로 되돌린다.
    /// </summary>
    void UpdateBackdrop()
    {
        if (!fullScreen || backdrop == null)
        {
            if (_backdropQuad != null) _backdropQuad.gameObject.SetActive(false);
            return;
        }

        if (_backdropQuad == null)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "ShowcaseBackdrop";
            var col = q.GetComponent<Collider>(); if (col != null) Destroy(col);
            var mr = q.GetComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            _backdropMat = new Material(Shader.Find("HDRP/Unlit"));
            mr.sharedMaterial = _backdropMat;
            _backdropQuad = q.transform;
            _backdropQuad.SetParent(_cam.transform, false);
            int layer = LayerMask.NameToLayer("Showcase");
            if (layer >= 0) q.layer = layer;
        }

        _backdropQuad.gameObject.SetActive(true);

        // 카메라 앞 먼 거리에 절두체를 꽉 채우는 크기로 놓는다(배보다 항상 뒤).
        float dist = Mathf.Max(_cam.farClipPlane * 0.85f, 50f);
        float height = 2f * dist * Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float width = height * Aspect;
        _backdropQuad.localPosition = new Vector3(0f, 0f, dist);
        _backdropQuad.localRotation = Quaternion.identity;
        _backdropQuad.localScale = new Vector3(width, height, 1f);

        // 밝기 보정: 처음엔 노출(EV)의 역수 1.2·2^EV 를 곱했는데 화면이 통째로 흰색으로 타 버렸다.
        // 실제로는 HDRP/Unlit 이 고정 노출의 영향을 거의 받지 않았다. 렌더된 RT 와 원본 PNG 를 직접 비교해
        // 계수를 찾은 결과 1.7 에서 평균 오차 0.018 (256점 표본, 2026-09-19 측정).
        float k = Mathf.Max(0.01f, backdropBrightness);
        _backdropMat.SetTexture("_UnlitColorMap", backdrop);
        _backdropMat.SetColor("_UnlitColor", new Color(k, k, k, 1f));

        // 비율 유지하며 채우기(cover): 남는 쪽을 잘라낸다.
        float texAspect = backdrop.width / (float)backdrop.height;
        Vector2 scale = Vector2.one, offset = Vector2.zero;
        if (texAspect > Aspect) { scale.x = Aspect / texAspect; offset.x = (1f - scale.x) * 0.5f; }
        else { scale.y = texAspect / Aspect; offset.y = (1f - scale.y) * 0.5f; }
        _backdropMat.SetTextureScale("_UnlitColorMap", scale);
        _backdropMat.SetTextureOffset("_UnlitColorMap", offset);
    }

    // ================= 전시 =================

    /// <summary>source 의 겉모습(메시 렌더러)만 복사해 전시한다. 같은 source 를 다시 주면 기존 복사본을 재사용한다.</summary>
    public void Show(GameObject source)
    {
        if (source == null) return;
        _stage.position = stagePosition; // Awake 이후 메뉴가 stagePosition 을 바꿀 수 있으므로 여기서 적용
        EnsureTexture();
        if (view != null && view.texture != Texture) view.texture = Texture;

        string wantName = "Showcase_" + source.name;
        bool rebuilt = false;
        if (_subject == null || _subject.name != wantName)
        {
            if (_subject != null) Destroy(_subject);
            _subject = new GameObject(wantName);
            _subject.transform.SetParent(_stage, false);
            CopyVisual(source.transform, _subject.transform);
            int layer = LayerMask.NameToLayer("Showcase");
            if (layer >= 0) foreach (var t in _subject.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            _subject.transform.localRotation = Quaternion.identity;
            rebuilt = true;
        }
        _subject.SetActive(true);
        _stage.gameObject.SetActive(true);
        _cam.enabled = true;
        _dragging = false;
        if (rebuilt) Frame(); else Reframe();
    }

    public void Hide()
    {
        if (_subject != null) _subject.SetActive(false);
        _cam.enabled = false;
        SetLightsActive(false);
    }

    public GameObject Subject { get { return _subject; } }

    /// <summary>복사본을 버리고 다시 만든다(선원 영입 등으로 원본 모습이 바뀐 뒤).</summary>
    public void Rebuild(GameObject source)
    {
        if (_subject != null) Destroy(_subject);
        _subject = null;
        Show(source);
    }

    /// <summary>
    /// 원본 계층을 따라가며 Transform 과 MeshFilter/MeshRenderer 만 새 오브젝트로 복사한다.
    /// Instantiate 로 통째로 복제하면 스크립트 Awake(체력바 UI, 소리 등)가 돌고 RequireComponent 때문에 컴포넌트를 떼기도 어려워 이 방식으로 바꿈.
    /// SkinnedMeshRenderer(깃발 Cloth) 는 현재 모양을 BakeMesh 로 굳혀서 넣는다. 파티클·비활성 오브젝트·물 효과는 뺀다.
    /// </summary>
    static void CopyVisual(Transform src, Transform dst)
    {
        dst.localPosition = src.localPosition; dst.localRotation = src.localRotation; dst.localScale = src.localScale;

        var mf = src.GetComponent<MeshFilter>(); var mr = src.GetComponent<MeshRenderer>();
        if (mf != null && mr != null && mf.sharedMesh != null && mr.enabled)
        {
            var nmf = dst.gameObject.AddComponent<MeshFilter>(); nmf.sharedMesh = mf.sharedMesh;
            var nmr = dst.gameObject.AddComponent<MeshRenderer>(); nmr.sharedMaterials = mr.sharedMaterials;
            nmr.shadowCastingMode = mr.shadowCastingMode;
            var block = new MaterialPropertyBlock(); mr.GetPropertyBlock(block); if (!block.isEmpty) nmr.SetPropertyBlock(block);
        }
        var smr = src.GetComponent<SkinnedMeshRenderer>();
        if (smr != null && smr.sharedMesh != null && smr.enabled)
        {
            // BakeMesh 결과는 useScale 과 무관하게 월드 크기 정점이 나온다(깃발 Cloth: 로컬 스케일 998 배가 이미 반영됨 — 확인됨).
            // 그대로 계층 아래 두면 스케일이 다시 곱해져 깃발이 수백 m 로 커지므로, 이 노드의 스케일을 부모 lossyScale 의 역수로 두어 총 스케일 1 로 만든다.
            // useScale = false 로 굽는다: 측정해 보니(2026-09-25, Unity 6) useScale=true 는 렌더러 자신의 스케일을 뺀 정점을 주고, false 가 실제 크기 정점을 준다.
            // 예전엔 true 여서 스케일 1.21 인 Body(머리·손 포함)가 1/1.21 로 작아져 옷 속에 묻혀 머리·손이 안 보였음.
            var baked = new Mesh(); smr.BakeMesh(baked, false); baked.RecalculateBounds();
            Vector3 pl = dst.parent != null ? dst.parent.lossyScale : Vector3.one;
            dst.localScale = new Vector3(1f / (Mathf.Abs(pl.x) < 1e-6f ? 1f : pl.x), 1f / (Mathf.Abs(pl.y) < 1e-6f ? 1f : pl.y), 1f / (Mathf.Abs(pl.z) < 1e-6f ? 1f : pl.z));
            var nmf = dst.gameObject.AddComponent<MeshFilter>(); nmf.sharedMesh = baked;
            var nmr = dst.gameObject.AddComponent<MeshRenderer>(); nmr.sharedMaterials = smr.sharedMaterials;
            var block = new MaterialPropertyBlock(); smr.GetPropertyBlock(block); if (!block.isEmpty) nmr.SetPropertyBlock(block);
        }

        for (int i = 0; i < src.childCount; i++)
        {
            var c = src.GetChild(i);
            if (!c.gameObject.activeSelf) continue;
            bool skip = false;
            for (int k = 0; k < StripChildren.Length; k++) if (c.name == StripChildren[k]) { skip = true; break; }
            if (skip) continue;
            var nc = new GameObject(c.name).transform;
            nc.SetParent(dst, false);
            CopyVisual(c, nc);
        }
    }

    // ================= 카메라 프레이밍 =================

    /// <summary>복제본의 렌더러 바운드로 중심을 맞추고 카메라 거리를 잡는다.</summary>
    void Frame()
    {
        var rs = _subject.GetComponentsInChildren<Renderer>();
        Bounds b = new Bounds(_stage.position, Vector3.one);
        bool first = true;
        foreach (var r in rs)
        {
            if (r is ParticleSystemRenderer) continue;
            if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
        }
        // 대상의 중심이 스테이지 원점에 오게 옮긴다
        Vector3 offset = b.center - _stage.position;
        _subject.transform.position -= offset;
        _radius = b.extents.magnitude;

        _yaw = 0f;
        _stage.rotation = Quaternion.identity;
        Reframe();
    }

    /// <summary>화면 비율·여백 비율에 맞춰 카메라 거리만 다시 잡는다(해상도 변경 시에도 호출).</summary>
    void Reframe()
    {
        if (_radius <= 0f) return;

        float vHalf = _cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float hHalf = Mathf.Atan(Mathf.Tan(vHalf) * Aspect);
        // UI 에 가리지 않는 영역 안에 들어가도록 쓸 수 있는 반각을 비율만큼 줄인다.
        float vUse = Mathf.Atan(Mathf.Tan(vHalf) * Mathf.Clamp(verticalFreeFraction, 0.2f, 1f));
        float hUse = Mathf.Atan(Mathf.Tan(hHalf) * Mathf.Clamp(horizontalFreeFraction, 0.2f, 1f));
        float distV = _radius / Mathf.Max(Mathf.Tan(vUse), 0.01f);
        float distH = _radius / Mathf.Max(Mathf.Tan(hUse), 0.01f);
        float dist = Mathf.Max(distV, distH) * distanceScale;

        Quaternion look = Quaternion.Euler(cameraElevation, 90f, 0f); // 배의 옆모습(+z 가 화면 오른쪽)
        _cam.transform.position = _stage.position - look * Vector3.forward * dist;
        _cam.transform.rotation = look;
        // 좌측 그리드와 우측 패널의 폭이 달라 빈 영역의 가운데가 화면 중앙이 아니다 → 카메라를 옆으로 이동한다.
        float shift = (0.5f - Mathf.Clamp01(viewportCenterX)) * 2f * Mathf.Tan(hHalf) * dist;
        _cam.transform.position += _cam.transform.right * shift;
        _cam.farClipPlane = dist + _radius * 6f;
        UpdateBackdrop();
        UpdateStageLights();
    }

    // ================= 전시 조명 =================

    Light NewSpot(string n)
    {
        var go = new GameObject(n + "_" + name);
        var li = go.AddComponent<Light>();
        li.type = LightType.Spot; li.spotAngle = 70f; li.innerSpotAngle = 40f; li.shadows = LightShadows.None;
        if (go.GetComponent<HDAdditionalLightData>() == null) go.AddComponent<HDAdditionalLightData>();
        return li;
    }

    void SetLightsActive(bool on)
    {
        if (_key != null) _key.gameObject.SetActive(on);
        if (_fill != null) _fill.gameObject.SetActive(on);
        if (_rim != null) _rim.gameObject.SetActive(on);
    }

    /// <summary>카메라 기준 위치에 키(앞 오른쪽 위)·보조(앞 왼쪽)·역광(뒤 위) 스포트를 놓는다. 대상이 돌아도 조명은 카메라에 고정.</summary>
    void UpdateStageLights()
    {
        if (!stageLights) { SetLightsActive(false); return; }
        if (_key == null) { _key = NewSpot("ShowcaseKey"); _fill = NewSpot("ShowcaseFill"); _rim = NewSpot("ShowcaseRim"); }
        SetLightsActive(true);
        Vector3 c = _stage.position, f = _cam.transform.forward, r = _cam.transform.right, u = Vector3.up;
        float d = Mathf.Max(_radius * 3f, 2f);
        PlaceSpot(_key, c, (-f + r * 0.8f + u * 0.9f).normalized, d, keyLux);
        PlaceSpot(_fill, c, (-f - r * 1.0f + u * 0.2f).normalized, d, fillLux);
        PlaceSpot(_rim, c, (f - r * 0.3f + u * 0.8f).normalized, d, rimLux);
    }

    static void PlaceSpot(Light li, Vector3 center, Vector3 dir, float d, float lux)
    {
        li.transform.position = center + dir * d;
        li.transform.rotation = Quaternion.LookRotation(-dir, Vector3.up);
        li.range = d * 3f;
        // 광도(cd) = 조도(lux) × 거리²
        li.GetComponent<HDAdditionalLightData>().SetIntensity(lux * d * d, UnityEngine.Rendering.LightUnit.Candela);
    }

    // ================= 입력 =================

    void Update()
    {
        if (_cam == null || !_cam.enabled) return;

        // 해상도(비율)가 바뀌면 RT 와 프레이밍을 다시 잡는다.
        if (fullScreen)
        {
            int w, h; WantedSize(out w, out h);
            if (w != _rtW || h != _rtH) { EnsureTexture(); Reframe(); }
        }

        var mouse = Mouse.current;
        if (mouse == null || view == null) return;

        Vector2 pos = mouse.position.ReadValue();
        if (mouse.leftButton.wasPressedThisFrame && IsViewerTopUI(pos))
        { _dragging = true; _lastMouse = pos; }
        if (mouse.leftButton.wasReleasedThisFrame) _dragging = false;

        if (_dragging)
        {
            float dx = pos.x - _lastMouse.x;
            _lastMouse = pos;
            _yaw -= dx * dragDegreesPerPixel;
            _stage.rotation = Quaternion.Euler(0f, _yaw, 0f);
        }
    }

    /// <summary>마우스 아래 가장 위 UI 가 뷰어일 때만 true. 버튼·패널 위에서 드래그하면 배가 돌지 않는다.</summary>
    bool IsViewerTopUI(Vector2 screenPos)
    {
        if (view == null) return false;
        if (!RectTransformUtility.RectangleContainsScreenPoint(view.rectTransform, screenPos)) return false;
        var es = EventSystem.current;
        if (es == null) return true;   // EventSystem 이 없으면 UI 판정을 못 하므로 뷰어 영역이면 허용

        var ped = new PointerEventData(es); ped.position = screenPos;
        var results = new System.Collections.Generic.List<RaycastResult>();
        es.RaycastAll(ped, results);
        for (int i = 0; i < results.Count; i++)
        {
            var go = results[i].gameObject;
            if (go == null) continue;
            return go == view.gameObject;   // 가장 위에 있는 것이 뷰어인가
        }
        return true;
    }


    /// <summary>
    /// UI 가 차지하는 폭이 화면 비율에 따라 달라지므로(CanvasScaler 가 UI 를 키우거나 줄임),
    /// 메뉴가 매 프레임 실제 캔버스(Canvas) 크기로 계산해 넘겨준다. 값이 달라질 때만 다시 프레이밍한다.
    /// </summary>
    public void SetFraming(float horizontalFree, float verticalFree, float centerX)
    {
        if (Mathf.Abs(horizontalFree - horizontalFreeFraction) < 0.002f
            && Mathf.Abs(verticalFree - verticalFreeFraction) < 0.002f
            && Mathf.Abs(centerX - viewportCenterX) < 0.002f) return;
        horizontalFreeFraction = Mathf.Clamp(horizontalFree, 0.2f, 1f);
        verticalFreeFraction = Mathf.Clamp(verticalFree, 0.2f, 1f);
        viewportCenterX = Mathf.Clamp01(centerX);
        Reframe();
    }
}
