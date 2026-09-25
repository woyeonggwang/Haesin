using System.Collections;
using UnityEngine;

/// <summary>
/// 플레이어 배가 완파되면 침몰 연출(ShipHealth.sinkOnDeath) 을 끝까지 보여준 뒤
/// 베이스캠프 선착장(RespawnPoint)으로 옮겨 되살린다.
/// - ShipHealth: diesAtZeroHealth = true, sinkOnDeath = true, destroyAfterSink = false 여야 한다.
/// - 침몰 중에는 ShipFaction.FindNearestHostile 이 이 배를 표적에서 빼므로 적이 공격하지 않는다.
/// - 침몰이 끝나면(OnSunk) 화면 전환 없이 바로 리스폰한다. 화면 전환 연출은 추후 fadeDelay 자리에 넣는다.
/// - 자원 시스템이 생기면 여기서 일부 자원을 잃게 할 예정(아직 없음).
/// BeginSink 는 이 컴포넌트를 끄지 않는다(ShipHealth 가 이름으로 제외).
/// </summary>
[RequireComponent(typeof(ShipHealth))]
public class PlayerRespawn : MonoBehaviour
{
    [Tooltip("침몰이 끝난 뒤 리스폰까지 추가로 기다리는 시간(초). 화면 전환 연출을 넣을 자리. 지금은 0.")]
    public float fadeDelay = 0f;
    [Tooltip("sinkOnDeath 가 꺼져 있을 때(침몰 연출 없음) 완파 후 리스폰까지 기다리는 시간(초).")]
    public float noSinkDelay = 3f;
    [Tooltip("리스폰 위치. 비워두면 씬의 RespawnPoint.Instance 를 쓴다.")]
    public Transform respawnPoint;

    ShipHealth _health;
    Rigidbody _rb;
    bool _respawning;

    void Awake()
    {
        _health = GetComponent<ShipHealth>();
        _rb = GetComponent<Rigidbody>();
    }

    void OnEnable() { _health.OnDied += OnDied; _health.OnSunk += OnSunk; }
    void OnDisable() { _health.OnDied -= OnDied; _health.OnSunk -= OnSunk; }

    void OnDied()
    {
        if (_respawning) return;
        // 침몰 연출이 있으면 OnSunk 를 기다리고, 없으면 잠시 뒤 바로 리스폰.
        if (!_health.sinkOnDeath) StartCoroutine(RespawnRoutine(noSinkDelay));
    }

    void OnSunk()
    {
        if (_respawning) return;
        StartCoroutine(RespawnRoutine(fadeDelay));
    }

    IEnumerator RespawnRoutine(float delay)
    {
        _respawning = true;
        if (delay > 0f) yield return new WaitForSeconds(delay);

        Transform p = respawnPoint != null ? respawnPoint : (RespawnPoint.Instance != null ? RespawnPoint.Instance.transform : null);
        Vector3 pos = p != null ? p.position : transform.position;
        Quaternion rot = p != null ? Quaternion.Euler(0f, p.eulerAngles.y, 0f) : Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        if (p == null) Debug.LogWarning("[PlayerRespawn] RespawnPoint 가 없어 제자리에서 되살립니다.");

        // 먼저 되살려(침몰로 꺼진 것 복구) 놓고 자세를 잡는다. Rigidbody 가 kinematic 에서 풀린 뒤 position 을 넣어야 한다.
        _health.Revive();
        if (_rb != null)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.position = pos;
            _rb.rotation = rot;
        }
        transform.SetPositionAndRotation(pos, rot);

        _respawning = false;
    }
}
