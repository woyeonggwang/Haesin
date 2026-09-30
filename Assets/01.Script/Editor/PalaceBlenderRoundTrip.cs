// 경복궁 건물을 Blender 로 보내 사람이 눈으로 보며 지운 뒤 다시 Unity 원본 프리팹에 넣는 왕복 도구 — MCP 에서 정적 메서드로 호출.
// (자동 "방 판정"은 예외가 많아 멈춤 — 사용자 지시 2026-09-27: 사람이 보고 지우는 게 맞다)
// 1) ExportForEdit(단지, 코드) → D:\UnityAssetCache\_edit\<단지>_<코드>.obj/.mtl/.map.tsv  (렌더러 하나 = OBJ 오브젝트 하나 "R<번호>_<메시>")
// 2) Blender 에서 가져와 면·오브젝트를 지움 → 같은 폴더에 <단지>_<코드>_edited.obj 로 내보냄
// 3) ImportEdited(단지, 코드) → 오브젝트가 통째로 없어졌으면 렌더러를 지우고, 면이 줄었으면 새 메시로 바꾼다(그대로면 원래 공유 메시 유지)
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class PalaceBlenderRoundTrip
{
    public const string EditDir = @"D:\UnityAssetCache\_edit";
    const string Root = PalaceBakeTool.BakedAssetRoot;
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    static string F(float v) { return v.ToString("0.#####", Inv); }

    static bool Match(string meshName, string code)
    {
        // 코드 "GNJ" = SM_GNJ_… 중 행각(SM_GNJ_HG_…) 제외. 코드 "GNJ_HG" = 행각만.
        if (!meshName.StartsWith("SM_" + code + "_")) return false;
        if (!code.Contains("_HG") && meshName.StartsWith("SM_" + code + "_HG")) return false;
        return true;
    }

    static int[] PathOf(Transform t, Transform root) { var p = new List<int>(); while (t != root) { p.Add(t.GetSiblingIndex()); t = t.parent; } p.Reverse(); return p.ToArray(); }
    static Transform ByPath(Transform root, int[] p) { var t = root; foreach (var i in p) { if (i >= t.childCount) return null; t = t.GetChild(i); } return t; }

    static string TexPath(Material m, string prop)
    {
        if (m == null || !m.HasProperty(prop)) return null;
        var t = m.GetTexture(prop); if (t == null) return null;
        var ap = AssetDatabase.GetAssetPath(t); if (string.IsNullOrEmpty(ap)) return null;
        return Path.GetFullPath(ap).Replace('\\', '/');
    }

    public static string ExportForEdit(string cx, string code)
    {
        Directory.CreateDirectory(EditDir);
        var path = Root + "/" + cx + "/" + cx + "_Original.prefab";
        var rt = PrefabUtility.LoadPrefabContents(path);
        var name = cx + "_" + code;
        var obj = new StringBuilder(); var map = new StringBuilder(); var mats = new Dictionary<string, Material>();
        obj.AppendLine("mtllib " + name + ".mtl");
        int vBase = 1, count = 0; long tris = 0;
        try
        {
            var rends = rt.GetComponentsInChildren<MeshRenderer>(false);
            for (int i = 0; i < rends.Length; i++)
            {
                var r = rends[i]; if (!r.enabled) continue;
                var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
                var m = mf.sharedMesh; if (!Match(m.name, code)) continue;
                var M = rt.transform.worldToLocalMatrix * r.transform.localToWorldMatrix; var N = M.inverse.transpose;
                bool mir = M.determinant < 0;
                var v = m.vertices; var n = m.normals; var uv = m.uv;
                string oname = "R" + i + "_" + m.name;
                obj.AppendLine("o " + oname);
                // Unity(왼손) → OBJ(오른손): x 를 뒤집고 면 순서도 뒤집는다
                foreach (var p in v) { var w = M.MultiplyPoint3x4(p); obj.Append("v ").Append(F(-w.x)).Append(' ').Append(F(w.y)).Append(' ').Append(F(w.z)).Append('\n'); }
                foreach (var t in uv) obj.Append("vt ").Append(F(t.x)).Append(' ').Append(F(t.y)).Append('\n');
                foreach (var q in n) { var w = N.MultiplyVector(q).normalized; obj.Append("vn ").Append(F(-w.x)).Append(' ').Append(F(w.y)).Append(' ').Append(F(w.z)).Append('\n'); }
                var sm = r.sharedMaterials;
                for (int s = 0; s < m.subMeshCount; s++)
                {
                    var mat = s < sm.Length ? sm[s] : null; string mn = mat != null ? mat.name : "none";
                    if (mat != null) mats[mn] = mat;
                    obj.AppendLine("usemtl " + mn);
                    var idx = m.GetTriangles(s);
                    for (int k = 0; k < idx.Length; k += 3)
                    {
                        int a = idx[k] + vBase, b = idx[k + 1] + vBase, c = idx[k + 2] + vBase;
                        if (!mir) { int tmp = b; b = c; c = tmp; }   // x 뒤집기로 한 번 뒤집힘(음수 배율이면 두 번 → 그대로)
                        bool hasUv = uv.Length > 0, hasN = n.Length > 0;
                        obj.Append("f ");
                        foreach (var ix in new[] { a, b, c }) { obj.Append(ix); if (hasUv || hasN) { obj.Append('/'); if (hasUv) obj.Append(ix); if (hasN) obj.Append('/').Append(ix); } obj.Append(' '); }
                        obj.Append('\n'); tris++;
                    }
                }
                map.Append(oname).Append('\t').Append(string.Join(",", PathOf(r.transform, rt.transform).Select(x => x.ToString()).ToArray()))
                   .Append('\t').Append(m.name).Append('\t').Append(m.triangles.Length / 3).Append('\t').Append(string.Join("|", sm.Select(x => x ? x.name : "none").ToArray())).Append('\n');
                vBase += v.Length; count++;
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(rt); }
        var mtl = new StringBuilder();
        foreach (var kv in mats)
        {
            var mm = kv.Value; mtl.AppendLine("newmtl " + kv.Key);
            var col = mm.HasProperty("_BaseColor") ? mm.GetColor("_BaseColor") : Color.white;
            mtl.AppendLine("Kd " + F(col.r) + " " + F(col.g) + " " + F(col.b));
            var bc = TexPath(mm, "_BaseColorMap"); if (bc != null) mtl.AppendLine("map_Kd " + bc);
            var nm = TexPath(mm, "_NormalMap"); if (nm != null) mtl.AppendLine("norm " + nm);
        }
        File.WriteAllText(Path.Combine(EditDir, name + ".obj"), obj.ToString());
        File.WriteAllText(Path.Combine(EditDir, name + ".mtl"), mtl.ToString());
        File.WriteAllText(Path.Combine(EditDir, name + ".map.tsv"), map.ToString());
        return "exported " + name + " objects=" + count + " tris=" + tris + " mats=" + mats.Count;
    }

    // ---------------- 되돌려 넣기 ----------------
    const string ShellMatDir = "Assets/02.Resources/01.Asset/Models/Gyeongbokgung/_Shared/ShellMaterials";

    // 한쪽 면만 그리는 재질 사본(방 안에서 벽 바깥 면의 뒷면이 안 보이게)
    public static Material ShellMat(Material m)
    {
        if (m == null) return null;
        if (!AssetDatabase.IsValidFolder(ShellMatDir)) AssetDatabase.CreateFolder(Path.GetDirectoryName(ShellMatDir).Replace('\\', '/'), Path.GetFileName(ShellMatDir));
        if (m.name.EndsWith("_Shell")) return m;
        var p = ShellMatDir + "/" + m.name + "_Shell.mat";
        var s = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (s != null) return s;
        s = new Material(m) { name = m.name + "_Shell" };
        s.SetFloat("_DoubleSidedEnable", 0); s.SetFloat("_CullMode", 2); s.SetFloat("_CullModeForward", 2);
        s.DisableKeyword("_DOUBLESIDED_ON");
        AssetDatabase.CreateAsset(s, p);
        var t = Type.GetType("UnityEditor.Rendering.HighDefinition.HDShaderUtils, Unity.RenderPipelines.HighDefinition.Editor");
        var mi = t == null ? null : t.GetMethod("ResetMaterialKeywords", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, new[] { typeof(Material) }, null);
        if (mi != null) mi.Invoke(null, new object[] { s });
        return s;
    }

    class EObj { public List<KeyValuePair<string, List<int[]>>> groups = new List<KeyValuePair<string, List<int[]>>>(); public int tris; }

    // Blender 에서 지운 결과(<단지>_<코드>_edited.obj)를 원본 프리팹에 넣는다.
    // singleSided: 손댄 오브젝트는 한쪽 면 재질로(벽 안쪽 면을 지웠을 때 방 안에서 바깥 면 뒷면이 안 보이게)
    public static string ImportEdited(string cx, string code, bool singleSided = true, bool dryRun = false)
    {
        var check = new StringBuilder();
        var name = cx + "_" + code;
        var objPath = Path.Combine(EditDir, name + "_edited.obj"); var mapPath = Path.Combine(EditDir, name + ".map.tsv");
        if (!File.Exists(objPath)) return "no " + objPath;
        var V = new List<Vector3>(); var VT = new List<Vector2>(); var VN = new List<Vector3>();
        var objs = new Dictionary<string, EObj>(); EObj cur = null; List<int[]> curG = null;
        foreach (var raw in File.ReadLines(objPath))
        {
            if (raw.Length < 2) continue;
            var line = raw.Trim();
            if (line.StartsWith("v ")) { var a = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries); V.Add(new Vector3(float.Parse(a[1], Inv), float.Parse(a[2], Inv), float.Parse(a[3], Inv))); }
            else if (line.StartsWith("vt ")) { var a = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries); VT.Add(new Vector2(float.Parse(a[1], Inv), float.Parse(a[2], Inv))); }
            else if (line.StartsWith("vn ")) { var a = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries); VN.Add(new Vector3(float.Parse(a[1], Inv), float.Parse(a[2], Inv), float.Parse(a[3], Inv))); }
            else if (line.StartsWith("o ")) { var on = line.Substring(2).Trim(); cur = new EObj(); objs[on] = cur; curG = null; }
            else if (line.StartsWith("usemtl ")) { if (cur == null) { cur = new EObj(); objs["?"] = cur; } curG = new List<int[]>(); cur.groups.Add(new KeyValuePair<string, List<int[]>>(line.Substring(7).Trim(), curG)); }
            else if (line.StartsWith("f "))
            {
                if (cur == null) { cur = new EObj(); objs["?"] = cur; }
                if (curG == null) { curG = new List<int[]>(); cur.groups.Add(new KeyValuePair<string, List<int[]>>("none", curG)); }
                var a = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var poly = new List<int[]>();
                for (int i = 1; i < a.Length; i++)
                {
                    var q = a[i].Split('/'); var t3 = new int[3];
                    t3[0] = int.Parse(q[0]); t3[0] = t3[0] < 0 ? V.Count + t3[0] : t3[0] - 1;
                    t3[1] = q.Length > 1 && q[1].Length > 0 ? int.Parse(q[1]) : 0; t3[1] = t3[1] < 0 ? VT.Count + t3[1] : t3[1] - 1;
                    t3[2] = q.Length > 2 && q[2].Length > 0 ? int.Parse(q[2]) : 0; t3[2] = t3[2] < 0 ? VN.Count + t3[2] : t3[2] - 1;
                    poly.Add(t3);
                }
                for (int i = 1; i + 1 < poly.Count; i++) { curG.Add(poly[0]); curG.Add(poly[i]); curG.Add(poly[i + 1]); cur.tris++; }
            }
        }
        // Blender 가 붙인 ".001" 같은 꼬리·메시 이름 꼬리 없애고 R번호로 찾기
        // Blender 에서 복제한 오브젝트는 이름 끝에 ".001" 같은 꼬리가 붙는다 → 원본 짝(R번호)과 따로 모아 새 오브젝트로 만든다
        var byIdx = new Dictionary<string, EObj>(); var dups = new List<KeyValuePair<string, EObj>>();
        var dupRx = new System.Text.RegularExpressions.Regex(@"\.\d{3,}$");
        foreach (var kv in objs)
        {
            var k = kv.Key; int u = k.IndexOf('_'); if (!k.StartsWith("R") || u <= 1) continue;
            if (dupRx.IsMatch(k)) dups.Add(new KeyValuePair<string, EObj>(k.Substring(0, u), kv.Value));
            else byIdx[k.Substring(0, u)] = kv.Value;
        }

        var prefabPath = Root + "/" + cx + "/" + cx + "_Original.prefab";
        var container = Root + "/" + cx + "/" + name + "_Edited.asset";
        var rt = PrefabUtility.LoadPrefabContents(prefabPath);
        int removed = 0, changed = 0, same = 0; var newMeshes = new List<Mesh>(); var toDestroy = new List<GameObject>();
        try
        {
            foreach (var line in File.ReadAllLines(mapPath))
            {
                var c = line.Split('\t'); if (c.Length < 5) continue;
                var key = c[0].Substring(0, c[0].IndexOf('_'));
                var t = ByPath(rt.transform, c[1].Length == 0 ? new int[0] : c[1].Split(',').Select(int.Parse).ToArray());
                if (t == null) continue;
                var r = t.GetComponent<MeshRenderer>(); var mf = t.GetComponent<MeshFilter>(); if (r == null || mf == null || mf.sharedMesh == null || mf.sharedMesh.name != c[2]) continue;
                int origTris = int.Parse(c[3]);
                EObj e;
                if (!byIdx.TryGetValue(key, out e) || e.tris == 0)
                {
                    if (t.childCount == 0) toDestroy.Add(t.gameObject); else { r.enabled = false; var mc0 = t.GetComponent<MeshCollider>(); if (mc0) mc0.enabled = false; }
                    removed++; continue;
                }
                // 새 메시(그 오브젝트의 로컬 좌표로)
                var M = rt.transform.worldToLocalMatrix * t.localToWorldMatrix; var W2L = M.inverse; var N = M.transpose;   // 법선: (M^-1)^T 의 역 = M^T
                if (e.tris == origTris)
                {
                    // 면 수가 같아도 옮기거나 돌렸으면(들어열개 문 내리기 등) 바뀐 것으로 본다 — 자리(바운드)를 비교
                    var ob = GeometryUtility.CalculateBounds(mf.sharedMesh.vertices, M);
                    bool first = true; var eb = new Bounds();
                    foreach (var g in e.groups) foreach (var q in g.Value) { var w = V[q[0]]; var pnt = new Vector3(-w.x, w.y, w.z); if (first) { eb = new Bounds(pnt, Vector3.zero); first = false; } else eb.Encapsulate(pnt); }
                    if ((ob.center - eb.center).magnitude < 0.01f && (ob.size - eb.size).magnitude < 0.01f) { same++; continue; }
                }
                Material[] mats;
                var nm = BuildEditedMesh(e, M, r.sharedMaterials, singleSided, c[2] + "_edit_" + key, V, VT, VN, out mats);
                if (dryRun)
                {
                    // 검사: 새 삼각형이 원래 메시의 같은 자리·같은 방향 삼각형과 맞는지(좌표·면 방향 변환 확인)
                    var om = mf.sharedMesh; var ov = om.vertices; var ot = om.triangles;
                    var oset = new Dictionary<string, Vector3>();
                    for (int i = 0; i < ot.Length; i += 3)
                    {
                        var ce = (ov[ot[i]] + ov[ot[i + 1]] + ov[ot[i + 2]]) / 3f;
                        oset[ce.x.ToString("F3") + "," + ce.y.ToString("F3") + "," + ce.z.ToString("F3")] = Vector3.Cross(ov[ot[i + 1]] - ov[ot[i]], ov[ot[i + 2]] - ov[ot[i]]);
                    }
                    int ok = 0, bad = 0, flip = 0; var nt = nm.triangles; var nv = nm.vertices;
                    for (int i = 0; i < nt.Length; i += 3)
                    {
                        var ce = (nv[nt[i]] + nv[nt[i + 1]] + nv[nt[i + 2]]) / 3f; Vector3 on;
                        if (!oset.TryGetValue(ce.x.ToString("F3") + "," + ce.y.ToString("F3") + "," + ce.z.ToString("F3"), out on)) { bad++; continue; }
                        if (Vector3.Dot(on, Vector3.Cross(nv[nt[i + 1]] - nv[nt[i]], nv[nt[i + 2]] - nv[nt[i]])) > 0) ok++; else flip++;
                    }
                    check.Append(c[0] + " ok=" + ok + " flipped=" + flip + " notfound=" + bad + "; ");
                    changed++; continue;
                }
                PalaceMeshCompact.CompactMesh(nm);
                newMeshes.Add(nm);
                mf.sharedMesh = nm; r.sharedMaterials = mats;
                var mc = t.GetComponent<MeshCollider>(); if (mc) mc.sharedMesh = nm;
                changed++;
            }
            // 복제한 오브젝트: 원본 짝 옆에 새 오브젝트로(같은 부모·같은 자세, 메시는 Blender 에서 옮긴 자리 그대로)
            var rowByKey = new Dictionary<string, string[]>();
            foreach (var line in File.ReadAllLines(mapPath)) { var c = line.Split('\t'); if (c.Length >= 5) rowByKey[c[0].Substring(0, c[0].IndexOf('_'))] = c; }
            int added = 0;
            foreach (var d in dups)
            {
                string[] c; if (!rowByKey.TryGetValue(d.Key, out c) || d.Value.tris == 0) continue;
                var t = ByPath(rt.transform, c[1].Length == 0 ? new int[0] : c[1].Split(',').Select(int.Parse).ToArray()); if (t == null) continue;
                var r = t.GetComponent<MeshRenderer>(); if (r == null) continue;
                var M = rt.transform.worldToLocalMatrix * t.localToWorldMatrix;
                Material[] mats;
                var nm = BuildEditedMesh(d.Value, M, r.sharedMaterials, singleSided, c[2] + "_copy_" + d.Key + "_" + added, V, VT, VN, out mats);
                if (dryRun) { check.Append(c[0] + " copy tris=" + d.Value.tris + "; "); added++; continue; }
                PalaceMeshCompact.CompactMesh(nm); newMeshes.Add(nm);
                var g = new GameObject(t.name + "_copy" + added);
                g.transform.SetParent(t.parent, false);
                g.transform.localPosition = t.localPosition; g.transform.localRotation = t.localRotation; g.transform.localScale = t.localScale;
                g.AddComponent<MeshFilter>().sharedMesh = nm;
                var r2 = g.AddComponent<MeshRenderer>(); r2.sharedMaterials = mats; r2.shadowCastingMode = r.shadowCastingMode;
                if (t.GetComponent<MeshCollider>() != null) g.AddComponent<MeshCollider>().sharedMesh = nm;
                added++;
            }
            if (dryRun) return "DRYRUN copies=" + added + " " + check;
            // 메시 저장(한 파일) 뒤 프리팹 저장
            if (newMeshes.Count > 0)
            {
                if (File.Exists(container)) AssetDatabase.DeleteAsset(container);
                AssetDatabase.CreateAsset(newMeshes[0], container);
                for (int i = 1; i < newMeshes.Count; i++) AssetDatabase.AddObjectToAsset(newMeshes[i], container);
                AssetDatabase.SaveAssets();
            }
            foreach (var g in toDestroy) UnityEngine.Object.DestroyImmediate(g);
            PrefabUtility.SaveAsPrefabAsset(rt, prefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(rt); }
        return string.Format("imported {0}: removed={1} changed={2} same={3} copies={5} (edited objects {4})", name, removed, changed, same, objs.Count, dups.Count);
    }

    // OBJ 오브젝트 하나 → Unity 메시(M = 원본 프리팹 루트 기준 그 오브젝트의 변환, 결과는 그 오브젝트 로컬 좌표)
    static Mesh BuildEditedMesh(EObj e, Matrix4x4 M, Material[] srcMats, bool singleSided, string meshName, List<Vector3> V, List<Vector2> VT, List<Vector3> VN, out Material[] outMats)
    {
        var W2L = M.inverse; var N = M.transpose;
        bool mir = M.determinant < 0;
        var vmap = new Dictionary<long, int>(); var pv = new List<Vector3>(); var pn = new List<Vector3>(); var pu = new List<Vector2>();
        var subs = new List<List<int>>(); var mats = new List<Material>();
        foreach (var g in e.groups)
        {
            if (g.Value.Count == 0) continue;
            var mat = srcMats.FirstOrDefault(x => x != null && x.name == g.Key);
            if (mat == null) { var guid = AssetDatabase.FindAssets(g.Key + " t:Material").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == g.Key); if (guid != null) mat = AssetDatabase.LoadAssetAtPath<Material>(guid); }
            if (singleSided) mat = ShellMat(mat);
            int si = mats.IndexOf(mat); if (si < 0) { mats.Add(mat); subs.Add(new List<int>()); si = mats.Count - 1; }
            var tri = g.Value;
            for (int i = 0; i < tri.Count; i += 3)
            {
                var order = mir ? new[] { tri[i], tri[i + 1], tri[i + 2] } : new[] { tri[i], tri[i + 2], tri[i + 1] };   // 내보낼 때 뒤집은 순서를 되돌림
                foreach (var q in order)
                {
                    long k = ((long)q[0] << 42) ^ ((long)(q[1] + 1) << 21) ^ (long)(q[2] + 1); int vi;
                    if (!vmap.TryGetValue(k, out vi))
                    {
                        vi = pv.Count; vmap[k] = vi;
                        var w = V[q[0]]; pv.Add(W2L.MultiplyPoint3x4(new Vector3(-w.x, w.y, w.z)));
                        var nn = q[2] >= 0 && q[2] < VN.Count ? VN[q[2]] : Vector3.up; pn.Add(N.MultiplyVector(new Vector3(-nn.x, nn.y, nn.z)).normalized);
                        pu.Add(q[1] >= 0 && q[1] < VT.Count ? VT[q[1]] : Vector2.zero);
                    }
                    subs[si].Add(vi);
                }
            }
        }
        var nm = new Mesh { name = meshName, indexFormat = pv.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
        nm.SetVertices(pv); nm.SetNormals(pn); nm.SetUVs(0, pu); nm.subMeshCount = subs.Count;
        for (int s = 0; s < subs.Count; s++) nm.SetTriangles(subs[s], s, false);
        nm.RecalculateBounds(); nm.RecalculateTangents();
        outMats = mats.ToArray();
        return nm;
    }
}
