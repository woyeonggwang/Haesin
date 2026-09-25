using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Haesin_Base.fbx 의 Body 블렌드셰이프(Blink_L/R)에 법선 변화량을 넣는다.
/// 임포트 설정 Blend Shape Normals = None 이면 법선 변화가 0 이라 감은 눈꺼풀이 원래(벽 모양) 법선으로 칠해지고,
/// Calculate 로 하면 얼굴 전체 법선이 다시 계산돼 음영이 깨진다(CLAUDE.md §39).
/// → 움직인 정점과 그 이웃만 (감은 형상 법선 − 기본 형상 법선) 을 넣고 나머지는 0.
/// 법선은 같은 위치 정점을 묶어(UV 이음새 무시) 면적 가중 평균으로 계산.
/// </summary>
public class BlinkBlendShapeNormals : AssetPostprocessor
{
    const string TargetFile = "Haesin_Base.fbx";

    void OnPostprocessModel(GameObject root)
    {
        if (!assetPath.EndsWith(TargetFile)) return;
        foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var mesh = smr.sharedMesh;
            if (mesh == null || mesh.blendShapeCount == 0) continue;
            Process(mesh);
        }
    }

    static void Process(Mesh mesh)
    {
        var v = mesh.vertices;
        var tris = mesh.triangles;
        int n = v.Length;

        // 위치로 묶기
        var weldMap = new Dictionary<Vector3Int, int>();
        var weld = new int[n];
        int groups = 0;
        for (int i = 0; i < n; i++)
        {
            var key = new Vector3Int(Mathf.RoundToInt(v[i].x * 1e5f), Mathf.RoundToInt(v[i].y * 1e5f), Mathf.RoundToInt(v[i].z * 1e5f));
            int g;
            if (!weldMap.TryGetValue(key, out g)) { g = groups++; weldMap[key] = g; }
            weld[i] = g;
        }
        // 이웃(그룹 단위)
        var nb = new List<int>[groups];
        for (int g = 0; g < groups; g++) nb[g] = new List<int>(6);
        for (int t = 0; t < tris.Length; t += 3)
            for (int k = 0; k < 3; k++)
            {
                int a = weld[tris[t + k]], b = weld[tris[t + (k + 1) % 3]];
                if (a != b) { nb[a].Add(b); nb[b].Add(a); }
            }

        var baseN = GroupNormals(v, tris, weld, groups);

        int count = mesh.blendShapeCount;
        var names = new string[count];
        var frames = new List<(float w, Vector3[] dv, Vector3[] dn, Vector3[] dt)>[count];
        for (int s = 0; s < count; s++)
        {
            names[s] = mesh.GetBlendShapeName(s);
            frames[s] = new List<(float, Vector3[], Vector3[], Vector3[])>();
            int fc = mesh.GetBlendShapeFrameCount(s);
            for (int f = 0; f < fc; f++)
            {
                var dv = new Vector3[n]; var dn = new Vector3[n]; var dt = new Vector3[n];
                mesh.GetBlendShapeFrameVertices(s, f, dv, dn, dt);
                float w = mesh.GetBlendShapeFrameWeight(s, f);

                var moved = new bool[groups];
                for (int i = 0; i < n; i++) if (dv[i].sqrMagnitude > 1e-12f) moved[weld[i]] = true;
                var touched = new bool[groups];
                for (int g = 0; g < groups; g++)
                    if (moved[g]) { touched[g] = true; foreach (var h in nb[g]) touched[h] = true; }

                var pv = new Vector3[n];
                for (int i = 0; i < n; i++) pv[i] = v[i] + dv[i];
                var defN = GroupNormals(pv, tris, weld, groups);

                var outN = new Vector3[n];
                for (int i = 0; i < n; i++)
                {
                    int g = weld[i];
                    outN[i] = touched[g] ? defN[g] - baseN[g] : Vector3.zero;
                }
                frames[s].Add((w, dv, outN, dt));
            }
        }
        mesh.ClearBlendShapes();
        for (int s = 0; s < count; s++)
            foreach (var fr in frames[s])
                mesh.AddBlendShapeFrame(names[s], fr.w, fr.dv, fr.dn, fr.dt);
    }

    static Vector3[] GroupNormals(Vector3[] p, int[] tris, int[] weld, int groups)
    {
        var acc = new Vector3[groups];
        for (int t = 0; t < tris.Length; t += 3)
        {
            int a = tris[t], b = tris[t + 1], c = tris[t + 2];
            var fn = Vector3.Cross(p[b] - p[a], p[c] - p[a]); // 길이 = 2×면적
            acc[weld[a]] += fn; acc[weld[b]] += fn; acc[weld[c]] += fn;
        }
        // Vector3.normalized 는 길이 < 1e-5 이면 0 을 돌려준다. 얼굴 삼각형(면적 ~1e-6 m²)은 여기에 걸려
        // 법선이 0 이 되어 법선 변화량이 빠졌었음(CLAUDE.md §40) → 직접 나눔.
        for (int g = 0; g < groups; g++)
        {
            float len = acc[g].magnitude;
            acc[g] = len > 1e-20f ? acc[g] / len : Vector3.zero;
        }
        return acc;
    }
}
