// 육조거리 관아 조립 — 궁 단지(사정전·강녕전·교태전) 단순화판을 관아 하나로 쓴 것. MCP 에서 정적 메서드로 부름.
//
// 밖(98.Blender/Impostor/yj_build.py)에서 만든 것(형식 t = A 사정전형 / B 강녕전형 / C 교태전형, 폴더 dir = …\Captures\impostor\out\YJ_<t>):
//   YJ_<t>_Front.bin  길에 가까운 앞쪽(정면 행각 + 문 + 옆 행각 앞부분) 원래 메시 하나 — 좌표 = 기준점 기준(정면 가운데·땅 높이·처마 끝)
//   YJ_<t>_Imp.bin / _Keep.bin / P<k>_BC·NOS.png  단지 통째 임포스터(오브젝트 FRONT = 앞쪽, 나머지 = 안쪽 전각·뒤 행각) — 좌표 = 단지 프리팹 루트
//   front.json        기준점(pivot)·경계·삼각형 수
// 현판(Stela 재질)·땅판은 밖에서 이미 뺐음.
//
// 조립 결과 PalaceImpostorTool.Root/Kits/YJ_<t>.prefab(빈터 묶음과 같은 폴더 — PlaceFill 로 배치):
//   Front  LODGroup: LOD0 = 앞쪽 원래 메시(가까이서 깨진 곳 없게 — 사용자 지시 2026-09-30), LOD1 = 앞쪽 임포스터. 기준점 = 앞쪽 가운데,
//          크기는 수동(frontSize) — 110 m 짜리 정면을 90 m 안에서 원래 메시로 보려면 화면 높이가 1 을 넘어 그대로는 못 정함.
//   Rest   나머지 임포스터 + Keep 을 메시 하나로 합침(기준점 기준 좌표), 화면 높이 0.002 에서 끔.
// 섞은 관아(BuildGwanaMix, 사용자 지시 "너무 획일적이라 섞어도 돼요"): 앞쪽은 한 형식, 안쪽 전각은 다른 형식의 임포스터 오브젝트를 옮겨 씀
//   (예: YJ_AB = 사정전 앞쪽·행각 + 강녕전 전각 3채, YJ_BA = 강녕전 앞쪽·행각 + 사정전 전각 3채). 배치마다 거울·늘이고 줄이기는 계획 tsv 배율로.
// 행랑 방식(2026-09-30, 지금 씀): 길가는 사정전 남행각 조각을 일자로 이어 붙인 행랑(BuildRowModules, 조각 = 두 칸 BAY·문 GATE·줄 끝 END_W/E),
//   관아는 행랑 뒤 안쪽만(BuildRest — 정면에서 12.6 m 안은 버리고 구운 임포스터). 위의 Front LODGroup 방식(BuildGwana*)은 모서리가 길에 드러나 안 씀.
// 순서: 그림을 PalaceImpostorTool.Root/YJ_<t>/IMP_YJ_<t>_P<k>_BC·NOS.png 로 → ImportFront → PalaceImpostorTool.Setup("YJ_"+t, dir, 단순화판 프리팹, 페이지 수)
//       → BuildGwana / BuildGwanaMix → PalaceImpostorTool.PlaceFill(홀더, 계획 tsv, 10)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class PalaceYukjoTool
{
    public const string Root = "Assets/02.Resources/01.Asset/Models/Gyeongbokgung/_Baked/Yukjo";
    const int Magic = 0x534D4E48;

    // 앞쪽 원래 메시 → Root/YJ_<t>/Meshes/YJ_<t>_Front.asset (시간 제한, MORE 면 다시 부름)
    public static string ImportFront(string t, string dir)
    {
        return PalaceLodBinImport.ImportMeshes(dir, Root + "/YJ_" + t, 20f, "", "YJ_" + t + "_Front.bin", "Meshes", 10);
    }

    // HNMS 메시 이름 → 서브메시 재질 이름
    static Dictionary<string, List<string>> ReadSubMats(string path)
    {
        var res = new Dictionary<string, List<string>>();
        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
        using (var br = new BinaryReader(fs))
        {
            if (br.ReadInt32() != Magic) throw new Exception("매직이 다름: " + path);
            int n = br.ReadInt32();
            for (int k = 0; k < n; k++)
            {
                string name = Encoding.UTF8.GetString(br.ReadBytes(br.ReadInt32()));
                int nv = br.ReadInt32(), ns = br.ReadInt32();
                fs.Seek((long)nv * 32, SeekOrigin.Current);
                var ms = new List<string>();
                for (int s = 0; s < ns; s++) { ms.Add(Encoding.UTF8.GetString(br.ReadBytes(br.ReadInt32()))); int ni = br.ReadInt32(); fs.Seek((long)ni * 4, SeekOrigin.Current); }
                res[name] = ms;
            }
        }
        return res;
    }

    static Dictionary<string, Material> MatsOf(string prefabPath, Dictionary<string, Material> into = null)
    {
        var d = into ?? new Dictionary<string, Material>();
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (go == null) throw new Exception("프리팹 없음: " + prefabPath);
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            foreach (var m in r.sharedMaterials) if (m != null && !d.ContainsKey(m.name)) d[m.name] = m;
        return d;
    }

    // 관아 프리팹 조립. (px, py, pz) = 기준점(단지 좌표), frontDist = 앞쪽을 원래 메시로 보는 거리(m, 앞쪽 가운데에서),
    // frontSize = LODGroup 수동 크기(m) — 화면 높이 = frontSize / (거리 × 2·tan30°)
    public static string BuildGwana(string t, string dir, string simplifiedPrefab, float px, float py, float pz, float frontDist = 90f, float frontSize = 40f)
    {
        return BuildGwanaMix("YJ_" + t, t, dir, simplifiedPrefab, px, py, pz, "", frontDist, frontSize);
    }

    // 기준점(front.json 의 pivot) — 형식 t 의 단지 좌표
    static Vector3 PivotOf(string t)
    {
        var js = File.ReadAllText(Path.Combine(OutDir, "YJ_" + t, "front.json"));
        int a = js.IndexOf("\"pivot\""); int b = js.IndexOf('[', a), c = js.IndexOf(']', b);
        var v = js.Substring(b + 1, c - b - 1).Split(',').Select(x => float.Parse(x.Trim(), System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        return new Vector3(v[0], v[1], v[2]);
    }
    public static string OutDir = "D:/Unity/Haesin/Captures/impostor/out";

    // 섞은 관아(사용자 지시 2026-09-30 "너무 획일적이라 섞어도 돼요"): 앞쪽은 형식 t, 안쪽은 restSpec 으로 고름.
    // restSpec = "형식:오브젝트:dx:dz;…" — 형식 YJ_<형식>_Impostor 의 오브젝트(Keep 은 "Keep/<이름>_Keep")를 그 형식 기준점 기준으로 옮긴 뒤 (dx, dz) 더함.
    // 비우면 형식 t 의 FRONT 뺀 전부. 결과 Kits/<kitName>.prefab
    public static string BuildGwanaMix(string kitName, string t, string dir, string simplifiedPrefab, float px, float py, float pz, string restSpec, float frontDist = 90f, float frontSize = 40f)
    {
        string cx = "YJ_" + t;
        var pivot = new Vector3(px, py, pz);
        var mats = MatsOf(simplifiedPrefab);
        var front = AssetDatabase.LoadAssetAtPath<Mesh>(Root + "/" + cx + "/Meshes/" + cx + "_Front.asset");
        if (front == null) return "앞쪽 메시 없음";
        var fmats = ReadSubMats(Path.Combine(dir, cx + "_Front.bin"))[cx + "_Front"];
        var missing = new HashSet<string>();
        var fm = fmats.Select(n => { Material m; if (!mats.TryGetValue(n, out m)) { missing.Add(n); return null; } return m; }).ToArray();
        var imp = AssetDatabase.LoadAssetAtPath<GameObject>(PalaceImpostorTool.Root + "/" + cx + "/" + cx + "_Impostor.prefab");
        if (imp == null) return "임포스터 프리팹 없음";

        var go = new GameObject(kitName);
        // Front
        var fgo = new GameObject("Front"); fgo.transform.SetParent(go.transform, false);
        var f0 = new GameObject("Front_LOD0"); f0.transform.SetParent(fgo.transform, false);
        f0.AddComponent<MeshFilter>().sharedMesh = front;
        var r0 = f0.AddComponent<MeshRenderer>(); r0.sharedMaterials = fm; r0.shadowCastingMode = ShadowCastingMode.On;
        var fi = imp.transform.Find("FRONT");
        Renderer r1 = null;
        if (fi != null)
        {
            var f1 = new GameObject("Front_LOD1"); f1.transform.SetParent(fgo.transform, false);
            f1.transform.localPosition = -pivot;
            f1.AddComponent<MeshFilter>().sharedMesh = fi.GetComponent<MeshFilter>().sharedMesh;
            r1 = f1.AddComponent<MeshRenderer>(); r1.sharedMaterials = fi.GetComponent<MeshRenderer>().sharedMaterials; r1.shadowCastingMode = ShadowCastingMode.On;
        }
        var lg = fgo.AddComponent<LODGroup>();
        lg.fadeMode = LODFadeMode.CrossFade; lg.animateCrossFading = true;
        float h0 = frontSize / (frontDist * 2f * Mathf.Tan(30f * Mathf.Deg2Rad));
        if (r1 != null) lg.SetLODs(new[] { new LOD(h0, new Renderer[] { r0 }), new LOD(0.001f, new Renderer[] { r1 }) });
        else lg.SetLODs(new[] { new LOD(0.001f, new Renderer[] { r0 }) });
        lg.localReferencePoint = front.bounds.center; lg.size = frontSize;      // SetLODs 뒤에 넣어야 남음

        // Rest: FRONT 를 뺀 임포스터 오브젝트 + Keep 을 메시 하나로(기준점 기준)
        var V = new List<Vector3>(); var N = new List<Vector3>(); var U = new List<Vector2>();
        var rmats = new List<Material>(); var idx = new List<List<int>>();
        int nobj = 0;
        var parts = new List<KeyValuePair<MeshFilter, Vector3>>();      // (메시, 빼는 값 = 원래 기준점 - 옮김)
        var srcRoot = new Dictionary<MeshFilter, Transform>();
        if (string.IsNullOrEmpty(restSpec))
        {
            foreach (var mf in imp.GetComponentsInChildren<MeshFilter>(true)) if (mf.transform != fi) { parts.Add(new KeyValuePair<MeshFilter, Vector3>(mf, pivot)); srcRoot[mf] = imp.transform; }
        }
        else
        {
            foreach (var e in restSpec.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var c = e.Split(':');
                var sp = AssetDatabase.LoadAssetAtPath<GameObject>(PalaceImpostorTool.Root + "/YJ_" + c[0] + "/YJ_" + c[0] + "_Impostor.prefab");
                if (sp == null) { missing.Add("prefab YJ_" + c[0]); continue; }
                var tr = sp.transform.Find(c[1]);
                if (tr == null) { missing.Add(c[0] + ":" + c[1]); continue; }
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                var off = PivotOf(c[0]) - new Vector3(float.Parse(c[2], inv), 0f, float.Parse(c[3], inv));
                var mf = tr.GetComponent<MeshFilter>();
                parts.Add(new KeyValuePair<MeshFilter, Vector3>(mf, off)); srcRoot[mf] = sp.transform;
            }
        }
        foreach (var kvp in parts)
        {
            var mf = kvp.Key; var sub = kvp.Value;
            var mesh = mf.sharedMesh; var mm = mf.GetComponent<MeshRenderer>().sharedMaterials;
            var M = srcRoot[mf].worldToLocalMatrix * mf.transform.localToWorldMatrix;
            var v = mesh.vertices; var n = mesh.normals; var u = mesh.uv;
            int b0 = V.Count;
            for (int i = 0; i < v.Length; i++) { V.Add(M.MultiplyPoint3x4(v[i]) - sub); N.Add(n.Length == v.Length ? M.MultiplyVector(n[i]).normalized : Vector3.up); U.Add(u.Length == v.Length ? u[i] : Vector2.zero); }
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                var mat = s < mm.Length ? mm[s] : null;
                if (mat == null) missing.Add(mf.name + "#" + s);
                int gi = rmats.IndexOf(mat);
                if (gi < 0) { rmats.Add(mat); idx.Add(new List<int>()); gi = rmats.Count - 1; }
                foreach (var k in mesh.GetTriangles(s)) idx[gi].Add(k + b0);
            }
            nobj++;
        }
        var ap = Root + "/" + cx + "/Meshes/" + kitName + "_Rest.asset";
        var rm = AssetDatabase.LoadAssetAtPath<Mesh>(ap); bool isNew = rm == null;
        if (isNew) rm = new Mesh();
        rm.Clear(); rm.name = kitName + "_Rest";
        rm.indexFormat = V.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        rm.SetVertices(V); rm.SetNormals(N); rm.SetUVs(0, U);
        rm.subMeshCount = idx.Count;
        for (int s = 0; s < idx.Count; s++) rm.SetTriangles(idx[s], s, false);
        rm.RecalculateBounds();
        PalaceMeshCompact.CompactMesh(rm);
        if (isNew) AssetDatabase.CreateAsset(rm, ap); else EditorUtility.SetDirty(rm);
        AssetDatabase.SaveAssets();
        var rgo = new GameObject("Rest"); rgo.transform.SetParent(go.transform, false);
        rgo.AddComponent<MeshFilter>().sharedMesh = rm;
        var rr = rgo.AddComponent<MeshRenderer>(); rr.sharedMaterials = rmats.ToArray(); rr.shadowCastingMode = ShadowCastingMode.On;
        var lr = rgo.AddComponent<LODGroup>(); lr.fadeMode = LODFadeMode.None;
        lr.SetLODs(new[] { new LOD(0.002f, new Renderer[] { rr }) }); lr.RecalculateBounds();

        var path = PalaceImpostorTool.Root + "/Kits/" + kitName + ".prefab";
        PrefabUtility.SaveAsPrefabAsset(go, path);
        UnityEngine.Object.DestroyImmediate(go);
        long ft = 0, it = 0, rt = 0;
        for (int s = 0; s < front.subMeshCount; s++) ft += front.GetIndexCount(s) / 3;
        if (fi != null) { var m1 = fi.GetComponent<MeshFilter>().sharedMesh; for (int s = 0; s < m1.subMeshCount; s++) it += m1.GetIndexCount(s) / 3; }
        for (int s = 0; s < rm.subMeshCount; s++) rt += rm.GetIndexCount(s) / 3;
        return (missing.Count > 0 ? "MISSING " + string.Join(",", missing.Take(20)) + " " : "") + "prefab " + path + " front(LOD0) tris=" + ft + " front(LOD1) tris=" + it +
               " rest tris=" + rt + " (objects " + nobj + ", mats " + rmats.Count + ") LOD0→1 h=" + h0.ToString("F3");
    }

    // ---------- 행랑 방식(2026-09-30 사용자 지시 "멀쩡한 벽 한칸이랑 문을 떼가지고 계속 일자로 이어붙이세요") ----------
    // 길가 = 사정전 남행각 조각(yj_row.py: YJR_Modules.bin 의 YJR_<조각>_LOD0/1)을 이어 붙인 행랑, 관아 = 행랑 뒤의 안쪽만(BuildRest).
    // 조각 프리팹 PalaceImpostorTool.Root/Kits/YJR_<조각>.prefab: LODGroup(LOD0 원래 메시 / LOD1 8 cm 줄임 / LOD2 YJ_A 임포스터의 ROW_<조각> 껍데기).
    // 조각 좌표 = (원래 x - x0, y - 0.55, z - 2.52). size = 조각 크기(수동), 전환 거리 d0·d1(m), 끝 dCull.
    public static string BuildRowModules(string dir, string simplifiedPrefab, float d0 = 45f, float d1 = 130f, float dCull = 1200f)
    {
        var mats = MatsOf(simplifiedPrefab);
        var sub = ReadSubMats(Path.Combine(dir, "YJR_Modules.bin"));
        var imp = AssetDatabase.LoadAssetAtPath<GameObject>(PalaceImpostorTool.Root + "/YJ_A/YJ_A_Impostor.prefab");
        var sb = new StringBuilder(); var missing = new HashSet<string>();
        // 줄 끝 END_W/END_E 조각은 2026-09-30 부터 안 씀(ㄱ자 모서리 = BuildCornerRing)
        var spec = new[] { new object[] { "BAY", 14.130f }, new object[] { "GATE", -7.953f } };
        float k2 = 2f * Mathf.Tan(30f * Mathf.Deg2Rad);
        foreach (var e in spec)
        {
            string k = (string)e[0]; float x0 = (float)e[1];
            var go = new GameObject("YJR_" + k);
            var rs = new List<Renderer[]>();
            Bounds bb = new Bounds(); bool have = false;
            for (int l = 0; l < 2; l++)
            {
                string mn = "YJR_" + k + "_LOD" + l;
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Root + "/YJR/Meshes/" + mn + ".asset");
                if (mesh == null) { missing.Add(mn); continue; }
                var c = new GameObject(mn); c.transform.SetParent(go.transform, false);
                c.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = c.AddComponent<MeshRenderer>();
                r.sharedMaterials = sub[mn].Select(n => { Material m; if (!mats.TryGetValue(n, out m)) { missing.Add(n); return null; } return m; }).ToArray();
                r.shadowCastingMode = ShadowCastingMode.On;
                rs.Add(new Renderer[] { r });
                if (l == 0) { bb = mesh.bounds; have = true; }
            }
            var hull = imp != null ? imp.transform.Find("ROW_" + k) : null;
            if (hull != null)
            {
                var c = new GameObject("YJR_" + k + "_LOD2"); c.transform.SetParent(go.transform, false);
                c.transform.localPosition = -new Vector3(x0, 0.55f, 2.52f);
                c.AddComponent<MeshFilter>().sharedMesh = hull.GetComponent<MeshFilter>().sharedMesh;
                var r = c.AddComponent<MeshRenderer>(); r.sharedMaterials = hull.GetComponent<MeshRenderer>().sharedMaterials; r.shadowCastingMode = ShadowCastingMode.On;
                rs.Add(new Renderer[] { r });
            }
            else missing.Add("ROW_" + k);
            float size = have ? Mathf.Max(bb.size.x, bb.size.z) : 10f;
            var lg = go.AddComponent<LODGroup>(); lg.fadeMode = LODFadeMode.CrossFade; lg.animateCrossFading = true;
            var ds = new[] { d0, d1, dCull };
            var lods = new List<LOD>();
            for (int l = 0; l < rs.Count; l++) lods.Add(new LOD(Mathf.Min(0.99f, size / (ds[Mathf.Min(l, 2)] * k2)), rs[l]));
            lg.SetLODs(lods.ToArray());
            lg.localReferencePoint = bb.center; lg.size = size;
            var path = PalaceImpostorTool.Root + "/Kits/YJR_" + k + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
            sb.Append("YJR_" + k + " size " + size.ToString("F1") + " lods " + rs.Count + "\n");
        }
        return (missing.Count > 0 ? "MISSING " + string.Join(",", missing.Take(20)) + "\n" : "") + sb;
    }

    // 행랑 ㄱ자 모서리(yj_corner.py: YJR_Corner.bin 의 YJR_CORNER_LOD0/1/2 — BAY 두 칸 조각 두 개를 45° 로 잘라 맞댐, 바깥 추녀·안쪽 골,
    // LOD2 는 임포스터 껍데기(재질 IMP_YJ_A_P<k>)) + 줄 끝 관아 둘레 껍데기(yj_plan3.py: YJR_Ring.bin 의 YJR_RING_START/END — 뒤 행각·안쪽 모서리·안쪽 옆 행각, LOD 하나).
    // 메시는 먼저 PalaceLodBinImport.ImportMeshes(dir, Root + "/YJR", 20, "", "YJR_Corner.bin" / "YJR_Ring.bin", "Meshes") 로.
    public static string BuildCornerRing(string dir, string simplifiedPrefab, float d0 = 45f, float d1 = 130f, float dCull = 1200f, float ringCull = 0.002f)
    {
        var mats = MatsOf(simplifiedPrefab);
        MatsOf(PalaceImpostorTool.Root + "/YJ_A/YJ_A_Impostor.prefab", mats);
        var sub = ReadSubMats(Path.Combine(dir, "YJR_Corner.bin"));
        foreach (var kv in ReadSubMats(Path.Combine(dir, "YJR_Ring.bin"))) sub[kv.Key] = kv.Value;
        var missing = new HashSet<string>(); var sb = new StringBuilder();
        float k2 = 2f * Mathf.Tan(30f * Mathf.Deg2Rad);
        Func<string, GameObject, Renderer> add = (mn, parent) =>
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Root + "/YJR/Meshes/" + mn + ".asset");
            if (mesh == null || !sub.ContainsKey(mn)) { missing.Add(mn); return null; }
            var c = new GameObject(mn); c.transform.SetParent(parent.transform, false);
            c.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = c.AddComponent<MeshRenderer>();
            r.sharedMaterials = sub[mn].Select(n => { Material m; if (!mats.TryGetValue(n, out m)) { missing.Add(n); return null; } return m; }).ToArray();
            r.shadowCastingMode = ShadowCastingMode.On;
            return r;
        };
        {
            var go = new GameObject("YJR_CORNER");
            var rs = new List<Renderer>();
            for (int l = 0; l < 3; l++) { var r = add("YJR_CORNER_LOD" + l, go); if (r != null) rs.Add(r); }
            var bb = rs.Count > 0 ? rs[0].GetComponent<MeshFilter>().sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one * 10f);
            float size = Mathf.Max(bb.size.x, bb.size.z);
            var lg = go.AddComponent<LODGroup>(); lg.fadeMode = LODFadeMode.CrossFade; lg.animateCrossFading = true;
            var ds = new[] { d0, d1, dCull };
            var lods = new List<LOD>();
            for (int l = 0; l < rs.Count; l++) lods.Add(new LOD(Mathf.Min(0.99f, size / (ds[Mathf.Min(l, 2)] * k2)), new Renderer[] { rs[l] }));
            lg.SetLODs(lods.ToArray());
            lg.localReferencePoint = bb.center; lg.size = size;
            PrefabUtility.SaveAsPrefabAsset(go, PalaceImpostorTool.Root + "/Kits/YJR_CORNER.prefab");
            UnityEngine.Object.DestroyImmediate(go);
            sb.Append("YJR_CORNER size " + size.ToString("F1") + " lods " + rs.Count + "\n");
        }
        foreach (var k in new[] { "YJR_RING_START", "YJR_RING_END" })
        {
            var go = new GameObject(k);
            var r = add(k, go);
            if (r == null) { UnityEngine.Object.DestroyImmediate(go); continue; }
            var lg = go.AddComponent<LODGroup>(); lg.fadeMode = LODFadeMode.None;
            lg.SetLODs(new[] { new LOD(ringCull, new Renderer[] { r }) }); lg.RecalculateBounds();
            var mesh = r.GetComponent<MeshFilter>().sharedMesh; long t = 0;
            PrefabUtility.SaveAsPrefabAsset(go, PalaceImpostorTool.Root + "/Kits/" + k + ".prefab");
            UnityEngine.Object.DestroyImmediate(go);
            for (int s = 0; s < mesh.subMeshCount; s++) t += mesh.GetIndexCount(s) / 3;
            sb.Append(k + " tris " + t + "\n");
        }
        return (missing.Count > 0 ? "MISSING " + string.Join(",", missing.Take(20)) + "\n" : "") + sb;
    }

    // 행랑 길 쪽 지붕면용 원본 기와(yj_row.py 의 orig_front): 사정전 지붕 LOD 프리팹의 LOD0(데모 원본 기와)에서
    // 재질 MI_SJJ_HG_Roof01(남행각)·MI_SJM_Kiwa(사정문) 삼각형 중 무게중심 x -10~22 · z < 9.5 인 것을 HNMS(메시 = 재질마다 하나, 단지 프리팹 루트 좌표)로 씀.
    public static string ExportOrigTiles(string outPath, string roofLodPrefab = "Assets/02.Resources/01.Asset/Models/Gyeongbokgung/_Baked/Sajeongjeon/Sajeongjeon_RoofLOD.prefab")
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(roofLodPrefab);
        if (go == null) return "no prefab " + roofLodPrefab;
        var root = go.transform.worldToLocalMatrix;
        var want = new HashSet<string>(new[] { "MI_SJJ_HG_Roof01", "MI_SJJ_HG_Roof01_Shell", "MI_SJM_Kiwa", "MI_SJM_Kiwa_Shell" });
        var P = new Dictionary<string, List<Vector3>>(); var N = new Dictionary<string, List<Vector3>>(); var U = new Dictionary<string, List<Vector2>>();
        foreach (var lg in go.GetComponentsInChildren<LODGroup>(true))
        {
            var lods = lg.GetLODs(); if (lods.Length == 0) continue;
            foreach (var r in lods[0].renderers)
            {
                if (r == null) continue; var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
                var M = root * r.transform.localToWorldMatrix; var NM = M.inverse.transpose; var mesh = mf.sharedMesh;
                bool mir = M.determinant < 0;
                var v = mesh.vertices; var nn = mesh.normals; var uv = mesh.uv; var mats = r.sharedMaterials;
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    string mn = s < mats.Length && mats[s] != null ? mats[s].name : "";
                    if (!want.Contains(mn)) continue;
                    if (!P.ContainsKey(mn)) { P[mn] = new List<Vector3>(); N[mn] = new List<Vector3>(); U[mn] = new List<Vector2>(); }
                    var t = mesh.GetTriangles(s);
                    for (int i = 0; i < t.Length; i += 3)
                    {
                        var a = M.MultiplyPoint3x4(v[t[i]]); var b = M.MultiplyPoint3x4(v[t[i + 1]]); var c = M.MultiplyPoint3x4(v[t[i + 2]]);
                        float cx = (a.x + b.x + c.x) / 3f, cz = (a.z + b.z + c.z) / 3f;
                        if (cx < -10f || cx > 22f || cz > 9.5f) continue;
                        int i1 = mir ? i + 2 : i + 1, i2 = mir ? i + 1 : i + 2;
                        foreach (var k in new[] { t[i], t[i1], t[i2] })
                        {
                            P[mn].Add(M.MultiplyPoint3x4(v[k]));
                            N[mn].Add(nn.Length == v.Length ? NM.MultiplyVector(nn[k]).normalized : Vector3.up);
                            U[mn].Add(uv.Length == v.Length ? uv[k] : Vector2.zero);
                        }
                    }
                }
            }
        }
        var sb = new StringBuilder();
        using (var fs = new FileStream(outPath, FileMode.Create))
        using (var bw = new BinaryWriter(fs))
        {
            bw.Write(Magic); bw.Write(P.Count);
            foreach (var kv in P)
            {
                var nb = Encoding.UTF8.GetBytes("ORIG_" + kv.Key); bw.Write(nb.Length); bw.Write(nb);
                int nv = kv.Value.Count; bw.Write(nv); bw.Write(1);
                foreach (var q in kv.Value) { bw.Write(q.x); bw.Write(q.y); bw.Write(q.z); }
                foreach (var q in N[kv.Key]) { bw.Write(q.x); bw.Write(q.y); bw.Write(q.z); }
                foreach (var q in U[kv.Key]) { bw.Write(q.x); bw.Write(q.y); }
                var mb = Encoding.UTF8.GetBytes(kv.Key); bw.Write(mb.Length); bw.Write(mb);
                bw.Write(nv); for (int i = 0; i < nv; i++) bw.Write(i);
                sb.Append(kv.Key + " tris " + (nv / 3) + "\n");
            }
        }
        return sb.ToString();
    }

    // 관아 안쪽만(행랑 뒤): restSpec 오브젝트를 합쳐 메시 하나 + LODGroup(0.002 에서 끔). 좌표 = 기준점 기준(정면 처마 끝 z 0, 땅 y 0).
    public static string BuildRest(string kitName, string restSpec)
    {
        var missing = new HashSet<string>();
        var V = new List<Vector3>(); var N = new List<Vector3>(); var U = new List<Vector2>();
        var rmats = new List<Material>(); var idx = new List<List<int>>();
        int nobj = 0;
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        foreach (var e in restSpec.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var c = e.Split(':');
            var sp = AssetDatabase.LoadAssetAtPath<GameObject>(PalaceImpostorTool.Root + "/YJ_" + c[0] + "/YJ_" + c[0] + "_Impostor.prefab");
            if (sp == null) { missing.Add("prefab YJ_" + c[0]); continue; }
            var tr = sp.transform.Find(c[1]);
            if (tr == null) { missing.Add(c[0] + ":" + c[1]); continue; }
            var off = PivotOf(c[0]) - new Vector3(float.Parse(c[2], inv), 0f, float.Parse(c[3], inv));
            var mf = tr.GetComponent<MeshFilter>();
            var mesh = mf.sharedMesh; var mm = mf.GetComponent<MeshRenderer>().sharedMaterials;
            var M = sp.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            var v = mesh.vertices; var n = mesh.normals; var u = mesh.uv;
            int b0 = V.Count;
            for (int i = 0; i < v.Length; i++) { V.Add(M.MultiplyPoint3x4(v[i]) - off); N.Add(n.Length == v.Length ? M.MultiplyVector(n[i]).normalized : Vector3.up); U.Add(u.Length == v.Length ? u[i] : Vector2.zero); }
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                var mat = s < mm.Length ? mm[s] : null;
                if (mat == null) missing.Add(mf.name + "#" + s);
                int gi = rmats.IndexOf(mat);
                if (gi < 0) { rmats.Add(mat); idx.Add(new List<int>()); gi = rmats.Count - 1; }
                foreach (var k in mesh.GetTriangles(s)) idx[gi].Add(k + b0);
            }
            nobj++;
        }
        EnsureFolder(Root + "/Rest");
        var ap = Root + "/Rest/" + kitName + ".asset";
        var rm = AssetDatabase.LoadAssetAtPath<Mesh>(ap); bool isNew = rm == null;
        if (isNew) rm = new Mesh();
        rm.Clear(); rm.name = kitName;
        rm.indexFormat = V.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        rm.SetVertices(V); rm.SetNormals(N); rm.SetUVs(0, U);
        rm.subMeshCount = idx.Count;
        for (int s = 0; s < idx.Count; s++) rm.SetTriangles(idx[s], s, false);
        rm.RecalculateBounds();
        PalaceMeshCompact.CompactMesh(rm);
        if (isNew) AssetDatabase.CreateAsset(rm, ap); else EditorUtility.SetDirty(rm);
        AssetDatabase.SaveAssets();
        var go = new GameObject(kitName);
        go.AddComponent<MeshFilter>().sharedMesh = rm;
        var rr = go.AddComponent<MeshRenderer>(); rr.sharedMaterials = rmats.ToArray(); rr.shadowCastingMode = ShadowCastingMode.On;
        var lr = go.AddComponent<LODGroup>(); lr.fadeMode = LODFadeMode.None;
        lr.SetLODs(new[] { new LOD(0.002f, new Renderer[] { rr }) }); lr.RecalculateBounds();
        var path = PalaceImpostorTool.Root + "/Kits/" + kitName + ".prefab";
        PrefabUtility.SaveAsPrefabAsset(go, path);
        UnityEngine.Object.DestroyImmediate(go);
        long rt = 0; for (int s = 0; s < rm.subMeshCount; s++) rt += rm.GetIndexCount(s) / 3;
        return (missing.Count > 0 ? "MISSING " + string.Join(",", missing.Take(20)) + " " : "") + kitName + " tris=" + rt + " objects " + nobj + " mats " + rmats.Count;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
