using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.UI;

/// <summary>
/// 지도(M) + 좌상단 미니맵. UI 는 PlayerHealthUI 처럼 코드로 전부 만든다.
///
/// 땅 그림은 씬의 Terrain(Base) 헤이트맵에서 한 번 구워 쓴다(해수면 y=0 기준으로
/// 바다 깊이·모래·풀·바위 + 경사 음영). 터레인 밖은 배경색(깊은 바다)으로 채운다.
/// Terrain과 암초 메시의 실제 상부 형상을 함께 지도에 표시한다.
///
/// 지도 화면
///   - M 으로 열고 M / ESC 로 닫는다. 화면 전체를 채운다.
///   - 열려 있는 동안 Time.timeScale = 0 (배·포탄·파티클 정지) + 모든 WaterSurface 의
///     timeMultiplier = 0 (바다 정지) + AudioListener.pause.
///   - 카메라 오빗·포격 등 마우스를 쓰는 컴포넌트는 꺼 둔다(disableWhileOpen).
///     CameraFollow 는 ESC 로 커서를 토글하므로, 닫을 때는 한 프레임 뒤에 다시 켠다
///     (같은 프레임에 켜면 ESC 입력을 CameraFollow 도 받아 커서가 뒤집힌다).
///   - 적 위치는 표시하지 않는다(플레이어·베이스만). 휠 확대/축소, 좌클릭 드래그 이동.
///
/// 미니맵
///   - 북쪽 고정, 플레이어 중심. 적대 진영 배만 점(삼각형)으로 표시한다.
/// </summary>
[DefaultExecutionOrder(200)]
public partial class MapSystem : MonoBehaviour
{
    [Header("참조 (비우면 자동)")]
    public Terrain terrain;
    [Tooltip("플레이어 배. 비우면 Player 태그의 ShipFaction.")]
    public ShipFaction player;

    [Header("지도 텍스처")]
    public int textureResolution = 1024;
    public Color deepSea = new Color(0.10f, 0.24f, 0.33f);
    public Color shallowSea = new Color(0.30f, 0.58f, 0.62f);
    public Color sand = new Color(0.82f, 0.74f, 0.54f);
    public Color grass = new Color(0.40f, 0.52f, 0.30f);
    public Color rock = new Color(0.50f, 0.46f, 0.40f);
    public Color coastLine = new Color(0.20f, 0.17f, 0.12f);
    [Tooltip("이 깊이(m)에서 깊은 바다색이 된다.")]
    public float deepDepth = 45f;

    [Header("지도 화면")]
    [Tooltip("처음 열 때 화면 세로에 들어가는 월드 거리(m).")]
    public float fullMapViewSize = 900f;
    public Vector2 fullMapZoomRange = new Vector2(250f, 4000f);
    [Tooltip("열려 있는 동안 끌 컴포넌트 타입 이름.")]
    public string[] disableWhileOpen = { "CameraFollow", "CannonControl", "WeatherTestKeys" };

    [Header("미니맵")]
    public float minimapSize = 280f;
    [Tooltip("미니맵 중심에서 가장자리까지 월드 거리(m).")]
    public float minimapRadius = 250f;
    [Tooltip("미니맵 배경에 보여줄 해도 그림의 크기(텍스처 픽셀). 작을수록 물결선이 크게 보인다.")]
    public float minimapChartPixels = 300f;
    public Color enemyJapanese = new Color(0.95f, 0.22f, 0.15f);
    public Color enemyNavy = new Color(0.35f, 0.60f, 1.00f);
    public Color enemyMerchant = new Color(0.95f, 0.80f, 0.30f);

    [Header("상태 (읽기 전용)")]
    public bool isOpen;
    public int minimapMarkers;

    // ---- 내부 ----
    Texture2D _landTex;
    Vector2 _landMin, _landSize;           // 터레인 xz 범위(월드)

    RectTransform _miniRoot, _miniView, _miniLand, _miniPlayer, _miniMarkerHolder;
    RectTransform _fullRoot, _fullView, _fullLand, _fullPlayer, _fullBase;
    readonly List<Image> _markerPool = new List<Image>();

    Vector2 _fullCenter;
    float _fullViewSize;
    bool _dragging;
    Vector2 _dragStartMouse, _dragStartCenter;

