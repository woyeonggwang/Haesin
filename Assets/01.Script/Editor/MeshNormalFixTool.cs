using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 정점 법선 다시 계산(자동 스무딩) — MCP 에서 정적 메서드로 부름.
/// 외부에서 줄인 경복궁 메시(광화문 문루 등)는 정점을 줄이면서 원래(다른 면에 붙어 있던) 정점 법선을 그대로 가져와,
/// 평평한 면 위 삼각형마다 법선이 비스듬히 섞여 대각선으로 갈라져 보이거나(사용자 지적 "면이 두 개 겹친 것 같다"),
/// 돌 단 모서리가 둥글게 음영 져 그림자가 이상하게 보였음(2026-10-05, 광화문 GHM_Core 모서리의 38 % 가 면 법선과 40° 넘게 어긋남).
/// 방법: 같은 자리(0.5 mm 격자) 정점을 한 무리로 보고, 모서리마다 그 무리에 닿은 면 중 이 면과 angle 도 안쪽인 면의
/// 면 법선을 넓이 가중 평균 → (원래 정점, 새 법선) 이 같은 모서리끼리만 정점을 같이 씀 → 탄젠트 다시 계산.
/// 위치·UV·서브메시·삼각형 순서는 그대로. 결과는 새 메시 에셋(원래 에셋은 그대로 — 되돌리려면 프리팹 참조만).
/// </summary>
public static class MeshNormalFixTool
{
    /// <summary>면 법선과 angleCheck 도 넘게 어긋난 정점 법선 모서리 비율(%).</summary>
    public static float BadPercent(Mesh m, float angleCheck)
    {
        var v = m.vertices; var n = m.normals; var t = m.triangles;
        if (n == null || n.Length != v.Length) return 100f;
        float c = Mathf.Cos(angleCheck * Mathf.Deg2Rad); int bad = 0, total = 0;
        for (int i = 0; i < t.Length; i += 3)
        {
            Vector3 fn = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
            if (fn.sqrMagnitude < 1e-14f) continue; fn.Normalize();
            for (int k = 0; k < 3; k++) { total++; if (Vector3.Dot(n[t[i + k]], fn) < c) bad++; }
        }
        return total > 0 ? 100f * bad / total : 0f;
    }

    /// <summary>src 의 법선을 다시 계산한 새 메시(에셋 저장 안 함).</summary>
    public static Mesh Build(Mesh src, float angle)
    {
        var v = src.vertices; var n0 = src.normals; var uv = src.uv;
        bool hasN0 = n0 != null && n0.Length == v.Length;
        bool hasUV = uv != null && uv.Length == v.Length;
        int subs = src.subMeshCount;
        var subTris = new int[subs][]; int triTotal = 0;
        for (int s = 0; s < subs; s++) { subTris[s] = src.GetTriangles(s); triTotal += subTris[s].Length / 3; }

        // 면 법선·넓이(전 서브메시를 이어 붙인 면 번호)
        var all = new int[triTotal * 3]; int o = 0;
        for (int s = 0; s < subs; s++) { System.Array.Copy(subTris[s], 0, all, o, subTris[s].Length); o += subTris[s].Length; }
        var fn = new Vector3[triTotal]; var fa = new float[triTotal];
        for (int f = 0; f < triTotal; f++)
        {
            Vector3 c = Vector3.Cross(v[all[f * 3 + 1]] - v[all[f * 3]], v[all[f * 3 + 2]] - v[all[f * 3]]);
            float a = c.magnitude; fa[f] = a * 0.5f; fn[f] = a > 1e-12f ? c / a : Vector3.zero;
        }

        // 같은 자리 정점 무리
        var grp = new int[v.Length]; var map = new Dictionary<Vector3Int, int>();
        for (int i = 0; i < v.Length; i++)
        {
            var key = new Vector3Int(Mathf.RoundToInt(v[i].x * 2000f), Mathf.RoundToInt(v[i].y * 2000f), Mathf.RoundToInt(v[i].z * 2000f));
            int g; if (!map.TryGetValue(key, out g)) { g = map.Count; map[key] = g; }
            grp[i] = g;
        }
        int G = map.Count;
        // 무리마다 닿은 면 목록(CSR)
        var cnt = new int[G + 1];
        for (int c = 0; c < all.Length; c++) cnt[grp[all[c]] + 1]++;
        for (int g = 0; g < G; g++) cnt[g + 1] += cnt[g];
        var fill = (int[])cnt.Clone(); var gf = new int[all.Length];
        for (int c = 0; c < all.Length; c++) gf[fill[grp[all[c]]]++] = c / 3;

        float cosA = Mathf.Cos(angle * Mathf.Deg2Rad);
        var newIndex = new Dictionary<long, int>();
        var nv = new List<Vector3>(v.Length); var nn = new List<Vector3>(v.Length); var nuv = new List<Vector2>(v.Length);
        var newAll = new int[all.Length];
        for (int f = 0; f < triTotal; f++)
        {
            for (int k = 0; k < 3; k++)
            {
                int vi = all[f * 3 + k];
                Vector3 nrm;
                if (fn[f] == Vector3.zero) nrm = hasN0 ? n0[vi] : Vector3.up;
                else
                {
                    Vector3 sum = Vector3.zero; int g = grp[vi];
                    for (int j = cnt[g]; j < cnt[g + 1]; j++)
                    {
                        int h = gf[j];
                        if (j > cnt[g] && gf[j - 1] == h) continue; // 같은 면 두 번(퇴화) 건너뜀
                        if (Vector3.Dot(fn[h], fn[f]) >= cosA) sum += fn[h] * fa[h];
                    }
                    nrm = sum.sqrMagnitude > 1e-20f ? sum.normalized : fn[f];
                }
                int qx = Mathf.Clamp(Mathf.RoundToInt((nrm.x + 1f) * 511.5f), 0, 1023);
                int qy = Mathf.Clamp(Mathf.RoundToInt((nrm.y + 1f) * 511.5f), 0, 1023);
                int qz = Mathf.Clamp(Mathf.RoundToInt((nrm.z + 1f) * 511.5f), 0, 1023);
                long key = ((long)vi << 30) | ((long)qx << 20) | ((long)qy << 10) | (long)qz;
                int ni;
                if (!newIndex.TryGetValue(key, out ni))
                {
                    ni = nv.Count; newIndex[key] = ni;
                    nv.Add(v[vi]); nn.Add(nrm); nuv.Add(hasUV ? uv[vi] : Vector2.zero);
                }
                newAll[f * 3 + k] = ni;
            }
        }

        var m = new Mesh();
        m.name = src.name;
        m.indexFormat = nv.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        m.SetVertices(nv); m.SetNormals(nn); if (hasUV) m.SetUVs(0, nuv);
        m.subMeshCount = subs; o = 0;
        for (int s = 0; s < subs; s++)
        {
            var tri = new int[subTris[s].Length];
            System.Array.Copy(newAll, o, tri, 0, tri.Length); o += tri.Length;
            m.SetTriangles(tri, s, false);
        }
        m.RecalculateBounds();
        if (hasUV) m.RecalculateTangents();
        return m;
    }

