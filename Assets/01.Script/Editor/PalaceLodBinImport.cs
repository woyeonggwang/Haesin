// 궁궐 단순화판 가져오기(흥례문부터): 밖에서 만든 메시 묶음(meshes.bin, 'HNMS' 형식) + 배치표(layout.txt)로
// 메시 에셋 → 부분별 LODGroup 프리팹 → 씬 배치까지. MCP 에서 정적 메서드로 부름.
//
// 폴더(dir, 예: D:\UnityAssetCache\_hnm) 안:
//   meshes.bin   메시 묶음(Unity 좌표 = 단지 프리팹 루트 좌표). 형식: int32 매직 0x534D4E48, int32 개수,
//                메시마다 이름, 정점 수, 서브메시 수, 위치·법선(float32×3)·UV(float32×2), 서브메시마다 재질 이름 + 인덱스(int32)
//   doors.bin    여는 문짝 판(같은 형식, 메시 이름 = 원래 문짝 메시 이름, 문짝 메시 자기 좌표)
//   cards.tsv    판 재질: 재질 이름, 텍스처 파일, 알파 컷(0/1)
//   Cards\*.png  판 텍스처
//   layout.txt   PREFAB <이름> / PART <이름> <기준 x y z> <크기> <LOD0→1 화면 높이> <LOD1→2> <끝> / MESH <LOD 번호> <메시 이름> [collider]
//                / DOORS <원래 문짝 프리팹 경로> <새 문짝 프리팹 이름>
//                / COPYFAR <LOD 번호> <다른 프리팹 경로> <그 안의 오브젝트 경로> — 그 오브젝트의 렌더러를 그대로 가져다 씀
//                  (LODGroup 이 있으면 마지막(먼) 단계 렌더러만). 건춘문·영추문·신무문은 먼 LOD 로 예전 구운 덩어리를 씀(2026-09-30).
//                / COPYFARBOX <LOD 번호> <다른 프리팹 경로> <오브젝트 경로> <x최소> <x최대> <z최소> <z최대> — COPYFAR 와 같은 렌더러에서
//                  무게중심(그 프리팹 루트 좌표)이 상자 안에 든 삼각형만 잘라 새 메시(outFolder/Far/<부분>_<렌더러>.asset)로 씀.
//                  구운 덩어리는 건물 코드별로 합쳐져 있어, 부분을 건물별로 나눈 영추문(문·건물 3채 따로 LOD)에서 씀(2026-09-30).
// 순서: ImportMeshes(dir, outFolder, 20) 를 DONE 까지 → BuildDoorsCard → BuildPrefab → Place
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class PalaceLodBinImport
{
    const int Magic = 0x534D4E48;

    public class BinMesh
    {
        public string name; public Vector3[] pos; public Vector3[] nrm; public Vector2[] uv;
        public List<string> mats = new List<string>(); public List<int[]> subs = new List<int[]>();
    }

    // 파일에서 메시 목록(이름·위치)만 먼저 훑고, 필요한 것만 읽음
    static List<KeyValuePair<string, long>> Index(string path)
    {
        var list = new List<KeyValuePair<string, long>>();
        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
        using (var br = new BinaryReader(fs))
        {
            if (br.ReadInt32() != Magic) throw new Exception("매직이 다름: " + path);
            int n = br.ReadInt32();
            for (int k = 0; k < n; k++)
            {
                long at = fs.Position;
                string name = Encoding.UTF8.GetString(br.ReadBytes(br.ReadInt32()));
                int nv = br.ReadInt32(), ns = br.ReadInt32();
                fs.Seek((long)nv * 32, SeekOrigin.Current);
                for (int s = 0; s < ns; s++) { int l = br.ReadInt32(); fs.Seek(l, SeekOrigin.Current); int ni = br.ReadInt32(); fs.Seek((long)ni * 4, SeekOrigin.Current); }
                list.Add(new KeyValuePair<string, long>(name, at));
            }
        }
        return list;
    }

    static BinMesh ReadAt(string path, long at)
    {
        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
        using (var br = new BinaryReader(fs))
        {
            fs.Seek(at, SeekOrigin.Begin);
            var m = new BinMesh();
            m.name = Encoding.UTF8.GetString(br.ReadBytes(br.ReadInt32()));
            int nv = br.ReadInt32(), ns = br.ReadInt32();
            var f = new float[nv * 3];
            Buffer.BlockCopy(br.ReadBytes(nv * 12), 0, f, 0, nv * 12);
            m.pos = new Vector3[nv]; for (int i = 0; i < nv; i++) m.pos[i] = new Vector3(f[3 * i], f[3 * i + 1], f[3 * i + 2]);
            Buffer.BlockCopy(br.ReadBytes(nv * 12), 0, f, 0, nv * 12);
            m.nrm = new Vector3[nv]; for (int i = 0; i < nv; i++) m.nrm[i] = new Vector3(f[3 * i], f[3 * i + 1], f[3 * i + 2]);
            var g = new float[nv * 2];
            Buffer.BlockCopy(br.ReadBytes(nv * 8), 0, g, 0, nv * 8);
            m.uv = new Vector2[nv]; for (int i = 0; i < nv; i++) m.uv[i] = new Vector2(g[2 * i], g[2 * i + 1]);
            for (int s = 0; s < ns; s++)
            {
                m.mats.Add(Encoding.UTF8.GetString(br.ReadBytes(br.ReadInt32())));
                int ni = br.ReadInt32();
                var idx = new int[ni];
                Buffer.BlockCopy(br.ReadBytes(ni * 4), 0, idx, 0, ni * 4);
                m.subs.Add(idx);
            }
            return m;
        }
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static Mesh ToMesh(BinMesh b, Mesh reuse)
    {
        var m = reuse != null ? reuse : new Mesh();
        m.Clear();
        m.name = b.name;
        m.indexFormat = b.pos.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        m.vertices = b.pos; m.normals = b.nrm; m.uv = b.uv;
        m.subMeshCount = b.subs.Count;
        for (int s = 0; s < b.subs.Count; s++) m.SetTriangles(b.subs[s], s, false);
        m.RecalculateBounds();
        m.RecalculateTangents();
        PalaceMeshCompact.CompactMesh(m);
        return m;
    }

    // 메시 에셋 만들기(시간 제한). 이미 있고 파일보다 새로우면 건너뜀. force = 다시 만들 메시 이름(쉼표)
    // 메모리 주의: 궁 전체가 든 SampleScene 을 연 채로 큰 메시를 저장하다 메모리 부족으로 Unity 가 멈춘 적이 있음(2026-09-30)
    // → 먼저 CloseHeavyScene 으로 빈 씬을 열고, 한 번에 maxCount 개씩 저장하며 메모리를 비움
    public static string ImportMeshes(string dir, string outFolder, float budget = 20f, string force = "", string binName = "meshes.bin", string sub = "Meshes", int maxCount = 1000)
    {
        var t0 = DateTime.Now; var path = Path.Combine(dir, binName);
        var folder = outFolder + "/" + sub; EnsureFolder(folder);
        var forced = new HashSet<string>(force.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
        var stamp = File.GetLastWriteTimeUtc(path).Ticks.ToString();
        int made = 0, skipped = 0, left = 0;
        foreach (var kv in Index(path))
        {
            var ap = folder + "/" + kv.Key + ".asset";
            var key = "PalaceLodBinImport." + ap + "." + stamp;
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(ap);
            if (old != null && !forced.Contains(kv.Key) && SessionState.GetBool(key, false)) { skipped++; continue; }
            if (old != null && !forced.Contains(kv.Key) && File.GetLastWriteTimeUtc(Path.GetFullPath(ap)) > File.GetLastWriteTimeUtc(path)) { skipped++; continue; }
            if ((DateTime.Now - t0).TotalSeconds > budget || made >= maxCount) { left++; continue; }
            var b = ReadAt(path, kv.Value);
            Mesh mm;
            if (old != null) { mm = ToMesh(b, old); EditorUtility.SetDirty(old); }
            else { mm = ToMesh(b, null); AssetDatabase.CreateAsset(mm, ap); }
            b = null;
            AssetDatabase.SaveAssets();
            Resources.UnloadAsset(mm);
            SessionState.SetBool(key, true);
            made++;
        }
        AssetDatabase.SaveAssets();
        EditorUtility.UnloadUnusedAssetsImmediate();
        GC.Collect();
        return (left > 0 ? "MORE" : "DONE") + " made=" + made + " skipped=" + skipped + " left=" + left + " " + (DateTime.Now - t0).TotalSeconds.ToString("F1") + "s";
    }

    // 무거운 씬(궁 전체) 닫고 빈 씬 열기. 저장 안 된 변경이 있으면 아무것도 안 함.
    public static string CloseHeavyScene()
    {
        var sc = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (sc.isDirty) return "DIRTY " + sc.path + " — 저장 안 된 변경이 있어 그대로 둠";
        var path = sc.path;
        UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
        EditorUtility.UnloadUnusedAssetsImmediate();
        GC.Collect();
        return "closed " + path;
    }

    public static string OpenScene(string path)
    {
        var sc = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (sc.isDirty) return "DIRTY " + sc.path;
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Single);
        return "opened " + path;
    }

    // ---------- 재질 ----------
    static System.Reflection.MethodInfo resetKw;
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

    // 판 재질(한쪽 면, 매끄러움 0.12, 알파 컷 0.5)
    static Dictionary<string, Material> CardMaterials(string dir, string outFolder)
    {
        var res = new Dictionary<string, Material>();
        var tsv = Path.Combine(dir, "cards.tsv");
        if (!File.Exists(tsv)) return res;
        var folder = outFolder + "/Cards"; EnsureFolder(folder);
        var sh = Shader.Find("HDRP/Lit");
        foreach (var line in File.ReadAllLines(tsv, Encoding.UTF8))
        {
            var c = line.Split('\t');
            if (c.Length < 3 || c[0] == "material") continue;
            string mname = c[0], tex = c[1]; bool clip = c[2] == "1";
            var dst = folder + "/" + tex; var src = Path.Combine(dir, "Cards", tex);
            if (!SameFile(Path.GetFullPath(dst), src)) { File.Copy(src, Path.GetFullPath(dst), true); AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceSynchronousImport); }
            var ti = (TextureImporter)AssetImporter.GetAtPath(dst);
            bool dirty = false;
            var asrc = clip ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            if (ti.wrapMode != TextureWrapMode.Clamp) { ti.wrapMode = TextureWrapMode.Clamp; dirty = true; }
            if (ti.alphaSource != asrc) { ti.alphaSource = asrc; dirty = true; }
            if (ti.alphaIsTransparency != clip) { ti.alphaIsTransparency = clip; dirty = true; }
            if (ti.mipMapsPreserveCoverage != clip) { ti.mipMapsPreserveCoverage = clip; ti.alphaTestReferenceValue = 0.5f; dirty = true; }
            if (ti.npotScale != TextureImporterNPOTScale.ToNearest) { ti.npotScale = TextureImporterNPOTScale.ToNearest; dirty = true; }
            if (ti.maxTextureSize != 4096) { ti.maxTextureSize = 4096; dirty = true; }
            if (dirty) ti.SaveAndReimport();
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(dst);
            var mp = folder + "/" + mname + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, mp); }
            m.shader = sh;
            m.SetTexture("_BaseColorMap", t); m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Metallic", 0f); m.SetFloat("_Smoothness", 0.12f);
            m.SetFloat("_AlphaCutoffEnable", clip ? 1f : 0f); m.SetFloat("_AlphaCutoff", 0.5f);
            m.SetFloat("_DoubleSidedEnable", 0f);
            ResetKeywords(m);
            EditorUtility.SetDirty(m);
            res[mname] = m;
        }
        AssetDatabase.SaveAssets();
        return res;
    }

    // 원본 프리팹의 렌더러 재질(이름 → 재질) + 판 재질 + 검은 판
    static Dictionary<string, Material> MaterialMap(string dir, string outFolder, string originalPrefab)
    {
        var map = new Dictionary<string, Material>();
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(originalPrefab);
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            foreach (var m in r.sharedMaterials)
                if (m != null && !map.ContainsKey(m.name)) map[m.name] = m;
        foreach (var kv in CardMaterials(dir, outFolder)) map[kv.Key] = kv.Value;
        var black = AssetDatabase.LoadAssetAtPath<Material>("Assets/02.Resources/01.Asset/Models/Gyeongbokgung/_Shared/Materials/MAT_RoofPlate_Black.mat");
        if (black != null) map["MAT_RoofPlate_Black"] = black;
        return map;
    }

    // ---------- 여는 문짝 판 ----------
    public static string BuildDoorsCard(string dir, string outFolder, string doorsPrefab, string newName, string originalPrefab)
    {
        var mats = MaterialMap(dir, outFolder, originalPrefab);
        var bin = Path.Combine(dir, "doors.bin");
        var folder = outFolder + "/DoorCards"; EnsureFolder(folder);
        var meshes = new Dictionary<string, Mesh>(); var dmat = new Dictionary<string, Material>();
        foreach (var kv in Index(bin))
        {
            var b = ReadAt(bin, kv.Value);
            var ap = folder + "/" + b.name + "_Card.asset";
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(ap);
            b.name = b.name + "_Card";
            if (old != null) { ToMesh(b, old); EditorUtility.SetDirty(old); meshes[kv.Key] = old; }
            else { var m = ToMesh(b, null); AssetDatabase.CreateAsset(m, ap); meshes[kv.Key] = m; }
            if (!mats.TryGetValue(b.mats[0], out var mm)) throw new Exception("판 재질 없음: " + b.mats[0]);
            dmat[kv.Key] = mm;
        }
        AssetDatabase.SaveAssets();
        var dst = outFolder + "/" + newName + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(dst) == null) AssetDatabase.CopyAsset(doorsPrefab, dst);
        var root = PrefabUtility.LoadPrefabContents(dst);
        int n = 0; var sb = new StringBuilder();
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            var src = mf.sharedMesh; if (src == null) continue;
            var key = src.name.EndsWith("_Card") ? src.name.Substring(0, src.name.Length - 5) : src.name;
            if (!meshes.ContainsKey(key)) { sb.Append(" skip:" + src.name); continue; }
            mf.sharedMesh = meshes[key];
            var mr = mf.GetComponent<MeshRenderer>(); mr.sharedMaterials = new[] { dmat[key] };
            var mc = mf.GetComponent<MeshCollider>(); if (mc != null) mc.sharedMesh = meshes[key];
            n++;
        }
        PrefabUtility.SaveAsPrefabAsset(root, dst);
        PrefabUtility.UnloadPrefabContents(root);
        return "doors " + n + sb.ToString();
    }

    // ---------- 프리팹 ----------
    class Part { public string name; public Vector3 refp; public float size; public float[] h = new float[3]; public List<List<string>> lods = new List<List<string>>(); public HashSet<string> col = new HashSet<string>(); }

    public static string BuildPrefab(string dir, string outFolder, string originalPrefab)
    {
        var mats = MaterialMap(dir, outFolder, originalPrefab);
        string prefabName = null; string doorsName = null;
        var parts = new List<Part>(); Part cur = null;
        foreach (var line in File.ReadAllLines(Path.Combine(dir, "layout.txt"), Encoding.UTF8))
        {
            if (line.StartsWith("#") || line.Trim() == "") continue;
            var c = line.Split('\t');
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            if (c[0] == "PREFAB") prefabName = c[1];
            else if (c[0] == "DOORS") doorsName = c[1];
            else if (c[0] == "PART")
            {
                cur = new Part { name = c[1], refp = new Vector3(float.Parse(c[2], inv), float.Parse(c[3], inv), float.Parse(c[4], inv)), size = float.Parse(c[5], inv) };
                for (int k = 0; k < 3; k++) cur.h[k] = float.Parse(c[6 + k], inv);
                parts.Add(cur);
            }
            else if (c[0] == "COPYFAR")
            {
                int lod = int.Parse(c[1]);
                while (cur.lods.Count <= lod) cur.lods.Add(new List<string>());
                cur.lods[lod].Add("COPYFAR|" + c[2] + "|" + c[3]);
            }
            else if (c[0] == "COPYFARBOX")
            {
                int lod = int.Parse(c[1]);
                while (cur.lods.Count <= lod) cur.lods.Add(new List<string>());
                cur.lods[lod].Add("COPYFARBOX|" + c[2] + "|" + c[3] + "|" + c[4] + "|" + c[5] + "|" + c[6] + "|" + c[7]);
            }
            else if (c[0] == "MESH")
            {
                int lod = int.Parse(c[1]);
                while (cur.lods.Count <= lod) cur.lods.Add(new List<string>());
                cur.lods[lod].Add(c[2]);
                if (c.Length > 3 && c[3] == "collider") cur.col.Add(c[2]);
            }
        }
        var go = new GameObject(prefabName);
        var missing = new HashSet<string>(); int nr = 0; long tris = 0;
        foreach (var p in parts)
        {
            var pg = new GameObject(p.name); pg.transform.SetParent(go.transform, false);
            var lods = new List<LOD>();
            for (int l = 0; l < p.lods.Count; l++)
            {
                var rs = new List<Renderer>();
                foreach (var mn in p.lods[l])
                {
                    if (mn.StartsWith("COPYFAR|"))
                    {
                        var q = mn.Split('|');
                        foreach (var r in CopyFar(q[1], q[2], pg.transform)) { rs.Add(r); nr++; }
                        continue;
                    }
                    if (mn.StartsWith("COPYFARBOX|"))
                    {
                        var q = mn.Split('|'); var ci = System.Globalization.CultureInfo.InvariantCulture;
                        var box = new Vector4(float.Parse(q[3], ci), float.Parse(q[4], ci), float.Parse(q[5], ci), float.Parse(q[6], ci));
                        foreach (var r in CopyFarBox(q[1], q[2], box, pg.transform, outFolder + "/Far", p.name)) { rs.Add(r); nr++; }
                        continue;
                    }
                    var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(outFolder + "/Meshes/" + mn + ".asset");
                    if (mesh == null) throw new Exception("메시 없음: " + mn);
                    var o = new GameObject(mn); o.transform.SetParent(pg.transform, false);
                    o.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var mr = o.AddComponent<MeshRenderer>();
                    // 메시 서브메시 순서 = 재질 이름 순서(메시 에셋에는 이름이 없으므로 bin 에서 다시 읽음)
                    mr.sharedMaterials = SubMats(dir, mn).Select(x => { Material m; if (!mats.TryGetValue(x, out m)) { missing.Add(x); return null; } return m; }).ToArray();
                    if (p.col.Contains(mn)) o.AddComponent<MeshCollider>().sharedMesh = mesh;
                    rs.Add(mr); nr++;
                    if (l == 0) for (int s = 0; s < mesh.subMeshCount; s++) tris += (long)mesh.GetIndexCount(s) / 3;
                }
                float h = l == p.lods.Count - 1 ? p.h[2] : p.h[l];
                lods.Add(new LOD(h, rs.ToArray()));
            }
            var lg = pg.AddComponent<LODGroup>();
            lg.fadeMode = LODFadeMode.CrossFade; lg.animateCrossFading = true;
            lg.SetLODs(lods.ToArray());
            lg.localReferencePoint = p.refp; lg.size = p.size;      // SetLODs 뒤에 넣어야 남음
        }
        if (doorsName != null)
        {
            var dp = AssetDatabase.LoadAssetAtPath<GameObject>(outFolder + "/" + doorsName + ".prefab");
            if (dp == null) throw new Exception("문짝 프리팹 없음: " + doorsName);
            var d = (GameObject)PrefabUtility.InstantiatePrefab(dp); d.transform.SetParent(go.transform, false);
        }
        var path = outFolder + "/" + prefabName + ".prefab";
        PrefabUtility.SaveAsPrefabAsset(go, path);
        UnityEngine.Object.DestroyImmediate(go);
        if (missing.Count > 0) return "MISSING MATERIALS: " + string.Join(",", missing);
        return "prefab " + path + " parts=" + parts.Count + " renderers=" + nr + " LOD0 tris=" + tris;
    }

    // 다른 프리팹(예: <단지>_Complex)의 오브젝트에서 렌더러를 복사(메시·재질·그림자만). LODGroup 이 있으면 마지막 단계만.
    static List<Renderer> CopyFar(string prefabPath, string objectPath, Transform parent)
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (root == null) throw new Exception("프리팹 없음: " + prefabPath);
        var t = root.transform.Find(objectPath);
        if (t == null) throw new Exception("오브젝트 없음: " + prefabPath + " / " + objectPath);
        var srcs = new List<Renderer>();
        var lg = t.GetComponent<LODGroup>();
        if (lg != null) { var ls = lg.GetLODs(); foreach (var r in ls[ls.Length - 1].renderers) if (r != null) srcs.Add(r); }
        else srcs.AddRange(t.GetComponentsInChildren<Renderer>(true));
        var list = new List<Renderer>();
        foreach (var r in srcs)
        {
            var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
            var o = new GameObject(r.name); o.transform.SetParent(parent, false);
            var m = root.transform.worldToLocalMatrix * r.transform.localToWorldMatrix;
            o.transform.localPosition = m.GetColumn(3); o.transform.localRotation = m.rotation; o.transform.localScale = m.lossyScale;
            o.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            var mr = o.AddComponent<MeshRenderer>(); mr.sharedMaterials = r.sharedMaterials; mr.shadowCastingMode = r.shadowCastingMode;
            list.Add(mr);
        }
        return list;
    }

    // CopyFar 와 같은 렌더러에서 무게중심(프리팹 루트 좌표)이 box(x최소, x최대, z최소, z최대) 안인 삼각형만 잘라 새 메시 에셋으로.
    // 메시는 원래 렌더러의 자기 좌표 그대로(오브젝트 변환도 원래와 같게), 삼각형이 하나도 없는 서브메시·렌더러는 뺌.
    // 예전엔 부분 = 건물 코드라 구운 덩어리를 통째로 붙였는데, 영추문은 부분을 건물별로 나누어 구운 덩어리도 건물별로 잘라야 했음.
    static List<Renderer> CopyFarBox(string prefabPath, string objectPath, Vector4 box, Transform parent, string folder, string partName)
    {
        EnsureFolder(folder);
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (root == null) throw new Exception("프리팹 없음: " + prefabPath);
        var t = root.transform.Find(objectPath);
        if (t == null) throw new Exception("오브젝트 없음: " + prefabPath + " / " + objectPath);
        var srcs = new List<Renderer>();
        var lg = t.GetComponent<LODGroup>();
        if (lg != null) { var ls = lg.GetLODs(); foreach (var r in ls[ls.Length - 1].renderers) if (r != null) srcs.Add(r); }
        else srcs.AddRange(t.GetComponentsInChildren<Renderer>(true));
        var list = new List<Renderer>();
        foreach (var r in srcs)
        {
            var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
            var src = mf.sharedMesh;
            var m = root.transform.worldToLocalMatrix * r.transform.localToWorldMatrix;
            var v = src.vertices; var nrm = src.normals; var tan = src.tangents; var uv = src.uv;
            var mats = r.sharedMaterials;
            var remap = new Dictionary<int, int>();
            var nv = new List<Vector3>(); var nn = new List<Vector3>(); var nt = new List<Vector4>(); var nu = new List<Vector2>();
            var subs = new List<int[]>(); var subMats = new List<Material>();
            for (int s = 0; s < src.subMeshCount; s++)
            {
                var tri = src.GetTriangles(s); var keep = new List<int>();
                for (int i = 0; i + 2 < tri.Length; i += 3)
                {
                    var c = m.MultiplyPoint3x4((v[tri[i]] + v[tri[i + 1]] + v[tri[i + 2]]) / 3f);
                    if (c.x < box.x || c.x >= box.y || c.z < box.z || c.z >= box.w) continue;
                    for (int k = 0; k < 3; k++)
                    {
                        int o = tri[i + k]; int ni;
                        if (!remap.TryGetValue(o, out ni))
                        {
                            ni = nv.Count; remap[o] = ni; nv.Add(v[o]);
                            if (nrm.Length == v.Length) nn.Add(nrm[o]);
                            if (tan.Length == v.Length) nt.Add(tan[o]);
                            if (uv.Length == v.Length) nu.Add(uv[o]);
                        }
                        keep.Add(ni);
                    }
                }
                if (keep.Count == 0) continue;
                subs.Add(keep.ToArray());
                subMats.Add(s < mats.Length ? mats[s] : (mats.Length > 0 ? mats[mats.Length - 1] : null));
            }
            if (subs.Count == 0) continue;
            var mname = partName + "_" + r.name;
            var ap = folder + "/" + mname + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(ap);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh();
            mesh.Clear();
            mesh.name = mname;
            mesh.indexFormat = nv.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(nv);
            if (nn.Count == nv.Count) mesh.SetNormals(nn);
            if (nt.Count == nv.Count) mesh.SetTangents(nt);
            if (nu.Count == nv.Count) mesh.SetUVs(0, nu);
            mesh.subMeshCount = subs.Count;
            for (int s = 0; s < subs.Count; s++) mesh.SetTriangles(subs[s], s, false);
            mesh.RecalculateBounds();
            PalaceMeshCompact.CompactMesh(mesh);
            if (isNew) AssetDatabase.CreateAsset(mesh, ap); else EditorUtility.SetDirty(mesh);
            var o2 = new GameObject(r.name); o2.transform.SetParent(parent, false);
            o2.transform.localPosition = m.GetColumn(3); o2.transform.localRotation = m.rotation; o2.transform.localScale = m.lossyScale;
            o2.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = o2.AddComponent<MeshRenderer>(); mr.sharedMaterials = subMats.ToArray(); mr.shadowCastingMode = r.shadowCastingMode;
            list.Add(mr);
        }
        AssetDatabase.SaveAssets();
        return list;
    }

    static Dictionary<string, List<string>> subMatCache;
    static List<string> SubMats(string dir, string meshName)
    {
        if (subMatCache == null) subMatCache = new Dictionary<string, List<string>>();
        var key = dir + "|" + meshName;
        if (subMatCache.TryGetValue(key, out var r)) return r;
        var path = Path.Combine(dir, "meshes.bin");
        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
        using (var br = new BinaryReader(fs))
        {
            br.ReadInt32(); int n = br.ReadInt32();
            for (int k = 0; k < n; k++)
            {
                string name = Encoding.UTF8.GetString(br.ReadBytes(br.ReadInt32()));
                int nv = br.ReadInt32(), ns = br.ReadInt32();
                fs.Seek((long)nv * 32, SeekOrigin.Current);
                var ms = new List<string>();
                for (int s = 0; s < ns; s++) { ms.Add(Encoding.UTF8.GetString(br.ReadBytes(br.ReadInt32()))); int ni = br.ReadInt32(); fs.Seek((long)ni * 4, SeekOrigin.Current); }
                subMatCache[dir + "|" + name] = ms;
            }
        }
        return subMatCache[key];
    }

    // ---------- 씬 배치 ----------
    // holderPath 아래 oldName 과 같은 자리(로컬 변환)에 프리팹을 두고 oldName 은 끔. 씬 저장은 사용자.
    public static string Place(string holderPath, string oldName, string prefabPath, string newName)
    {
        var holder = GameObject.Find(holderPath);
        if (holder == null) return "holder not found";
        var old = holder.transform.Find(oldName);
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        var ex = holder.transform.Find(newName);
        GameObject inst = ex != null ? ex.gameObject : (GameObject)PrefabUtility.InstantiatePrefab(pf, holder.transform);
        inst.name = newName;
        if (old != null)
        {
            inst.transform.localPosition = old.localPosition; inst.transform.localRotation = old.localRotation; inst.transform.localScale = old.localScale;
            old.gameObject.SetActive(false);
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(holder.scene);
        return "placed " + inst.name + " at " + inst.transform.localPosition + " old active=" + (old != null && old.gameObject.activeSelf);
    }
}
