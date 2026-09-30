// 근정전 단순화판(Blender 에서 줄인 OBJ)을 유니티 프리팹으로 조립하는 도구 — MCP 에서 정적 메서드로 호출.
// 사정전·강녕전용 PalaceLodImport 를 그대로 쓰되, 근정전은 구성이 달라 따로 둠:
//  - 전각(Hall)과 월대(Woldae)를 다른 렌더러로(내부에 들어갔을 때 외부만 저품질로 바꿀 수 있게), LOD1 은 구운 GJJ 덩어리
//  - 행각은 12토막(HG_SegNN), 토막마다 LOD0/1/2(모두 Blender 에서 만든 것) — 예전엔 행각 전체가 한 LOD 라 한쪽에만 가까이 가도 전부 원본으로 그렸음
//  - 내부(구조·집기)는 따로 LODGroup(가까울 때만)
//  - 여는 문짝은 원본 문짝(한 짝 수천~수만 삼각형) 대신 앞뒤 판(카드) — Blender 에서 구운 GJCARD_* 텍스처
// OBJ 좌표는 단지 프리팹 루트 기준(LoadObj 규칙: Unity x = -OBJ x).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class PalaceGJImport
{
    public const string CX = "Geunjeongjeon";
    public static string ObjDir = @"D:\Blender\Enviroment\PalaceJoseon\근정전\LOD";
    static string Dir { get { return PalaceBakeTool.BakedAssetRoot + "/" + CX; } }
    static string Simp { get { return Dir + "/Simplified"; } }

    static void Ensure()
    {
        if (!AssetDatabase.IsValidFolder(Simp)) AssetDatabase.CreateFolder(Dir, "Simplified");
    }

    static void SaveMeshes(List<Mesh> ms, string path)
    {
        foreach (var m in ms) PalaceMeshCompact.CompactMesh(m);
        if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(ms[0], path);
        for (int i = 1; i < ms.Count; i++) AssetDatabase.AddObjectToAsset(ms[i], path);
        AssetDatabase.SaveAssets();
    }

    static LOD MakeLod(float h, GameObject g)
    {
        return new LOD(h, g.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && r.gameObject.activeSelf).ToArray());
    }

    static GameObject Chunk(string chunk, Transform parent)
    {
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + "/" + chunk + "_Baked.prefab");
        if (pf == null) return null;
        var g = (GameObject)PrefabUtility.InstantiatePrefab(pf); g.transform.SetParent(parent, false); return g;
    }

    static Mesh Load(string obj, List<Mesh> ms, out Material[] mats)
    {
        var m = PalaceLodImport.LoadObj(Path.Combine(ObjDir, obj + ".obj"), obj, out mats); ms.Add(m); return m;
    }

    static string Tris(Mesh m) { return (m.triangles.Length / 3).ToString(); }

    // 건물: LOD0 = OBJ 들(각각 렌더러) + 구운 지붕 껍데기로 만든 받침판, LOD1 = 구운 덩어리들
    public static string BuildBuilding(string name, string[] objs, string[] chunks, float h0, float cull)
    {
        Ensure();
        var root = new GameObject(name + "_LOD");
        try
        {
            var ms = new List<Mesh>(); var log = name + ":";
            var l0 = new GameObject("LOD0"); l0.transform.SetParent(root.transform, false);
            foreach (var o in objs)
            {
                Material[] mats; var m = Load(o, ms, out mats);
                PalaceLodImport.AddRenderer(l0.transform, o, m, mats, true);
                log += " " + o + "=" + Tris(m) + "(null mats " + mats.Count(x => x == null) + ")";
            }
            foreach (var ch in chunks)
            {
                var env = PalaceLodImport.EnvMesh(CX, ch); if (env == null) continue;
                var bk = PalaceLodImport.MakeBacking(env, 0.25f, ch + "_RoofBacking"); ms.Add(bk);
                PalaceLodImport.AddRenderer(l0.transform, ch + "_RoofBacking", bk, new[] { PalaceLodImport.BlackRoof() })
                    .GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                log += " backing:" + ch;
            }
            var lods = new List<LOD> { };
            if (chunks.Length > 0)
            {
                var l1 = new GameObject("LOD1"); l1.transform.SetParent(root.transform, false);
                foreach (var ch in chunks) Chunk(ch, l1.transform);
                lods.Add(MakeLod(h0, l0)); lods.Add(MakeLod(cull, l1));
            }
            else lods.Add(MakeLod(cull, l0));
            var lg = root.AddComponent<LODGroup>();
            lg.SetLODs(lods.ToArray());
            lg.RecalculateBounds(); lg.fadeMode = LODFadeMode.CrossFade; lg.animateCrossFading = true;
            SaveMeshes(ms, Simp + "/" + name + ".asset");
            PrefabUtility.SaveAsPrefabAsset(root, Simp + "/" + name + "_LOD.prefab");
            return log + " size=" + lg.size.ToString("F1");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    // 행각 토막들: 토막마다 LODGroup(LOD0/1/2 = GJ_HG_SegNN_LOD0/1/2.obj). from~to 토막만 만들어 기존 프리팹에 덧붙임(시간 제한 때문에 나눠 호출)
    public static string BuildHGSegments(int from, int to, float h0, float h1, float cull)
    {
        Ensure();
        var path = Simp + "/GJ_HG_Segments_LOD.prefab";
        var root = File.Exists(path) ? PrefabUtility.LoadPrefabContents(path) : new GameObject("GJ_HG_Segments_LOD");
        bool loaded = File.Exists(path);
        var log = "";
        try
        {
            for (int k = from; k <= to; k++)
            {
                var sn = "HG_Seg" + k.ToString("00");
                var old = root.transform.Find(sn); if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                var sg = new GameObject(sn); sg.transform.SetParent(root.transform, false);
                var ms = new List<Mesh>(); var lods = new List<LOD>();
                float[] hs = { h0, h1, cull };
                for (int L = 0; L < 3; L++)
                {
                    var o = "GJ_" + sn + "_LOD" + L; Material[] mats; var m = Load(o, ms, out mats);
                    var lg0 = new GameObject("LOD" + L); lg0.transform.SetParent(sg.transform, false);
                    PalaceLodImport.AddRenderer(lg0.transform, o, m, mats, L == 0);
                    lods.Add(MakeLod(hs[L], lg0));
                    log += o + "=" + Tris(m) + " ";
                }
                var lg = sg.AddComponent<LODGroup>(); lg.SetLODs(lods.ToArray());
                lg.RecalculateBounds(); lg.fadeMode = LODFadeMode.CrossFade; lg.animateCrossFading = true;
                SaveMeshes(ms, Simp + "/GJ_" + sn + ".asset");
                log += "size=" + lg.size.ToString("F1") + "; ";
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
            return log;
        }
        finally { if (loaded) PrefabUtility.UnloadPrefabContents(root); else UnityEngine.Object.DestroyImmediate(root); }
    }

    // 행각 토막 번호(1~12) — 토막을 나눌 때 쓴 규칙과 같음(단지 좌표 c 를 나눌 때의 좌표 bx = 140 − x, by = −z 로 바꿔 판정)
    public static int HGSegOf(Vector3 c)
    {
        float bx = 140f - c.x, by = -c.z;
        float mnx = 81.15262f, mny = -1.2451899f, mxx = 198.81877f, mxy = 138.06981f;
        float[] d = { by - mny, mxy - by, bx - mnx, mxx - bx };
        int side = 0; for (int i = 1; i < 4; i++) if (d[i] < d[side]) side = i;
        if (side == 0) return bx < 140f ? 5 : 9;
        if (side == 1) { if (bx < 106f) return 1; if (bx < 131.5f) return 2; if (bx < 173f) return 3; return 4; }
        float step = (138.2f + 1.3f) / 4f; int b = Mathf.Clamp((int)((by + 1.3f) / step), 0, 3);
        return (side == 2 ? 5 : 9) + b;
    }

    // 행각 지붕 검은 판: 구운 행각 지붕 껍데기(GJJ_HGa/HGb_roofenv)의 윗면만 토막별로 잘라 depth 만큼 법선 반대로 내린 것
    static Mesh HGShell(int seg, float depth, string name)
    {
        var V = new List<Vector3>(); var N = new List<Vector3>(); var T = new List<int>();
        foreach (var ch in new[] { "GJJ_HGa", "GJJ_HGb" })
        {
            var env = PalaceLodImport.EnvMesh(CX, ch); if (env == null) continue;
            var v = env.vertices; var n = env.normals; var t = env.triangles; var map = new Dictionary<int, int>();
            for (int i = 0; i < t.Length; i += 3)
            {
                var a = v[t[i]]; var b = v[t[i + 1]]; var c = v[t[i + 2]];
                var fn = Vector3.Cross(b - a, c - a); if (fn.sqrMagnitude <= 0 || fn.normalized.y <= -0.2f) continue;
                if (HGSegOf((a + b + c) / 3f) != seg) continue;
                for (int k = 0; k < 3; k++)
                {
                    int vi = t[i + k], ni;
                    if (!map.TryGetValue(vi, out ni)) { ni = V.Count; map[vi] = ni; var nn = n.Length > 0 ? n[vi] : Vector3.up; V.Add(v[vi] - nn * depth); N.Add(nn); }
                    T.Add(ni);
                }
            }
        }
        var m = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
        m.SetVertices(V); m.SetNormals(N); m.SetTriangles(T, 0); m.RecalculateBounds();
        return m;
    }

    // 메시에서 같은 이름 재질의 서브메시를 뺀 사본(행각 먼 LOD 의 기와 빼기)
    static Mesh DropSubmeshes(Mesh src, Material[] mats, Func<string, bool> drop, out Material[] outMats, out int dropped)
    {
        var keep = new List<int>(); dropped = 0;
        for (int s = 0; s < src.subMeshCount; s++) { var mn = s < mats.Length && mats[s] != null ? mats[s].name : ""; if (drop(mn)) dropped += (int)src.GetIndexCount(s) / 3; else keep.Add(s); }
        var m = new Mesh { name = src.name, indexFormat = IndexFormat.UInt32 };
        m.vertices = src.vertices; m.normals = src.normals; m.uv = src.uv; if (src.tangents.Length > 0) m.tangents = src.tangents;
        m.subMeshCount = keep.Count;
        for (int i = 0; i < keep.Count; i++) m.SetTriangles(src.GetTriangles(keep[i]), i, false);
        m.RecalculateBounds();
        outMats = keep.Select(s => mats[s]).ToArray();
        return m;
    }

    // 행각 토막(검은 지붕판 버전): LOD0 = 줄인 행각 + 받침판(0.25 m 아래, 그림자 끔) /
    // LOD1·LOD2 = 줄인 행각에서 기와(MI_GJJ_HG_Roof01)를 빼고 검은 지붕판(구운 껍데기 윗면) — 사정전·강녕전처럼 멀리서는 기와가 단색 면(사용자 지시).
    // 예전(BuildHGSegments)엔 LOD1·2 기와도 줄인 메시라 멀리서 울통불통·가시처럼 보였음.
    public static string BuildHGSegmentsBlack(int from, int to, float h0, float h1, float cull)
    {
        Ensure();
        var path = Simp + "/GJ_HG_Segments_LOD.prefab";
        bool loaded = File.Exists(path);
        var root = loaded ? PrefabUtility.LoadPrefabContents(path) : new GameObject("GJ_HG_Segments_LOD");
        var log = "";
        try
        {
            for (int k = from; k <= to; k++)
            {
                var sn = "HG_Seg" + k.ToString("00");
                var old = root.transform.Find(sn); if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                var sg = new GameObject(sn); sg.transform.SetParent(root.transform, false);
                var ms = new List<Mesh>(); var lods = new List<LOD>();
                float[] hs = { h0, h1, cull };
                var back = HGShell(k, 0.25f, sn + "_RoofBacking"); var shell = HGShell(k, 0.02f, sn + "_RoofBlack");
                ms.Add(back); ms.Add(shell);
                for (int L = 0; L < 3; L++)
                {
                    var o = "GJ_" + sn + "_LOD" + L; Material[] mats; var m = Load(o, ms, out mats);
                    var lg0 = new GameObject("LOD" + L); lg0.transform.SetParent(sg.transform, false);
                    if (L == 0)
                    {
                        PalaceLodImport.AddRenderer(lg0.transform, o, m, mats, true);
                        PalaceLodImport.AddRenderer(lg0.transform, sn + "_RoofBacking", back, new[] { PalaceLodImport.BlackRoof() }).GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                        log += o + "=" + Tris(m) + " ";
                    }
                    else
                    {
                        Material[] km; int dr; var mk = DropSubmeshes(m, mats, n => n == "MI_GJJ_HG_Roof01", out km, out dr);
                        ms.Remove(m); ms.Add(mk);
                        PalaceLodImport.AddRenderer(lg0.transform, o, mk, km, false);
                        PalaceLodImport.AddRenderer(lg0.transform, sn + "_RoofBlack", shell, new[] { PalaceLodImport.BlackRoof() });
                        log += o + "=" + (Tris(mk)) + "+black" + (shell.triangles.Length / 3) + "(-tile " + dr + ") ";
                    }
                    lods.Add(MakeLod(hs[L], lg0));
                }
                var lg = sg.AddComponent<LODGroup>(); lg.SetLODs(lods.ToArray());
                lg.RecalculateBounds(); lg.fadeMode = LODFadeMode.CrossFade; lg.animateCrossFading = true;
                SaveMeshes(ms, Simp + "/GJ_" + sn + ".asset");
                log += "; ";
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
            return log;
        }
        finally { if (loaded) PrefabUtility.UnloadPrefabContents(root); else UnityEngine.Object.DestroyImmediate(root); }
    }

    [Serializable] public class DCItem { public string m; public float[] v; public float[] uv; }
    [Serializable] public class DCList { public DCItem[] items; }

    // 여는 문짝을 앞뒤 판으로: <cx>_Doors.prefab 의 문짝 메시를 Blender 판(GJ_DoorCards.json, 단지 좌표) 으로 바꾼 사본 <cx>_DoorsCard.prefab
    public static string BuildDoorsCard()
    {
        Ensure();
        var dl = JsonUtility.FromJson<DCList>(File.ReadAllText(Path.Combine(ObjDir, "GJ_DoorCards.json")));
        var root = PrefabUtility.LoadPrefabContents(Dir + "/" + CX + "_Doors.prefab");
        var ms = new List<Mesh>(); int done = 0, miss = 0; var log = "";
        try
        {
            var used = new HashSet<int>();
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
                var mn = mf.sharedMesh.name; var cm = "GJCARD_" + mn;
                if (!dl.items.Any(x => x.m == cm)) continue;
                var M = root.transform.worldToLocalMatrix * r.transform.localToWorldMatrix;
                var b = GeometryUtility.CalculateBounds(mf.sharedMesh.vertices, M); b.Expand(0.4f);   // Expand 는 크기를 늘림(한쪽 0.2 m) — 판 앞뒤가 문짝 두께 끝에 있어 여유가 필요
                var inv = M.inverse;
                var V = new List<Vector3>(); var U = new List<Vector2>(); var T = new List<int>();
                for (int i = 0; i < dl.items.Length; i++)
                {
                    var it = dl.items[i]; if (it.m != cm || used.Contains(i)) continue;
                    var c = Vector3.zero; int n = it.v.Length / 3;
                    for (int j = 0; j < n; j++) c += new Vector3(it.v[j * 3], it.v[j * 3 + 1], it.v[j * 3 + 2]);
                    c /= n; if (!b.Contains(c)) continue;
                    used.Add(i); int bse = V.Count;
                    for (int j = 0; j < n; j++) { V.Add(inv.MultiplyPoint3x4(new Vector3(it.v[j * 3], it.v[j * 3 + 1], it.v[j * 3 + 2]))); U.Add(new Vector2(it.uv[j * 2], it.uv[j * 2 + 1])); }
                    for (int j = 1; j + 1 < n; j++) { T.Add(bse); T.Add(bse + j); T.Add(bse + j + 1); }
                }
                if (V.Count == 0) { miss++; log += "miss:" + r.name + " "; continue; }
                var mesh = new Mesh { name = r.name + "_Card" }; mesh.SetVertices(V); mesh.SetUVs(0, U); mesh.SetTriangles(T, 0);
                mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.RecalculateTangents(); ms.Add(mesh);
                mf.sharedMesh = mesh;
                var mat = PalaceLodImport.FindMat(cm);
                r.sharedMaterials = new[] { mat };
                var mc = r.GetComponent<MeshCollider>(); if (mc) mc.sharedMesh = mesh;
                done++;
            }
            var mp = Simp + "/Cards/" + CX + "_DoorCards.asset";
            if (File.Exists(mp)) AssetDatabase.DeleteAsset(mp);
            AssetDatabase.CreateAsset(ms[0], mp); for (int i = 1; i < ms.Count; i++) AssetDatabase.AddObjectToAsset(ms[i], mp);
            AssetDatabase.SaveAssets();
            PrefabUtility.SaveAsPrefabAsset(root, Dir + "/" + CX + "_DoorsCard.prefab");
            long tris = 0; foreach (var f in root.GetComponentsInChildren<MeshFilter>(true)) if (f.sharedMesh) tris += f.sharedMesh.triangles.Length / 3;
            return "cards=" + done + " miss=" + miss + " itemsUsed=" + used.Count + "/" + dl.items.Length + " prefabTris=" + tris + " " + log;
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // 조립: 문짝(판) + 건물 LOD 프리팹들 + 행각 토막 + 내부
    public static string Assemble(string[] prefabs)
    {
        var root = new GameObject(CX + "_Simplified");
        try
        {
            var dp = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + "/" + CX + "_DoorsCard.prefab");
            if (dp) ((GameObject)PrefabUtility.InstantiatePrefab(dp)).transform.SetParent(root.transform, false);
            int n = 0;
            foreach (var p in prefabs)
            {
                var bp = AssetDatabase.LoadAssetAtPath<GameObject>(Simp + "/" + p + ".prefab");
                if (bp) { ((GameObject)PrefabUtility.InstantiatePrefab(bp)).transform.SetParent(root.transform, false); n++; }
            }
            PrefabUtility.SaveAsPrefabAsset(root, Dir + "/" + CX + "_Simplified.prefab");
            long tris = 0; foreach (var f in root.GetComponentsInChildren<MeshFilter>(true)) if (f.sharedMesh) tris += f.sharedMesh.triangles.Length / 3;
            return "assembled doors=" + (dp != null) + " prefabs=" + n + "/" + prefabs.Length + " allTris(all LODs)=" + tris;
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
