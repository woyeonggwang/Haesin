// Blender 에서 줄인 경복궁 건물(OBJ)을 유니티 메시로 가져와 건물마다 LODGroup 으로 조립하는 도구 — MCP 에서 정적 메서드로 호출.
// OBJ 는 Blender 에서 forward -Z / up Y 로 내보낸 것(ExportForEdit 와 같은 규칙: Unity x = -OBJ x, 면 순서 뒤집음).
// 머티리얼 이름 끝 "_1S" = 한쪽 면만 그림(PalaceBlenderRoundTrip.ShellMat), ".001" 같은 Blender 꼬리는 뗀다.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class PalaceLodImport
{
    const string GRoot = "Assets/02.Resources/01.Asset/Models/Gyeongbokgung";
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static Dictionary<string, Material> matCache = new Dictionary<string, Material>();

    public static Material FindMat(string objMat)
    {
        Material m;
        if (matCache.TryGetValue(objMat, out m) && m != null) return m;
        var n = Regex.Replace(objMat, @"\.\d{3}", "");
        bool one = n.EndsWith("_1S"); if (one) n = n.Substring(0, n.Length - 3);
        Material b = null;
        foreach (var g in AssetDatabase.FindAssets(n + " t:Material", new[] { GRoot }))
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            if (Path.GetFileNameWithoutExtension(p) == n && !p.Contains("/ShellMaterials/")) { b = AssetDatabase.LoadAssetAtPath<Material>(p); break; }
        }
        // 외부에서 구운 덩어리 아틀라스(OBJ 재질 이름 ATLAS_<덩어리>_<종류>)는 유니티에선 MAT_<덩어리>_<종류>
        if (b == null && n.StartsWith("ATLAS_"))
        {
            foreach (var g in AssetDatabase.FindAssets("MAT_" + n.Substring(6) + " t:Material", new[] { PalaceBakeTool.BakedAssetRoot }))
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                if (Path.GetFileNameWithoutExtension(p) == "MAT_" + n.Substring(6)) { b = AssetDatabase.LoadAssetAtPath<Material>(p); break; }
            }
        }
        if (b == null) Debug.LogWarning("[PalaceLodImport] material not found: " + objMat);
        m = (one && b != null) ? PalaceBlenderRoundTrip.ShellMat(b) : b;
        matCache[objMat] = m;
        return m;
    }

    // OBJ 파일 하나(여러 오브젝트 가능)를 메시 하나로(머티리얼마다 서브메시). 좌표는 경복궁 단지 프리팹 루트 기준.
    public static Mesh LoadObj(string path, string meshName, out Material[] mats)
    {
        var V = new List<Vector3>(); var VT = new List<Vector2>(); var VN = new List<Vector3>();
        var groups = new Dictionary<string, List<int[]>>(); var order = new List<string>(); List<int[]> cur = null;
        foreach (var raw in File.ReadLines(path))
        {
            if (raw.Length < 2) continue;
            char c0 = raw[0], c1 = raw[1];
            if (c0 == 'v' && c1 == ' ') { var a = raw.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries); V.Add(new Vector3(-float.Parse(a[1], Inv), float.Parse(a[2], Inv), float.Parse(a[3], Inv))); }
            else if (c0 == 'v' && c1 == 't') { var a = raw.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries); VT.Add(new Vector2(float.Parse(a[1], Inv), float.Parse(a[2], Inv))); }
            else if (c0 == 'v' && c1 == 'n') { var a = raw.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries); VN.Add(new Vector3(-float.Parse(a[1], Inv), float.Parse(a[2], Inv), float.Parse(a[3], Inv))); }
            else if (raw.StartsWith("usemtl "))
            {
                var k = raw.Substring(7).Trim();
                if (!groups.TryGetValue(k, out cur)) { cur = new List<int[]>(); groups[k] = cur; order.Add(k); }
            }
            else if (c0 == 'f' && c1 == ' ')
            {
                if (cur == null) { cur = new List<int[]>(); groups["none"] = cur; order.Add("none"); }
                var a = raw.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var poly = new List<int[]>();
                for (int i = 1; i < a.Length; i++)
                {
                    var q = a[i].Split('/'); var t = new int[3];
                    t[0] = int.Parse(q[0]) - 1;
                    t[1] = q.Length > 1 && q[1].Length > 0 ? int.Parse(q[1]) - 1 : -1;
                    t[2] = q.Length > 2 && q[2].Length > 0 ? int.Parse(q[2]) - 1 : -1;
                    poly.Add(t);
                }
                for (int i = 1; i + 1 < poly.Count; i++) { cur.Add(poly[0]); cur.Add(poly[i + 1]); cur.Add(poly[i]); }   // x 를 뒤집었으니 면 순서도 뒤집음
            }
        }
        var vmap = new Dictionary<long, int>(); var pv = new List<Vector3>(); var pn = new List<Vector3>(); var pu = new List<Vector2>();
        var subs = new List<List<int>>(); var ml = new List<Material>();
        foreach (var k in order)
        {
            var tri = groups[k]; if (tri.Count == 0) continue;
            var mat = FindMat(k);
            int si = ml.IndexOf(mat); if (si < 0 || mat == null) { ml.Add(mat); subs.Add(new List<int>()); si = ml.Count - 1; }
            foreach (var q in tri)
            {
                long key = ((long)q[0] << 42) ^ ((long)(q[1] + 1) << 21) ^ (long)(q[2] + 1); int vi;
                if (!vmap.TryGetValue(key, out vi))
                {
                    vi = pv.Count; vmap[key] = vi; pv.Add(V[q[0]]);
                    pn.Add(q[2] >= 0 ? VN[q[2]] : Vector3.up); pu.Add(q[1] >= 0 ? VT[q[1]] : Vector2.zero);
                }
                subs[si].Add(vi);
            }
        }
        var mesh = new Mesh { name = meshName, indexFormat = IndexFormat.UInt32 };
        mesh.SetVertices(pv); mesh.SetNormals(pn); mesh.SetUVs(0, pu); mesh.subMeshCount = subs.Count;
        for (int s = 0; s < subs.Count; s++) mesh.SetTriangles(subs[s], s, false);
        mesh.RecalculateBounds(); mesh.RecalculateTangents();
        mats = ml.ToArray();
        return mesh;
    }

    // 지붕 받침판: 구운 지붕 껍데기(roofenv)의 윗면만 남겨 법선 반대로 내린 사본(기와 틈으로 속이 비치지 않게).
    // 머티리얼은 어두운 단색(BlackRoof) — 예전엔 roofenv 구운 텍스처였는데 지붕 끝에 처마 단청(초록)이 구워져 있어 기와 틈으로 비쳤음
    public static Mesh MakeBacking(Mesh env, float depth, string name)
    {
        var v = env.vertices; var n = env.normals; var uv = env.uv; var t = env.triangles;
        var keep = new List<int>();
        for (int i = 0; i < t.Length; i += 3)
        {
            var fn = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
            if (fn.sqrMagnitude > 0 && fn.normalized.y > -0.2f) { keep.Add(t[i]); keep.Add(t[i + 1]); keep.Add(t[i + 2]); }
        }
        var nv = new Vector3[v.Length];
        for (int i = 0; i < v.Length; i++) nv[i] = v[i] - (n.Length > 0 ? n[i] : Vector3.up) * depth;
        var m = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
        m.vertices = nv; if (n.Length > 0) m.normals = n; if (uv.Length > 0) m.uv = uv;
        m.SetTriangles(keep, 0); m.RecalculateBounds(); m.RecalculateTangents();
        return m;
    }

    public static GameObject AddRenderer(Transform parent, string name, Mesh mesh, Material[] mats, bool collider = false)
    {
        var g = new GameObject(name); g.transform.SetParent(parent, false);
        g.AddComponent<MeshFilter>().sharedMesh = mesh;
        g.AddComponent<MeshRenderer>().sharedMaterials = mats;
        if (collider) g.AddComponent<MeshCollider>().sharedMesh = mesh;
        return g;
    }

    public static Mesh EnvMesh(string cx, string chunk)
    {
        return AssetDatabase.LoadAllAssetsAtPath(PalaceBakeTool.BakedAssetRoot + "/" + cx + "/" + chunk + "_roofenv.fbx").OfType<Mesh>().FirstOrDefault();
    }

    // 구운 덩어리의 지붕이 꺼져 있으면(사정전처럼 지붕을 RoofLOD 로 맡긴 단지) 구운 지붕 껍데기(없으면 평면)를 대신 넣음
    static void AddChunkRoofIfOff(string cx, string ch, GameObject ci, Transform parent)
    {
        var rr = ci.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.name == ch + "_roof");
        if (rr != null && rr.enabled && rr.gameObject.activeSelf) return;
        var dir = PalaceBakeTool.BakedAssetRoot + "/" + cx + "/";
        foreach (var kind in new[] { "roofenv", "roofflat" })
        {
            var m = AssetDatabase.LoadAllAssetsAtPath(dir + ch + "_" + kind + ".fbx").OfType<Mesh>().FirstOrDefault();
            var mat = AssetDatabase.LoadAssetAtPath<Material>(dir + "MAT_" + ch + "_" + kind + ".mat");
            if (m == null || mat == null) continue;
            AddRenderer(parent, ch + "_" + kind, m, new[] { mat });
            return;
        }
    }
    public static string LodDir = @"D:\Blender\Enviroment\PalaceJoseon\강녕전_LOD";
    static string SimpDir(string cx) { return PalaceBakeTool.BakedAssetRoot + "/" + cx + "/Simplified"; }
    static void EnsureDir(string cx) { if (!AssetDatabase.IsValidFolder(SimpDir(cx))) AssetDatabase.CreateFolder(PalaceBakeTool.BakedAssetRoot + "/" + cx, "Simplified"); }
    static void SaveMeshes(List<Mesh> ms, string path)
    {
        foreach (var m in ms) PalaceMeshCompact.CompactMesh(m);
        if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(ms[0], path);
        for (int i = 1; i < ms.Count; i++) AssetDatabase.AddObjectToAsset(ms[i], path);
        AssetDatabase.SaveAssets();
    }
    static GameObject Chunk(string cx, string chunk, Transform parent)
    {
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>(PalaceBakeTool.BakedAssetRoot + "/" + cx + "/" + chunk + "_Baked.prefab");
        var g = (GameObject)PrefabUtility.InstantiatePrefab(pf); g.transform.SetParent(parent, false); return g;
    }
    static Material ChunkRoofMat(string cx, string chunk)
    {
        return AssetDatabase.LoadAssetAtPath<Material>(PalaceBakeTool.BakedAssetRoot + "/" + cx + "/MAT_" + chunk + "_roofenv.mat");
    }
    static LOD MakeLod(float h, GameObject g) { return new LOD(h, g.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && r.gameObject.activeSelf).ToArray()); }

    // 건물 하나: LOD0 = Blender 에서 줄인 메시 + 지붕 받침판, LOD1 = 예전에 구운 덩어리(들)
    public static string BuildBuilding(string cx, string objName, string[] chunks, float lod0h, float cullh, float backDepth = 0.25f)
    {
        EnsureDir(cx);
        var root = new GameObject(objName + "_LOD");
        try
        {
            Material[] mats; var ms = new List<Mesh>();
            var mesh = LoadObj(Path.Combine(LodDir, objName + ".obj"), objName, out mats); ms.Add(mesh);
            var l0 = new GameObject("LOD0"); l0.transform.SetParent(root.transform, false);
            AddRenderer(l0.transform, objName, mesh, mats, true);
            foreach (var ch in chunks)
            {
                var env = EnvMesh(cx, ch); if (env == null) continue;
                var bk = MakeBacking(env, backDepth, ch + "_RoofBacking"); ms.Add(bk);
                var br = AddRenderer(l0.transform, ch + "_RoofBacking", bk, new[] { BlackRoof() });
                br.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            var l1 = new GameObject("LOD1"); l1.transform.SetParent(root.transform, false);
            foreach (var ch in chunks) { var ci = Chunk(cx, ch, l1.transform); AddChunkRoofIfOff(cx, ch, ci, l1.transform); }
            var lg = root.AddComponent<LODGroup>();
            lg.SetLODs(new[] { MakeLod(lod0h, l0), MakeLod(cullh, l1) });
            lg.RecalculateBounds(); lg.fadeMode = LODFadeMode.CrossFade; lg.animateCrossFading = true;
            SaveMeshes(ms, SimpDir(cx) + "/" + objName + ".asset");
            PrefabUtility.SaveAsPrefabAsset(root, SimpDir(cx) + "/" + objName + "_LOD.prefab");
            return objName + " tris=" + (mesh.triangles.Length / 3) + " mats=" + mats.Length + " null=" + mats.Count(x => x == null) + " size=" + lg.size.ToString("F1");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    // 강녕전 본채: LOD0 복제본 / LOD1 구운 몸체 + 구운 지붕 윗면 / LOD2 줄인 몸체 + 검은 판 + 흰 양성 선
    public static string BuildMainHall(string cx, string lod0Obj, string chunk, float h0, float h1, float cull)
    {
        EnsureDir(cx);
        var root = new GameObject(lod0Obj + "_LOD");
        try
        {
            var ms = new List<Mesh>(); Material[] mats;
            var l0 = new GameObject("LOD0"); l0.transform.SetParent(root.transform, false);
            var m0 = LoadObj(Path.Combine(LodDir, lod0Obj + ".obj"), lod0Obj, out mats); ms.Add(m0);
            AddRenderer(l0.transform, lod0Obj, m0, mats, true);
            var env = EnvMesh(cx, chunk);
            if (env != null) { var bk = MakeBacking(env, 0.25f, chunk + "_RoofBacking"); ms.Add(bk); AddRenderer(l0.transform, chunk + "_RoofBacking", bk, new[] { BlackRoof() }).GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off; }
            var l1 = new GameObject("LOD1"); l1.transform.SetParent(root.transform, false);
            var ci = Chunk(cx, chunk, l1.transform);
            foreach (var r in ci.GetComponentsInChildren<MeshRenderer>(true)) if (r.name.EndsWith("_roof")) r.gameObject.SetActive(false);
            var mr = LoadObj(Path.Combine(LodDir, "GNJ_LOD1_Roof.obj"), "GNJ_LOD1_Roof", out mats); ms.Add(mr);
            AddRenderer(l1.transform, "GNJ_LOD1_Roof", mr, new[] { ChunkRoofMat(cx, chunk) });
            var l2 = new GameObject("LOD2"); l2.transform.SetParent(root.transform, false);
            var mb = LoadObj(Path.Combine(LodDir, "GNJ_LOD2_Body.obj"), "GNJ_LOD2_Body", out mats); ms.Add(mb); AddRenderer(l2.transform, "GNJ_LOD2_Body", mb, mats);
            var mp = LoadObj(Path.Combine(LodDir, "GNJ_LOD2_RoofPlate.obj"), "GNJ_LOD2_RoofPlate", out mats); ms.Add(mp); AddRenderer(l2.transform, "GNJ_LOD2_RoofPlate", mp, new[] { BlackRoof() });
            var mw = LoadObj(Path.Combine(LodDir, "GNJ_WhiteRidge.obj"), "GNJ_WhiteRidge", out mats); ms.Add(mw); AddRenderer(l2.transform, "GNJ_WhiteRidge", mw, mats);
            var lg = root.AddComponent<LODGroup>();
            lg.SetLODs(new[] { MakeLod(h0, l0), MakeLod(h1, l1), MakeLod(cull, l2) });
            lg.RecalculateBounds(); lg.fadeMode = LODFadeMode.CrossFade; lg.animateCrossFading = true;
            SaveMeshes(ms, SimpDir(cx) + "/" + lod0Obj + ".asset");
            PrefabUtility.SaveAsPrefabAsset(root, SimpDir(cx) + "/" + lod0Obj + "_LOD.prefab");
            return "main tris lod0=" + (m0.triangles.Length / 3) + " lod1roof=" + (mr.triangles.Length / 3) + " lod2=" + (mb.triangles.Length / 3 + mp.triangles.Length / 3 + mw.triangles.Length / 3) + " null0=" + m0.subMeshCount + " size=" + lg.size.ToString("F1");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    // 본채(덩어리 여러 개): LOD0 단순화본+받침판 / LOD1 구운 몸체 + 구운 지붕 평면(roofflat) / LOD2 줄인 몸체 + 검은 판 + 흰 양성 선(<pfx>_LOD2_Body/RoofPlate, <pfx>_WhiteRidge)
    public static string BuildMainHallMulti(string cx, string lod0Obj, string[] chunks, string pfx, float h0, float h1, float cull)
    {
        EnsureDir(cx);
        var root = new GameObject(lod0Obj + "_LOD");
        try
        {
            var ms = new List<Mesh>(); Material[] mats; var dir = PalaceBakeTool.BakedAssetRoot + "/" + cx + "/";
            var l0 = new GameObject("LOD0"); l0.transform.SetParent(root.transform, false);
            var m0 = LoadObj(Path.Combine(LodDir, lod0Obj + ".obj"), lod0Obj, out mats); ms.Add(m0);
            AddRenderer(l0.transform, lod0Obj, m0, mats, true);
            foreach (var ch in chunks)
            {
                var env = EnvMesh(cx, ch); if (env == null) continue;
                var bk = MakeBacking(env, 0.25f, ch + "_RoofBacking"); ms.Add(bk);
                AddRenderer(l0.transform, ch + "_RoofBacking", bk, new[] { BlackRoof() }).GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            var l1 = new GameObject("LOD1"); l1.transform.SetParent(root.transform, false);
            foreach (var ch in chunks)
            {
                var ci = Chunk(cx, ch, l1.transform);
                foreach (var r in ci.GetComponentsInChildren<MeshRenderer>(true)) if (r.name.EndsWith("_roof")) r.gameObject.SetActive(false);
                var fm = AssetDatabase.LoadAllAssetsAtPath(dir + ch + "_roofflat.fbx").OfType<Mesh>().FirstOrDefault();
                var fmat = AssetDatabase.LoadAssetAtPath<Material>(dir + "MAT_" + ch + "_roofflat.mat");
                if (fm != null && fmat != null) AddRenderer(l1.transform, ch + "_roofflat", fm, new[] { fmat });
                else AddChunkRoofIfOff(cx, ch, ci, l1.transform);
            }
            var l2 = new GameObject("LOD2"); l2.transform.SetParent(root.transform, false);
            var mb = LoadObj(Path.Combine(LodDir, pfx + "_LOD2_Body.obj"), pfx + "_LOD2_Body", out mats); ms.Add(mb); AddRenderer(l2.transform, pfx + "_LOD2_Body", mb, mats);
            var mp = LoadObj(Path.Combine(LodDir, pfx + "_LOD2_RoofPlate.obj"), pfx + "_LOD2_RoofPlate", out mats); ms.Add(mp); AddRenderer(l2.transform, pfx + "_LOD2_RoofPlate", mp, new[] { BlackRoof() });
            var mw = LoadObj(Path.Combine(LodDir, pfx + "_WhiteRidge.obj"), pfx + "_WhiteRidge", out mats); ms.Add(mw); AddRenderer(l2.transform, pfx + "_WhiteRidge", mw, mats);
            var lg = root.AddComponent<LODGroup>();
            lg.SetLODs(new[] { MakeLod(h0, l0), MakeLod(h1, l1), MakeLod(cull, l2) });
            lg.RecalculateBounds(); lg.fadeMode = LODFadeMode.CrossFade; lg.animateCrossFading = true;
            SaveMeshes(ms, SimpDir(cx) + "/" + lod0Obj + ".asset");
            PrefabUtility.SaveAsPrefabAsset(root, SimpDir(cx) + "/" + lod0Obj + "_LOD.prefab");
            return "main tris lod0=" + (m0.triangles.Length / 3) + " lod2=" + (mb.triangles.Length / 3 + mp.triangles.Length / 3 + mw.triangles.Length / 3) + " size=" + lg.size.ToString("F1");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    // 단지: 바닥·기타(원본에서 SM_ 코드 없는 것과 흙바닥) + 문짝 + 건물 LOD 프리팹들
    public static string AssembleSimplified(string cx, string[] buildingPrefabs, string[] keepMeshPrefixes)
    {
        var root = new GameObject(cx + "_Simplified");
        try
        {
            var og = PrefabUtility.LoadPrefabContents(PalaceBakeTool.BakedAssetRoot + "/" + cx + "/" + cx + "_Original.prefab");
            var ground = new GameObject("Ground"); ground.transform.SetParent(root.transform, false); int gk = 0;
            try
            {
                foreach (var r in og.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null || !r.enabled) continue;
                    var mn = mf.sharedMesh.name;
                    bool keep = !mn.StartsWith("SM_") || keepMeshPrefixes.Any(p => mn.StartsWith(p));
                    // 예전 방 속 비우기·창호지 작업의 한지 막(ShellSkin, WindowPaper)은 단순화본에선 기둥 줄에 흰 띠로 보여 뺌
                    if (r.name == "ShellSkin" || r.name == "WindowPaper") keep = false;
                    if (!keep) continue;
                    var g = new GameObject(r.name); g.transform.SetParent(ground.transform, false);
                    var M = og.transform.worldToLocalMatrix * r.transform.localToWorldMatrix;
                    g.transform.localPosition = M.GetColumn(3); g.transform.localRotation = M.rotation; g.transform.localScale = M.lossyScale;
                    g.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh; var nr = g.AddComponent<MeshRenderer>(); nr.sharedMaterials = r.sharedMaterials; nr.shadowCastingMode = r.shadowCastingMode;
                    var mc = r.GetComponent<MeshCollider>(); if (mc) g.AddComponent<MeshCollider>().sharedMesh = mc.sharedMesh;
                    gk++;
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(og); }
            // 문짝을 판(카드)으로 바꾼 <cx>_DoorsCard 가 있으면 그것을 씀(사정전 — 원본 문짝은 한 짝에 삼각형 수천~1만)
            var dp = AssetDatabase.LoadAssetAtPath<GameObject>(PalaceBakeTool.BakedAssetRoot + "/" + cx + "/" + cx + "_DoorsCard.prefab");
            if (dp == null) dp = AssetDatabase.LoadAssetAtPath<GameObject>(PalaceBakeTool.BakedAssetRoot + "/" + cx + "/" + cx + "_Doors.prefab");
            if (dp) ((GameObject)PrefabUtility.InstantiatePrefab(dp)).transform.SetParent(root.transform, false);
            foreach (var b in buildingPrefabs)
            {
                var bp = AssetDatabase.LoadAssetAtPath<GameObject>(SimpDir(cx) + "/" + b + "_LOD.prefab");
                if (bp) ((GameObject)PrefabUtility.InstantiatePrefab(bp)).transform.SetParent(root.transform, false);
            }
            PrefabUtility.SaveAsPrefabAsset(root, PalaceBakeTool.BakedAssetRoot + "/" + cx + "/" + cx + "_Simplified.prefab");
            return "assembled ground=" + gk + " buildings=" + buildingPrefabs.Length;
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    // 단지 원본 + 지붕마다 LOD(RoofLOD) 의 LOD0 기와를 건물 코드별 OBJ 로(ExportForEdit 와 같은 형식). 기와 칸은 위치로 건물을 정함.
    // (사정전처럼 원본에서 기와를 RoofLOD 로 옮긴 단지용. "_Shell" 재질은 원래 이름으로 씀)
    public static string ExportCodes(string cx, string[] codes)
    {
        var og = PrefabUtility.LoadPrefabContents(PalaceBakeTool.BakedAssetRoot + "/" + cx + "/" + cx + "_Original.prefab");
        var rlPath = PalaceBakeTool.BakedAssetRoot + "/" + cx + "/" + cx + "_RoofLOD.prefab";
        GameObject rl = File.Exists(rlPath) ? PrefabUtility.LoadPrefabContents(rlPath) : null;
        var log = new System.Text.StringBuilder();
        try
        {
            Func<string, string> codeOf = mn => { if (!mn.StartsWith("SM_")) return null; var p = mn.Split('_'); if (p.Length < 3) return null; return p[2] == "HG" ? p[1] + "_HG" : p[1]; };
            var items = new Dictionary<string, List<KeyValuePair<MeshRenderer, Matrix4x4>>>(); var bb = new Dictionary<string, Bounds>();
            foreach (var c in codes) items[c] = new List<KeyValuePair<MeshRenderer, Matrix4x4>>();
            foreach (var r in og.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null || !r.enabled) continue;
                var c = codeOf(mf.sharedMesh.name); if (c == null || !items.ContainsKey(c)) continue;
                var M = og.transform.worldToLocalMatrix * r.transform.localToWorldMatrix;
                items[c].Add(new KeyValuePair<MeshRenderer, Matrix4x4>(r, M));
                var b = GeometryUtility.CalculateBounds(mf.sharedMesh.vertices, M);
                if (bb.ContainsKey(c)) { var x = bb[c]; x.Encapsulate(b); bb[c] = x; } else bb[c] = b;
            }
            int roofAdded = 0, roofOrphan = 0;
            if (rl != null)
                foreach (var lg in rl.GetComponentsInChildren<LODGroup>(true))
                    foreach (var r0 in lg.GetLODs()[0].renderers)
                    {
                        var r = r0 as MeshRenderer; if (r == null) continue; var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
                        var M = rl.transform.worldToLocalMatrix * r.transform.localToWorldMatrix;
                        var ctr = GeometryUtility.CalculateBounds(mf.sharedMesh.vertices, M).center;
                        var c = codeOf(mf.sharedMesh.name);
                        if (c == null || !items.ContainsKey(c))
                        {
                            c = null; float best = float.MaxValue;
                            foreach (var kv in bb) { var e = kv.Value; e.Expand(6f); if (!e.Contains(ctr)) continue; float d = (e.center - ctr).sqrMagnitude; if (d < best) { best = d; c = kv.Key; } }
                        }
                        if (c == null) { roofOrphan++; continue; }
                        items[c].Add(new KeyValuePair<MeshRenderer, Matrix4x4>(r, M)); roofAdded++;
                    }
            log.Append("roof lod0 added=" + roofAdded + " orphan=" + roofOrphan + "; ");
            foreach (var c in codes)
            {
                var name = cx + "_" + c; var obj = new System.Text.StringBuilder(); var mats = new Dictionary<string, Material>();
                obj.AppendLine("mtllib " + name + ".mtl"); int vb = 1, i = 0; long tris = 0;
                foreach (var kv in items[c])
                {
                    var r = kv.Key; var M = kv.Value; var m = r.GetComponent<MeshFilter>().sharedMesh; var N = M.inverse.transpose; bool mir = M.determinant < 0;
                    var v = m.vertices; var n = m.normals; var uv = m.uv;
                    obj.AppendLine("o R" + (i++) + "_" + m.name);
                    foreach (var p in v) { var w = M.MultiplyPoint3x4(p); obj.Append("v ").Append((-w.x).ToString("0.#####", Inv)).Append(' ').Append(w.y.ToString("0.#####", Inv)).Append(' ').Append(w.z.ToString("0.#####", Inv)).Append('\n'); }
                    foreach (var t in uv) obj.Append("vt ").Append(t.x.ToString("0.#####", Inv)).Append(' ').Append(t.y.ToString("0.#####", Inv)).Append('\n');
                    foreach (var q in n) { var w = N.MultiplyVector(q).normalized; obj.Append("vn ").Append((-w.x).ToString("0.####", Inv)).Append(' ').Append(w.y.ToString("0.####", Inv)).Append(' ').Append(w.z.ToString("0.####", Inv)).Append('\n'); }
                    var sm = r.sharedMaterials;
                    for (int s = 0; s < m.subMeshCount; s++)
                    {
                        var mat = s < sm.Length ? sm[s] : null; string mn = mat != null ? mat.name : "none";
                        if (mn.EndsWith("_Shell")) { mn = mn.Substring(0, mn.Length - 6); var bm = FindMat(mn); if (bm != null) mat = bm; }
                        if (mat != null) mats[mn] = mat;
                        obj.AppendLine("usemtl " + mn);
                        var idx = m.GetTriangles(s);
                        for (int k = 0; k < idx.Length; k += 3)
                        {
                            int a = idx[k] + vb, b = idx[k + 1] + vb, cc = idx[k + 2] + vb;
                            if (!mir) { int tmp = b; b = cc; cc = tmp; }
                            bool hu = uv.Length > 0, hn = n.Length > 0;
                            obj.Append("f ");
                            foreach (var ix in new[] { a, b, cc }) { obj.Append(ix); if (hu || hn) { obj.Append('/'); if (hu) obj.Append(ix); if (hn) obj.Append('/').Append(ix); } obj.Append(' '); }
                            obj.Append('\n'); tris++;
                        }
                    }
                    vb += v.Length;
                }
                var mtl = new System.Text.StringBuilder();
                foreach (var kv in mats)
                {
                    mtl.AppendLine("newmtl " + kv.Key);
                    var bc = kv.Value.HasProperty("_BaseColorMap") ? kv.Value.GetTexture("_BaseColorMap") : null;
                    if (bc != null) mtl.AppendLine("map_Kd " + Path.GetFullPath(AssetDatabase.GetAssetPath(bc)).Replace('\\', '/'));
                }
                File.WriteAllText(Path.Combine(PalaceBlenderRoundTrip.EditDir, name + ".obj"), obj.ToString());
                File.WriteAllText(Path.Combine(PalaceBlenderRoundTrip.EditDir, name + ".mtl"), mtl.ToString());
                log.Append(c + " objs=" + items[c].Count + " tris=" + tris + "; ");
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(og); if (rl != null) PrefabUtility.UnloadPrefabContents(rl); }
        return log.ToString();
    }

    public static Material BlackRoof()
    {
        var p = GRoot + "/_Shared/Materials/MAT_RoofPlate_Black.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m != null) return m;
        m = new Material(Shader.Find("HDRP/Lit")) { name = "MAT_RoofPlate_Black" };
        m.SetColor("_BaseColor", new Color(0.035f, 0.037f, 0.042f)); m.SetFloat("_Smoothness", 0.2f);
        AssetDatabase.CreateAsset(m, p);
        return m;
    }
}
