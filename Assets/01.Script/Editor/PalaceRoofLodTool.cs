// 경복궁 지붕을 "지붕마다" LOD 로 바꾸는 도구 — MCP 에서 정적 메서드로 호출한다.
// 예전엔 단지 하나 전체가 LODGroup 하나(거리 = 단지 중심에서)라, 근정전 월대에 서 있으면 30~70 m 떨어진 행각 기와까지
// 전부 원본 기와 메시로 그려져 지글거렸다(사용자 지적 2026-09-27). 이제 지붕을 16 m 칸으로 잘라 칸마다 LODGroup 을 둔다:
//   LOD0 = 원본 기와(구운 것만 쓰는 단지는 구운 지붕 메시) — 칸에서 약 nearDist(25 m) 안
//   LOD1 = 매끈한 껍데기 + 노멀맵(<덩어리>_roofenv)                 — flatDist(150 m) 안
//   LOD2 = 윗면만 남겨 크게 줄인 평면 지붕(<덩어리>_roofflat, 노멀맵 없음)
// 칸들은 <단지>_RoofLOD.prefab 에 모으고, 단지 LOD 에서는 기와를 뺀다(원본 기와 오브젝트는 끄고, 구운 덩어리의 _roof 도 끈다).
// 껍데기 품질이 나쁜 덩어리(cov_env < 0.85 또는 cov_high < 0.8)는 건드리지 않는다(예전처럼 단지 LOD 에 남음).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class PalaceRoofLodTool
{
    const string Root = PalaceBakeTool.BakedAssetRoot;
    static readonly float K = 2f * Mathf.Tan(30f * Mathf.Deg2Rad);   // 시야각 60° 기준 화면 높이 ↔ 거리

    // 굽기(bake_roofenv)의 category() 와 같은 규칙
    public static bool IsRoofTile(string meshName)
    {
        var n = meshName.ToLowerInvariant();
        if (n.Contains("rafter") || n.Contains("rooffloor") || n.Contains("roofinner") || n.Contains("roofwall") || n.Contains("bridge")) return false;
        return n.Contains("roof") || n.Contains("kiwa") || n.Contains("giwa") || n.Contains("roofplate") || n.Contains("yongmeoli")
            || n.Contains("chimi") || n.Contains("japsang") || n.Contains("ridge") || n.Contains("mangwa") || n.Contains("sumaksae");
    }

    static void ResetKeywords(Material m)
    {
        var t = Type.GetType("UnityEditor.Rendering.HighDefinition.HDShaderUtils, Unity.RenderPipelines.HighDefinition.Editor");
        var mi = t == null ? null : t.GetMethod("ResetMaterialKeywords", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, new[] { typeof(Material) }, null);
        if (mi != null) mi.Invoke(null, new object[] { m });
    }

    static Material MakeMat(string path, Texture2D bc, Texture2D n, float smooth)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path); bool isNew = m == null;
        if (isNew) m = new Material(Shader.Find("HDRP/Lit"));
        m.SetTexture("_BaseColorMap", bc); m.SetTexture("_NormalMap", n);
        m.SetFloat("_Smoothness", smooth);
        m.SetFloat("_DoubleSidedEnable", 1); m.SetFloat("_CullMode", 0); m.SetFloat("_CullModeForward", 0);
        if (isNew) AssetDatabase.CreateAsset(m, path);
        ResetKeywords(m); EditorUtility.SetDirty(m);
        return m;
    }

    static float Num(string txt, string key, float def)
    {
        var mm = Regex.Match(txt, "\"" + key + "\": ([0-9.]+)");
        return mm.Success ? float.Parse(mm.Groups[1].Value, CultureInfo.InvariantCulture) : def;
    }

    class Tri { public Vector3 a, b, c, na, nb, nc; public Vector2 ua, ub, uc; public Vector4 ta, tb, tc; public int mat; }

    static void Collect(Mesh mesh, Matrix4x4 M, int mat, List<Tri> into, int subMesh = -1, bool noDown = false)
    {
        var v = mesh.vertices; var nrm = mesh.normals; var uv = mesh.uv; var tg = mesh.tangents;
        var N = M.inverse.transpose;
        for (int s = 0; s < mesh.subMeshCount; s++)
        {
            if (subMesh >= 0 && s != subMesh) continue;
            var idx = mesh.GetTriangles(s);
            for (int i = 0; i < idx.Length; i += 3)
            {
                var t = new Tri { mat = mat };
                int i0 = idx[i], i1 = idx[i + 1], i2 = idx[i + 2];
                t.a = M.MultiplyPoint3x4(v[i0]); t.b = M.MultiplyPoint3x4(v[i1]); t.c = M.MultiplyPoint3x4(v[i2]);
                if (nrm.Length > 0) { t.na = N.MultiplyVector(nrm[i0]).normalized; t.nb = N.MultiplyVector(nrm[i1]).normalized; t.nc = N.MultiplyVector(nrm[i2]).normalized; }
                if (uv.Length > 0) { t.ua = uv[i0]; t.ub = uv[i1]; t.uc = uv[i2]; }
                if (tg.Length > 0)
                {
                    Vector3 x0 = M.MultiplyVector(tg[i0]).normalized, x1 = M.MultiplyVector(tg[i1]).normalized, x2 = M.MultiplyVector(tg[i2]).normalized;
                    t.ta = new Vector4(x0.x, x0.y, x0.z, tg[i0].w); t.tb = new Vector4(x1.x, x1.y, x1.z, tg[i1].w); t.tc = new Vector4(x2.x, x2.y, x2.z, tg[i2].w);
                }
                // 껍데기 아랫면(아래를 보는 면)은 뺀다 — 처마 밑은 서까래가 가리고, 건물 안(속 비운 방)에서 올려다보면 회색 덩어리로 보였음
                if (noDown && (t.na + t.nb + t.nc).normalized.y < -0.2f) continue;
                into.Add(t);
            }
        }
    }

    static Mesh Build(string name, List<Tri> tris, int matCount, bool hasTangent)
    {
        var verts = new List<Vector3>(); var nr = new List<Vector3>(); var uv = new List<Vector2>(); var tg = new List<Vector4>();
        var subs = new List<int>[matCount]; for (int i = 0; i < matCount; i++) subs[i] = new List<int>();
        foreach (var t in tris)
        {
            int b = verts.Count;
            verts.Add(t.a); verts.Add(t.b); verts.Add(t.c);
            nr.Add(t.na); nr.Add(t.nb); nr.Add(t.nc);
            uv.Add(t.ua); uv.Add(t.ub); uv.Add(t.uc);
            tg.Add(t.ta); tg.Add(t.tb); tg.Add(t.tc);
            subs[t.mat].Add(b); subs[t.mat].Add(b + 1); subs[t.mat].Add(b + 2);
        }
        var m = new Mesh { name = name, indexFormat = verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
        m.SetVertices(verts); m.SetNormals(nr); m.SetUVs(0, uv);
        if (hasTangent) m.SetTangents(tg);
        m.subMeshCount = matCount;
        for (int i = 0; i < matCount; i++) m.SetTriangles(subs[i], i, false);
        m.RecalculateBounds();
        if (!hasTangent) m.RecalculateTangents();
        // 정점이 삼각형마다 따로라 이어 붙인다(메모리 절약)
        MeshUtilityWeld(m);
        return m;
    }

    static void MeshUtilityWeld(Mesh m)
    {
        var v = m.vertices; var n = m.normals; var u = m.uv; var t = m.tangents;
        var map = new Dictionary<string, int>(); var remap = new int[v.Length];
        var nv = new List<Vector3>(); var nn = new List<Vector3>(); var nu = new List<Vector2>(); var nt = new List<Vector4>();
        for (int i = 0; i < v.Length; i++)
        {
            string key = v[i].x.ToString("F4") + "," + v[i].y.ToString("F4") + "," + v[i].z.ToString("F4") + "|" + u[i].x.ToString("F5") + "," + u[i].y.ToString("F5") + "|" + n[i].x.ToString("F2") + "," + n[i].y.ToString("F2") + "," + n[i].z.ToString("F2");
            int j; if (!map.TryGetValue(key, out j)) { j = nv.Count; map[key] = j; nv.Add(v[i]); nn.Add(n[i]); nu.Add(u[i]); nt.Add(t.Length > 0 ? t[i] : Vector4.zero); }
            remap[i] = j;
        }
        var subs = new List<int[]>(); for (int s = 0; s < m.subMeshCount; s++) subs.Add(m.GetTriangles(s).Select(x => remap[x]).ToArray());
        m.Clear(); m.indexFormat = nv.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        m.SetVertices(nv); m.SetNormals(nn); m.SetUVs(0, nu); m.SetTangents(nt);
        m.subMeshCount = subs.Count; for (int s = 0; s < subs.Count; s++) m.SetTriangles(subs[s], s, false);
        m.RecalculateBounds();
    }

    static Matrix4x4 RelTo(Transform t, Transform root) { return root.worldToLocalMatrix * t.localToWorldMatrix; }

    static string Rel(string abs) { return abs.Replace('\\', '/'); }

    // 굽기 결과 복사(바뀐 것만). 복사했으면 true(가져오기 뒤 다시 불러야 함).
    static bool CopyOut(string complex, out List<string> names)
    {
        var src = Path.Combine(PalaceBakeTool.BakeRoot, complex, "out_roofenv");
        names = new List<string>();
        if (!Directory.Exists(src)) return false;
        var dst = Path.Combine(Directory.GetCurrentDirectory(), (Root + "/" + complex).Replace('/', '\\'));
        bool copied = false;
        foreach (var f in Directory.GetFiles(src))
        {
            var fn = Path.GetFileName(f);
            if (!(fn.EndsWith(".png") || fn.EndsWith(".fbx"))) continue;
            var d = Path.Combine(dst, fn);
            if (File.Exists(d) && new FileInfo(d).Length == new FileInfo(f).Length) continue;
            File.Copy(f, d, true); copied = true;
        }
        names = Directory.GetFiles(src, "*_roofenv_info.json").Select(p => Path.GetFileName(p).Replace("_roofenv_info.json", "")).ToList();
        return copied;
    }

    class Src { public Mesh m; public Matrix4x4 M; public Material[] mats; public bool noDown, clip; }

    struct CV { public Vector3 p, n; public Vector2 u; public Vector4 t; }
    static CV Lerp(CV a, CV b, float f) { return new CV { p = Vector3.Lerp(a.p, b.p, f), n = Vector3.Lerp(a.n, b.n, f).normalized, u = Vector2.Lerp(a.u, b.u, f), t = Vector4.Lerp(a.t, b.t, f) }; }
    // 다각형을 축 평면(x 또는 z = v)의 한쪽으로 자름(keepGreater: v 이상만 남김)
    static List<CV> ClipAxis(List<CV> poly, bool isX, float val, bool keepGreater)
    {
        var o = new List<CV>(); if (poly.Count == 0) return o;
        for (int i = 0; i < poly.Count; i++)
        {
            var a = poly[i]; var b = poly[(i + 1) % poly.Count];
            float da = (isX ? a.p.x : a.p.z) - val, db = (isX ? b.p.x : b.p.z) - val;
            if (!keepGreater) { da = -da; db = -db; }
            if (da >= 0) o.Add(a);
            if ((da >= 0) != (db >= 0)) o.Add(Lerp(a, b, da / (da - db)));
        }
        return o;
    }

    // 칸 하나의 메시 모으기 — 같은 원본 정점은 한 번만 넣는다(문자열 용접보다 빠름)
    class NB
    {
        public Dictionary<long, int> map = new Dictionary<long, int>();
        public List<Vector3> v = new List<Vector3>(), n = new List<Vector3>(); public List<Vector2> u = new List<Vector2>(); public List<Vector4> t = new List<Vector4>();
        public List<Material> mats = new List<Material>(); public List<List<int>> subs = new List<List<int>>();
        public int TriCount { get { return subs.Sum(x => x.Count) / 3; } }
        public List<int> Sub(Material m) { int i = mats.IndexOf(m); if (i < 0) { mats.Add(m); subs.Add(new List<int>()); i = mats.Count - 1; } return subs[i]; }
        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name, indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, u); mesh.SetTangents(t);
            mesh.subMeshCount = subs.Count; for (int i = 0; i < subs.Count; i++) mesh.SetTriangles(subs[i], i, false);
            mesh.RecalculateBounds(); return mesh;
        }
    }

    static Dictionary<Vector2Int, NB> SplitCells(List<Src> srcs, float cell)
    {
        var by = new Dictionary<Vector2Int, NB>();
        for (int si = 0; si < srcs.Count; si++)
        {
            var sr = srcs[si]; var m = sr.m; var M = sr.M; var N = M.inverse.transpose; bool mir = M.determinant < 0;
            var v = m.vertices; var nr = m.normals; var uv = m.uv; var tg = m.tangents;
            for (int s = 0; s < m.subMeshCount; s++)
            {
                var mat = s < sr.mats.Length ? sr.mats[s] : sr.mats[sr.mats.Length - 1];
                var idx = m.GetTriangles(s);
                for (int i = 0; i < idx.Length; i += 3)
                {
                    Vector3 a = M.MultiplyPoint3x4(v[idx[i]]), b = M.MultiplyPoint3x4(v[idx[i + 1]]), c = M.MultiplyPoint3x4(v[idx[i + 2]]);
                    if (sr.noDown && nr.Length > 0 && N.MultiplyVector(nr[idx[i]] + nr[idx[i + 1]] + nr[idx[i + 2]]).normalized.y < -0.2f) continue;
                    bool spans = Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x)) / cell) != Mathf.FloorToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x)) / cell)
                              || Mathf.FloorToInt(Mathf.Min(a.z, Mathf.Min(b.z, c.z)) / cell) != Mathf.FloorToInt(Mathf.Max(a.z, Mathf.Max(b.z, c.z)) / cell);
                    if (spans && (sr.clip || (a - b).sqrMagnitude > 4f || (b - c).sqrMagnitude > 4f || (c - a).sqrMagnitude > 4f))
                    {
                        // 큰 삼각형(평면 지붕은 지붕 하나가 150개 남짓)을 칸 경계에서 잘라 칸마다 제 몫만 넣는다
                        // (무게중심으로만 나누면 어떤 칸엔 평면이 없어 멀리서 구멍, 어떤 칸은 옆 칸까지 덮음)
                        var tri = new List<CV>();
                        for (int j = 0; j < 3; j++)
                        {
                            int o = idx[i + (mir ? 2 - j : j)];
                            Vector4 tt = new Vector4(1, 0, 0, 1);
                            if (tg.Length > 0) { var x = M.MultiplyVector(tg[o]).normalized; tt = new Vector4(x.x, x.y, x.z, tg[o].w * (mir ? -1 : 1)); }
                            tri.Add(new CV { p = M.MultiplyPoint3x4(v[o]), n = nr.Length > 0 ? N.MultiplyVector(nr[o]).normalized : Vector3.up, u = uv.Length > 0 ? uv[o] : Vector2.zero, t = tt });
                        }
                        int x0 = Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x)) / cell), x1 = Mathf.FloorToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x)) / cell);
                        int z0 = Mathf.FloorToInt(Mathf.Min(a.z, Mathf.Min(b.z, c.z)) / cell), z1 = Mathf.FloorToInt(Mathf.Max(a.z, Mathf.Max(b.z, c.z)) / cell);
                        for (int cx = x0; cx <= x1; cx++) for (int cz = z0; cz <= z1; cz++)
                            {
                                var poly = ClipAxis(tri, true, cx * cell, true); poly = ClipAxis(poly, true, (cx + 1) * cell, false);
                                poly = ClipAxis(poly, false, cz * cell, true); poly = ClipAxis(poly, false, (cz + 1) * cell, false);
                                if (poly.Count < 3) continue;
                                var k2 = new Vector2Int(cx, cz); NB nb2; if (!by.TryGetValue(k2, out nb2)) by[k2] = nb2 = new NB();
                                var sub2 = nb2.Sub(mat); int bi = nb2.v.Count;
                                foreach (var q in poly) { nb2.v.Add(q.p); nb2.n.Add(q.n); nb2.u.Add(q.u); nb2.t.Add(q.t); }
                                for (int f = 1; f + 1 < poly.Count; f++) { sub2.Add(bi); sub2.Add(bi + f); sub2.Add(bi + f + 1); }
                            }
                        continue;
                    }
                    var ce = (a + b + c) / 3f; var key = new Vector2Int(Mathf.FloorToInt(ce.x / cell), Mathf.FloorToInt(ce.z / cell));
                    NB nb; if (!by.TryGetValue(key, out nb)) by[key] = nb = new NB();
                    var sub = nb.Sub(mat);
                    for (int j = 0; j < 3; j++)
                    {
                        int o = idx[i + (mir ? 2 - j : j)];
                        long k = ((long)si << 32) | (uint)o; int vi;
                        if (!nb.map.TryGetValue(k, out vi))
                        {
                            vi = nb.v.Count; nb.map[k] = vi;
                            nb.v.Add(M.MultiplyPoint3x4(v[o]));
                            nb.n.Add(nr.Length > 0 ? N.MultiplyVector(nr[o]).normalized : Vector3.up);
                            nb.u.Add(uv.Length > 0 ? uv[o] : Vector2.zero);
                            if (tg.Length > 0) { var x = M.MultiplyVector(tg[o]).normalized; nb.t.Add(new Vector4(x.x, x.y, x.z, tg[o].w * (mir ? -1 : 1))); }
                            else nb.t.Add(new Vector4(1, 0, 0, 1));
                        }
                        sub.Add(vi);
                    }
                }
            }
        }
        return by;
    }

    public static string Build(string complex, float nearDist = 25f, float flatDist = 150f, float cell = 16f)
    {
        List<string> names;
        if (CopyOut(complex, out names)) { AssetDatabase.Refresh(); return "COPIED " + complex + " (다시 부르기)"; }
        bool full = PalaceOptimizeTool.FullOriginalComplexes.Contains(complex);
        var dir = Root + "/" + complex; var src = Path.Combine(PalaceBakeTool.BakeRoot, complex, "out_roofenv");
        var cellDir = dir + "/RoofLOD";
        if (!AssetDatabase.IsValidFolder(cellDir)) AssetDatabase.CreateFolder(dir, "RoofLOD");
        var log = new StringBuilder();

        // ---- 덩어리별 껍데기·평면 모으기 ----
        var envSrc = new List<Src>(); var flatSrc = new List<Src>(); var nearSrc = new List<Src>();
        var goodBounds = new List<Bounds>(); var goodNames = new List<string>();
        foreach (var name in names)
        {
            var info = File.ReadAllText(Path.Combine(src, name + "_roofenv_info.json"));
            if (!info.Contains("\"roof\": 1")) continue;
            float ce = Num(info, "cov_env", 0), ch = Num(info, "cov_high", 0);
            if (ce < 0.85f || ch < 0.8f) { log.Append("bad " + name + " " + ce + "/" + ch + "; "); continue; }
            var baked = AssetDatabase.LoadAssetAtPath<GameObject>(dir + "/" + name + "_Baked.prefab");
            var env = AssetDatabase.LoadAllAssetsAtPath(dir + "/" + name + "_roofenv.fbx").OfType<Mesh>().FirstOrDefault();
            var flat = AssetDatabase.LoadAllAssetsAtPath(dir + "/" + name + "_roofflat.fbx").OfType<Mesh>().FirstOrDefault();
            if (baked == null || env == null || flat == null) { log.Append("missing " + name + "; "); continue; }
            var roofR = baked.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.name == name + "_roof");
            if (roofR == null) { log.Append("noroof " + name + "; "); continue; }
            var M = RelTo(roofR.transform, baked.transform);
            var bc = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "/" + name + "_roofenv_BC.png");
            var nm = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "/" + name + "_roofenv_N.png");
            var envMat = MakeMat(dir + "/MAT_" + name + "_roofenv.mat", bc, nm, 0.25f);
            // 멀리서 반짝임(지글거림)을 줄이려고 노멀맵 없이, 매끄러움도 낮게
            var flatMat = MakeMat(dir + "/MAT_" + name + "_roofflat.mat", bc, null, 0.1f);
            envSrc.Add(new Src { m = env, M = M, mats = new[] { envMat }, noDown = true });
            flatSrc.Add(new Src { m = flat, M = M, mats = new[] { flatMat }, clip = true });
            var eb = GeometryUtility.CalculateBounds(env.vertices, M); eb.Expand(2f);
            goodBounds.Add(eb); goodNames.Add(name);
            if (!full)
            {
                // 구운 것만 쓰는 단지: 가까이는 구운 지붕 메시(FBX 의 <덩어리>_roof)
                var bakedRoof = AssetDatabase.LoadAllAssetsAtPath(dir + "/" + name + ".fbx").OfType<Mesh>().FirstOrDefault(x => x.name == name + "_roof");
                var bakedMat = AssetDatabase.LoadAssetAtPath<Material>(dir + "/MAT_" + name + "_roof.mat");
                if (bakedRoof != null && bakedMat != null) nearSrc.Add(new Src { m = bakedRoof, M = M, mats = new[] { bakedMat } });
            }
        }
        AssetDatabase.SaveAssets();
        if (goodNames.Count == 0) return "no good roofs " + complex + " " + log;

        // ---- 원본 기와(원본 단지): 덮인 것은 원본에서 끄고 칸 메시로 ----
        GameObject origRoot = null; var nearObjs = new List<Transform>(); var bigToDestroy = new List<GameObject>();
        if (full)
        {
            origRoot = PrefabUtility.LoadPrefabContents(dir + "/" + complex + "_Original.prefab");
            foreach (var r in origRoot.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null || !IsRoofTile(mf.sharedMesh.name)) continue;
                if (!r.enabled) continue;
                var c = origRoot.transform.InverseTransformPoint(r.bounds.center);
                bool covered = goodBounds.Any(b => b.Contains(c));
                if (!covered) { r.gameObject.SetActive(true); continue; }
                var wb = r.bounds;
                if (Mathf.Max(wb.size.x, wb.size.z) <= 20f)
                {
                    // 작은 기와 조각은 메시를 복사하지 않고 같은 메시를 쓰는 오브젝트로(공유 메시 유지 — 굽어 복사하면 사정전 하나에 정점 765만 개였음)
                    nearObjs.Add(r.transform); r.gameObject.SetActive(false);
                }
                else
                {
                    // 긴 지붕 판(20 m 넘음)은 칸마다 잘라 새 메시로 — 칸 거리 기준이 맞게. 원본에서는 지워 같은 메시가 두 번 올라가지 않게.
                    nearSrc.Add(new Src { m = mf.sharedMesh, M = RelTo(r.transform, origRoot.transform), mats = r.sharedMaterials });
                    if (r.transform.childCount == 0) bigToDestroy.Add(r.gameObject); else r.gameObject.SetActive(false);
                }
            }
        }

        // ---- 칸 나누기(삼각형 무게중심 기준, 큰 기와 메시도 칸마다 잘라 칸 크기가 cell 에 가깝게) ----
        var nearBy = SplitCells(nearSrc, cell); var envBy = SplitCells(envSrc, cell); var flatBy = SplitCells(flatSrc, cell);
        var objBy = new Dictionary<Vector2Int, List<Transform>>();
        foreach (var t in nearObjs)
        {
            var c = origRoot.transform.InverseTransformPoint(t.GetComponent<Renderer>().bounds.center);
            var k = new Vector2Int(Mathf.FloorToInt(c.x / cell), Mathf.FloorToInt(c.z / cell)); List<Transform> l;
            if (!objBy.TryGetValue(k, out l)) objBy[k] = l = new List<Transform>(); l.Add(t);
        }
        var keys = new HashSet<Vector2Int>(nearBy.Keys.Concat(envBy.Keys).Concat(flatBy.Keys).Concat(objBy.Keys));

        // ---- 칸 프리팹 ----
        // 칸 메시는 한 파일(<단지>_RoofLOD_Meshes.asset)에 모은다 — 파일마다 만들면 만들 때마다 새로고침(0.2 s)이 돌아 오래 걸렸음
        foreach (var p in AssetDatabase.FindAssets("t:Mesh", new[] { cellDir }).Select(AssetDatabase.GUIDToAssetPath).Distinct().ToList()) AssetDatabase.DeleteAsset(p);
        var meshFile = cellDir + "/" + complex + "_RoofLOD_Meshes.asset"; bool firstMesh = true;
        var root = new GameObject(complex + "_RoofLOD");
        int nCells = 0; long trisNear = 0, trisEnv = 0, trisFlat = 0;
        foreach (var key in keys)
        {
            var go = new GameObject("RoofCell_" + key.x + "_" + key.y); go.transform.SetParent(root.transform, false);
            var near = new List<Renderer>(); var mid = new List<Renderer>(); var far = new List<Renderer>();
            Func<NB, string, string, bool, Renderer> mk = (nb, suffix, label, collider) =>
            {
                var mesh = nb.ToMesh(complex + "_roof_" + key.x + "_" + key.y + "_" + suffix);
                PalaceMeshCompact.CompactMesh(mesh);
                if (firstMesh) { AssetDatabase.CreateAsset(mesh, meshFile); firstMesh = false; } else AssetDatabase.AddObjectToAsset(mesh, meshFile);
                var o = new GameObject(label); o.transform.SetParent(go.transform, false);
                o.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = o.AddComponent<MeshRenderer>(); r.sharedMaterials = nb.mats.ToArray();
                if (collider) o.AddComponent<MeshCollider>().sharedMesh = mesh;
                return r;
            };
            List<Transform> objs;
            if (objBy.TryGetValue(key, out objs))
                foreach (var t in objs)
                {
                    var copy = UnityEngine.Object.Instantiate(t.gameObject); copy.name = t.name; copy.SetActive(true);
                    foreach (Transform ch in copy.transform.Cast<Transform>().ToList()) UnityEngine.Object.DestroyImmediate(ch.gameObject);
                    copy.transform.SetParent(go.transform, false);
                    var M = RelTo(t, origRoot.transform);
                    copy.transform.localPosition = M.GetColumn(3); copy.transform.localRotation = M.rotation; copy.transform.localScale = M.lossyScale;
                    near.Add(copy.GetComponent<Renderer>()); trisNear += Tris(copy.GetComponent<MeshFilter>().sharedMesh);
                }
            NB b;
            if (nearBy.TryGetValue(key, out b)) { near.Add(mk(b, "near", "Near", true)); trisNear += b.TriCount; }
            if (envBy.TryGetValue(key, out b)) { mid.Add(mk(b, "env", "Smooth", false)); trisEnv += b.TriCount; }
            if (flatBy.TryGetValue(key, out b)) { far.Add(mk(b, "flat", "Flat", false)); trisFlat += b.TriCount; }
            if (near.Count == 0) near.AddRange(mid);
            var lg = go.AddComponent<LODGroup>();
            lg.SetLODs(new[] { new LOD(0.5f, near.ToArray()), new LOD(0.1f, mid.ToArray()), new LOD(0.001f, far.ToArray()) });
            lg.RecalculateBounds();
            // 거리는 칸 한가운데에서, 크기는 칸 크기로 고정(칸 밖으로 조금 삐져나온 기와 조각 때문에 크기가 커져 원본이 멀리까지 남지 않게)
            var allR = near.Concat(mid).Concat(far).Where(x => x != null).ToList();
            float cy = allR.Count > 0 ? allR.Average(x => x.bounds.center.y) : 0f;
            lg.localReferencePoint = go.transform.InverseTransformPoint(new Vector3((key.x + 0.5f) * cell, cy, (key.y + 0.5f) * cell));
            lg.size = cell * 1.25f;
            float size = lg.size;
            float h0 = Mathf.Min(0.98f, size / (nearDist * K)), h1 = Mathf.Min(h0 * 0.9f, size / (flatDist * K)), h2 = Mathf.Min(h1 * 0.5f, size / (4000f * K));
            lg.SetLODs(new[] { new LOD(h0, near.ToArray()), new LOD(h1, mid.ToArray()), new LOD(h2, far.ToArray()) });
            nCells++;
        }
        var path = dir + "/" + complex + "_RoofLOD.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path);
        UnityEngine.Object.DestroyImmediate(root);
        foreach (var g in bigToDestroy) UnityEngine.Object.DestroyImmediate(g);
        if (origRoot != null) { PrefabUtility.SaveAsPrefabAsset(origRoot, dir + "/" + complex + "_Original.prefab"); PrefabUtility.UnloadPrefabContents(origRoot); }

        // ---- 구운 덩어리: _roof 끄고, 예전 껍데기 LOD 는 떼기 ----
        foreach (var name in goodNames)
        {
            var bp = dir + "/" + name + "_Baked.prefab";
            var rt = PrefabUtility.LoadPrefabContents(bp);
            try
            {
                var lg0 = rt.GetComponent<LODGroup>(); if (lg0 != null) UnityEngine.Object.DestroyImmediate(lg0);
                var env0 = rt.transform.Find(name + "_roofenv"); if (env0 != null) UnityEngine.Object.DestroyImmediate(env0.gameObject);
                var rr = rt.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.name == name + "_roof");
                if (rr != null)
                {
                    // 예전(ImportRoofEnvStep)에 껍데기로 바꿔 둔 메시는 원래 구운 지붕으로 되돌려 두고 끈다
                    var orig = AssetDatabase.LoadAllAssetsAtPath(dir + "/" + name + ".fbx").OfType<Mesh>().FirstOrDefault(x => x.name == name + "_roof");
                    var om = AssetDatabase.LoadAssetAtPath<Material>(dir + "/MAT_" + name + "_roof.mat");
                    if (orig != null) rr.GetComponent<MeshFilter>().sharedMesh = orig;
                    if (om != null) rr.sharedMaterial = om;
                    rr.gameObject.SetActive(false);
                }
                PrefabUtility.SaveAsPrefabAsset(rt, bp);
            }
            finally { PrefabUtility.UnloadPrefabContents(rt); }
        }
        AssetDatabase.SaveAssets();
        return string.Format("DONE {0} cells={1} near={2} smooth={3} flat={4} good={5} {6}", complex, nCells, trisNear, trisEnv, trisFlat, goodNames.Count, log);
    }

    static long Tris(Mesh m) { long t = 0; if (m == null) return 0; for (int i = 0; i < m.subMeshCount; i++) t += m.GetIndexCount(i) / 3; return t; }
}
