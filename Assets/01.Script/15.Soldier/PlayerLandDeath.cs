using System.Collections;
using UnityEngine;

/// <summary>
/// 메인 씬: 걸어 다니다 칼에 쓰러지면 reviveDelay 초 뒤 배(CharacterSeat)에 태워 되살린다(2026-10-06 — 병사와 싸울 수 있게 되면서).
/// 병사들은 플레이어가 쓰러지거나 배에 타면 포기하고 제자리로 돌아간다(SoldierAI).
/// 싸움이 없으면(덤비는 병사가 regenRange 밖, 마지막 피해 뒤 regenDelay 초) 체력이 천천히 찬다(체력 회복 수단이 따로 없어서).
/// 플레이어 캐릭터(SwordCombatant 가 있는 Haesin_Player)에 붙인다.
/// </summary>
[RequireComponent(typeof(SwordCombatant))]
public class PlayerLandDeath : MonoBehaviour
{
    [Tooltip("쓰러진 뒤 배에서 깨어나기까지(초).")] public float reviveDelay = 4f;
    public BoardingSystem boarding;

    SwordCombatant _sc;
    public bool Pending { get; private set; }
    public float ReviveAt { get; private set; }

    [Tooltip("덤비는 병사가 regenRange 안에 없고 마지막 피해 뒤 이 시간(초)이 지나면 체력이 차오름.")] public float regenDelay = 8f;
    [Tooltip("초당 체력 회복.")] public float regenPerSecond = 5f;
    public float regenRange = 25f;
    float _lastHurt = -999f;

    CombatStats _st;
    void Awake() { _sc = GetComponent<SwordCombatant>(); _st = GetComponent<CombatStats>(); if (_st != null) _st.OnDamaged += a => _lastHurt = Time.time; }

    void Update()
    {
        var st = _st;
        if (st == null || st.IsDead || st.health >= st.maxHealth || Time.time - _lastHurt < regenDelay) return;
        float r2 = regenRange * regenRange;
        foreach (var h in SoldierAI.Hostiles)
            if (h != null && !h.IsDead && (h.transform.position - transform.position).sqrMagnitude < r2) return;
        st.health = Mathf.Min(st.maxHealth, st.health + regenPerSecond * Time.deltaTime);
    }
    void OnEnable() { if (_sc != null) _sc.OnDeath += OnDeath; }
    void OnDisable() { if (_sc != null) _sc.OnDeath -= OnDeath; }

    void OnDeath(SwordCombatant who)
    {
        if (Pending) return;
        if (boarding == null) boarding = FindFirstObjectByType<BoardingSystem>();
        if (boarding == null || !boarding.IsOnFoot) return;   // 배 위(또는 시험 씬)에서는 하지 않음 — T 키 부활 그대로
        StartCoroutine(ReviveLater());
    }

    IEnumerator ReviveLater()
    {
        Pending = true;
        ReviveAt = Time.time + reviveDelay;
        yield return new WaitForSeconds(reviveDelay);
        Pending = false;
        if (_sc.state != SwordCombatant.State.Dead) yield break;   // 그 사이 T 로 되살아남
        _sc.Revive();
        var wh = GetComponent<WeaponHolder>();
        if (wh != null && wh.IsDrawn) wh.Sheathe();
        if (boarding != null && boarding.IsOnFoot) boarding.Board();
    }
}
