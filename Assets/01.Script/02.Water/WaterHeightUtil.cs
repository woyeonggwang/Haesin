using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// HDRP WaterSurface 의 수면 높이/법선/해류를 질의하는 공용 헬퍼.
/// HDRP Asset 의 Water > Script Interactions 가 켜져 있어야 동작한다.
/// </summary>
public static class WaterHeightUtil
{
    private static WaterSurface _cached;

    /// <summary>씬에서 첫 번째 WaterSurface 를 찾아 캐시한다.</summary>
    public static WaterSurface FindSurface()
    {
        if (_cached != null) return _cached;
        _cached = Object.FindFirstObjectByType<WaterSurface>();
        return _cached;
    }

    /// <summary>
    /// worldPos 바로 아래(또는 위)의 수면 지점을 구한다.
    /// 실패하면 surface 의 기준 높이를 그대로 돌려준다.
    /// </summary>
    public static bool Sample(WaterSurface surface, Vector3 worldPos,
                              out Vector3 surfacePos, out Vector3 normal, out Vector3 current)
    {
        surfacePos = new Vector3(worldPos.x, surface != null ? surface.transform.position.y : 0f, worldPos.z);
        normal = Vector3.up;
        current = Vector3.zero;
        if (surface == null) return false;

        WaterSearchParameters sp = new WaterSearchParameters();
        WaterSearchResult sr = new WaterSearchResult();

        sp.startPositionWS = sr.candidateLocationWS;
        sp.targetPositionWS = worldPos;
        sp.error = 0.01f;
        sp.maxIterations = 8;
        sp.includeDeformation = true;   // 충돌 파문(디포머)까지 높이에 반영
        sp.outputNormal = true;

        if (surface.ProjectPointOnWaterSurface(sp, out sr))
        {
            surfacePos = sr.projectedPositionWS;
            normal = sr.normalWS;
            current = sr.currentDirectionWS;
            return true;
        }
        return false;
    }

    /// <summary>해당 위치의 수면 높이(y)만 필요할 때.</summary>
    public static float SampleHeight(WaterSurface surface, Vector3 worldPos)
    {
        Vector3 p, n, c;
        Sample(surface, worldPos, out p, out n, out c);
        return p.y;
    }
}
