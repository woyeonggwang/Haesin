using UnityEngine;

/// <summary>
/// 플레이어 리스폰 위치 마커. 베이스캠프 선착장(swamp-bridge) 옆 바다에 둔다.
/// 이 오브젝트의 위치·방향(forward = 뱃머리)이 그대로 리스폰 자세가 된다.
/// </summary>
public class RespawnPoint : MonoBehaviour
{
    public static RespawnPoint Instance { get; private set; }

    void OnEnable() { Instance = this; }
    void OnDisable() { if (Instance == this) Instance = null; }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, 2f);
        Gizmos.DrawRay(transform.position, transform.forward * 10f);
    }
}
