// 데모 씬(PalaceBakeTool.OpenComplex 로 y +3000 에 연 것)의 원본 렌더러를 손대지 않은 채 그룹별 OBJ 로 내보내는 도구 — MCP 에서 정적 메서드로 호출.
// 좌표 = 단지 프리팹 루트 기준(데모 루트 y +3000 을 뺌), x 는 뒤집어 씀(LoadObj 규칙: Unity x = −OBJ x). 면 순서도 뒤집음.
// 텍스처는 OBJ 에 넣지 않고 tex_map.txt(재질|BaseColor 경로|Normal 경로)로 따로 적음 — 받는 쪽이 직접 머티리얼을 만듦
// (MTL 절대 경로를 OBJ 폴더 기준으로 붙이는 임포터가 있어서).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PalaceOrigExport
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    static UnityEngine.SceneManagement.Scene FindScene(string contains)
    {
        for (int i = 0; i < EditorSceneManager.sceneCount; i++) { var s = EditorSceneManager.GetSceneAt(i); if (s.path.Contains(contains)) return s; }
        return default(UnityEngine.SceneManagement.Scene);
    }

    // 메시 이름 SM_<코드>_<부위><번호> 의 부위(번호·끝 글자 뗀 것)
    public static string PartKey(string meshName)
    {
        var p = meshName.Split('_'); var key = p.Length > 2 ? p[2] : meshName;
        return System.Text.RegularExpressions.Regex.Replace(key, @"\d+[A-Za-z]?$", "");
    }

    // group(r) 이 돌려준 이름별로 OBJ 하나씩. 이미 있는 파일은 건너뜀(MCP 재시도 대비) — 다시 만들려면 지우고 부름.
    public static string Export(string sceneContains, string outDir, string prefix, Func<MeshRenderer, string> group, string onlyGroup)
    {
        var sc = FindScene(sceneContains); if (!sc.IsValid()) return "scene not open";
        Directory.CreateDirectory(outDir);
        var items = new Dictionary<string, List<MeshRenderer>>();
        Transform holder = sc.GetRootGameObjects()[0].transform;
        foreach (var root in sc.GetRootGameObjects())
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
                var g = group(r); if (g == null) continue;
                if (onlyGroup != null && g != onlyGroup) continue;
                List<MeshRenderer> l; if (!items.TryGetValue(g, out l)) { l = new List<MeshRenderer>(); items[g] = l; }
                l.Add(r);
            }
        var log = new System.Text.StringBuilder();
        var texMap = new Dictionary<string, string>();
        foreach (var kv in items)
        {
            var path = Path.Combine(outDir, prefix + "_" + kv.Key + "_orig.obj");
            long tris = 0; int i = 0, vb = 1;
            if (File.Exists(path)) { log.Append(kv.Key + " SKIP(exists); "); }
            else using (var w = new StreamWriter(path, false, new System.Text.UTF8Encoding(false), 1 << 20))
            {
                foreach (var r in kv.Value)
                {
                    var m = r.GetComponent<MeshFilter>().sharedMesh;
                    var M = holder.worldToLocalMatrix * r.transform.localToWorldMatrix; var N = M.inverse.transpose; bool mir = M.determinant < 0;
                    var v = m.vertices; var n = m.normals; var uv = m.uv;
                    w.Write("o O" + (i++) + "_" + m.name + "\n");
                    foreach (var p in v) { var q = M.MultiplyPoint3x4(p); w.Write("v " + (-q.x).ToString("0.#####", Inv) + " " + q.y.ToString("0.#####", Inv) + " " + q.z.ToString("0.#####", Inv) + "\n"); }
                    foreach (var t in uv) w.Write("vt " + t.x.ToString("0.#####", Inv) + " " + t.y.ToString("0.#####", Inv) + "\n");
                    foreach (var q0 in n) { var q = N.MultiplyVector(q0).normalized; w.Write("vn " + (-q.x).ToString("0.####", Inv) + " " + q.y.ToString("0.####", Inv) + " " + q.z.ToString("0.####", Inv) + "\n"); }
                    bool hu = uv.Length > 0, hn = n.Length > 0;
                    var sm = r.sharedMaterials;
                    for (int s = 0; s < m.subMeshCount; s++)
                    {
                        var mat = s < sm.Length ? sm[s] : null; var mn = mat != null ? mat.name : "none";
                        if (mat != null && !texMap.ContainsKey(mn))
                        {
                            var bc = mat.HasProperty("_BaseColorMap") ? mat.GetTexture("_BaseColorMap") : null;
                            var nm = mat.HasProperty("_NormalMap") ? mat.GetTexture("_NormalMap") : null;
                            texMap[mn] = (bc ? Path.GetFullPath(AssetDatabase.GetAssetPath(bc)) : "") + "|" + (nm ? Path.GetFullPath(AssetDatabase.GetAssetPath(nm)) : "");
                        }
                        w.Write("usemtl " + mn + "\n");
                        var idx = m.GetTriangles(s);
                        for (int k = 0; k < idx.Length; k += 3)
                        {
                            int a = idx[k] + vb, b = idx[k + 1] + vb, c = idx[k + 2] + vb;
                            if (!mir) { int tmp = b; b = c; c = tmp; }
                            w.Write("f");
                            foreach (var ix in new[] { a, b, c })
                            {
                                w.Write(" " + ix);
                                if (hu || hn) { w.Write("/"); if (hu) w.Write(ix.ToString()); if (hn) w.Write("/" + ix); }
                            }
                            w.Write("\n"); tris++;
                        }
                    }
                    vb += v.Length;
                }
                log.Append(kv.Key + " renderers=" + kv.Value.Count + " tris=" + tris + "; ");
            }
        }
        var tp = Path.Combine(outDir, "tex_map.txt");
        var old = File.Exists(tp) ? File.ReadAllLines(tp).Where(l => l.Contains("|")).ToDictionary(l => l.Split('|')[0], l => l.Substring(l.IndexOf('|') + 1)) : new Dictionary<string, string>();
        foreach (var kv in texMap) old[kv.Key] = kv.Value;
        File.WriteAllLines(tp, old.OrderBy(k => k.Key).Select(k => k.Key + "|" + k.Value));
        return log.ToString() + " texMap=" + old.Count;
    }

    // 경회루 데모 씬 그룹: 누각(Pavilion) / 월대·석조 기단(Base) / 연못 담·다리(Pond) / 데모 땅·물(Ground)
    static readonly HashSet<string> KhlBase = new HashSet<string> { "HayebdongjaSton", "HCS", "SSSA", "SSSW", "WoldaeStonerailing", "WoldaeA", "WoldaeB", "WoldaeC", "WoldaeD", "StonePiler", "StairA", "StairB", "StairC", "Land", "Stela" };
    static readonly HashSet<string> KhlPond = new HashSet<string> { "StonewallA", "Stonebridg", "DoorframeC", "DoorframeD", "DoorH", "DoorI", "GiwaB", "GiwaC", "YongA", "Mystery", "IS", "Yeonhwadae" };
    static readonly HashSet<string> KhlGround = new HashSet<string> { "Ground", "Grass", "Water" };
    public static string KhlGroup(MeshRenderer r)
    {
        var k = PartKey(r.GetComponent<MeshFilter>().sharedMesh.name);
        if (KhlGround.Contains(k)) return "Ground";
        if (KhlPond.Contains(k)) return "Pond";
        if (KhlBase.Contains(k)) return "Base";
        return "Pavilion";
    }
    public static string ExportGyeonghoeru(string onlyGroup)
    {
        return Export("Demo_Gyeonghoeru", @"D:\Blender\Enviroment\PalaceJoseon\경회루\Original", "Gyeonghoeru", KhlGroup, onlyGroup);
    }
}
