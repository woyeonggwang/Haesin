using UnityEngine;

/// <summary>
/// 퀘스트 하나(2026-10-07, 2026-10-09 2막~에필로그용으로 넓힘). 에셋 하나 = 퀘스트 하나, `02.Resources/00.Component/09.Quest/` 아래.
/// 1막 데이터는 Editor/QuestAct1Builder, 2막~에필로그는 Editor/QuestStoryBuilder 가 만든다.
/// 흐름: 시작 → onStart 동작 → 시작 대사(introDialogue, 걸으면서 흘러감) → 목표(objectives)를 순서대로 하나씩(목표마다 onBegin·onEnd 동작)
///       → onComplete 동작 → 끝 대사 → 다음 퀘스트.
/// 분기: branchFlag 깃발이 켜져 있으면 next 대신 branchNext 로 간다(깃발은 대화 선택지·SetFlag 동작으로 켬).
/// </summary>
[CreateAssetMenu(menuName = "HaeSin/퀘스트", fileName = "Quest")]
public class QuestData : ScriptableObject
{
    [Tooltip("퀘스트 번호(스토리 표 기준, 예: P-1, 1-3).")] public string questId = "P-1";
    [Tooltip("막 이름(예: 프롤로그, 1막).")] public string act = "1막";
    public string title = "퀘스트";
    [Tooltip("의뢰인(표시용).")] public string giver = "";
    [TextArea(2, 5)] public string summary = "";

    [Header("시작")]
    public QuestAction[] onStart = new QuestAction[0];
    [Tooltip("시작할 때 화면 아래로 흘러가는 대사(멈추지 않음).")] public DialogueLine[] introDialogue = new DialogueLine[0];

    [Header("목표 (순서대로)")]
    public QuestObjective[] objectives = new QuestObjective[0];

    [Header("끝")]
    public QuestAction[] onComplete = new QuestAction[0];
    [Tooltip("끝날 때 흘러가는 대사(멈추지 않음).")] public DialogueLine[] outroDialogue = new DialogueLine[0];

    [Header("다음")]
    public QuestData next;
    [Tooltip("이 깃발이 켜져 있으면 next 대신 branchNext 로(분기).")] public string branchFlag = "";
    public QuestData branchNext;
}

/// <summary>대사 한 줄.</summary>
[System.Serializable]
public class DialogueLine
{
    public string speaker = "";
    [TextArea(2, 4)] public string text = "";
    public DialogueLine() { }
    public DialogueLine(string s, string t) { speaker = s; text = t; }
}

public enum QuestObjectiveType
{
    /// <summary>NPC(QuestNPC.npcId = targetId)에게 걸어가 F 로 대화.</summary>
    Talk,
    /// <summary>faction 진영 배를 플레이어가 count 척 격침(마지막 공격이 플레이어). spawnTemplates 가 있으면 늘 keepAlive 척이 둘레에 있게 띄움.</summary>
    SinkShips,
    /// <summary>QuestMarker(markerId = targetId) 의 radius(m) 안에 플레이어(배 또는 걷는 캐릭터)가 들어감.</summary>
    Reach,
    /// <summary>faction 현상금 단계 ≥ count.</summary>
    BountyAtLeast,
    /// <summary>faction 현상금 단계 ≤ count.</summary>
    BountyAtMost,
    /// <summary>호위(2026-10-09): escortTemplate 배를 marker 자리에 띄워 targetId 마커까지 가게 하고 지킴. 그 배가 가라앉으면 목표를 처음부터.
    /// spawnTemplates 가 있으면 호위 배 둘레 spawnDistance 에 습격 배를 count 척까지 keepAlive 척씩 띄움.</summary>
    Escort,
    /// <summary>미행(2026-10-09): QuestTag(targetId) 의 QuestWalker 를 따라감. 플레이어가 followStartRange 안에 오면 출발,
    /// radius 보다 멀어지면 놓침, minDistance 보다 가까우면 들킴 → 처음 자리에서 다시. 끝에 닿으면 dialogue 를 자막으로.</summary>
    Follow,
    /// <summary>제압(2026-10-09): QuestTag(targetId) 가 붙은 병사(SoldierAI)가 모두 쓰러짐.</summary>
    Defeat
}

