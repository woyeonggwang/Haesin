using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 퀘스트가 켜고 끄거나 가리키는 씬 오브젝트 표(2026-10-09). id 가 같은 것이 여럿이어도 됨(예: 불 여러 개, 호위 병사 무리).
/// QuestManager 의 동작 SetTagActive(켜기·끄기), SoldierAggro(병사가 보자마자 덤빔), 목표 Defeat(그 id 의 병사가 모두 쓰러짐)·Follow(미행 대상 QuestWalker)가 씀.
/// 꺼진 오브젝트도 찾음(FindObjectsInactive.Include) — 그래서 처음엔 꺼 둔 NPC·병사·불도 퀘스트가 켤 수 있다.
/// startHidden: 게임 시작 때 끔(QuestManager.Start 가 한 번 — 병사면 SoldierAI.questHidden 도 켬). 씬에서는 켜 둔 채 배치·확인하기 쉽게.
/// </summary>
public class QuestTag : MonoBehaviour
{
    public string id = "";
    [Tooltip("Play 를 시작할 때 꺼 둠(퀘스트가 켤 때까지).")] public bool startHidden;

    /// <summary>켜기·끄기. 병사면 무리가 거리로 다시 켜지 않게 questHidden 도 같이.</summary>
    public void SetShown(bool on)
    {
        var ai = GetComponent<SoldierAI>();
        if (ai != null) ai.questHidden = !on;
        gameObject.SetActive(on);
    }

    public static List<QuestTag> FindAll(string id)
    {
        var list = new List<QuestTag>();
        if (string.IsNullOrEmpty(id)) return list;
        foreach (var t in Object.FindObjectsByType<QuestTag>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t != null && t.id == id && t.gameObject.scene.IsValid()) list.Add(t);
        return list;
    }
}
