// 경복궁 원본 단지(<단지>_Original.prefab) 최적화 도구 — MCP 에서 정적 메서드로 호출한다.
// 1) AddWindowPaper: 창살이 실제 막대 메시라 창호지 면이 없어 속이 비쳐 보이는 창에 창호지 면을 붙인다(단지마다 한 메시).
// 2) Cull*: 사람이 갈 수 있는 곳(바깥·마당·트인 복도)과 먼 시점에서 한 번도 안 보이는 부재를 원본 프리팹에서 지운다.
//    들어갈 수 있는 건물(근정전 본채, 경회루)은 지우지 않는다(사용자 지시 2026-09-27).
// 오래 걸리는 일은 CullStep(단지, 초) 를 여러 번 불러 나눠 한다(MCP 응답 30초 제한). 상태는 static 이라 도메인 리로드 때 사라진다.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PalaceOptimizeTool
{
    const string Root = PalaceBakeTool.BakedAssetRoot;
    const string HanjiMatPath = "Assets/02.Resources/01.Asset/Models/Gyeongbokgung/_Shared/Materials/MAT_Hanji_Window.mat";
    const string HanjiTexName = "T_GSJ_Koreanpaper_BC";

    static string OriginalPath(string complex) { return Root + "/" + complex + "/" + complex + "_Original.prefab"; }

    // ---------------- 1. 창호지 ----------------
    static readonly string[] WinKeys = { "window", "changho", "munsal", "gyeokja", "salchang", "door" };
    static readonly string[] WinExclude = { "frame", "changbang", "ornament", "handle", "lock", "iron", "hinge", "paper", "nodoors" };

    static bool IsWindowName(string meshName)
    {
        var l = meshName.ToLowerInvariant();
        if (!WinKeys.Any(k => l.Contains(k))) return false;
        return !WinExclude.Any(k => l.Contains(k));
    }

    public static Material HanjiMaterial()
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(HanjiMatPath);
        if (m != null) { ResetKeywords(m); return m; }
        m = new Material(Shader.Find("HDRP/Lit"));
        var g = AssetDatabase.FindAssets(HanjiTexName + " t:Texture2D");
        if (g.Length > 0) m.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(g[0])));
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Smoothness", 0.15f);
        m.SetFloat("_Metallic", 0f);
        // 양면(창 안팎 어느 쪽에서 봐도 보이게)
        m.SetFloat("_DoubleSidedEnable", 1f); m.SetFloat("_DoubleSidedNormalMode", 1f);
        m.SetFloat("_CullMode", 0f); m.SetFloat("_CullModeForward", 0f);
        m.doubleSidedGI = true;
        AssetDatabase.CreateAsset(m, HanjiMatPath);
        ResetKeywords(m);
        EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssets();
        return m;
    }

    static void ResetKeywords(Material m)
    {
        var t = Type.GetType("UnityEditor.Rendering.HighDefinition.HDShaderUtils, Unity.RenderPipelines.HighDefinition.Editor");
        // 오버로드가 여러 개라 인자 형식으로 고른다
        var mi = t == null ? null : t.GetMethod("ResetMaterialKeywords", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, new[] { typeof(Material) }, null);
        if (mi != null) mi.Invoke(null, new object[] { m });
    }

    // 메시를 가장 얇은 축 방향으로 격자 레이를 쏴서 빠져나가는 비율(0~1). 창살만 있고 창호지가 없으면 크다.
    static float SeeThrough(Mesh m, MeshCollider mc)
    {
        mc.sharedMesh = m; Physics.SyncTransforms();
        var b = m.bounds; var s = b.size;
        int ax = (s.x < s.y && s.x < s.z) ? 0 : ((s.y < s.z) ? 1 : 2);
        int a1 = (ax + 1) % 3, a2 = (ax + 2) % 3; int miss = 0, n = 0;
        for (int i = 0; i < 16; i++)
            for (int j = 0; j < 16; j++)
            {
                Vector3 p = b.center;
                p[a1] = b.min[a1] + s[a1] * ((i + 0.5f) / 16f);
                p[a2] = b.min[a2] + s[a2] * ((j + 0.5f) / 16f);
                p[ax] = b.min[ax] - 1f;
                var dir = Vector3.zero; dir[ax] = 1f;
                n++;
                if (!mc.Raycast(new Ray(mc.transform.TransformPoint(p), dir), out _, s[ax] + 2f)) miss++;
            }
        return (float)miss / n;
    }

    // 원본 프리팹에 "WindowPaper" 자식(창호지 면을 모두 합친 메시 1개 + MeshCollider)을 만든다. 다시 부르면 새로 만든다.
    public static string AddWindowPaper(string complex, float threshold = 0.2f)
    {
        var path = OriginalPath(complex);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return "no prefab " + path;
        var mat = HanjiMaterial();
        var tmp = new GameObject("__seethru"); tmp.transform.position = new Vector3(0, -5000, 0);
        var mc = tmp.AddComponent<MeshCollider>();
        var root = PrefabUtility.LoadPrefabContents(path);
        var sb = new StringBuilder();
        try
        {
            var old = root.transform.Find("WindowPaper"); if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var frac = new Dictionary<Mesh, float>();
            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>(); var idx = new List<int>();
            var used = new Dictionary<string, int>();
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
                var m = mf.sharedMesh; if (!IsWindowName(m.name)) continue;
                if (!frac.TryGetValue(m, out float f)) { f = SeeThrough(m, mc); frac[m] = f; }
                if (f <= threshold) continue;
                int c; used.TryGetValue(m.name, out c); used[m.name] = c + 1;
                // 가장 얇은 축의 가운데 면에 창 크기만 한 사각형(바깥 테두리 1 cm 안쪽)
                var b = m.bounds; var s = b.size;
                int ax = (s.x < s.y && s.x < s.z) ? 0 : ((s.y < s.z) ? 1 : 2);
                int a1 = (ax + 1) % 3, a2 = (ax + 2) % 3;
                var corner = new Vector3[4];
                for (int k = 0; k < 4; k++)
                {
                    Vector3 p = b.center;
                    p[a1] = (k == 0 || k == 3) ? b.min[a1] + 0.01f : b.max[a1] - 0.01f;
                    p[a2] = (k < 2) ? b.min[a2] + 0.01f : b.max[a2] - 0.01f;
                    corner[k] = root.transform.InverseTransformPoint(r.transform.TransformPoint(p));
                }
                var nl = Vector3.zero; nl[ax] = 1f;
                var n = root.transform.InverseTransformDirection(r.transform.TransformDirection(nl)).normalized;
                float w = (corner[1] - corner[0]).magnitude, h = (corner[3] - corner[0]).magnitude;
                int bi = verts.Count;
                verts.AddRange(corner);
                for (int k = 0; k < 4; k++) norms.Add(n);
                uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(w * 0.5f, 0)); uvs.Add(new Vector2(w * 0.5f, h * 0.5f)); uvs.Add(new Vector2(0, h * 0.5f));
                idx.AddRange(new[] { bi, bi + 1, bi + 2, bi, bi + 2, bi + 3 });
            }
            if (verts.Count == 0) { PrefabUtility.UnloadPrefabContents(root); root = null; return complex + ": no see-through windows"; }
            var mesh = new Mesh { name = complex + "_WindowPaper", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetUVs(0, uvs); mesh.SetTriangles(idx, 0);
            mesh.RecalculateBounds(); mesh.RecalculateTangents();
            var meshPath = Root + "/" + complex + "/" + complex + "_WindowPaper.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existing != null) { EditorUtility.CopySerialized(mesh, existing); mesh = existing; EditorUtility.SetDirty(existing); }
            else AssetDatabase.CreateAsset(mesh, meshPath);
            var go = new GameObject("WindowPaper");
            go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            PrefabUtility.SaveAsPrefabAsset(root, path);
            AssetDatabase.SaveAssets();
            sb.Append(complex + ": paper quads " + (verts.Count / 4) + " :: ");
            foreach (var kv in used) sb.Append(kv.Key + "=" + kv.Value + " ");
        }
        finally
        {
            if (root != null) PrefabUtility.UnloadPrefabContents(root);
            UnityEngine.Object.DestroyImmediate(tmp);
        }
        return sb.ToString();
    }

    // ---------------- 2. 안 보이는 부재 지우기 ----------------
    // 들어갈 수 있는 건물: 단지 → 남길 건물 코드(PalaceBakeTool.CodeOf). "*" 는 단지 전체.
    static readonly Dictionary<string, string[]> Enterable = new Dictionary<string, string[]>
    {
        { "Geunjeongjeon", new[] { "GJJ" } },
        { "Gyeonghoeru", new[] { "*" } },
    };
    static readonly string[] RoofKeys = { "roof", "tile", "giwa", "kiwa", "ridge", "chimi", "japsang", "yongmeoli", "mangwa", "sumaksae", "rafter", "buyeon", "gaepan" };
    static readonly Vector3 CullOffset = new Vector3(0, 5000, 0);

    class CullState
    {
        public string complex; public Scene scene; public GameObject inst; public GameObject groundGo;
        public List<MeshRenderer> rends = new List<MeshRenderer>();
        public Dictionary<int, int> colToRend = new Dictionary<int, int>();
        public List<int[]> paths = new List<int[]>();
        public bool[] keep, vis, roofish;
        public Bounds B; public float cell = 0.75f; public int nx, nz; public Vector3 gmin;
        public float[] ground; public bool[] pass, reach;
        public Queue<int> queue = new Queue<int>();
        public List<Vector3> eyes = new List<Vector3>(); public List<Vector3> far = new List<Vector3>();
        public int phase, cursor; public System.Random rnd = new System.Random(7);
        public bool oldBackfaces;
        public long rays;
    }
    static CullState S;

    static bool IsRoofish(string n) { var l = n.ToLowerInvariant(); return RoofKeys.Any(k => l.Contains(k)); }

    static int[] PathOf(Transform t, Transform root)
    {
        var l = new List<int>();
        while (t != null && t != root) { l.Add(t.GetSiblingIndex()); t = t.parent; }
        l.Reverse(); return l.ToArray();
    }
    static Transform ByPath(Transform root, int[] p)
    {
        var t = root;
        foreach (var i in p) { if (i >= t.childCount) return null; t = t.GetChild(i); }
        return t;
    }

    public static string CullBegin(string complex)
    {
        CullEnd();
        var pre = AssetDatabase.LoadAssetAtPath<GameObject>(OriginalPath(complex));
        if (pre == null) return "no prefab";
        var s = new CullState { complex = complex };
        s.scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        s.inst = UnityEngine.Object.Instantiate(pre);
        SceneManager.MoveGameObjectToScene(s.inst, s.scene);
        s.inst.transform.position = CullOffset;
        // 예전 창호지는 빼고 시작(PaperFromScene 이 다시 만든다). 항상 루트의 마지막 자식이라 다른 부재 경로는 안 바뀐다.
        var oldPaper = s.inst.transform.Find("WindowPaper");
        if (oldPaper != null) UnityEngine.Object.DestroyImmediate(oldPaper.gameObject);
        foreach (var c in s.inst.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        string[] keepCodes; Enterable.TryGetValue(complex, out keepCodes);
        foreach (var r in s.inst.GetComponentsInChildren<MeshRenderer>(true))
        {
            var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
            var mc = r.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; mc.enabled = true;
            s.colToRend[mc.GetInstanceID()] = s.rends.Count;
            s.rends.Add(r); s.paths.Add(PathOf(r.transform, s.inst.transform));
        }
        int n = s.rends.Count;
        s.keep = new bool[n]; s.vis = new bool[n]; s.roofish = new bool[n];
        bool first = true;
        for (int i = 0; i < n; i++)
        {
            var r = s.rends[i]; var mn = r.GetComponent<MeshFilter>().sharedMesh.name;
            s.roofish[i] = IsRoofish(mn);
            if (keepCodes != null && (keepCodes.Contains("*") || keepCodes.Contains(PalaceBakeTool.CodeOf(mn)))) s.keep[i] = true;
            if (r.gameObject.name == "WindowPaper") s.keep[i] = true;
            if (mn.IndexOf("Water", StringComparison.OrdinalIgnoreCase) >= 0) s.keep[i] = true;
            if (r.bounds.size.magnitude > 400f) continue;
            if (first) { s.B = r.bounds; first = false; } else s.B.Encapsulate(r.bounds);
        }
        // 빈 마당에도 설 수 있게 가장 낮은 곳에 바닥판
        s.groundGo = new GameObject("__ground"); SceneManager.MoveGameObjectToScene(s.groundGo, s.scene);
        var bc = s.groundGo.AddComponent<BoxCollider>();
        s.groundGo.transform.position = new Vector3(s.B.center.x, s.B.min.y - 0.5f, s.B.center.z);
        bc.size = new Vector3(s.B.size.x + 200f, 1f, s.B.size.z + 200f);
        s.oldBackfaces = Physics.queriesHitBackfaces;
        Physics.queriesHitBackfaces = true;   // 창호지·벽이 한 면 메시라 뒤쪽에서 오는 레이도 막게
        Physics.SyncTransforms();
        s.gmin = new Vector3(s.B.min.x - 10f, 0, s.B.min.z - 10f);
        s.nx = Mathf.CeilToInt((s.B.size.x + 20f) / s.cell); s.nz = Mathf.CeilToInt((s.B.size.z + 20f) / s.cell);
        s.ground = new float[s.nx * s.nz]; s.pass = new bool[s.nx * s.nz]; s.reach = new bool[s.nx * s.nz];
        s.phase = 1; s.cursor = 0;
        S = s;
        var paper = PaperFromScene();
        return $"begin {complex} renderers={n} keep={s.keep.Count(x => x)} grid={s.nx}x{s.nz} | {paper}";
    }

    // 창호지: 창·창틀·문 이름의 얇은 부재마다 그 면을 가로지르는 레이를 쏴서(주변 부재 전체를 막는 것으로 침)
    // 25 % 넘게 빠져나가면 그 부재 두께 가운데에 창호지 면을 붙인다. 창살이 창틀 메시에 붙어 있는 경우(흥례문 Windowframe_A)도 잡힌다.
    // 작은 것부터 처리하고 붙인 창호지는 곧바로 막는 것으로 넣어, 창과 창틀에 겹쳐 붙지 않게 한다.
    // 문짝(창살 문)은 막고, 문틀(사람이 지나가는 구멍)은 막지 않는다. 궐문 문짝은 원본에 없다(_Doors 프리팹).
    static readonly string[] PaperKeys = { "window", "frame", "changho", "munsal", "gyeokja", "salchang", "door" };
    static readonly string[] PaperExclude = { "doorframe", "door_frame", "changbang", "ornament", "handle", "lock", "iron", "hinge", "paper", "nodoors", "wallframe", "rail", "fence", "balustrade", "nangan", "gyeran" };
    const float PaperCell = 0.06f;

    static string PaperFromScene()
    {
        var s = S; var root = s.inst.transform;
        var cands = new List<MeshRenderer>();
        foreach (var r in s.rends)
        {
            var m = r.GetComponent<MeshFilter>().sharedMesh; var l = m.name.ToLowerInvariant();
            if (!PaperKeys.Any(k => l.Contains(k)) || PaperExclude.Any(k => l.Contains(k))) continue;
            var sz = Vector3.Scale(m.bounds.size, r.transform.lossyScale); var a = new[] { Mathf.Abs(sz.x), Mathf.Abs(sz.y), Mathf.Abs(sz.z) }; Array.Sort(a);
            if (a[0] > 0.35f || a[1] < 0.25f || a[2] > 6f) continue;
            cands.Add(r);
        }
        cands.Sort((x, y) => Area(x).CompareTo(Area(y)));
        var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>(); var idx = new List<int>();
        var used = new Dictionary<string, int>();
        foreach (var r in cands)
        {
            var m = r.GetComponent<MeshFilter>().sharedMesh; var b = m.bounds; var sz = b.size; var tr = r.transform;
            var ws = Vector3.Scale(sz, tr.lossyScale);
            int ax = (Mathf.Abs(ws.x) < Mathf.Abs(ws.y) && Mathf.Abs(ws.x) < Mathf.Abs(ws.z)) ? 0 : ((Mathf.Abs(ws.y) < Mathf.Abs(ws.z)) ? 1 : 2);
            int a1 = (ax + 1) % 3, a2 = (ax + 2) % 3;
            float pad = 0.25f / Mathf.Max(1e-4f, Mathf.Abs(tr.lossyScale[ax]));
            // 면을 6 cm 칸으로 나눠 칸마다 두께 방향 레이 → 빠지는 칸의 덩어리(창살 막대로 끊긴 것은 2칸 닫기로 이음)마다 창호지 한 장
            float s1 = Mathf.Abs(ws[a1]), s2 = Mathf.Abs(ws[a2]);
            int g1 = Mathf.Max(2, Mathf.CeilToInt(s1 / PaperCell)), g2 = Mathf.Max(2, Mathf.CeilToInt(s2 / PaperCell));
            var passM = new bool[g1, g2];
            for (int i = 0; i < g1; i++)
                for (int j = 0; j < g2; j++)
                {
                    Vector3 p = b.center;
                    p[a1] = b.min[a1] + sz[a1] * ((i + 0.5f) / g1);
                    p[a2] = b.min[a2] + sz[a2] * ((j + 0.5f) / g2);
                    var p0 = p; p0[ax] = b.min[ax] - pad; var p1 = p; p1[ax] = b.max[ax] + pad;
                    passM[i, j] = !Physics.Linecast(tr.TransformPoint(p0), tr.TransformPoint(p1));
                }
            var closed = Close(passM, g1, g2, 2);
            var label = new int[g1, g2]; int nlab = 0;
            var added = 0;
            for (int i0 = 0; i0 < g1; i0++)
                for (int j0 = 0; j0 < g2; j0++)
                {
                    if (!closed[i0, j0] || label[i0, j0] != 0) continue;
                    nlab++; int mi = i0, ma = i0, mj = j0, mx = j0, cells = 0, passCells = 0;
                    var st = new Stack<Vector2Int>(); st.Push(new Vector2Int(i0, j0)); label[i0, j0] = nlab;
                    while (st.Count > 0)
                    {
                        var q = st.Pop(); cells++; if (passM[q.x, q.y]) passCells++;
                        mi = Mathf.Min(mi, q.x); ma = Mathf.Max(ma, q.x); mj = Mathf.Min(mj, q.y); mx = Mathf.Max(mx, q.y);
                        for (int d = 0; d < 4; d++)
                        {
                            int ii = q.x + (d == 0 ? 1 : d == 1 ? -1 : 0), jj = q.y + (d == 2 ? 1 : d == 3 ? -1 : 0);
                            if (ii < 0 || jj < 0 || ii >= g1 || jj >= g2 || !closed[ii, jj] || label[ii, jj] != 0) continue;
                            label[ii, jj] = nlab; st.Push(new Vector2Int(ii, jj));
                        }
                    }
                    float wA = (ma - mi + 1) * s1 / g1, hA = (mx - mj + 1) * s2 / g2;
                    if (wA < 0.15f || hA < 0.15f || passCells < 6) continue;   // 틈·작은 구멍은 무시
                    // 덩어리 바깥으로 한 칸씩 넓힌 사각형(창살 테두리까지 덮게)
                    float u0 = Mathf.Max(0, mi - 1) / (float)g1, u1 = Mathf.Min(g1, ma + 2) / (float)g1;
                    float v0 = Mathf.Max(0, mj - 1) / (float)g2, v1 = Mathf.Min(g2, mx + 2) / (float)g2;
                    AddQuad(r, b, ax, a1, a2, u0, u1, v0, v1, root, verts, norms, uvs, idx);
                    added++;
                }
            if (added == 0) continue;
            int c; used.TryGetValue(m.name, out c); used[m.name] = c + added;
        }
        // 원본 프리팹에 저장
        var path = OriginalPath(s.complex);
        var proot = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var old = proot.transform.Find("WindowPaper"); if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            if (verts.Count > 0)
            {
                var mesh = new Mesh { name = s.complex + "_WindowPaper", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetUVs(0, uvs); mesh.SetTriangles(idx, 0);
                mesh.RecalculateBounds(); mesh.RecalculateTangents();
                var meshPath = Root + "/" + s.complex + "/" + s.complex + "_WindowPaper.asset";
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (existing != null) { EditorUtility.CopySerialized(mesh, existing); mesh = existing; EditorUtility.SetDirty(existing); }
                else AssetDatabase.CreateAsset(mesh, meshPath);
                var go = new GameObject("WindowPaper");
                go.transform.SetParent(proot.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = HanjiMaterial();
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
            }
            PrefabUtility.SaveAsPrefabAsset(proot, path);
            AssetDatabase.SaveAssets();
        }
        finally { PrefabUtility.UnloadPrefabContents(proot); }
        var sb = new StringBuilder("paper " + (verts.Count / 4) + " of candidates " + cands.Count + " :: ");
        foreach (var kv in used) sb.Append(kv.Key + "=" + kv.Value + " ");
        return sb.ToString();
    }

    // 2D 닫기(팽창 후 침식) — 창살 막대에 끊긴 칸을 하나로 잇는다
    static bool[,] Close(bool[,] m, int g1, int g2, int rad)
    {
        var dil = new bool[g1, g2];
        for (int i = 0; i < g1; i++) for (int j = 0; j < g2; j++)
            {
                if (!m[i, j]) continue;
                for (int a = -rad; a <= rad; a++) for (int b = -rad; b <= rad; b++)
                    {
                        int ii = i + a, jj = j + b; if (ii < 0 || jj < 0 || ii >= g1 || jj >= g2) continue; dil[ii, jj] = true;
                    }
            }
        var ero = new bool[g1, g2];
        for (int i = 0; i < g1; i++) for (int j = 0; j < g2; j++)
            {
                bool all = dil[i, j];
                for (int a = -rad; a <= rad && all; a++) for (int b = -rad; b <= rad && all; b++)
                    {
                        int ii = i + a, jj = j + b; if (ii < 0 || jj < 0 || ii >= g1 || jj >= g2) continue; if (!dil[ii, jj]) all = false;
                    }
                ero[i, j] = all || m[i, j];
            }
        return ero;
    }

    // 부재 r 의 메시 공간 경계상자에서 (u0..u1, v0..v1) 부분의 두께 가운데 면에 사각형을 추가하고, 막는 상자도 함께 넣는다
    static void AddQuad(MeshRenderer r, Bounds b, int ax, int a1, int a2, float u0, float u1, float v0, float v1, Transform root,
        List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> idx)
    {
        var tr = r.transform; var sz = b.size;
        var corner = new Vector3[4]; var local = new Vector3[4];
        for (int k = 0; k < 4; k++)
        {
            Vector3 p = b.center;
            p[a1] = b.min[a1] + sz[a1] * ((k == 0 || k == 3) ? u0 : u1);
            p[a2] = b.min[a2] + sz[a2] * ((k < 2) ? v0 : v1);
            local[k] = p;
            corner[k] = root.InverseTransformPoint(tr.TransformPoint(p));
        }
        var nl = Vector3.zero; nl[ax] = 1f;
        var nn = root.InverseTransformDirection(tr.TransformDirection(nl)).normalized;
        float w = (corner[1] - corner[0]).magnitude, h = (corner[3] - corner[0]).magnitude;
        int bi = verts.Count;
        verts.AddRange(corner);
        for (int k = 0; k < 4; k++) norms.Add(nn);
        uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(w * 0.5f, 0)); uvs.Add(new Vector2(w * 0.5f, h * 0.5f)); uvs.Add(new Vector2(0, h * 0.5f));
        idx.AddRange(new[] { bi, bi + 1, bi + 2, bi, bi + 2, bi + 3 });
        // 곧바로 막는 것으로(뒤에 오는 큰 창틀이 같은 구멍에 또 붙지 않게, 가려짐 계산에도 쓰임)
        var q = new GameObject("__paperQuad"); q.transform.SetParent(tr, false);
        q.transform.localPosition = (local[0] + local[2]) * 0.5f;
        var bc = q.AddComponent<BoxCollider>(); var bs = local[2] - local[0]; bs = new Vector3(Mathf.Abs(bs.x), Mathf.Abs(bs.y), Mathf.Abs(bs.z));
        bs[ax] = 0.02f / Mathf.Max(1e-4f, Mathf.Abs(tr.lossyScale[ax])); bc.size = bs;
        Physics.SyncTransforms();
    }

    static float Area(MeshRenderer r)
    {
        var sz = Vector3.Scale(r.GetComponent<MeshFilter>().sharedMesh.bounds.size, r.transform.lossyScale);
        var a = new[] { Mathf.Abs(sz.x), Mathf.Abs(sz.y), Mathf.Abs(sz.z) }; Array.Sort(a);
        return a[1] * a[2];
    }

    static Vector3 CellPos(int c) { int i = c % S.nx, k = c / S.nx; return new Vector3(S.gmin.x + (i + 0.5f) * S.cell, S.ground[c], S.gmin.z + (k + 0.5f) * S.cell); }

    public static string CullStep(string complex, float budgetSec = 20f)
    {
        if (S == null || S.complex != complex) return "not begun";
        var s = S; var t0 = EditorApplication.timeSinceStartup;
        Func<bool> over = () => EditorApplication.timeSinceStartup - t0 > budgetSec;
        var up = Vector3.up;
        while (!over())
        {
            if (s.phase == 1)   // 칸마다 설 수 있는 바닥 높이
            {
                int total = s.nx * s.nz;
                for (; s.cursor < total && !over(); s.cursor++)
                {
                    int c = s.cursor; int i = c % s.nx, k = c / s.nx;
                    var top = new Vector3(s.gmin.x + (i + 0.5f) * s.cell, s.B.max.y + 5f, s.gmin.z + (k + 0.5f) * s.cell);
                    var hits = Physics.RaycastAll(top, Vector3.down, s.B.size.y + 20f);
                    Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                    foreach (var h in hits)
                    {
                        if (h.normal.y < 0.7f && h.normal.y > -0.7f) continue;
                        int ri; bool isRoof = s.colToRend.TryGetValue(h.collider.GetInstanceID(), out ri) && s.roofish[ri];
                        if (isRoof) continue;
                        var p = h.point;
                        if (Physics.CheckCapsule(p + up * 0.4f, p + up * 1.7f, 0.3f)) continue;
                        s.ground[c] = p.y; s.pass[c] = true; break;
                    }
                }
                if (s.cursor >= total)
                {
                    s.phase = 2; s.cursor = 0;
                    // 시작 칸: 바깥 가장자리 + 하늘이 트인 칸(마당). 문짝을 뺐어도 문턱·기단 계단 때문에 마당까지 못 이어지는 일이 있어
                    // 트인 마당은 모두 갈 수 있는 곳으로 본다. 지붕 밑(복도·방)은 여기서 이어져야만 갈 수 있다.
                    for (int i = 0; i < s.nx; i++) for (int k = 0; k < s.nz; k++)
                        {
                            int c = k * s.nx + i; if (!s.pass[c] || s.reach[c]) continue;
                            bool border = i == 0 || k == 0 || i == s.nx - 1 || k == s.nz - 1;
                            bool sky = !Physics.Raycast(CellPos(c) + up * 1.6f, up, 200f);
                            if (border || sky) { s.reach[c] = true; s.queue.Enqueue(c); }
                        }
                }
            }
            else if (s.phase == 2)   // 바깥 가장자리에서 걸어서 갈 수 있는 칸(범람 채우기)
            {
                while (s.queue.Count > 0 && !over())
                {
                    int c = s.queue.Dequeue(); int i = c % s.nx, k = c / s.nx; var a = CellPos(c);
                    for (int d = 0; d < 4; d++)
                    {
                        int ii = i + (d == 0 ? 1 : d == 1 ? -1 : 0), kk = k + (d == 2 ? 1 : d == 3 ? -1 : 0);
                        if (ii < 0 || kk < 0 || ii >= s.nx || kk >= s.nz) continue;
                        int cc = kk * s.nx + ii; if (!s.pass[cc] || s.reach[cc]) continue;
                        var b = CellPos(cc);
                        // 기단(행각 복도 바닥)이 마당보다 높아 계단이 없는 곳도 있어 1.2 m 까지는 이어진 것으로 본다(방은 벽·문으로 막혀 여기와 무관)
                        if (Mathf.Abs(b.y - a.y) > 1.2f) continue;
                        if (Physics.Linecast(a + up * 1.0f, b + up * 1.0f) || Physics.Linecast(a + up * 1.6f, b + up * 1.6f)) continue;
                        s.reach[cc] = true; s.queue.Enqueue(cc);
                    }
                }
                if (s.queue.Count == 0)
                {
                    for (int c = 0; c < s.reach.Length; c++) if (s.reach[c]) s.eyes.Add(CellPos(c) + up * 1.6f);
                    float rad = Mathf.Max(s.B.extents.x, s.B.extents.z) * 1.5f + 40f;
                    foreach (var hgt in new[] { 3f, 20f, 60f, 150f })
                        for (int a = 0; a < 16; a++)
                        {
                            float ang = a * Mathf.PI * 2f / 16f;
                            s.far.Add(new Vector3(s.B.center.x + Mathf.Cos(ang) * rad, s.B.min.y + hgt, s.B.center.z + Mathf.Sin(ang) * rad));
                        }
                    s.phase = 3; s.cursor = 0;
                }
            }
            else if (s.phase == 3)   // 갈 수 있는 곳 눈높이에서 사방으로
            {
                for (; s.cursor < s.eyes.Count && !over(); s.cursor++)
                {
                    var e = s.eyes[s.cursor];
                    for (int j = 0; j < 32; j++)
                    {
                        var dir = new Vector3((float)s.rnd.NextDouble() * 2 - 1, (float)s.rnd.NextDouble() * 1.4f - 0.4f, (float)s.rnd.NextDouble() * 2 - 1).normalized;
                        Mark(e, dir, 400f);
                    }
                }
                if (s.cursor >= s.eyes.Count) { s.phase = 4; s.cursor = 0; }
            }
            else if (s.phase == 4)   // 먼 시점(바다·언덕)에서 단지 쪽으로
            {
                for (; s.cursor < s.far.Count && !over(); s.cursor++)
                {
                    var f = s.far[s.cursor];
                    for (int j = 0; j < 4000; j++)
                    {
                        var target = new Vector3(Rand(s.B.min.x, s.B.max.x), Rand(s.B.min.y, s.B.max.y), Rand(s.B.min.z, s.B.max.z));
                        Mark(f, (target - f).normalized, 2000f);
                    }
                }
                if (s.cursor >= s.far.Count) { s.phase = 5; s.cursor = 0; }
            }
            else if (s.phase == 5)   // 한 번도 안 맞은 부재: 표면 점 → 시점 방향 역검사
            {
                // 처마 밑 공포처럼 틈으로만 보이는 작은 부재를 놓치지 않게, 부재 가까이(35 m 안)의 갈 수 있는 칸을 주로 쓴다
                for (; s.cursor < s.rends.Count && !over(); s.cursor++)
                {
                    int ri = s.cursor; if (s.vis[ri] || s.keep[ri]) continue;
                    // 바로 아래·옆(12 m 안) 칸은 빠짐없이, 그 밖(35 m 안)은 일부만
                    var targets = NearEyes(s.rends[ri].bounds.center, 12f, 96);
                    targets.AddRange(NearEyes(s.rends[ri].bounds.center, 35f, 72));
                    for (int j = 0; j < 8 && s.eyes.Count > 0; j++) targets.Add(s.eyes[s.rnd.Next(s.eyes.Count)]);
                    targets.AddRange(s.far);
                    if (ReverseVisible(ri, targets) || EscapeVisible(ri)) s.vis[ri] = true;
                }
                if (s.cursor >= s.rends.Count) { s.phase = 6; }
            }
            else break;
        }
        return Status();
    }

    static float Rand(float a, float b) { return a + (float)S.rnd.NextDouble() * (b - a); }

    static List<Vector3> NearEyes(Vector3 c, float radius, int count)
    {
        var s = S; var l = new List<Vector3>();
        int ci = Mathf.FloorToInt((c.x - s.gmin.x) / s.cell), ck = Mathf.FloorToInt((c.z - s.gmin.z) / s.cell);
        int rr = Mathf.CeilToInt(radius / s.cell);
        for (int tries = 0; tries < count * 6 && l.Count < count; tries++)
        {
            int i = ci + s.rnd.Next(-rr, rr + 1), k = ck + s.rnd.Next(-rr, rr + 1);
            if (i < 0 || k < 0 || i >= s.nx || k >= s.nz) continue;
            int cc = k * s.nx + i; if (!s.reach[cc]) continue;
            l.Add(CellPos(cc) + Vector3.up * 1.6f);
        }
        return l;
    }

    static void Mark(Vector3 o, Vector3 dir, float dist)
    {
        S.rays++;
        RaycastHit h;
        if (Physics.Raycast(o, dir, out h, dist))
        {
            int ri; if (S.colToRend.TryGetValue(h.collider.GetInstanceID(), out ri)) S.vis[ri] = true;
        }
    }

    static bool ReverseVisible(int ri, List<Vector3> targets)
    {
        var r = S.rends[ri]; var m = r.GetComponent<MeshFilter>().sharedMesh;
        var tris = m.triangles; var vs = m.vertices;
        if (tris.Length == 0) return false;
        var tr = r.transform; var myCol = r.GetComponent<MeshCollider>();
        for (int k = 0; k < 40; k++)
        {
            int t = S.rnd.Next(tris.Length / 3) * 3;
            float u = (float)S.rnd.NextDouble(), v = (float)S.rnd.NextDouble(); if (u + v > 1) { u = 1 - u; v = 1 - v; }
            var a = tr.TransformPoint(vs[tris[t]]); var b = tr.TransformPoint(vs[tris[t + 1]]); var c = tr.TransformPoint(vs[tris[t + 2]]);
            var p = a + (b - a) * u + (c - a) * v;
            var n = Vector3.Cross(b - a, c - a).normalized;
            for (int j = 0; j < targets.Count; j++)
            {
                var tg = targets[j];
                var side = Vector3.Dot(tg - p, n) >= 0 ? n : -n;
                var start = p + side * 0.03f;
                S.rays++;
                RaycastHit h;
                if (!Physics.Linecast(start, tg, out h)) return true;
                if (h.collider == myCol) continue;
            }
        }
        return false;
    }

    // 부재 표면에서 사방으로 레이를 쏴서, 막히기 전까지 지나는 길에 '갈 수 있는 칸의 눈높이 공간'(바닥 +0.3~2.2 m)을 지나거나
    // 단지 밖(먼 바다·언덕 쪽)으로 빠져나가면 보이는 것으로 친다. 열린 문 안쪽 깊은 곳처럼 좁은 틈으로만 보이는 부재를 잡는다.
    static bool EscapeVisible(int ri)
    {
        var s = S; var r = s.rends[ri]; var m = r.GetComponent<MeshFilter>().sharedMesh;
        var tris = m.triangles; var vs = m.vertices; if (tris.Length == 0) return false;
        var tr = r.transform;
        for (int k = 0; k < 16; k++)
        {
            int t = s.rnd.Next(tris.Length / 3) * 3;
            float u = (float)s.rnd.NextDouble(), v = (float)s.rnd.NextDouble(); if (u + v > 1) { u = 1 - u; v = 1 - v; }
            var a = tr.TransformPoint(vs[tris[t]]); var b = tr.TransformPoint(vs[tris[t + 1]]); var c = tr.TransformPoint(vs[tris[t + 2]]);
            var p = a + (b - a) * u + (c - a) * v;
            var n = Vector3.Cross(b - a, c - a).normalized;
            for (int j = 0; j < 64; j++)
            {
                var d = new Vector3((float)s.rnd.NextDouble() * 2 - 1, (float)s.rnd.NextDouble() * 2 - 1, (float)s.rnd.NextDouble() * 2 - 1);
                if (d.sqrMagnitude < 1e-4f) continue; d.Normalize();
                var side = Vector3.Dot(d, n) >= 0 ? n : -n;
                var o = p + side * 0.03f;
                RaycastHit h; float dist = 80f; bool hit = Physics.Raycast(o, d, out h, dist);
                if (hit) dist = h.distance;
                else
                {
                    var end = o + d * dist;
                    bool outside = end.x < s.B.min.x || end.x > s.B.max.x || end.z < s.B.min.z || end.z > s.B.max.z;
                    if (outside && end.y < s.B.max.y + 60f) return true;
                }
                for (float q = 0.4f; q < dist; q += 0.6f)
                {
                    var pos = o + d * q;
                    int ci = Mathf.FloorToInt((pos.x - s.gmin.x) / s.cell), ck = Mathf.FloorToInt((pos.z - s.gmin.z) / s.cell);
                    if (ci < 0 || ck < 0 || ci >= s.nx || ck >= s.nz) continue;
                    int cc = ck * s.nx + ci; if (!s.reach[cc]) continue;
                    float hy = pos.y - s.ground[cc];
                    if (hy > 0.3f && hy < 2.2f) return true;
                }
            }
        }
        return false;
    }

    static string Status()
    {
        var s = S;
        int visN = s.vis.Count(x => x), keepN = s.keep.Count(x => x);
        int hidden = 0; long hiddenTris = 0;
        for (int i = 0; i < s.rends.Count; i++) if (!s.vis[i] && !s.keep[i]) { hidden++; hiddenTris += Tris(s.rends[i].GetComponent<MeshFilter>().sharedMesh); }
        return $"{s.complex} phase={s.phase} cursor={s.cursor} passCells={s.pass.Count(x => x)} reachCells={s.reach.Count(x => x)} eyes={s.eyes.Count} rays={s.rays} renderers={s.rends.Count} visible={visN} keep={keepN} hidden={hidden} hiddenTris={hiddenTris}";
    }

    static long Tris(Mesh m) { long t = 0; for (int i = 0; i < m.subMeshCount; i++) t += m.GetIndexCount(i) / 3; return t; }

    // 결과를 원본 프리팹에 적용: 안 보이는 부재를 지운다. 지운 목록은 <단지>_Culled.txt 에 남긴다.
    public static string CullApply(string complex)
    {
        if (S == null || S.complex != complex || S.phase != 6) return "not finished";
        var path = OriginalPath(complex);
        var root = PrefabUtility.LoadPrefabContents(path);
        var log = new StringBuilder();
        int removedGo = 0, removedComp = 0; long tris = 0;
        try
        {
            var targets = new List<Transform>();
            for (int i = 0; i < S.rends.Count; i++)
            {
                if (S.vis[i] || S.keep[i]) continue;
                var t = ByPath(root.transform, S.paths[i]);
                if (t == null || t.name != S.rends[i].name) { log.AppendLine("PATH MISMATCH " + S.rends[i].name); continue; }
                targets.Add(t);
                log.AppendLine(string.Join("/", S.paths[i]) + "\t" + t.name + "\t" + t.GetComponent<MeshFilter>().sharedMesh.name);
                tris += Tris(t.GetComponent<MeshFilter>().sharedMesh);
            }
            var set = new HashSet<Transform>(targets);
            foreach (var t in targets)
            {
                if (t == null) continue;
                // 자식에 남길 렌더러가 있으면 컴포넌트만, 없으면 오브젝트째
                bool childKept = t.GetComponentsInChildren<MeshRenderer>(true).Any(r => r.transform != t && !set.Contains(r.transform));
                if (childKept)
                {
                    foreach (var c in t.GetComponents<Collider>()) UnityEngine.Object.DestroyImmediate(c);
                    UnityEngine.Object.DestroyImmediate(t.GetComponent<MeshRenderer>());
                    UnityEngine.Object.DestroyImmediate(t.GetComponent<MeshFilter>());
                    removedComp++;
                }
                else { UnityEngine.Object.DestroyImmediate(t.gameObject); removedGo++; }
            }
            // 렌더러가 하나도 안 남은 빈 껍데기 정리
            foreach (Transform top in root.transform.Cast<Transform>().ToList())
                if (top.GetComponentInChildren<MeshRenderer>(true) == null) UnityEngine.Object.DestroyImmediate(top.gameObject);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        File.WriteAllText(Path.Combine(PalaceBakeTool.BakeRoot, complex + "_Culled.txt"), log.ToString());
        return $"applied {complex} removedObjects={removedGo} removedComponents={removedComp} tris={tris}";
    }

    public static string CullEnd()
    {
        if (S == null) return "none";
        Physics.queriesHitBackfaces = S.oldBackfaces;
        if (S.scene.IsValid() && S.scene.isLoaded) EditorSceneManager.CloseScene(S.scene, true);
        S = null;
        return "ended";
    }

    // ---------------- 3. 먼 거리 지붕(매끈한 껍데기) ----------------
    // 굽기 결과(<BakeRoot>/<단지>/out_roofenv/<덩어리>_roofenv.fbx + _BC/_N.png)를 가져와 덩어리 프리팹(<덩어리>_Baked)에 넣는다.
    // farOnly(원본 단지: 구운 것은 멀리서만 보임) = 기와 메시를 껍데기로 바꿔 넣는다.
    // 아니면(구운 것만 쓰는 단지) = 가까이는 지금 기와 메시, 멀리는 껍데기(덩어리마다 LODGroup, 화면 높이 lodSwitch 에서 바뀜).
    public static readonly string[] FullOriginalComplexes = { "GwanghwamunGate", "Gwanghwamun", "Heungnyemun", "Geunjeongjeon", "Gyeonghoeru", "Gangnyeongjeon", "Sajeongjeon" };

    public static string ImportRoofEnvStep(string complex, float budgetSec = 20f, float lodSwitch = 0.5f)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        bool farOnly = FullOriginalComplexes.Contains(complex);
        var src = Path.Combine(PalaceBakeTool.BakeRoot, complex, "out_roofenv");
        if (!Directory.Exists(src)) return "no out_roofenv";
        var dstRel = Root + "/" + complex;
        var dst = Path.Combine(Directory.GetCurrentDirectory(), dstRel.Replace('/', '\\'));
        bool copied = false;
        foreach (var f in Directory.GetFiles(src))
        {
            var fn = Path.GetFileName(f); if (!(fn.EndsWith(".png") || fn.EndsWith(".fbx"))) continue;
            var d = Path.Combine(dst, fn);
            if (File.Exists(d) && new FileInfo(d).Length == new FileInfo(f).Length) continue;
            File.Copy(f, d, true); copied = true;
        }
        if (copied) AssetDatabase.Refresh();
        var names = Directory.GetFiles(src, "*_roofenv.fbx").Select(p => Path.GetFileNameWithoutExtension(p).Replace("_roofenv", "")).ToList();
        int done = 0, left = 0, skipped = 0;
        foreach (var name in names)
        {
            if (sw.Elapsed.TotalSeconds > budgetSec) { left++; continue; }
            string prefabPath = dstRel + "/" + name + "_Baked.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null) { skipped++; continue; }
            // 굽기에서 레이가 기와에 많이 못 닿은 덩어리(info.bad, 섬 안 15 % 넘게 빈 칸)는 껍데기를 쓰지 않는다
            var infoP = Path.Combine(src, name + "_roofenv_info.json");
            // 판정: 껍데기→원본 적중률 cov_env < 0.85 또는 원본→껍데기 적중률 cov_high < 0.8 이면 버림
            // (0.9 로 하니 틈 조금 있는 멀쩡한 덩어리까지 버려졌음. 값이 없으면 info.bad 를 따름)
            string infoTxt = File.Exists(infoP) ? File.ReadAllText(infoP) : "";
            var mE = System.Text.RegularExpressions.Regex.Match(infoTxt, "\"cov_env\": ([0-9.]+)");
            var mH = System.Text.RegularExpressions.Regex.Match(infoTxt, "\"cov_high\": ([0-9.]+)");
            bool badEnv = (mE.Success && mH.Success)
                ? (float.Parse(mE.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) < 0.85f
                   || float.Parse(mH.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) < 0.8f)
                : infoTxt.Contains("\"bad\": true");
            if (badEnv)
            {
                // 예전에 껍데기를 넣어 뒀으면 FBX 지붕으로 되돌린다
                var rt = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    bool ch = false;
                    var rr = rt.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.name == name + "_roof");
                    var orig = AssetDatabase.LoadAllAssetsAtPath(dstRel + "/" + name + ".fbx").OfType<Mesh>().FirstOrDefault(x => x.name == name + "_roof");
                    var om = AssetDatabase.LoadAssetAtPath<Material>(dstRel + "/MAT_" + name + "_roof.mat");
                    if (rr != null && orig != null && om != null && rr.GetComponent<MeshFilter>().sharedMesh != orig)
                    { rr.GetComponent<MeshFilter>().sharedMesh = orig; rr.sharedMaterial = om; ch = true; }
                    var lg0 = rt.GetComponent<LODGroup>(); if (lg0 != null) { UnityEngine.Object.DestroyImmediate(lg0); ch = true; }
                    var env0 = rt.transform.Find(name + "_roofenv"); if (env0 != null) { UnityEngine.Object.DestroyImmediate(env0.gameObject); ch = true; }
                    if (ch) PrefabUtility.SaveAsPrefabAsset(rt, prefabPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(rt); }
                skipped++; continue;
            }
            string bcp = dstRel + "/" + name + "_roofenv_BC.png", np = dstRel + "/" + name + "_roofenv_N.png", fbx = dstRel + "/" + name + "_roofenv.fbx";
            var mesh = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Mesh>().FirstOrDefault();
            if (mesh == null) { skipped++; continue; }
            string mp = dstRel + "/MAT_" + name + "_roofenv.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(mp); bool isNew = m == null; if (isNew) m = new Material(Shader.Find("HDRP/Lit"));
            m.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(bcp)); m.SetTexture("_NormalMap", AssetDatabase.LoadAssetAtPath<Texture2D>(np));
            m.SetFloat("_Smoothness", 0.25f);
            // 리메시 껍데기의 법선이 군데군데 뒤집힐 수 있어 양면으로 그린다
            m.SetFloat("_DoubleSidedEnable", 1); m.SetFloat("_CullMode", 0); m.SetFloat("_CullModeForward", 0);
            if (isNew) AssetDatabase.CreateAsset(m, mp);
            ResetKeywords(m); EditorUtility.SetDirty(m);

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var roofT = root.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.name == name + "_roof");
                if (roofT == null) { skipped++; continue; }
                if (farOnly)
                {
                    roofT.GetComponent<MeshFilter>().sharedMesh = mesh;
                    roofT.sharedMaterial = m;
                }
                else
                {
                    var envT = root.transform.Find(name + "_roofenv");
                    if (envT == null)
                    {
                        envT = new GameObject(name + "_roofenv").transform;
                        envT.SetParent(roofT.transform.parent, false);
                        envT.localPosition = roofT.transform.localPosition; envT.localRotation = roofT.transform.localRotation; envT.localScale = roofT.transform.localScale;
                        envT.gameObject.AddComponent<MeshFilter>(); envT.gameObject.AddComponent<MeshRenderer>();
                    }
                    envT.GetComponent<MeshFilter>().sharedMesh = mesh;
                    var envR = envT.GetComponent<MeshRenderer>(); envR.sharedMaterial = m;
                    var others = root.GetComponentsInChildren<MeshRenderer>(true).Where(r => r != envR && r != roofT).Cast<Renderer>().ToArray();
                    var lg = root.GetComponent<LODGroup>(); if (lg == null) lg = root.AddComponent<LODGroup>();
                    lg.SetLODs(new[] {
                        new LOD(lodSwitch, others.Concat(new Renderer[] { roofT }).ToArray()),
                        new LOD(0.002f, others.Concat(new Renderer[] { envR }).ToArray()) });
                    lg.RecalculateBounds();
                }
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                done++;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        return (left == 0 ? "DONE " : "PARTIAL ") + $"{complex} farOnly={farOnly} done={done} left={left} skipped={skipped}";
    }

    // 다시 구운 덩어리(out/)를 기존 _Baked 프리팹 자리에 덮어 넣는다(GUID 유지 — 단지·광화문 부분 프리팹 연결이 안 끊기게).
    // 프리팹 안에서 예전에 바꿔 둔 메시(_clean·_roofenv)는 FBX 메시로 되돌리고, 종류별 재질을 다시 붙인다.
    public static string RefreshBakedStep(string complex, float budgetSec = 20f)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var src = Path.Combine(PalaceBakeTool.BakeRoot, complex, "out");
        var dstRel = Root + "/" + complex;
        var dst = Path.Combine(Directory.GetCurrentDirectory(), dstRel.Replace('/', '\\'));
        bool copied = false;
        foreach (var f in Directory.GetFiles(src))
        {
            var fn = Path.GetFileName(f); if (!(fn.EndsWith(".png") || fn.EndsWith(".fbx"))) continue;
            var d = Path.Combine(dst, fn);
            if (File.Exists(d) && new FileInfo(d).Length == new FileInfo(f).Length && File.GetLastWriteTime(d) >= File.GetLastWriteTime(f)) continue;
            File.Copy(f, d, true); copied = true;
            if (sw.Elapsed.TotalSeconds > budgetSec * 0.6f) { AssetDatabase.Refresh(); return "COPYING " + complex; }
        }
        if (copied) { AssetDatabase.Refresh(); return "COPIED " + complex + " (다시 부르기)"; }
        var names = Directory.GetFiles(src, "*.fbx").Select(Path.GetFileNameWithoutExtension).ToList();
        var stampPath = Path.Combine(src, "_refreshed.txt");
        var doneSet = File.Exists(stampPath) ? new HashSet<string>(File.ReadAllLines(stampPath)) : new HashSet<string>();
        int done = 0, left = 0;
        foreach (var name in names)
        {
            if (doneSet.Contains(name)) { done++; continue; }
            if (sw.Elapsed.TotalSeconds > budgetSec) { left++; continue; }
            var fbx = dstRel + "/" + name + ".fbx";
            var mi = AssetImporter.GetAtPath(fbx) as ModelImporter;
            if (mi != null && mi.materialImportMode != ModelImporterMaterialImportMode.None) { mi.materialImportMode = ModelImporterMaterialImportMode.None; mi.SaveAndReimport(); }
            var meshes = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Mesh>().ToDictionary(x => x.name, x => x);
            var mats = new Dictionary<string, Material>();
            foreach (var cat in new[] { "wood", "win", "stone", "roof" })
            {
                string bcp = dstRel + "/" + name + "_" + cat + "_BC.png", np = dstRel + "/" + name + "_" + cat + "_N.png";
                if (!meshes.ContainsKey(name + "_" + cat)) continue;
                var tb = AssetImporter.GetAtPath(bcp) as TextureImporter; var tn = AssetImporter.GetAtPath(np) as TextureImporter;
                if (tb == null || tn == null) continue;
                if (tn.textureType != TextureImporterType.NormalMap) { tn.textureType = TextureImporterType.NormalMap; tn.SaveAndReimport(); }
                string mp = dstRel + "/MAT_" + name + "_" + cat + ".mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(mp); bool isNew = m == null; if (isNew) m = new Material(Shader.Find("HDRP/Lit"));
                m.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(bcp)); m.SetTexture("_NormalMap", AssetDatabase.LoadAssetAtPath<Texture2D>(np));
                m.SetFloat("_Smoothness", cat == "roof" ? 0.3f : (cat == "stone" ? 0.15f : 0.25f));
                m.SetFloat("_DoubleSidedEnable", 1); m.SetFloat("_CullMode", 0); m.SetFloat("_CullModeForward", 0);
                if (isNew) AssetDatabase.CreateAsset(m, mp);
                ResetKeywords(m); EditorUtility.SetDirty(m);
                mats[cat] = m;
            }
            string prefabPath = dstRel + "/" + name + "_Baked.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(fbx));
                foreach (var r in inst.GetComponentsInChildren<MeshRenderer>(true)) foreach (var kv in mats) if (r.name.EndsWith("_" + kv.Key)) r.sharedMaterial = kv.Value;
                PrefabUtility.SaveAsPrefabAsset(inst, prefabPath); UnityEngine.Object.DestroyImmediate(inst);
            }
            else
            {
                var root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    var lg = root.GetComponent<LODGroup>(); if (lg != null) UnityEngine.Object.DestroyImmediate(lg);
                    var env = root.transform.Find(name + "_roofenv"); if (env != null) UnityEngine.Object.DestroyImmediate(env.gameObject);
                    foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        // 오브젝트가 하나뿐인 FBX(해태·월대)는 루트 이름이 <덩어리>_Baked 라 메시 이름으로 종류를 찾는다
                        var mf = r.GetComponent<MeshFilter>(); string key = r.name;
                        if (!meshes.ContainsKey(key) && mf != null && mf.sharedMesh != null) key = mf.sharedMesh.name.Replace("_clean", "").Replace("_roofenv", "_roof");
                        Mesh fm; if (mf != null && meshes.TryGetValue(key, out fm)) mf.sharedMesh = fm;
                        foreach (var kv in mats) if (key.EndsWith("_" + kv.Key)) r.sharedMaterial = kv.Value;
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            File.AppendAllText(stampPath, name + "\n");
            done++;
        }
        AssetDatabase.SaveAssets();
        return (left == 0 ? "DONE " : "PARTIAL ") + $"{complex} done={done} left={left}";
    }

    // ---------------- 4. 창·문 판(카드) ----------------
    // 창살·문살을 실제 막대 메시로 두면 멀리서 지글거린다(사용자 지적 2026-09-27) → 메시마다 앞·뒤 두 장의 판에
    // 창살+창호지 모습을 구워(_Cards/<키>_card.fbx + _BC/_N.png) 원본 단지의 해당 부재 메시를 판으로 바꾼다.
    public const string CardDir = Root + "/_Cards";
    static string CardBake { get { return Path.Combine(PalaceBakeTool.BakeRoot, "_cards"); } }
    static readonly string[] CardKeys = { "window", "changho", "munsal", "gyeokja", "salchang", "door" };
    static readonly string[] CardExclude = { "doorframe", "door_frame", "frame", "handle", "iron", "ornament", "hinge", "lock", "knob", "paper", "nodoors", "shadow" };
    // 창살이 창틀 메시에 붙어 있는 것(창호지를 붙였던 창틀)
    static readonly HashSet<string> CardFrames = new HashSet<string> { "SM_HNM_Windowframe_A", "SM_HNM_Windowframe_B", "SM_SJJ_HG_Windowframe01S", "SM_SMJC_WindowFrame02D", "SM_CGCS_WindowFrameB", "SM_SMJC_WindowFrameC" };

    static bool IsCardMesh(Mesh m)
    {
        var l = m.name.ToLowerInvariant();
        bool ok = CardFrames.Contains(m.name) || (CardKeys.Any(k => l.Contains(k)) && !CardExclude.Any(k => l.Contains(k)));
        if (!ok) return false;
        var s = m.bounds.size; var a = new[] { s.x, s.y, s.z }; Array.Sort(a);
        return a[0] <= 0.3f && a[1] >= 0.2f && a[2] <= 7f;
    }
    public static string CardKey(Mesh m)
    {
        return m.name + "_" + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(m)).Substring(0, 6);
    }
    static Dictionary<string, KeyValuePair<Mesh, Material[]>> CardCandidates()
    {
        var d = new Dictionary<string, KeyValuePair<Mesh, Material[]>>();
        foreach (var cx in FullOriginalComplexes)
        {
            var pre = AssetDatabase.LoadAssetAtPath<GameObject>(OriginalPath(cx)); if (pre == null) continue;
            foreach (var r in pre.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
                var m = mf.sharedMesh; if (m.name.EndsWith("_card") || !IsCardMesh(m)) continue;
                var k = CardKey(m); if (!d.ContainsKey(k)) d[k] = new KeyValuePair<Mesh, Material[]>(m, r.sharedMaterials);
            }
        }
        return d;
    }

    // 메시마다 OBJ(메시 자기 좌표, x 뒤집기 — 덩어리 OBJ 와 같은 규칙) + MTL. 이미 있으면 건너뜀.
    public static string ExportCardsStep(float budgetSec = 20f)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Directory.CreateDirectory(CardBake);
        var tsv = Path.Combine(CardBake, "cards.tsv");
        var have = File.Exists(tsv) ? new HashSet<string>(File.ReadAllLines(tsv).Select(l => l.Split('\t')[0])) : new HashSet<string>();
        var all = CardCandidates(); int done = 0, left = 0; string proj = Directory.GetCurrentDirectory();
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        foreach (var kv in all)
        {
            if (have.Contains(kv.Key)) { done++; continue; }
            if (sw.Elapsed.TotalSeconds > budgetSec) { left++; continue; }
            var mesh = kv.Value.Key; var mats = kv.Value.Value;
            var v = mesh.vertices; var nrm = mesh.normals; var uv0 = mesh.uv;
            bool hasN = nrm != null && nrm.Length == v.Length, hasUV = uv0 != null && uv0.Length == v.Length;
            var mtl = new StringBuilder(); var seen = new HashSet<string>(); int vtCount = 0; long tris = 0;
            using (var w = new StreamWriter(Path.Combine(CardBake, kv.Key + ".obj"), false, new UTF8Encoding(false), 1 << 20))
            {
                w.Write("mtllib " + kv.Key + ".mtl\no " + kv.Key + "\n");
                foreach (var p in v) w.Write("v " + (-p.x).ToString("F5", inv) + " " + p.y.ToString("F5", inv) + " " + p.z.ToString("F5", inv) + "\n");
                if (hasN) foreach (var d in nrm) w.Write("vn " + (-d.x).ToString("F4", inv) + " " + d.y.ToString("F4", inv) + " " + d.z.ToString("F4", inv) + "\n");
                var vtBlocks = new Dictionary<Vector4, int>();
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    var m = s < mats.Length ? mats[s] : null; if (m == null) continue;
                    Vector2 sc = m.HasProperty("_BaseColorMap") ? m.GetTextureScale("_BaseColorMap") : Vector2.one;
                    Vector2 of = m.HasProperty("_BaseColorMap") ? m.GetTextureOffset("_BaseColorMap") : Vector2.zero;
                    string mname = m.name.Replace(' ', '_') + "_" + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(m)).Substring(0, 6);
                    if (seen.Add(mname))
                    {
                        var bc = m.HasProperty("_BaseColorMap") ? m.GetTexture("_BaseColorMap") : null;
                        var nm = m.HasProperty("_NormalMap") ? m.GetTexture("_NormalMap") : null;
                        var col = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
                        mtl.Append("newmtl " + mname + "\nKd " + col.r.ToString("F4", inv) + " " + col.g.ToString("F4", inv) + " " + col.b.ToString("F4", inv) + "\n");
                        if (bc) mtl.Append("map_Kd " + Path.Combine(proj, AssetDatabase.GetAssetPath(bc)).Replace('\\', '/') + "\n");
                        if (nm) mtl.Append("norm " + Path.Combine(proj, AssetDatabase.GetAssetPath(nm)).Replace('\\', '/') + "\n");
                        mtl.Append("\n");
                    }
                    int vtStart; var stKey = new Vector4(sc.x, sc.y, of.x, of.y);
                    if (!vtBlocks.TryGetValue(stKey, out vtStart))
                    {
                        vtStart = vtCount + 1; vtBlocks[stKey] = vtStart;
                        if (hasUV) { foreach (var u in uv0) w.Write("vt " + (u.x * sc.x + of.x).ToString("F5", inv) + " " + (u.y * sc.y + of.y).ToString("F5", inv) + "\n"); vtCount += uv0.Length; }
                    }
                    w.Write("usemtl " + mname + "\n");
                    var idx = mesh.GetTriangles(s);
                    for (int t = 0; t < idx.Length; t += 3)
                    {
                        w.Write("f");
                        foreach (int kk in new[] { idx[t], idx[t + 2], idx[t + 1] })
                        {
                            w.Write(" " + (kk + 1));
                            if (hasUV) w.Write("/" + (vtStart + kk)); else if (hasN) w.Write("/");
                            if (hasN) w.Write("/" + (kk + 1));
                        }
                        w.Write("\n"); tris++;
                    }
                }
            }
            File.WriteAllText(Path.Combine(CardBake, kv.Key + ".mtl"), mtl.ToString());
            File.AppendAllText(tsv, kv.Key + "\t" + AssetDatabase.GetAssetPath(mesh) + "\t" + tris + "\n");
            done++;
        }
        return (left == 0 ? "DONE " : "PARTIAL ") + $"cards total={all.Count} done={done} left={left}";
    }

    // 구운 판을 가져오고(재질 만들기) 원본 단지 프리팹의 해당 부재를 판으로 바꾼다
    public static string ImportCardsStep(float budgetSec = 20f)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var src = Path.Combine(CardBake, "out"); if (!Directory.Exists(src)) return "no out";
        PalaceBakeTool_EnsureFolder(CardDir);
        var dst = Path.Combine(Directory.GetCurrentDirectory(), CardDir.Replace('/', '\\'));
        bool copied = false;
        foreach (var f in Directory.GetFiles(src))
        {
            var fn = Path.GetFileName(f); if (!(fn.EndsWith(".png") || fn.EndsWith(".fbx"))) continue;
            var d = Path.Combine(dst, fn);
            if (File.Exists(d) && new FileInfo(d).Length == new FileInfo(f).Length) continue;
            File.Copy(f, d, true); copied = true;
            if (sw.Elapsed.TotalSeconds > budgetSec * 0.5f) break;
        }
        if (copied) AssetDatabase.Refresh();
        int mats = 0;
        foreach (var f in Directory.GetFiles(dst, "*_card.fbx"))
        {
            var key = Path.GetFileNameWithoutExtension(f); var mp = CardDir + "/MAT_" + key + ".mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(mp) != null) continue;
            var bc = AssetDatabase.LoadAssetAtPath<Texture2D>(CardDir + "/" + key + "_BC.png");
            var nm = AssetDatabase.LoadAssetAtPath<Texture2D>(CardDir + "/" + key + "_N.png");
            if (bc == null || nm == null) continue;
            var m = new Material(Shader.Find("HDRP/Lit"));
            m.SetTexture("_BaseColorMap", bc); m.SetTexture("_NormalMap", nm);
            m.SetFloat("_Smoothness", 0.12f); m.SetFloat("_Metallic", 0f);
            // 팔각창처럼 사각 판 모서리가 창 밖으로 나오는 것은 투명 처리(굽기 쪽 info 의 cutout)
            var infoPath = Path.Combine(src, key + "_info.json");
            if (File.Exists(infoPath) && File.ReadAllText(infoPath).Contains("\"cutout\": true"))
            {
                m.SetFloat("_AlphaCutoffEnable", 1f); m.SetFloat("_AlphaCutoff", 0.5f);
                var ti = AssetImporter.GetAtPath(CardDir + "/" + key + "_BC.png") as TextureImporter;
                if (ti != null && !ti.alphaIsTransparency) { ti.alphaIsTransparency = true; ti.SaveAndReimport(); }
            }
            AssetDatabase.CreateAsset(m, mp); ResetKeywords(m); EditorUtility.SetDirty(m); mats++;
            if (sw.Elapsed.TotalSeconds > budgetSec) break;
        }
        AssetDatabase.SaveAssets();
        int ready = Directory.GetFiles(dst, "*_card.fbx").Count(f => AssetDatabase.LoadAssetAtPath<Material>(CardDir + "/MAT_" + Path.GetFileNameWithoutExtension(f) + ".mat") != null);
        return $"cards copied={copied} newMats={mats} ready={ready}";
    }

    static void PalaceBakeTool_EnsureFolder(string rel)
    {
        var parts = rel.Split('/'); var cur = parts[0];
        for (int i = 1; i < parts.Length; i++) { var nx = cur + "/" + parts[i]; if (!AssetDatabase.IsValidFolder(nx)) AssetDatabase.CreateFolder(cur, parts[i]); cur = nx; }
    }

    public static string ApplyCards(string complex)
    {
        var path = OriginalPath(complex);
        var root = PrefabUtility.LoadPrefabContents(path);
        int swapped = 0, missing = 0; long before = 0, after = 0;
        try
        {
            var cache = new Dictionary<string, KeyValuePair<Mesh, Material>>();
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
                var m = mf.sharedMesh; if (m.name.EndsWith("_card") || !IsCardMesh(m)) continue;
                var key = CardKey(m);
                KeyValuePair<Mesh, Material> c;
                if (!cache.TryGetValue(key, out c))
                {
                    var cm = AssetDatabase.LoadAllAssetsAtPath(CardDir + "/" + key + "_card.fbx").OfType<Mesh>().FirstOrDefault();
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(CardDir + "/MAT_" + key + "_card.mat");
                    c = new KeyValuePair<Mesh, Material>(cm, mat); cache[key] = c;
                }
                if (c.Key == null || c.Value == null) { missing++; continue; }
                before += Tris(m); after += Tris(c.Key);
                mf.sharedMesh = c.Key; r.sharedMaterials = new[] { c.Value };
                foreach (var mc in r.GetComponents<MeshCollider>()) mc.sharedMesh = c.Key;
                swapped++;
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        return $"{complex} swapped={swapped} missing={missing} tris {before} -> {after}";
    }

    // 원본 프리팹의 렌더러·삼각형 수
    public static string Count(string complex)
    {
        var pre = AssetDatabase.LoadAssetAtPath<GameObject>(OriginalPath(complex));
        long t = 0; int n = 0;
        foreach (var r in pre.GetComponentsInChildren<MeshRenderer>(true)) { var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue; n++; t += Tris(mf.sharedMesh); }
        return $"{complex} renderers={n} tris={t}";
    }
}