    // 일시정지 복구용
    float _prevTimeScale = 1f;
    readonly List<Behaviour> _disabled = new List<Behaviour>();
    readonly List<KeyValuePair<WaterSurface, float>> _waters = new List<KeyValuePair<WaterSurface, float>>();
    bool _prevCursorVisible;
    CursorLockMode _prevCursorLock;
    int _restoreFrame = -1;

    static Sprite _white, _circle, _triangle;
    static readonly Dictionary<string, Sprite> _tinted = new Dictionary<string, Sprite>();
    static Font _font;

    void Start()
    {
        if (terrain == null) terrain = Terrain.activeTerrain;
        FindPlayer();
        BuildAtlasTexture();
        BuildUI();
        _fullCenter = _landMin + _landSize * 0.5f;
        _fullViewSize = fullMapViewSize;
        SetOpen(false, true);
    }

    void OnDestroy()
    {
        if (isOpen) SetOpen(false, true);
        RestoreAtlasInput();
        if (_landTex != null) Destroy(_landTex);
    }

    void FindPlayer()
    {
        if (player != null) return;
        GameObject go = GameObject.FindGameObjectWithTag("Player");
        if (go != null) player = go.GetComponentInParent<ShipFaction>();
        if (player == null)
            foreach (var s in ShipFaction.All) if (s.faction == Faction.Pirate) { player = s; break; }
    }

    // ================= 입력 =================

    void Update()
    {
        // 닫은 다음 프레임에 컴포넌트·커서 복구 (위 주석 참고)
        if (_restoreFrame >= 0 && Time.frameCount >= _restoreFrame)
        {
            _restoreFrame = -1;
            for (int i = 0; i < _disabled.Count; i++) if (_disabled[i] != null) _disabled[i].enabled = true;
            _disabled.Clear();
            Cursor.visible = _prevCursorVisible;
            Cursor.lockState = _prevCursorLock;
        }

        var kb = Keyboard.current;
        if (kb == null) { if (isOpen) HandleFullMapMouse(); return; }

        if (!isOpen)
        {
            // 함선(Y)/인벤토리(I) 메뉴가 열려 있으면 지도는 열지 않는다
            if (kb.mKey.wasPressedThisFrame && _restoreFrame < 0 && !GameMenuPause.IsPaused) SetOpen(true, false);
        }
        else
        {
            if (kb.mKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame) { SetOpen(false, false); return; }
            HandleFullMapMouse();
        }
    }

    void HandleFullMapMouse()
    {
        var mouse = Mouse.current;
        if (mouse == null || _fullView == null) return;

        if (HandleAtlasControls(mouse)) return;
        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            float step = Mathf.Sign(scroll);
            _fullViewSize = Mathf.Clamp(_fullViewSize * (step > 0 ? 0.8f : 1.25f), fullMapZoomRange.x, fullMapZoomRange.y);
        }

