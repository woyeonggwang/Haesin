using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// 밤낮 준비(2026-10-09). MCP 에서 DayNightSetup.Build() 를 부름.
///   1) 별 큐브맵 `08.Environment/Sky/T_StarsCubemap.asset`(512² RGBA32, 무작위 별 9,000 개, 밝기 거듭제곱 분포, 은하수 띠 약간 — 밝기는 PBS spaceEmissionMultiplier 로) — 이미 있으면 다시 만들지 않음(forceStars 로 다시).
///   2) `Assets/Settings/SkyAndFog Profile.asset` 에 Physically Based Sky 를 넣고(없을 때) Visual Environment 하늘 종류를 PBS 로.
///      별 = spaceEmissionTexture, 하늘은 실시간 갱신(updateMode Realtime, 1 s). GradientSky 는 지우지 않고 남겨 둠(하늘 종류만 바꿈).
///   3) 씬 루트 `DayNightCycle`(+ 자식 `Moon` 방향광: 그림자 Soft, 원반 0.55°, 푸른빛) — 이미 있으면 그대로 두고 참조만 채움.
///   해 원반은 Directional Light 의 HDAdditionalLightData(angularDiameter·interactsWithSky)로 PBS 가 그림.
/// </summary>
public static class DayNightSetup
{
    const string ProfilePath = "Assets/Settings/SkyAndFog Profile.asset";
    const string StarsPath = "Assets/02.Resources/00.Component/08.Environment/Sky/T_StarsCubemap.asset";

