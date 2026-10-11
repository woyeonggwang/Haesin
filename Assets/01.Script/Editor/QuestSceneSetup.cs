using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 2막~에필로그 퀘스트 씬 배치(2026-10-09, 사용자 "나머지 퀘스트도 만드세요"). MCP 에서 QuestSceneSetup.Build() 를 부름.
/// 다시 부르면 이 스크립트가 만든 오브젝트(이름이 같은 것)를 지우고 다시 만든다 — 손으로 옮긴 자리는 사라짐(자리를 바꾸려면 아래 표를 고칠 것).
/// 만드는 것(씬 루트):
///   QuestSystem/Markers   — 퀘스트 장소 마커(QuestMarker)
///   구로세지마_Kurose      — 2막 일본섬 NPC(하야시·영주·전령·가신·정체 모를 조선인 등), 상인 거리 불, 미행 길
///   망운도_Mangundo        — 3막 관아·한윤겸 집(한옥 프리팹)·NPC
///   한양_Hanyang           — 4막 권시헌·궁 안 NPC(내관·대비·궁녀·반란파·왕), 대비전(교태전 자리) 불
///   궁궐호위_PalaceGuards  — 4-4 궁궐 호위 병사(SoldierGroup, NavMesh 는 따로 구움)
///   일본군_성/영주_구로세_갑주 — 2-8 암살 분기의 영주(병사, 처음엔 숨김)
///   왜구소굴_WokouDen/NPC_구로세_소굴 — 2-6 뒤 영주(갑주)
/// NPC = 뿌리(QuestNPC·QuestTag) + 자식 Model(06.Character/&lt;이름&gt;/&lt;이름&gt;.prefab, Animator = Villager 컨트롤러 Idle).
/// 높이: 표의 대략 높이 + 6 m 에서 아래로 레이(지붕을 피하려고 위에서 쏘지 않음).
/// </summary>
public static class QuestSceneSetup
{
    const string Char = "Assets/02.Resources/00.Component/06.Character/";
    const string VillagerCtrl = Char + "Citizen/Animations/Villager.controller";
    const string Hanok = "Assets/02.Resources/00.Component/08.Environment/Hanok/Prefabs/";
    const string FirePrefab = "Assets/02.Resources/01.Asset/VFX/CamelotVFX_ Fire&Smoke/Prefabs/Fire_v2.prefab";
    static System.Text.StringBuilder _log;

    static DialogueLine L(string s, string t) { return new DialogueLine(s, t); }

