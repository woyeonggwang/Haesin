using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 부스터로 들이받기(2026-10-06 사용자 지시 "부스터 상태로 배를 박으면 배 데미지 추가"). 플레이어 배(panok)에 붙인다.
/// 부스트 게이지(MoveControl.boostLevel)가 minBoostLevel 이상일 때 다른 배(ShipHealth)와 부딪히면
/// 피해 = damagePerMps × 부딪힌 상대 속도(m/s, Collision.relativeVelocity) × boostLevel 을 그 배에 준다(공격자 = 이 배 진영 → 우호도 규칙 그대로).
/// 같은 배는 cooldown 초에 한 번만. 내 배는 피해를 받지 않는다(요청에 없어서).
/// </summary>
[RequireComponent(typeof(MoveControl))]
public class RamDamage : MonoBehaviour
{
    [Tooltip("부스트 게이지가 이 값 이상일 때만.")] [Range(0f, 1f)] public float minBoostLevel = 0.2f;
    [Tooltip("부딪힌 상대 속도가 이 값(m/s) 이상일 때만.")] public float minImpactSpeed = 5f;
    [Tooltip("피해 = 이 값 × 상대 속도 × 부스트 게이지.")] public float damagePerMps = 1.2f;
    [Tooltip("같은 배에 다시 피해를 주기까지(초).")] public float cooldown = 1.5f;

    [Header("읽기 전용")]
    public float lastDamage; public string lastTarget;

    MoveControl _mc; ShipFaction _self;
    readonly Dictionary<ShipHealth, float> _next = new Dictionary<ShipHealth, float>();

    void Awake() { _mc = GetComponent<MoveControl>(); _self = GetComponent<ShipFaction>(); }

    void OnCollisionEnter(Collision c) { TryRam(c); }
    void OnCollisionStay(Collision c) { TryRam(c); }

    void TryRam(Collision c)
    {
        if (_mc == null || _mc.boostLevel < minBoostLevel || c.rigidbody == null) return;
        var target = c.rigidbody.GetComponentInParent<ShipHealth>();
        if (target == null || target.transform == transform || target.isDead || target.isSinking) return;
        float speed = c.relativeVelocity.magnitude;
        if (speed < minImpactSpeed) return;
        float t;
        if (_next.TryGetValue(target, out t) && Time.time < t) return;
        _next[target] = Time.time + cooldown;
        float dmg = damagePerMps * speed * _mc.boostLevel;
        Vector3 at = c.contactCount > 0 ? c.GetContact(0).point : target.transform.position;
        target.TakeDamage(dmg, at, _self != null ? _self.faction : Faction.Pirate);
        lastDamage = dmg; lastTarget = target.name;
    }
}
