// 만들어 낸 메시(<단지>_Shell.asset, RoofLOD 메시)를 작게 만든다 — MCP 에서 정적 메서드로 호출.
// 처음 만든 속 비운 메시는 삼각형마다 정점 3개를 따로 넣어(용접 안 함) 사정전 하나가 텍스트 에셋 1.86 GB 였다.
// 같은 정점을 합치고(위치·법선·UV·접선이 같으면 하나), 법선·접선을 16비트로 줄이고, 2번 UV(라이트맵, 안 씀)를 뺀다.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class PalaceMeshCompact
{
    [StructLayout(LayoutKind.Sequential)]
    struct V
    {
        public Vector3 p;
        public ushort nx, ny, nz, nw;
        public ushort tx, ty, tz, tw;
        public Vector2 uv;
    }

    struct Key : IEquatable<Key>
    {
        public int px, py, pz, u, v; public short nx, ny, nz, tw;
        public bool Equals(Key o) { return px == o.px && py == o.py && pz == o.pz && u == o.u && v == o.v && nx == o.nx && ny == o.ny && nz == o.nz && tw == o.tw; }
        public override int GetHashCode() { unchecked { int h = px; h = h * 397 ^ py; h = h * 397 ^ pz; h = h * 397 ^ u; h = h * 397 ^ v; h = h * 397 ^ nx; h = h * 397 ^ ny; h = h * 397 ^ nz; return h * 397 ^ tw; } }
    }

    static readonly VertexAttributeDescriptor[] Layout =
    {
        new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
        new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float16, 4),
        new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float16, 4),
        new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
    };

    // 메시 하나를 제자리에서 줄인다(같은 오브젝트라 프리팹 참조는 그대로). 이미 줄인 것은 건너뜀.
    public static bool CompactMesh(Mesh m)
    {
        if (m == null) return false;
        if (m.GetVertexAttributeFormat(VertexAttribute.Normal) == VertexAttributeFormat.Float16 && !m.HasVertexAttribute(VertexAttribute.TexCoord1)) return false;
        var pos = m.vertices; var nor = m.normals; var tan = m.tangents; var uv = m.uv;
        int n = pos.Length;
        var map = new Dictionary<Key, int>(n);
        var remap = new int[n]; var outV = new List<V>(n / 2 + 16);
        for (int i = 0; i < n; i++)
        {
            var nn = nor.Length > 0 ? nor[i] : Vector3.up; var tt = tan.Length > 0 ? tan[i] : new Vector4(1, 0, 0, 1); var uu = uv.Length > 0 ? uv[i] : Vector2.zero;
            var k = new Key
            {
                px = Mathf.RoundToInt(pos[i].x * 2000f), py = Mathf.RoundToInt(pos[i].y * 2000f), pz = Mathf.RoundToInt(pos[i].z * 2000f),
                u = Mathf.RoundToInt(uu.x * 8192f), v = Mathf.RoundToInt(uu.y * 8192f),
                nx = (short)Mathf.RoundToInt(nn.x * 100f), ny = (short)Mathf.RoundToInt(nn.y * 100f), nz = (short)Mathf.RoundToInt(nn.z * 100f), tw = (short)(tt.w < 0 ? -1 : 1)
            };
            int j;
            if (!map.TryGetValue(k, out j))
            {
                j = outV.Count; map[k] = j;
                outV.Add(new V
                {
                    p = pos[i],
                    nx = Mathf.FloatToHalf(nn.x), ny = Mathf.FloatToHalf(nn.y), nz = Mathf.FloatToHalf(nn.z), nw = 0,
                    tx = Mathf.FloatToHalf(tt.x), ty = Mathf.FloatToHalf(tt.y), tz = Mathf.FloatToHalf(tt.z), tw = Mathf.FloatToHalf(tt.w),
                    uv = uu
                });
            }
            remap[i] = j;
        }
        int subCount = m.subMeshCount;
        var subs = new List<int[]>(); for (int s = 0; s < subCount; s++) subs.Add(m.GetIndices(s).Select(x => remap[x]).ToArray());
        var b = m.bounds; var name = m.name;
        m.Clear();
        m.indexFormat = outV.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        m.SetVertexBufferParams(outV.Count, Layout);
        m.SetVertexBufferData(outV, 0, 0, outV.Count, 0, MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds);
        int total = subs.Sum(x => x.Length);
        m.SetIndexBufferParams(total, m.indexFormat);
        var all = new int[total]; int off = 0; var descs = new SubMeshDescriptor[subCount];
        for (int s = 0; s < subCount; s++) { Array.Copy(subs[s], 0, all, off, subs[s].Length); descs[s] = new SubMeshDescriptor(off, subs[s].Length, MeshTopology.Triangles); off += subs[s].Length; }
        if (m.indexFormat == IndexFormat.UInt16) m.SetIndexBufferData(all.Select(x => (ushort)x).ToArray(), 0, 0, total, MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds);
        else m.SetIndexBufferData(all, 0, 0, total, MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds);
        m.subMeshCount = subCount;
        for (int s = 0; s < subCount; s++) m.SetSubMesh(s, descs[s], MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds);
        m.name = name; m.RecalculateBounds();
        return true;
    }

    static List<Mesh> pending; static string pendingPath; static long before, after;

    // 에셋 파일 안의 메시를 시간 제한을 두고 조금씩 줄인다. "DONE" 이 나올 때까지 부른다.
    public static string CompactAsset(string path, float budget = 20f)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        if (pending == null || pendingPath != path)
        {
            pending = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().ToList(); pendingPath = path; before = 0; after = 0;
        }
        int done = 0;
        while (pending.Count > 0 && sw.Elapsed.TotalSeconds < budget)
        {
            var m = pending[pending.Count - 1]; pending.RemoveAt(pending.Count - 1);
            long vb = m.vertexCount; before += vb;
            if (CompactMesh(m)) EditorUtility.SetDirty(m);
            after += m.vertexCount; done++;
        }
        if (pending.Count > 0) return "PARTIAL left=" + pending.Count + " (+" + done + ")";
        AssetDatabase.SaveAssets();
        pending = null;
        return "DONE verts " + before + " -> " + after;
    }
}