    public static string Build()
    {
        _log = new System.Text.StringBuilder();

        // ---------------- 마커 ----------------
        var qs = GameObject.Find("QuestSystem");
        var markers = qs.transform.Find("Markers");
        Marker(markers, "구로세_상인거리", "구로세지마 상인 거리", 1470f, 5f, -905f, 25f, 0f);
        Marker(markers, "구로세_항구", "구로세지마 항구 앞바다", 1250f, 0f, -700f, 160f, 0f);
        Marker(markers, "구로세_호위출발", "수송선 출발지", 1300f, 0f, -760f, 30f, 0f);
        Marker(markers, "구로세_북서포구", "수송선 목적지(북서 포구)", 700f, 0f, -300f, 60f, 0f);
        Marker(markers, "구로세_침몰선", "가라앉은 왜구 배", 1180f, 0f, -640f, 60f, 0f);
        Marker(markers, "구로세_성문", "구로세 성문", 1768f, 95f, -968f, 12f, 0f);
        Marker(markers, "왜구섬_앞바다", "왜구섬 앞바다", 400f, 0f, -1950f, 220f, 0f);
        Marker(markers, "왜구섬_선착장", "왜구섬 선착장", 269.6f, 6.5f, -2050.4f, 60f, 0f);
        Marker(markers, "망운도_선착장", "망운도 선착장", -962f, 6.5f, 1402.8f, 120f, 0f);
        Marker(markers, "망운도_약초", "약초가 자라는 골짜기", -960f, 46f, 1640f, 14f, 0f);
        Marker(markers, "중섬_선착장", "중섬 장터 선착장", 1281.6f, 6.5f, 2452.2f, 80f, 0f);
        Marker(markers, "한양_선착장", "한양(조선 큰섬) 선착장", -2944.1f, 6.4f, 3032.7f, 90f, 0f);
        Marker(markers, "광화문_앞", "광화문 앞", -2745f, 10.3f, 3815f, 14f, 180f);
        Marker(markers, "근정전_마당", "근정전 마당", -2745f, 11f, 4082f, 6f, 0f);
        Marker(markers, "경회루_동쪽", "경회루", -2830f, 12.3f, 4270f, 6f, 270f);
        Marker(markers, "강녕전_마당", "강녕전", -2745f, 11.8f, 4244f, 6f, 0f);

        // ---------------- 1막 NPC 모델은 따로(QuestSceneSetup 밖에서 바꿈) ----------------

        // ---------------- 2막 구로세지마 ----------------
        var kur = Root("구로세지마_Kurose");
        Npc(kur, "NPC_하야시", "HayashiSoemon", 1470f, -905f, 5f, 315f, "hayashi", "지주 하야시 소에몬", 3.5f,
            new[] { L("하야시", "쌀값이 또 올랐소. 왜구 놈들 때문에 배가 들어오질 않으니…"), L("하야시", "조선 배라… 낯설지만, 장사에 국적이 어디 있겠소.") }, "", false);
        Npc(kur, "NPC_구로세", "KuroseMasatoki", 1776f, -967f, 95f, 270f, "kurose", "영주 구로세 마사토키", 3.5f,
            new[] { L("구로세", "성 안에서 함부로 칼을 뽑지 마시오. 내 가신들은 성미가 급하오.") }, "kurose_castle", false);
        Npc(kur, "NPC_구로세_항구", "KuroseMasatoki_Armor", 1440f, -880f, 5f, 315f, "kurose_harbor", "영주 구로세 마사토키", 3.5f,
            new[] { L("구로세", "불길부터 잡아라! 배는 내가 맡는다!") }, "kurose_harbor", true);
        Npc(kur, "NPC_전령", "GwanaClerk", 1425f, -868f, 5f, 135f, "jeonryeong", "해월도 관아 전령", 3.5f,
            new[] { L("전령", "현감 나리께서 서찰을 보내셨습니다.") }, "jeonryeong", true);
        Npc(kur, "NPC_숙부댁_하인", "Citizen/HanbokMale", 1431f, -873f, 5f, 135f, "sukbu_servant", "숙부댁 하인", 3.5f,
            new[] { L("하인", "아씨, 나리께서 기다리십니다.") }, "sukbu_servant", true);
        // 미행: 가신(걷는 NPC)과 정체 모를 조선인
        var walker = Npc(kur, "NPC_배신한_가신", "JpRetainer", 1614f, -1089f, 6f, 300f, "", "가신", 0f, null, "retainer", true);
        AddWalker(walker, kur, new[] { new Vector3(1572f, 5f, -1060f), new Vector3(1527f, 5f, -984f), new Vector3(1490f, 5f, -930f), new Vector3(1462f, 5f, -879f) });
        Npc(kur, "NPC_정체모를_조선인", "MaskedMan", 1466f, -873f, 5f, 200f, "", "", 0f, null, "masked_man", true);
        // 상인 거리 불(2-4)
        var md = GameObject.Find("일본마을_JapanTown").transform.Find("MerchantDistrict");
        var houses = new List<Transform>();
        foreach (Transform h in md) houses.Add(h);
        houses.Sort((a, b) => Flat(a.position - new Vector3(1418f, 0f, -862f)).CompareTo(Flat(b.position - new Vector3(1418f, 0f, -862f))));
        var fires = Child(kur, "불_상인거리");
        Clear(fires);
        for (int i = 0; i < Mathf.Min(4, houses.Count); i++) Fire(fires, "Fire_" + i, houses[i].position + Vector3.up * 3.5f, "fire_kurose");
        // 암살 분기 영주(병사)
        var castleGroup = GameObject.Find("일본군_성");
        Soldier(castleGroup.transform, "영주_구로세_갑주", "Soldiers/Kurose_Armor_Sword", 1776f, -967f, 95f, 270f, "영주 구로세", "kurose_combat", true, 0f);
        // 왜구 소굴 영주(2-6 뒤)
        var den = GameObject.Find("왜구소굴_WokouDen");
        Npc(den.transform, "NPC_구로세_소굴", "KuroseMasatoki_Armor", 212f, -2090f, 6.5f, 210f, "kurose_den", "영주 구로세 마사토키", 3.5f,
            new[] { L("구로세", "두목은 내 배로 옮겼소. 이야기는 성에서 마저 하지.") }, "kurose_den", true);
        // 왜구 두목에 표
        var boss = GameObject.Find("왜구두목");
        if (boss != null) { var t = Tag(boss); t.id = "wokou_boss"; t.startHidden = false; _log.AppendLine("왜구두목 tag"); }

        // ---------------- 3막 망운도 ----------------
        var man = Root("망운도_Mangundo");
        Building(man, "관아_Gwana", "Hanok_Giwa_Digeut_01", -1060f, 1465f, 0f);
        Building(man, "한윤겸_집", "Hanok_Choga_Il_01", -875f, 1482f, 0f);
        Npc(man, "NPC_관아_관리", "GwanaClerk", -1046f, 1447f, 10f, 160f, "clerk_mangun", "망운도 관아 관리", 3.5f,
            new[] { L("관리", "유배 죄인이 섬을 벗어나지 않게 지켜보는 게 우리 일이오.") }, "", false);
        Npc(man, "NPC_한윤겸", "HanYungyeom", -884f, 1466f, 10f, 200f, "han_yungyeom", "한윤겸", 3.5f,
            new[] { L("한윤겸", "바다를 보고 있으면… 지난 일들이 파도처럼 밀려오는구려.") }, "", false);

        // ---------------- 4막 한양 ----------------
        var han = Root("한양_Hanyang");
        Npc(han, "NPC_권시헌_육조", "GwonSiheon", -2752f, 3470f, 10f, 180f, "gwon_hanyang", "병조참판 권시헌", 3.5f,
            new[] { L("권시헌", "육조거리에서는 말을 아끼시오. 듣는 귀가 많소.") }, "gwon_hanyang", false);
        Npc(han, "NPC_내관", "Eunuch", -2735f, 4098f, 11f, 200f, "naegwan", "내관", 3.5f,
            new[] { L("내관", "별감은 맡은 일이나 하게.") }, "palace_day", true);
        Npc(han, "NPC_홍대비", "HongDaebi", -2770f, 4106f, 11f, 150f, "daebi", "홍 대비", 3.5f,
            new[] { L("홍 대비", "…") }, "palace_day", true);
        Npc(han, "NPC_궁녀_1", "CourtLady", -2774f, 4102f, 11f, 150f, "", "궁녀", 0f, null, "palace_day", true);
        Npc(han, "NPC_궁녀_2", "CourtLady", -2766f, 4101f, 11f, 150f, "", "궁녀", 0f, null, "palace_day", true);
        Npc(han, "NPC_권시헌_경회루", "GwonSiheon", -2840f, 4270f, 12f, 90f, "gwon_palace", "병조참판 권시헌", 3.5f,
            new[] { L("권시헌", "때가 되면 신호가 갈 것이오.") }, "gyeonghoeru", true);
        Npc(han, "NPC_반란파_1", "RebelSoldier", -2842f, 4265f, 12f, 70f, "", "반란파 무사", 3.5f,
            new[] { L("반란파 무사", "…") }, "gyeonghoeru", true);
        Npc(han, "NPC_반란파_2", "RebelSoldier", -2842f, 4275f, 12f, 110f, "", "반란파 무사", 3.5f,
            new[] { L("반란파 무사", "칼집에서 손을 떼지 마시오.") }, "gyeonghoeru", true);
        var king = Npc(han, "NPC_현왕", "YiDam_King", -2745f, 4249.5f, 13f, 0f, "king", "현왕 이담", 6f,
            new[] { L("왕", "…") }, "king", true);
        var dfires = Child(han, "불_대비전");
        Clear(dfires);
        Vector3 dc = new Vector3(-2720f, 11.7f, 4302f);
        Vector3[] off = { new Vector3(-9f, 4f, -6f), new Vector3(8f, 4.5f, -5f), new Vector3(-6f, 5f, 8f), new Vector3(7f, 4f, 9f), new Vector3(0f, 6f, 0f) };
        for (int i = 0; i < off.Length; i++) Fire(dfires, "Fire_" + i, dc + off[i], "fire_daebi");

        // 궁궐 호위(4-4) — 근정전 마당, 보자마자 덤빔은 퀘스트(SoldierAggro)가 켬
        var pg = Root("궁궐호위_PalaceGuards");
        var sg = pg.GetComponent<SoldierGroup>(); if (sg == null) sg = pg.gameObject.AddComponent<SoldierGroup>();
        sg.factionName = "궁궐 호위";
        Soldier(pg, "궁궐호위_1", "Soldiers/PalaceGuard_Sword", -2762f, 4112f, 11f, 180f, "궁궐 호위", "palace_guard", true, 0f);
        Soldier(pg, "궁궐호위_2", "Soldiers/PalaceGuard_Sword", -2728f, 4112f, 11f, 180f, "궁궐 호위", "palace_guard", true, 0f);
        Soldier(pg, "궁궐호위_3", "Soldiers/PalaceGuard_Sword", -2752f, 4100f, 11f, 180f, "궁궐 호위", "palace_guard", true, 0f);
        Soldier(pg, "궁궐호위_4", "Soldiers/PalaceGuard_Sword", -2738f, 4100f, 11f, 180f, "궁궐 호위", "palace_guard", true, 0f);
        sg.Collect();

        // 육조거리 관군(4-5 시역 분기에서 덤빔)
        var yuk = GameObject.Find("관군_육조거리");
        if (yuk != null) { var t = Tag(yuk); t.id = "yukjo_guards"; t.startHidden = false; }

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        return _log.ToString();
    }

