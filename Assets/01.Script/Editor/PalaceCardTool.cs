// 경복궁 단순화판(Unity 에 올린 단지)에서 아직 메시로 남은 창살·문·홍살·문 쇠붙이를 "판(카드)"으로 바꾸는 도구 — MCP 에서 정적 메서드로 호출.
// 1) ExportTargets: 대상 렌더러의 메시 전체(메시 로컬 좌표) + 재질 정보 + 어느 프리팹의 어느 오브젝트인지를 파일로 내보낸다.
// 2) (외부) 서브메시별로 판 그림을 굽고, 대상 서브메시를 판으로 바꾼 새 메시를 만든다.
// 3) PrepareCards: 새 메시·판 텍스처·재질을 에셋으로 만든다. ApplyToPrefabs: 그 오브젝트가 들어 있는 프리팹(가장 가까운 프리팹)에서 메시·재질·MeshCollider 를 바꾼다.
//    원래 메시(FBX·옛 .asset)는 건드리지 않는다(새 메시 에셋으로 교체) — 되돌리려면 프리팹만 되돌리면 됨. Verify: 씬에서 새 메시를 쓰는지 확인.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class PalaceCardTool
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    const string Root = "Assets/02.Resources/01.Asset/Models/Gyeongbokgung/_Baked";

    // GyeongBok 아래 켜진 *_Simplified 에서 이름이 같은 오브젝트를 찾는다(옛 *_Complex 쪽 같은 이름은 제외)
    static List<Transform> FindTargets(string objName)
    {
        var gb = GameObject.Find("GyeongBok");
        var res = new List<Transform>();
        if (gb == null) return res;
        foreach (var t in gb.GetComponentsInChildren<Transform>(true))
        {
            if (t.name != objName) continue;
            bool underSimplified = false; var p = t;
            while (p != null) { if (p.name.EndsWith("_Simplified")) { underSimplified = true; break; } p = p.parent; }
            if (underSimplified) res.Add(t);
        }
        return res;
    }

    static string HierPath(Transform t, Transform stop)
    {
        var parts = new List<string>();
        while (t != null && t != stop) { parts.Add(t.name); t = t.parent; }
        parts.Reverse();
        return string.Join("/", parts);
    }

    // spec: 줄마다 "오브젝트이름|재질이름 정규식" (정규식이 비면 모든 서브메시). 같은 메시를 쓰는 오브젝트는 메시 하나로 묶어 내보냄.
    public static string ExportTargets(string spec, string outDir)
    {
        Directory.CreateDirectory(outDir);
        var meshes = new List<Mesh>(); var meshIdx = new Dictionary<Mesh, int>();
        var tsv = new StringBuilder(); var mats = new Dictionary<string, Material>();
        int nobj = 0;
        foreach (var raw in spec.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim(); if (line.Length == 0) continue;
            var parts = line.Split(new[] { '|' }, 2);   // 앞 = 오브젝트 이름, 뒤 = 재질 정규식(정규식 안의 | 는 그대로 둠)
            var rx = new Regex(parts.Length > 1 && parts[1].Length > 0 ? parts[1] : ".*", RegexOptions.IgnoreCase);
            foreach (var t in FindTargets(parts[0]))
            {
                var mf = t.GetComponent<MeshFilter>(); var mr = t.GetComponent<MeshRenderer>();
                if (mf == null || mr == null || mf.sharedMesh == null) continue;
                var m = mf.sharedMesh;
                if (!meshIdx.TryGetValue(m, out var mi)) { mi = meshes.Count; meshIdx[m] = mi; meshes.Add(m); }
                var sm = mr.sharedMaterials; var sel = new List<int>(); var names = new List<string>();
                for (int s = 0; s < m.subMeshCount; s++)
                {
                    var mat = s < sm.Length ? sm[s] : null;
                    names.Add(mat ? mat.name : "");
                    if (mat) mats[mat.name] = mat;
                    if (mat != null && rx.IsMatch(mat.name) && !mat.name.ToLowerInvariant().Contains("card")) sel.Add(s);
                }
                var prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject);
                var proot = PrefabUtility.GetNearestPrefabInstanceRoot(t.gameObject);
                var rel = proot ? HierPath(t, proot.transform) : "";
                var gb = GameObject.Find("GyeongBok");
                var M = gb.transform.worldToLocalMatrix * t.localToWorldMatrix;
                tsv.Append(mi).Append('\t').Append(HierPath(t, gb.transform)).Append('\t').Append(m.name).Append('\t').Append(AssetDatabase.GetAssetPath(m))
                   .Append('\t').Append(prefabPath).Append('\t').Append(rel).Append('\t').Append(string.Join(",", sel)).Append('\t').Append(string.Join("|", names))
                   .Append('\t').Append(t.GetComponent<MeshCollider>() ? "1" : "0");
                for (int i = 0; i < 16; i++) tsv.Append('\t').Append(M[i / 4, i % 4].ToString("R", Inv));
                tsv.Append('\n'); nobj++;
            }
        }
        using (var w = new BinaryWriter(File.Open(Path.Combine(outDir, "targets.bin"), FileMode.Create)))
        {
            w.Write(0x31584250); w.Write(meshes.Count);
            foreach (var m in meshes)
            {
                var nb = Encoding.UTF8.GetBytes(m.name); w.Write(nb.Length); w.Write(nb);
                var v = m.vertices; var n = m.normals; var uv = m.uv;
                w.Write(v.Length); w.Write(m.subMeshCount);
                foreach (var p in v) { w.Write(p.x); w.Write(p.y); w.Write(p.z); }
                if (n.Length == v.Length) foreach (var q in n) { w.Write(q.x); w.Write(q.y); w.Write(q.z); } else for (int i = 0; i < v.Length; i++) { w.Write(0f); w.Write(1f); w.Write(0f); }
                if (uv.Length == v.Length) foreach (var q in uv) { w.Write(q.x); w.Write(q.y); } else for (int i = 0; i < v.Length; i++) { w.Write(0f); w.Write(0f); }
                for (int s = 0; s < m.subMeshCount; s++) { var tr = m.GetTriangles(s); w.Write(tr.Length); foreach (var x in tr) w.Write(x); }
            }
        }
        File.WriteAllText(Path.Combine(outDir, "targets.tsv"), tsv.ToString(), new UTF8Encoding(false));
        var mt = new StringBuilder(); var texs = new HashSet<string>();
        foreach (var kv in mats.OrderBy(k => k.Key))
        {
            var mat = kv.Value;
            string Tex(string p) { if (!mat.HasProperty(p)) return ""; var tx = mat.GetTexture(p); return tx ? Path.GetFullPath(AssetDatabase.GetAssetPath(tx)) : ""; }
            string Col(string p) { if (!mat.HasProperty(p)) return ""; var c = mat.GetColor(p); return c.r.ToString("R", Inv) + "," + c.g.ToString("R", Inv) + "," + c.b.ToString("R", Inv) + "," + c.a.ToString("R", Inv); }
            string Fl(string p) { return mat.HasProperty(p) ? mat.GetFloat(p).ToString("R", Inv) : ""; }
            var st = mat.HasProperty("_BaseColorMap") ? mat.GetTextureScale("_BaseColorMap") : Vector2.one;
            var so = mat.HasProperty("_BaseColorMap") ? mat.GetTextureOffset("_BaseColorMap") : Vector2.zero;
            var bc = Tex("_BaseColorMap"); if (bc.Length > 0) texs.Add(bc);
            mt.Append(kv.Key).Append('\t').Append(mat.shader.name).Append('\t').Append(bc).Append('\t').Append(Tex("_NormalMap")).Append('\t').Append(Col("_BaseColor"))
              .Append('\t').Append(st.x.ToString("R", Inv)).Append(',').Append(st.y.ToString("R", Inv)).Append(',').Append(so.x.ToString("R", Inv)).Append(',').Append(so.y.ToString("R", Inv))
              .Append('\t').Append(Fl("_AlphaCutoffEnable")).Append('\t').Append(Fl("_AlphaCutoff")).Append('\t').Append(Fl("_Smoothness")).Append('\t').Append(Fl("_NormalScale"))
              .Append('\t').Append(Fl("_DoubleSidedEnable")).Append('\t').Append(Tex("_MaskMap")).Append('\n');
        }
        File.WriteAllText(Path.Combine(outDir, "materials.tsv"), mt.ToString(), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(outDir, "textures.txt"), string.Join("\n", texs.OrderBy(x => x)), new UTF8Encoding(false));
        return "objects=" + nobj + " meshes=" + meshes.Count + " mats=" + mats.Count + " textures=" + texs.Count;
    }

    // 판 텍스처를 텍스처 폴더에서 한곳으로 모아 스테이징하기 쉽게 복사(얕은 경로)
    public static string CopyTextures(string listFile, string dstDir)
    {
        Directory.CreateDirectory(dstDir); int n = 0;
        foreach (var p in File.ReadAllLines(listFile))
        {
            if (p.Trim().Length == 0 || !File.Exists(p)) continue;
            var d = Path.Combine(dstDir, Path.GetFileName(p));
            if (!File.Exists(d) || new FileInfo(d).Length != new FileInfo(p).Length) { File.Copy(p, d, true); n++; }
        }
        return "copied=" + n;
    }

    // ───────────── 판 적용 (외부에서 구운 결과 → 에셋·프리팹) ─────────────
    // dir 에 있어야 하는 것: ucards2.bin(판 정점 + 새 서브메시 목록), ucards.tsv(재질 이름·텍스처·알파 클립), targets.tsv(ExportTargets 결과), Cards/*.png
    // 1) PrepareCards(dir, 초): 텍스처·재질·새 메시 에셋을 만든다(이미 있으면 건너뜀). "DONE" 이 나올 때까지 부른다.
    // 2) ApplyToPrefabs(dir): 프리팹(오브젝트가 들어 있는 가장 가까운 프리팹)에서 메시·재질·MeshCollider 를 새 것으로.
    // 새 에셋 위치 = _Baked/<단지>/Simplified/Cards/ (텍스처 <메시>_card.png, 재질 CARD_<텍스처 이름>.mat), 메시 = 그 아래 Meshes/<원래 메시>_Card.asset

    class UEntry
    {
        public int k; public string name; public int nb;
        public Vector3[] cp, cn; public Vector2[] cu;
        public List<int> subKind = new List<int>(), subVal = new List<int>(); public List<int[]> subIdx = new List<int[]>();
        public string[] mats; public string tex; public bool clip; public int before, after;
    }

    class TRow { public int k; public string gbPath, meshName, meshAsset, prefab, rel; }

    static List<UEntry> ReadEntries(string dir)
    {
        var list = new List<UEntry>();
        using (var br = new BinaryReader(File.OpenRead(Path.Combine(dir, "ucards2.bin"))))
        {
            if (br.ReadInt32() != 0x32435550) throw new Exception("ucards2.bin 형식이 아님");
            int n = br.ReadInt32();
            for (int i = 0; i < n; i++)
            {
                var e = new UEntry();
                e.name = Encoding.UTF8.GetString(br.ReadBytes(br.ReadInt32()));
                e.nb = br.ReadInt32(); int nc = br.ReadInt32();
                e.cp = new Vector3[nc]; e.cn = new Vector3[nc]; e.cu = new Vector2[nc];
                for (int j = 0; j < nc; j++) e.cp[j] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                for (int j = 0; j < nc; j++) e.cn[j] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                for (int j = 0; j < nc; j++) e.cu[j] = new Vector2(br.ReadSingle(), br.ReadSingle());
                int ns = br.ReadInt32();
                for (int s = 0; s < ns; s++)
                {
                    int kind = br.ReadInt32(), v = br.ReadInt32();
                    e.subKind.Add(kind); e.subVal.Add(v);
                    if (kind == 1) { var idx = new int[v]; for (int j = 0; j < v; j++) idx[j] = br.ReadInt32(); e.subIdx.Add(idx); }
                    else e.subIdx.Add(null);
                }
                list.Add(e);
            }
        }
        var lines = File.ReadAllLines(Path.Combine(dir, "ucards.tsv"), Encoding.UTF8);
        for (int i = 1; i < lines.Length; i++)
        {
            if (lines[i].Trim().Length == 0) continue;
            var p = lines[i].Split('\t'); var e = list[i - 1];
            if (p[1] != e.name) throw new Exception("ucards.tsv 순서가 ucards2.bin 과 다름: " + p[1] + " / " + e.name);
            e.k = int.Parse(p[0], Inv); e.mats = p[2].Split('|'); e.tex = p[4]; e.clip = p[5] == "1";
            e.before = int.Parse(p[7], Inv); e.after = int.Parse(p[8], Inv);
        }
        return list;
    }

    static List<TRow> ReadTargets(string dir)
    {
        var res = new List<TRow>();
        foreach (var line in File.ReadAllLines(Path.Combine(dir, "targets.tsv"), Encoding.UTF8))
        {
            var p = line.Split('\t'); if (p.Length < 9) continue;
            res.Add(new TRow { k = int.Parse(p[0], Inv), gbPath = p[1], meshName = p[2], meshAsset = p[3], prefab = p[4], rel = p[5] });
        }
        return res;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    // 프리팹 경로의 _Baked/<단지>/ → _Baked/<단지>/Simplified/Cards
    static string CardsFolder(string prefabPath)
    {
        int i = prefabPath.IndexOf("/_Baked/", StringComparison.Ordinal);
        var rest = prefabPath.Substring(i + 8); var cx = rest.Substring(0, rest.IndexOf('/'));
        return Root + "/" + cx + "/Simplified/Cards";
    }

    static string CardMatName(UEntry e) { return "CARD_" + Path.GetFileNameWithoutExtension(e.tex).Replace("_card", ""); }

    static Mesh FindMesh(string assetPath, string name)
    {
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(assetPath)) { var m = o as Mesh; if (m != null && m.name == name) return m; }
        return null;
    }

    static MethodInfo resetKw;
    static void ResetKeywords(Material m)
    {
        if (resetKw == null)
            resetKw = Type.GetType("UnityEditor.Rendering.HighDefinition.HDShaderUtils, Unity.RenderPipelines.HighDefinition.Editor").GetMethod("ResetMaterialKeywords", new[] { typeof(Material) });
        resetKw.Invoke(null, new object[] { m });
    }

    static bool SameFile(string a, string b)
    {
        var fa = new FileInfo(a); var fb = new FileInfo(b);
        if (!fa.Exists || !fb.Exists || fa.Length != fb.Length) return false;
        return File.ReadAllBytes(a).SequenceEqual(File.ReadAllBytes(b));
    }

    static Texture2D EnsureTexture(string dir, UEntry e, string folder)
    {
        var dst = folder + "/" + e.tex; var src = Path.Combine(dir, "Cards", e.tex); var full = Path.GetFullPath(dst);
        if (!SameFile(full, src))
        {
            File.Copy(src, full, true);
            AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceSynchronousImport);
        }
        var ti = (TextureImporter)AssetImporter.GetAtPath(dst);
        bool dirty = false;
        var asrc = e.clip ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
        if (ti.wrapMode != TextureWrapMode.Clamp) { ti.wrapMode = TextureWrapMode.Clamp; dirty = true; }   // 아틀라스라 가장자리가 반대편과 섞이지 않게
        if (ti.alphaSource != asrc) { ti.alphaSource = asrc; dirty = true; }
        if (ti.alphaIsTransparency != e.clip) { ti.alphaIsTransparency = e.clip; dirty = true; }
        if (ti.mipMapsPreserveCoverage != e.clip) { ti.mipMapsPreserveCoverage = e.clip; ti.alphaTestReferenceValue = 0.5f; dirty = true; }   // 멀리서 창살·홍살이 사라지지 않게
        if (!ti.sRGBTexture) { ti.sRGBTexture = true; dirty = true; }
        if (!ti.mipmapEnabled) { ti.mipmapEnabled = true; dirty = true; }
        if (ti.maxTextureSize != 4096) { ti.maxTextureSize = 4096; dirty = true; }
        if (dirty) ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(dst);
    }

    static Material EnsureMaterial(string folder, UEntry e, Texture2D tex)
    {
        var path = folder + "/" + CardMatName(e) + ".mat";
        var sh = Shader.Find("HDRP/Lit");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
        m.shader = sh;
        m.SetTexture("_BaseColorMap", tex); m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Metallic", 0f); m.SetFloat("_Smoothness", 0.12f);
        m.SetFloat("_AlphaCutoffEnable", e.clip ? 1f : 0f); m.SetFloat("_AlphaCutoff", 0.5f);
        m.SetFloat("_DoubleSidedEnable", 0f);   // 앞·뒤 판이 따로 있으므로 한쪽 면
        ResetKeywords(m);
        EditorUtility.SetDirty(m);
        return m;
    }

    // 원래 메시 + 판 정점 → 새 메시(안 쓰는 정점은 뺌, 판 정점 접선은 UV 방향으로 계산) → PalaceMeshCompact 로 16비트화
    static Mesh BuildMesh(UEntry e, Mesh orig)
    {
        if (orig.vertexCount != e.nb) throw new Exception("정점 수가 다름: " + orig.name + " " + orig.vertexCount + " / " + e.nb);
        var P = new List<Vector3>(); orig.GetVertices(P);
        var N = new List<Vector3>(); orig.GetNormals(N);
        var T = new List<Vector4>(); orig.GetTangents(T);
        var U = new List<Vector2>(); orig.GetUVs(0, U);
        int nb = P.Count, nc = e.cp.Length;
        if (N.Count != nb) N = Enumerable.Repeat(Vector3.up, nb).ToList();
        if (T.Count != nb) T = Enumerable.Repeat(new Vector4(1, 0, 0, 1), nb).ToList();
        if (U.Count != nb) U = Enumerable.Repeat(Vector2.zero, nb).ToList();
        P.AddRange(e.cp); N.AddRange(e.cn); U.AddRange(e.cu);
        var subs = new List<int[]>();
        for (int s = 0; s < e.subKind.Count; s++) subs.Add(e.subKind[s] == 0 ? orig.GetIndices(e.subVal[s]) : e.subIdx[s]);
        var ct = new Vector4[nc];
        for (int j = 0; j < nc; j++) ct[j] = new Vector4(1, 0, 0, 1);
        foreach (var idx in subs)
            for (int i = 0; i + 2 < idx.Length; i += 3)
            {
                int a = idx[i], b = idx[i + 1], c = idx[i + 2];
                if (a < nb || b < nb || c < nb) continue;
                Vector3 e1 = P[b] - P[a], e2 = P[c] - P[a]; Vector2 d1 = U[b] - U[a], d2 = U[c] - U[a];
                float det = d1.x * d2.y - d2.x * d1.y; if (Mathf.Abs(det) < 1e-12f) continue;
                float r = 1f / det;
                var tv = (e1 * d2.y - e2 * d1.y) * r; var bv = (e2 * d1.x - e1 * d2.x) * r;
                var n = N[a]; var t = (tv - n * Vector3.Dot(n, tv)).normalized;
                float w = Vector3.Dot(Vector3.Cross(n, t), bv) < 0f ? -1f : 1f;
                var t4 = new Vector4(t.x, t.y, t.z, w);
                ct[a - nb] = t4; ct[b - nb] = t4; ct[c - nb] = t4;
            }
        T.AddRange(ct);
        var used = new bool[P.Count];
        foreach (var idx in subs) foreach (var x in idx) used[x] = true;
        var remap = new int[P.Count];
        var P2 = new List<Vector3>(); var N2 = new List<Vector3>(); var T2 = new List<Vector4>(); var U2 = new List<Vector2>();
        for (int i = 0; i < P.Count; i++)
        {
            if (!used[i]) { remap[i] = -1; continue; }
            remap[i] = P2.Count; P2.Add(P[i]); N2.Add(N[i]); T2.Add(T[i]); U2.Add(U[i]);
        }
        var m = new Mesh(); m.name = orig.name + "_Card";
        m.indexFormat = P2.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        m.SetVertices(P2); m.SetNormals(N2); m.SetTangents(T2); m.SetUVs(0, U2);
        m.subMeshCount = subs.Count;
        for (int s = 0; s < subs.Count; s++) m.SetTriangles(subs[s].Select(x => remap[x]).ToArray(), s, false);
        m.RecalculateBounds();
        PalaceMeshCompact.CompactMesh(m);
        return m;
    }

    // 텍스처·재질·새 메시 에셋 만들기(시간 제한, 여러 번 부름). force = 다시 만들 항목 이름(쉼표) — 메시는 같은 에셋에 덮어써 프리팹 참조 유지
    public static string PrepareCards(string dir, float budget = 20f, string force = "")
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var entries = ReadEntries(dir); var rows = ReadTargets(dir);
        var forced = new HashSet<string>(force.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()));
        var stamp = new FileInfo(Path.Combine(dir, "ucards2.bin")).LastWriteTimeUtc.Ticks.ToString(Inv);   // 이 판 데이터로 이미 다시 만들었는지 표시용
        var sb = new StringBuilder(); int done = 0, made = 0;
        foreach (var e in entries)
        {
            var r = rows.FirstOrDefault(x => x.k == e.k);
            if (r == null) { sb.Append("NO TARGET ").Append(e.name).Append('\n'); continue; }
            var folder = CardsFolder(r.prefab); EnsureFolder(folder); EnsureFolder(folder + "/Meshes");
            var meshPath = folder + "/Meshes/" + r.meshName + "_Card.asset";
            var texPath = folder + "/" + e.tex; var matPath = folder + "/" + CardMatName(e) + ".mat";
            var exMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            bool rebuild = forced.Contains(e.name) && !SessionState.GetBool("PalaceCardTool.rebuilt." + meshPath + "." + stamp, false);
            bool haveAll = !rebuild && exMesh != null && AssetDatabase.LoadAssetAtPath<Material>(matPath) != null
                           && SameFile(Path.GetFullPath(texPath), Path.Combine(dir, "Cards", e.tex));
            if (haveAll) { done++; continue; }
            if (sw.Elapsed.TotalSeconds > budget) break;
            var tex = EnsureTexture(dir, e, folder);
            EnsureMaterial(folder, e, tex);
            if (exMesh == null || rebuild)
            {
                var orig = FindMesh(r.meshAsset, r.meshName);
                if (orig == null) { sb.Append("NO MESH ").Append(r.meshAsset).Append(' ').Append(r.meshName).Append('\n'); continue; }
                var nm = BuildMesh(e, orig);
                int nv = nm.vertexCount;
                if (exMesh == null) AssetDatabase.CreateAsset(nm, meshPath);
                else
                {
                    var keep = exMesh.name;
                    EditorUtility.CopySerialized(nm, exMesh); exMesh.name = keep; EditorUtility.SetDirty(exMesh);
                    UnityEngine.Object.DestroyImmediate(nm);
                    SessionState.SetBool("PalaceCardTool.rebuilt." + meshPath + "." + stamp, true);   // 같은 호출이 다시 와도 두 번 덮어쓰지 않게
                }
                sb.Append(e.name).Append(rebuild ? " (다시)" : "").Append(" v").Append(orig.vertexCount).Append("->").Append(nv).Append(" tris ").Append(e.before).Append("->").Append(e.after).Append('\n');
            }
            done++; made++;
        }
        AssetDatabase.SaveAssets();
        return (done < entries.Count ? "PARTIAL " : "DONE ") + done + "/" + entries.Count + " (made " + made + ")\n" + sb;
    }

    // 프리팹에 적용: 오브젝트마다 메시·재질·(원래 메시를 쓰던) MeshCollider 교체
    public static string ApplyToPrefabs(string dir)
    {
        var entries = ReadEntries(dir); var rows = ReadTargets(dir);
        var byK = new Dictionary<int, UEntry>(); foreach (var e in entries) byK[e.k] = e;
        var sb = new StringBuilder(); int nobj = 0;
        foreach (var grp in rows.Where(x => byK.ContainsKey(x.k)).GroupBy(x => x.prefab))
        {
            var root = PrefabUtility.LoadPrefabContents(grp.Key);
            int n = 0;
            try
            {
                foreach (var r in grp)
                {
                    var e = byK[r.k]; var folder = CardsFolder(r.prefab);
                    var t = root.transform.Find(r.rel);
                    if (t == null) { sb.Append("MISSING ").Append(r.rel).Append('\n'); continue; }
                    var mf = t.GetComponent<MeshFilter>(); var mr = t.GetComponent<MeshRenderer>();
                    var nm = AssetDatabase.LoadAssetAtPath<Mesh>(folder + "/Meshes/" + r.meshName + "_Card.asset");
                    var cm = AssetDatabase.LoadAssetAtPath<Material>(folder + "/" + CardMatName(e) + ".mat");
                    if (mf == null || mr == null || nm == null || cm == null) { sb.Append("NOT READY ").Append(r.rel).Append('\n'); continue; }
                    var byName = new Dictionary<string, Material>();
                    foreach (var mm in mr.sharedMaterials) if (mm != null) byName[mm.name] = mm;
                    var mats = new Material[e.mats.Length]; bool ok = true;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        if (e.mats[i].StartsWith("CARD_", StringComparison.Ordinal)) mats[i] = cm;
                        else if (!byName.TryGetValue(e.mats[i], out mats[i])) { ok = false; sb.Append("NO MAT ").Append(e.mats[i]).Append(" @ ").Append(r.rel).Append('\n'); }
                    }
                    if (!ok) continue;
                    var old = mf.sharedMesh;
                    mf.sharedMesh = nm; mr.sharedMaterials = mats;
                    var mc = t.GetComponent<MeshCollider>();
                    if (mc != null && (mc.sharedMesh == old || mc.sharedMesh == null || mc.sharedMesh.name == r.meshName)) mc.sharedMesh = nm;
                    n++;
                }
                PrefabUtility.SaveAsPrefabAsset(root, grp.Key);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            sb.Append(grp.Key).Append(" objects ").Append(n).Append('\n'); nobj += n;
        }
        return "objects=" + nobj + "\n" + sb;
    }

    // 확인: GyeongBok 아래 켜진 *_Simplified 의 대상 오브젝트가 새 메시·재질을 쓰는지
    public static string Verify(string dir)
    {
        var entries = ReadEntries(dir); var rows = ReadTargets(dir);
        var byK = new Dictionary<int, UEntry>(); foreach (var e in entries) byK[e.k] = e;
        var gb = GameObject.Find("GyeongBok"); var sb = new StringBuilder(); int ok = 0, bad = 0;
        foreach (var r in rows.Where(x => byK.ContainsKey(x.k)))
        {
            var t = gb.transform.Find(r.gbPath);
            var mf = t ? t.GetComponent<MeshFilter>() : null; var mr = t ? t.GetComponent<MeshRenderer>() : null;
            bool good = mf != null && mf.sharedMesh != null && mf.sharedMesh.name == r.meshName + "_Card" && mr.sharedMaterials.Length == mf.sharedMesh.subMeshCount && mr.sharedMaterials.All(x => x != null);
            if (good) ok++; else { bad++; sb.Append("BAD ").Append(r.gbPath).Append(' ').Append(mf && mf.sharedMesh ? mf.sharedMesh.name : "null").Append('\n'); }
        }
        return "ok=" + ok + " bad=" + bad + "\n" + sb;
    }
}
