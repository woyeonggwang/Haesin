using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 병사 무리(관군 육조거리·광화문, 일본성, 일본 마을 등 — 2026-10-06). 자식의 SoldierAI 를 모아
///   - 보이기: 0.25 s 마다 카메라(Camera.main)와 병사 거리가 showDistance 밖이면 오브젝트를 끄고(덤비는 병사는 늘 켬), 안으로 들어오면 켬.
///   - 알리기: Alert(자리) — 그 자리에서 alertRadius 안의 살아 있는 이 무리 병사가 플레이어에게 덤빔.
///     플레이어가 마을 사람을 쓰러뜨리면(Villager.OnKilledByPlayer) 그 자리에서 villagerAlertRadius 안 병사가 덤빔.
///   - 되살리기: 카메라가 무리의 모든 병사 제자리에서 respawnDistance 밖이면 쓰러진 병사를 제자리에 되살림.
/// </summary>
public class SoldierGroup : MonoBehaviour
{
    [Tooltip("무리 이름(진영).")] public string factionName = "관군";
    [Tooltip("카메라에서 이 거리(m) 밖 병사는 끔.")] public float showDistance = 160f;
    public float showHysteresis = 10f;
    [Tooltip("공격받은 병사 둘레 이 거리(m) 안 같은 무리 병사가 덤빔.")] public float alertRadius = 30f;
    [Tooltip("마을 사람이 쓰러진 자리 둘레 이 거리(m) 안 병사가 덤빔.")] public float villagerAlertRadius = 40f;
    [Tooltip("카메라가 모든 병사 제자리에서 이 거리(m) 밖이면 쓰러진 병사를 되살림.")] public float respawnDistance = 300f;

    public readonly List<SoldierAI> members = new List<SoldierAI>();
    float _next;

    void Awake() { Collect(); }

    public void Collect()
    {
        members.Clear();
        GetComponentsInChildren(true, members);
        foreach (var m in members) m.group = this;
    }

    void OnEnable() { Villager.OnKilledByPlayer += OnVillagerKilled; }
    void OnDisable() { Villager.OnKilledByPlayer -= OnVillagerKilled; }

    void OnVillagerKilled(Vector3 pos) { Alert(pos, villagerAlertRadius); }

    public void Alert(Vector3 pos) { Alert(pos, alertRadius); }

    public void Alert(Vector3 pos, float radius)
    {
        float r2 = radius * radius;
        foreach (var m in members)
        {
            if (m == null || m.IsDead || m.IsHostile || m.questHidden) continue;
            Vector3 d = m.transform.position - pos; d.y = 0f;
            if (d.sqrMagnitude <= r2) m.BecomeHostile();
        }
    }

    void Update()
    {
        if (Time.time < _next) return;
        _next = Time.time + 0.25f;
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 c = cam.transform.position;
        bool allFar = true;
        float show2 = showDistance * showDistance, hide2 = (showDistance + showHysteresis) * (showDistance + showHysteresis), resp2 = respawnDistance * respawnDistance;
        foreach (var m in members)
        {
            if (m == null) continue;
            if (m.questHidden) { if (m.gameObject.activeSelf) m.gameObject.SetActive(false); continue; }
            float d2 = (m.transform.position - c).sqrMagnitude;
            if ((m.Post - c).sqrMagnitude < resp2) allFar = false;
            bool on = m.gameObject.activeSelf;
            bool want = m.IsHostile || (on ? d2 < hide2 : d2 < show2);
            if (want != on) m.gameObject.SetActive(want);
        }
        if (allFar)
            foreach (var m in members)
                if (m != null && !m.questHidden && !m.questNoRespawn && (m.IsDead || m.mode != SoldierAI.Mode.Calm))
                {
                    if (!m.gameObject.activeSelf) m.gameObject.SetActive(true);   // 꺼진 채로는 Animator·에이전트를 못 다룸 — 다음 확인 때 다시 꺼짐
                    m.Respawn();
                }
    }
}
