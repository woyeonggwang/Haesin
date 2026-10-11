// 지붕 모양 상자(임포스터) 가져오기 — 밖(98.Blender/Impostor/impcx.py)에서 만든 것을 재질·프리팹으로. MCP 에서 정적 메서드로 부름.
//
// 밖에서 만든 것(단지 <cx> 마다):
//   <cx>_Imp.bin   HNMS 메시 묶음: 오브젝트마다 '<이름>_Imp'(서브메시 = 그림 페이지, 재질 이름 IMP_<cx>_P<k>) — 좌표 = 단지 프리팹 루트 좌표
//   <cx>_Keep.bin  그대로 둘 원본(삼각형이 아주 적은 오브젝트·넓은 땅판): '<이름>_Keep', 재질 이름 = 원래 재질 이름
//   IMP_<cx>_P<k>_BC.png   기본색(AO 를 곱해 구움, sRGB, 파일이 크면 .jpg) → DXT1
//   IMP_<cx>_P<k>_NOS.png  물체 공간 노멀(0.5 + 0.5·n, 반 크기) → BC7, sRGB 끔
// 재질 = HDRP/Lit, Normal Map Space = Object(메시 탄젠트가 필요 없음), 마스크 없음(매끄러움 0.2 고정).
// 예전(영추문 ㄱ자 시험, 2026-09-30)은 건물 하나에 2048 그림 3장(BC·NOS·MASK)이었는데, 단지를 통째로 바꾸며 여러 건물이
// 4096 페이지를 함께 쓰고 AO 는 색에 곱해 마스크를 뺐다(그림 메모리 페이지당 약 16 MB).
//
// 순서: 그림(jpg·png)을 <Root>/<cx>/ 에 넣고 → Setup(cx, binDir, simplifiedPrefab) → PalaceLodBinImport.Place(홀더, 옛 이름, 프리팹, 새 이름)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class PalaceImpostorTool
{
    public const string Root = "Assets/02.Resources/01.Asset/Models/Gyeongbokgung/_Baked/Impostor";
    const int Magic = 0x534D4E48;

    static System.Reflection.MethodInfo resetKw;
    static void ResetKeywords(Material m)
    {
        if (resetKw == null)
        {
            var t = Type.GetType("UnityEditor.Rendering.HighDefinition.HDShaderUtils, Unity.RenderPipelines.HighDefinition.Editor");
            resetKw = t.GetMethods().First(mi => mi.Name == "ResetMaterialKeywords" && mi.GetParameters().Length == 1 && mi.GetParameters()[0].ParameterType == typeof(Material));
        }
        resetKw.Invoke(null, new object[] { m });
    }

    // 메시 이름 → 서브메시 재질 이름
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

    static void SetTex(string path, bool srgb, bool hq)
    {
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        if (ti == null) throw new Exception("그림 없음: " + path);
        bool d = false;
        if (ti.textureType != TextureImporterType.Default) { ti.textureType = TextureImporterType.Default; d = true; }
        if (ti.sRGBTexture != srgb) { ti.sRGBTexture = srgb; d = true; }
        if (ti.alphaSource != TextureImporterAlphaSource.None) { ti.alphaSource = TextureImporterAlphaSource.None; d = true; }
        if (ti.wrapMode != TextureWrapMode.Clamp) { ti.wrapMode = TextureWrapMode.Clamp; d = true; }
        if (ti.maxTextureSize != 4096) { ti.maxTextureSize = 4096; d = true; }
        if (!ti.streamingMipmaps) { ti.streamingMipmaps = true; d = true; }
        if (!ti.mipmapEnabled) { ti.mipmapEnabled = true; d = true; }
        if (ti.anisoLevel != 4) { ti.anisoLevel = 4; d = true; }
        var ps = ti.GetPlatformTextureSettings("Standalone");
        var fmt = hq ? TextureImporterFormat.BC7 : TextureImporterFormat.DXT1;
        if (!ps.overridden || ps.format != fmt || ps.maxTextureSize != 4096)
        {
            ps.overridden = true; ps.format = fmt; ps.maxTextureSize = 4096; ps.compressionQuality = 50;
            ti.SetPlatformTextureSettings(ps); d = true;
        }
        if (d) ti.SaveAndReimport();
    }

    // 페이지 재질 IMP_<cx>_P<k>
    public static string Materials(string cx, int pages, float smoothness = 0.2f)
    {
        var folder = Root + "/" + cx;
        var sh = Shader.Find("HDRP/Lit");
        var sb = new StringBuilder();
        for (int k = 0; k < pages; k++)
        {
            string bc = folder + "/IMP_" + cx + "_P" + k + "_BC.png", nos = folder + "/IMP_" + cx + "_P" + k + "_NOS.png";
            if (AssetImporter.GetAtPath(bc) == null) bc = bc.Substring(0, bc.Length - 4) + ".jpg";      // 20 MB 넘는 PNG 는 jpg 로 옮김
            SetTex(bc, true, false); SetTex(nos, false, true);
            var mp = folder + "/IMP_" + cx + "_P" + k + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, mp); }
            m.shader = sh;
            m.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(bc)); m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_NormalMapSpace", 1f); m.SetTexture("_NormalMapOS", AssetDatabase.LoadAssetAtPath<Texture2D>(nos)); m.SetTexture("_NormalMap", null);
            m.SetTexture("_MaskMap", null);
            m.SetFloat("_Metallic", 0f); m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_AlphaCutoffEnable", 0f); m.SetFloat("_DoubleSidedEnable", 0f);
            ResetKeywords(m);
            EditorUtility.SetDirty(m);
            sb.Append(m.name + "[" + string.Join(" ", m.shaderKeywords) + "] ");
        }
        AssetDatabase.SaveAssets();
        return sb.ToString();
    }

    // 프리팹 <cx>_Impostor: 오브젝트마다 LODGroup 하나(화면 높이 cull 아래면 끔) + Keep(원본 그대로, 재질은 simplifiedPrefab 에서 이름으로)
    public static string BuildPrefab(string cx, string binDir, string simplifiedPrefab, float cull = 0.002f)
    {
        var folder = Root + "/" + cx;
        var matByName = new Dictionary<string, Material>();
        for (int k = 0; ; k++) { var m = AssetDatabase.LoadAssetAtPath<Material>(folder + "/IMP_" + cx + "_P" + k + ".mat"); if (m == null) break; matByName[m.name] = m; }
        var orig = AssetDatabase.LoadAssetAtPath<GameObject>(simplifiedPrefab);
        var origR = new Dictionary<string, Renderer>();
        if (orig != null)
            foreach (var r in orig.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var m in r.sharedMaterials) if (m != null && !matByName.ContainsKey(m.name)) matByName[m.name] = m;
                var nm = r.name.EndsWith("_LOD0") ? r.name.Substring(0, r.name.Length - 5) : r.name;
                if (!origR.ContainsKey(nm)) origR[nm] = r;
            }
        var go = new GameObject(cx + "_Impostor");
        var missing = new HashSet<string>(); long tris = 0; int nr = 0;
        foreach (var kind in new[] { "Imp", "Keep" })
        {
            var bin = Path.Combine(binDir, cx + "_" + kind + ".bin");
            if (!File.Exists(bin)) continue;
            Transform parent = go.transform;
            if (kind == "Keep") { var kg = new GameObject("Keep"); kg.transform.SetParent(go.transform, false); parent = kg.transform; }
            foreach (var kv in ReadSubMats(bin))
            {
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(folder + "/Meshes/" + kv.Key + ".asset");
                if (mesh == null) throw new Exception("메시 없음: " + kv.Key);
                var oname = kv.Key.Substring(0, kv.Key.Length - kind.Length - 1);
                var o = new GameObject(kind == "Imp" ? oname : kv.Key); o.transform.SetParent(parent, false);
                o.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = o.AddComponent<MeshRenderer>();
                mr.sharedMaterials = kv.Value.Select(x => { Material m; if (!matByName.TryGetValue(x, out m)) { missing.Add(x); return null; } return m; }).ToArray();
                Renderer src;
                if (kind == "Keep" && origR.TryGetValue(oname, out src)) { mr.shadowCastingMode = src.shadowCastingMode; mr.receiveShadows = src.receiveShadows; }
                else mr.shadowCastingMode = ShadowCastingMode.On;
                var lg = o.AddComponent<LODGroup>();
                lg.fadeMode = LODFadeMode.None;
                lg.SetLODs(new[] { new LOD(cull, new Renderer[] { mr }) });
                lg.RecalculateBounds();
                for (int s = 0; s < mesh.subMeshCount; s++) tris += (long)mesh.GetIndexCount(s) / 3;
                nr++;
            }
        }
        var path = folder + "/" + cx + "_Impostor.prefab";
        PrefabUtility.SaveAsPrefabAsset(go, path);
        UnityEngine.Object.DestroyImmediate(go);
        return (missing.Count > 0 ? "MISSING " + string.Join(",", missing) + " " : "") + "prefab " + path + " renderers=" + nr + " tris=" + tris;
    }

    // 한 번에: 그림 설정·재질 → 메시(ImportMeshes, 시간 제한) → 프리팹. 메시가 덜 됐으면 MORE 를 돌려줌(다시 부르면 이어서).
    public static string Setup(string cx, string binDir, string simplifiedPrefab, int pages)
    {
        var folder = Root + "/" + cx;
        var sb = new StringBuilder();
        foreach (var kind in new[] { "Imp", "Keep" })
        {
            var bin = cx + "_" + kind + ".bin";
            if (!File.Exists(Path.Combine(binDir, bin))) continue;
            var r = PalaceLodBinImport.ImportMeshes(binDir, folder, 20f, "", bin, "Meshes", 1000);
            sb.Append(kind + ": " + r + " | ");
            if (r.StartsWith("MORE")) return "MORE " + sb;
        }
        sb.Append(Materials(cx, pages)).Append(" | ");
        sb.Append(BuildPrefab(cx, binDir, simplifiedPrefab));
        return sb.ToString();
    }

    // 단지 임포스터에서 몇 오브젝트만 모은 묶음 프리팹(빈터 채우기용, 같은 그림 페이지·재질을 다시 씀 — 그림 메모리 추가 없음).
    // 오브젝트들을 메시 하나(재질마다 서브메시)로 합침 — 묶음 하나 = 렌더러 하나(그리기 호출 = 페이지 수).
    // (px, py, pz) = 단지 좌표의 기준점(묶음 가운데·땅 높이) → 묶음 루트가 이 점. 결과 Root/Kits/<kitName>.prefab, 메시 Root/Kits/Meshes/<kitName>.asset
    public static string BuildKit(string cx, string kitName, string objNamesCsv, float px, float py, float pz, float cull = 0.002f)
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/" + cx + "/" + cx + "_Impostor.prefab");
        if (src == null) return "no impostor prefab: " + cx;
        EnsureFolder(Root + "/Kits/Meshes");
        var pivot = new Vector3(px, py, pz);
        var mats = new List<Material>(); var idx = new List<List<int>>();
        var V = new List<Vector3>(); var N = new List<Vector3>(); var U = new List<Vector2>();
        var sb = new StringBuilder();
        foreach (var nm in objNamesCsv.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var t = src.transform.Find(nm.Trim()) ?? src.transform.Find("Keep/" + nm.Trim() + "_Keep");
            if (t == null) { sb.Append(" missing:" + nm); continue; }
            var mesh = t.GetComponent<MeshFilter>().sharedMesh; var mm = t.GetComponent<MeshRenderer>().sharedMaterials;
            var M = src.transform.worldToLocalMatrix * t.localToWorldMatrix;
            var v = mesh.vertices; var n = mesh.normals; var u = mesh.uv;
            int b0 = V.Count;
            for (int i = 0; i < v.Length; i++) { V.Add(M.MultiplyPoint3x4(v[i]) - pivot); N.Add(n.Length == v.Length ? M.MultiplyVector(n[i]).normalized : Vector3.up); U.Add(u.Length == v.Length ? u[i] : Vector2.zero); }
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                var mat = s < mm.Length ? mm[s] : null;
                int gi = mats.IndexOf(mat);
                if (gi < 0) { mats.Add(mat); idx.Add(new List<int>()); gi = mats.Count - 1; }
                foreach (var k in mesh.GetTriangles(s)) idx[gi].Add(k + b0);
            }
        }
        var ap = Root + "/Kits/Meshes/" + kitName + ".asset";
        var km = AssetDatabase.LoadAssetAtPath<Mesh>(ap); bool isNew = km == null;
        if (isNew) km = new Mesh();
        km.Clear(); km.name = kitName;
        km.indexFormat = V.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        km.SetVertices(V); km.SetNormals(N); km.SetUVs(0, U);
        km.subMeshCount = idx.Count;
        for (int s = 0; s < idx.Count; s++) km.SetTriangles(idx[s], s, false);
        km.RecalculateBounds();
        PalaceMeshCompact.CompactMesh(km);
        if (isNew) AssetDatabase.CreateAsset(km, ap); else EditorUtility.SetDirty(km);
        AssetDatabase.SaveAssets();
        var go = new GameObject(kitName);
        go.AddComponent<MeshFilter>().sharedMesh = km;
        var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterials = mats.ToArray(); mr.shadowCastingMode = ShadowCastingMode.On;
        var lg = go.AddComponent<LODGroup>(); lg.fadeMode = LODFadeMode.None;
        lg.SetLODs(new[] { new LOD(cull, new Renderer[] { mr }) }); lg.RecalculateBounds();
        var path = Root + "/Kits/" + kitName + ".prefab";
        PrefabUtility.SaveAsPrefabAsset(go, path);
        UnityEngine.Object.DestroyImmediate(go);
        long tris = 0; for (int s = 0; s < km.subMeshCount; s++) tris += (long)km.GetIndexCount(s) / 3;
        return "kit " + kitName + " verts=" + km.vertexCount + " tris=" + tris + " mats=" + mats.Count + sb;
    }

    // 빈터 배치: plan = 줄마다 "묶음\tx\tz\t회전[\t배율x\t배율y\t배율z]" (월드 x·z = 묶음 기준점, 배율 = 묶음 로컬, 배율x 음수 = 거울).
    // 땅 높이: groundY 가 NaN 이면 묶음 발자국 안(가장자리 1 m 안쪽 5×5 점) 터레인 높이의 가운데값(단, 최저 + 0.5 m 를 넘지 않게), 아니면 groundY.
    // (2026-09-30 동궁 자리 터레인을 올려 땅이 한 높이가 아니게 됨. 최고점을 쓰면 발자국 가장자리의 작은 둔덕 하나에 묶음 전체가 들림 —
    //  가운데값을 쓰고, 묶음 기단이 땅 아래 0.6 m 까지 있으므로 최저점 + 0.5 m 로 막아 낮은 쪽이 뜨지 않게. 높이 차가 0.3 m 넘으면 결과에 적음.)
    // holderPath 아래 이름 <묶음>_<번호> 로(있으면 옮김). 씬 저장은 사용자.
    public static string PlaceFill(string holderPath, string planFile, float groundY)
    {
        var holder = GameObject.Find(holderPath);
        if (holder == null) return "holder not found: " + holderPath;
        int n = 0; var sb = new StringBuilder(); var inv = System.Globalization.CultureInfo.InvariantCulture;
        var keep = new HashSet<string>();
        foreach (var line in File.ReadAllLines(planFile, Encoding.UTF8))
        {
            var c = line.Split('\t'); if (c.Length < 4 || c[0].StartsWith("#")) continue;
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Kits/" + c[0] + ".prefab");
            if (pf == null) { sb.Append(" no kit " + c[0]); continue; }
            var name = c[0] + "_" + (n + 1).ToString("00");
            var ex = holder.transform.Find(name);
            if (ex != null && PrefabUtility.GetCorrespondingObjectFromSource(ex.gameObject) != pf) { UnityEngine.Object.DestroyImmediate(ex.gameObject); ex = null; }
            var inst = ex != null ? ex.gameObject : (GameObject)PrefabUtility.InstantiatePrefab(pf, holder.transform);
            inst.name = name; keep.Add(name);
            var scale = c.Length >= 7 ? new Vector3(float.Parse(c[4], inv), float.Parse(c[5], inv), float.Parse(c[6], inv)) : Vector3.one;
            var rot = Quaternion.Euler(0, float.Parse(c[3], inv), 0);
            float x = float.Parse(c[1], inv), z = float.Parse(c[2], inv), gy = groundY;
            if (float.IsNaN(groundY))
            {
                float lo, med, hi; GroundUnder(pf, new Vector3(x, 0, z), rot, scale, out lo, out med, out hi);
                gy = Mathf.Min(med, lo + 0.5f);
                if (hi - lo > 0.3f) sb.Append(" " + name + " 땅차 " + (hi - lo).ToString("F2"));
            }
            inst.transform.position = new Vector3(x, gy, z); inst.transform.rotation = rot; inst.transform.localScale = scale;
            n++;
        }
        // 계획에 없는 옛 배치는 지움(이 홀더 아래 묶음 인스턴스만)
        var drop = new List<GameObject>();
        foreach (Transform t in holder.transform) if (!keep.Contains(t.name) && PrefabUtility.IsPartOfPrefabInstance(t.gameObject)) drop.Add(t.gameObject);
        foreach (var g in drop) UnityEngine.Object.DestroyImmediate(g);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(holder.scene);
        return "placed " + n + " removed " + drop.Count + sb;
    }

    // 묶음 프리팹 메시 경계(로컬)를 (위치 xz, 회전, 배율)로 옮긴 발자국 안 5×5 점의 터레인 높이 최저·가운데값·최고(터레인 밖이면 0)
    static void GroundUnder(GameObject pf, Vector3 pos, Quaternion rot, Vector3 scale, out float lo, out float med, out float hi)
    {
        lo = float.MaxValue; hi = float.MinValue; var hs = new List<float>();
        var mf = pf.GetComponentInChildren<MeshFilter>();
        var b = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one);
        float ix = Mathf.Max(0f, b.extents.x - 1f), iz = Mathf.Max(0f, b.extents.z - 1f);
        for (int a = 0; a <= 4; a++)
            for (int e = 0; e <= 4; e++)
            {
                var lp = new Vector3(b.center.x + ix * (a / 2f - 1f), 0, b.center.z + iz * (e / 2f - 1f));
                var wp = pos + rot * Vector3.Scale(lp, scale);
                float h = 0f; bool got = false;
                foreach (var t in Terrain.activeTerrains)
                {
                    var tp = t.transform.position; var sz = t.terrainData.size;
                    if (wp.x < tp.x || wp.z < tp.z || wp.x > tp.x + sz.x || wp.z > tp.z + sz.z) continue;
                    h = t.SampleHeight(wp) + tp.y; got = true; break;
                }
                if (!got) continue;
                lo = Mathf.Min(lo, h); hi = Mathf.Max(hi, h); hs.Add(h);
            }
        if (hs.Count == 0) { lo = med = hi = 0f; return; }
        hs.Sort(); med = hs[hs.Count / 2];
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    // 프리팹 인스턴스를 holderPath 아래 로컬 (위치, y 회전)에 둠(같은 이름이 있으면 옮김). 씬 저장은 사용자.
    public static string PlaceAt(string holderPath, string prefabPath, string name, float x, float y, float z, float rotY)
    {
        var holder = GameObject.Find(holderPath);
        if (holder == null) return "holder not found: " + holderPath;
        var ex = holder.transform.Find(name);
        GameObject inst = ex != null ? ex.gameObject : (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath), holder.transform);
        inst.name = name;
        inst.transform.localPosition = new Vector3(x, y, z); inst.transform.localRotation = Quaternion.Euler(0, rotY, 0); inst.transform.localScale = Vector3.one;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(holder.scene);
        return "placed " + name;
    }
}