/// <summary>퀘스트 목표 하나.</summary>
[System.Serializable]
public class QuestObjective
{
    public QuestObjectiveType type = QuestObjectiveType.Talk;
    [Tooltip("추적 UI 에 보이는 글.")] public string description = "";
    [Tooltip("Talk = NPC id, Reach·Escort = 마커 id(목적지), Follow·Defeat = QuestTag id.")] public string targetId = "";
    [Tooltip("SinkShips·Bounty 의 진영.")] public Faction faction = Faction.Wokou;
    [Tooltip("SinkShips = 격침 수, Bounty = 단계, Escort = 습격 배 수.")] public int count = 1;
    [Tooltip("Reach 반경(m, 0 이면 마커 반경). Follow = 놓치는 거리.")] public float radius = 0f;

    [Header("목표 시작·끝 동작")]
    public QuestAction[] onBegin = new QuestAction[0];
    public QuestAction[] onEnd = new QuestAction[0];

    [Header("Talk / Follow")]
    [Tooltip("Talk = 대화, Follow = 끝에 엿듣는 대사(자막).")] public DialogueLine[] dialogue = new DialogueLine[0];
    [Tooltip("대사 끝에 고르는 선택지(비우면 없음). 고르면 같은 순번의 choiceFlags 깃발이 켜짐.")] public string[] choices = new string[0];
    public string[] choiceFlags = new string[0];

    [Header("SinkShips·Escort — 배 띄우기")]
    [Tooltip("씬의 원본 배 이름들(돌아가며 씀, 예: seki_wokou, anteck_wokou). 비우면 띄우지 않고 돌아다니는 배만 셈.")] public string[] spawnTemplates = new string[0];
    [Tooltip("이 목표가 띄운 배 중 살아 있는 배가 이 수보다 적으면 더 띄움(남은 격침 수까지만).")] public int keepAlive = 2;
    [Tooltip("띄울 거리(m) — 플레이어(또는 marker·호위 배) 둘레.")] public float spawnDistance = 380f;
    [Tooltip("SinkShips: 비우지 않으면 플레이어 대신 이 마커 둘레에 띄움. Escort: 호위 배가 생기는 자리.")] public string marker = "";
    [Tooltip("띄운 배가 섬 영해 규칙을 무시(남의 영해 안에서도 싸움).")] public bool ignoreTerritory = true;

    [Header("Escort")]
    public string escortTemplate = "merchant";
    public Faction escortFaction = Faction.Japanese;
    public float escortSpeed = 4.5f;

    [Header("Follow")]
    public float followStartRange = 30f;
    public float minDistance = 5f;
}

public enum QuestActionType
{
    /// <summary>화면 가운데 알림(text).</summary>
    Message,
    /// <summary>faction 현상금을 value 단계로(text 가 "atleast" 면 그보다 낮을 때만 올림).</summary>
    SetBountyLevel,
    /// <summary>원본 배(template)를 플레이어(또는 marker) 둘레 distance 에 value 척. factionOverride ≥ 0 이면 진영을 바꿈.</summary>
    SpawnShips,
    /// <summary>깃발(text) 켜기.</summary>
    SetFlag,
    /// <summary>금화 value 더하기.</summary>
    GiveGold,
    /// <summary>QuestTag(text) 오브젝트를 켜기(value 1)·끄기(value 0).</summary>
    SetTagActive,
    /// <summary>걷는 플레이어를 마커(text) 자리로 옮김(화면을 잠깐 어둡게). 궁궐 안이면 궁을 켬.</summary>
    TeleportPlayer,
    /// <summary>QuestTag(text) 병사가 distance(m) 안에 들어온 걷는 플레이어에게 바로 덤빔(value 1 이면 지금 바로 덤빔).</summary>
    SoldierAggro,
    /// <summary>lines 를 자막으로 흘림.</summary>
    Subtitles
}

/// <summary>퀘스트·목표 시작·끝에 하는 일 하나.</summary>
[System.Serializable]
public class QuestAction
{
    public QuestActionType type = QuestActionType.Message;
    public string text = "";
    public Faction faction = Faction.Navy;
    public int value = 0;
    public string template = "";
    public float distance = 400f;
    [Tooltip("SpawnShips: 플레이어 대신 이 마커 둘레.")] public string marker = "";
    [Tooltip("SpawnShips: 섬 영해 규칙 무시.")] public bool ignoreTerritory;
    [Tooltip("SpawnShips: -1 = 원본 진영 그대로, 0~3 = Faction 값.")] public int factionOverride = -1;
    [Tooltip("Subtitles 대사.")] public DialogueLine[] lines = new DialogueLine[0];
}
