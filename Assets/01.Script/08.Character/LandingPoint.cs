using UnityEngine;

/// <summary>하차 지점 마커(선착장·마을). 배가 dockRange 안에 있을 때 E 를 길게 누르면 가장 가까운 이 지점에 내린다.</summary>
public class LandingPoint : MonoBehaviour
{
    [Tooltip("이 지점에 내리려면 배가 이 거리(m) 안에 있어야 한다.")] public float dockRange = 35f;
    void OnDrawGizmos() { Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.8f); Gizmos.DrawWireSphere(transform.position, 1f); Gizmos.DrawWireSphere(transform.position, dockRange); }
}
