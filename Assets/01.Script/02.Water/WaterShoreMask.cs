using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// 육지 가까운 바다의 파도를 줄이는 HDRP 워터 마스크를 굽는다(Ocean 에 붙임).
///
/// 폭풍우(바람 140)에서 먼바다 너울은 ±20 m 에 이르러 선착장(판자 y 4.7)과 해안 마을이 잠긴다.
/// WaterSurface.waterMask(R = 너울, G = 잔물결 agitation, B = 리플)는 각 주파수 대역의 변위에 곱해지고,
/// CPU 시뮬레이션(ProjectPointOnWaterSurface — 배 부력·발 높이)에도 같이 반영된다.
/// 전역 설정 "Water Decal Mask and Current" 가 꺼져 있어야 이 텍스처 방식이 쓰인다(켜면 데칼 방식으로 바뀜).
///
/// 육지 = 위에서 아래로 쏜 레이가 처음 맞는 고정 콜라이더(리지드바디 없는 것 — 터레인·동굴 바위·선착장 등)의 높이가
/// landHeight 이상인 곳. 동굴 지붕 밑 물도 '덮인 곳'이라 육지로 친다.
/// 육지에서 멀어질수록 fadeDistance 에 걸쳐 1(원래 파도)로 돌아간다. 터레인·마을을 바꾸면 컨텍스트 메뉴로 다시 굽는다.
/// </summary>
public class WaterShoreMask : MonoBehaviour
{
    public WaterSurface ocean;

    [Header("범위")]
    [Tooltip("마스크 중심(월드 XZ).")] public Vector2 center = Vector2.zero;
    [Tooltip("마스크 한 변 길이(m). 밖은 원래 파도. 육지 + fadeDistance 를 덮어야 함.")] public float size = 1000f;
    [Tooltip("텍스처 해상도.")] public int resolution = 1024;

    [Header("육지 판정")]
    [Tooltip("이 높이 이상에서 콜라이더를 맞으면 육지(m). 해수면 0.")] public float landHeight = -0.5f;
    [Tooltip("레이를 쏘는 높이(m).")] public float castHeight = 1000f;
    public LayerMask landMask = ~((1 << 2) | (1 << 4) | (1 << 5) | (1 << 8));

    [Header("세기 (1 = 원래 파도)")]
    [Tooltip("육지에서 이 거리(m)만큼 멀어지면 원래 파도.")] public float fadeDistance = 200f;
    [Range(0f, 1f)] public float swellNearShore = 0.1f;
    [Range(0f, 1f)] public float agitationNearShore = 0.2f;
    [Range(0f, 1f)] public float ripplesNearShore = 0.6f;

    /// <summary>
    /// 파도를 거의 없애는 구역(호수 등). 다각형(월드 XZ) 안쪽에서 가장자리부터 feather(m) 에 걸쳐 아래 값으로 낮춘다(높이지는 않음).
    /// 육조거리 남쪽 호수는 바다와 서남쪽 입구(폭 약 140 m)로 이어져 있고 너비가 약 200 m 라,
    /// 육지 거리만으로는 가운데가 원래 너울의 절반(폭풍우 ±10 m)이 남아 길(약 10 m) 위로 물이 올라왔음(2026-10-04).
    /// </summary>
    [System.Serializable]
    public class CalmZone
    {
        public string name;
        [Tooltip("구역 꼭짓점(월드 X, Z). 순서대로 잇는다.")] public Vector2[] polygon;
        [Tooltip("가장자리에서 안쪽으로 이 거리(m)에 걸쳐 아래 값으로 바뀐다.")] public float feather = 80f;
        [Range(0f, 1f)] public float swell = 0f;
        [Range(0f, 1f)] public float agitation = 0.1f;
        [Range(0f, 1f)] public float ripples = 0.5f;
    }
    [Header("잔잔한 물 (호수)")]
    public CalmZone[] calmZones;

    [Header("결과")]
    public Texture2D bakedMask;

    public void ApplyToOcean()
    {
        if (ocean == null) ocean = GetComponent<WaterSurface>();
        if (ocean == null) return;
        ocean.waterMask = bakedMask;
        ocean.waterMaskExtent = new Vector2(size, size);
        ocean.waterMaskOffset = center;
        ocean.waterMaskRemap = new Vector2(0f, 1f);
    }

    /// <summary>다각형 안이면 가장자리에서의 거리 / feather (0~1, smoothstep), 밖이면 0.</summary>
    public static float CalmWeight(CalmZone cz, Vector2 p)
    {
        if (cz == null || cz.polygon == null || cz.polygon.Length < 3) return 0f;
        var poly = cz.polygon;
        bool inside = false;
        float best = float.MaxValue;
        for (int a = 0, b = poly.Length - 1; a < poly.Length; b = a++)
        {
            Vector2 pa = poly[a], pb = poly[b];
            if (((pa.y > p.y) != (pb.y > p.y)) && (p.x < (pb.x - pa.x) * (p.y - pa.y) / (pb.y - pa.y) + pa.x)) inside = !inside;
            Vector2 e = pb - pa;
            float tt = Mathf.Clamp01(Vector2.Dot(p - pa, e) / Mathf.Max(1e-6f, e.sqrMagnitude));
            best = Mathf.Min(best, (pa + e * tt - p).magnitude);
        }
        if (!inside) return 0f;
        return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(best / Mathf.Max(1f, cz.feather)));
    }