    /// <summary>
    /// 프리팹 안에서 이름이 meshNamesCsv 에 든 메시(MeshFilter·MeshCollider 가 쓰는 것)를 법선 고친 새 메시로 바꾼다.
    /// 새 에셋 = 원래 에셋 폴더/NormalFix/&lt;메시 이름&gt;_N.asset(있으면 덮어씀 — 프리팹 연결 유지). 반환 = 보고.
    /// </summary>
    public static string ApplyToPrefab(string prefabPath, string meshNamesCsv, float angle)
    {
        var names = new HashSet<string>(meshNamesCsv.Split(','));
        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        var report = new System.Text.StringBuilder();
        try
        {
            var done = new Dictionary<Mesh, Mesh>();
            System.Func<Mesh, Mesh> fix = src =>
            {
                Mesh dst;
                if (done.TryGetValue(src, out dst)) return dst;
                string sp = AssetDatabase.GetAssetPath(src);
                string dir = Path.GetDirectoryName(sp).Replace('\\', '/') + "/NormalFix";
                if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(Path.GetDirectoryName(sp).Replace('\\', '/'), "NormalFix");
                string dp = dir + "/" + src.name + "_N.asset";
                var built = Build(src, angle);
                built.name = src.name + "_N";
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(dp);
                if (existing != null) { EditorUtility.CopySerialized(built, existing); Object.DestroyImmediate(built); dst = existing; EditorUtility.SetDirty(dst); }
                else { AssetDatabase.CreateAsset(built, dp); dst = built; }
                report.AppendLine(src.name + ": verts " + src.vertexCount + " -> " + dst.vertexCount + ", bad40 " + BadPercent(src, 40f).ToString("F1") + "% -> " + BadPercent(dst, 40f).ToString("F1") + "% → " + dp);
                done[src] = dst;
                return dst;
            };
            int swapped = 0;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null && names.Contains(mf.sharedMesh.name)) { mf.sharedMesh = fix(mf.sharedMesh); swapped++; }
            foreach (var mc in root.GetComponentsInChildren<MeshCollider>(true))
                if (mc.sharedMesh != null && names.Contains(mc.sharedMesh.name)) { mc.sharedMesh = fix(mc.sharedMesh); swapped++; }
            AssetDatabase.SaveAssets();
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            report.AppendLine("swapped refs " + swapped);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        return report.ToString();
    }
}
