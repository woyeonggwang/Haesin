using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 퀘스트 장소 표시(2026-10-07). Reach 목표가 markerId 로 찾는다(예: 해월도_선착장, 구로세지마_선착장).
/// 이 오브젝트 위치 둘레 radius(m) 안(수평 거리)에 플레이어 배 또는 걷는 캐릭터가 들어가면 도착.
/// </summary>
public class QuestMarker : MonoBehaviour
{
    public string markerId = "";
    [Tooltip("지도·추적 UI 에 쓰는 이름.")] public string displayName = "";
    [Tooltip("도착 반경(m, 수평).")] public float radius = 60f;

    public static readonly List<QuestMarker> All = new List<QuestMarker>();

    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() { All.Remove(this); }

    public static QuestMarker Find(string id)
    {
        for (int i = 0; i < All.Count; i++) if (All[i] != null && All[i].markerId == id) return All[i];
        return null;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.82f, 0.25f, 0.8f);
        Vector3 c = transform.position, prev = c + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= 48; i++) { float a = i / 48f * Mathf.PI * 2f; Vector3 n = c + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius); Gizmos.DrawLine(prev, n); prev = n; }
    }
}
