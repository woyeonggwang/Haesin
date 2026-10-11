using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 2막~에필로그 퀘스트 데이터 만들기(2026-10-09, 사용자 "나머지 퀘스트도 만드세요"). 스토리 원본 = 스프레드시트 `해신_스토리_구조.xlsx`(2-1 ~ E-2, 분기 ①·②).
/// MCP 에서 QuestStoryBuilder.Build() 를 부름 — 먼저 QuestAct1Builder.Build() 를 부르고, 2막~에필로그 에셋을
/// `02.Resources/00.Component/09.Quest/Act2·Act3·Act4·Epilogue/Q_*.asset` 에 만들거나 덮어쓴 뒤 1-4 → 2-1 을 잇는다.
/// 다시 부르면 인스펙터에서 고친 대사도 덮어씀. 대사는 스프레드시트 줄거리를 Claude 가 씀.
/// 장소·인물 id 는 Editor/QuestSceneSetup 이 씬에 놓는 마커·NPC·QuestTag 와 같아야 함.
/// 분기 ① 2-8: 깃발 kill_lord → 2-8A(영주 암살 — 일본 현상금 5단계, 현감 보상) / 아니면 2-8B(명령 불복 — 관군 현상금 2단계, 숙부의 밀서).
/// 분기 ② 4-5: 깃발 kill_king → 4-5A(시역 — 관군 현상금 5단계, 육조거리 관군이 덤빔) / 아니면 4-5B(왕의 고백).
/// </summary>
public static class QuestStoryBuilder
{
    const string Root = "Assets/02.Resources/00.Component/09.Quest";

    // ---------------- 짧게 쓰는 도우미 ----------------
    static DialogueLine L(string s, string t) { return new DialogueLine(s, t); }
    static DialogueLine[] D(params DialogueLine[] l) { return l; }
    static QuestAction[] A(params QuestAction[] a) { return a; }
    static QuestObjective[] O(params QuestObjective[] o) { return o; }
    static QuestAction Msg(string t) { return new QuestAction { type = QuestActionType.Message, text = t }; }
    static QuestAction Show(string tag, bool on) { return new QuestAction { type = QuestActionType.SetTagActive, text = tag, value = on ? 1 : 0 }; }
    static QuestAction Tele(string marker) { return new QuestAction { type = QuestActionType.TeleportPlayer, text = marker }; }
    static QuestAction Aggro(string tag, float range, bool now) { return new QuestAction { type = QuestActionType.SoldierAggro, text = tag, distance = range, value = now ? 1 : 0 }; }
    static QuestAction Bounty(Faction f, int lv) { return new QuestAction { type = QuestActionType.SetBountyLevel, faction = f, value = lv }; }
    static QuestAction BountyAtLeast(Faction f, int lv) { return new QuestAction { type = QuestActionType.SetBountyLevel, faction = f, value = lv, text = "atleast" }; }
    static QuestAction Gold(int v) { return new QuestAction { type = QuestActionType.GiveGold, value = v }; }
    static QuestAction Subs(params DialogueLine[] l) { return new QuestAction { type = QuestActionType.Subtitles, lines = l }; }
    static QuestAction Spawn(string tpl, int n, float dist, string marker)
    { return new QuestAction { type = QuestActionType.SpawnShips, template = tpl, value = n, distance = dist, marker = marker, ignoreTerritory = true }; }

    static QuestObjective Talk(string npc, string desc, DialogueLine[] lines) { return new QuestObjective { type = QuestObjectiveType.Talk, targetId = npc, description = desc, dialogue = lines }; }
    static QuestObjective Choice(string npc, string desc, DialogueLine[] lines, string[] choices, string[] flags)
    { var o = Talk(npc, desc, lines); o.choices = choices; o.choiceFlags = flags; return o; }
    static QuestObjective Reach(string marker, string desc) { return new QuestObjective { type = QuestObjectiveType.Reach, targetId = marker, description = desc }; }
    static QuestObjective Sink(Faction f, int n, string desc, string[] tpl, int keep, float dist, string marker)
    { return new QuestObjective { type = QuestObjectiveType.SinkShips, faction = f, count = n, description = desc, spawnTemplates = tpl, keepAlive = keep, spawnDistance = dist, marker = marker, ignoreTerritory = true }; }
    static QuestObjective Defeat(string tag, string desc) { return new QuestObjective { type = QuestObjectiveType.Defeat, targetId = tag, description = desc }; }

    static readonly string[] Wokou = { "seki_wokou", "anteck_wokou" };

    static QuestData Get(string dir, string name)
    {
        string folder = Root + "/" + dir;
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(Root, dir);
        string path = folder + "/" + name + ".asset";
        var q = AssetDatabase.LoadAssetAtPath<QuestData>(path);
        if (q == null) { q = ScriptableObject.CreateInstance<QuestData>(); AssetDatabase.CreateAsset(q, path); }
        q.onStart = new QuestAction[0]; q.introDialogue = new DialogueLine[0]; q.onComplete = new QuestAction[0]; q.outroDialogue = new DialogueLine[0];
        q.next = null; q.branchFlag = ""; q.branchNext = null; q.giver = "";
        return q;
    }

