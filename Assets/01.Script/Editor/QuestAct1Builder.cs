using UnityEditor;
using UnityEngine;

/// <summary>
/// 1막 퀘스트 데이터 만들기(2026-10-07, 스토리 구조 스프레드시트 `해신_스토리_구조.xlsx` 의 프롤로그 P-1 ~ 1-4). 2막~에필로그는 QuestStoryBuilder(이 Build 도 부름).
/// MCP 에서 QuestAct1Builder.Build() 를 부름 — 에셋 `02.Resources/00.Component/09.Quest/Act1/Q_*.asset` 을 만들거나 덮어씀(같은 경로면 GUID 유지).
/// 대사·목표를 고치려면 에셋을 인스펙터에서 직접 고치거나 이 파일을 고쳐 다시 Build(덮어씀 — 인스펙터에서 고친 것은 사라짐).
/// 장소·인물 id: 해월도_선착장 / 구로세지마_선착장(QuestMarker), suk_bu(숙부) / hyeongam(현감 조명구)(QuestNPC).
/// </summary>
public static class QuestAct1Builder
{
    public const string Dir = "Assets/02.Resources/00.Component/09.Quest/Act1";

    static DialogueLine L(string s, string t) { return new DialogueLine(s, t); }

    public static string Build()
    {
        if (!AssetDatabase.IsValidFolder("Assets/02.Resources/00.Component/09.Quest")) AssetDatabase.CreateFolder("Assets/02.Resources/00.Component", "09.Quest");
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/02.Resources/00.Component/09.Quest", "Act1");

        var p1 = Get("Q_P1_WokouRaid");
        var p2 = Get("Q_P2_NavyChase");
        var q11 = Get("Q_1-1_Uncle");
        var q12 = Get("Q_1-2_Magistrate");
        var q13 = Get("Q_1-3_WokouHunt");
        var q14 = Get("Q_1-4_KuroseInfiltration");

        // ---------------- P-1 왜구 습격 ----------------
        p1.questId = "P-1"; p1.act = "프롤로그"; p1.title = "왜구 습격"; p1.giver = "";
        p1.summary = "바다에서 왜구 배의 습격을 받는다. 해상 전투 튜토리얼.";
        p1.onStart = new QuestAction[0];
        p1.introDialogue = new[]
        {
            L("선원", "아씨, 왜구 배입니다! 이쪽으로 곧장 옵니다!"),
            L("혜", "피할 수 없다면 맞선다. 포를 준비하라."),
            L("안내", "마우스 오른쪽 버튼으로 겨누고 왼쪽 버튼으로 쏩니다. 배 옆구리(현측)가 적을 향해야 포가 나갑니다."),
        };
        p1.objectives = new[]
        {
            new QuestObjective { type = QuestObjectiveType.SinkShips, description = "습격해 온 왜구 배 격침", faction = Faction.Wokou, count = 2,
                spawnTemplates = new[] { "seki_wokou" }, keepAlive = 2, spawnDistance = 380f },
        };
        p1.onComplete = new QuestAction[0];
        p1.outroDialogue = new[]
        {
            L("선원", "왜구를 물리쳤습니다! …아씨, 저기 판옥선이 다가옵니다. 관군입니다!"),
        };
        p1.next = p2;

        // ---------------- P-2 관군의 오인 추격 ----------------
        p2.questId = "P-2"; p2.act = "프롤로그"; p2.title = "관군의 오인 추격"; p2.giver = "";
        p2.summary = "왜구와 싸운 혜의 배를 관군이 해적선으로 오인해 추격한다. 현상금 튜토리얼.";
        p2.onStart = new[]
        {
            new QuestAction { type = QuestActionType.SetBountyLevel, faction = Faction.Navy, value = 1 },
        };
        p2.introDialogue = new[]
        {
            L("관군", "거기 해적선은 돛을 내리고 멈춰라!"),
            L("혜", "해적이라니… 왜구와 한패로 보는구나."),
            L("안내", "관군 현상금이 생겼습니다. 화면 아래 투구 아이콘이 현상금 단계이며, 1단계부터 그 진영이 공격합니다."),
        };
        p2.objectives = new[]
        {
            new QuestObjective { type = QuestObjectiveType.SinkShips, description = "추격해 오는 관군 판옥선 격침", faction = Faction.Navy, count = 2,
                spawnTemplates = new[] { "panok_navy" }, keepAlive = 2, spawnDistance = 420f },
        };
        p2.onComplete = new[]
        {
            new QuestAction { type = QuestActionType.SetBountyLevel, faction = Faction.Navy, value = 0 },
            new QuestAction { type = QuestActionType.Message, text = "관군의 추격이 멎었습니다 — 해월도로 가십시오" },
        };
        p2.outroDialogue = new[]
        {
            L("혜", "…숙부님께 가서 이 일을 말씀드려야겠다. 해월도로 가자."),
        };
        p2.next = q11;

        // ---------------- 1-1 숙부와의 재회 ----------------
        q11.questId = "1-1"; q11.act = "1막"; q11.title = "숙부와의 재회"; q11.giver = "숙부";
        q11.summary = "해월도에서 숙부라 부르며 따르던 사람을 만난다(실은 현왕 — 혜는 모른다).";
        q11.onStart = new QuestAction[0];
        q11.introDialogue = new DialogueLine[0];
        q11.objectives = new[]
        {
            new QuestObjective { type = QuestObjectiveType.Reach, description = "해월도 선착장으로 가기", targetId = "해월도_선착장", radius = 0f },
            new QuestObjective { type = QuestObjectiveType.Talk, description = "배에서 내려 숙부와 이야기하기 (E 3초 하차, F 대화)", targetId = "suk_bu",
                dialogue = new[]
                {
                    L("숙부", "혜야, 무사했구나. 관군과 부딪쳤다는 소식은 들었다. 걱정 마라, 그쪽에는 내가 말을 넣어 두었다."),
                    L("혜", "숙부님. 왜구가 갈수록 기승입니다. 지나는 섬마다 불탄 배가 떠다녀요."),
                    L("숙부", "그래서 너를 불렀다. 이 섬 현감이 왜구 일로 골치를 썩고 있지. 네 솜씨라면 도움이 될 게다."),
                    L("숙부", "가서 내 이름을 대거라. …그리고 혜야, 어디서든 네 이름을 함부로 밝히지는 마라."),
                    L("혜", "늘 하시는 말씀이시네요. 알겠습니다, 숙부님."),
                } },
        };
        q11.onComplete = new QuestAction[0];
        q11.outroDialogue = new DialogueLine[0];
        q11.next = q12;

        // ---------------- 1-2 현감 ----------------
        q12.questId = "1-2"; q12.act = "1막"; q12.title = "해월도 현감"; q12.giver = "현감 조명구";
        q12.summary = "숙부의 소개로 해월도 현감 조명구를 만나 왜구 토벌을 의뢰받는다(현감은 대비파 — 혜는 모른다).";
        q12.onStart = new QuestAction[0];
        q12.introDialogue = new DialogueLine[0];
        q12.objectives = new[]
        {
            new QuestObjective { type = QuestObjectiveType.Talk, description = "관아로 가서 현감 조명구 만나기", targetId = "hyeongam",
                dialogue = new[]
                {
                    L("현감 조명구", "숙부 되시는 분께 이야기는 들었소. 배를 몰고 왜구와 싸웠다지?"),
                    L("혜", "예. 무엇을 도와드리면 되겠습니까."),
                    L("현감 조명구", "요즘 왜구가 해월도 앞바다를 제집처럼 드나드오. 관군 배로는 감당이 안 되니, 그대가 왜구 배 열 척만 바다에 가라앉혀 주시오."),
                    L("현감 조명구", "값은 후히 치르리다."),
                } },
        };
        q12.onComplete = new QuestAction[0];
        q12.outroDialogue = new DialogueLine[0];
        q12.next = q13;

        // ---------------- 1-3 왜구 토벌 ----------------
        q13.questId = "1-3"; q13.act = "1막"; q13.title = "왜구 토벌"; q13.giver = "현감 조명구";
        q13.summary = "왜구 배 10척을 격침하고 현감에게 보고한다. 현감은 근거지로 일본섬 구로세지마를 지목한다.";
        q13.onStart = new QuestAction[0];
        q13.introDialogue = new[]
        {
            L("혜", "열 척이라… 바다로 나가 보자."),
        };
        q13.objectives = new[]
        {
            new QuestObjective { type = QuestObjectiveType.SinkShips, description = "왜구 배 격침", faction = Faction.Wokou, count = 10,
                spawnTemplates = new[] { "seki_wokou", "seki_wokou", "anteck_wokou" }, keepAlive = 3, spawnDistance = 400f },
            new QuestObjective { type = QuestObjectiveType.Talk, description = "해월도 현감에게 보고하기", targetId = "hyeongam",
                dialogue = new[]
                {
                    L("현감 조명구", "열 척이라… 듣던 것보다 솜씨가 좋구려."),
                    L("현감 조명구", "헌데 놈들 근거지가 따로 있소. 일본섬 구로세지마. 왜구가 그 섬 영주의 비호를 받는다는 말이 돌고 있소."),
                    L("혜", "그 섬으로 가라는 말씀입니까?"),
                    L("현감 조명구", "잠입하시오. 다만 조선 배를 일본군이 곱게 들여보내 줄 리 없지. 관군과 한바탕 싸워 쫓기는 밀무역선처럼 보이면 저들도 의심을 덜 할 게요."),
                    L("혜", "…관군과 싸우란 말씀이군요."),
                    L("현감 조명구", "관군 쪽 뒷일은 내가 수습하겠소. 이건 약속한 값이오."),
                } },
        };
        q13.onComplete = new[]
        {
            new QuestAction { type = QuestActionType.GiveGold, value = 300 },
        };
        q13.outroDialogue = new DialogueLine[0];
        q13.next = q14;

        // ---------------- 1-4 구로세지마 잠입 준비 ----------------
        q14.questId = "1-4"; q14.act = "1막"; q14.title = "구로세지마 잠입 준비"; q14.giver = "현감 조명구";
        q14.summary = "관군과 싸워 관군 현상금을 얻고(밀무역선 위장) 일본 현상금을 없앤 뒤 구로세지마에 닿는다. 1막 끝.";
        q14.onStart = new[]
        {
            new QuestAction { type = QuestActionType.SpawnShips, template = "panok_navy", value = 1, distance = 450f },
        };
        q14.introDialogue = new[]
        {
            L("안내", "관군 배를 공격하면 관군 현상금이 오르고, 그만큼 일본 현상금은 내려갑니다."),
        };
        q14.objectives = new[]
        {
            new QuestObjective { type = QuestObjectiveType.BountyAtLeast, description = "관군과 싸워 관군 현상금 1단계 이상 얻기 (밀무역선 위장)", faction = Faction.Navy, count = 1 },
            new QuestObjective { type = QuestObjectiveType.BountyAtMost, description = "일본 현상금 없애기 (0단계)", faction = Faction.Japanese, count = 0 },
            new QuestObjective { type = QuestObjectiveType.Reach, description = "구로세지마 선착장으로 가기", targetId = "구로세지마_선착장", radius = 0f },
        };
        q14.onComplete = new[]
        {
            new QuestAction { type = QuestActionType.Message, text = "1막 끝 — 2막 「구로세지마」로 이어집니다" },
        };
        q14.outroDialogue = new[]
        {
            L("혜", "여기가 구로세지마… 영주가 정말 왜구를 감싸고 있는지, 내 눈으로 확인하겠다."),
        };
        q14.next = AssetDatabase.LoadAssetAtPath<QuestData>("Assets/02.Resources/00.Component/09.Quest/Act2/Q_2-1_KuroseArrival.asset"); // 2막(QuestStoryBuilder 가 만듦) — 없으면 null

        foreach (var q in new[] { p1, p2, q11, q12, q13, q14 }) EditorUtility.SetDirty(q);
        AssetDatabase.SaveAssets();
        return "1막 퀘스트 6개 → " + Dir;
    }

    static QuestData Get(string name)
    {
        string path = Dir + "/" + name + ".asset";
        var q = AssetDatabase.LoadAssetAtPath<QuestData>(path);
        if (q == null) { q = ScriptableObject.CreateInstance<QuestData>(); AssetDatabase.CreateAsset(q, path); }
        return q;
    }

    /// <summary>첫 퀘스트(P-1).</summary>
    public static QuestData First() { return AssetDatabase.LoadAssetAtPath<QuestData>(Dir + "/Q_P1_WokouRaid.asset"); }
}
