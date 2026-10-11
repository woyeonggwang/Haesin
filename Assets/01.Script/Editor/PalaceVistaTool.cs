using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering.HighDefinition.Attributes;

/// <summary>
/// 궁궐 원경 그림(PalaceVista 의 card) 만들기 — MCP 에서 정적 메서드로 부름.
/// Capture(eye, …): 눈 위치(마을 쪽)에서 궁궐(GyeongBok − 육조거리)만 HDRP AOV 로 찍어
///   기본색(알파 = 궁이 있는 픽셀) + 월드 노멀(= 판의 물체 공간 노멀, 판 회전 0)을 PNG 로 저장하고,
///   눈에서 본 궁궐의 시야 범위(비대칭 절두체)를 궁 맨 앞 깊이의 평면에 그대로 펼친 사각형 메시를 만든다
///   → 그 눈 위치에서는 진짜 궁과 같은 자리에 같은 모양으로 보이고, 조명은 실시간(물체 공간 노멀)으로 받는다.
/// 궁 렌더러만 찍으려고 잠깐 레이어 captureLayer 로 옮겼다가 되돌린다(씬은 바꾸지 않음).
/// </summary>
public static class PalaceVistaTool
{
    public const string Dir = "Assets/02.Resources/00.Component/08.Environment/PalaceVista";
    public static int captureLayer = 30;

    static List<Renderer> PalaceRenderers(Transform root, string[] keep)
    {
        var list = new List<Renderer>();
        foreach (var r in root.GetComponentsInChildren<Renderer>(false))
        {
            if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
            if (r is ParticleSystemRenderer) continue;
            bool skip = false;
            for (Transform t = r.transform; t != null && t != root; t = t.parent)
                if (t.parent == root && System.Array.IndexOf(keep, t.name) >= 0) { skip = true; break; }
            if (!skip) list.Add(r);
        }
        return list;
    }

