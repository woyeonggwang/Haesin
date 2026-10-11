using UnityEditor;
using UnityEngine;

/// <summary>
/// 섬 터레인 생성기 (에디터 전용, 메뉴 HaeSin/섬 터레인 생성).
///
/// 섬 위치는 씬의 IslandPlan 아래 계획 큐브(이름으로 찾음, 비활성이어도 됨)의 XZ 중심을 쓴다 —
/// 큐브를 옮기고 다시 실행하면 그 자리로 다시 만든다(같은 시드라 모양은 같음).
/// 섬 하나 = Terrain 하나. 타일 = 육지 크기 + 사방 margin(해저 경사가 타일 가장자리에서 −45 m 로 내려가게).
/// 높이: 모양(둥근 사각 + 해안선 노이즈) → 해안에서 안쪽 거리 e(m) → 바다는 e 에 따라 −45 m 까지,
///       육지는 해변(40 m) 뒤 섬별 지형 함수. 텍스처는 Base 터레인의 8레이어를 높이·경사 규칙으로 칠함.
/// 결과 TerrainData = Assets/02.Resources/00.Component/08.Environment/Islands/*.asset (덮어씀).
/// </summary>
public static class IslandTerrainGenerator
{
    public enum Kind { JoseonLarge, JoseonMid, SmallPeak, SmallTwin, JapanCastle, Wokou }

    public struct Def
    {
        public string key, planName; public Kind kind; public float land; public int seed;
        public float dockAngle;               // 섬 중심에서 선착장 쪽 방향(°, 0 = +x 동, 90 = +z 북)
        public string[] moored;               // 정박선 원본 이름(첫째 = 선착장 옆, 둘째 = 앞바다)
        public Def(string k, string p, Kind kd, float l, int s, float dock, string[] m) { key = k; planName = p; kind = kd; land = l; seed = s; dockAngle = dock; moored = m; }
    }

    public static readonly Def[] Islands =
    {
        new Def("Joseon_Large", "Plan_Joseon_Large_1000", Kind.JoseonLarge, 1000f, 11, -81.6f, new[] { "panok_navy", "merchant" }),
        new Def("Joseon_Mid",   "Plan_Joseon_Mid_800",    Kind.JoseonMid,   800f, 23, -70.7f, new[] { "panok_navy", "merchant" }),
        new Def("Joseon_Small_A", "Plan_Joseon_Small_600_A", Kind.SmallPeak, 600f, 37, -40.6f, new[] { "panok_navy" }),
        new Def("Joseon_Small_B", "Plan_Joseon_Small_600_B", Kind.SmallTwin, 600f, 41, -90f, new[] { "panok_navy" }),
        // 일본섬: 처음 800 은 둥근 모양·만 때문에 실제 땅이 좁아(사용자 지적) 1100 으로 키움
        new Def("Japan_Castle", "Plan_Japan_1100",        Kind.JapanCastle, 1100f, 53, 135f, new[] { "anteck", "seki" }),
        // 왜구섬(2026-10-07, 사용자 선택 "일본섬 남서쪽 무인도"): 작은 바위섬, 선착장은 일본섬 쪽(북동 30°) 만 안, 그 뒤 소굴 터(약 6 m 평지), 남서쪽 산(약 60 m).
        new Def("Wokou_Den",    "Plan_Wokou_400",         Kind.Wokou,       400f, 67, 30f, new[] { "seki_wokou", "anteck_wokou" }),
    };

    /// <summary>섬 하나만 만든다(이름 = Def.key). 다른 섬은 손대지 않음 — 메뉴(전부 다시 만들기)는 손으로 다듬은 섬을 덮어써서 비활성.</summary>
    public static Terrain GenerateOne(string key)
    {
        foreach (var d in Islands) if (d.key == key) { var t = Generate(d); AssetDatabase.SaveAssets(); return t; }
        Debug.LogError("[Island] 없는 섬: " + key);
        return null;
    }

    const float Margin = 200f;      // 육지 밖 해저 경사 폭(m)
    const float BaseY = -60f, SizeY = 300f; // 높이 범위 −60 ~ +240
    const int HeightRes = 1025, AlphaRes = 1024;
    const string Folder = "Assets/02.Resources/00.Component/08.Environment/Islands";

