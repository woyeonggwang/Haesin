// 경복궁 "들어갈 수 없는 건물"을 속이 빈 상자처럼 만드는 도구 — MCP 에서 정적 메서드로 호출한다(사용자 지시 2026-09-27).
// 목표: 방 안에 카메라가 들어가면 큐브 안에 들어간 것처럼 아무것도 안 보이고(벽은 바깥 면만, 한쪽 면만 그림),
//       바깥에서도 방 안이 보이지 않는다(천장에 걸어 올린 들어열개 문은 내려 닫음). 근정전 본채(GJJ)·경회루는 제외.
// 순서: Begin(단지) → Step(단지, 초) 를 "DONE" 이 나올 때까지 → Apply(단지) 를 "SAVED" 가 나올 때까지 → End()
//   1) 들어열개 문(가로로 누운 문 카드)을 경첩 쪽 모서리를 축으로 90° 내려 닫는다.
//   2) 방 판정(0.5 m 칸): 가까운 기둥 높이의 1/3·3/4 에서 8방향 가로 레이가 모두 30 m 안에서 막히고, 레이가 지나는 곳이 모두 지붕 밑이면 방.
//      (트인 회랑·문 통로·처마 밑은 한쪽이 마당(하늘)으로 트여 방이 아님)
//   3) 방 칸(+1칸) 안의 삼각형만 지울 후보. 방 밖 시점(건물 둘레 2 m 간격, 눈높이·4 m + 먼 시점)에서 큐브맵으로 그려
//      한 번이라도 보인 후보만 남긴다(앞면으로 보였으면 그대로, 뒷면으로 보였으면 뒤집은 사본). 후보 삼각형은 한쪽 면만 그리는 재질(_Shell)로.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class PalaceShellTool
{
    const string Root = PalaceBakeTool.BakedAssetRoot;
    const string ShellMatDir = "Assets/02.Resources/01.Asset/Models/Gyeongbokgung/_Shared/ShellMaterials";
    static readonly Vector3 Off = new Vector3(0, 5000, 0);
    static readonly Regex PillarRx = new Regex("pill?ar", RegexOptions.IgnoreCase);

    class Pillar { public Vector2 p; public float bottom, top; public string name; }

    class St
    {
        public string cx, phase = ""; public bool roofPass;
        public Scene scene;
        public GameObject orig, doors, roof;
        public List<MeshRenderer> rends = new List<MeshRenderer>();   // 그리는 것 전부(가림막 포함)
        public List<int> process = new List<int>();                    // 손볼 것(원본·문짝) — rends 의 번호
        public List<int[]> triBase = new List<int[]>();
        public int totalTris;
        public ComputeBuffer vis;
        public Material mat; public RenderTexture rt; public bool flip;
        public List<Pillar> pillars = new List<Pillar>();
        public Dictionary<long, List<int>> pgrid = new Dictionary<long, List<int>>();
        public float cell = 0.5f; public Vector3 gmin; public int nx, nz;
        public byte[] inter;   // 1 = 방
        public float[] baseY;
        public int rasterIdx;
        public List<Vector3> views = new List<Vector3>(); public int viewIdx;
        public uint[] visData;
        public int buildIdx;
        public Dictionary<int, Mesh> newMesh = new Dictionary<int, Mesh>(); public HashSet<int> drop = new HashSet<int>();
        public Dictionary<int, Material[]> newMats = new Dictionary<int, Material[]>();
        public Dictionary<int, Material[]> matOnly = new Dictionary<int, Material[]>();   // 메시는 그대로, 재질만 한쪽 면으로
        public Mesh container, skin; public string containerPath;
        public long candTris, keptTris, dropTris;
        public StringBuilder log = new StringBuilder();
        public List<string> rotated = new List<string>();
    }
    static St S;

    static long Key(int i, int k) { return ((long)i << 32) ^ (uint)k; }
    static int Tris(Mesh m, int s) { return (int)(m.GetIndexCount(s) / 3); }
    static bool IsEnterable(string cx, string meshName)
    {
        if (cx == "Gyeonghoeru") return true;
        if (cx == "Geunjeongjeon") return meshName.StartsWith("SM_GJJ_") && !meshName.StartsWith("SM_GJJ_HG");
        return false;
    }

    // ------------------------------------------------------------------
    public static string Begin(string cx) { return Begin(cx, false); }

    // roofPass: 이미 속을 비운 단지에서 지붕(기와)만 다시 처리(RepairRoofs 뒤). 메시는 기존 <단지>_Shell.asset 에 덧붙임, 창호지 막은 그대로.
    public static string Begin(string cx, bool roofPass)
    {
        if (S != null) End();
        S = new St { cx = cx, roofPass = roofPass };
        var dir = Root + "/" + cx;
        S.containerPath = dir + "/" + cx + "_Shell.asset";
        if (!roofPass && File.Exists(S.containerPath)) return "이미 속을 비운 단지입니다(" + S.containerPath + ") — 원본을 다시 만들고 해야 함";
        S.scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        Func<string, GameObject> inst = p =>
        {
            var a = AssetDatabase.LoadAssetAtPath<GameObject>(p); if (a == null) return null;
            var g = (GameObject)PrefabUtility.InstantiatePrefab(a, S.scene); g.transform.position = Off; return g;
        };
        S.orig = inst(dir + "/" + cx + "_Original.prefab");
        S.doors = inst(dir + "/" + cx + "_Doors.prefab");
        S.roof = inst(dir + "/" + cx + "_RoofLOD.prefab");
        if (S.orig == null) return "no original";
        Physics.queriesHitBackfaces = true;
        // 콜라이더(레이용): 렌더러마다 MeshCollider
        foreach (var g in new[] { S.orig, S.doors, S.roof })
        {
            if (g == null) continue;
            foreach (var r in g.GetComponentsInChildren<MeshRenderer>(false))
            {
                var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
                var mc = r.GetComponent<MeshCollider>(); if (mc == null) mc = r.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh; mc.enabled = true;
            }
        }
        // 렌더러 없는 콜라이더(데모 프리팹의 BP_* 충돌용 메시 — 기와와 겹쳐 있음)는 끈다: 위로 쏜 레이가 제자리에서 걸려 기와가 전부 "덮임"으로 잘못 판정됐음
        foreach (var g in new[] { S.orig, S.doors, S.roof })
            if (g != null) foreach (var c in g.GetComponentsInChildren<Collider>(true)) if (c.GetComponent<MeshRenderer>() == null) c.enabled = false;
        // 지붕 칸 LOD 는 가까운 것(LOD0 원본 기와)만 가림막으로
        if (S.roof != null)
            foreach (var lg in S.roof.GetComponentsInChildren<LODGroup>())
            {
                var lods = lg.GetLODs();
                for (int i = 1; i < lods.Length; i++) foreach (var r in lods[i].renderers) if (r != null) { r.enabled = false; var mc = r.GetComponent<MeshCollider>(); if (mc) mc.enabled = false; }
            }
        Physics.SyncTransforms();

        // 기둥
        foreach (var r in S.orig.GetComponentsInChildren<MeshRenderer>(false))
        {
            var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
            var n = mf.sharedMesh.name; if (!PillarRx.IsMatch(n) || n.ToLower().Contains("bridge")) continue;
            var b = r.bounds; if (b.size.y < 1.5f || Mathf.Max(b.size.x, b.size.z) > 1.2f) continue;
            S.pillars.Add(new Pillar { p = new Vector2(b.center.x, b.center.z), bottom = b.min.y, top = b.max.y, name = n });
        }
        for (int i = 0; i < S.pillars.Count; i++)
        {
            long k = Key(Mathf.FloorToInt(S.pillars[i].p.x / 8f), Mathf.FloorToInt(S.pillars[i].p.y / 8f)); List<int> l;
            if (!S.pgrid.TryGetValue(k, out l)) S.pgrid[k] = l = new List<int>(); l.Add(i);
        }
        // 1) 들어열개 문 내리기
        RotateLiftedDoors();
        AddDoorFrameBlockers();
        Physics.SyncTransforms();

        // 격자
        var pb = new Bounds(new Vector3(S.pillars[0].p.x, 0, S.pillars[0].p.y), Vector3.zero);
        foreach (var p in S.pillars) pb.Encapsulate(new Vector3(p.p.x, 0, p.p.y));
        pb.Expand(12f);
        S.gmin = pb.min; S.nx = Mathf.CeilToInt(pb.size.x / S.cell); S.nz = Mathf.CeilToInt(pb.size.z / S.cell);
        S.inter = new byte[S.nx * S.nz]; S.baseY = new float[S.nx * S.nz];
        S.phase = "raster";

        // 그리기 준비
        var sh = Shader.Find("Hidden/HaesinTriVis"); if (sh == null) return "shader missing";
        S.mat = new Material(sh);
        S.rt = new RenderTexture(256, 256, 24, RenderTextureFormat.R8); S.rt.Create();
        foreach (var g in new[] { S.orig, S.doors, S.roof })
        {
            if (g == null) continue;
            foreach (var r in g.GetComponentsInChildren<MeshRenderer>(false))
            {
                if (!r.enabled) continue;
                var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
                int id = S.rends.Count; S.rends.Add(r);
                var tb = new int[mf.sharedMesh.subMeshCount];
                for (int s = 0; s < tb.Length; s++) { tb[s] = S.totalTris; S.totalTris += Tris(mf.sharedMesh, s); }
                S.triBase.Add(tb);
                // 기와는 방 안을 보는 아랫면만 지움(BuildOne 의 roofOnly)
                if (g != S.roof && !IsEnterable(cx, mf.sharedMesh.name) && (!roofPass || PalaceRoofLodTool.IsRoofTile(mf.sharedMesh.name))) S.process.Add(id);
            }
        }
        S.vis = new ComputeBuffer(Math.Max(1, S.totalTris), 4);
        S.vis.SetData(new uint[Math.Max(1, S.totalTris)]);
        S.flip = CalibrateFace();
        return string.Format("BEGIN {0} rends={1} process={2} tris={3} pillars={4} grid={5}x{6} lifted={7} flip={8}",
            cx, S.rends.Count, S.process.Count, S.totalTris, S.pillars.Count, S.nx, S.nz, S.rotated.Count, S.flip);
    }

    // 가로로 누운 문(들어열개)을 내려 닫는다
    static void RotateLiftedDoors()
    {
        foreach (var r in S.orig.GetComponentsInChildren<MeshRenderer>(false))
        {
            var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
            var n = mf.sharedMesh.name.ToLowerInvariant();
            if (!n.Contains("door") || n.Contains("frame") || n.Contains("handle") || n.Contains("hinge") || n.Contains("lock") || n.Contains("iron") || n.Contains("knob")) continue;
            var b = r.bounds; float hx = b.size.x, hz = b.size.z;
            if (b.size.y > 0.3f || Mathf.Min(hx, hz) < 0.4f || b.size.y > 0.25f * Mathf.Min(hx, hz)) continue;
            bool longX = hx >= hz; float len = longX ? hx : hz;
            float e0 = longX ? b.min.x : b.min.z, e1 = longX ? b.max.x : b.max.z;
            float lat = longX ? b.center.z : b.center.x;
            // 경첩 = 벽(기둥 줄)에 가까운 끝
            float best0 = 99, best1 = 99;
            foreach (var p in S.pillars)
            {
                float pl = longX ? p.p.x : p.p.y, pt = longX ? p.p.y : p.p.x;
                if (Mathf.Abs(pt - lat) > 4f || pl < e0 - 1.5f || pl > e1 + 1.5f) continue;
                best0 = Mathf.Min(best0, Mathf.Abs(pl - e0)); best1 = Mathf.Min(best1, Mathf.Abs(pl - e1));
            }
            if (best0 > 98 && best1 > 98) continue;
            float hingeL = best0 <= best1 ? e0 : e1;
            var pivot = longX ? new Vector3(hingeL, b.center.y, b.center.z) : new Vector3(b.center.x, b.center.y, hingeL);
            var axis = longX ? Vector3.forward : Vector3.right;
            var t = r.transform; var p0 = t.position; var q0 = t.rotation;
            t.RotateAround(pivot, axis, 90f);
            if (r.bounds.center.y > b.center.y) { t.position = p0; t.rotation = q0; t.RotateAround(pivot, axis, -90f); }
            S.rotated.Add(mf.sharedMesh.name + " bottom=" + (r.bounds.min.y - Off.y).ToString("F2"));
        }
    }

    // 문짝이 달린 문틀(문 카드가 틀 안에 있는 것)은 방 판정 레이가 새지 않게 얇은 상자 콜라이더로 막는다.
    // 들어열개 분합문은 두 짝이 겹쳐 올려져 있어 내리면 한 칸의 절반만 덮여(강녕전 대청) 레이가 빠져나가 방으로 안 잡혔음.
    // 문짝 없는 통로 문틀은 막지 않는다(예전에 통로를 창호지로 막은 적 있음).
    static void AddDoorFrameBlockers()
    {
        var doors = new List<Bounds>();
        foreach (var r in S.orig.GetComponentsInChildren<MeshRenderer>(false))
        {
            var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
            var n = mf.sharedMesh.name.ToLowerInvariant();
            if (n.Contains("door") && !n.Contains("frame") && !n.Contains("handle")) doors.Add(r.bounds);
        }
        int added = 0;
        foreach (var r in S.orig.GetComponentsInChildren<MeshRenderer>(false))
        {
            var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
            var ln = mf.sharedMesh.name.ToLowerInvariant();
            bool isWin = ln.Contains("window"), isFrame = ln.Contains("doorframe");
            if (!isWin && !isFrame) continue;
            var b = r.bounds; bool thinX = b.size.x < 0.35f, thinZ = b.size.z < 0.35f;
            if (thinX == thinZ) continue;
            if (isFrame)
            {
                if (b.size.y < 1.8f || Mathf.Max(b.size.x, b.size.z) < 1.5f) continue;
                var e = b; e.Expand(new Vector3(thinX ? 1f : 0f, 0f, thinZ ? 1f : 0f));
                if (!doors.Any(d => e.Contains(d.center))) continue;
            }
            else if (b.size.y < 0.3f || Mathf.Max(b.size.x, b.size.z) < 0.3f) continue;   // 창(창살 틈으로 레이가 샘)
            var g = new GameObject("DoorFrameBlocker"); SceneManager.MoveGameObjectToScene(g, S.scene);
            g.transform.position = b.center;
            var bc = g.AddComponent<BoxCollider>(); bc.size = new Vector3(thinX ? 0.05f : b.size.x, b.size.y, thinZ ? 0.05f : b.size.z);
            added++;
        }
        S.log.Append("frameBlockers " + added + "; ");
    }

    static bool CalibrateFace()
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad); SceneManager.MoveGameObjectToScene(q, S.scene);
        UnityEngine.Object.DestroyImmediate(q.GetComponent<Collider>());
        q.transform.position = Off + new Vector3(0, -300, 0);   // 앞면 = -z 쪽
        var buf = new ComputeBuffer(4, 4); buf.SetData(new uint[4]);
        var cmd = new CommandBuffer();
        var cam = q.transform.position + new Vector3(0, 0, -2);
        var view = Matrix4x4.Scale(new Vector3(1, 1, -1)) * Matrix4x4.TRS(cam, Quaternion.identity, Vector3.one).inverse;
        cmd.SetRenderTarget(S.rt); cmd.ClearRenderTarget(true, true, Color.clear);
        cmd.SetViewProjectionMatrices(view, Matrix4x4.Perspective(60, 1, 0.05f, 50));
        cmd.SetRandomWriteTarget(1, buf, false);
        cmd.SetGlobalInteger("_FlipFace", 0); cmd.SetGlobalInteger("_TriBase", 0);
        var r = q.GetComponent<MeshRenderer>();
        cmd.DrawRenderer(r, S.mat, 0, 0); cmd.DrawRenderer(r, S.mat, 0, 1);
        cmd.ClearRandomWriteTargets();
        Graphics.ExecuteCommandBuffer(cmd); cmd.Release();
        var d = new uint[4]; buf.GetData(d); buf.Release();
        UnityEngine.Object.DestroyImmediate(q);
        S.log.Append("calib " + d[0] + "," + d[1] + "; ");
        return d[0] == 2 || d[1] == 2;
    }

    static Pillar NearestPillar(float x, float z, float maxD)
    {
        int ci = Mathf.FloorToInt(x / 8f), ck = Mathf.FloorToInt(z / 8f); Pillar best = null; float bd = maxD * maxD;
        for (int di = -2; di <= 2; di++) for (int dk = -2; dk <= 2; dk++)
            {
                List<int> l; if (!S.pgrid.TryGetValue(Key(ci + di, ck + dk), out l)) continue;
                foreach (var i in l) { var p = S.pillars[i]; float d = (p.p.x - x) * (p.p.x - x) + (p.p.y - z) * (p.p.y - z); if (d < bd) { bd = d; best = p; } }
            }
        return best;
    }

    static bool Roofed(Vector3 p) { return Physics.Raycast(p, Vector3.up, 25f); }

    static readonly Vector3[] Dirs = Enumerable.Range(0, 8).Select(i => new Vector3(Mathf.Cos(i * Mathf.PI / 4), 0, Mathf.Sin(i * Mathf.PI / 4))).ToArray();

    static bool IsRoom(float x, float z, Pillar p)
    {
        float h = p.top - p.bottom;
        foreach (var fr in new[] { 0.35f, 0.75f })
        {
            var o = new Vector3(x, p.bottom + fr * h, z);
            if (!Roofed(o)) return false;
            foreach (var d in Dirs)
            {
                RaycastHit hit;
                if (!Physics.Raycast(o, d, out hit, 30f)) return false;
                float L = hit.distance;
                if (L > 1f)
                    foreach (var f in new[] { 0.33f, 0.66f })
                        if (!Roofed(o + d * (L * f))) return false;
                if (L > 0.6f && !Roofed(o + d * (L - 0.3f))) return false;
            }
        }
        return true;
    }

    // ------------------------------------------------------------------
    public static string Step(string cx, float budget = 20f)
    {
        if (S == null || S.cx != cx) return "Begin 먼저";
        var sw = System.Diagnostics.Stopwatch.StartNew();
        if (S.phase == "raster")
        {
            int n = S.nx * S.nz;
            while (S.rasterIdx < n && sw.Elapsed.TotalSeconds < budget)
            {
                int c = S.rasterIdx++; int i = c % S.nx, k = c / S.nx;
                float x = S.gmin.x + (i + 0.5f) * S.cell, z = S.gmin.z + (k + 0.5f) * S.cell;
                var p = NearestPillar(x, z, 12f); if (p == null) continue;
                S.baseY[c] = p.bottom;
                if (IsEnterable(cx, p.name)) continue;
                if (IsRoom(x, z, p)) S.inter[c] = 1;
            }
            if (S.rasterIdx < n) return string.Format("raster {0}/{1}", S.rasterIdx, n);
            MakeViews();
            S.phase = "views";
            return string.Format("raster done rooms={0} views={1}", S.inter.Count(b => b == 1), S.views.Count);
        }
        if (S.phase == "views")
        {
            var cmd = new CommandBuffer();
            var rot = new[] { Quaternion.LookRotation(Vector3.forward), Quaternion.LookRotation(Vector3.back), Quaternion.LookRotation(Vector3.right),
                Quaternion.LookRotation(Vector3.left), Quaternion.LookRotation(Vector3.up, Vector3.back), Quaternion.LookRotation(Vector3.down, Vector3.forward) };
            var proj = Matrix4x4.Perspective(92f, 1f, 0.05f, 250f);
            cmd.SetGlobalInteger("_FlipFace", S.flip ? 1 : 0);
            int done = 0;
            while (S.viewIdx < S.views.Count && sw.Elapsed.TotalSeconds < budget)
            {
                var pos = S.views[S.viewIdx++];
                var near = new List<int>();
                for (int i = 0; i < S.rends.Count; i++) if (S.rends[i].bounds.SqrDistance(pos) < 70f * 70f) near.Add(i);
                foreach (var q in rot)
                {
                    var view = Matrix4x4.Scale(new Vector3(1, 1, -1)) * Matrix4x4.TRS(pos, q, Vector3.one).inverse;
                    var planes = GeometryUtility.CalculateFrustumPlanes(proj * view);
                    var list = near.Where(i => GeometryUtility.TestPlanesAABB(planes, S.rends[i].bounds)).ToList();
                    cmd.SetRenderTarget(S.rt); cmd.ClearRenderTarget(true, true, Color.clear);
                    cmd.SetViewProjectionMatrices(view, proj);
                    cmd.SetRandomWriteTarget(1, S.vis, false);
                    foreach (var i in list) { var r = S.rends[i]; for (int s = 0; s < S.triBase[i].Length; s++) cmd.DrawRenderer(r, S.mat, s, 0); }
                    foreach (var i in list) { var r = S.rends[i]; for (int s = 0; s < S.triBase[i].Length; s++) { cmd.SetGlobalInteger("_TriBase", S.triBase[i][s]); cmd.DrawRenderer(r, S.mat, s, 1); } }
                    cmd.ClearRandomWriteTargets();
                }
                Graphics.ExecuteCommandBuffer(cmd); cmd.Clear(); done++;
            }
            cmd.Release();
            if (S.viewIdx < S.views.Count) return string.Format("views {0}/{1} (+{2})", S.viewIdx, S.views.Count, done);
            S.visData = new uint[S.totalTris]; S.vis.GetData(S.visData);
            S.phase = "build";
            return "views done seen=" + S.visData.Count(v => v != 0);
        }
        if (S.phase == "build")
        {
            while (S.buildIdx < S.process.Count && sw.Elapsed.TotalSeconds < budget) BuildOne(S.process[S.buildIdx++]);
            if (S.buildIdx < S.process.Count) return string.Format("build {0}/{1}", S.buildIdx, S.process.Count);
            S.skin = S.roofPass ? new Mesh() : BuildSkin();
            S.phase = "ready";
            return string.Format("DONE skinTris={7} cand={0} kept={1} dropped={2} newMesh={3} dropRend={4}", S.candTris, S.keptTris, S.dropTris, S.newMesh.Count, S.drop.Count, 0, 0, S.skin.triangles.Length / 3);
        }
        return "phase " + S.phase;
    }

    static bool Cand(Vector3 w)
    {
        int i = Mathf.FloorToInt((w.x - S.gmin.x) / S.cell), k = Mathf.FloorToInt((w.z - S.gmin.z) / S.cell);
        for (int di = -1; di <= 1; di++) for (int dk = -1; dk <= 1; dk++)
            {
                int a = i + di, b = k + dk; if (a < 0 || b < 0 || a >= S.nx || b >= S.nz) continue;
                int c = b * S.nx + a;
                if (S.inter[c] == 1 && w.y >= S.baseY[c] - 10f) return true;   // 방 기둥 밑보다 한참 낮은 흙바닥·기단 윗면(강녕전 2 m)도 방 안이면 지움
            }
        return false;
    }

    // 바깥을 보는 면인가: 옆을 보는 면은 "뒤로 1 m 는 방, 앞으로 0.6 m 는 방 아님"(벽 바깥 면),
    // 위·아래를 보는 면은 방 밖(둘레 띠)에 있으면 남김(방 안 바닥·천장은 지움)
    static bool Outward(Vector3 c, Vector3 n)
    {
        if (Mathf.Abs(n.y) > 0.7f) return !IsRoomXZ(c);
        var h = new Vector3(n.x, 0, n.z).normalized;
        return IsRoomXZ(c - h * 1.0f) && !IsRoomXZ(c + h * 0.6f);
    }

    static bool IsRoomXZ(Vector3 w)
    {
        int i = Mathf.FloorToInt((w.x - S.gmin.x) / S.cell), k = Mathf.FloorToInt((w.z - S.gmin.z) / S.cell);
        if (i < 0 || k < 0 || i >= S.nx || k >= S.nz) return false;
        return S.inter[k * S.nx + i] == 1;
    }

    // 방 둘레 안쪽(마지막 방 칸 한가운데 줄)에 바깥을 보는 창호지 막을 두른다 — 창살 틈·문 틈·들어열개 자리로 속(비어 있음)이 비쳐 보이지 않게.
    // 벽 안쪽 면보다 안쪽이라 벽이 있는 곳에선 안 보이고, 구멍 난 곳에서만 보인다. 한쪽 면만 그려 방 안에서는 안 보인다.
    static Mesh BuildSkin()
    {
        var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
        int[] di = { 1, -1, 0, 0 }, dk = { 0, 0, 1, -1 };
        for (int d = 0; d < 4; d++)
        {
            // 같은 방향 경계를 줄 단위로 이어 붙인다
            bool alongZ = d < 2;   // x 방향 이웃 → 경계면은 z 를 따라감
            int outer = alongZ ? S.nx : S.nz, inner = alongZ ? S.nz : S.nx;
            for (int a = 0; a < outer; a++)
            {
                int runStart = -1; float y0 = 0, y1 = 0;
                for (int b = 0; b <= inner; b++)
                {
                    bool edge = false; float by = 0, ty = 0;
                    if (b < inner)
                    {
                        int i = alongZ ? a : b, k = alongZ ? b : a; int c = k * S.nx + i;
                        int ni = i + di[d], nk = k + dk[d];
                        bool nOut = ni < 0 || nk < 0 || ni >= S.nx || nk >= S.nz || S.inter[nk * S.nx + ni] != 1;
                        if (S.inter[c] == 1 && nOut)
                        {
                            var p = NearestPillar(S.gmin.x + (i + 0.5f) * S.cell, S.gmin.z + (k + 0.5f) * S.cell, 12f);
                            if (p != null) { edge = true; by = p.bottom; ty = p.top; }
                        }
                    }
                    if (edge && runStart >= 0 && Mathf.Abs(by - y0) < 0.05f && Mathf.Abs(ty - y1) < 0.05f) continue;
                    if (runStart >= 0)
                    {
                        // [runStart, b) 구간 막
                        float s0 = (alongZ ? S.gmin.z : S.gmin.x) + runStart * S.cell, s1 = (alongZ ? S.gmin.z : S.gmin.x) + b * S.cell;
                        float fixedC = (alongZ ? S.gmin.x : S.gmin.z) + (a + 0.5f) * S.cell;
                        Vector3 nor = new Vector3(di[d], 0, dk[d]);
                        Vector3 P0, P1;
                        if (alongZ) { P0 = new Vector3(fixedC, 0, s0); P1 = new Vector3(fixedC, 0, s1); } else { P0 = new Vector3(s0, 0, fixedC); P1 = new Vector3(s1, 0, fixedC); }
                        int bi = v.Count;
                        v.Add(new Vector3(P0.x, y0, P0.z)); v.Add(new Vector3(P1.x, y0, P1.z)); v.Add(new Vector3(P1.x, y1, P1.z)); v.Add(new Vector3(P0.x, y1, P0.z));
                        for (int q = 0; q < 4; q++) n.Add(nor);
                        float L = s1 - s0;
                        uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(L, 0)); uv.Add(new Vector2(L, y1 - y0)); uv.Add(new Vector2(0, y1 - y0));
                        // 앞면이 nor 쪽을 보게
                        var cr = Vector3.Cross(v[bi + 1] - v[bi], v[bi + 2] - v[bi]);
                        if (Vector3.Dot(cr, nor) > 0) { tri.AddRange(new[] { bi, bi + 1, bi + 2, bi, bi + 2, bi + 3 }); }
                        else { tri.AddRange(new[] { bi, bi + 2, bi + 1, bi, bi + 3, bi + 2 }); }
                        runStart = -1;
                    }
                    if (edge) { runStart = b; y0 = by; y1 = ty; }
                }
            }
        }
        var m = new Mesh { name = S.cx + "_ShellSkin", indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
        m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(tri, 0); m.RecalculateTangents(); m.RecalculateBounds();
        return m;
    }

    static Material SkinMat()
    {
        var hp = "Assets/02.Resources/01.Asset/Models/Gyeongbokgung/_Shared/Materials/MAT_Hanji_Window.mat";
        var h = AssetDatabase.LoadAssetAtPath<Material>(hp);
        return h == null ? null : ShellMat(h);
    }

    static void MakeViews()
    {
        // 방 칸에서 6 m 안, 방이 아닌 칸을 2 m 간격으로
        int step = Mathf.RoundToInt(2f / S.cell), reach = Mathf.RoundToInt(6f / S.cell);
        var near = new bool[S.nx * S.nz];
        for (int k = 0; k < S.nz; k++) for (int i = 0; i < S.nx; i++)
            {
                if (S.inter[k * S.nx + i] != 1) continue;
                for (int dk = -reach; dk <= reach; dk += 1) for (int di = -reach; di <= reach; di += 1)
                    {
                        int a = i + di, b = k + dk; if (a < 0 || b < 0 || a >= S.nx || b >= S.nz) continue; near[b * S.nx + a] = true;
                    }
            }
        for (int k = 0; k < S.nz; k += step) for (int i = 0; i < S.nx; i += step)
            {
                int c = k * S.nx + i; if (!near[c] || S.inter[c] == 1) continue;
                // 방 칸과 붙어 있으면(벽 속) 건너뜀
                bool touch = false;
                for (int dk = -1; dk <= 1 && !touch; dk++) for (int di = -1; di <= 1; di++)
                    { int a = i + di, b = k + dk; if (a >= 0 && b >= 0 && a < S.nx && b < S.nz && S.inter[b * S.nx + a] == 1) { touch = true; break; } }
                if (touch) continue;
                float x = S.gmin.x + (i + 0.5f) * S.cell, z = S.gmin.z + (k + 0.5f) * S.cell;
                float top = S.baseY[c] + 3f; RaycastHit hit; float g = S.baseY[c];
                if (Physics.Raycast(new Vector3(x, top, z), Vector3.down, out hit, 15f)) g = hit.point.y;
                foreach (var h in new[] { 1.6f, 4f })
                {
                    var p = new Vector3(x, g + h, z);
                    if (Physics.CheckSphere(p, 0.15f)) continue;
                    S.views.Add(p);
                }
            }
        // 먼 시점(하늘에서 둘러봄)
        var ctr = new Vector3(S.gmin.x + S.nx * S.cell * 0.5f, Off.y, S.gmin.z + S.nz * S.cell * 0.5f);
        float rad = Mathf.Max(S.nx, S.nz) * S.cell * 0.5f;
        for (int a = 0; a < 16; a++) foreach (var el in new[] { 12f, 35f })
            {
                float ang = a * Mathf.PI * 2 / 16;
                S.views.Add(ctr + new Vector3(Mathf.Cos(ang) * rad * 0.8f, el, Mathf.Sin(ang) * rad * 0.8f));
            }
    }

    static Material ShellMat(Material m)
    {
        if (m == null) return null;
        if (!AssetDatabase.IsValidFolder(ShellMatDir)) AssetDatabase.CreateFolder(Path.GetDirectoryName(ShellMatDir).Replace('\\', '/'), Path.GetFileName(ShellMatDir));
        var p = ShellMatDir + "/" + m.name + "_Shell.mat";
        var s = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (s != null) return s;
        s = new Material(m) { name = m.name + "_Shell" };
        s.SetFloat("_DoubleSidedEnable", 0); s.SetFloat("_CullMode", 2); s.SetFloat("_CullModeForward", 2);
        s.DisableKeyword("_DOUBLESIDED_ON");
        AssetDatabase.CreateAsset(s, p);
        var t = Type.GetType("UnityEditor.Rendering.HighDefinition.HDShaderUtils, Unity.RenderPipelines.HighDefinition.Editor");
        var mi = t == null ? null : t.GetMethod("ResetMaterialKeywords", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, new[] { typeof(Material) }, null);
        if (mi != null) mi.Invoke(null, new object[] { s });
        return s;
    }

    struct VR { public Vector3 p, n; public Vector2 u, u1; public Vector4 t; public int oi; }   // oi = 원래 정점 번호(나눠서 새로 생긴 정점은 -1)
    static VR Mid(VR a, VR b)
    {
        return new VR { p = (a.p + b.p) * 0.5f, n = (a.n + b.n).normalized, u = (a.u + b.u) * 0.5f, u1 = (a.u1 + b.u1) * 0.5f, t = (a.t + b.t) * 0.5f, oi = -1 };
    }

    // 삼각형 하나를 판정해 넣는다. 방 경계에 걸친 큰 삼각형(바닥 한 장이 20 m 인 것 등)은 1 m 밑이 될 때까지 4등분해 조각마다 판정한다
    // (예전엔 무게중심 하나로만 판정해 방 밖에 무게중심이 있는 큰 바닥·천장이 방 안에 그대로 남았음).
    // 결과 코드: 0 = 원래 재질로 남김, 1 = 한쪽 면 재질로 남김, 2 = 뒤집어 한쪽 면 재질로 남김, -1 = 지움
    static void Classify(VR a, VR b, VR c, Matrix4x4 L2W, bool mirror, bool roofOnly, uint vis, int depth, List<VR>[] outs, ref int removed)
    {
        Vector3 wa = L2W.MultiplyPoint3x4(a.p), wb = L2W.MultiplyPoint3x4(b.p), wc = L2W.MultiplyPoint3x4(c.p);
        float edge = Mathf.Max((wa - wb).sqrMagnitude, Mathf.Max((wb - wc).sqrMagnitude, (wc - wa).sqrMagnitude));
        if (edge > 1f && depth < 8)
        {
            // 조각마다 결과가 같을지 표본으로 확인(꼭짓점·변 가운데·무게중심)
            var pts = new[] { wa, wb, wc, (wa + wb) * 0.5f, (wb + wc) * 0.5f, (wc + wa) * 0.5f, (wa + wb + wc) / 3f };
            int k0 = State(pts[0]); bool mixed = false;
            for (int i = 1; i < pts.Length && !mixed; i++) if (State(pts[i]) != k0) mixed = true;
            if (mixed)
            {
                VR ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                Classify(a, ab, ca, L2W, mirror, roofOnly, vis, depth + 1, outs, ref removed);
                Classify(ab, b, bc, L2W, mirror, roofOnly, vis, depth + 1, outs, ref removed);
                Classify(ca, bc, c, L2W, mirror, roofOnly, vis, depth + 1, outs, ref removed);
                Classify(ab, bc, ca, L2W, mirror, roofOnly, vis, depth + 1, outs, ref removed);
                return;
            }
        }
        var ce = (wa + wb + wc) / 3f;
        int code;
        if (!Cand(ce)) code = 0;
        else
        {
            var gn = Vector3.Cross(wb - wa, wc - wa); float gl = gn.magnitude;
            if (gl < 1e-12f) code = -1;
            else
            {
                gn /= gl; if (mirror) gn = -gn;
                if (roofOnly)
                {
                    // 기와·지붕 판(방 위): 삼각형에서 위로 쏜 레이가 하늘로 빠지면(맨 위에 드러난 면) 남기고 위를 보게 맞춘다(한쪽 면 재질 →
                    // 방 안에서 올려다보면 뒷면이라 안 보임). 위가 막힌 아래층(기와 밑면·개판 등)은 지운다.
                    // 예전엔 "아래를 보는 면은 지움 + 먼 시점 그림에서 뒷면이 보였으면 남김" 이었는데, 이 에셋의 기와판은 법선이 아래인 채 양면으로
                    // 그려지는 것이 많고 먼 시점 그림(256 px)이 기와 조각을 놓쳐 사정전 지붕에 구멍이 났음(사용자 캡처 2026-09-27).
                    if (!IsRoomXZ(ce)) code = 0;
                    else if (Physics.Raycast(ce + Vector3.up * 0.05f, Vector3.up, 40f)) code = -1;
                    else if (gn.y <= -0.3f) code = 2;
                    else code = 1;
                }
                else if (Outward(ce, gn)) code = 1;
                else if ((vis & 2) != 0 && Outward(ce, -gn)) code = 2;
                else code = -1;
            }
        }
        if (code < 0) { removed++; return; }
        var o = outs[code];
        if (code == 2) { o.Add(c); o.Add(b); o.Add(a); } else { o.Add(a); o.Add(b); o.Add(c); }
    }

    static int State(Vector3 w) { return (Cand(w) ? 1 : 0) + (IsRoomXZ(w) ? 2 : 0); }

    static void BuildOne(int ri)
    {
        var r = S.rends[ri]; var mf = r.GetComponent<MeshFilter>(); var m = mf.sharedMesh;
        bool roofOnly = PalaceRoofLodTool.IsRoofTile(m.name);
        var L2W = r.transform.localToWorldMatrix; bool mirror = L2W.determinant < 0;   // 음수 배율이면 앞면 방향이 뒤집힘
        // 빠른 거르기: 렌더러 바운드가 후보 영역과 전혀 안 겹치면 건너뜀
        var bb = r.bounds; bool touch = false;
        for (float x = bb.min.x; x <= bb.max.x + S.cell && !touch; x += S.cell)
            for (float z = bb.min.z; z <= bb.max.z + S.cell && !touch; z += S.cell)
                if (Cand(new Vector3(Mathf.Min(x, bb.max.x), bb.max.y, Mathf.Min(z, bb.max.z)))) touch = true;
        if (!touch) return;
        var v = m.vertices; var nrm = m.normals; var uv = m.uv; var tg = m.tangents; var uv1 = m.uv2;
        int sub = m.subMeshCount;
        var outs = new List<VR>[sub][];
        int removed = 0; long total = 0;
        for (int s = 0; s < sub; s++)
        {
            outs[s] = new[] { new List<VR>(), new List<VR>(), new List<VR>() };
            var idx = m.GetTriangles(s); int tb = S.triBase[ri][s];
            for (int t = 0; t < idx.Length / 3; t++)
            {
                var q = new VR[3];
                for (int j = 0; j < 3; j++)
                {
                    int o = idx[t * 3 + j];
                    q[j] = new VR { p = v[o], n = nrm.Length > 0 ? nrm[o] : Vector3.up, u = uv.Length > 0 ? uv[o] : Vector2.zero, u1 = uv1.Length > 0 ? uv1[o] : Vector2.zero, t = tg.Length > 0 ? tg[o] : new Vector4(1, 0, 0, 1), oi = o };
                }
                total++;
                Classify(q[0], q[1], q[2], L2W, mirror, roofOnly, S.visData[tb + t], 0, outs[s], ref removed);
            }
        }
        bool changed = removed > 0 || outs.Any(o => o[1].Count > 0 || o[2].Count > 0);
        if (!changed) return;
        // 지운 것도 없고 뒤집은 것도 없으며 서브메시마다 전부 같은 쪽(원래 재질 / 한쪽 면 재질)이면 메시는 그대로 두고 재질만 바꾼다(공유 메시 유지)
        if (removed == 0 && outs.All(o => o[2].Count == 0 && (o[0].Count == 0 || o[1].Count == 0)))
        {
            var sm = r.sharedMaterials.ToArray(); bool any1 = false;
            for (int s = 0; s < sub && s < sm.Length; s++) if (outs[s][1].Count > 0) { sm[s] = ShellMat(sm[s]); any1 = true; }
            if (any1) { S.matOnly[ri] = sm; S.keptTris += total; }
            return;
        }
        long keptN = outs.Sum(o => (long)(o[0].Count + o[1].Count + o[2].Count) / 3);
        S.candTris += total; S.keptTris += keptN; S.dropTris += removed;
        if (keptN == 0) { S.drop.Add(ri); return; }
        // 새 메시: [원래 재질 부분…] + [한쪽 면 재질 부분…]
        var nv = new List<Vector3>(); var nn = new List<Vector3>(); var nu = new List<Vector2>(); var nu1 = new List<Vector2>(); var nt = new List<Vector4>();
        var subs = new List<List<int>>(); var mats = new List<Material>();
        var src = r.sharedMaterials;
        var reuse = new Dictionary<long, int>();
        Action<List<VR>, bool, List<int>> add = (list, flip, into) =>
        {
            foreach (var x in list)
            {
                // 원래 정점은 한 번만 넣는다(예전엔 삼각형마다 3개씩 새로 넣어 사정전 하나가 1.86 GB 였음)
                long key = x.oi >= 0 ? ((long)x.oi << 1) | (flip ? 1L : 0L) : -1; int vi;
                if (key >= 0 && reuse.TryGetValue(key, out vi)) { into.Add(vi); continue; }
                vi = nv.Count; if (key >= 0) reuse[key] = vi;
                into.Add(vi); nv.Add(x.p);
                nn.Add(flip ? -x.n : x.n); nu.Add(x.u); nu1.Add(x.u1);
                nt.Add(flip ? new Vector4(x.t.x, x.t.y, x.t.z, -x.t.w) : x.t);
            }
        };
        for (int s = 0; s < sub; s++)
        {
            if (outs[s][0].Count == 0) continue;
            var l = new List<int>(); add(outs[s][0], false, l); subs.Add(l); mats.Add(s < src.Length ? src[s] : null);
        }
        for (int s = 0; s < sub; s++)
        {
            if (outs[s][1].Count == 0 && outs[s][2].Count == 0) continue;
            var l = new List<int>(); add(outs[s][1], false, l); add(outs[s][2], true, l);
            subs.Add(l); mats.Add(ShellMat(s < src.Length ? src[s] : null));
        }
        var nm = new Mesh { name = m.name + "_shell_" + ri, indexFormat = nv.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
        nm.SetVertices(nv); nm.SetNormals(nn); nm.SetUVs(0, nu); if (uv1.Length > 0) nm.SetUVs(1, nu1); nm.SetTangents(nt);
        nm.subMeshCount = subs.Count; for (int s = 0; s < subs.Count; s++) nm.SetTriangles(subs[s], s, false);
        nm.RecalculateBounds();
        PalaceMeshCompact.CompactMesh(nm);
        S.newMesh[ri] = nm; S.newMats[ri] = mats.ToArray();
    }

    static int[] PathOf(Transform t, Transform root)
    {
        var p = new List<int>(); while (t != root) { p.Add(t.GetSiblingIndex()); t = t.parent; }
        p.Reverse(); return p.ToArray();
    }
    static Transform ByPath(Transform root, int[] p) { var t = root; foreach (var i in p) { if (i >= t.childCount) return null; t = t.GetChild(i); } return t; }

    // 결과를 원본·문짝 프리팹에 적용해 저장
    public static string Apply(string cx)
    {
        if (S == null || S.phase != "ready") return "not ready";
        var dir = Root + "/" + cx;
        // 메시는 한 파일(<단지>_Shell.asset)에 모음
        bool first = !File.Exists(S.containerPath);
        foreach (var kv in S.newMesh)
        {
            if (first) { AssetDatabase.CreateAsset(kv.Value, S.containerPath); first = false; }
            else AssetDatabase.AddObjectToAsset(kv.Value, S.containerPath);
        }
        if (S.roofPass) S.skin = null;
        if (S.skin != null) { if (first) { AssetDatabase.CreateAsset(S.skin, S.containerPath); first = false; } else AssetDatabase.AddObjectToAsset(S.skin, S.containerPath); }
        AssetDatabase.SaveAssets();
        var sb = new StringBuilder();
        foreach (var pair in new[] { new KeyValuePair<GameObject, string>(S.doors, dir + "/" + cx + "_Doors.prefab"), new KeyValuePair<GameObject, string>(S.orig, dir + "/" + cx + "_Original.prefab") })
        {
            if (pair.Key == null) continue;
            var rt = PrefabUtility.LoadPrefabContents(pair.Value);
            int ch = 0, rotN = 0;
            try
            {
                // 문 회전 반영(원본만)
                if (pair.Key == S.orig)
                    foreach (var r in S.orig.GetComponentsInChildren<MeshRenderer>(false))
                    {
                        var t2 = ByPath(rt.transform, PathOf(r.transform, S.orig.transform)); if (t2 == null) continue;
                        var lr = Quaternion.Inverse(S.orig.transform.rotation) * r.transform.rotation;
                        if (Quaternion.Angle(t2.rotation, rt.transform.rotation * lr) > 1f)
                        { t2.position = rt.transform.TransformPoint(S.orig.transform.InverseTransformPoint(r.transform.position)); t2.rotation = rt.transform.rotation * lr; rotN++; }
                    }
                var dropT = new List<GameObject>();
                for (int i = 0; i < S.rends.Count; i++)
                {
                    var r = S.rends[i]; if (r == null || !r.transform.IsChildOf(pair.Key.transform)) continue;   // 앞 프리팹을 저장하면 작업 씬 복사본이 다시 만들어져 참조가 끊김
                    bool nm = S.newMesh.ContainsKey(i), dr = S.drop.Contains(i), mo = S.matOnly.ContainsKey(i); if (!nm && !dr && !mo) continue;
                    var t2 = ByPath(rt.transform, PathOf(r.transform, pair.Key.transform)); if (t2 == null) continue;
                    var r2 = t2.GetComponent<MeshRenderer>(); var mf2 = t2.GetComponent<MeshFilter>(); if (r2 == null || mf2 == null) continue;
                    if (dr)
                    {
                        if (t2.childCount == 0) dropT.Add(t2.gameObject); else { r2.enabled = false; var mc0 = t2.GetComponent<MeshCollider>(); if (mc0) mc0.enabled = false; }
                    }
                    else if (mo) r2.sharedMaterials = S.matOnly[i];
                    else
                    {
                        mf2.sharedMesh = S.newMesh[i]; r2.sharedMaterials = S.newMats[i];
                        var mc = t2.GetComponent<MeshCollider>(); if (mc) mc.sharedMesh = S.newMesh[i];
                    }
                    ch++;
                }
                foreach (var g in dropT) UnityEngine.Object.DestroyImmediate(g);
                if (pair.Key == S.orig && S.skin != null)
                {
                    var old = rt.transform.Find("ShellSkin"); if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                    var sk = new GameObject("ShellSkin"); sk.transform.SetParent(rt.transform, false);
                    sk.transform.localPosition = -S.orig.transform.position;   // 작업 씬은 y +5000 에 놓았음
                    sk.AddComponent<MeshFilter>().sharedMesh = S.skin; sk.AddComponent<MeshRenderer>().sharedMaterial = SkinMat();
                }
                PrefabUtility.SaveAsPrefabAsset(rt, pair.Value);
            }
            finally { PrefabUtility.UnloadPrefabContents(rt); }
            sb.Append(Path.GetFileName(pair.Value) + " changed=" + ch + " rotated=" + rotN + "; ");
        }
        return "SAVED " + sb;
    }

    // 작업 씬(y +5000)의 복사본에 결과를 입혀 캡처로 확인
    public static string Preview()
    {
        if (S == null || S.phase != "ready") return "not ready";
        foreach (var kv in S.newMesh) { S.rends[kv.Key].GetComponent<MeshFilter>().sharedMesh = kv.Value; S.rends[kv.Key].sharedMaterials = S.newMats[kv.Key]; }
        foreach (var i in S.drop) S.rends[i].enabled = false;
        foreach (var kv in S.matOnly) S.rends[kv.Key].sharedMaterials = kv.Value;
        var sk = new GameObject("ShellSkinPreview"); SceneManager.MoveGameObjectToScene(sk, S.scene);
        sk.AddComponent<MeshFilter>().sharedMesh = S.skin; sk.AddComponent<MeshRenderer>().sharedMaterial = SkinMat();
        return "preview";
    }

    // 지붕(기와) 렌더러를 데모 씬(원본)에서 다시 가져온다 — 예전 규칙으로 기와에 구멍이 난 단지(사정전)를 고칠 때.
    // 이름 경로 + 위치로 짝을 찾아 메시·재질·콜라이더를 원래대로 돌리고, 통째로 지워졌던 것은 다시 만든다.
    public static string RepairRoofs(string cx, string demoScenePath)
    {
        if (S != null) End();
        PalaceBakeTool.OpenComplex(demoScenePath);
        var holder = GameObject.Find("__VIS_HOLDER"); if (holder == null) return "no holder";
        var path = Root + "/" + cx + "/" + cx + "_Original.prefab";
        var rt = PrefabUtility.LoadPrefabContents(path);
        int restored = 0, recreated = 0;
        try
        {
            var hp = holder.transform.position;
            foreach (var r in holder.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
                if (!PalaceRoofLodTool.IsRoofTile(mf.sharedMesh.name) || !r.gameObject.activeInHierarchy) continue;
                var chain = new List<Transform>(); var t = r.transform; while (t != holder.transform) { chain.Add(t); t = t.parent; } chain.Reverse();
                Transform cur = rt.transform; int level = 0;
                for (; level < chain.Count; level++)
                {
                    var want = chain[level].position - hp; Transform best = null; float bd = 0.05f;
                    foreach (Transform c in cur) if (c.name == chain[level].name) { float d = (c.position - want).magnitude; if (d < bd) { bd = d; best = c; } }
                    if (best == null) break;
                    cur = best;
                }
                if (level == chain.Count)
                {
                    var mf2 = cur.GetComponent<MeshFilter>(); var r2 = cur.GetComponent<MeshRenderer>();
                    if (mf2 == null) mf2 = cur.gameObject.AddComponent<MeshFilter>(); if (r2 == null) r2 = cur.gameObject.AddComponent<MeshRenderer>();
                    mf2.sharedMesh = mf.sharedMesh; r2.sharedMaterials = r.sharedMaterials; r2.enabled = true; cur.gameObject.SetActive(true);
                    r2.shadowCastingMode = r.shadowCastingMode;
                    var mc = cur.GetComponent<MeshCollider>(); if (mc == null) mc = cur.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; mc.enabled = true;
                    restored++;
                }
                else
                {
                    for (; level < chain.Count; level++)
                    {
                        var src = chain[level];
                        GameObject g;
                        if (level == chain.Count - 1) { g = UnityEngine.Object.Instantiate(src.gameObject); foreach (Transform ch in g.transform.Cast<Transform>().ToList()) UnityEngine.Object.DestroyImmediate(ch.gameObject); }
                        else g = new GameObject();
                        g.name = src.name; g.transform.SetParent(cur, false);
                        g.transform.position = src.position - hp; g.transform.rotation = src.rotation;
                        var ls = src.lossyScale; var ps = cur.lossyScale; g.transform.localScale = new Vector3(ls.x / ps.x, ls.y / ps.y, ls.z / ps.z);
                        g.SetActive(true);
                        cur = g.transform;
                    }
                    var mc = cur.GetComponent<MeshCollider>(); if (mc != null) { mc.sharedMesh = mf.sharedMesh; mc.enabled = true; }
                    recreated++;
                }
            }
            PrefabUtility.SaveAsPrefabAsset(rt, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(rt); PalaceBakeTool.CloseComplex(); }
        return "repaired restored=" + restored + " recreated=" + recreated;
    }

    public static string Info() { return S == null ? "none" : S.log + " rotated: " + string.Join(" | ", S.rotated.Take(40)); }

    public static string End()
    {
        if (S == null) return "none";
        if (S.vis != null) S.vis.Release();
        if (S.rt != null) S.rt.Release();
        if (S.scene.IsValid()) EditorSceneManager.CloseScene(S.scene, true);
        S = null; return "ended";
    }
}
