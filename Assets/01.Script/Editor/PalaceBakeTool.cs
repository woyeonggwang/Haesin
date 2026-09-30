// 경복궁 에셋(KHS) 최적화 도구 — 데모 씬을 열어 밖에서 보이는 부재만 골라 OBJ 로 내보내고,
// Blender 에서 아틀라스로 구운 결과(FBX+PNG)를 다시 가져와 프리팹으로 만든다.
// 사용: Unity MCP execute_code 에서 PalaceBakeTool.XXX() 를 단계별로 호출(한 번에 하면 MCP 시간 초과).
// 예전엔 execute_code 안에 매번 코드를 붙여 넣었는데, 단지가 11개라 재사용하려고 파일로 뺐다.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PalaceBakeTool
{
    public const string BakeRoot = @"D:\UnityAssetCache\_bake";
    public const string BakedAssetRoot = "Assets/02.Resources/01.Asset/Models/Gyeongbokgung/_Baked";
    const string HolderName = "__VIS_HOLDER";
    static readonly Vector3 Offset = new Vector3(0, 3000, 0);   // 기지 섬 지형과 겹치지 않게 위로 띄움
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // ---------- 1. 데모 씬 열기 ----------
    public static string OpenComplex(string scenePath)
    {
        CloseComplex();
        var sc = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        var holder = new GameObject(HolderName);
        SceneManager.MoveGameObjectToScene(holder, sc);
        foreach (var go in sc.GetRootGameObjects())
        {
            if (go == holder) continue;
            foreach (var l in go.GetComponentsInChildren<Light>(true)) l.enabled = false;
            foreach (var v in go.GetComponentsInChildren<UnityEngine.Rendering.Volume>(true)) v.enabled = false;
            foreach (var c in go.GetComponentsInChildren<Camera>(true)) c.enabled = false;
            foreach (var r in go.GetComponentsInChildren<ReflectionProbe>(true)) r.enabled = false;
            go.transform.SetParent(holder.transform, true);
        }
        holder.transform.position = Offset;
        foreach (var c in holder.GetComponentsInChildren<Collider>(true)) if (c.GetComponent<MeshRenderer>() == null) c.enabled = false;
        int n = 0;
        foreach (var r in holder.GetComponentsInChildren<MeshRenderer>(true))
        {
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            var mc = r.GetComponent<MeshCollider>();
            if (r.bounds.size.magnitude > 400f || !r.gameObject.activeInHierarchy || !r.enabled) { if (mc) mc.enabled = false; continue; }
            if (mc == null) mc = r.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh; mc.enabled = true; n++;
        }
        Physics.SyncTransforms();
        return $"opened {scenePath} renderers-with-collider={n}";
    }

    public static void CloseComplex()
    {
        for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
        {
            var s = SceneManager.GetSceneAt(i);
            if (s.path.Contains("/Gyeongbokgung/") && s.isLoaded) EditorSceneManager.CloseScene(s, true);
        }
    }

    static GameObject Holder() { return GameObject.Find(HolderName); }

    static List<MeshRenderer> Renderers()
    {
        var h = Holder();
        return h.GetComponentsInChildren<MeshRenderer>(true)
            .Where(r => r.enabled && r.gameObject.activeInHierarchy && r.GetComponent<MeshFilter>() && r.GetComponent<MeshFilter>().sharedMesh)
            .ToList();
    }

    static long Tris(Mesh m) { long t = 0; for (int i = 0; i < m.subMeshCount; i++) t += m.GetIndexCount(i) / 3; return t; }

    // 따로 굽고 따로 놓아야 하는 덩어리 — 작아도 SMALL 로 합치지 않는다.
    // 광화문 해태상은 1870년(고종 7), 월대는 1866년(고종 3) 조성 기록이라 인종·명종대 배경에서 끌 수 있게 분리.
    static readonly HashSet<string> KeepSeparate = new HashSet<string> { "HAETAE", "WOLDAE" };
    static readonly string[] WoldaeParts = { "Woldae", "Balustrade", "Cornerstone", "Dongjaju", "Stairs", "Stairdecoration", "Stereobate", "Strereobate", "Auspiciousanimal" };

    public static string CodeOf(string meshName)
    {
        if (meshName.StartsWith("SM_GHM_"))
        {
            if (meshName.IndexOf("Haetae", StringComparison.OrdinalIgnoreCase) >= 0) return "HAETAE";
            foreach (var w in WoldaeParts) if (meshName.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0) return "WOLDAE";
        }
        var p = meshName.Split('_');
        if (p[0] != "SM" || p.Length < 2) return "MISC";
        string code = p[1];
        if (p.Length > 2 && (p[2] == "HG" || p[2] == "IN")) code += "_" + p[2];
        return code;
    }

    // ---------- 2. 보이는 부재 판정 ----------
    // 1차: 마당 눈높이(1.7 m, 지붕 밑 제외) 격자 + 먼 위쪽 시점에서 무작위 레이. 2차: 한 번도 안 맞은 부재는
    // 표면 점 24개 → 시점 40개로 역방향 검사. 둘 다 실패하면 실내(안 보임).
    public static string RunVisibility(string complex, float gridStep = 2.5f)
    {
        var rends = Renderers();
        Bounds B = default; bool first = true;
        foreach (var r in rends) { if (r.bounds.size.magnitude > 400f) continue; if (first) { B = r.bounds; first = false; } else B.Encapsulate(r.bounds); }
        var rnd = new System.Random(1);
        var eyes = new List<Vector3>(); var far = new List<Vector3>();
        float margin = 25f;
        for (float x = B.min.x - margin; x <= B.max.x + margin; x += gridStep)
            for (float z = B.min.z - margin; z <= B.max.z + margin; z += gridStep)
            {
                var hits = Physics.RaycastAll(new Vector3(x, B.max.y + 30, z), Vector3.down, B.size.y + 80);
                float gy = B.min.y; if (hits.Length > 0) gy = hits.Min(h => h.point.y);
                var eye = new Vector3(x, gy + 1.7f, z);
                if (Physics.Raycast(eye, Vector3.up, 25f)) continue;
                eyes.Add(eye);
            }
        float dist = Mathf.Max(140f, B.extents.magnitude * 2.2f);
        for (int az = 0; az < 360; az += 15) foreach (float el in new[] { 8f, 25f, 45f, 70f })
                far.Add(B.center - (Quaternion.Euler(-el, az, 0) * Vector3.forward) * dist);

        // pass 1
        int raysEye = 500, raysFar = 1500;
        int total = eyes.Count * raysEye + far.Count * raysFar;
        var visible = new HashSet<int>();
        {
            var cmds = new NativeArray<RaycastCommand>(total, Allocator.TempJob);
            var res = new NativeArray<RaycastHit>(total, Allocator.TempJob);
            int k = 0; var qp = QueryParameters.Default;
            foreach (var o in eyes)
                for (int j = 0; j < raysEye; j++)
                {
                    double u = rnd.NextDouble(), v = rnd.NextDouble();
                    float elev = Mathf.Asin((float)(-0.35 + 1.35 * u)); float azr = (float)(v * 2 * Math.PI);
                    var dir = new Vector3(Mathf.Cos(elev) * Mathf.Cos(azr), Mathf.Sin(elev), Mathf.Cos(elev) * Mathf.Sin(azr));
                    cmds[k++] = new RaycastCommand(o, dir, qp, dist * 2);
                }
            foreach (var o in far)
                for (int j = 0; j < raysFar; j++)
                {
                    var tp = new Vector3(B.min.x + (float)rnd.NextDouble() * B.size.x, B.min.y + (float)rnd.NextDouble() * B.size.y, B.min.z + (float)rnd.NextDouble() * B.size.z);
                    cmds[k++] = new RaycastCommand(o, (tp - o).normalized, qp, dist * 2);
                }
            RaycastCommand.ScheduleBatch(cmds, res, 256, 1).Complete();
            for (int i = 0; i < k; i++) { var c = res[i].collider; if (c) visible.Add(c.gameObject.GetInstanceID()); }
            cmds.Dispose(); res.Dispose();
        }
        int pass1 = rends.Count(r => visible.Contains(r.gameObject.GetInstanceID()));

        // pass 2
        var targets = rends.Where(r => !visible.Contains(r.gameObject.GetInstanceID()) && r.GetComponent<MeshCollider>() && r.GetComponent<MeshCollider>().enabled).ToList();
        int ptsPer = 24, vpPer = 40;
        int batchTargets = 1500;
        for (int bStart = 0; bStart < targets.Count; bStart += batchTargets)
        {
            var sub = targets.Skip(bStart).Take(batchTargets).ToList();
            int tot = sub.Count * ptsPer * vpPer;
            var cmds = new NativeArray<RaycastCommand>(tot, Allocator.TempJob);
            var res = new NativeArray<RaycastHit>(tot, Allocator.TempJob);
            var owner = new int[tot]; int k = 0; var qp = QueryParameters.Default;
            for (int ti = 0; ti < sub.Count; ti++)
            {
                var r = sub[ti]; var mesh = r.GetComponent<MeshFilter>().sharedMesh; var tr = r.transform;
                var verts = mesh.vertices; var tris = mesh.triangles; int triN = tris.Length / 3;
                if (triN == 0) continue;
                for (int pi = 0; pi < ptsPer; pi++)
                {
                    int t = rnd.Next(triN);
                    var a = tr.TransformPoint(verts[tris[t * 3]]); var b = tr.TransformPoint(verts[tris[t * 3 + 1]]); var c = tr.TransformPoint(verts[tris[t * 3 + 2]]);
                    float u = (float)rnd.NextDouble(), v = (float)rnd.NextDouble(); if (u + v > 1) { u = 1 - u; v = 1 - v; }
                    var p = a + (b - a) * u + (c - a) * v;
                    for (int vi = 0; vi < vpPer; vi++)
                    {
                        Vector3 o = (vi % 2 == 0 && eyes.Count > 0) ? eyes[rnd.Next(eyes.Count)] : far[rnd.Next(far.Count)];
                        var d = p - o; float dl = d.magnitude; if (dl < 0.01f) continue;
                        cmds[k] = new RaycastCommand(o, d / dl, qp, dl + 0.05f); owner[k] = ti; k++;
                    }
                }
            }
            RaycastCommand.ScheduleBatch(cmds.GetSubArray(0, k), res.GetSubArray(0, k), 256, 1).Complete();
            for (int i = 0; i < k; i++) { var col = res[i].collider; if (col && col.gameObject == sub[owner[i]].gameObject) visible.Add(sub[owner[i]].gameObject.GetInstanceID()); }
            cmds.Dispose(); res.Dispose();
        }
        var dir2 = Path.Combine(BakeRoot, complex); Directory.CreateDirectory(dir2);
        var sb = new StringBuilder();
        long visT = 0, hidT = 0; int visN = 0, hidN = 0;
        foreach (var r in rends)
        {
            bool v = visible.Contains(r.gameObject.GetInstanceID());
            var m = r.GetComponent<MeshFilter>().sharedMesh; long t = Tris(m);
            if (v) { visN++; visT += t; } else { hidN++; hidT += t; }
            sb.Append(r.gameObject.GetInstanceID()).Append('\t').Append(v ? 1 : 0).Append('\t').Append(m.name).Append('\t').Append(t).Append('\n');
        }
        File.WriteAllText(Path.Combine(dir2, "vis.tsv"), sb.ToString());
        return $"{complex}: eyes={eyes.Count} far={far.Count} pass1={pass1} visible={visN}({visT} tris) hidden={hidN}({hidT} tris)";
    }

    static Dictionary<int, bool> LoadVis(string complex)
    {
        var d = new Dictionary<int, bool>();
        foreach (var l in File.ReadAllLines(Path.Combine(BakeRoot, complex, "vis.tsv"))) { var p = l.Split('\t'); d[int.Parse(p[0])] = p[1] == "1"; }
        return d;
    }

    // ---------- 3. 덩어리 나누기 ----------
    // 코드(SM_<CODE>_)별로 묶고, 삼각형이 maxTris 를 넘으면 XZ 로 긴 축을 반씩 자른다.
    public class Chunk { public string name, code; public List<MeshRenderer> rs = new List<MeshRenderer>(); public long tris; }

    public static List<Chunk> BuildChunks(string complex, long maxTris, bool includeHidden)
    {
        var vis = LoadVis(complex);
        var byCode = new SortedDictionary<string, List<MeshRenderer>>();
        foreach (var r in Renderers().OrderBy(r => r.transform.position.x).ThenBy(r => r.transform.position.z).ThenBy(r => r.GetComponent<MeshFilter>().sharedMesh.name))
        {
            bool v; if (!vis.TryGetValue(r.gameObject.GetInstanceID(), out v)) continue;
            if (!v && !includeHidden) continue;
            if (r.bounds.size.magnitude > 400f) continue;
            var mnm = r.GetComponent<MeshFilter>().sharedMesh.name;
            // 데모용 바닥 판(Plane)은 버리고, 물(Water)은 굽지 않고 원본을 쓴다(구우면 흰 판이 됨)
            if (mnm == "Plane" || mnm.StartsWith("Plane") || mnm.IndexOf("Water", StringComparison.OrdinalIgnoreCase) >= 0) continue;
            // 광화문 데모의 길·잔디·바닥(폭 140~170 m)도 데모용 바닥이라 버린다
            if (IsDemoGround(mnm)) continue;
            // 궐문 문짝은 원본을 따로 두어 열 수 있게 하고(굽지 않음), 사정전 실내 소품은 쓰지 않는다(사용자 지시)
            if (IsGateLeaf(r.GetComponent<MeshFilter>().sharedMesh) || IsExcludedProp(complex, mnm)) continue;
            string code = CodeOf(mnm);
            if (code.EndsWith("_IN")) code = code.Substring(0, code.Length - 3);
            if (!byCode.ContainsKey(code)) byCode[code] = new List<MeshRenderer>();
            byCode[code].Add(r);
        }
        // 작은 코드(6만 삼각형 미만)는 아틀라스를 따로 만들면 낭비라 SMALL 한 덩어리로 합친다
        var small = new List<MeshRenderer>();
        foreach (var key in byCode.Keys.ToList()) { if (KeepSeparate.Contains(key)) continue; long tt = byCode[key].Sum(r => Tris(r.GetComponent<MeshFilter>().sharedMesh)); if (tt < 60000) { small.AddRange(byCode[key]); byCode.Remove(key); } }
        if (small.Count > 0) byCode["SMALL"] = small;
        var result = new List<Chunk>();
        foreach (var kv in byCode) Split(kv.Key, kv.Key, kv.Value, maxTris, result);
        return result;
    }

    static void Split(string code, string name, List<MeshRenderer> rs, long maxTris, List<Chunk> outList)
    {
        long t = rs.Sum(r => Tris(r.GetComponent<MeshFilter>().sharedMesh));
        if (t <= maxTris || rs.Count < 2)
        {
            outList.Add(new Chunk { code = code, name = name, rs = rs, tris = t });
            return;
        }
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        bool alongX = b.size.x >= b.size.z;
        var sorted = rs.OrderBy(r => alongX ? r.bounds.center.x : r.bounds.center.z).ToList();
        long acc = 0, half = t / 2; int cut = 0;
        for (int i = 0; i < sorted.Count; i++) { acc += Tris(sorted[i].GetComponent<MeshFilter>().sharedMesh); if (acc >= half) { cut = i + 1; break; } }
        cut = Mathf.Clamp(cut, 1, sorted.Count - 1);
        Split(code, name + "a", sorted.Take(cut).ToList(), maxTris, outList);
        Split(code, name + "b", sorted.Skip(cut).ToList(), maxTris, outList);
    }

    public static string ListChunks(string complex, long maxTris = 2500000)
    {
        var ch = BuildChunks(complex, maxTris, false);
        return string.Join("; ", ch.Select((c, i) => $"{i}:{c.name} r={c.rs.Count} tris={c.tris}"));
    }

    // ---------- 4. OBJ 내보내기(한 덩어리씩) ----------
    public static string ExportChunk(string complex, int index, long maxTris = 2500000)
    {
        var ch = BuildChunks(complex, maxTris, false);
        if (index >= ch.Count) return "ALLDONE " + ch.Count;
        var c = ch[index];
        var dir = Path.Combine(BakeRoot, complex); Directory.CreateDirectory(dir);
        // MCP 가 시간 초과로 같은 호출을 다시 보내면 이미 내보낸 덩어리는 건너뛴다(chunks.tsv 에 이름이 있으면 끝난 것)
        var tsvPath = Path.Combine(dir, "chunks.tsv");
        if (File.Exists(tsvPath) && File.ReadAllLines(tsvPath).Any(l => l.Split('\t')[0] == c.name)) return $"SKIP {index}/{ch.Count} {c.name}";
        var off = Holder().transform.position;
        var matSeen = new HashSet<string>(); var mtl = new StringBuilder();
        string proj = Directory.GetCurrentDirectory();
        long tris = 0; int vbase = 1, vtCount = 0, oi = 0;
        using (var w = new StreamWriter(Path.Combine(dir, c.name + ".obj"), false, new UTF8Encoding(false), 1 << 20))
        {
            w.Write("mtllib " + c.name + ".mtl\n");
            foreach (var r in c.rs)
            {
                var mesh = r.GetComponent<MeshFilter>().sharedMesh; var tr = r.transform; var mats = r.sharedMaterials;
                var v = mesh.vertices; var n = mesh.normals; var uv0 = mesh.uv;
                bool hasN = n != null && n.Length == v.Length; bool hasUV = uv0 != null && uv0.Length == v.Length;
                w.Write("o " + mesh.name + "__" + (oi++) + "\n");
                var vtBlocks = new Dictionary<Vector4, int>();   // (UV 배율, 오프셋) 별로 vt 를 한 번만 쓴다(예전엔 서브메시마다 다시 써서 OBJ 가 수백 MB)
                for (int i = 0; i < v.Length; i++) { var p = tr.TransformPoint(v[i]) - off; w.Write("v " + (-p.x).ToString("F5", Inv) + " " + p.y.ToString("F5", Inv) + " " + p.z.ToString("F5", Inv) + "\n"); }
                if (hasN) for (int i = 0; i < n.Length; i++) { var d = tr.TransformDirection(n[i]); w.Write("vn " + (-d.x).ToString("F4", Inv) + " " + d.y.ToString("F4", Inv) + " " + d.z.ToString("F4", Inv) + "\n"); }
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    var m = s < mats.Length ? mats[s] : null; if (m == null) continue;
                    if (GateLeafMaterials.Contains(m.name)) continue;   // 한 메시 안의 문짝 서브메시(광화문)는 굽지 않음
                    Vector2 sc = m.HasProperty("_BaseColorMap") ? m.GetTextureScale("_BaseColorMap") : Vector2.one;
                    Vector2 of = m.HasProperty("_BaseColorMap") ? m.GetTextureOffset("_BaseColorMap") : Vector2.zero;
                    string mname = m.name.Replace(' ', '_') + "_" + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(m)).Substring(0, 6);
                    if (matSeen.Add(mname))
                    {
                        var bc = m.HasProperty("_BaseColorMap") ? m.GetTexture("_BaseColorMap") : null;
                        var nm = m.HasProperty("_NormalMap") ? m.GetTexture("_NormalMap") : null;
                        var col = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
                        mtl.Append("newmtl " + mname + "\nKd " + col.r.ToString("F4", Inv) + " " + col.g.ToString("F4", Inv) + " " + col.b.ToString("F4", Inv) + "\n");
                        if (bc) mtl.Append("map_Kd " + Path.Combine(proj, AssetDatabase.GetAssetPath(bc)).Replace('\\', '/') + "\n");
                        if (nm) mtl.Append("norm " + Path.Combine(proj, AssetDatabase.GetAssetPath(nm)).Replace('\\', '/') + "\n");
                        mtl.Append("\n");
                    }
                    // 한 메시에 기와·목부·창호·석재가 다 들어 있는 경우(광화문 SM_GHM_Gwanghwamun 등) 서브메시마다
                    // 오브젝트를 나누고 이름에 종류 힌트를 붙여 굽는 쪽이 종류별 아틀라스로 나누게 한다.
                    string hint = SubmeshCategoryHint(m.name);
                    if (hint != null) w.Write("o " + mesh.name + "__" + m.name.Replace(' ', '_') + hint + "__" + (oi++) + "\n");
                    int vtStart;
                    var stKey = new Vector4(sc.x, sc.y, of.x, of.y);
                    if (!vtBlocks.TryGetValue(stKey, out vtStart))
                    {
                        vtStart = vtCount + 1; vtBlocks[stKey] = vtStart;
                        if (hasUV) { for (int i = 0; i < uv0.Length; i++) { var u = uv0[i]; w.Write("vt " + (u.x * sc.x + of.x).ToString("F5", Inv) + " " + (u.y * sc.y + of.y).ToString("F5", Inv) + "\n"); } vtCount += uv0.Length; }
                    }
                    w.Write("usemtl " + mname + "\n");
                    var idx = mesh.GetTriangles(s);
                    for (int t = 0; t < idx.Length; t += 3)
                    {
                        w.Write("f");
                        foreach (int kk in new[] { idx[t], idx[t + 2], idx[t + 1] })
                        {
                            w.Write(" " + (vbase + kk));
                            if (hasUV) w.Write("/" + (vtStart + kk)); else if (hasN) w.Write("/");
                            if (hasN) w.Write("/" + (vbase + kk));
                        }
                        w.Write("\n"); tris++;
                    }
                }
                vbase += v.Length;
            }
        }
        File.WriteAllText(Path.Combine(dir, c.name + ".mtl"), mtl.ToString());
        File.AppendAllText(Path.Combine(dir, "chunks.tsv"), c.name + "\t" + c.code + "\t" + c.rs.Count + "\t" + tris + "\n");
        return $"{index}/{ch.Count} {c.name} r={c.rs.Count} tris={tris}";
    }

    // 재질 이름 → 굽기 종류 힌트(오브젝트 이름에 붙이면 굽기 스크립트의 키워드에 걸림). 없으면 null(메시 이름으로 판단).
    static readonly Dictionary<string, string> MatCategory = new Dictionary<string, string>
    {
        // 광화문 문루: 기와(담장 기와 Top01A 포함)·용마루·장식기와
        {"M_GHM_Top01A","roof"},{"M_GHM_Top02A","roof"},{"M_GHM_1F_Top01A","roof"},{"M_GHM_T0301A","roof"},{"M_GHM_Stone01A","roof"},
        // 창호·문짝·현판
        {"M_GHM_Win01A","win"},{"M_GHM_HangingBoard","win"},{"M_GHM_Gate01A","win"},{"M_GHM_Gate02A","win"},{"M_GHM_Gate04A","win"},
        // 석축·담장·홍예
        {"M_GHM_Gw01A","stone"},{"M_GHM_Gw02A","stone"},{"M_GHM_Ws01A","stone"},{"M_GHM_Ws02A","stone"},{"M_GHM_Ws03A","stone"},{"M_GHM_W01A","stone"},
        {"M_GHM_Wb01A","stone"},{"M_GHM_Gst01A","stone"},{"M_GHM_3D01A","stone"},{"M_GHM_3D02A","stone"},{"M_GHM_Gp01A","stone"},{"M_GHM_Gate03A","stone"},{"M_GHM_Mark01A","stone"},
        // 단청 목부
        {"M_GHM_Symbol01A","wood"},{"M_GHM_Symbol02A","wood"},{"M_GHM_Symbol03A","wood"},{"M_GHM_Symbol04A","wood"},{"M_GHM_Fw01A","wood"},{"M_GHM_1F_Wood","wood"},{"M_GHM_Pole01A","wood"},
    };
    static string SubmeshCategoryHint(string matName)
    {
        string c; if (!MatCategory.TryGetValue(matName, out c)) return null;
        // 창호 힌트에 "window" 를 쓰면 굽기 쪽에서 상자 대체(proxy)가 돼서 "hyeonpan" 으로 붙인다
        switch (c) { case "roof": return "_roof_"; case "win": return "_hyeonpan_"; case "stone": return "_stone_"; default: return "_wd_"; }
    }

    // ---------- 궐문 문짝 / 사정전 실내 소품 / 데모 바닥 ----------
    // 궐문(궁 대문 + 단지 안 문 + 행각 문) 문짝 메시 이름 앞부분. 건물 창호(분합문 등)는 넣지 않는다.
    // 경회루 DoorE/F/H/I 는 크기(폭 0.9~1.6 m, 높이 2.6~3.7 m)로 담장 문으로 판단 — 어느 문인지는 확인 필요.
    static readonly string[] GateLeafPrefixes = {
        "SM_GJM_Door", "SM_GJJ_HG_Door", "SM_HNM_Door_", "SM_SJM_Door", "SM_SJJ_HG_Door", "SM_HOM_Door", "SM_GNJ_HG_Door",
        "SM_YEM_Door", "SM_GTJ_HG_Door", "SM_DG_HG_Door", "SM_KHL_DoorE", "SM_KHL_DoorF", "SM_KHL_DoorH", "SM_KHL_DoorI",
        "SM_GCM_Door0", "SM_YCM_Door0", "SM_SMM_Door0", "SM_HSM_Door", "SM_YSM_Door" };
    public static bool IsGateLeaf(string meshName)
    {
        var l = meshName.ToLowerInvariant();
        if (l.Contains("frame") || l.Contains("handle") || l.Contains("lock")) return false;
        foreach (var p in GateLeafPrefixes) if (meshName.StartsWith(p, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
    // 행각 문(_HG_Door)은 방문(폭 0.6~0.9 m)이 섞여 있어 폭 1.1 m 이상만 궐문 문짝으로 본다(강녕전 행각 방문 440여 짝 제외)
    public static bool IsGateLeaf(Mesh m)
    {
        if (m == null || !IsGateLeaf(m.name)) return false;
        if (m.name.IndexOf("_HG_Door", StringComparison.OrdinalIgnoreCase) >= 0) { var sz = m.bounds.size; return Mathf.Max(sz.x, sz.z) >= 1.1f; }
        return true;
    }
    // 사정전(사정전·천추전·만춘전) 실내 소품 — 사용자 지시로 되살리지 않음
    static readonly string[] SajeongjeonPropKeys = { "_Book", "Cabinet", "Cabient", "_Candle", "_Light", "_Table", "_Desk", "_Throne", "Ilwolobongdo",
        "FoldingScreen", "Paperweight", "OpenBook", "InkStone", "BrushStand", "WaterDropper", "Blanket", "IncenseBurner", "WhitePorcelain",
        "_Shelf", "Red-Lacquered", "_King", "_Painting" };
    public static bool IsExcludedProp(string complex, string meshName)
    {
        // 경회루 2층 분합문은 실제로 들어 올려 열어 두는 곳이라 문짝을 뺀다(사용자 지시 2026-09-27, 2층을 트인 누마루로)
        if (complex == "Gyeonghoeru")
            return meshName.StartsWith("SM_KHL_Door", StringComparison.Ordinal) && !meshName.StartsWith("SM_KHL_Doorframe", StringComparison.Ordinal);
        if (complex != "Sajeongjeon") return false;
        foreach (var k in SajeongjeonPropKeys) if (meshName.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }
    // 문짝이 큰 메시의 서브메시로 들어 있는 경우(광화문 문루 SM_GHM_Gwanghwamun 의 M_GHM_Gate02A = 홍예 3곳 문짝 6장)
    static readonly HashSet<string> GateLeafMaterials = new HashSet<string> { "M_GHM_Gate02A" };
    static bool HasGateLeafSubmesh(Renderer r) { return r.sharedMaterials.Any(m => m != null && GateLeafMaterials.Contains(m.name)); }

    // ---- 서브메시 안 문짝 나누기(광화문) ----
    // 문짝 서브메시(M_GHM_Gate02A)에는 홍예 3곳의 문판(두 짝이 한 판으로 붙어 있음)과 띠쇠·장식 조각, 그리고 문과 상관없는 작은 철물이 섞여 있다.
    // 1) 위치를 붙여(1 mm) 섬(연결된 삼각형 묶음)으로 나누고 2) 폭 3 m·높이 4 m 넘는 섬 = 문판 3) 문판 둘레 안에 든 섬 = 그 문의 부속
    // 4) 문마다 가운데 x 로 삼각형을 잘라 좌·우 문짝 5) 문과 상관없는 섬은 문루 메시에 남긴다.
    class LeafSplit { public List<int> keepTris = new List<int>(); public List<KeyValuePair<Mesh, Vector3>> leaves = new List<KeyValuePair<Mesh, Vector3>>(); public Material mat; }
    static readonly Dictionary<string, LeafSplit> leafCache = new Dictionary<string, LeafSplit>();

    static LeafSplit SplitLeaves(Mesh src, Material[] mats, string dir)
    {
        LeafSplit res; if (leafCache.TryGetValue(src.name, out res)) return res;
        res = new LeafSplit();
        var v = src.vertices; var n = src.normals; var uv = src.uv; var tg = src.tangents;
        bool hn = n.Length == v.Length, hu = uv.Length == v.Length, ht = tg.Length == v.Length;
        var tris = new List<int>();
        for (int s = 0; s < src.subMeshCount; s++) if (s < mats.Length && mats[s] != null && GateLeafMaterials.Contains(mats[s].name)) { tris.AddRange(src.GetTriangles(s)); res.mat = mats[s]; }
        int triN = tris.Count / 3;
        // 섬 나누기
        var key = new Dictionary<Vector3Int, int>(); var parent = new List<int>(); var vid = new int[tris.Count];
        for (int i = 0; i < tris.Count; i++)
        {
            var q = v[tris[i]]; var k = new Vector3Int(Mathf.RoundToInt(q.x * 1000), Mathf.RoundToInt(q.y * 1000), Mathf.RoundToInt(q.z * 1000));
            int id; if (!key.TryGetValue(k, out id)) { id = parent.Count; key[k] = id; parent.Add(id); }
            vid[i] = id;
        }
        Func<int, int> find = null; find = a => { while (parent[a] != a) { parent[a] = parent[parent[a]]; a = parent[a]; } return a; };
        for (int t = 0; t < triN; t++) { int a = find(vid[t * 3]); parent[find(vid[t * 3 + 1])] = a; parent[find(vid[t * 3 + 2])] = a; }
        var islTris = new Dictionary<int, List<int>>(); var islB = new Dictionary<int, Bounds>();
        for (int t = 0; t < triN; t++)
        {
            int r = find(vid[t * 3]); List<int> l; if (!islTris.TryGetValue(r, out l)) { l = new List<int>(); islTris[r] = l; islB[r] = new Bounds(v[tris[t * 3]], Vector3.zero); }
            l.Add(t); var b = islB[r]; for (int k = 0; k < 3; k++) b.Encapsulate(v[tris[t * 3 + k]]); islB[r] = b;
        }
        var panels = islB.Where(kv => kv.Value.size.x > 3f && kv.Value.size.y > 4f).Select(kv => kv.Value).OrderBy(b => b.center.x).ToList();
        var doorTris = panels.Select(_ => new List<int>()).ToList();
        foreach (var kv in islTris)
        {
            var b = islB[kv.Key]; int owner = -1;
            for (int p = 0; p < panels.Count; p++)
            {
                var P = panels[p];
                if (b.min.x >= P.min.x - 0.05f && b.max.x <= P.max.x + 0.05f && b.min.y >= P.min.y - 0.3f && b.max.y <= P.max.y + 0.3f && Mathf.Abs(b.center.z - P.center.z) < 0.6f) { owner = p; break; }
            }
            if (owner < 0) foreach (var t in kv.Value) { res.keepTris.Add(tris[t * 3]); res.keepTris.Add(tris[t * 3 + 1]); res.keepTris.Add(tris[t * 3 + 2]); }
            else doorTris[owner].AddRange(kv.Value);
        }
        int leafIdx = 0;
        for (int p = 0; p < panels.Count; p++)
        {
            float mid = panels[p].center.x;
            for (int half = 0; half < 2; half++)
            {
                var nv = new List<Vector3>(); var nn = new List<Vector3>(); var nu = new List<Vector2>(); var nt = new List<Vector4>(); var ni = new List<int>();
                foreach (var t in doorTris[p])
                {
                    // 삼각형을 x = mid 평면으로 잘라 이 쪽(half 0 = 왼쪽 x<mid, 1 = 오른쪽) 조각만 남긴다(속성은 선형 보간)
                    var poly = new List<int>(); var polyT = new List<float>(); var polyB = new List<int>();
                    var pv = new List<Vector3>(); var pn = new List<Vector3>(); var pu = new List<Vector2>(); var pt = new List<Vector4>();
                    for (int k = 0; k < 3; k++)
                    {
                        int a = tris[t * 3 + k], b2 = tris[t * 3 + (k + 1) % 3];
                        float da = (v[a].x - mid) * (half == 0 ? -1 : 1), db = (v[b2].x - mid) * (half == 0 ? -1 : 1);
                        if (da >= 0) { pv.Add(v[a]); pn.Add(hn ? n[a] : Vector3.up); pu.Add(hu ? uv[a] : Vector2.zero); pt.Add(ht ? tg[a] : Vector4.zero); }
                        if ((da >= 0) != (db >= 0))
                        {
                            float f = da / (da - db);
                            pv.Add(Vector3.Lerp(v[a], v[b2], f)); pn.Add(hn ? Vector3.Lerp(n[a], n[b2], f).normalized : Vector3.up);
                            pu.Add(hu ? Vector2.Lerp(uv[a], uv[b2], f) : Vector2.zero); pt.Add(ht ? Vector4.Lerp(tg[a], tg[b2], f) : Vector4.zero);
                        }
                    }
                    if (pv.Count < 3) continue;
                    int b0 = nv.Count; nv.AddRange(pv); nn.AddRange(pn); nu.AddRange(pu); nt.AddRange(pt);
                    for (int k = 1; k + 1 < pv.Count; k++) { ni.Add(b0); ni.Add(b0 + k); ni.Add(b0 + k + 1); }
                }
                if (ni.Count == 0) continue;
                var m = new Mesh(); m.name = src.name + "_DoorLeaf" + leafIdx; m.indexFormat = nv.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
                m.SetVertices(nv); m.SetNormals(nn); if (hu) m.SetUVs(0, nu); if (ht) m.SetTangents(nt);
                m.SetTriangles(ni, 0); m.RecalculateBounds();
                var path = dir + "/" + m.name + ".asset";
                if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(m, path);
                var P = panels[p];
                res.leaves.Add(new KeyValuePair<Mesh, Vector3>(m, new Vector3(half == 0 ? P.min.x : P.max.x, P.min.y, P.center.z)));   // 경첩 = 문판 바깥 끝
                leafIdx++;
            }
        }
        leafCache[src.name] = res;
        return res;
    }

    // 문짝을 뺀 문루 메시(.asset) — 원본 LOD0 에서 쓴다(문과 상관없는 같은 재질 조각은 남김)
    static Mesh NoDoorMesh(Mesh src, Material[] mats, string dir)
    {
        var path = dir + "/" + src.name + "_NoDoors.asset";
        var split = SplitLeaves(src, mats, dir);
        var m = UnityEngine.Object.Instantiate(src); m.name = src.name + "_NoDoors";
        bool first = true;
        for (int s = 0; s < m.subMeshCount; s++)
            if (s < mats.Length && mats[s] != null && GateLeafMaterials.Contains(mats[s].name)) { m.SetTriangles(first ? split.keepTris.ToArray() : new int[0], s, false); first = false; }
        m.RecalculateBounds();
        if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    public static bool IsDemoGround(string mnm)
    {
        // 흥례문 데모에는 하늘 구(SM_SkySphere, 메시 없음)도 들어 있음
        return mnm == "Plane" || mnm.StartsWith("Plane") || mnm.StartsWith("SM_GHM_Grass") || mnm.StartsWith("SM_GHM_Road") || mnm.StartsWith("SM_GHM_Ground") || mnm.IndexOf("SkySphere", StringComparison.OrdinalIgnoreCase) >= 0;
    }
    static bool Recent(string assetPath)
    {
        var full = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
        return File.Exists(full) && (DateTime.Now - File.GetLastWriteTime(full)).TotalMinutes < 10;
    }

    // ---------- 5b. 단지 전체 원본 프리팹 ----------
    // 플레이어가 드나드는 단지(행각·담장 포함)는 가까이서 원본 전체를 쓴다. 문짝·데모 바닥·(사정전) 실내 소품은 뺀다.
    public static string SaveOriginalComplexPrefab(string complex)
    {
        var dir = BakedAssetRoot + "/" + complex; EnsureFolder(dir);
        var path = dir + "/" + complex + "_Original.prefab";
        if (Recent(path)) return "RECENT " + path;
        var holder = Holder();
        var root = new GameObject(complex + "_Original");
        SceneManager.MoveGameObjectToScene(root, holder.scene);
        int kept = 0, removed = 0;
        var tops = new List<Transform>(); foreach (Transform t in holder.transform) tops.Add(t);
        foreach (var t in tops)
        {
            if (t.GetComponentInChildren<MeshRenderer>(true) == null) continue;
            var copy = UnityEngine.Object.Instantiate(t.gameObject, root.transform);
            copy.name = t.name;
            copy.transform.position = t.position - holder.transform.position; copy.transform.rotation = t.rotation; copy.transform.localScale = t.lossyScale;
            foreach (var r in copy.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) { UnityEngine.Object.DestroyImmediate(r); if (mf) UnityEngine.Object.DestroyImmediate(mf); removed++; continue; }   // 메시 없는 렌더러(흥례문 SM_SkySphere 등)
                var mnm = mf.sharedMesh.name;
                if (HasGateLeafSubmesh(r)) mf.sharedMesh = NoDoorMesh(mf.sharedMesh, r.sharedMaterials, dir);
                if (IsGateLeaf(mf.sharedMesh) || IsExcludedProp(complex, mnm) || IsDemoGround(mnm))
                {
                    var mc = r.GetComponent<MeshCollider>(); if (mc) UnityEngine.Object.DestroyImmediate(mc);
                    UnityEngine.Object.DestroyImmediate(r); UnityEngine.Object.DestroyImmediate(mf); removed++;
                }
                else kept++;
            }
            // 조명은 굽기 전용 장면에서 가져오지 않음(HDAdditionalLightData 가 Light 에 묶여 있어 컴포넌트만 지울 수 없음 → 렌더러 없는 조명 오브젝트는 통째로, 아니면 끔)
            foreach (var l in copy.GetComponentsInChildren<Light>(true)) { if (l == null) continue; if (l.GetComponent<MeshRenderer>() == null && l.gameObject != copy && l.GetComponentInChildren<MeshRenderer>(true) == null) UnityEngine.Object.DestroyImmediate(l.gameObject); else l.enabled = false; }
            foreach (var cam in copy.GetComponentsInChildren<Camera>(true)) UnityEngine.Object.DestroyImmediate(cam);
            foreach (var rp in copy.GetComponentsInChildren<ReflectionProbe>(true)) UnityEngine.Object.DestroyImmediate(rp);
            foreach (var cc in copy.GetComponentsInChildren<Collider>(true)) cc.enabled = true;
            // 작은 부재(대각선 1.5 m 미만: 소로·소품 등)는 그림자를 끔 — 그림자 드로우콜이 원본 렌더러 수만큼 늘어남
            foreach (var sr in copy.GetComponentsInChildren<MeshRenderer>(true)) if (sr.bounds.size.magnitude < 1.5f) sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (copy.GetComponentInChildren<MeshRenderer>(true) == null) UnityEngine.Object.DestroyImmediate(copy);
        }
        PrefabUtility.SaveAsPrefabAsset(root, path);
        UnityEngine.Object.DestroyImmediate(root);
        return $"saved {path} kept={kept} removed={removed}";
    }

    // ---------- 5c. 문짝 프리팹(경첩 피벗) ----------
    // 문짝마다 경첩 쪽 아래 모서리에 피벗 오브젝트를 두고 그 아래에 원본 문짝을 넣는다(피벗을 Y 축으로 돌리면 열림).
    // 경첩 쪽 = 옆에 짝 문짝이 있으면 그 반대쪽, 없으면 폭 축의 − 쪽(확인 필요).
    public static string SaveDoorsPrefab(string complex)
    {
        var dir = BakedAssetRoot + "/" + complex; EnsureFolder(dir);
        var path = dir + "/" + complex + "_Doors.prefab";
        if (Recent(path)) return "RECENT " + path;
        var holder = Holder(); var off = holder.transform.position;
        var leaves = Renderers().Where(r => IsGateLeaf(r.GetComponent<MeshFilter>().sharedMesh)).ToList();
        var subLeafOwners = Renderers().Where(r => HasGateLeafSubmesh(r)).ToList();
        if (leaves.Count == 0 && subLeafOwners.Count == 0) return "no doors " + complex;
        var root = new GameObject(complex + "_Doors");
        SceneManager.MoveGameObjectToScene(root, holder.scene);
        int i = 0;
        foreach (var owner in subLeafOwners)
        {
            var split = SplitLeaves(owner.GetComponent<MeshFilter>().sharedMesh, owner.sharedMaterials, dir);
            var leafMat = split.mat;
            foreach (var kv in split.leaves)
            {
                var hinge = new GameObject("DoorHinge_" + (i++).ToString("000") + "_" + kv.Key.name);
                hinge.transform.SetParent(root.transform, false);
                hinge.transform.position = owner.transform.TransformPoint(kv.Value) - off;
                hinge.transform.rotation = owner.transform.rotation;
                var leaf = new GameObject(kv.Key.name);
                leaf.transform.SetParent(hinge.transform, false);
                leaf.transform.position = owner.transform.position - off; leaf.transform.rotation = owner.transform.rotation; leaf.transform.localScale = owner.transform.lossyScale;
                leaf.AddComponent<MeshFilter>().sharedMesh = kv.Key;
                var lr = leaf.AddComponent<MeshRenderer>(); lr.sharedMaterial = leafMat;
                leaf.AddComponent<MeshCollider>().sharedMesh = kv.Key;
                var lg0 = hinge.AddComponent<LODGroup>(); lg0.SetLODs(new[] { new LOD(0.01f, new Renderer[] { lr }) }); lg0.RecalculateBounds();
            }
        }
        foreach (var r in leaves)
        {
            var b = r.bounds; bool alongX = b.size.x >= b.size.z;
            Vector3 axis = alongX ? Vector3.right : Vector3.forward;
            float half = alongX ? b.extents.x : b.extents.z;
            int side = -1;
            foreach (var o in leaves)
            {
                if (o == r) continue; var ob = o.bounds;
                float perp = alongX ? Mathf.Abs(ob.center.z - b.center.z) : Mathf.Abs(ob.center.x - b.center.x);
                float along = alongX ? ob.center.x - b.center.x : ob.center.z - b.center.z;
                if (perp < 0.8f && Mathf.Abs(along) < half * 2f + 0.5f && Mathf.Abs(ob.center.y - b.center.y) < 1f) { side = along > 0 ? -1 : 1; break; }
            }
            var hingeW = b.center + axis * (side * half); hingeW.y = b.min.y;
            var hinge = new GameObject("DoorHinge_" + (i++).ToString("000") + "_" + r.GetComponent<MeshFilter>().sharedMesh.name);
            hinge.transform.SetParent(root.transform, false);
            hinge.transform.position = hingeW - off;
            var copy = UnityEngine.Object.Instantiate(r.gameObject);
            copy.name = r.gameObject.name;
            foreach (Transform ch in copy.transform.Cast<Transform>().ToList()) UnityEngine.Object.DestroyImmediate(ch.gameObject);
            copy.transform.SetParent(hinge.transform, false);
            copy.transform.position = r.transform.position - off; copy.transform.rotation = r.transform.rotation; copy.transform.localScale = r.transform.lossyScale;
            foreach (var cc in copy.GetComponentsInChildren<Collider>(true)) cc.enabled = true;
            var lg = hinge.AddComponent<LODGroup>();
            lg.SetLODs(new[] { new LOD(0.01f, copy.GetComponentsInChildren<Renderer>(true)) });
            lg.RecalculateBounds();
        }
        PrefabUtility.SaveAsPrefabAsset(root, path);
        UnityEngine.Object.DestroyImmediate(root);
        return $"saved {path} doors={i}";
    }

    // ---------- 7b. 단지 프리팹 조립(전체 원본 LOD + 문짝) ----------
    // fullOriginal: LODGroup(LOD0 = <단지>_Original 전체, LOD1 = 구운 덩어리 전체 + 원본 물). 아니면 구운 덩어리만.
    // 문짝(<단지>_Doors)은 LOD 밖에 둔다(멀리서도 열린 상태가 보이게).
    public static string AssembleComplex2(string complex, bool fullOriginal, float lod0ScreenHeight = 1.0f)   // 0.25 면 단지 크기의 약 3.5배 거리까지 원본이라 궁 어디서든 7개 단지 원본을 다 그려 매우 느렸음
    {
        var dir = BakedAssetRoot + "/" + complex;
        var root = new GameObject(complex);
        var baked = new List<GameObject>();
        foreach (var g in AssetDatabase.FindAssets("t:Prefab", new[] { dir }))
        {
            var p = AssetDatabase.GUIDToAssetPath(g); if (!p.EndsWith("_Baked.prefab")) continue;
            baked.Add((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(p)));
        }
        var orig = AssetDatabase.LoadAssetAtPath<GameObject>(dir + "/" + complex + "_Original.prefab");
        if (fullOriginal && orig != null)
        {
            var grp = new GameObject("LOD"); grp.transform.SetParent(root.transform, false);
            var o = (GameObject)PrefabUtility.InstantiatePrefab(orig); o.transform.SetParent(grp.transform, false);
            foreach (var b in baked) b.transform.SetParent(grp.transform, false);
            var r0 = o.GetComponentsInChildren<Renderer>(true);
            var r1 = baked.SelectMany(b => b.GetComponentsInChildren<Renderer>(true))
                .Concat(r0.Where(x => x.GetComponent<MeshFilter>() && x.GetComponent<MeshFilter>().sharedMesh && x.GetComponent<MeshFilter>().sharedMesh.name.IndexOf("Water", StringComparison.OrdinalIgnoreCase) >= 0)).ToArray();
            var lg = grp.AddComponent<LODGroup>();
            lg.SetLODs(new[] { new LOD(lod0ScreenHeight, r0), new LOD(0.005f, r1) });
            lg.RecalculateBounds();
        }
        else foreach (var b in baked) b.transform.SetParent(root.transform, false);
        var doors = AssetDatabase.LoadAssetAtPath<GameObject>(dir + "/" + complex + "_Doors.prefab");
        if (doors != null) { var d = (GameObject)PrefabUtility.InstantiatePrefab(doors); d.transform.SetParent(root.transform, false); }
        // 지붕은 칸마다 따로 LOD(PalaceRoofLodTool) — 단지 LOD 밖에 둔다(단지 전체가 한 번에 바뀌면 단지 안에서 먼 기와도 원본이라 지글거렸음)
        var roofLod = AssetDatabase.LoadAssetAtPath<GameObject>(dir + "/" + complex + "_RoofLOD.prefab");
        if (roofLod != null) { var rl = (GameObject)PrefabUtility.InstantiatePrefab(roofLod); rl.transform.SetParent(root.transform, false); }
        var path2 = dir + "/" + complex + "_Complex.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path2);
        UnityEngine.Object.DestroyImmediate(root);
        return $"assembled {path2} baked={baked.Count} original={(fullOriginal && orig != null)} doors={(doors != null)} roofLod={(roofLod != null)}";
    }

    // ---------- 5. 원본(진입 가능 건물) 프리팹 ----------
    // 진입 가능 건물은 원본(실내 포함)을 LOD0, 구운 외관을 LOD1 로 쓴다. 원본 부재를 한 루트 아래 모아 프리팹으로 저장.
    public static string SaveOriginalPrefab(string complex, string code)
    {
        var holder = Holder();
        var root = new GameObject(complex + "_" + code + "_Original");
        SceneManager.MoveGameObjectToScene(root, holder.scene);
        root.transform.position = Vector3.zero;
        int n = 0;
        var tops = new HashSet<Transform>();
        foreach (var r in Renderers())
        {
            string c = CodeOf(r.GetComponent<MeshFilter>().sharedMesh.name); if (c.EndsWith("_IN")) c = c.Substring(0, c.Length - 3);
            if (c != code) continue;
            var t = r.transform; while (t.parent != null && t.parent != holder.transform) t = t.parent;
            tops.Add(t);
        }
        foreach (var t in tops)
        {
            var copy = UnityEngine.Object.Instantiate(t.gameObject, root.transform);
            copy.name = t.name;
            copy.transform.position = t.position - holder.transform.position; copy.transform.rotation = t.rotation; copy.transform.localScale = t.lossyScale;
            foreach (var cc in copy.GetComponentsInChildren<Collider>(true)) cc.enabled = true;
            n++;
        }
        var dir = BakedAssetRoot + "/" + complex; EnsureFolder(dir);
        var path = dir + "/" + complex + "_" + code + "_Original.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path);
        UnityEngine.Object.DestroyImmediate(root);
        return $"saved {path} tops={n}";
    }

    static void EnsureFolder(string rel)
    {
        var parts = rel.Split('/'); string cur = parts[0];
        for (int i = 1; i < parts.Length; i++) { string next = cur + "/" + parts[i]; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]); cur = next; }
    }

    // ---------- 6. 구운 결과 가져오기 ----------
    public static string ImportBaked(string complex)
    {
        var src = Path.Combine(BakeRoot, complex, "out");
        var dstRel = BakedAssetRoot + "/" + complex; EnsureFolder(dstRel);
        var dst = Path.Combine(Directory.GetCurrentDirectory(), dstRel.Replace('/', '\\'));
        var names = new List<string>();
        foreach (var f in Directory.GetFiles(src))
        {
            var fn = Path.GetFileName(f);
            if (fn.EndsWith(".png") || fn.EndsWith(".fbx")) File.Copy(f, Path.Combine(dst, fn), true);
            if (fn.EndsWith(".fbx")) names.Add(Path.GetFileNameWithoutExtension(fn));
        }
        AssetDatabase.Refresh();
        var reset = Type.GetType("UnityEditor.Rendering.HighDefinition.HDShaderUtils, Unity.RenderPipelines.HighDefinition.Editor").GetMethod("ResetMaterialKeywords", new[] { typeof(Material) });
        var sb = new StringBuilder();
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var name in names)
                foreach (var cat in new[] { "wood", "win", "stone", "roof" })
                {
                    string bcp = dstRel + "/" + name + "_" + cat + "_BC.png", np = dstRel + "/" + name + "_" + cat + "_N.png";
                    var tb = AssetImporter.GetAtPath(bcp) as TextureImporter; var tn = AssetImporter.GetAtPath(np) as TextureImporter;
                    if (tb == null || tn == null) continue;
                    tb.sRGBTexture = true; tb.maxTextureSize = 4096; tb.streamingMipmaps = true; tb.SaveAndReimport();
                    tn.textureType = TextureImporterType.NormalMap; tn.maxTextureSize = 4096; tn.streamingMipmaps = true; tn.SaveAndReimport();
                }
            foreach (var name in names)
            {
                var mi = AssetImporter.GetAtPath(dstRel + "/" + name + ".fbx") as ModelImporter;
                mi.materialImportMode = ModelImporterMaterialImportMode.None; mi.SaveAndReimport();
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }
        foreach (var name in names)
        {
            var mats = new Dictionary<string, Material>();
            foreach (var cat in new[] { "wood", "win", "stone", "roof" })
            {
                string bcp = dstRel + "/" + name + "_" + cat + "_BC.png", np = dstRel + "/" + name + "_" + cat + "_N.png";
                var bc = AssetDatabase.LoadAssetAtPath<Texture2D>(bcp); if (bc == null) continue;
                string mp = dstRel + "/MAT_" + name + "_" + cat + ".mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(mp); bool isNew = m == null; if (isNew) m = new Material(Shader.Find("HDRP/Lit"));
                m.SetTexture("_BaseColorMap", bc); m.SetTexture("_NormalMap", AssetDatabase.LoadAssetAtPath<Texture2D>(np));
                m.SetFloat("_Smoothness", cat == "roof" ? 0.3f : (cat == "stone" ? 0.15f : 0.25f));
                m.SetFloat("_DoubleSidedEnable", 1); m.SetFloat("_CullMode", 0); m.SetFloat("_CullModeForward", 0);
                reset.Invoke(null, new object[] { m });
                if (isNew) AssetDatabase.CreateAsset(m, mp); else EditorUtility.SetDirty(m);
                mats[cat] = m;
            }
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(dstRel + "/" + name + ".fbx");
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            foreach (var r in inst.GetComponentsInChildren<MeshRenderer>(true))
                foreach (var kv in mats) if (r.name.EndsWith("_" + kv.Key)) r.sharedMaterial = kv.Value;
            PrefabUtility.SaveAsPrefabAsset(inst, dstRel + "/" + name + "_Baked.prefab");
            UnityEngine.Object.DestroyImmediate(inst);
            sb.Append(name).Append(' ');
        }
        AssetDatabase.SaveAssets();
        return "imported: " + sb;
    }

    // 시간 제한 있는 가져오기: MCP 호출이 시간 초과되면 같은 호출이 다시 들어와서, 한 번에 조금씩(budget 초) 처리하고
    // 이미 프리팹이 있는 덩어리는 건너뛴다. 끝나면 DONE 을 돌려준다.
    public static string ImportBakedStep(string complex, float budgetSec = 20f)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var src = Path.Combine(BakeRoot, complex, "out");
        var dstRel = BakedAssetRoot + "/" + complex; EnsureFolder(dstRel);
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
        var reset = Type.GetType("UnityEditor.Rendering.HighDefinition.HDShaderUtils, Unity.RenderPipelines.HighDefinition.Editor").GetMethod("ResetMaterialKeywords", new[] { typeof(Material) });
        var names = Directory.GetFiles(src, "*.fbx").Select(Path.GetFileNameWithoutExtension).ToList();
        int done = 0, left = 0;
        foreach (var name in names)
        {
            string prefabPath = dstRel + "/" + name + "_Baked.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null) { done++; continue; }
            if (sw.Elapsed.TotalSeconds > budgetSec) { left++; continue; }
            var mats = new Dictionary<string, Material>();
            foreach (var cat in new[] { "wood", "win", "stone", "roof" })
            {
                string bcp = dstRel + "/" + name + "_" + cat + "_BC.png", np = dstRel + "/" + name + "_" + cat + "_N.png";
                var tb = AssetImporter.GetAtPath(bcp) as TextureImporter; var tn = AssetImporter.GetAtPath(np) as TextureImporter;
                if (tb == null || tn == null) continue;
                if (!tb.streamingMipmaps || tb.maxTextureSize != 4096) { tb.sRGBTexture = true; tb.maxTextureSize = 4096; tb.streamingMipmaps = true; tb.SaveAndReimport(); }
                if (tn.textureType != TextureImporterType.NormalMap) { tn.textureType = TextureImporterType.NormalMap; tn.maxTextureSize = 4096; tn.streamingMipmaps = true; tn.SaveAndReimport(); }
                string mp = dstRel + "/MAT_" + name + "_" + cat + ".mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(mp); bool isNew = m == null; if (isNew) m = new Material(Shader.Find("HDRP/Lit"));
                m.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(bcp)); m.SetTexture("_NormalMap", AssetDatabase.LoadAssetAtPath<Texture2D>(np));
                m.SetFloat("_Smoothness", cat == "roof" ? 0.3f : (cat == "stone" ? 0.15f : 0.25f));
                m.SetFloat("_DoubleSidedEnable", 1); m.SetFloat("_CullMode", 0); m.SetFloat("_CullModeForward", 0);
                reset.Invoke(null, new object[] { m });
                if (isNew) AssetDatabase.CreateAsset(m, mp); else EditorUtility.SetDirty(m);
                mats[cat] = m;
            }
            var mi = AssetImporter.GetAtPath(dstRel + "/" + name + ".fbx") as ModelImporter;
            if (mi.materialImportMode != ModelImporterMaterialImportMode.None) { mi.materialImportMode = ModelImporterMaterialImportMode.None; mi.SaveAndReimport(); }
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(dstRel + "/" + name + ".fbx");
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            foreach (var r in inst.GetComponentsInChildren<MeshRenderer>(true))
                foreach (var kv in mats) if (r.name.EndsWith("_" + kv.Key)) r.sharedMaterial = kv.Value;
            PrefabUtility.SaveAsPrefabAsset(inst, prefabPath);
            UnityEngine.Object.DestroyImmediate(inst);
            done++;
        }
        AssetDatabase.SaveAssets();
        return left == 0 ? $"DONE {complex} {done}" : $"PARTIAL {complex} done={done} left={left}";
    }

    // ---------- 6b. 기본 큐브 지우기 ----------
    // 굽기 스크립트가 빈 장면의 기본 큐브(원점, 2 m)까지 같이 구워 덩어리마다 목부 메시에 큐브가 붙어 나왔다.
    // FBX 는 고칠 수 없어 큐브 삼각형(모든 꼭짓점이 원점 기준 최대 좌표 ±1 m)을 뺀 메시를 .asset 으로 만들어 프리팹에서 바꿔 끼운다.
    public static string CleanDefaultCube(string complex, float budgetSec = 20f)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var dir = BakedAssetRoot + "/" + complex;
        int fixedN = 0, left = 0, checkedN = 0;
        foreach (var g in AssetDatabase.FindAssets("t:Prefab", new[] { dir }))
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            if (!path.EndsWith("_Baked.prefab")) continue;
            if (sw.Elapsed.TotalSeconds > budgetSec) { left++; continue; }
            var root = PrefabUtility.LoadPrefabContents(path);
            bool changed = false;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var m = mf.sharedMesh; if (m == null) continue;
                if (AssetDatabase.GetAssetPath(m).EndsWith(".asset")) continue;
                checkedN++;
                var v = m.vertices; bool any = false;
                var subs = new List<int[]>();
                for (int s = 0; s < m.subMeshCount; s++)
                {
                    var t = m.GetTriangles(s); var keep = new List<int>(t.Length);
                    for (int i = 0; i < t.Length; i += 3)
                    {
                        bool cube = true;
                        for (int k = 0; k < 3 && cube; k++) { var q = v[t[i + k]]; float mx = Mathf.Max(Mathf.Abs(q.x), Mathf.Max(Mathf.Abs(q.y), Mathf.Abs(q.z))); if (mx < 0.998f || mx > 1.002f) cube = false; }
                        if (cube) { any = true; continue; }
                        keep.Add(t[i]); keep.Add(t[i + 1]); keep.Add(t[i + 2]);
                    }
                    subs.Add(keep.ToArray());
                }
                if (!any) continue;
                var nm = UnityEngine.Object.Instantiate(m); nm.name = m.name;
                for (int s = 0; s < subs.Count; s++) nm.SetTriangles(subs[s], s, false);
                nm.RecalculateBounds();
                var ap = dir + "/" + m.name + "_clean.asset";
                if (AssetDatabase.LoadAssetAtPath<Mesh>(ap) != null) AssetDatabase.DeleteAsset(ap);
                AssetDatabase.CreateAsset(nm, ap);
                mf.sharedMesh = nm;
                var mc = mf.GetComponent<MeshCollider>(); if (mc) mc.sharedMesh = nm;
                changed = true; fixedN++;
            }
            if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        return (left == 0 ? "DONE " : "PARTIAL ") + complex + " fixed=" + fixedN + " checked=" + checkedN + " left=" + left;
    }

    // ---------- 7. 단지 프리팹 조립 ----------
    // enterableCodes 의 원본 프리팹이 있으면 LODGroup(LOD0 원본 / LOD1 구운 것)으로 묶는다.
    public static string AssembleComplex(string complex, string[] enterableCodes, float lod0ScreenHeight = 0.25f)
    {
        var dir = BakedAssetRoot + "/" + complex;
        var root = new GameObject(complex);
        int n = 0;
        var chunkCode = new Dictionary<string, string>();
        var tsv = Path.Combine(BakeRoot, complex, "chunks.tsv");
        if (File.Exists(tsv)) foreach (var l in File.ReadAllLines(tsv)) { var p = l.Split('\t'); chunkCode[p[0]] = p[1]; }
        var bakedByCode = new Dictionary<string, List<GameObject>>();
        foreach (var g in AssetDatabase.FindAssets("t:Prefab", new[] { dir }))
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            if (!path.EndsWith("_Baked.prefab")) continue;
            var nm = Path.GetFileNameWithoutExtension(path).Replace("_Baked", "");
            string code; if (!chunkCode.TryGetValue(nm, out code)) code = nm;
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            inst.transform.SetParent(root.transform, false);
            if (!bakedByCode.ContainsKey(code)) bakedByCode[code] = new List<GameObject>();
            bakedByCode[code].Add(inst); n++;
        }
        foreach (var code in enterableCodes ?? new string[0])
        {
            var op = dir + "/" + complex + "_" + code + "_Original.prefab";
            var orig = AssetDatabase.LoadAssetAtPath<GameObject>(op); if (orig == null || !bakedByCode.ContainsKey(code)) continue;
            var grp = new GameObject(code + "_LOD"); grp.transform.SetParent(root.transform, false);
            var o = (GameObject)PrefabUtility.InstantiatePrefab(orig); o.transform.SetParent(grp.transform, false);
            foreach (var b in bakedByCode[code]) b.transform.SetParent(grp.transform, false);
            var lg = grp.AddComponent<LODGroup>();
            var r0 = o.GetComponentsInChildren<Renderer>(true);
            var r1 = bakedByCode[code].SelectMany(b => b.GetComponentsInChildren<Renderer>(true))
                .Concat(r0.Where(x => x.GetComponent<MeshFilter>() && x.GetComponent<MeshFilter>().sharedMesh && x.GetComponent<MeshFilter>().sharedMesh.name.IndexOf("Water", StringComparison.OrdinalIgnoreCase) >= 0)).ToArray();
            lg.SetLODs(new[] { new LOD(lod0ScreenHeight, r0), new LOD(0.005f, r1) });
            lg.RecalculateBounds();
        }
        var path2 = dir + "/" + complex + "_Complex.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path2);
        UnityEngine.Object.DestroyImmediate(root);
        return $"assembled {path2} parts={n}";
    }
}
#endif