    // 메뉴 비활성화(사용자 지시, 2026-09-26): 섬 지형을 손으로 다듬기 시작해 다시 돌리면 덮어써진다. 필요하면 아래 줄의 주석을 풀 것.
    // [MenuItem("HaeSin/섬 터레인 생성")]
    public static void GenerateAll()
    {
        foreach (var d in Islands) Generate(d);
        AssetDatabase.SaveAssets();
    }

    public static Terrain Generate(Def d)
    {
        var planRoot = GameObject.Find("IslandPlan");
        Transform plan = planRoot != null ? planRoot.transform.Find(d.planName) : null;
        if (plan == null) { Debug.LogError("[Island] 계획 큐브 없음: " + d.planName); return null; }
        Vector2 c = new Vector2(plan.position.x, plan.position.z);
        float tile = d.land + Margin * 2f;

        var baseT = GameObject.Find("BaseGroup/Base") != null ? GameObject.Find("BaseGroup/Base").GetComponent<Terrain>() : Terrain.activeTerrain;

        // ---- TerrainData ----
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/02.Resources/00.Component/08.Environment", "Islands");
        string path = Folder + "/Island_" + d.key + ".asset";
        var td = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
        if (td == null) { td = new TerrainData(); AssetDatabase.CreateAsset(td, path); }
        td.heightmapResolution = HeightRes;
        td.alphamapResolution = AlphaRes;
        td.size = new Vector3(tile, SizeY, tile);
        if (baseT != null) td.terrainLayers = baseT.terrainData.terrainLayers;

        // ---- 높이 ----
        var h = new float[HeightRes, HeightRes];
        var world = new float[HeightRes * HeightRes];
        for (int j = 0; j < HeightRes; j++)
            for (int i = 0; i < HeightRes; i++)
            {
                float x = (i / (float)(HeightRes - 1) - 0.5f) * tile; // 섬 중심 기준
                float z = (j / (float)(HeightRes - 1) - 0.5f) * tile;
                float y = Height(d, x, z);
                world[j * HeightRes + i] = y;
                h[j, i] = Mathf.Clamp01((y - BaseY) / SizeY);
            }
        Vector2 dockFoot, dockDir;
        FindDock(d, out dockFoot, out dockDir);
        ApplyDockPad(h, world, tile, dockFoot, dockDir);
        td.SetHeights(0, 0, h);

        // ---- 텍스처 ----
        Paint(td, world, tile, d.seed);
        EditorUtility.SetDirty(td);

        // ---- 오브젝트 ----
        var root = GameObject.Find("Islands");
        if (root == null) root = new GameObject("Islands");
        string goName = "Island_" + d.key;
        var tr = root.transform.Find(goName);
        GameObject go = tr != null ? tr.gameObject : Terrain.CreateTerrainGameObject(td);
        go.name = goName;
        go.transform.SetParent(root.transform, true);
        go.transform.position = new Vector3(c.x - tile * 0.5f, BaseY, c.y - tile * 0.5f);
        var t = go.GetComponent<Terrain>();
        t.terrainData = td;
        var col = go.GetComponent<TerrainCollider>();
        if (col == null) col = go.AddComponent<TerrainCollider>();
        col.terrainData = td;
        if (baseT != null)
        {
            t.materialTemplate = baseT.materialTemplate;
            t.heightmapPixelError = baseT.heightmapPixelError;
            t.basemapDistance = baseT.basemapDistance;
            t.shadowCastingMode = baseT.shadowCastingMode;
            t.reflectionProbeUsage = baseT.reflectionProbeUsage;
            t.drawInstanced = baseT.drawInstanced;
            go.layer = baseT.gameObject.layer;
            GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(baseT.gameObject));
        }
        t.Flush();
        BuildDock(d, go.transform, c, dockFoot, dockDir);
        SetupTerritory(d, go.transform, c);
        EditorUtility.SetDirty(go);
        Debug.Log("[Island] " + goName + " 타일 " + tile + " m, 중심 " + c);
        return t;
    }

    // ================= 선착장 =================
    const float PadHeight = 1.8f;     // 선착장 육지 쪽 경사로 발치(Base 선착장: 판자 4.73, 경사로 끝 약 1.9)
    const float FootInland = 8f;      // 해안선에서 발치까지 육지 쪽 거리(m)
    const float RootFromFoot = 15.7f; // Base 선착장 루트 ~ 육지 쪽 경사로 끝(측정)

    /// <summary>섬 중심에서 dockAngle 방향으로 가다 처음 바다가 되는 곳(해안선) → 그보다 FootInland 안쪽이 발치.</summary>
    static void FindDock(Def d, out Vector2 foot, out Vector2 dir)
    {
        float a = d.dockAngle * Mathf.Deg2Rad;
        dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        float r0 = d.land * 0.5f;
        for (float r = 0f; r < d.land; r += 0.5f) if (Height(d, dir.x * r, dir.y * r) < 0f) { r0 = r; break; }
        foot = dir * (r0 - FootInland);
    }

    /// <summary>
    /// 발치 주변 육지를 PadHeight 로 평평하게(바다 쪽으로는 거의 안 번지게) + 발치에서 육지 쪽으로 올라가는 길(경사 약 9°, 폭 약 12 m)을
    /// 둑에 깎아 낸다 — 만(cove) 안쪽은 해안이 가팔라서 길이 없으면 선착장에서 내린 뒤 올라갈 수 없었다.
    /// </summary>
    static void ApplyDockPad(float[,] h, float[] world, float tile, Vector2 foot, Vector2 dir)
    {
        float cell = tile / (HeightRes - 1);
        int ci = Mathf.RoundToInt((foot.x / tile + 0.5f) * (HeightRes - 1)), cj = Mathf.RoundToInt((foot.y / tile + 0.5f) * (HeightRes - 1));
        int rr = Mathf.CeilToInt(140f / cell);
        for (int j = Mathf.Max(0, cj - rr); j <= Mathf.Min(HeightRes - 1, cj + rr); j++)
            for (int i = Mathf.Max(0, ci - rr); i <= Mathf.Min(HeightRes - 1, ci + rr); i++)
            {
                Vector2 q = new Vector2((i / (float)(HeightRes - 1) - 0.5f) * tile, (j / (float)(HeightRes - 1) - 0.5f) * tile) - foot;
                float along = Vector2.Dot(q, dir);
                int k = j * HeightRes + i;
                float lateral = Mathf.Abs(q.x * dir.y - q.y * dir.x);
                if (along < 0f && along > -135f)
                {
                    float roadY = PadHeight + (-along) * 0.16f;
                    float wr = SmoothStep(12f, 6f, lateral);
                    if (world[k] > roadY && wr > 0f) world[k] = Mathf.Lerp(world[k], roadY, wr);
                }
                float w = SmoothStep(24f, 9f, q.magnitude) * SmoothStep(10f, 3f, along);
                if (w > 0f) world[k] = Mathf.Lerp(world[k], PadHeight, w);
                h[j, i] = Mathf.Clamp01((world[k] - BaseY) / SizeY);
            }
    }

    /// <summary>Base 선착장(BaseGroup/swamp-bridge-4k-ready_2016)을 복제해 세우고 하차 지점·정박선을 단다. 바다 쪽 경사로(Ramp_Upper)는 뺀다.</summary>
    static void BuildDock(Def d, Transform island, Vector2 c, Vector2 foot, Vector2 dir)
    {
        var src = GameObject.Find("BaseGroup/swamp-bridge-4k-ready_2016");
        if (src == null) { Debug.LogWarning("[Island] Base 선착장 없음 — 선착장 생략"); return; }
        var old = island.Find("Dock");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var root = new GameObject("Dock").transform;
        root.SetParent(island, true);
        Vector2 rp = c + foot + dir * RootFromFoot;
        root.position = new Vector3(rp.x, 0f, rp.y);
        root.rotation = Quaternion.Euler(0f, Mathf.Atan2(-dir.y, dir.x) * Mathf.Rad2Deg, 0f); // 로컬 +x = 바다 쪽
        var model = Object.Instantiate(src, root);
        model.name = "DockModel";
        model.transform.localPosition = new Vector3(0f, src.transform.position.y, 0f);
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = src.transform.localScale;
        var seaRamp = model.transform.Find("Ramp_Upper");
        if (seaRamp != null) Object.DestroyImmediate(seaRamp.gameObject);
        var lp = new GameObject("LandingPoint_" + d.key).transform;
        lp.SetParent(root, false);
        lp.localPosition = new Vector3(6f, 6.5f, 0f);
        lp.gameObject.AddComponent<LandingPoint>().dockRange = 35f;
        var hm = root.gameObject.AddComponent<HarborMooring>();
        int n = d.moored != null ? d.moored.Length : 0;
        hm.spots = new HarborMooring.Spot[n];
        for (int i = 0; i < n; i++)
        {
            hm.spots[i].templateName = d.moored[i];
            // 첫째: 선착장 왼쪽(+z)에 나란히 — 오른쪽은 플레이어 배 자리로 비움. 둘째: 앞바다에 비스듬히.
            hm.spots[i].localPosition = i == 0 ? new Vector3(4f, 0f, 12f) : new Vector3(50f, 0f, 20f);
            hm.spots[i].localYaw = i == 0 ? 90f : 70f;
        }
        Debug.Log("[Island] 선착장 " + d.key + " 루트 " + root.position + " 방향 " + dir);
    }

    /// <summary>섬 가운데 FactionTerritory(일본섬 = Japanese, 나머지 = Navy). inner = 1.35 × 반폭 + 60, outer = inner + 700.</summary>
    static void SetupTerritory(Def d, Transform island, Vector2 c)
    {
        var tr = island.Find("Territory");
        var g = tr != null ? tr.gameObject : new GameObject("Territory");
        g.transform.SetParent(island, false);
        g.transform.position = new Vector3(c.x, 0f, c.y);
        var ft = g.GetComponent<FactionTerritory>();
        if (ft == null) ft = g.AddComponent<FactionTerritory>();
        ft.faction = d.kind == Kind.JapanCastle ? Faction.Japanese : d.kind == Kind.Wokou ? Faction.Wokou : Faction.Navy;
        ft.innerRadius = Mathf.Round(1.35f * d.land * 0.5f + 60f);
        ft.outerRadius = ft.innerRadius + (d.kind == Kind.Wokou ? 400f : 700f); // 왜구섬은 작은 소굴이라 영해도 좁게(일본 영해와 약 380 m 만 겹침)
    }

    // ================= 높이 함수 =================

    static float Fbm(float x, float z, int seed, int oct)
    {
        float a = 0.5f, f = 1f, s = 0f, n = 0f;
        float ox = seed * 17.13f + 100f, oz = seed * 31.7f + 100f;
        for (int k = 0; k < oct; k++)
        {
            s += a * (Mathf.PerlinNoise(x * f + ox, z * f + oz) * 2f - 1f);
            n += a; a *= 0.5f; f *= 2.03f;
        }
        return s / n; // −1..1 근처
    }

    static float Ridged(float x, float z, int seed, int oct)
    {
        float a = 0.5f, f = 1f, s = 0f, n = 0f;
        float ox = seed * 7.77f + 300f, oz = seed * 13.3f + 300f;
        for (int k = 0; k < oct; k++)
        {
            float v = 1f - Mathf.Abs(Mathf.PerlinNoise(x * f + ox, z * f + oz) * 2f - 1f);
            s += a * v * v; n += a; a *= 0.5f; f *= 2.1f;
        }
        return s / n; // 0..1
    }

    static float Gauss(float x, float z, float cx, float cz, float r)
    {
        float dx = x - cx, dz = z - cz;
        return Mathf.Exp(-(dx * dx + dz * dz) / (2f * r * r));
    }

    static float SmoothStep(float a, float b, float v) { float t = Mathf.Clamp01((v - a) / (b - a)); return t * t * (3f - 2f * t); }

    /// <summary>계단식 논밭·집터용 완만한 단(단 높이 step).</summary>
    static float Terrace(float y, float step)
    {
        float k = y / step, fl = Mathf.Floor(k);
        return step * (fl + SmoothStep(0.72f, 1f, k - fl));
    }

    /// <summary>해안선 안쪽으로의 거리(m, 바다는 음수). 둥근 사각형 + 노이즈 해안선 + 섬별 만(cove).</summary>
    static float Inland(Def d, float x, float z)
    {
        float S = d.land * 0.5f;
        float wx = x + Fbm(x / 260f, z / 260f, d.seed + 1, 3) * S * 0.18f;
        float wz = z + Fbm(x / 260f, z / 260f, d.seed + 2, 3) * S * 0.18f;
        float u = Mathf.Abs(wx / S), v = Mathf.Abs(wz / S);
        const float p = 2.6f;
        float dn = Mathf.Pow(Mathf.Pow(u, p) + Mathf.Pow(v, p), 1f / p);
        float ang = Mathf.Atan2(z, x);
        dn /= 1f + 0.08f * Fbm(Mathf.Cos(ang) * 1.3f, Mathf.Sin(ang) * 1.3f, d.seed + 3, 3);
        // 만(배를 댈 곳)
        switch (d.kind)
        {
            case Kind.JoseonLarge: dn += 0.85f * Gauss(x, z, 0.15f * S, -1.02f * S, 0.24f * S); break;
            case Kind.JoseonMid: dn += 0.90f * Gauss(x, z, 0.35f * S, -1.0f * S, 0.26f * S); break;
            case Kind.SmallTwin: dn += 0.90f * Gauss(x, z, 0f, -1.0f * S, 0.24f * S); break;
            case Kind.JapanCastle: dn += 0.55f * Gauss(x, z, -0.80f * S, 0.80f * S, 0.18f * S); break;
            case Kind.Wokou: dn += 0.80f * Gauss(x, z, 0.92f * S * 0.866f, 0.92f * S * 0.5f, 0.24f * S); break; // 선착장 쪽(30°) 숨은 만
        }
        return (1f - dn) * S;
    }

    static float Height(Def d, float x, float z)
    {
        float e = Inland(d, x, z);
        if (e <= 0f) return -45f * (1f - Mathf.Exp(e / 70f)); // 해저: 해안 0 → 바깥 −45
        float beach = Mathf.Min(e * 0.12f, 3.5f);              // 완만한 해변(약 30 m 에 3.5 m)
        float land = Land(d, x, z, e);
        return Mathf.Lerp(beach, Mathf.Max(beach, land), SmoothStep(0f, 35f, e));
    }

    static float Land(Def d, float x, float z, float e)
    {
        float S = d.land * 0.5f;
        switch (d.kind)
        {
            case Kind.JoseonLarge:
            {
                // 평야(6~11 m) + 북쪽(+z) 산줄기(배산) + 동남쪽 낮은 구릉. 가운데(궁터)는 10 m 로 평평하게.
                float plain = 8f + 2.5f * Fbm(x / 220f, z / 220f, d.seed + 10, 3);
                float ridgeMask = SmoothStep(0.05f * S, 0.50f * S, z + 0.12f * S * Fbm(x / 300f, 0f, d.seed + 11, 2));
                float mount = ridgeMask * (50f + 95f * Ridged(x / 200f, z / 200f, d.seed + 12, 4));
                float hill = 22f * Gauss(x, z, 0.62f * S, -0.35f * S, 0.16f * S) * (0.7f + 0.3f * Ridged(x / 90f, z / 90f, d.seed + 13, 2));
                float y = plain + mount + hill;
                float palace = SmoothStep(0.40f * S, 0.28f * S, Mathf.Sqrt(x * x + (z + 0.15f * S) * (z + 0.15f * S)));
                return Mathf.Lerp(y, 10f, palace);
            }
            case Kind.JoseonMid:
            {
                // 동·남쪽 해안 마을 평지(4~8 m) + 서쪽 구릉(최고 약 55 m).
                float basey = 6f + 3f * Fbm(x / 180f, z / 180f, d.seed + 10, 3);
                float hill = 65f * Gauss(x, z, -0.50f * S, 0.20f * S, 0.28f * S) * (0.75f + 0.35f * Ridged(x / 150f, z / 150f, d.seed + 12, 3));
                float y = basey + hill;
                float slopeZone = SmoothStep(8f, 30f, y) * (1f - SmoothStep(35f, 50f, y)); // 구릉 아랫자락은 계단밭
                return Mathf.Lerp(y, Terrace(y, 4f), slopeZone * 0.8f);
            }
            case Kind.SmallPeak:
            {
                // 뾰족한 산 하나(약 150 m, 북서쪽) + 남동쪽 중턱(약 20~45 m)에 계단식 산간 마을.
                float r = Mathf.Sqrt((x + 0.2f * S) * (x + 0.2f * S) + (z - 0.25f * S) * (z - 0.25f * S));
                float cone = 150f * Mathf.Pow(Mathf.Clamp01(1f - r / (0.95f * S)), 1.6f);
                float rough = 1f + 0.25f * (Ridged(x / 80f, z / 80f, d.seed + 12, 4) - 0.5f);
                float shoulder = 55f * Gauss(x, z, -0.55f * S, -0.15f * S, 0.22f * S); // 서쪽 어깨 능선(원뿔 모양 깨기)
                float y = 4f + Mathf.Max(cone, shoulder) * rough + 3f * Fbm(x / 120f, z / 120f, d.seed + 10, 3);
                float village = Gauss(x, z, 0.35f * S, -0.30f * S, 0.30f * S);
                float shelf = Mathf.Lerp(y, 18f + 0.45f * (y - 18f), village * 0.7f); // 중턱을 완만하게
                return Mathf.Lerp(shelf, Terrace(shelf, 5f), SmoothStep(0.25f, 0.6f, village));
            }
            case Kind.SmallTwin:
            {
                // 두 봉우리(서 약 120 m, 동 약 100 m) 사이 골짜기 마을(바닥 약 12~20 m), 남쪽 만으로 열림.
                float a = 115f * Mathf.Pow(Gauss(x, z, -0.48f * S, 0.30f * S, 0.30f * S), 0.8f);
                float b = 95f * Mathf.Pow(Gauss(x, z, 0.50f * S, 0.35f * S, 0.27f * S), 0.8f);
                float rough = 0.8f + 0.4f * Ridged(x / 90f, z / 90f, d.seed + 12, 4);
                float valley = 10f + 8f * SmoothStep(-0.6f * S, 0.5f * S, z);
                float y = Mathf.Max(valley, (a + b) * rough) + 2.5f * Fbm(x / 100f, z / 100f, d.seed + 10, 3);
                float vz = Gauss(x, z, 0f, -0.05f * S, 0.28f * S);
                return Mathf.Lerp(y, Terrace(y, 4f), SmoothStep(0.2f, 0.6f, vz));
            }
            case Kind.JapanCastle:
            {
                // 성하 마을 평지(4~7 m) + 가운데 성 언덕: 혼마루 42 m(반경 55) / 니노마루 26 m(반경 105) / 150 m 에서 마을 높이.
                float town = 5f + 1.8f * Fbm(x / 150f, z / 150f, d.seed + 10, 3);
                float r = Mathf.Sqrt(x * x + z * z);
                float y = town;
                y = Mathf.Lerp(y, 26f, SmoothStep(150f, 112f, r));
                y = Mathf.Lerp(y, 42f, SmoothStep(78f, 58f, r));
                return y;
            }
            case Kind.Wokou:
            {
                // 남서쪽 바위산(약 60 m, 거친 능선) + 북동쪽 만 뒤 소굴 터(약 6 m 평지, 단 2 m) — 배를 숨기는 바위섬.
                float hill = 58f * Gauss(x, z, -0.32f * S, -0.30f * S, 0.36f * S) * (0.7f + 0.5f * Ridged(x / 70f, z / 70f, d.seed + 12, 4));
                float spur = 30f * Gauss(x, z, 0.30f * S, -0.55f * S, 0.20f * S) * (0.8f + 0.4f * Ridged(x / 50f, z / 50f, d.seed + 13, 3)); // 남동 곶(만을 감쌈)
                float y = 4f + Mathf.Max(hill, spur) + 2.5f * Fbm(x / 60f, z / 60f, d.seed + 10, 3);
                float camp = Gauss(x, z, 0.28f * S, 0.16f * S, 0.26f * S);
                float flat = 6f + 0.6f * Fbm(x / 80f, z / 80f, d.seed + 14, 2);
                y = Mathf.Lerp(y, Mathf.Min(y, flat) + Mathf.Max(0f, y - flat) * 0.25f, SmoothStep(0.25f, 0.65f, camp));
                return Mathf.Lerp(y, Terrace(y, 2f), SmoothStep(0.35f, 0.7f, camp) * 0.6f);
            }
        }
        return 5f;
    }

    // ================= 텍스처 =================
    // 레이어 순서(Base 와 같음): 0 해저 산호자갈, 1 해저 바위, 2 모래, 3 풀+바위, 4 숲 바닥, 5 바위 땅, 6 바위 자갈, 7 절벽
    static void Paint(TerrainData td, float[] world, float tile, int seed)
    {
        int L = td.alphamapLayers;
        if (L < 8) { Debug.LogWarning("[Island] 레이어가 8개가 아님 — 칠하지 않음"); return; }
        var a = new float[AlphaRes, AlphaRes, L];
        float cell = tile / (HeightRes - 1);
        var w = new float[8];
        for (int j = 0; j < AlphaRes; j++)
            for (int i = 0; i < AlphaRes; i++)
            {
                float fx = (i + 0.5f) / AlphaRes * (HeightRes - 1), fz = (j + 0.5f) / AlphaRes * (HeightRes - 1);
                int hx = Mathf.Clamp(Mathf.RoundToInt(fx), 1, HeightRes - 2), hz = Mathf.Clamp(Mathf.RoundToInt(fz), 1, HeightRes - 2);
                float y = world[hz * HeightRes + hx];
                float dx = (world[hz * HeightRes + hx + 1] - world[hz * HeightRes + hx - 1]) / (2f * cell);
                float dz = (world[(hz + 1) * HeightRes + hx] - world[(hz - 1) * HeightRes + hx]) / (2f * cell);
                float slope = Mathf.Atan(Mathf.Sqrt(dx * dx + dz * dz)) * Mathf.Rad2Deg;
                float wx = fx * cell, wz = fz * cell;
                float n1 = Mathf.PerlinNoise(wx / 60f + seed, wz / 60f + seed);   // 같은 구역 2종 섞기
                float n2 = Mathf.PerlinNoise(wx / 25f + seed * 2, wz / 25f);

                for (int k = 0; k < 8; k++) w[k] = 0f;
                float sea = 1f - SmoothStep(-3.5f, -1.5f, y);
                float sand = (1f - sea) * (1f - SmoothStep(2.0f, 3.2f, y)) * (1f - SmoothStep(22f, 30f, slope));
                float rest = Mathf.Max(0f, 1f - sea - sand);
                float cliff = SmoothStep(38f, 48f, slope);
                float rock = SmoothStep(20f, 28f, slope) * (1f - cliff);
                float high = SmoothStep(90f, 130f, y) * (1f - cliff);        // 높은 산 꼭대기는 바위가 드러남
                rock = Mathf.Max(rock, high * 0.7f);
                float grass = Mathf.Max(0f, 1f - rock - cliff);
                w[0] = sea * n1; w[1] = sea * (1f - n1);
                w[2] = sand;
                w[3] = rest * grass * (0.35f + 0.65f * n2); w[4] = rest * grass * (0.65f - 0.65f * n2);
                w[5] = rest * rock * n1; w[6] = rest * rock * (1f - n1);
                w[7] = rest * cliff;
                float sum = 0f; for (int k = 0; k < 8; k++) sum += w[k];
                if (sum < 1e-4f) { w[3] = 1f; sum = 1f; }
                for (int k = 0; k < L; k++) a[j, i, k] = k < 8 ? w[k] / sum : 0f;
            }
        td.SetAlphamaps(0, 0, a);
    }
}