    // ================= 도우미 =================

    static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

    static QuestTag Tag(GameObject g) { var t = g.GetComponent<QuestTag>(); if (t == null) t = g.AddComponent<QuestTag>(); return t; }

    static Transform Root(string name)
    {
        var sc = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        foreach (var r in sc.GetRootGameObjects()) if (r.name == name) return r.transform;
        var go = new GameObject(name);
        return go.transform;
    }

    static Transform Child(Transform p, string name)
    {
        var c = p.Find(name);
        if (c != null) return c;
        var go = new GameObject(name); go.transform.SetParent(p, false);
        return go.transform;
    }

    static void Clear(Transform t) { for (int i = t.childCount - 1; i >= 0; i--) Object.DestroyImmediate(t.GetChild(i).gameObject); }

    static void Remove(Transform p, string name) { var c = p.Find(name); if (c != null) Object.DestroyImmediate(c.gameObject); }

    /// <summary>approxY + 6 m 에서 아래로 레이 — 바닥 높이. 못 맞추면 터레인.</summary>
    static float Ground(float x, float z, float approxY)
    {
        RaycastHit h;
        var o = new Vector3(x, approxY + 6f, z);
        var hits = Physics.RaycastAll(o, Vector3.down, 30f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.NegativeInfinity;
        foreach (var hh in hits)
        {
            if (hh.collider is CharacterController) continue;
            if (hh.collider.GetComponentInParent<SoldierAI>() != null || hh.collider.GetComponentInParent<QuestNPC>() != null) continue;
            if (hh.point.y > best) best = hh.point.y;
        }
        if (!float.IsNegativeInfinity(best)) return best;
        if (Physics.Raycast(o, Vector3.down, out h, 60f)) return h.point.y;
        return approxY;
    }

    static void Marker(Transform parent, string id, string display, float x, float y, float z, float r, float rotY)
    {
        Remove(parent, id);
        var go = new GameObject(id);
        go.transform.SetParent(parent, false);
        float gy = y > 1f ? Ground(x, z, y) : 0f;
        go.transform.SetPositionAndRotation(new Vector3(x, gy, z), Quaternion.Euler(0f, rotY, 0f));
        var m = go.AddComponent<QuestMarker>();
        m.markerId = id; m.displayName = display; m.radius = r;
        _log.AppendLine("마커 " + id + " " + go.transform.position.ToString("F1"));
    }

    /// <param name="model">06.Character 아래 폴더 이름(프리팹은 폴더/폴더.prefab) — "Citizen/HanbokMale" 처럼 경로도 됨.</param>
    static GameObject Npc(Transform parent, string objName, string model, float x, float z, float approxY, float rotY,
        string npcId, string display, float talkRange, DialogueLine[] idle, string tag, bool startHidden)
    {
        Remove(parent, objName);
        var root = new GameObject(objName);
        root.transform.SetParent(parent, false);
        root.transform.SetPositionAndRotation(new Vector3(x, Ground(x, z, approxY), z), Quaternion.Euler(0f, rotY, 0f));
        string leaf = model.Contains("/") ? model.Substring(model.LastIndexOf('/') + 1) : model;
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>(Char + model + "/" + leaf + ".prefab");
        if (pf == null) { _log.AppendLine("!! 프리팹 없음 " + model); return root; }
        var m = (GameObject)PrefabUtility.InstantiatePrefab(pf, root.transform);
        m.name = "Model"; m.transform.localPosition = Vector3.zero; m.transform.localRotation = Quaternion.identity; m.transform.localScale = Vector3.one;
        var an = m.GetComponent<Animator>();
        if (an != null) { an.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(VillagerCtrl); an.applyRootMotion = false; an.cullingMode = AnimatorCullingMode.CullUpdateTransforms; }
        if (!string.IsNullOrEmpty(npcId))
        {
            var q = root.AddComponent<QuestNPC>();
            q.npcId = npcId; q.displayName = display; q.talkRange = talkRange; q.idleLines = idle ?? new DialogueLine[0];
        }
        if (!string.IsNullOrEmpty(tag)) { var t = root.AddComponent<QuestTag>(); t.id = tag; t.startHidden = startHidden; }
        _log.AppendLine("NPC " + objName + " " + root.transform.position.ToString("F1"));
        return root;
    }

    static void AddWalker(GameObject npc, Transform parent, Vector3[] pts)
    {
        var agent = npc.AddComponent<NavMeshAgent>();
        agent.radius = 0.3f; agent.height = 1.7f; agent.speed = 1.1f; agent.angularSpeed = 240f; agent.acceleration = 6f;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
        var w = npc.AddComponent<QuestWalker>();
        var path = Child(parent, "미행길_RetainerPath");
        Clear(path);
        var list = new List<Transform>();
        for (int i = 0; i < pts.Length; i++)
        {
            var p = new GameObject("P" + i).transform; p.SetParent(path, false);
            NavMeshHit hit; Vector3 v = pts[i];
            if (NavMesh.SamplePosition(v, out hit, 8f, NavMesh.AllAreas)) v = hit.position;
            p.position = v; list.Add(p);
        }
        w.points = list.ToArray();
        _log.AppendLine("미행 길 " + pts.Length + "점");
    }

    static void Soldier(Transform parent, string objName, string prefab, float x, float z, float approxY, float rotY, string display, string tag, bool startHidden, float aggro)
    {
        Remove(parent, objName);
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>(Char + prefab + ".prefab");
        if (pf == null) { _log.AppendLine("!! 병사 프리팹 없음 " + prefab); return; }
        var s = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent);
        s.name = objName;
        Vector3 pos = new Vector3(x, Ground(x, z, approxY), z);
        NavMeshHit hit; if (NavMesh.SamplePosition(pos, out hit, 3f, NavMesh.AllAreas)) pos = hit.position;
        s.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, rotY, 0f));
        var ai = s.GetComponent<SoldierAI>();
        ai.displayName = display; ai.patrolRadius = 0f; ai.aggroOnSightRange = aggro; ai.questNoRespawn = true;
        var t = s.AddComponent<QuestTag>(); t.id = tag; t.startHidden = startHidden;
        var g = parent.GetComponent<SoldierGroup>(); if (g != null) g.Collect();
        _log.AppendLine("병사 " + objName + " " + pos.ToString("F1"));
    }

    static void Fire(Transform parent, string name, Vector3 pos, string tag)
    {
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>(FirePrefab);
        if (pf == null) { _log.AppendLine("!! 불 프리팹 없음"); return; }
        var f = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent);
        f.name = name; f.transform.position = pos;
        var t = f.AddComponent<QuestTag>(); t.id = tag; t.startHidden = true;
    }

    static void Building(Transform parent, string name, string prefab, float x, float z, float rotY)
    {
        Remove(parent, name);
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>(Hanok + prefab + ".prefab");
        if (pf == null) { _log.AppendLine("!! 건물 프리팹 없음 " + prefab); return; }
        var b = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent);
        b.name = name;
        b.transform.SetPositionAndRotation(new Vector3(x, 0f, z), Quaternion.Euler(0f, rotY, 0f));
        // 바닥 = 건물 발치 9 점 터레인 높이 중 가장 낮은 곳(떠 보이지 않게)
        Bounds bb = new Bounds(b.transform.position, Vector3.zero); bool first = true;
        foreach (var r in b.GetComponentsInChildren<Renderer>()) { if (first) { bb = r.bounds; first = false; } else bb.Encapsulate(r.bounds); }
        float minY = float.MaxValue;
        for (int i = -1; i <= 1; i++)
            for (int j = -1; j <= 1; j++)
            {
                Vector3 p = new Vector3(bb.center.x + i * bb.extents.x * 0.9f, 0f, bb.center.z + j * bb.extents.z * 0.9f);
                foreach (var t in Terrain.activeTerrains)
                {
                    var o = t.transform.position; var sz = t.terrainData.size;
                    if (p.x < o.x || p.x > o.x + sz.x || p.z < o.z || p.z > o.z + sz.z) continue;
                    minY = Mathf.Min(minY, t.SampleHeight(p) + o.y);
                }
            }
        float baseOffset = b.transform.position.y - bb.min.y;
        if (minY < float.MaxValue) b.transform.position = new Vector3(x, minY + baseOffset - 0.05f, z);
        _log.AppendLine("건물 " + name + " " + b.transform.position.ToString("F1") + " 크기 " + bb.size.ToString("F1"));
    }
}
