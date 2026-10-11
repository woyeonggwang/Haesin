using UnityEngine;

/// <summary>하차 지점 마커(선착장·마을). 배가 dockRange 안에 있을 때 E 를 길게 누르면 가장 가까운 이 지점에 내린다.
/// 2026-10-06: 내릴 때 배를 선착장 옆에 붙임(BoardingSystem.PlaceShipAtBerth) — 부모(Dock) 로컬 좌표로 판자 폭·배 댈 자리.</summary>
public class LandingPoint : MonoBehaviour
{
    [Tooltip("이 지점에 내리려면 배가 이 거리(m) 안에 있어야 한다.")] public float dockRange = 35f;
    [Header("배 대기 (부모 Dock 로컬 좌표, 판자는 로컬 x — 바다 쪽 +x)")]
    [Tooltip("내릴 때 배를 선착장 옆에 붙인다(부모가 있을 때만).")] public bool snapShip = true;
    [Tooltip("판자 폭(로컬 z) 한쪽 끝.")] public float pierMinZ = -2.25f;
    [Tooltip("판자 폭(로컬 z) 다른 쪽 끝.")] public float pierMaxZ = 3.5f;
    [Tooltip("배 가운데를 둘 로컬 x(판자 육지 끝 약 −8.75, 바다 끝 11.5).")] public float berthX = 10f;
    [Tooltip("판자 가장자리와 배 옆구리 사이(m).")] public float berthGap = 0.6f;
    [Tooltip("판자 육지 끝 로컬 x. 배 앞뒤 끝이 이보다 bowClearance 만큼 바다 쪽에 오게 berthX 를 늘림(얕은 해변에 걸려 밀려나지 않게).")] public float pierLandEndX = -8.75f;
    [Tooltip("배 끝과 판자 육지 끝 사이 여유(m).")] public float bowClearance = 4f;
    [Tooltip("배 밑 땅이 이 높이(m, 월드 y)보다 낮아야 댈 수 있음 — 얕은 곳이면 바다 쪽으로 1 m 씩 옮겨 찾음.")] public float maxSeabedY = -2.5f;
    [Tooltip("판자 바다 끝 로컬 x.")] public float pierSeaEndX = 11.5f;
    [Tooltip("true = 판자 바다 끝에 배 옆구리 가운데를 대고 판자와 직각(T 자, 2026-10-07 사용자 \"밀착되는 부분은 배의 측면\") / false = 판자와 나란히 옆에.")] public bool berthAtPierHead = true;
    void OnDrawGizmos() { Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.8f); Gizmos.DrawWireSphere(transform.position, 1f); Gizmos.DrawWireSphere(transform.position, dockRange); }
}