#if UNITY_EDITOR
    public string assetPath = "Assets/02.Resources/00.Component/02.Water/WaterShoreMask.png";

    [ContextMenu("해안 마스크 굽기")]
    public void Bake()
    {
        int n = Mathf.Clamp(resolution, 64, 4096);
        float cell = size / n;
        Physics.SyncTransforms();

        // 1) 육지 칸
        var land = new bool[n * n];
        var hits = new RaycastHit[16];
        for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                float x = center.x + (i + 0.5f) * cell - size * 0.5f;
                float z = center.y + (j + 0.5f) * cell - size * 0.5f;
                int c = Physics.RaycastNonAlloc(new Vector3(x, castHeight, z), Vector3.down, hits, castHeight + 200f, landMask, QueryTriggerInteraction.Ignore);
                float top = float.NegativeInfinity;
                for (int h = 0; h < c; h++)
                {
                    if (hits[h].collider.attachedRigidbody != null) continue; // 배
                    if (hits[h].point.y > top) top = hits[h].point.y;
                }
                land[j * n + i] = top >= landHeight;
            }

        // 2) 거리(체임퍼 3-4, 두 번 훑기)
        const float INF = 1e9f;
        var d = new float[n * n];
        for (int k = 0; k < d.Length; k++) d[k] = land[k] ? 0f : INF;
        float o = cell, g = cell * 1.41421356f;
        for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                int k = j * n + i; float v = d[k];
                if (i > 0) v = Mathf.Min(v, d[k - 1] + o);
                if (j > 0)
                {
                    v = Mathf.Min(v, d[k - n] + o);
                    if (i > 0) v = Mathf.Min(v, d[k - n - 1] + g);
                    if (i < n - 1) v = Mathf.Min(v, d[k - n + 1] + g);
                }
                d[k] = v;
            }
        for (int j = n - 1; j >= 0; j--)
            for (int i = n - 1; i >= 0; i--)
            {
                int k = j * n + i; float v = d[k];
                if (i < n - 1) v = Mathf.Min(v, d[k + 1] + o);
                if (j < n - 1)
                {
                    v = Mathf.Min(v, d[k + n] + o);
                    if (i < n - 1) v = Mathf.Min(v, d[k + n + 1] + g);
                    if (i > 0) v = Mathf.Min(v, d[k + n - 1] + g);
                }
                d[k] = v;
            }

        // 3) 색
        var px = new Color32[n * n];
        for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                int k = j * n + i;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(d[k] / Mathf.Max(1f, fadeDistance)));
                bool edge = i == 0 || j == 0 || i == n - 1 || j == n - 1; // 밖(Clamp)은 원래 파도
                if (edge) t = 1f;
                float vr = Mathf.Lerp(swellNearShore, 1f, t), vg = Mathf.Lerp(agitationNearShore, 1f, t), vb = Mathf.Lerp(ripplesNearShore, 1f, t);
                if (calmZones != null && !edge)
                {
                    float x = center.x + (i + 0.5f) * cell - size * 0.5f;
                    float z = center.y + (j + 0.5f) * cell - size * 0.5f;
                    for (int zi = 0; zi < calmZones.Length; zi++)
                    {
                        var cz = calmZones[zi];
                        float w = CalmWeight(cz, new Vector2(x, z));
                        if (w <= 0f) continue;
                        vr = Mathf.Min(vr, Mathf.Lerp(vr, cz.swell, w));
                        vg = Mathf.Min(vg, Mathf.Lerp(vg, cz.agitation, w));
                        vb = Mathf.Min(vb, Mathf.Lerp(vb, cz.ripples, w));
                    }
                }
                px[k] = new Color32(
                    (byte)Mathf.RoundToInt(255f * vr),
                    (byte)Mathf.RoundToInt(255f * vg),
                    (byte)Mathf.RoundToInt(255f * vb),
                    255);
            }

        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false, true);
        tex.SetPixels32(px);
        tex.Apply();
        System.IO.File.WriteAllBytes(assetPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        UnityEditor.AssetDatabase.ImportAsset(assetPath);
        var imp = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(assetPath);
        imp.textureType = UnityEditor.TextureImporterType.Default;
        imp.sRGBTexture = false;
        imp.mipmapEnabled = false;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.filterMode = FilterMode.Bilinear;
        imp.textureCompression = UnityEditor.TextureImporterCompression.Uncompressed;
        imp.alphaSource = UnityEditor.TextureImporterAlphaSource.None;
        imp.maxTextureSize = Mathf.Max(n, 32);
        imp.npotScale = UnityEditor.TextureImporterNPOTScale.None;
        imp.SaveAndReimport();
        bakedMask = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);

        ApplyToOcean();
        UnityEditor.EditorUtility.SetDirty(this);
        if (ocean != null) UnityEditor.EditorUtility.SetDirty(ocean);
        int landCount = 0; foreach (var b in land) if (b) landCount++;
        Debug.Log("[WaterShoreMask] 구움: " + n + "x" + n + ", 육지 칸 " + landCount + ", " + assetPath);
    }
#endif
}
