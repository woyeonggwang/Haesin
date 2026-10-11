using UnityEditor;
using UnityEngine;

/// <summary>
/// 선착장 판자 육지 끝과 땅 높이 맞추기(2026-10-06 사용자 지시 "모든 지역 선착장 땅 높이 맞춰 주기").
/// LandingPoint 의 부모(Dock) 로컬 좌표: 판자는 로컬 x(바다 쪽 +x), 폭 z = pierMinZ~pierMaxZ.
/// 판자 육지 끝(endX — 위에서 쏜 레이가 판자에 처음 맞는 가장 작은 x)과 판자 윗면 높이(top)를 재고,
/// 땅을 (top − 0.08) 에서 육지 쪽으로 slopeDeg 기울기 안에 들도록 깎거나 돋움: 새 높이 = clamp(원래, top−0.08 − d·tan, top−0.08 + d·tan), d = 판자 끝에서 육지 쪽 거리.
/// 폭은 판자 가운데에서 halfWidth 까지 100 %, blend 만큼 부드럽게 0 %. 판자 밑으로 under m 까지 같이(판자 끝 아래 빈틈 없앰).
/// 바꾸기 전 높이는 Captures/terrain_backup/<터레인>_dock_<시각>.bin 에 저장(int x0,y0,w,h + float[h,w]).
/// </summary>
public static class DockRampTool
{
    public static string ApplyAll(float slopeDeg, float halfWidth, float blend, float length, float under)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var lp in Object.FindObjectsByType<LandingPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            sb.AppendLine(Apply(lp, slopeDeg, halfWidth, blend, length, under));
        AssetDatabase.SaveAssets();
        return sb.ToString();
    }

    public static string Apply(LandingPoint lp, float slopeDeg, float halfWidth, float blend, float length, float under)
    {
        var dock = lp.transform.parent;
        if (dock == null) return lp.name + ": 부모(Dock) 없음 — 건너뜀";
        float zc = (lp.pierMinZ + lp.pierMaxZ) * 0.5f;
        float endX = 99f, top = 0f;
        for (float x = -20f; x <= 20f; x += 0.25f)
        {
            var w = dock.TransformPoint(new Vector3(x, 0f, zc)); RaycastHit h;
            if (Physics.Raycast(new Vector3(w.x, dock.position.y + 60f, w.z), Vector3.down, out h, 120f, ~(1 << 2), QueryTriggerInteraction.Ignore)
                && !(h.collider is TerrainCollider) && h.collider.transform.IsChildOf(dock) && x < endX) { endX = x; top = h.point.y; }
        }
        if (endX > 50f) return lp.name + ": 판자를 못 찾음";
        var we = dock.TransformPoint(new Vector3(endX - 2f, 0f, zc));
        Terrain t = null;
        foreach (var tt in Terrain.activeTerrains) { var p = tt.transform.position; var s = tt.terrainData.size; if (we.x >= p.x && we.x <= p.x + s.x && we.z >= p.z && we.z <= p.z + s.z) t = tt; }
        if (t == null) return lp.name + ": 터레인 없음";
        var td = t.terrainData; Vector3 tp = t.transform.position; Vector3 size = td.size; int res = td.heightmapResolution;
        // 영역 모서리 → 높이맵 범위
        float zr = halfWidth + blend;
        Vector3[] cs = { new Vector3(endX - length, 0, zc - zr), new Vector3(endX - length, 0, zc + zr), new Vector3(endX + under, 0, zc - zr), new Vector3(endX + under, 0, zc + zr) };
        float minX = 1e9f, maxX = -1e9f, minZ = 1e9f, maxZ = -1e9f;
        foreach (var c in cs) { var w = dock.TransformPoint(c); minX = Mathf.Min(minX, w.x); maxX = Mathf.Max(maxX, w.x); minZ = Mathf.Min(minZ, w.z); maxZ = Mathf.Max(maxZ, w.z); }
        int x0 = Mathf.Clamp(Mathf.FloorToInt((minX - tp.x) / size.x * (res - 1)), 0, res - 1), x1 = Mathf.Clamp(Mathf.CeilToInt((maxX - tp.x) / size.x * (res - 1)), 0, res - 1);
        int y0 = Mathf.Clamp(Mathf.FloorToInt((minZ - tp.z) / size.z * (res - 1)), 0, res - 1), y1 = Mathf.Clamp(Mathf.CeilToInt((maxZ - tp.z) / size.z * (res - 1)), 0, res - 1);
        int wN = x1 - x0 + 1, hN = y1 - y0 + 1;
        var hs = td.GetHeights(x0, y0, wN, hN);
        // 백업
        string dir = System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, "Captures/terrain_backup");
        System.IO.Directory.CreateDirectory(dir);
        string bk = System.IO.Path.Combine(dir, t.name + "_dock_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".bin");
        using (var bw = new System.IO.BinaryWriter(System.IO.File.Create(bk)))
        { bw.Write(x0); bw.Write(y0); bw.Write(wN); bw.Write(hN); for (int j = 0; j < hN; j++) for (int i = 0; i < wN; i++) bw.Write(hs[j, i]); }
        float tan = Mathf.Tan(slopeDeg * Mathf.Deg2Rad), target = top - 0.08f;
        int changed = 0; float maxDelta = 0f;
        for (int j = 0; j < hN; j++)
            for (int i = 0; i < wN; i++)
            {
                float wx = tp.x + (x0 + i) / (float)(res - 1) * size.x, wz = tp.z + (y0 + j) / (float)(res - 1) * size.z;
                var l = dock.InverseTransformPoint(new Vector3(wx, 0f, wz));
                if (l.x < endX - length || l.x > endX + under) continue;
                float az = Mathf.Abs(l.z - zc); if (az > zr) continue;
                float orig = tp.y + hs[j, i] * size.y;
                float d = Mathf.Max(0f, endX - l.x);
                float cl = Mathf.Clamp(orig, target - d * tan, target + d * tan);
                float k = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(halfWidth, zr, az));
                float nw = Mathf.Lerp(orig, cl, k);
                if (Mathf.Abs(nw - orig) > 0.005f) { changed++; maxDelta = Mathf.Max(maxDelta, Mathf.Abs(nw - orig)); hs[j, i] = Mathf.Clamp01((nw - tp.y) / size.y); }
            }
        Undo.RegisterCompleteObjectUndo(td, "Dock ramp");
        td.SetHeights(x0, y0, hs);
        EditorUtility.SetDirty(td);
        return lp.name + ": 판자 끝 x " + endX + " 높이 " + top.ToString("F2") + " → 터레인 " + t.name + " 칸 " + wN + "×" + hN + " 바뀐 점 " + changed + " 최대 " + maxDelta.ToString("F2") + " m, 백업 " + System.IO.Path.GetFileName(bk);
    }
}
