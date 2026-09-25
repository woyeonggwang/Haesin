using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public partial class MapSystem
{
    [Header("해도 / 암초")]
    [Tooltip("추가로 지도에 표시할 암초 루트. Cave, Rock, Reef 이름의 지형은 자동 포함됩니다.")]
    public Transform[] mapGeometryRoots;
    public int mappedRockMeshes;
    [System.Serializable]
    public struct AtlasGeometrySource { public MeshFilter filter; public Mesh mesh; }
    [SerializeField, HideInInspector] AtlasGeometrySource[] atlasGeometrySources;
    RectTransform _atlasCursor, _closeRect, _recenterRect;
    Image _closeFace;
    Text _scaleLabel;
    bool _previousAudioPause;
    Texture2D _fallbackCursor;
    static readonly Color AtlasGold = new Color(0.78f, 0.68f, 0.43f);
    static readonly Color AtlasInk = new Color(0.065f, 0.10f, 0.115f, 0.97f);
    static readonly Color AtlasIvory = new Color(0.93f, 0.91f, 0.81f);

    bool IsMapGeometry(MeshFilter mf)
    {
        if (mf.sharedMesh == null || mf.GetComponentInParent<ShipFaction>() != null) return false;
        var renderer = mf.GetComponent<Renderer>();
        if (renderer == null || !renderer.enabled || renderer.bounds.max.y <= 0f) return false;
        for (Transform t = mf.transform; t != null; t = t.parent)
        {
            if (mapGeometryRoots != null)
                foreach (var root in mapGeometryRoots) if (root != null && root == t) return true;
            string n = t.name.ToLowerInvariant();
            if (n.Contains("cave") || n.Contains("rock") || n.Contains("reef") || n.Contains("암초")) return true;
        }
        return false;
    }

    // Keep original local-space meshes before Unity static batching replaces MeshFilter.sharedMesh.
    [ContextMenu("Refresh Map Geometry Sources")]
    public void CacheAtlasGeometrySources()
    {
        var sources = new List<AtlasGeometrySource>();
        foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            if (IsMapGeometry(mf)) sources.Add(new AtlasGeometrySource { filter = mf, mesh = mf.sharedMesh });
        atlasGeometrySources = sources.ToArray();
    }

    Mesh AtlasSourceMesh(MeshFilter mf)
    {
        if (atlasGeometrySources != null)
            foreach (var source in atlasGeometrySources)
                if (source.filter == mf && source.mesh != null && source.mesh.isReadable) return source.mesh;
        return mf.sharedMesh != null && mf.sharedMesh.isReadable ? mf.sharedMesh : null;
    }

    // Rasterize real mesh triangles into the same world-space height field as Terrain.
    // No colliders, camera layers, scene materials or scene geometry are changed.
    void BuildAtlasTexture()
    {
        var terrains = Terrain.activeTerrains;
        var meshes = new List<MeshFilter>();
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = -min;
        foreach (var t in terrains)
        {
            Vector3 p = t.transform.position, s = t.terrainData.size;
            min = Vector2.Min(min, new Vector2(p.x, p.z));
            max = Vector2.Max(max, new Vector2(p.x + s.x, p.z + s.z));
        }
        foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
        {
            if (!IsMapGeometry(mf) || AtlasSourceMesh(mf) == null) continue;
            meshes.Add(mf);
            Bounds b = mf.GetComponent<Renderer>().bounds;
            min = Vector2.Min(min, new Vector2(b.min.x, b.min.z));
            max = Vector2.Max(max, new Vector2(b.max.x, b.max.z));
        }
        mappedRockMeshes = meshes.Count;
        if (min.x == float.MaxValue) { min = Vector2.one * -250f; max = Vector2.one * 250f; }
        _landMin = min - Vector2.one * 30f;
        _landSize = max - min + Vector2.one * 60f;
        int res = Mathf.Clamp(textureResolution, 128, 2048);
        var heights = new float[res * res];
        for (int i = 0; i < heights.Length; i++) heights[i] = -deepDepth;
        foreach (var t in terrains)
        {
            Vector3 tp = t.transform.position, ts = t.terrainData.size;
            for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float u = (_landMin.x + (x + 0.5f) / res * _landSize.x - tp.x) / ts.x;
                float v = (_landMin.y + (y + 0.5f) / res * _landSize.y - tp.z) / ts.z;
                if (u < 0 || u > 1 || v < 0 || v > 1) continue;
                int i = y * res + x;
                heights[i] = Mathf.Max(heights[i], t.terrainData.GetInterpolatedHeight(u, v) + tp.y);
            }
        }
        foreach (var mf in meshes)
        {
            var sourceMesh = AtlasSourceMesh(mf);
            var vertices = sourceMesh.vertices;
            var triangles = sourceMesh.triangles;
            for (int i = 0; i < vertices.Length; i++) vertices[i] = mf.transform.TransformPoint(vertices[i]);
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
                if (Mathf.Max(a.y, Mathf.Max(b.y, c.y)) <= 0) continue;
                Vector2 pa = AtlasPixel(a, res), pb = AtlasPixel(b, res), pc = AtlasPixel(c, res);
                float den = (pb.y - pc.y) * (pa.x - pc.x) + (pc.x - pb.x) * (pa.y - pc.y);
                if (Mathf.Abs(den) < 0.0001f) continue;
                int x0 = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(pa.x, Mathf.Min(pb.x, pc.x))), 0, res - 1);
                int x1 = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(pa.x, Mathf.Max(pb.x, pc.x))), 0, res - 1);
                int y0 = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(pa.y, Mathf.Min(pb.y, pc.y))), 0, res - 1);
                int y1 = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(pa.y, Mathf.Max(pb.y, pc.y))), 0, res - 1);
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float wa = ((pb.y - pc.y) * (x + 0.5f - pc.x) + (pc.x - pb.x) * (y + 0.5f - pc.y)) / den;
                    float wb = ((pc.y - pa.y) * (x + 0.5f - pc.x) + (pa.x - pc.x) * (y + 0.5f - pc.y)) / den;
                    float wc = 1f - wa - wb;
                    if (wa < 0 || wb < 0 || wc < 0) continue;
                    float h = a.y * wa + b.y * wb + c.y * wc;
                    if (h > 0) heights[y * res + x] = Mathf.Max(heights[y * res + x], h);
                }
            }
        }
        var pixels = new Color[heights.Length];
        Vector3 light = new Vector3(-0.65f, 0.8f, 0.45f).normalized;
        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            int i = y * res + x;
            float h = heights[i];
            if (h < 0)
            {
                float shallow = 1f - Mathf.Clamp01(-h / Mathf.Max(deepDepth, 1));
                pixels[i] = new Color(0.35f, 0.61f, 0.61f, shallow * shallow * 0.65f);
                continue;
            }
            float dx = heights[y * res + Mathf.Min(x + 1, res - 1)] - heights[y * res + Mathf.Max(x - 1, 0)];
            float dz = heights[Mathf.Min(y + 1, res - 1) * res + x] - heights[Mathf.Max(y - 1, 0) * res + x];
            Vector3 n = new Vector3(-dx / (_landSize.x / res * 2), 1, -dz / (_landSize.y / res * 2)).normalized;
            float shade = 0.67f + Mathf.Clamp01(Vector3.Dot(n, light)) * 0.42f;
            Color c = Color.Lerp(new Color(0.68f, 0.66f, 0.43f), new Color(0.84f, 0.79f, 0.61f), Mathf.Clamp01(h / 200));
            float grain = Mathf.PerlinNoise(x * 0.18f, y * 0.18f);
            c *= shade * (0.94f + grain * 0.10f);
            if (h % 20f < 0.65f) c *= 0.88f;
            bool coast = x > 0 && heights[i - 1] < 0 || x < res - 1 && heights[i + 1] < 0 || y > 0 && heights[i - res] < 0 || y < res - 1 && heights[i + res] < 0;
            if (coast) c = new Color(0.93f, 0.86f, 0.62f);
            c.a = 1;
            pixels[i] = UIColor(c);
        }
        _landTex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        _landTex.name = "Atlas Terrain + Reefs";
        _landTex.wrapMode = TextureWrapMode.Clamp;
        _landTex.SetPixels(pixels);
        _landTex.Apply(false, true);
    }

    Vector2 AtlasPixel(Vector3 p, int res)
    {
        return new Vector2((p.x - _landMin.x) / _landSize.x * res, (p.z - _landMin.y) / _landSize.y * res);
    }

    void FrameAtlasOpening()
    {
        Vector3 bp;
        _fullViewSize = fullMapViewSize;
        if (player != null && TryGetBase(out bp))
        {
            Vector2 pp = new Vector2(player.transform.position.x, player.transform.position.z);
            Vector2 basePoint = new Vector2(bp.x, bp.z);
            _fullCenter = (pp + basePoint) * 0.5f;
            float aspect = Mathf.Max(0.5f, (float)Screen.width / Mathf.Max(1, Screen.height));
            Vector2 distance = pp - basePoint;
            _fullViewSize = Mathf.Clamp(Mathf.Max(fullMapViewSize, Mathf.Abs(distance.y) * 1.5f + 450, Mathf.Abs(distance.x) / aspect * 1.5f + 450), fullMapZoomRange.x, fullMapZoomRange.y);
        }
    }

    RectTransform AtlasPanel(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color color)
    {
        var image = NewImage(name, parent, WhiteSprite(), color);
        image.raycastTarget = false;
        var rt = image.rectTransform;
        rt.anchorMin = rt.anchorMax = anchor; rt.pivot = pivot;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        return rt;
    }

    Text AtlasText(string name, Transform parent, string text, int size, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 bounds, TextAnchor alignment, Color color)
    {
        var t = NewText(name, parent, text, size, alignment, color);
        t.rectTransform.anchorMin = t.rectTransform.anchorMax = anchor;
        t.rectTransform.pivot = pivot; t.rectTransform.anchoredPosition = position; t.rectTransform.sizeDelta = bounds;
        return t;
    }

    void BuildAtlasChrome()
    {
        var ocean = Resources.Load<Texture2D>("MapAtlas/OceanChart");
        if (ocean != null)
        {
            var sea = NewRawImage("Engraved ocean", _fullRoot, ocean);
            Stretch(sea.rectTransform, 0); sea.transform.SetSiblingIndex(1);
        }
        // Fixed chrome above the freely moving map. Only real map actions are shown.
        var header = AtlasPanel("Atlas header", _fullRoot, new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 104), AtlasInk);
        header.anchorMin = new Vector2(0, 1); header.anchorMax = Vector2.one;
        var topLine = AtlasPanel("Gold rule", header, new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 1), AtlasGold);
        topLine.anchorMin = Vector2.zero; topLine.anchorMax = new Vector2(1, 0);
        AtlasText("Identity", header, "해 신  /  항 해 도", 18, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(42, 0), new Vector2(320, 44), TextAnchor.MiddleLeft, AtlasGold);
        AtlasText("Map title", header, "세 계   지 도", 30, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(350, 60), TextAnchor.MiddleCenter, AtlasIvory);
        AtlasPanel("Selected underline", header, new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(180, 3), AtlasGold);
        _closeRect = AtlasPanel("Close map", header, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-32, 0), new Vector2(152, 52), new Color(0.19f, 0.23f, 0.23f));
        _closeFace = _closeRect.GetComponent<Image>();
        _closeFace.raycastTarget = true;
        var close = _closeRect.gameObject.AddComponent<Button>();
        close.targetGraphic = _closeFace; close.onClick.AddListener(() => SetOpen(false, false));
        var nav = close.navigation; nav.mode = Navigation.Mode.None; close.navigation = nav;
        AtlasText("Close label", _closeRect, "닫기   ×", 23, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(140, 45), TextAnchor.MiddleCenter, AtlasIvory);
        var footer = AtlasPanel("Atlas footer", _fullRoot, new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 78), AtlasInk);
        footer.anchorMin = Vector2.zero; footer.anchorMax = new Vector2(1, 0);
        var bottomLine = AtlasPanel("Gold rule", footer, new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 1), AtlasGold);
        bottomLine.anchorMin = new Vector2(0, 1); bottomLine.anchorMax = Vector2.one;
        AtlasText("Help", footer, "[ M / ESC ]  닫기       [ 휠 ]  확대 · 축소       [ 드래그 ]  이동", 18, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(42, 0), new Vector2(920, 44), TextAnchor.MiddleLeft, AtlasIvory);
        _recenterRect = AtlasPanel("Recenter", footer, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-35, 0), new Vector2(255, 48), new Color(0.17f, 0.21f, 0.22f));
        AtlasText("Recenter label", _recenterRect, "현재 위치로 이동", 18, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(240, 40), TextAnchor.MiddleCenter, AtlasIvory);
        AtlasText("Region", _fullRoot, "베 이 스   해 역", 24, new Vector2(0, 1), new Vector2(0, 1), new Vector2(44, -140), new Vector2(330, 42), TextAnchor.MiddleLeft, AtlasIvory);
        AtlasText("Region subtitle", _fullRoot, "연안 · 암초 지형", 16, new Vector2(0, 1), new Vector2(0, 1), new Vector2(46, -186), new Vector2(330, 30), TextAnchor.MiddleLeft, AtlasGold);
        AtlasText("Compass", _fullRoot, "N\n│\n◇", 26, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-67, -145), new Vector2(70, 110), TextAnchor.UpperCenter, AtlasIvory);
        AtlasText("Legend", _fullRoot, "▲  내 함선     ◆  베이스", 17, new Vector2(0, 0), new Vector2(0, 0), new Vector2(44, 100), new Vector2(440, 36), TextAnchor.MiddleLeft, AtlasIvory);
        _scaleLabel = AtlasText("Scale", _fullRoot, "", 16, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-44, 99), new Vector2(220, 35), TextAnchor.MiddleRight, AtlasIvory);
        AtlasPanel("Scale bar", _fullRoot, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-44, 98), new Vector2(160, 2), AtlasGold);
        Texture2D cursor = Resources.Load<Texture2D>("MapAtlas/OdysseyCursor");
        if (cursor == null)
        {
            _fallbackCursor = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            var px = new Color[64 * 64];
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), Vector2.one * 32);
                float alpha = Mathf.Clamp01(1.5f - Mathf.Abs(d - 25)) + Mathf.Clamp01(1 - Mathf.Abs(d - 21)) * 0.4f;
                if (d < 1.4f) alpha = 1;
                px[y * 64 + x] = new Color(0.96f, 0.94f, 0.83f, Mathf.Clamp01(alpha));
            }
            _fallbackCursor.SetPixels(px); _fallbackCursor.Apply(); cursor = _fallbackCursor;
        }
        _atlasCursor = NewRawImage("Odyssey cursor", _fullRoot, cursor).rectTransform;
        _atlasCursor.sizeDelta = new Vector2(64, 64);
        var shadow = _atlasCursor.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.65f); shadow.effectDistance = new Vector2(1, -1);
    }

    bool HandleAtlasControls(Mouse mouse)
    {
        Vector2 p = mouse.position.ReadValue();
        if (RectTransformUtility.RectangleContainsScreenPoint(_closeRect, p))
        {
            _dragging = false;
            if (mouse.leftButton.wasPressedThisFrame) SetOpen(false, false);
            return true;
        }
        if (RectTransformUtility.RectangleContainsScreenPoint(_recenterRect, p))
        {
            _dragging = false;
            if (mouse.leftButton.wasPressedThisFrame && player != null)
                _fullCenter = new Vector2(player.transform.position.x, player.transform.position.z);
            return true;
        }
        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_fullRoot, p, null, out local);
        if (local.y > _fullRoot.rect.yMax - 104 || local.y < _fullRoot.rect.yMin + 78) { _dragging = false; return true; }
        return false;
    }

    void UpdateAtlasCursor()
    {
        // Draw the cursor in the overlay canvas so it remains visible in paused gameplay.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = false;
        Vector2 p = Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(Screen.width, Screen.height) * 0.5f;
        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_fullRoot, p, null, out local);
        if (_atlasCursor != null) { _atlasCursor.anchoredPosition = local; _atlasCursor.SetAsLastSibling(); }
        bool hover = RectTransformUtility.RectangleContainsScreenPoint(_closeRect, p);
        if (_closeFace != null) _closeFace.color = hover ? new Color(1.7f, 1.5f, 1.1f, 1) : Color.white;
        if (_scaleLabel != null) _scaleLabel.text = Mathf.RoundToInt(_fullViewSize * 160f / Mathf.Max(1, _fullView.rect.height)) + " m";
    }

    void RestoreAtlasInput()
    {
        if (_restoreFrame >= 0)
        {
            foreach (var b in _disabled) if (b != null) b.enabled = true;
            _disabled.Clear(); _restoreFrame = -1;
            Cursor.lockState = _prevCursorLock; Cursor.visible = _prevCursorVisible;
        }
        if (_fallbackCursor != null) Destroy(_fallbackCursor);
    }

    void OnDisable()
    {
        if (isOpen) SetOpen(false, true);
        RestoreAtlasInput();
    }
}