        Vector2 mp = mouse.position.ReadValue();
        if (mouse.leftButton.wasPressedThisFrame) { _dragging = true; _dragStartMouse = mp; _dragStartCenter = _fullCenter; }
        if (mouse.leftButton.wasReleasedThisFrame) _dragging = false;
        if (_dragging)
        {
            float mPerScreenPx = _fullViewSize / Mathf.Max(Screen.height, 1);
            _fullCenter = _dragStartCenter - (mp - _dragStartMouse) * mPerScreenPx;
        }
    }

    // ================= 열기 / 닫기 =================

    void SetOpen(bool open, bool silent)
    {
        if (_fullRoot == null) return;
        if (!silent && open == isOpen) return;

        if (open)
        {
            _prevTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;

            _waters.Clear();
            foreach (var w in Object.FindObjectsByType<WaterSurface>(FindObjectsSortMode.None))
            {
                _waters.Add(new KeyValuePair<WaterSurface, float>(w, w.timeMultiplier));
                w.timeMultiplier = 0f;
            }
            _previousAudioPause = AudioListener.pause;
            AudioListener.pause = true;

            _disabled.Clear();
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (mb == null || !mb.enabled || mb == this) continue;
                string tn = mb.GetType().Name;
                for (int i = 0; i < disableWhileOpen.Length; i++)
                    if (tn == disableWhileOpen[i]) { mb.enabled = false; _disabled.Add(mb); break; }
            }

            if (_restoreFrame < 0) { _prevCursorVisible = Cursor.visible; _prevCursorLock = Cursor.lockState; }
            _restoreFrame = -1;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            // 열 때마다 플레이어 위치로 맞춘다
            if (player != null) _fullCenter = new Vector2(player.transform.position.x, player.transform.position.z);
            FrameAtlasOpening();
            _dragging = false;

            isOpen = true;
            _fullRoot.gameObject.SetActive(true);
            _miniRoot.gameObject.SetActive(false);
            LateUpdate();
        }
        else
        {
            bool wasOpen = isOpen;
            isOpen = false;
            _fullRoot.gameObject.SetActive(false);
            _miniRoot.gameObject.SetActive(true);
            if (!wasOpen) return;

            Time.timeScale = _prevTimeScale;
            for (int i = 0; i < _waters.Count; i++) if (_waters[i].Key != null) _waters[i].Key.timeMultiplier = _waters[i].Value;
            _waters.Clear();
            AudioListener.pause = _previousAudioPause;

            _restoreFrame = Time.frameCount + 1;
        }
    }

    // ================= 매 프레임 갱신 =================

    void LateUpdate()
    {
        if (player == null) FindPlayer();
        if (_miniRoot == null) return;

        if (isOpen) { UpdateFullMap(); UpdateAtlasCursor(); }
        else UpdateMinimap();
    }

    void UpdateMinimap()
    {
        if (player == null) return;
        Vector3 pp = player.transform.position;
        Vector2 center = new Vector2(pp.x, pp.z);
        float half = _miniView.rect.width * 0.5f;
        float ppm = half / Mathf.Max(minimapRadius, 1f);

        PlaceLand(_miniLand, center, ppm);
        _miniPlayer.localEulerAngles = new Vector3(0f, 0f, -player.transform.eulerAngles.y);

        int used = 0;
        var all = ShipFaction.All;
        for (int i = 0; i < all.Count; i++)
        {
            ShipFaction s = all[i];
            if (s == null || s == player || !s.isActiveAndEnabled) continue;
            if (!player.IsHostileTo(s)) continue;
            ShipHealth hp = s.GetComponent<ShipHealth>();
            if (hp != null && (hp.isSinking || hp.isDead)) continue;

            Vector3 p = s.transform.position;
            Vector2 d = (new Vector2(p.x, p.z) - center) * ppm;
            if (Mathf.Abs(d.x) > half - 4f || Mathf.Abs(d.y) > half - 4f) continue;   // 미니맵 밖

            Image m = GetMarker(used++);
            m.rectTransform.anchoredPosition = d;
            m.rectTransform.localEulerAngles = new Vector3(0f, 0f, -s.transform.eulerAngles.y);
            Sprite ms = Tint(TriangleSprite(), MarkerColor(s));
            if (m.sprite != ms) m.sprite = ms;
            float size = s.GetComponent<SekiScoutAI>() != null ? 11f : 14f;
            m.rectTransform.sizeDelta = new Vector2(size, size);
        }
        for (int i = used; i < _markerPool.Count; i++)
            if (_markerPool[i].gameObject.activeSelf) _markerPool[i].gameObject.SetActive(false);
        minimapMarkers = used;
    }

    void UpdateFullMap()
    {
        float h = _fullView.rect.height;
        float ppm = h / Mathf.Max(_fullViewSize, 1f);
        PlaceLand(_fullLand, _fullCenter, ppm);

        if (player != null)
        {
            Vector3 pp = player.transform.position;
            _fullPlayer.anchoredPosition = (new Vector2(pp.x, pp.z) - _fullCenter) * ppm;
            _fullPlayer.localEulerAngles = new Vector3(0f, 0f, -player.transform.eulerAngles.y);
        }

        Vector3 basePos;
        if (TryGetBase(out basePos))
        {
            _fullBase.gameObject.SetActive(true);
            _fullBase.anchoredPosition = (new Vector2(basePos.x, basePos.z) - _fullCenter) * ppm;
        }
        else _fullBase.gameObject.SetActive(false);
    }

    bool TryGetBase(out Vector3 pos)
    {
        pos = Vector3.zero;
        if (RespawnPoint.Instance == null) return false;
        pos = RespawnPoint.Instance.transform.position;
        return true;
    }

    void PlaceLand(RectTransform land, Vector2 viewCenter, float ppm)
    {
        Vector2 landCenter = _landMin + _landSize * 0.5f;
        land.anchoredPosition = (landCenter - viewCenter) * ppm;
        land.sizeDelta = _landSize * ppm;
    }

    Color MarkerColor(ShipFaction s)
    {
        if (s.GetComponent<MerchantAI>() != null) return enemyMerchant;
        if (s.faction == Faction.Navy) return enemyNavy;
        return enemyJapanese;
    }

    Image GetMarker(int index)
    {
        while (_markerPool.Count <= index)
        {
            var img = NewImage("Enemy", _miniMarkerHolder, TriangleSprite(), Color.red);
            img.rectTransform.sizeDelta = new Vector2(14f, 14f);
            img.raycastTarget = false;
            _markerPool.Add(img);
        }
        Image m = _markerPool[index];
        if (!m.gameObject.activeSelf) m.gameObject.SetActive(true);
        return m;
    }

    // ================= 땅 텍스처 =================

    // ================= UI 생성 =================

    void BuildUI()
    {
        var canvasGo = new GameObject("MapUI");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150;   // PlayerHUD(100) 위
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        // ---- 미니맵 (좌상단) ----
        _miniRoot = NewRect("Minimap", canvasGo.transform);
        _miniRoot.anchorMin = _miniRoot.anchorMax = _miniRoot.pivot = new Vector2(0f, 1f);
        _miniRoot.anchoredPosition = new Vector2(20f, -64f);   // 맨 위는 WeatherTestKeys OnGUI 라벨 자리
        _miniRoot.sizeDelta = new Vector2(minimapSize, minimapSize);
        NewImage("Frame", _miniRoot, WhiteSprite(), new Color(0.12f, 0.09f, 0.06f, 0.95f)).raycastTarget = false;
        Stretch(_miniRoot.GetChild(0) as RectTransform, 0f);

        _miniView = NewRect("View", _miniRoot);
        Stretch(_miniView, 4f);
        NewImage("Sea", _miniView, WhiteSprite(), deepSea).raycastTarget = false;
        Stretch(_miniView.GetChild(0) as RectTransform, 0f);

        // 지도 화면과 같은 해도 배경(MapAtlas/OceanChart)을 미니맵에도 깔아 둔다.
        // 미니맵은 정사각이고 해도 그림은 2:1 이므로 uvRect 로 가운데를 잘라 비율을 유지한다(cover).
        var miniChartTex = Resources.Load<Texture2D>("MapAtlas/OceanChart");
        if (miniChartTex != null)
        {
            var miniChart = NewRawImage("Engraved ocean", _miniView, miniChartTex);
            Stretch(miniChart.rectTransform, 0f);
            miniChart.raycastTarget = false;
            // 해도 물결선이 지도 화면과 비슷한 크기로 보이게 일부만 잘라 보여준다.
            // 전체 지도는 폭 1920 에 2048px 를 펼치므로, 미니맵(280) 은 약 300px 만 보여주면 물결선 밀도가 비슷해진다.
            miniChart.uvRect = ZoomUV(miniChartTex, 1f, minimapChartPixels);
        }

        _miniView.gameObject.AddComponent<RectMask2D>();

        _miniLand = NewRawImage("Land", _miniView, _landTex).rectTransform;
        _miniMarkerHolder = NewRect("Markers", _miniView);
        Stretch(_miniMarkerHolder, 0f);

        var mp = NewImage("Player", _miniView, TriangleSprite(), Color.white);
        mp.raycastTarget = false;
        _miniPlayer = mp.rectTransform;
        _miniPlayer.sizeDelta = new Vector2(18f, 18f);
        var mpOutline = mp.gameObject.AddComponent<Outline>();
        mpOutline.effectColor = new Color(0f, 0f, 0f, 0.8f);

        var north = NewText("N", _miniView, "N", 16, TextAnchor.UpperCenter, new Color(1f, 1f, 1f, 0.85f));
        north.rectTransform.anchorMin = north.rectTransform.anchorMax = north.rectTransform.pivot = new Vector2(0.5f, 1f);
        north.rectTransform.anchoredPosition = new Vector2(0f, -2f);
        north.rectTransform.sizeDelta = new Vector2(30f, 22f);

        var hint = NewText("Hint", _miniRoot, "M : 지도", 15, TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.8f));
        hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = hint.rectTransform.pivot = new Vector2(0f, 0f);
        hint.rectTransform.anchoredPosition = new Vector2(2f, -22f);
        hint.rectTransform.sizeDelta = new Vector2(200f, 20f);

        // ---- 전체 지도 ----
        _fullRoot = NewRect("FullMap", canvasGo.transform);
        Stretch(_fullRoot, 0f);
        var bg = NewImage("Sea", _fullRoot, WhiteSprite(), deepSea);
        Stretch(bg.rectTransform, 0f);
        bg.raycastTarget = true;   // 지도 뒤 게임 화면 클릭 차단

        _fullView = NewRect("View", _fullRoot);
        Stretch(_fullView, 0f);
        _fullView.gameObject.AddComponent<RectMask2D>();
        _fullLand = NewRawImage("Land", _fullView, _landTex).rectTransform;

        var baseMark = NewImage("Base", _fullView, CircleSprite(), new Color(1f, 0.85f, 0.35f, 0.95f));
        baseMark.raycastTarget = false;
        _fullBase = baseMark.rectTransform;
        _fullBase.sizeDelta = new Vector2(14f, 14f);
        var baseLabel = NewText("Label", _fullBase, "베이스", 18, TextAnchor.MiddleLeft, new Color(1f, 0.92f, 0.7f));
        baseLabel.rectTransform.anchoredPosition = new Vector2(62f, 0f);
        baseLabel.rectTransform.sizeDelta = new Vector2(100f, 24f);
        baseLabel.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);

        var fp = NewImage("Player", _fullView, TriangleSprite(), Color.white);
        fp.raycastTarget = false;
        _fullPlayer = fp.rectTransform;
        _fullPlayer.sizeDelta = new Vector2(26f, 26f);
        fp.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);

        BuildAtlasChrome();
    }

    // ================= UI 헬퍼 =================

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        return rt;
    }

    static void Stretch(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
    {
        var rt = NewRect(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.sprite = Tint(sprite, color);   // 색은 텍스처에 구워 넣는다 (UIColor 주석 참고)
        img.color = Color.white;
        return img;
    }

    static RawImage NewRawImage(string name, Transform parent, Texture tex)
    {
        var rt = NewRect(name, parent);
        var img = rt.gameObject.AddComponent<RawImage>();
        img.texture = tex;
        img.raycastTarget = false;
        return img;
    }

    static Text NewText(string name, Transform parent, string text, int size, TextAnchor align, Color color)
    {
        var rt = NewRect(name, parent);
        var t = rt.gameObject.AddComponent<Text>();
        if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.font = _font;
        t.fontSize = size;
        t.alignment = align;
        t.color = color;
        t.text = text;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        return t;
    }

    static Sprite WhiteSprite()
    {
        if (_white != null) return _white;
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        _white = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 100f);
        return _white;
    }

    static Sprite CircleSprite()
    {
        if (_circle != null) return _circle;
        const int S = 32;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(S * 0.5f, S * 0.5f));
                float a = Mathf.Clamp01(S * 0.5f - 1f - d + 0.5f);
                float ring = d > S * 0.5f - 4f ? 0.25f : 1f;   // 가장자리를 어둡게
                tex.SetPixel(x, y, new Color(ring, ring, ring, a));
            }
        tex.Apply();
        _circle = Sprite.Create(tex, new Rect(0f, 0f, S, S), new Vector2(0.5f, 0.5f), 100f);
        return _circle;
    }

    /// <summary>위(+y = 뱃머리)를 가리키는 삼각형.</summary>
    static Sprite TriangleSprite()
    {
        if (_triangle != null) return _triangle;
        const int S = 32;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Vector2 a = new Vector2(S * 0.5f, S - 1f), b = new Vector2(3f, 2f), c = new Vector2(S - 3f, 2f);
        Vector2 notch = new Vector2(S * 0.5f, 9f);   // 꼬리를 살짝 파서 화살촉 모양
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                bool inside = InTri(p, a, b, notch) || InTri(p, a, notch, c);
                tex.SetPixel(x, y, inside ? Color.white : new Color(1f, 1f, 1f, 0f));
            }
        tex.Apply();
        _triangle = Sprite.Create(tex, new Rect(0f, 0f, S, S), new Vector2(0.5f, 0.5f), 100f);
        return _triangle;
    }

    static bool InTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross(p, a, b), d2 = Cross(p, b, c), d3 = Cross(p, c, a);
        bool neg = d1 < 0 || d2 < 0 || d3 < 0;
        bool pos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(neg && pos);
    }

    static float Cross(Vector2 p, Vector2 a, Vector2 b)
    {
        return (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
    }


    /// <summary>
    /// HDRP(리니어 컬러 스페이스)에서 Screen Space Overlay UI 색이 감마 보정된 것처럼 밝게(뿌얏게) 나왔다
    /// (스크린샷으로 확인, 2026-09-17). 인스펙터에 적은 색이 화면에 그대로 보이도록 리니어로 바꿔 넣는다.
    /// </summary>
    static Color UIColor(Color c)
    {
        return QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c;
    }


    /// <summary>
    /// 스프라이트 텍스처에 색을 고정으로 구운 사본(색별 캐시). Image.color(정점색)는 이 프로젝트(HDRP)에서
    /// 인스펙터 값과 다르게 보여서(감마 보정 없이 넣으면 뿌얏고, .linear 로 넣으면 진한 남색) 쓰지 않는다.
    /// </summary>
    static Sprite Tint(Sprite src, Color color)
    {
        string key = src.GetInstanceID() + "_" + ColorUtility.ToHtmlStringRGBA(color);
        Sprite s;
        if (_tinted.TryGetValue(key, out s) && s != null) return s;

        Texture2D st = src.texture;
        var tex = new Texture2D(st.width, st.height, TextureFormat.RGBA32, false);
        tex.filterMode = st.filterMode;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color lc = UIColor(color);
        Color[] p = st.GetPixels();
        for (int i = 0; i < p.Length; i++)
            p[i] = new Color(p[i].r * lc.r, p[i].g * lc.g, p[i].b * lc.b, p[i].a * color.a);
        tex.SetPixels(p);
        tex.Apply();
        s = Sprite.Create(tex, new Rect(0f, 0f, st.width, st.height), new Vector2(0.5f, 0.5f), 100f);
        _tinted[key] = s;
        return s;
    }


    /// <summary>
    /// 텍스처를 상자에 비율 유지해 채울(cover) 때 쓰는 uvRect. 남는 쪽을 가운데 기준으로 잘라낸다.
    /// </summary>
    static Rect CoverUV(Texture tex, float viewAspect)
    {
        if (tex == null || tex.height == 0 || viewAspect <= 0f) return new Rect(0f, 0f, 1f, 1f);
        float texAspect = tex.width / (float)tex.height;
        if (texAspect > viewAspect)
        {
            float w = viewAspect / texAspect;
            return new Rect((1f - w) * 0.5f, 0f, w, 1f);
        }
        float h = texAspect / viewAspect;
        return new Rect(0f, (1f - h) * 0.5f, 1f, h);
    }

    /// <summary>
    /// 텍스처의 가운데 일부만 비율 유지해 보여주는 uvRect (확대 표시용).
    /// pixels 는 가로로 보여줄 텍스처 픽셀 수.
    /// </summary>
    static Rect ZoomUV(Texture tex, float viewAspect, float pixels)
    {
        if (tex == null || tex.width == 0 || tex.height == 0 || viewAspect <= 0f) return new Rect(0f, 0f, 1f, 1f);
        float px = Mathf.Clamp(pixels, 32f, tex.width);
        float uw = px / tex.width;
        float uh = (px / viewAspect) / tex.height;
        if (uh > 1f) { float k = 1f / uh; uh = 1f; uw *= k; }
        return new Rect(0.5f - uw * 0.5f, 0.5f - uh * 0.5f, uw, uh);
    }

}
