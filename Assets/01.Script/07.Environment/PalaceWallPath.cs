// 궁장(궁궐 담) 깔기 — 경복궁 광화문 양옆 궁장(단순화판 GHM_WallA3)의 평평한 구간에서 뽑은 반복 부품을
// 마디(nodes) 사이에 이어 깐다. 부품 메시는 밖에서 만들고(98.Blender/Gungjang/build_module.py)
// PalaceLodBinImport.ImportMeshes 로 메시 에셋을 만든 뒤, 여기서는 배치·자르기만 한다(에디터 전용).
//
// - 부품 메시 좌표: X = 진행 방향(0 ~ moduleLength), Y = 위(0 = 광화문 궁장 기단 바닥), Z = 진행 방향 왼쪽.
// - 모서리: 들어오는 방향과 나가는 방향의 이등분 면(마이터)으로 양쪽 부품을 잘라 맞댄다.
// - 문 쪽 끝: 수직으로 자르고 마구리(capMesh)로 막는다(문 궁장 토막 속으로 조금 들어가게 마디를 둠).
// - 잘린 부품은 새 메시 에셋(clippedFolder)으로 저장한다. 다시 깔 때 옛 것은 휴지통으로 보낸다.
// - LOD: 부품마다 LODGroup(LOD0 = 기와, LOD1 = 검은 판). 전환 화면 높이 = 크기 / (거리 × 2·tan(FOV/2)).
// 예전엔 KCISA 스플라인 빌더(화성 성곽, 1.9 GB, Built-in 재질)를 검토했으나 궁장 모양이 달라
// 이미 있는 광화문 궁장(같은 재질·같은 높이)을 이어 쓰기로 함(2026-09-30).
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class PalaceWallPath : MonoBehaviour
{
    [Serializable]
    public class Chain
    {
        [Tooltip("이름(만든 오브젝트·메시 이름에 씀, 영문)")] public string name = "Chain";
        [Tooltip("마디: 시작 → 모서리 … → 끝. 월드 XZ 만 쓰고 높이는 baseY")] public List<Transform> nodes = new List<Transform>();
        [Tooltip("시작 쪽 끝을 마구리로 막음(광화문 날개에 이어지는 쪽은 끔)")] public bool capStart = true;
        [Tooltip("끝 쪽 끝을 마구리로 막음")] public bool capEnd = true;
    }

    [Header("부품(밖에서 만든 메시)")]
    public Mesh moduleLod0;
    public Mesh moduleLod1;
    public Mesh capMesh;
    [Tooltip("LOD0 서브메시 순서: Ws02A(기단), Ws03A(몸통), Top01A(기와)")] public Material[] lod0Materials;
    [Tooltip("LOD1 서브메시 순서: Ws02A, Ws03A, 검은 판")] public Material[] lod1Materials;
    [Tooltip("마구리 서브메시 순서: Ws02A, Ws03A, 검은 판")] public Material[] capMaterials;
    [Tooltip("부품 길이(m) — 메시 X 0 ~ 이 값. 이 간격으로 잇는다")] public float moduleLength = 47.46432f;
    [Tooltip("부품 단면 반폭(m, 처마 끝까지) — 모서리 맞춤 여유 계산")] public float halfWidth = 2.4f;
    [Tooltip("부품 바닥(메시 Y 0)의 월드 높이 — 광화문 궁장 기단 바닥과 같게")] public float baseY = 10.59f;

    [Header("LOD (카메라 FOV·lodBias 1 기준 거리)")]
    [Tooltip("이 거리까지 기와(LOD0), 그 뒤 검은 판(LOD1)")] public float lod0Distance = 150f;
    [Tooltip("이 거리 밖에서 부품을 끔")] public float cullDistance = 5000f;
    [Tooltip("마구리를 끄는 거리")] public float capCullDistance = 1500f;
    public float referenceFov = 60f;

    [Header("충돌(부품마다 BoxCollider, 메시 Y 기준)")]
    public float colliderBottom = -2.5f;
    public float colliderTop = 4.7f;
    public float colliderThickness = 3.38f;

    [Header("저장")]
    [Tooltip("잘린 부품 메시를 저장할 폴더")]
    public string clippedFolder = "Assets/02.Resources/01.Asset/Models/Gyeongbokgung/_Baked/Gungjang/Clipped";

    public List<Chain> chains = new List<Chain>();

    // ------------------------------------------------------------------ 메시 자르기(에디터·런타임 공용 수학)
    [StructLayout(LayoutKind.Sequential)]
    struct PackedVertex
    {
        public Vector3 p;
        public ushort nx, ny, nz, nw;
        public ushort tx, ty, tz, tw;
        public Vector2 uv;
    }

    struct Vtx
    {
        public Vector3 p, n; public Vector4 t; public Vector2 uv;
    }

    static readonly VertexAttributeDescriptor[] Layout =
    {
        new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
        new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float16, 4),
        new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float16, 4),
        new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
    };

    static Vtx Lerp(Vtx a, Vtx b, float s)
    {
        Vtx r;
        r.p = Vector3.Lerp(a.p, b.p, s);
        r.n = Vector3.Lerp(a.n, b.n, s).normalized;
        var t3 = Vector3.Lerp(new Vector3(a.t.x, a.t.y, a.t.z), new Vector3(b.t.x, b.t.y, b.t.z), s).normalized;
        r.t = new Vector4(t3.x, t3.y, t3.z, a.t.w);
        r.uv = Vector2.Lerp(a.uv, b.uv, s);
        return r;
    }

    // 다각형을 평면으로 자름(평면의 양(+)쪽을 남김)
    static List<Vtx> ClipPoly(List<Vtx> poly, Plane pl)
    {
        var o = new List<Vtx>(poly.Count + 2);
        for (int i = 0; i < poly.Count; i++)
        {
            var a = poly[i]; var b = poly[(i + 1) % poly.Count];
            float da = pl.GetDistanceToPoint(a.p), db = pl.GetDistanceToPoint(b.p);
            if (da >= 0f) o.Add(a);
            if ((da >= 0f) != (db >= 0f)) o.Add(Lerp(a, b, da / (da - db)));
        }
        return o;
    }

    /// <summary>메시를 평면들(양쪽을 남김)로 자른 새 메시. 잘린 삼각형이 하나도 없으면 null(원래 것을 그대로 쓰면 됨).</summary>
    public static Mesh ClipMesh(Mesh src, IList<Plane> keep, string name, out int triCount)
    {
        triCount = 0;
        var pos = src.vertices; var nor = src.normals; var tan = src.tangents; var uv = src.uv;
        int nv = pos.Length;
        bool hasN = nor != null && nor.Length == nv, hasT = tan != null && tan.Length == nv, hasUV = uv != null && uv.Length == nv;
        var dist = new float[keep.Count][];
        for (int k = 0; k < keep.Count; k++)
        {
            dist[k] = new float[nv];
            for (int i = 0; i < nv; i++) dist[k][i] = keep[k].GetDistanceToPoint(pos[i]);
        }
        bool anyCut = false;
        var outV = new List<Vtx>(nv);
        var reuse = new Dictionary<int, int>();
        var subs = new List<List<int>>();
        Vtx Get(int i)
        {
            Vtx v; v.p = pos[i]; v.n = hasN ? nor[i] : Vector3.up; v.t = hasT ? tan[i] : new Vector4(1, 0, 0, 1); v.uv = hasUV ? uv[i] : Vector2.zero; return v;
        }
        int Reuse(int i)
        {
            if (!reuse.TryGetValue(i, out int j)) { j = outV.Count; outV.Add(Get(i)); reuse[i] = j; }
            return j;
        }
        for (int s = 0; s < src.subMeshCount; s++)
        {
            var tri = src.GetTriangles(s);
            var list = new List<int>(tri.Length);
            for (int i = 0; i < tri.Length; i += 3)
            {
                int ia = tri[i], ib = tri[i + 1], ic = tri[i + 2];
                bool allIn = true, allOut = false;
                for (int k = 0; k < keep.Count; k++)
                {
                    float da = dist[k][ia], db = dist[k][ib], dc = dist[k][ic];
                    if (da < 0f || db < 0f || dc < 0f) allIn = false;
                    if (da < 0f && db < 0f && dc < 0f) { allOut = true; break; }
                }
                if (allOut) { anyCut = true; continue; }
                if (allIn) { list.Add(Reuse(ia)); list.Add(Reuse(ib)); list.Add(Reuse(ic)); continue; }
                anyCut = true;
                var poly = new List<Vtx> { Get(ia), Get(ib), Get(ic) };
                for (int k = 0; k < keep.Count && poly.Count >= 3; k++) poly = ClipPoly(poly, keep[k]);
                if (poly.Count < 3) continue;
                int b0 = outV.Count; outV.AddRange(poly);
                for (int q = 1; q < poly.Count - 1; q++) { list.Add(b0); list.Add(b0 + q); list.Add(b0 + q + 1); }
            }
            subs.Add(list);
        }
        if (!anyCut) return null;
        var m = BuildPacked(outV, subs, name);
        foreach (var l in subs) triCount += l.Count / 3;
        return m;
    }

    static Mesh BuildPacked(List<Vtx> verts, List<List<int>> subs, string name)
    {
        var data = new PackedVertex[verts.Count];
        for (int i = 0; i < verts.Count; i++)
        {
            var v = verts[i];
            data[i] = new PackedVertex
            {
                p = v.p,
                nx = Mathf.FloatToHalf(v.n.x), ny = Mathf.FloatToHalf(v.n.y), nz = Mathf.FloatToHalf(v.n.z), nw = 0,
                tx = Mathf.FloatToHalf(v.t.x), ty = Mathf.FloatToHalf(v.t.y), tz = Mathf.FloatToHalf(v.t.z), tw = Mathf.FloatToHalf(v.t.w),
                uv = v.uv
            };
        }
        var m = new Mesh { name = name };
        m.indexFormat = verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        m.SetVertexBufferParams(verts.Count, Layout);
        m.SetVertexBufferData(data, 0, 0, data.Length, 0, MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds);
        int total = 0; foreach (var l in subs) total += l.Count;
        m.SetIndexBufferParams(total, m.indexFormat);
        var descs = new SubMeshDescriptor[subs.Count];
        int off = 0;
        if (m.indexFormat == IndexFormat.UInt16)
        {
            var all = new ushort[total];
            for (int s = 0; s < subs.Count; s++) { for (int i = 0; i < subs[s].Count; i++) all[off + i] = (ushort)subs[s][i]; descs[s] = new SubMeshDescriptor(off, subs[s].Count, MeshTopology.Triangles); off += subs[s].Count; }
            m.SetIndexBufferData(all, 0, 0, total, MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds);
        }
        else
        {
            var all = new int[total];
            for (int s = 0; s < subs.Count; s++) { subs[s].CopyTo(all, off); descs[s] = new SubMeshDescriptor(off, subs[s].Count, MeshTopology.Triangles); off += subs[s].Count; }
            m.SetIndexBufferData(all, 0, 0, total, MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds);
        }
        m.subMeshCount = subs.Count;
        for (int s = 0; s < subs.Count; s++) m.SetSubMesh(s, descs[s], MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds);
        m.RecalculateBounds();
        return m;
    }

    static int TriCount(Mesh m)
    {
        int n = 0; for (int s = 0; s < m.subMeshCount; s++) n += (int)m.GetIndexCount(s) / 3; return n;
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

#if UNITY_EDITOR
    const string BuiltName = "Built";

    [ContextMenu("궁장 다시 깔기")]
    void RebuildMenu() { Debug.Log(Rebuild()); }

    /// <summary>만든 것(Built)을 지우고 마디를 따라 다시 깐다. 결과 요약 문자열을 돌려줌.</summary>
    public string Rebuild()
    {
        if (EditorApplication.isPlaying) return "재생 중에는 안 함";
        if (moduleLod0 == null || moduleLod1 == null) return "부품 메시가 비어 있음";
        var sb = new System.Text.StringBuilder();

        // 1) 옛 결과 지우기
        var old = transform.Find(BuiltName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        EnsureFolder(clippedFolder);
        var oldAssets = new List<string>();
        foreach (var g in AssetDatabase.FindAssets("t:Mesh", new[] { clippedFolder })) oldAssets.Add(AssetDatabase.GUIDToAssetPath(g));
        if (oldAssets.Count > 0)
        {
            var failed = new List<string>();
            AssetDatabase.MoveAssetsToTrash(oldAssets.ToArray(), failed);
            sb.AppendLine("옛 잘린 메시 휴지통 " + (oldAssets.Count - failed.Count) + "개");
        }

        var built = new GameObject(BuiltName);
        Undo.RegisterCreatedObjectUndo(built, "궁장 다시 깔기");
        built.transform.SetParent(transform, false);

        float tanHalf = 2f * Mathf.Tan(referenceFov * 0.5f * Mathf.Deg2Rad);
        var lb = moduleLod0.bounds;
        int nModules = 0, nClipped = 0; long triLod0 = 0, triLod1 = 0;
        float totalLen = 0f;

        foreach (var ch in chains)
        {
            if (ch.nodes == null || ch.nodes.Count < 2) { sb.AppendLine(ch.name + ": 마디 부족"); continue; }
            var chRoot = new GameObject(ch.name);
            chRoot.transform.SetParent(built.transform, false);
            int nn = ch.nodes.Count;
            var P = new Vector3[nn];
            for (int i = 0; i < nn; i++) { var w = ch.nodes[i].position; P[i] = new Vector3(w.x, baseY, w.z); }
            var dir = new Vector3[nn - 1]; var len = new float[nn - 1];
            for (int j = 0; j < nn - 1; j++) { var d = Flat(P[j + 1] - P[j]); len[j] = d.magnitude; dir[j] = d / len[j]; totalLen += len[j]; }
            // 안쪽 마디의 마이터 면 법선(들어오는 방향 + 나가는 방향)
            var miter = new Vector3[nn];
            for (int i = 1; i < nn - 1; i++) miter[i] = (dir[i - 1] + dir[i]).normalized;

            for (int j = 0; j < nn - 1; j++)
            {
                var d = dir[j];
                var left = new Vector3(-d.z, 0f, d.x);
                var planes = new List<KeyValuePair<Vector3, Vector3>>(); // (점, 남길 쪽 법선)
                float tStart = 0f, tEnd = len[j];
                if (j > 0)
                {
                    var mN = miter[j];
                    planes.Add(new KeyValuePair<Vector3, Vector3>(P[j], mN));
                    tStart = -(halfWidth * Mathf.Abs(Vector3.Dot(left, mN)) / Vector3.Dot(d, mN) + 0.05f);
                }
                if (j < nn - 2)
                {
                    var mN = miter[j + 1];
                    planes.Add(new KeyValuePair<Vector3, Vector3>(P[j + 1], -mN));
                    tEnd += halfWidth * Mathf.Abs(Vector3.Dot(left, mN)) / Vector3.Dot(d, mN) + 0.05f;
                }
                else planes.Add(new KeyValuePair<Vector3, Vector3>(P[j + 1], -d));

                int count = Mathf.Max(1, Mathf.CeilToInt((tEnd - tStart) / moduleLength - 1e-4f));
                var rot = Quaternion.Euler(0f, Mathf.Atan2(-d.z, d.x) * Mathf.Rad2Deg, 0f);
                var inv = Quaternion.Inverse(rot);
                for (int k = 0; k < count; k++)
                {
                    var origin = P[j] + d * (tStart + k * moduleLength);
                    // 부품 좌표로 옮긴 평면
                    var local = new List<Plane>();
                    bool skip = false;
                    foreach (var pl in planes)
                    {
                        var lp = inv * (pl.Key - origin); var ln = inv * pl.Value;
                        var plane = new Plane(ln, lp);
                        // 부품 경계 상자 8 꼭짓점으로 판정
                        int inCnt = 0;
                        for (int c = 0; c < 8; c++)
                        {
                            var cp = new Vector3((c & 1) == 0 ? lb.min.x : lb.max.x, (c & 2) == 0 ? lb.min.y : lb.max.y, (c & 4) == 0 ? lb.min.z : lb.max.z);
                            if (plane.GetDistanceToPoint(cp) >= -1e-4f) inCnt++;
                        }
                        if (inCnt == 0) { skip = true; break; }
                        if (inCnt < 8) local.Add(plane);
                    }
                    if (skip) continue;
                    string baseName = ch.name + "_S" + j + "_M" + k.ToString("00");
                    Mesh m0 = moduleLod0, m1 = moduleLod1;
                    if (local.Count > 0)
                    {
                        m0 = ClipMesh(moduleLod0, local, baseName + "_LOD0", out int t0) ?? moduleLod0;
                        m1 = ClipMesh(moduleLod1, local, baseName + "_LOD1", out int t1) ?? moduleLod1;
                        if (m0 != moduleLod0) AssetDatabase.CreateAsset(m0, clippedFolder + "/" + baseName + "_LOD0.asset");
                        if (m1 != moduleLod1) AssetDatabase.CreateAsset(m1, clippedFolder + "/" + baseName + "_LOD1.asset");
                        nClipped++;
                    }
                    var go = MakeModule(chRoot.transform, baseName, origin, rot, m0, m1, tanHalf);
                    nModules++; triLod0 += TriCount(m0); triLod1 += TriCount(m1);
                }
            }
            if (capMesh != null)
            {
                if (ch.capStart) MakeCap(chRoot.transform, ch.name + "_CapStart", P[0], Quaternion.Euler(0f, Mathf.Atan2(-dir[0].z, dir[0].x) * Mathf.Rad2Deg, 0f), tanHalf);
                if (ch.capEnd) { var dl = dir[nn - 2]; MakeCap(chRoot.transform, ch.name + "_CapEnd", P[nn - 1], Quaternion.Euler(0f, Mathf.Atan2(-dl.z, dl.x) * Mathf.Rad2Deg + 180f, 0f), tanHalf); }
            }
        }
        AssetDatabase.SaveAssets();
        EditorUtility.SetDirty(gameObject);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        sb.AppendLine("길이 " + totalLen.ToString("F1") + " m, 부품 " + nModules + "개(잘린 것 " + nClipped + "), 삼각형 LOD0 합 " + triLod0 + " / LOD1 합 " + triLod1);
        return sb.ToString();
    }

    GameObject MakeModule(Transform parent, string name, Vector3 origin, Quaternion rot, Mesh m0, Mesh m1, float tanHalf)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(origin, rot);
        var r0 = MakeRenderer(go.transform, "LOD0", m0, lod0Materials);
        var r1 = MakeRenderer(go.transform, "LOD1", m1, lod1Materials);
        var b = m0.bounds;
        float size = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        var lg = go.AddComponent<LODGroup>();
        lg.fadeMode = LODFadeMode.CrossFade; lg.animateCrossFading = true;
        lg.SetLODs(new[]
        {
            new LOD(Mathf.Min(0.95f, size / (lod0Distance * tanHalf)), new Renderer[] { r0 }),
            new LOD(size / (cullDistance * tanHalf), new Renderer[] { r1 })
        });
        lg.localReferencePoint = b.center; lg.size = size;
        var bc = go.AddComponent<BoxCollider>();
        bc.center = new Vector3(b.center.x, (colliderBottom + colliderTop) * 0.5f, 0f);
        bc.size = new Vector3(b.size.x, colliderTop - colliderBottom, colliderThickness);
        return go;
    }

    void MakeCap(Transform parent, string name, Vector3 pos, Quaternion rot, float tanHalf)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(pos, rot);
        var r = MakeRenderer(go.transform, "Cap", capMesh, capMaterials);
        var b = capMesh.bounds;
        float size = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        var lg = go.AddComponent<LODGroup>();
        lg.fadeMode = LODFadeMode.CrossFade; lg.animateCrossFading = true;
        lg.SetLODs(new[] { new LOD(size / (capCullDistance * tanHalf), new Renderer[] { r }) });
        lg.localReferencePoint = b.center; lg.size = size;
    }

    static Renderer MakeRenderer(Transform parent, string name, Mesh mesh, Material[] mats)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterials = mats;
        return mr;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }

    // ------------------------------------------------------------------ 광화문 궁장 날개 끝 자르기
    // 광화문 단순화판(GwanghwamunGate_Simplified) 궁장 날개(A = 동쪽, B = 서쪽)를 날개 좌표 X = xt(평평한 구간,
    // 좁은 기와줄 c_60 자리)에서 잘라 바깥 끝(끝 마감 기와)을 없앤다 — 거기서부터 궁장 부품이 이어진다.
    // 원래 메시는 그대로 두고 잘린 사본을 outFolder 에 만들어 프리팹의 MeshFilter·MeshCollider 만 바꿔 낌.
    // 날개 좌표(프리팹 좌표, x 는 OBJ 의 반대): A 기준점 (-57.84, 1.49, zc), 바깥 방향 (-1, 0, 0.008713)
    public static string TrimGwanghwamunWings(string prefabPath, string outFolder, float xt)
    {
        const float slope = 0.008713f, xo = 57.84f;
        var sb = new System.Text.StringBuilder();
        EnsureFolder(outFolder);
        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            foreach (var side in new[] { "A", "B" })
            {
                Vector3 r0, a;
                if (side == "A") { r0 = new Vector3(-xo, 1.49f, -29.645f + slope * (xo - 14.735f)); a = new Vector3(-1f, 0f, slope).normalized; }
                else { r0 = new Vector3(xo, 1.49f, -29.905f - slope * (xo - 14.735f)); a = new Vector3(1f, 0f, -slope).normalized; }
                var cut = r0 + a * xt;       // 프리팹 루트 좌표
                foreach (var part in new[] { "1", "2", "3" })
                {
                    string baseName = "GHM_Wall" + side + part;
                    var lodT = root.transform.Find("Walls/" + baseName + "_LOD");
                    if (lodT == null) { sb.AppendLine(baseName + " 없음"); continue; }
                    foreach (var mf in lodT.GetComponentsInChildren<MeshFilter>(true))
                    {
                        var src = mf.sharedMesh; if (src == null) continue;
                        // 이미 잘린 사본이면 건너뜀(두 번 자르지 않게)
                        if (AssetDatabase.GetAssetPath(src).StartsWith(outFolder)) { sb.AppendLine(mf.name + " 이미 잘림"); continue; }
                        var tr = mf.transform;
                        var lp = tr.InverseTransformPoint(root.transform.TransformPoint(cut));
                        var ln = tr.InverseTransformDirection(root.transform.TransformDirection(-a));
                        var mesh = ClipMesh(src, new[] { new Plane(ln, lp) }, src.name + "_Trim", out int tris);
                        if (mesh == null) { sb.AppendLine(mf.name + " 그대로(" + TriCount(src) + ")"); continue; }
                        string ap = outFolder + "/" + src.name + "_Trim.asset";
                        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(ap);
                        if (existing != null) { existing.Clear(); EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); mesh = existing; EditorUtility.SetDirty(existing); }
                        else AssetDatabase.CreateAsset(mesh, ap);
                        // 같은 원본 메시를 쓰는 곳(LOD0·LOD1·충돌체) 모두 바꿈
                        foreach (var mf2 in lodT.GetComponentsInChildren<MeshFilter>(true)) if (mf2.sharedMesh == src) mf2.sharedMesh = mesh;
                        foreach (var mc in lodT.GetComponentsInChildren<MeshCollider>(true)) if (mc.sharedMesh == src) mc.sharedMesh = mesh;
                        sb.AppendLine(mf.name + " " + TriCount(src) + " -> " + tris + " (" + ap + ")");
                    }
                }
            }
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        return sb.ToString();
    }
#endif
}