    static void Head(QuestData q, string id, string act, string title, string giver, string summary)
    { q.questId = id; q.act = act; q.title = title; q.giver = giver; q.summary = summary; }

    public static string Build()
    {
        QuestAct1Builder.Build();
        var all = new List<QuestData>();

        // ==================== 2막 구로세지마 ====================
        var q21 = Get("Act2", "Q_2-1_KuroseArrival");
        var q22 = Get("Act2", "Q_2-2_LandlordRequest");
        var q23 = Get("Act2", "Q_2-3_SuspiciousGuest");
        var q24 = Get("Act2", "Q_2-4_BurningVillage");
        var q25 = Get("Act2", "Q_2-5_SilverRetainer");
        var q26 = Get("Act2", "Q_2-6_WokouDen");
        var q27 = Get("Act2", "Q_2-7_Mastermind");
        var q28 = Get("Act2", "Q_2-8_SecretOrder");
        var q28a = Get("Act2", "Q_2-8A_Assassination");
        var q28b = Get("Act2", "Q_2-8B_Refusal");
        all.AddRange(new[] { q21, q22, q23, q24, q25, q26, q27, q28, q28a, q28b });

        Head(q21, "2-1", "2막", "구로세지마 잠입", "", "관군에 쫓기는 밀무역선으로 위장해 일본섬 구로세지마에 들어간다.");
        q21.introDialogue = D(L("혜", "조선 배라는 걸 너무 드러내지 말자. 여기선 우리가 이방인이다."));
        q21.objectives = O(
            Reach("구로세_상인거리", "배에서 내려(E 3초) 상인 거리로 걸어가기"),
            Talk("hayashi", "상인 거리의 지주 하야시와 이야기하기", D(
                L("하야시", "조선 배라… 관군에 쫓기다 여기까지 왔다는 소문은 들었소."),
                L("혜", "장사를 하러 왔습니다. 일본 쪽 바다는 어떻습니까?"),
                L("하야시", "어떻긴. 왜구 놈들 때문에 쌀 한 섬 실어 나르기가 목숨 걸고 하는 일이 됐소."),
                L("하야시", "배가 있고 포가 있다니… 그대에게 부탁할 일이 생길지도 모르겠구려."))));
        q21.next = q22;

        Head(q22, "2-2", "2막", "지주의 부탁", "지주 하야시 소에몬", "[탈환] 왜구에게 빼앗긴 지주의 쌀을 되찾아 준다. 보답으로 영주 쪽 수송 일을 소개받는다.");
        q22.objectives = O(
            Talk("hayashi", "하야시의 부탁 듣기", D(
                L("하야시", "어젯밤 내 쌀을 실은 배가 왜구에게 털렸소. 놈들 배가 아직 앞바다를 맴돌고 있다 하오."),
                L("하야시", "쌀만 되찾아 준다면 값은 섭섭지 않게 치르리다."),
                L("혜", "놈들 배를 가라앉히면 되겠군요."))),
            Sink(Faction.Wokou, 3, "쌀을 실은 왜구 배 격침", Wokou, 2, 380f, ""),
            Talk("hayashi", "하야시에게 돌아가 알리기", D(
                L("하야시", "정말 해냈구려! 쌀 가마니가 바다에 떠다닌다는 소식에 어부들이 다 몰려갔소."),
                L("하야시", "그 솜씨라면 영주님께 말씀드려 볼 만하오. 성으로 올라가 보시오. 내 이름을 대면 들여보내 줄 게요."))));
        q22.onComplete = A(Gold(200));
        q22.next = q23;

        Head(q23, "2-3", "2막", "의심받는 손님", "영주 구로세 마사토키", "[호위] 조선에서 온 혜를 믿지 못하는 영주가 시험 삼아 쌀 수송선 호위를 맡긴다. 도중에 왜구가 습격한다.");
        q23.objectives = O(
            Talk("kurose", "성으로 올라가 영주 구로세 만나기", D(
                L("구로세", "하야시가 보낸 조선 사람이 그대인가."),
                L("구로세", "조선 배가 왜구를 친다? 쉽게 믿기지 않는군. 조선과 왜구가 한통속이라는 말도 돈다."),
                L("혜", "믿지 못하시겠다면 시험해 보십시오."),
                L("구로세", "좋다. 내 쌀 수송선이 북서 포구로 나간다. 무사히 닿게 지켜라. 그러면 그대 말을 믿어 주지."))),
            Reach("구로세_항구", "배로 돌아가 항구 앞바다로"),
            new QuestObjective
            {
                type = QuestObjectiveType.Escort, description = "쌀 수송선을 북서 포구까지 호위", targetId = "구로세_북서포구", marker = "구로세_호위출발",
                escortTemplate = "merchant", escortFaction = Faction.Japanese, escortSpeed = 4.5f,
                spawnTemplates = new[] { "seki_wokou" }, count = 3, keepAlive = 2, spawnDistance = 260f,
                onEnd = A(Show("kurose_castle", false), Show("kurose_harbor", true))
            },
            Talk("kurose_harbor", "항구에서 영주에게 알리기", D(
                L("구로세", "수송선이 무사히 닿았다는 전갈을 받았소. 왜구가 일본 배까지 노리는 걸 그대도 보았겠지."),
                L("구로세", "…놈들은 누구의 편도 아니군. 그대를 의심한 건 사과하리다."))));
        q23.onComplete = A(Gold(200));
        q23.next = q24;

        Head(q24, "2-4", "2막", "불타는 어촌", "영주 구로세 마사토키", "[연합 해전] 왜구에게 습격당한 영주령 항구를 영주 수군과 함께 지킨다. 침몰한 왜구 배에서 조선식 화포가 나온다.");
        q24.onStart = A(Show("fire_kurose", true), Msg("상인 거리에 불이 났습니다 — 왜구 습격!"), Spawn("anteck", 2, 120f, "구로세_항구"));
        q24.introDialogue = D(L("구로세", "놈들이 항구를 노린다! 조선 배, 힘을 보태 주시오!"), L("혜", "포를 준비하라! 일본 배는 쏘지 마라!"));
        q24.objectives = O(
            Sink(Faction.Wokou, 4, "항구를 습격한 왜구 배 격침 (구로세 수군과 함께)", Wokou, 2, 300f, "구로세_항구"),
            new QuestObjective
            {
                type = QuestObjectiveType.Reach, targetId = "구로세_침몰선", description = "가라앉은 왜구 배 살펴보기",
                onEnd = A(Subs(L("선원", "아씨, 건져 올린 화포 좀 보십시오. 이건… 조선 것입니다!"), L("혜", "조선식 화포가 왜 왜구 배에…?")))
            },
            Talk("kurose_harbor", "영주에게 화포 이야기하기", D(
                L("구로세", "조선식 화포라… 설명해 보시오. 조선이 왜구를 돕고 있는 것이오?"),
                L("혜", "저도 모릅니다. 하지만 이게 조선 조정의 뜻이라고는 믿지 않습니다."),
                L("구로세", "…그 눈빛은 거짓말하는 사람의 것이 아니군. 좋소. 다만 지켜보겠소."))));
        q24.onComplete = A(Show("fire_kurose", false));
        q24.next = q25;

        Head(q25, "2-5", "2막", "은을 받는 가신", "영주 구로세 마사토키", "[미행·잠입] 왜구에게 정보를 흘리는 가신을 찾아낸다. 정체 모를 조선인에게서 은을 받는 장면을 목격한다.");
        q25.onStart = A(Show("kurose_harbor", false), Show("kurose_castle", true));
        q25.objectives = O(
            Talk("kurose", "성의 영주에게 가기", D(
                L("구로세", "왜구가 내 수송선 길을 너무 잘 안다. 내 가신 가운데 누군가 정보를 흘리고 있소."),
                L("구로세", "의심 가는 자가 있소. 성 아래로 내려가는 그자를 따라가 보시오. 들키면 끝이오."))),
            new QuestObjective
            {
                type = QuestObjectiveType.Follow, targetId = "retainer", description = "성 아래 길에서 가신을 들키지 않게 미행", radius = 45f, minDistance = 5f, followStartRange = 30f,
                onBegin = A(Show("masked_man", true)),
                dialogue = D(L("가신", "약속한 물건이오. 다음 수송선은 사흘 뒤 북서 포구로 나가오."),
                             L("정체 모를 조선인", "수고했소. 은은 늘 그렇듯 넉넉히 넣었소."),
                             L("가신", "조선 나리들은 손이 크구려."),
                             L("혜", "(조선 사람이… 왜구에게 길을 대 주고 있다고?)"))
            },
            Talk("kurose", "영주에게 본 것을 알리기", D(
                L("혜", "가신이 정체 모를 조선인에게서 은을 받았습니다. 수송선 길을 넘겨주더군요."),
                L("구로세", "조선인이라… 그대가 직접 보았다니 믿겠소. 그자는 오늘 밤 안으로 잡아들이겠소."))));
        q25.onComplete = A(Show("masked_man", false), Show("retainer", false));
        q25.next = q26;

        Head(q26, "2-6", "2막", "왜구의 소굴", "영주 구로세 마사토키", "[해전+상륙] 가신을 심문해 알아낸 무인도의 왜구 본거지를 영주와 함께 급습한다. 상륙해 두목을 사로잡는다.");
        q26.objectives = O(
            Talk("kurose", "영주와 출정 이야기하기", D(
                L("구로세", "가신이 입을 열었소. 남서쪽 무인도에 왜구 소굴이 있다 하오."),
                L("구로세", "내 수군이 앞장서겠소. 그대는 바다의 배들을 정리하고 섬에 올라 두목을 잡아 주시오."))),
            new QuestObjective
            {
                type = QuestObjectiveType.SinkShips, faction = Faction.Wokou, count = 4, description = "왜구섬 앞바다의 왜구 배 격침 (구로세 수군과 함께)",
                spawnTemplates = Wokou, keepAlive = 2, spawnDistance = 150f, marker = "왜구섬_앞바다", ignoreTerritory = true,
                onBegin = A(Msg("구로세 수군이 왜구섬으로 향합니다"), Spawn("anteck", 2, 260f, "왜구섬_앞바다"))
            },
            Reach("왜구섬_선착장", "왜구섬에 상륙하기 (E 3초)"),
            new QuestObjective
            {
                type = QuestObjectiveType.Defeat, targetId = "wokou_boss", description = "왜구 두목 제압",
                onEnd = A(Show("wokou_boss", false), Show("kurose_den", true), Msg("왜구 두목을 사로잡았습니다"))
            },
            Talk("kurose_den", "소굴에 오른 영주와 이야기하기", D(
                L("구로세", "해냈구려. 놈을 산 채로 잡았으니 할 말이 많을 것이오."),
                L("구로세", "…내 아내와 어린 아들도 왜구 손에 잃었소. 그날 이후 바다만 보면 칼을 쥐었지."),
                L("혜", "그래서 그토록 왜구를 쫓으셨군요."),
                L("구로세", "그대가 조선 사람이라 의심했던 것을 용서하시오. 이제 그대는 내 손님이오."))));
        q26.next = q27;

        Head(q27, "2-7", "2막", "왜구의 배후", "영주 구로세 마사토키", "생포한 왜구 두목이 조선 관아의 화약·문서를 지니고 있다. 일본섬은 왜구와 무관함을 깨닫는다.");
        q27.objectives = O(
            Talk("kurose_den", "두목에게서 나온 것 살펴보기", D(
                L("구로세", "두목의 짐에서 나온 것이오. 화약 궤짝과 이 문서들."),
                L("혜", "이 인장은… 조선 관아의 것입니다. 해월도 관아…?"),
                L("구로세", "조선 쪽 누군가가 왜구를 부려 일본에 누명을 씌우고 있소. 이 증거를 쥐고 조선 조정에 따지겠소."),
                L("혜", "(현감 조명구… 설마.)"))));
        q27.onComplete = A(Show("kurose_den", false), Show("kurose_castle", true));
        q27.next = q28;

        Head(q28, "2-8", "2막", "암살 밀서", "현감 조명구(밀서)", "해월도에서 영주 암살 명령이 온다. 분기 ① — 영주를 죽일 것인가.");
        q28.onStart = A(Show("jeonryeong", true));
        q28.introDialogue = D(L("선원", "아씨, 선착장에 해월도에서 왔다는 사람이 기다립니다."));
        q28.objectives = O(
            Choice("jeonryeong", "구로세 선착장의 해월도 전령 만나기", D(
                L("전령", "현감 나리의 서찰입니다. 아무도 보지 않는 데서 읽으라 하셨습니다."),
                L("현감 조명구(서찰)", "구로세 영주를 없애라. 그자는 조선을 해치려 증거를 꾸미는 자다."),
                L("현감 조명구(서찰)", "일을 마치면 관아에서 그대를 크게 쓰겠다. 숙부 되시는 분께도 좋은 소식이 될 것이다."),
                L("혜", "(영주는 왜구를 쫓던 사람이다. 증거를 쥔 사람을 없애라니… 그런데 숙부님을 들먹이다니.)")),
                new[] { "영주를 죽인다 (현감의 명을 따른다)", "영주를 죽이지 않는다 (명을 거스른다)" }, new[] { "kill_lord", "spare_lord" }));
        q28.onComplete = A(Show("jeonryeong", false));
        q28.branchFlag = "kill_lord"; q28.branchNext = q28a; q28.next = q28b;

        Head(q28a, "2-8A", "2막", "영주 암살", "현감 조명구", "분기 ① 영주를 죽인다 — 구로세지마 수배령, 현감의 추천장으로 망운도 관아로.");
        q28a.onStart = A(Show("kurose_castle", false), Show("kurose_combat", true));
        q28a.introDialogue = D(L("혜", "…명을 받았으니 따른다. 미안하오, 영주."));
        q28a.objectives = O(
            new QuestObjective
            {
                type = QuestObjectiveType.Defeat, targetId = "kurose_combat", description = "성으로 올라가 영주 구로세 처치",
                onBegin = A(Aggro("kurose_combat", 8f, false)),
                onEnd = A(Bounty(Faction.Japanese, 5), Msg("구로세지마에 수배령이 내려졌습니다 — 일본군이 대거 출격합니다"),
                          Subs(L("구로세", "조선… 사람을… 믿은… 내가…")))
            },
            Reach("해월도_선착장", "해월도로 돌아가기"),
            Talk("hyeongam", "해월도 관아의 현감에게 알리기", D(
                L("현감 조명구", "해냈구려. 이제 일본 놈들이 들고 온 증거 따위는 아무도 믿지 않을 게요."),
                L("현감 조명구", "약속한 값이오. 그리고 이건 추천장이오. 망운도 관아에서 유배 죄인을 지키는 일을 맡으시오."),
                L("혜", "(…나는 무엇을 한 것인가.)"))));
        q28a.onComplete = A(Gold(500));

        Head(q28b, "2-8B", "2막", "명령 불복", "숙부", "분기 ① 영주를 죽이지 않는다 — 해월도 관군 적대, 숙부의 밀서로 망운도 항로 안내.");
        q28b.onStart = A(Bounty(Faction.Navy, 2), Msg("현감의 명을 거슬렀습니다 — 관군이 혜를 쫓습니다"), Show("sukbu_servant", true));
        q28b.introDialogue = D(L("혜", "증거를 쥔 사람을 죽일 수는 없다. 현감이 무엇을 숨기는지 알아내야 해."));
        q28b.objectives = O(
            Talk("sukbu_servant", "선착장의 숙부댁 하인 만나기", D(
                L("하인", "아씨, 나리께서 보내셨습니다. 해월도로는 돌아오지 마시랍니다."),
                L("하인", "망운도로 가시랍니다. 그곳 관아에 말을 넣어 두셨다고요."),
                L("혜", "숙부님이… 내가 명을 거스른 걸 어떻게 벌써 아시고?"),
                L("하인", "나리는 늘 아씨를 지켜보고 계십니다."))));
        q28b.onComplete = A(Show("sukbu_servant", false));

        // ==================== 3막 망운도 ====================
        var q31 = Get("Act3", "Q_3-1_ExileWatch");
        var q32 = Get("Act3", "Q_3-2_Herbs");
        var q33 = Get("Act3", "Q_3-3_OldComrade");
        var q34 = Get("Act3", "Q_3-4_Memorial");
        var q35 = Get("Act3", "Q_3-5_Trial");
        var q36 = Get("Act3", "Q_3-6_SealedLetter");
        all.AddRange(new[] { q31, q32, q33, q34, q35, q36 });
        q28a.next = q31; q28b.next = q31;

        Head(q31, "3-1", "3막", "유배 죄인 감시", "망운도 관아", "망운도 관아에서 유배 죄인을 관리하는 일을 받는다.");
        q31.objectives = O(
            Reach("망운도_선착장", "망운도로 가기"),
            Talk("clerk_mangun", "망운도 관아의 관리 만나기", D(
                L("관리", "그대가 새로 온 사람이오? 일은 간단하오."),
                L("관리", "이 섬에 유배 죄인이 하나 있소. 한윤겸, 선왕 때 도승지를 지낸 자요."),
                L("관리", "섬을 벗어나지 않는지, 누구와 서찰을 주고받는지 살피시오. 늙은이라 말썽은 없을 게요."))));
        q31.next = q32;

        Head(q32, "3-2", "3막", "유배 양반의 부탁 1", "한윤겸(청재)", "한윤겸의 부탁으로 약초를 구해 온다. 한윤겸은 혜의 얼굴에서 숙의 정씨를 알아보지만 말하지 않는다.");
        q32.objectives = O(
            Talk("han_yungyeom", "초가에 사는 한윤겸 찾아가기", D(
                L("한윤겸", "새로 온 감시인이구려. …"),
                L("한윤겸", "……"),
                L("혜", "왜 그렇게 보십니까?"),
                L("한윤겸", "아니오. 옛날에 알던 사람과 닮아서. 늙으면 눈이 흐려지는 법이오."),
                L("한윤겸", "부탁 하나 해도 되겠소? 기침이 도져서 그러오. 골짜기 위에 도라지와 감초가 자라오."))),
            new QuestObjective { type = QuestObjectiveType.Reach, targetId = "망운도_약초", description = "골짜기 위에서 약초 캐기", onEnd = A(Msg("약초를 캤습니다")) },
            Talk("han_yungyeom", "한윤겸에게 약초 갖다주기", D(
                L("한윤겸", "고맙소. 감시인이 죄인 약초를 캐 주다니, 세상이 많이 변했구려."),
                L("혜", "죄인이라 하기엔 너무 점잖으십니다."))));
        q32.next = q33;

        Head(q33, "3-3", "3막", "유배 양반의 부탁 2", "한윤겸(청재)", "해적에게 잡힌 한윤겸의 옛 동료를 구출한다.");
        q33.objectives = O(
            Talk("han_yungyeom", "한윤겸의 두 번째 부탁 듣기", D(
                L("한윤겸", "옛 벗 하나가 나를 찾아오다 해적에게 잡혔다 하오. 사헌부에 있던 강직한 사람이지."),
                L("한윤겸", "그대 배라면… 아니, 무리한 부탁이구려."),
                L("혜", "해적선이 어디 있습니까?"))),
            new QuestObjective
            {
                type = QuestObjectiveType.SinkShips, faction = Faction.Wokou, count = 2, description = "옛 동료를 잡아 둔 해적선 격침",
                spawnTemplates = new[] { "anteck_wokou", "seki_wokou" }, keepAlive = 2, spawnDistance = 400f, ignoreTerritory = true,
                onEnd = A(Msg("해적선에서 한윤겸의 옛 동료를 구해 냈습니다"))
            },
            Talk("han_yungyeom", "한윤겸에게 알리기", D(
                L("한윤겸", "벗이 무사하다니… 이 은혜를 어찌 갚겠소."),
                L("한윤겸", "그대는 감시인보다는… 장수의 기개를 지녔구려."))));
        q33.next = q34;

        Head(q34, "3-4", "3막", "제삿날", "한윤겸(청재)", "[교역·운송+검문 회피] 한윤겸이 '옛 주군의 기일'이라며 제수를 부탁한다. 다녀오는 길에 관군 검문선을 피한다.");
        q34.objectives = O(
            Talk("han_yungyeom", "한윤겸의 부탁 듣기", D(
                L("한윤겸", "내일이 옛 주군의 기일이오. 제사상이라도 차려 드리고 싶은데, 이 섬엔 아무것도 없구려."),
                L("한윤겸", "중섬 장터에 가면 제수를 구할 수 있소. 관군 검문이 심할 테니 조심하시오."))),
            new QuestObjective
            {
                type = QuestObjectiveType.Reach, targetId = "중섬_선착장", description = "중섬 장터에서 제수 사 오기",
                onEnd = A(Msg("제수를 샀습니다 — 관군 검문선이 다가옵니다"), BountyAtLeast(Faction.Navy, 1), Spawn("panok_navy", 2, 320f, ""))
            },
            Reach("망운도_선착장", "검문선을 피해(또는 뚫고) 망운도로 돌아가기"),
            Talk("han_yungyeom", "한윤겸과 제사 지내기", D(
                L("한윤겸", "고맙소. 이제 상을 차릴 수 있겠구려."),
                L("한윤겸", "(바다를 향해 절한다) 전하… 소신이 왔사옵니다."),
                L("한윤겸", "그대도… 같이 절해 주겠소?"),
                L("혜", "제가요? …예."),
                L("혜", "(왜 저렇게 우시는 걸까.)"))));
        q34.next = q35;

        Head(q35, "3-5", "3막", "무력 시험", "한윤겸(청재)", "근해에 출몰하는 해적 두목선과 싸운다.");
        q35.objectives = O(
            Talk("han_yungyeom", "한윤겸과 이야기하기", D(
                L("한윤겸", "그대 솜씨를 한 번 더 보고 싶소. 근해에 해적 두목선이 나타났다 하오."),
                L("한윤겸", "그 배를 가라앉힐 수 있다면… 그대에게 맡길 일이 있소."))),
            Sink(Faction.Wokou, 3, "해적 두목선과 호위선 격침", new[] { "anteck_wokou", "seki_wokou", "seki_wokou" }, 3, 420f, ""),
            Talk("han_yungyeom", "한윤겸에게 돌아가기", D(
                L("한윤겸", "됐소. 이제 확신이 서는구려."))));
        q35.next = q36;

        Head(q36, "3-6", "3막", "봉인된 서신", "한윤겸(청재)", "한윤겸이 봉인한 서신을 한양의 권시헌에게 전하러 간다.");
        q36.objectives = O(
            Talk("han_yungyeom", "한윤겸에게서 서신 받기", D(
                L("한윤겸", "이 서신을 한양의 병조참판 권시헌 대감께 직접 전해 주시오. 누구에게도 보이지 말고."),
                L("혜", "감시인더러 죄인의 서신을 나르라는 겁니까?"),
                L("한윤겸", "그대는 감시인이 아니오. …때가 되면 알게 될 것이오."),
                L("한윤겸", "관아에는 내가 손을 써 두었소. 가시오."))));
        q36.onComplete = A(Bounty(Faction.Navy, 0), Msg("한윤겸이 손을 써 관군 수배가 풀렸습니다 — 한양으로"));

        // ==================== 4막 한양 ====================
        var q41 = Get("Act4", "Q_4-1_Hanyang");
        var q42 = Get("Act4", "Q_4-2_EnterPalace");
        var q43 = Get("Act4", "Q_4-3_Gyeonghoeru");
        var q44 = Get("Act4", "Q_4-4_Uprising");
        var q45 = Get("Act4", "Q_4-5_Confrontation");
        var q45a = Get("Act4", "Q_4-5A_Regicide");
        var q45b = Get("Act4", "Q_4-5B_Truth");
        all.AddRange(new[] { q41, q42, q43, q44, q45, q45a, q45b });
        q36.next = q41;

        Head(q41, "4-1", "4막", "한양 입성", "병조참판 권시헌", "권시헌을 만난다. \"현왕이 네 아버지를 죽였다\"는 말을 듣는다(거짓말 — 실제 범인은 대비).");
        q41.objectives = O(
            Reach("한양_선착장", "한양(조선 큰섬)으로 가기"),
            Talk("gwon_hanyang", "나룻배(E 3초)로 호수를 건너 육조거리의 권시헌 만나기", D(
                L("권시헌", "한 대감의 서신이라… (서신을 읽는다) …그렇군. 역시 그랬어."),
                L("권시헌", "그대의 이름이 혜라고? 그대는 선왕 애종 전하의 핏줄이오. 숙의 정씨 마마의 따님이시지."),
                L("혜", "무슨… 말씀이십니까?"),
                L("권시헌", "선왕께서는 병으로 승하하신 게 아니오. 독살당하셨소."),
                L("권시헌", "그리고 그 자리에 오른 자가 지금의 왕이오. 그대의 아버지를 죽이고 왕위를 차지한 자."),
                L("혜", "……"))));
        q41.next = q42;

        Head(q42, "4-2", "4막", "입궐", "권시헌", "별감으로 위장해 궁에 들어간다. 왕은 만나지 못한다.");
        q42.onStart = A(Show("palace_day", true));
        q42.objectives = O(
            Talk("gwon_hanyang", "권시헌에게서 계획 듣기", D(
                L("권시헌", "궁에 들어가 보시오. 별감 차림이면 아무도 눈여겨보지 않을 게요."),
                L("권시헌", "왕의 얼굴을 직접 보면 마음이 굳어질 것이오. 광화문으로 가시오."))),
            new QuestObjective { type = QuestObjectiveType.Reach, targetId = "광화문_앞", description = "광화문 앞으로 걸어가기", onEnd = A(Tele("근정전_마당"), Msg("별감 차림으로 궁에 들어섰습니다")) },
            Talk("naegwan", "근정전 마당의 내관에게 말 걸기", D(
                L("내관", "새로 들어온 별감인가? 오늘 전하께서는 아무도 들이지 말라 하셨네."),
                L("내관", "강녕전 근처엔 얼씬도 하지 말게."))),
            Talk("daebi", "행차 중인 대비에게 고개 숙이기", D(
                L("홍 대비", "멈추거라. 고개를 들어 보아라."),
                L("홍 대비", "…처음 보는 얼굴이구나. 별감 주제에 눈빛이 곱구나."),
                L("혜", "황공하옵니다, 마마."),
                L("홍 대비", "궁에선 눈에 띄지 않는 것이 오래 사는 길이니라. 물러가거라."))));
        q42.onComplete = A(Show("palace_day", false));
        q42.next = q43;

        Head(q43, "4-3", "4막", "경회루 회합", "권시헌·반란파", "거사 계획을 듣는다: 대비전에 불을 질러 이목을 끌고 대비를 제거, 혜는 강녕전에서 왕을 붙잡아 둔다.");
        q43.onStart = A(Show("gyeonghoeru", true), Tele("경회루_동쪽"), Msg("그날 밤, 경회루"));
        q43.objectives = O(
            Talk("gwon_palace", "경회루에 모인 반란파와 권시헌 만나기", D(
                L("권시헌", "모두 모였군. 계획을 다시 말하겠소."),
                L("권시헌", "자정에 대비전에 불을 지른다. 궁의 눈이 모두 그리로 쏠릴 것이오."),
                L("반란파 무사", "대비는 우리가 맡겠소. 그 여자가 살아 있는 한 조정은 바로 서지 못하오."),
                L("권시헌", "그대는 강녕전으로 가 왕을 붙잡아 두시오. 새 왕을 모실 때까지 그자가 움직이지 못하게."),
                L("혜", "붙잡아 두기만 하라는 겁니까? 아버지를 죽인 자를?"),
                L("권시헌", "…그대의 칼이니, 그대가 정하시오."))));
        q43.next = q44;

        Head(q44, "4-4", "4막", "거사", "반란파", "대비전 화재. 근정전 마당의 호위를 뚫고 강녕전으로 난입한다.");
        q44.onStart = A(Show("gyeonghoeru", false), Show("fire_daebi", true), Show("palace_guard", true), Aggro("palace_guard", 45f, false),
                        Tele("근정전_마당"), Msg("대비전에 불길이 솟았습니다!"));
        q44.introDialogue = D(L("반란파 무사", "불이야! 대비전에 불이야!"), L("혜", "지금이다."));
        q44.objectives = O(Defeat("palace_guard", "근정전 마당의 궁궐 호위 제압"));
        q44.next = q45;

        Head(q45, "4-5", "4막", "강녕전의 대면", "현왕 이담", "등 돌린 왕에게 \"아버지의 복수를 하러 왔다\"고 말한다. 왕이 돌아서고, 숙부임을 깨닫는다. 분기 ②.");
        q45.onStart = A(Show("king", true), Tele("강녕전_마당"));
        q45.introDialogue = D(L("반란파 무사", "대비가… 대비가 불길 속에서 돌아가셨다!"));
        q45.objectives = O(
            Choice("king", "강녕전 앞에 등 돌리고 선 왕에게 다가가기", D(
                L("혜", "아버지의 복수를 하러 왔다."),
                L("왕", "…기다리고 있었다. 언젠가 네가 올 줄 알았다."),
                L("왕", "(왕이 돌아선다)"),
                L("혜", "……숙부님?"),
                L("왕", "그래, 혜야. 섬에서 너를 숨겨 키운 숙부가… 이 나라의 왕이다."),
                L("혜", "그럼 아버지를… 숙부님이…?")),
                new[] { "왕을 죽인다", "왕을 죽이지 않는다" }, new[] { "kill_king", "spare_king" }));
        q45.branchFlag = "kill_king"; q45.branchNext = q45a; q45.next = q45b;

        Head(q45a, "4-5A", "4막", "시역", "", "분기 ② 왕을 죽인다 — 새 조정도 정통성을 위해 시역 죄인을 쫓는다. 진실은 나중에 유서로만 알게 된다.");
        q45a.onStart = A(Show("king", false), Bounty(Faction.Navy, 5), Aggro("yukjo_guards", 40f, false), Tele("광화문_앞"),
                         Msg("혜가 왕을 베었습니다 — 수도의 군사들이 혜를 쫓습니다"));
        q45a.introDialogue = D(L("왕", "…그래. 이것으로… 되었다…"), L("반란파 무사", "왕을… 왕을 시해했다! 시역이다! 저 자를 잡아라!"));
        q45a.objectives = O(Reach("한양_선착장", "수도의 군사를 피해 배로 달아나기"));

        Head(q45b, "4-5B", "4막", "왕의 고백", "현왕 이담", "분기 ② 왕을 죽이지 않는다 — 왕이 대비의 독살 사실을 털어놓은 뒤 스스로 목숨을 끊는다.");
        q45b.objectives = O(
            Talk("king", "칼을 거두고 왕의 이야기 듣기", D(
                L("왕", "형님을… 네 아버지를 독살한 것은 내 어머니, 대비였다."),
                L("왕", "네 어머니 숙의 정씨도 그 일을 눈치채고 목숨을 잃었다."),
                L("왕", "나는 알면서도 어머니를 벌하지 못했다. 고발하지도 못했다. 그저 너를 어머니 눈에서 숨기는 것밖에."),
                L("혜", "권시헌은… 숙부님이 아버지를 죽였다고 했습니다."),
                L("왕", "그자는 네가 망설이지 않기를 바랐겠지. …어머니도 오늘 밤 가셨구나."),
                L("왕", "혜야. 이제 가거라. 뒤돌아보지 말고."))));
        q45b.onComplete = A(Show("king", false), Msg("왕은 스스로 목숨을 끊었습니다"), Tele("광화문_앞"));

        // ==================== 에필로그 ====================
        var qe1 = Get("Epilogue", "Q_E-1_NewKing");
        var qe2 = Get("Epilogue", "Q_E-2_MothersWill");
        all.AddRange(new[] { qe1, qe2 });
        q45a.next = qe1; q45b.next = qe1;

        Head(qe1, "E-1", "에필로그", "새 왕", "반란파", "반란파가 다른 종친을 왕으로 옹립한다. 현왕 묘호는 민종.");
        qe1.introDialogue = D(
            L("", "반란파는 종친 가운데 한 사람을 새 왕으로 세웠다."),
            L("", "세상을 떠난 왕에게는 민종(愍宗)이라는 묘호가 올려졌다."),
            L("혜", "나는 여인이라 왕이 될 수 없다. 그럼 나는… 무엇을 위해 여기까지 왔는가."));
        qe1.objectives = O(Reach("한양_선착장", "한양을 떠나 배로 돌아가기"));
        qe1.next = qe2;

        Head(qe2, "E-2", "에필로그", "생모의 유서", "숙의 정씨(유서)", "생모 숙의 정씨의 유서를 손에 넣는다. 권시헌의 거짓말을 알게 된다.");
        qe2.objectives = O(
            Reach("망운도_선착장", "망운도의 한윤겸에게 가기"),
            Talk("han_yungyeom", "한윤겸에게서 유서 받기", D(
                L("한윤겸", "돌아왔구려. …이제는 드려야겠소. 숙의 정씨 마마께서 남기신 서찰이오."),
                L("숙의 정씨(유서)", "혜야. 네가 이 글을 읽을 즈음이면 어미는 곁에 없겠구나."),
                L("숙의 정씨(유서)", "네 아버지를 해친 것은 대비다. 너의 숙부는 그 일로 평생 괴로워할 사람이니, 원망하지 말거라."),
                L("숙의 정씨(유서)", "그 사람이 너를 지켜 줄 것이다. 부디 바다처럼 넓게 살아라."),
                L("혜", "……권시헌은 거짓을 말했구나."),
                L("혜", "그날 바다를 향해 올린 제사… 그건 아버지의 제사였군요."),
                L("한윤겸", "그렇소. 그대는 아버지께 절을 올린 것이오."))));
        qe2.onComplete = A(Msg("해신 — 끝"));

        // 1막 → 2막
        var q14 = AssetDatabase.LoadAssetAtPath<QuestData>(QuestAct1Builder.Dir + "/Q_1-4_KuroseInfiltration.asset");
        if (q14 != null) { q14.next = q21; EditorUtility.SetDirty(q14); }

        foreach (var q in all) EditorUtility.SetDirty(q);
        AssetDatabase.SaveAssets();
        return "퀘스트 " + all.Count + "개(2막~에필로그) + 1막 다시 만듦, 1-4 → 2-1 연결 " + (q14 != null);
    }
}