    public static string Build(bool forceStars)
    {
        var sb = new System.Text.StringBuilder();
        var stars = AssetDatabase.LoadAssetAtPath<Cubemap>(StarsPath);
        if (stars == null || forceStars) { stars = MakeStars(); sb.AppendLine("별 큐브맵 만듦"); }

        var prof = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        PhysicallyBasedSky pbs;
        if (!prof.TryGet(out pbs)) { pbs = prof.Add<PhysicallyBasedSky>(false); AssetDatabase.AddObjectToAsset(pbs, prof); sb.AppendLine("PBS 넣음"); }
        pbs.active = true;
        pbs.spaceEmissionTexture.Override(stars);
        pbs.spaceEmissionMultiplier.Override(1f);
        pbs.exposure.Override(0f);
        pbs.updateMode.Override(EnvironmentUpdateMode.Realtime);
        pbs.updatePeriod.Override(1f);
        VisualEnvironment ve;
        if (prof.TryGet(out ve)) { ve.skyType.Override((int)SkyType.PhysicallyBased); sb.AppendLine("하늘 종류 → PBS"); }
        EditorUtility.SetDirty(pbs); EditorUtility.SetDirty(prof);
        AssetDatabase.SaveAssets();

        // 해 원반
        Light sun = null;
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (l.type == LightType.Directional && l.name == "Directional Light") sun = l;
        if (sun != null)
        {
            var hd = sun.GetComponent<HDAdditionalLightData>();
            hd.interactsWithSky = true; hd.angularDiameter = 0.53f; hd.flareSize = 2f; hd.flareFalloff = 4f;
            EditorUtility.SetDirty(hd);
        }

        // DayNightCycle + 달
        GameObject root = null;
        foreach (var r in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()) if (r.name == "DayNightCycle") root = r;
        if (root == null) root = new GameObject("DayNightCycle");
        var dn = root.GetComponent<DayNightCycle>(); if (dn == null) dn = root.AddComponent<DayNightCycle>();
        var mt = root.transform.Find("Moon");
        Light moon;
        if (mt == null)
        {
            var mg = new GameObject("Moon"); mg.transform.SetParent(root.transform, false);
            moon = mg.AddComponent<Light>(); moon.type = LightType.Directional;
            var mhd = mg.GetComponent<HDAdditionalLightData>(); if (mhd == null) mhd = mg.AddComponent<HDAdditionalLightData>();
            moon.shadows = LightShadows.Soft;
            mhd.SetIntensity(2.5f, LightUnit.Lux);
            mhd.angularDiameter = 0.55f; mhd.interactsWithSky = true; mhd.flareSize = 0.3f; mhd.flareFalloff = 8f;
            mhd.surfaceTint = new Color(0.85f, 0.88f, 0.95f);
            moon.color = new Color(0.72f, 0.80f, 1f);
            moon.lightmapBakeType = LightmapBakeType.Realtime;
            moon.enabled = false;
            sb.AppendLine("달 만듦");
        }
        else moon = mt.GetComponent<Light>();
        dn.sun = sun; dn.moon = moon;
        foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None)) { if (v.isGlobal && v.sharedProfile == prof) dn.globalVolume = v; if (v.name == "CaveInteriorVolume") dn.caveVolume = v; }
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        sb.AppendLine("DayNightCycle sun " + (sun != null) + " moon " + (moon != null) + " global " + (dn.globalVolume != null) + " cave " + (dn.caveVolume != null));
        return sb.ToString();
    }

    static Cubemap MakeStars()
    {
        const int N = 512;
        var cm = new Cubemap(N, TextureFormat.RGBA32, false);
        var faces = new Color[6][];
        for (int f = 0; f < 6; f++) { faces[f] = new Color[N * N]; }
        var rnd = new System.Random(20261009);
        // 은하수 띠(희미하게): 기울어진 큰 원 둘레
        Vector3 bandN = new Vector3(0.3f, 0.5f, 0.81f).normalized;
        for (int f = 0; f < 6; f++)
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    Vector3 d = Dir((CubemapFace)f, (x + 0.5f) / N * 2f - 1f, (y + 0.5f) / N * 2f - 1f);
                    float b = Mathf.Exp(-Mathf.Pow(Vector3.Dot(d, bandN) / 0.12f, 2f)) * 0.03f;
                    faces[f][y * N + x] = new Color(b * 0.9f, b * 0.95f, b * 1.1f, 1f);
                }
        for (int i = 0; i < 9000; i++)
        {
            // 구 위 고른 방향
            float z = (float)rnd.NextDouble() * 2f - 1f, t = (float)rnd.NextDouble() * Mathf.PI * 2f, r = Mathf.Sqrt(1f - z * z);
            Vector3 d = new Vector3(r * Mathf.Cos(t), z, r * Mathf.Sin(t));
            int face; float u, v; ToFace(d, out face, out u, out v);
            int px = Mathf.Clamp((int)((u * 0.5f + 0.5f) * N), 0, N - 1), py = Mathf.Clamp((int)((v * 0.5f + 0.5f) * N), 0, N - 1);
            float m = Mathf.Pow((float)rnd.NextDouble(), 6f); // 밝은 별은 드묾
            float lum = 0.08f + m * 0.92f;
            float temp = (float)rnd.NextDouble();
            Color c = Color.Lerp(new Color(1f, 0.82f, 0.66f), new Color(0.75f, 0.85f, 1f), temp) * lum;
            c.a = 1f;
            var arr = faces[face]; int k = py * N + px;
            arr[k] = new Color(Mathf.Min(1f, arr[k].r + c.r), Mathf.Min(1f, arr[k].g + c.g), Mathf.Min(1f, arr[k].b + c.b), 1f);
            if (lum > 0.5f) // 밝은 별은 옆 칸에도 조금
                for (int oy = -1; oy <= 1; oy++) for (int ox = -1; ox <= 1; ox++)
                    {
                        if (ox == 0 && oy == 0) continue;
                        int qx = px + ox, qy = py + oy; if (qx < 0 || qy < 0 || qx >= N || qy >= N) continue;
                        int q = qy * N + qx; arr[q] = new Color(arr[q].r + c.r * 0.15f, arr[q].g + c.g * 0.15f, arr[q].b + c.b * 0.15f, 1f);
                    }
        }
        for (int f = 0; f < 6; f++) cm.SetPixels(faces[f], (CubemapFace)f);
        cm.Apply(false, false);
        if (!AssetDatabase.IsValidFolder("Assets/02.Resources/00.Component/08.Environment/Sky")) AssetDatabase.CreateFolder("Assets/02.Resources/00.Component/08.Environment", "Sky");
        var old = AssetDatabase.LoadAssetAtPath<Cubemap>(StarsPath);
        if (old != null) { EditorUtility.CopySerialized(cm, old); Object.DestroyImmediate(cm); cm = old; }
        else AssetDatabase.CreateAsset(cm, StarsPath);
        AssetDatabase.SaveAssets();
        return cm;
    }

    // 큐브맵 면 좌표(u, v ∈ −1..1, v 는 아래로 증가하는 텍스처 행) ↔ 방향 — Unity 큐브맵 규칙
    static Vector3 Dir(CubemapFace f, float u, float v)
    {
        switch (f)
        {
            case CubemapFace.PositiveX: return new Vector3(1f, -v, -u).normalized;
            case CubemapFace.NegativeX: return new Vector3(-1f, -v, u).normalized;
            case CubemapFace.PositiveY: return new Vector3(u, 1f, v).normalized;
            case CubemapFace.NegativeY: return new Vector3(u, -1f, -v).normalized;
            case CubemapFace.PositiveZ: return new Vector3(u, -v, 1f).normalized;
            default: return new Vector3(-u, -v, -1f).normalized;
        }
    }

    static void ToFace(Vector3 d, out int face, out float u, out float v)
    {
        float ax = Mathf.Abs(d.x), ay = Mathf.Abs(d.y), az = Mathf.Abs(d.z);
        if (ax >= ay && ax >= az) { if (d.x > 0) { face = 0; u = -d.z / ax; v = -d.y / ax; } else { face = 1; u = d.z / ax; v = -d.y / ax; } }
        else if (ay >= az) { if (d.y > 0) { face = 2; u = d.x / ay; v = d.z / ay; } else { face = 3; u = d.x / ay; v = -d.z / ay; } }
        else { if (d.z > 0) { face = 4; u = d.x / az; v = -d.y / az; } else { face = 5; u = -d.x / az; v = -d.y / az; } }
    }
}