    /// <summary>궁궐을 찍는다. 결과: Dir/T_PalaceVista_BaseColor.png, T_PalaceVista_Normal.png, PalaceVista_Card.asset. 반환 = 보고 문자열.</summary>
    public static string Capture(Vector3 eye, int width, string keepCsv, float maxHeightPx)
    {
        var rootGo = GameObject.Find("GyeongBok"); if (rootGo == null) return "no GyeongBok";
        var root = rootGo.transform;
        string[] keep = keepCsv.Split(',');
        var rs = PalaceRenderers(root, keep);
        if (rs.Count == 0) return "no renderers";

        // 앞 방향 = 눈에서 궁 전체 중심(수평)
        var all = new Bounds(rs[0].bounds.center, Vector3.zero);
        foreach (var r in rs) all.Encapsulate(r.bounds);
        Vector3 fwd = all.center - eye; fwd.y = 0f; fwd.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized, up = Vector3.up;
        float l = 1e9f, rr = -1e9f, b = 1e9f, t = -1e9f, dmin = 1e9f;
        var c8 = new Vector3[8];
        foreach (var r in rs)
        {
            var bb = r.bounds; Vector3 mn = bb.min, mx = bb.max;
            for (int i = 0; i < 8; i++)
            {
                c8[i] = new Vector3((i & 1) == 0 ? mn.x : mx.x, (i & 2) == 0 ? mn.y : mx.y, (i & 4) == 0 ? mn.z : mx.z);
                Vector3 v = c8[i] - eye; float z = Vector3.Dot(v, fwd); if (z < 1f) continue;
                float x = Vector3.Dot(v, right) / z, y = Vector3.Dot(v, up) / z;
                if (x < l) l = x; if (x > rr) rr = x; if (y < b) b = y; if (y > t) t = y;
                if (z < dmin) dmin = z;
            }
        }
        // 약간 여유
        float padX = (rr - l) * 0.01f, padY = (t - b) * 0.03f; l -= padX; rr += padX; b -= padY; t += padY;
        int W = width; int H = Mathf.Clamp(Mathf.RoundToInt(W * (t - b) / (rr - l)), 64, (int)maxHeightPx);
        // 높이를 잘랐으면 그만큼 가로를 줄여 픽셀이 정사각형이 되게
        if (H == (int)maxHeightPx) W = Mathf.RoundToInt(H * (rr - l) / (t - b));
        W = (W + 3) / 4 * 4; H = (H + 3) / 4 * 4; // 블록 압축(BC7)은 4 의 배수

        // 레이어 옮기기
        var oldLayers = new Dictionary<GameObject, int>();
        foreach (var r in rs) { if (!oldLayers.ContainsKey(r.gameObject)) oldLayers[r.gameObject] = r.gameObject.layer; r.gameObject.layer = captureLayer; }

        var camGo = new GameObject("__VistaCaptureCam"); camGo.hideFlags = HideFlags.HideAndDontSave;
        var cam = camGo.AddComponent<Camera>();
        var hd = camGo.AddComponent<HDAdditionalCameraData>();
        string report = "";
        try
        {
            camGo.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(fwd, Vector3.up));
            cam.cullingMask = 1 << captureLayer;
            cam.nearClipPlane = 1f; cam.farClipPlane = 4000f;
            cam.fieldOfView = Mathf.Atan(Mathf.Max(Mathf.Abs(b), Mathf.Abs(t))) * 2f * Mathf.Rad2Deg;
            cam.projectionMatrix = Matrix4x4.Frustum(l, rr, b, t, 1f, 4000f);
            hd.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
            hd.backgroundColorHDR = Color.black;
            hd.antialiasing = HDAdditionalCameraData.AntialiasingMode.None;

            // 계단 현상·먼 기와 무늬를 줄이려고 supersample 배로 크게 찍어 평균으로 줄임(AA 없음)
            int ss = Mathf.Max(1, supersample);
            var albedo = RenderAOV(cam, hd, W * ss, H * ss, MaterialSharedProperty.Albedo);
            var normal = RenderAOV(cam, hd, W * ss, H * ss, MaterialSharedProperty.Normal);
            SaveTextures(albedo, normal, W * ss, H * ss, ss); // 알파 AOV 는 안 씀(배경도 1)
            report = "W=" + W + " H=" + H + " l=" + l.ToString("F4") + " r=" + rr.ToString("F4") + " b=" + b.ToString("F4") + " t=" + t.ToString("F4") + " dmin=" + dmin.ToString("F1") + " cover=" + lastCoverage + " bgN=" + lastBackgroundNormal.ToString("F3") + " fwd=" + fwd.ToString("F3") + " renderers=" + rs.Count;
            BuildCard(eye, fwd, right, l, rr, b, t, dmin);
        }
        finally
        {
            foreach (var kv in oldLayers) if (kv.Key != null) kv.Key.layer = kv.Value;
            Object.DestroyImmediate(camGo);
        }
        return report;
    }

    // 원시 AOV 결과(선형 float)를 돌려줌.
    public static Color[] RenderAOV(Camera cam, HDAdditionalCameraData hd, int W, int H, MaterialSharedProperty prop)
    {
        var target = new RenderTexture(W, H, 24, GraphicsFormat.R32G32B32A32_SFloat);
        var outRT = new RenderTexture(W, H, 0, GraphicsFormat.R32G32B32A32_SFloat);
        RTHandle buf = null;
        try
        {
            cam.targetTexture = target;
            var req = AOVRequest.NewDefault(); req.SetFullscreenOutput(prop);
            var builder = new AOVRequestBuilder();
            builder.Add(req,
                id => { buf = RTHandles.Alloc(W, H, colorFormat: GraphicsFormat.R32G32B32A32_SFloat, name: "vistaAOV"); return buf; },
                null,
                new[] { AOVBuffers.Color },
                (cmd, textures, props) => { cmd.Blit(textures[0], outRT); });
            hd.SetAOVRequests(builder.Build());
            cam.Render();
            hd.SetAOVRequests(null);
            var tex = new Texture2D(W, H, TextureFormat.RGBAFloat, false, true);
            var prev = RenderTexture.active; RenderTexture.active = outRT;
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
            RenderTexture.active = prev;
            var px = tex.GetPixels();
            Object.DestroyImmediate(tex);
            return px;
        }
        finally
        {
            cam.targetTexture = null;
            if (buf != null) RTHandles.Release(buf);
            target.Release(); Object.DestroyImmediate(target);
            outRT.Release(); Object.DestroyImmediate(outRT);
        }
    }

    // 시험용: AOV 값이 선형인지 sRGB 인지, 노멀 부호화가 어떤지 보려고 회색 판 하나를 찍어 픽셀 하나를 돌려줌
    public static string ProbeAOV(Vector3 eye, Vector3 fwd)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad); q.hideFlags = HideFlags.HideAndDontSave;
        q.layer = captureLayer;
        q.transform.position = eye + fwd * 10f; q.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up); q.transform.localScale = Vector3.one * 4f;
        var m = new Material(Shader.Find("HDRP/Lit")); m.SetColor("_BaseColor", new Color(0.5f, 0.5f, 0.5f, 1f));
        q.GetComponent<MeshRenderer>().sharedMaterial = m;
        var camGo = new GameObject("__ProbeCam"); camGo.hideFlags = HideFlags.HideAndDontSave;
        var cam = camGo.AddComponent<Camera>(); var hd = camGo.AddComponent<HDAdditionalCameraData>();
        string s = "";
        try
        {
            camGo.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(fwd, Vector3.up));
            cam.cullingMask = 1 << captureLayer; cam.fieldOfView = 40f;
            hd.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color; hd.backgroundColorHDR = Color.black;
            var a = RenderAOV(cam, hd, 64, 64, MaterialSharedProperty.Albedo);
            var n = RenderAOV(cam, hd, 64, 64, MaterialSharedProperty.Normal);
            var al = RenderAOV(cam, hd, 64, 64, MaterialSharedProperty.Alpha);
            s = "albedo c=" + a[32 * 64 + 32] + " bg=" + a[0] + " | normal c=" + n[32 * 64 + 32] + " bg=" + n[0] + " | alpha c=" + al[32 * 64 + 32] + " bg=" + al[0] + " | quad normal(world)=" + (-fwd).ToString("F3");
        }
        finally { Object.DestroyImmediate(q); Object.DestroyImmediate(camGo); Object.DestroyImmediate(m); }
        return s;
    }

    // ProbeAOV 로 확인(2026-10-05, HDRP 17.3): 회색 0.5(sRGB) 판 → 기본색 0.213 = 선형, 노멀 (0,0,−1) → (0.5, 0.5, 0.0) = n·0.5+0.5
    public static bool albedoIsLinear = true;
    public static bool normalIsEncoded01 = true;   // true = (n*0.5+0.5)
    public static Vector3 lastBackgroundNormal; public static int lastCoverage;

    public static int supersample = 2;

    // albedo·normal = 찍은 크기(Wr×Hr) → ss 배 줄여 저장. 알파 = 칸 안에서 궁이 덮은 비율(알파 잘라내기 0.5).
    static void SaveTextures(Color[] albedo, Color[] normal, int Wr, int Hr, int ss)
    {
        Directory.CreateDirectory(Dir);
        int N = Wr * Hr;
        // 알파 AOV 는 배경도 1 로 나옴(ProbeAOV 측정, HDRP 17.3) → 궁이 있는 픽셀 = 기본색이 0 이 아니거나 노멀이 배경 노멀과 다른 곳.
        // 배경 노멀 = 기본색이 0 인 픽셀에서 가장 흔한 노멀 값(측정: (0.5, 0.992, 0.5)).
        var count = new Dictionary<int, int>(); int bgKey = -1, bgCount = 0;
        for (int i = 0; i < N; i++)
        {
            Color a = albedo[i]; if (a.r + a.g + a.b > 1e-6f) continue;
            int key = Mathf.RoundToInt(normal[i].r * 255f) | (Mathf.RoundToInt(normal[i].g * 255f) << 8) | (Mathf.RoundToInt(normal[i].b * 255f) << 16);
            int c; count.TryGetValue(key, out c); c++; count[key] = c;
            if (c > bgCount) { bgCount = c; bgKey = key; }
        }
        Vector3 bgN = new Vector3((bgKey & 255) / 255f, ((bgKey >> 8) & 255) / 255f, ((bgKey >> 16) & 255) / 255f);
        var mask = new bool[N];
        for (int i = 0; i < N; i++)
        {
            Color a = albedo[i];
            bool hasAlbedo = a.r + a.g + a.b > 1e-6f;
            bool otherNormal = bgKey < 0 || Mathf.Abs(normal[i].r - bgN.x) > 0.01f || Mathf.Abs(normal[i].g - bgN.y) > 0.01f || Mathf.Abs(normal[i].b - bgN.z) > 0.01f;
            mask[i] = hasAlbedo || otherNormal;
        }
        lastBackgroundNormal = bgN;
        // ss×ss 칸 평균(궁 픽셀만) — 기본색은 선형으로 평균한 뒤 감마로
        int W = Wr / ss, H = Hr / ss; N = W * H;
        var col = new Color[N]; var nor = new Color[N]; var filled = new bool[N];
        lastCoverage = 0;
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
        {
            Vector3 sa = Vector3.zero, sn = Vector3.zero; int k = 0;
            for (int dy = 0; dy < ss; dy++) for (int dx = 0; dx < ss; dx++)
            {
                int i = (y * ss + dy) * Wr + (x * ss + dx); if (!mask[i]) continue;
                Color a = albedo[i];
                Vector3 lin = albedoIsLinear ? new Vector3(a.r, a.g, a.b) : new Vector3(Mathf.GammaToLinearSpace(a.r), Mathf.GammaToLinearSpace(a.g), Mathf.GammaToLinearSpace(a.b));
                sa += lin;
                Vector3 n = new Vector3(normal[i].r, normal[i].g, normal[i].b);
                if (normalIsEncoded01) n = n * 2f - Vector3.one;
                if (n.sqrMagnitude > 1e-4f) sn += n.normalized;
                k++;
            }
            int o = y * W + x;
            if (k == 0) { col[o] = Color.clear; nor[o] = new Color(0.5f, 1f, 0.5f, 1f); continue; }
            sa /= k; float cov = k / (float)(ss * ss);
            if (sn.sqrMagnitude < 1e-6f) sn = Vector3.up; sn.Normalize();
            col[o] = new Color(Mathf.LinearToGammaSpace(Mathf.Clamp01(sa.x)), Mathf.LinearToGammaSpace(Mathf.Clamp01(sa.y)), Mathf.LinearToGammaSpace(Mathf.Clamp01(sa.z)), cov);
            nor[o] = new Color(sn.x * 0.5f + 0.5f, sn.y * 0.5f + 0.5f, sn.z * 0.5f + 0.5f, 1f);
            filled[o] = true; if (cov >= 0.5f) lastCoverage++;
        }
        // 빈 곳으로 색 번지기(밉맵 가장자리 검은 테 방지)
        for (int pass = 0; pass < 24; pass++)
        {
            var add = new List<int>();
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
            {
                int i = y * W + x; if (filled[i]) continue;
                Color sc = Color.clear, sn = Color.clear; int k = 0;
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                {
                    int xx = x + dx, yy = y + dy; if (xx < 0 || yy < 0 || xx >= W || yy >= H) continue;
                    int j = yy * W + xx; if (!filled[j]) continue;
                    sc += col[j]; sn += nor[j]; k++;
                }
                if (k > 0) { col[i] = new Color(sc.r / k, sc.g / k, sc.b / k, 0f); nor[i] = sn / k; add.Add(i); }
            }
            if (add.Count == 0) break;
            foreach (var i in add) filled[i] = true;
        }
        var tc = new Texture2D(W, H, TextureFormat.RGBA32, false); tc.SetPixels(col); tc.Apply();
        File.WriteAllBytes(Dir + "/T_PalaceVista_BaseColor.png", tc.EncodeToPNG()); Object.DestroyImmediate(tc);
        var tn = new Texture2D(W, H, TextureFormat.RGBA32, false, true); tn.SetPixels(nor); tn.Apply();
        File.WriteAllBytes(Dir + "/T_PalaceVista_Normal.png", tn.EncodeToPNG()); Object.DestroyImmediate(tn);
        AssetDatabase.ImportAsset(Dir + "/T_PalaceVista_BaseColor.png");
        AssetDatabase.ImportAsset(Dir + "/T_PalaceVista_Normal.png");
        foreach (var p in new[] { "BaseColor", "Normal" })
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(Dir + "/T_PalaceVista_" + p + ".png");
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = p == "BaseColor";
            ti.alphaSource = p == "BaseColor" ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            ti.alphaIsTransparency = false;
            ti.mipmapEnabled = true;
            ti.mipMapsPreserveCoverage = p == "BaseColor"; ti.alphaTestReferenceValue = 0.5f;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.npotScale = TextureImporterNPOTScale.None; // 기본값(ToNearest)이면 4096×173 이 4096×128 로 줄어듦
            ti.maxTextureSize = 8192;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.SaveAndReimport();
        }
    }

    static void BuildCard(Vector3 eye, Vector3 fwd, Vector3 right, float l, float r, float b, float t, float D)
    {
        Vector3 C(float x, float y) { return eye + fwd * D + right * (x * D) + Vector3.up * (y * D); }
        var p = new[] { C(l, b), C(r, b), C(r, t), C(l, t) };
        Vector3 center = (p[0] + p[2]) * 0.5f;
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Dir + "/PalaceVista_Card.asset");
        bool create = mesh == null;
        if (create) mesh = new Mesh();
        mesh.Clear();
        mesh.name = "PalaceVista_Card";
        mesh.vertices = new[] { p[0] - center, p[1] - center, p[2] - center, p[3] - center };
        mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        mesh.normals = new[] { -fwd, -fwd, -fwd, -fwd };
        mesh.RecalculateTangents(); mesh.RecalculateBounds();
        // 앞면 = 눈 쪽: 감김이 반대면 뒤집음
        Vector3 fn = Vector3.Cross(p[2] - p[0], p[1] - p[0]);
        if (Vector3.Dot(fn, -fwd) < 0f) { mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 }; }
        if (create) AssetDatabase.CreateAsset(mesh, Dir + "/PalaceVista_Card.asset");
        EditorUtility.SetDirty(mesh); AssetDatabase.SaveAssets();
        EditorPrefs.SetString("PalaceVista_CardCenter", center.x + "," + center.y + "," + center.z);
    }
}
