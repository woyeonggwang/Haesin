// 데모 씬(PalaceBakeTool.OpenComplex 로 y +3000 에 연 것)의 렌더러를 "고유 메시 + 배치 목록" 이진 파일로 내보내는 도구 — MCP 에서 정적 메서드로 호출.
// 예전 OBJ 내보내기(PalaceOrigExport)는 렌더러마다 정점을 다시 써서 흥례문(렌더러 5,606 / 고유 메시 250)이면 수 GB 가 되고,
// 받는 쪽에서 같은 메시를 쓰는 인스턴스로 다시 묶을 수도 없었다. 그래서 고유 메시는 한 번만 쓰고(meshes.bin)
// 렌더러는 (메시 번호, 변환 행렬, 서브메시별 재질 이름) 목록(instances.tsv)으로, 재질은 텍스처 경로·색·타일링 목록(materials.tsv)으로 따로 적는다.
// 좌표 = 데모 루트(__VIS_HOLDER) 기준 Unity 좌표 그대로 — 좌표계 변환(Unity 왼손 → 받는 쪽 오른손)은 받는 쪽에서 한다.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class PalaceBinExport
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // outDir 에 meshes.bin / instances.tsv / materials.tsv 를 쓴다. meshes.bin 이 이미 있으면 건너뜀(MCP 재시도 대비 — 다시 만들려면 지우고 부름).
    // meshes.bin: int 매직 'PBX1', int 메시 수, 메시마다 [int 이름 길이, UTF-8 이름, int 정점 수, int 서브메시 수,
    //             float 위치 xyz × n, float 법선 xyz × n, float UV0 uv × n, 서브메시마다 (int 인덱스 수, int 인덱스들)] — 리틀 엔디언.
    public static string Export(string outDir)
    {
        var holder = GameObject.Find("__VIS_HOLDER");
        if (holder == null) return "no holder (PalaceBakeTool.OpenComplex 먼저)";
        Directory.CreateDirectory(outDir);
        var binPath = Path.Combine(outDir, "meshes.bin");
        if (File.Exists(binPath)) return "SKIP exists " + binPath;
        var toHolder = holder.transform.worldToLocalMatrix;
        var meshIndex = new Dictionary<Mesh, int>(); var meshes = new List<Mesh>();
        var inst = new StringBuilder(); var mats = new Dictionary<string, Material>();
        int nr = 0; long tris = 0;
        foreach (var r in holder.GetComponentsInChildren<MeshRenderer>(true))
        {
            var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
            if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
            var m = mf.sharedMesh;
            if (!meshIndex.TryGetValue(m, out var mi)) { mi = meshes.Count; meshIndex[m] = mi; meshes.Add(m); }
            var M = toHolder * r.transform.localToWorldMatrix;
            inst.Append(mi).Append('\t').Append(r.gameObject.name.Replace('\t', ' '));
            for (int i = 0; i < 16; i++) inst.Append('\t').Append(M[i / 4, i % 4].ToString("R", Inv));
            inst.Append('\t');
            var sm = r.sharedMaterials; var names = new List<string>();
            for (int s = 0; s < m.subMeshCount; s++)
            {
                var mat = s < sm.Length ? sm[s] : null;
                names.Add(mat != null ? mat.name : "");
                if (mat != null) mats[mat.name] = mat;
                tris += m.GetIndexCount(s) / 3;
            }
            inst.Append(string.Join("|", names)).Append('\n'); nr++;
        }
        using (var w = new BinaryWriter(File.Open(binPath, FileMode.Create)))
        {
            w.Write(0x31584250); w.Write(meshes.Count);
            foreach (var m in meshes)
            {
                var nameB = Encoding.UTF8.GetBytes(m.name); w.Write(nameB.Length); w.Write(nameB);
                var v = m.vertices; var n = m.normals; var uv = m.uv;
                w.Write(v.Length); w.Write(m.subMeshCount);
                foreach (var p in v) { w.Write(p.x); w.Write(p.y); w.Write(p.z); }
                if (n.Length == v.Length) foreach (var q in n) { w.Write(q.x); w.Write(q.y); w.Write(q.z); }
                else for (int i = 0; i < v.Length; i++) { w.Write(0f); w.Write(1f); w.Write(0f); }
                if (uv.Length == v.Length) foreach (var q in uv) { w.Write(q.x); w.Write(q.y); }
                else for (int i = 0; i < v.Length; i++) { w.Write(0f); w.Write(0f); }
                for (int s = 0; s < m.subMeshCount; s++) { var t = m.GetTriangles(s); w.Write(t.Length); foreach (var x in t) w.Write(x); }
            }
        }
        File.WriteAllText(Path.Combine(outDir, "instances.tsv"), inst.ToString(), new UTF8Encoding(false));
        // 재질: 이름, 셰이더, BaseColor 경로, Normal 경로, _BaseColor(rgba), BaseColor 타일링(sx,sy,ox,oy), 알파 클립(0/1), 컷오프, 매끄러움, 노멀 세기, 양면(0/1), 마스크 경로
        var mt = new StringBuilder();
        foreach (var kv in mats.OrderBy(k => k.Key))
        {
            var mat = kv.Value;
            string Tex(string p) { if (!mat.HasProperty(p)) return ""; var t = mat.GetTexture(p); return t ? Path.GetFullPath(AssetDatabase.GetAssetPath(t)) : ""; }
            string Col(string p) { if (!mat.HasProperty(p)) return ""; var c = mat.GetColor(p); return c.r.ToString("R", Inv) + "," + c.g.ToString("R", Inv) + "," + c.b.ToString("R", Inv) + "," + c.a.ToString("R", Inv); }
            string Fl(string p) { return mat.HasProperty(p) ? mat.GetFloat(p).ToString("R", Inv) : ""; }
            var st = mat.HasProperty("_BaseColorMap") ? mat.GetTextureScale("_BaseColorMap") : Vector2.one;
            var so = mat.HasProperty("_BaseColorMap") ? mat.GetTextureOffset("_BaseColorMap") : Vector2.zero;
            mt.Append(kv.Key).Append('\t').Append(mat.shader.name).Append('\t').Append(Tex("_BaseColorMap")).Append('\t').Append(Tex("_NormalMap")).Append('\t').Append(Col("_BaseColor"))
              .Append('\t').Append(st.x.ToString("R", Inv)).Append(',').Append(st.y.ToString("R", Inv)).Append(',').Append(so.x.ToString("R", Inv)).Append(',').Append(so.y.ToString("R", Inv))
              .Append('\t').Append(Fl("_AlphaCutoffEnable")).Append('\t').Append(Fl("_AlphaCutoff")).Append('\t').Append(Fl("_Smoothness")).Append('\t').Append(Fl("_NormalScale"))
              .Append('\t').Append(Fl("_DoubleSidedEnable")).Append('\t').Append(Tex("_MaskMap")).Append('\n');
        }
        File.WriteAllText(Path.Combine(outDir, "materials.tsv"), mt.ToString(), new UTF8Encoding(false));
        return "renderers=" + nr + " tris=" + tris + " meshes=" + meshes.Count + " mats=" + mats.Count + " bin=" + (new FileInfo(binPath).Length / 1048576) + "MB";
    }
}
