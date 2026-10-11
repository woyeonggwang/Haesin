using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 퀘스트 진행(2026-10-07, 사용자 지시 "퀘스트 시스템 + 1막까지 실제 진행" / 2026-10-09 "나머지 퀘스트도"). 씬 루트 `QuestSystem` 에 QuestUI 와 같이.
///   - 한 번에 퀘스트 하나(current), 목표는 순서대로 하나씩(objectiveIndex). 목표가 끝나면 다음 목표, 다 끝나면 onComplete → next(또는 분기 branchNext).
///   - 목표 판정: Talk = F 로 QuestNPC 와 대화(걸어서, talkRange 안) / SinkShips = ShipHealth.OnAnyDied 중 그 진영·마지막 공격이 플레이어 /
///     Reach = QuestMarker 반경 안(배든 걷는 캐릭터든) / BountyAtLeast·AtMost = ReputationSystem 단계 /
///     Escort = 띄운 호위 배(MerchantAI 목적지 모드)가 목적지 마커에 닿음(가라앉으면 목표를 처음부터) /
///     Follow = QuestWalker 를 너무 멀지도 가깝지도 않게 따라가 끝까지 / Defeat = QuestTag 병사가 모두 쓰러짐.
///   - 배 띄우기: 이 목표가 띄운 배 중 살아 있는 배가 keepAlive 보다 적으면(남은 수까지만) 2 초마다 더 띄움(EnemyFleetSpawner.SpawnForQuest).
///     퀘스트 배는 섬 영해 규칙을 무시(ignoreTerritory — 2026-10-09, 예전엔 해월도 영해 안에서 띄우면 영해 끝 약 1.5 km 밖에 생겼음).
///     목표가 끝나 남은 배는 플레이어에서 1000 m 넘게 멀어지면 지움(스포너 정리 목록에 없으므로).
///   - 깃발(flags): 대화 선택지·SetFlag 로 켬. 퀘스트 분기(QuestData.branchFlag)에 씀.
///   - 지도: 지금 목표 자리(대화 상대·도착 마커·띄운 배·호위 배·미행 대상·제압 대상 중 가장 가까운 것)를 MapSystem.questTarget 으로.
///   - QuestTag.startHidden 인 오브젝트는 Start 에서 끔(퀘스트가 SetTagActive 로 켬).
/// 저장/불러오기는 없음(게임 전체에 아직 없음) — Play 를 다시 하면 처음부터.
/// 시험용: F8 = 지금 목표를 끝난 것으로(debugCompleteKey).
/// </summary>
public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    [Header("시작")]
    public QuestData startQuest;
    public bool autoStart = true;
    [Tooltip("게임 시작 뒤 첫 퀘스트까지(초).")] public float startDelay = 4f;
    [Tooltip("퀘스트가 끝나고 다음 퀘스트까지(초).")] public float nextQuestDelay = 4f;

    [Header("키")]
    public KeyCode talkKey = KeyCode.F;
    [Tooltip("시험용: 지금 목표를 바로 끝냄. None 이면 끔.")] public KeyCode debugCompleteKey = KeyCode.F8;

    [Header("참조 (비우면 찾음)")]
    public EnemyFleetSpawner spawner;
    public BoardingSystem boarding;
    public QuestUI ui;

    [Header("상태 (읽기 전용)")]
    public QuestData current;
    public int objectiveIndex;
    public int progress;
    public List<string> flags = new List<string>();
    public List<string> completedIds = new List<string>();

    public System.Action<QuestData> OnQuestStarted;
    public System.Action<QuestData> OnQuestCompleted;
    public System.Action OnObjectiveChanged;

    readonly List<GameObject> _spawned = new List<GameObject>();
    readonly List<GameObject> _leftovers = new List<GameObject>();
    float _nextWaveCheck, _nextCleanup, _startAt = -1f;
    QuestData _pendingNext;
    int _templateCursor;
    bool _completeNext; // 격침 목표가 찬 프레임에는 끝내지 않고 다음 Update 에서 — ShipHealth 가 OnAnyDied 뒤에 현상금을 올리므로, 끝 동작(현상금 0 등)이 그보다 먼저 되면 덮어써짐(2026-10-07 Play 에서 확인: P-2 끝에 관군 현상금 0 → 곧바로 17)
    Transform _playerShip;
    QuestNPC _talkingTo;

    // 호위·미행·제압
    GameObject _escort; MerchantAI _escortAI; ShipHealth _escortHp; int _escortAttackersSpawned; float _restartAt = -1f;
    QuestWalker _walker; bool _followStarted;
    readonly List<SoldierAI> _defeatTargets = new List<SoldierAI>();
    bool _teleporting;

    public QuestObjective CurrentObjective
    {
        get { return current != null && current.objectives != null && objectiveIndex < current.objectives.Length ? current.objectives[objectiveIndex] : null; }
    }

    public bool IsTalking { get { return _talkingTo != null; } }

    void Awake() { if (Instance == null) Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; MapSystem.questTargetActive = false; }
    void OnEnable() { ShipHealth.OnAnyDied += OnShipDied; }
    void OnDisable() { ShipHealth.OnAnyDied -= OnShipDied; }

    void Start()
    {
        if (spawner == null) spawner = FindFirstObjectByType<EnemyFleetSpawner>();
        if (boarding == null) boarding = FindFirstObjectByType<BoardingSystem>();
        if (ui == null) ui = GetComponent<QuestUI>();
        var p = GameObject.FindGameObjectWithTag("Player"); if (p != null) _playerShip = p.transform;
        foreach (var t in Object.FindObjectsByType<QuestTag>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t != null && t.startHidden) t.SetShown(false);
        if (autoStart && startQuest != null) { _pendingNext = startQuest; _startAt = Time.time + startDelay; }
    }

    // ================= 진행 =================

    public void StartQuest(QuestData q)
    {
        if (q == null) return;
        current = q; objectiveIndex = 0; progress = 0;
        Debug.Log("[Quest] 시작 " + q.questId + " " + q.title);
        RunActions(q.onStart);
        if (ui != null) { ui.Toast(q.act + " · " + q.title, 4f); ui.QueueSubtitles(q.introDialogue); }
        if (OnQuestStarted != null) OnQuestStarted(q);
        BeginObjective();
    }

    void BeginObjective()
    {
        progress = 0; _completeNext = false; _restartAt = -1f;
        var o = CurrentObjective;
        if (o == null) { CompleteQuest(); return; }
        RunActions(o.onBegin);
        switch (o.type)
        {
            case QuestObjectiveType.SinkShips: _nextWaveCheck = 0f; TickWaves(o); break;
            case QuestObjectiveType.Escort: BeginEscort(o); break;
            case QuestObjectiveType.Follow: BeginFollow(o); break;
            case QuestObjectiveType.Defeat: BeginDefeat(o); break;
        }
        if (OnObjectiveChanged != null) OnObjectiveChanged();
    }

    /// <summary>지금 목표를 끝내고 다음으로(시험용으로도 부름).</summary>
    public void CompleteObjective()
    {
        if (current == null) return;
        var o = CurrentObjective;
        Debug.Log("[Quest] 목표 끝 " + current.questId + " #" + objectiveIndex + (o != null ? " " + o.description : ""));
        _leftovers.AddRange(_spawned); _spawned.Clear();
        EndEscort(false);
        _walker = null; _followStarted = false; _defeatTargets.Clear();
        if (o != null) RunActions(o.onEnd);
        objectiveIndex++;
        if (ui != null && CurrentObjective != null) ui.Toast("새 목표: " + CurrentObjective.description, 3f);
        BeginObjective();
    }

    void CompleteQuest()
    {
        var q = current;
        if (q == null) return;
        Debug.Log("[Quest] 완료 " + q.questId + " " + q.title);
        RunActions(q.onComplete);
        if (!completedIds.Contains(q.questId)) completedIds.Add(q.questId);
        if (ui != null) { ui.Toast("완료 — " + q.title, 3.5f); ui.QueueSubtitles(q.outroDialogue); }
        current = null;
        if (OnQuestCompleted != null) OnQuestCompleted(q);
        if (OnObjectiveChanged != null) OnObjectiveChanged();
        QuestData n = (!string.IsNullOrEmpty(q.branchFlag) && HasFlag(q.branchFlag) && q.branchNext != null) ? q.branchNext : q.next;
        if (n != null) { _pendingNext = n; _startAt = Time.time + nextQuestDelay; }
    }

    public bool HasFlag(string f) { return !string.IsNullOrEmpty(f) && flags.Contains(f); }
    public void SetFlag(string f) { if (!string.IsNullOrEmpty(f) && !flags.Contains(f)) { flags.Add(f); Debug.Log("[Quest] 깃발 " + f); } }

    void RunActions(QuestAction[] acts)
    {
        if (acts == null) return;
        foreach (var a in acts)
        {
            if (a == null) continue;
            switch (a.type)
            {
                case QuestActionType.Message: if (ui != null) ui.Toast(a.text, 4f); break;
                case QuestActionType.SetBountyLevel:
                    // text "atleast" = 지금 단계가 value 보다 낮을 때만 올림(내리지는 않음)
                    if (ReputationSystem.Instance != null && (a.text != "atleast" || ReputationSystem.Level(a.faction) < a.value)) ReputationSystem.Instance.SetLevel(a.faction, a.value);
                    break;
                case QuestActionType.SpawnShips:
                {
                    var tpl = FindTemplate(a.template);
                    var m = string.IsNullOrEmpty(a.marker) ? null : QuestMarker.Find(a.marker);
                    if (tpl != null && spawner != null) _leftovers.AddRange(spawner.SpawnForQuest(tpl, a.value, a.distance, 45f, a.ignoreTerritory, m != null ? m.transform : null, a.factionOverride));
                    else Debug.LogWarning("[Quest] 배를 띄우지 못함: " + a.template);
                    break;
                }
                case QuestActionType.SetFlag: SetFlag(a.text); break;
                case QuestActionType.GiveGold:
                    if (PlayerResources.Instance != null) PlayerResources.Instance.Add(0, 0, a.value);
                    if (ui != null) ui.Toast("금화 " + a.value + " 냥을 받았습니다", 3f);
                    break;
                case QuestActionType.SetTagActive:
                {
                    var list = QuestTag.FindAll(a.text);
                    if (list.Count == 0) Debug.LogWarning("[Quest] QuestTag 없음: " + a.text);
                    foreach (var t in list) t.SetShown(a.value != 0);
                    break;
                }
                case QuestActionType.TeleportPlayer:
                {
                    var m = QuestMarker.Find(a.text);
                    if (m == null) { Debug.LogWarning("[Quest] 옮길 마커 없음: " + a.text); break; }
                    StartCoroutine(TeleportRoutine(m.transform));
                    break;
                }
                case QuestActionType.SoldierAggro:
                    foreach (var t in QuestTag.FindAll(a.text))
                    {
                        foreach (var ai in t.GetComponentsInChildren<SoldierAI>(true))
                        {
                            ai.aggroOnSightRange = a.distance;
                            if (a.value != 0 && ai.isActiveAndEnabled) ai.BecomeHostile();
                        }
                    }
                    break;
                case QuestActionType.Subtitles: if (ui != null) ui.QueueSubtitles(a.lines); break;
            }
        }
    }

    /// <summary>걷는 플레이어를 마커 자리로(화면을 잠깐 어둡게). 배에 타고 있으면 하지 않음. 궁궐(PalaceVista 구역) 안이면 궁을 켬.</summary>
    IEnumerator TeleportRoutine(Transform to)
    {
        if (boarding == null || !boarding.IsOnFoot || boarding.onFootRoot == null) { Debug.LogWarning("[Quest] 배에 타고 있어 옮기지 않음"); yield break; }
        _teleporting = true;
        if (ui != null) ui.FadeTo(1f, 0.5f);
        yield return new WaitForSecondsRealtime(0.55f);
        var vista = PalaceVista.Instance;
        if (vista != null && !vista.palaceActive && vista.zoneMin.x <= to.position.x && to.position.x <= vista.zoneMax.x && vista.zoneMin.y <= to.position.z && to.position.z <= vista.zoneMax.y)
        {
            vista.Show();
            float until = Time.realtimeSinceStartup + 4f;
            while (vista.busy && Time.realtimeSinceStartup < until) yield return null;
        }
        var root = boarding.onFootRoot.transform;
        var cc = root.GetComponent<CharacterController>();
        Vector3 pos = to.position;
        RaycastHit hit;
        if (Physics.Raycast(pos + Vector3.up * 3f, Vector3.down, out hit, 20f, ~0, QueryTriggerInteraction.Ignore)) pos = hit.point + Vector3.up * 0.05f;
        if (cc != null) cc.enabled = false;
        root.SetPositionAndRotation(pos, Quaternion.Euler(0f, to.eulerAngles.y, 0f));
        if (cc != null) cc.enabled = true;
        boarding.onFootRoot.SetYaw(to.eulerAngles.y);
        Physics.SyncTransforms();
        yield return new WaitForSecondsRealtime(0.3f);
        if (ui != null) ui.FadeTo(0f, 0.7f);
        _teleporting = false;
    }

    // ================= 매 프레임 =================

    void Update()
    {
        if (_pendingNext != null && _startAt >= 0f && Time.time >= _startAt && !IsTalking)
        {
            var q = _pendingNext; _pendingNext = null; _startAt = -1f;
            StartQuest(q);
        }

        HandleTalkInput();

        if (_completeNext && current != null) { _completeNext = false; CompleteObjective(); }

        if (current != null)
        {
            if (debugCompleteKey != KeyCode.None && Input.GetKeyDown(debugCompleteKey) && !IsTalking) CompleteObjective();
            var o = CurrentObjective;
            if (o != null) TickObjective(o);
        }

        if (Time.time >= _nextCleanup) { _nextCleanup = Time.time + 5f; CleanupLeftovers(); }
        UpdateMapTarget();
    }

    void TickObjective(QuestObjective o)
    {
        switch (o.type)
        {
            case QuestObjectiveType.Reach:
            {
                if (_teleporting) return;
                var m = QuestMarker.Find(o.targetId);
                if (m == null) return;
                float r = o.radius > 0f ? o.radius : m.radius;
                Vector3 d = PlayerPosition() - m.transform.position; d.y = 0f;
                if (d.sqrMagnitude <= r * r) CompleteObjective();
                break;
            }
            case QuestObjectiveType.BountyAtLeast:
                if (ReputationSystem.Level(o.faction) >= o.count) CompleteObjective();
                break;
            case QuestObjectiveType.BountyAtMost:
                if (ReputationSystem.Level(o.faction) <= o.count) CompleteObjective();
                break;
            case QuestObjectiveType.SinkShips:
                if (Time.time >= _nextWaveCheck) { _nextWaveCheck = Time.time + 2f; TickWaves(o); }
                break;
            case QuestObjectiveType.Escort: TickEscort(o); break;
            case QuestObjectiveType.Follow: TickFollow(o); break;
            case QuestObjectiveType.Defeat:
            {
                if (_defeatTargets.Count == 0) return;
                foreach (var s in _defeatTargets) if (s != null && !s.IsDead) return;
                CompleteObjective();
                break;
            }
        }
    }

    void OnShipDied(ShipHealth h)
    {
        var o = CurrentObjective;
        if (o == null || o.type != QuestObjectiveType.SinkShips || h == null) return;
        var f = h.GetComponent<ShipFaction>();
        if (f == null || f.faction != o.faction || !h.hasLastAttacker || h.lastAttackerFaction != Faction.Pirate) return;
        progress++;
        if (OnObjectiveChanged != null) OnObjectiveChanged();
        if (progress >= o.count) _completeNext = true;
    }

    /// <summary>띄운 배가 keepAlive 보다 적으면(남은 격침 수까지만) 더 띄움.</summary>
    void TickWaves(QuestObjective o)
    {
        if (o.spawnTemplates == null || o.spawnTemplates.Length == 0 || spawner == null) return;
        _spawned.RemoveAll(s => s == null);
        int alive = 0;
        foreach (var s in _spawned) { var h = s.GetComponent<ShipHealth>(); if (h == null || !h.isDead) alive++; }
        int want = Mathf.Min(Mathf.Max(1, o.keepAlive), o.count - progress);
        if (alive >= want) return;
        int add = want - alive;
        var m = string.IsNullOrEmpty(o.marker) ? null : QuestMarker.Find(o.marker);
        for (int i = 0; i < add; i++)
        {
            string tn = o.spawnTemplates[_templateCursor++ % o.spawnTemplates.Length];
            var tpl = FindTemplate(tn);
            if (tpl == null) { Debug.LogWarning("[Quest] 원본 배 없음: " + tn); continue; }
            _spawned.AddRange(spawner.SpawnForQuest(tpl, 1, o.spawnDistance, 50f, o.ignoreTerritory, m != null ? m.transform : null, -1));
        }
    }

    // ---------------- 호위 ----------------

    void BeginEscort(QuestObjective o)
    {
        EndEscort(true);
        _escortAttackersSpawned = 0;
        var tpl = FindTemplate(o.escortTemplate);
        var start = QuestMarker.Find(o.marker);
        var dest = QuestMarker.Find(o.targetId);
        if (tpl == null || start == null || dest == null || spawner == null) { Debug.LogWarning("[Quest] 호위 준비 실패: " + o.escortTemplate + " / " + o.marker + " / " + o.targetId); return; }
        var list = spawner.SpawnForQuest(tpl, 1, 0f, 15f, true, start.transform, (int)o.escortFaction);
        if (list.Count == 0 || list[0] == null) return;
        _escort = list[0];
        _escort.name = "Quest_Escort";
        _escortHp = _escort.GetComponent<ShipHealth>();
        _escortAI = _escort.GetComponent<MerchantAI>();
        Vector3 to = dest.transform.position - _escort.transform.position; to.y = 0f;
        if (to.sqrMagnitude > 1f) _escort.transform.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
        if (_escortAI != null)
        {
            _escortAI.hasDestination = true; _escortAI.destination = dest.transform.position;
            _escortAI.destinationSpeed = o.escortSpeed; _escortAI.arriveRadius = dest.radius;
        }
        _nextWaveCheck = Time.time + 8f; // 출발하고 조금 뒤부터 습격
    }

    void TickEscort(QuestObjective o)
    {
        if (_restartAt >= 0f) { if (Time.time >= _restartAt) { _restartAt = -1f; _leftovers.AddRange(_spawned); _spawned.Clear(); BeginEscort(o); } return; }
        if (_escort == null) return;
        if (_escortHp != null && (_escortHp.isDead || _escortHp.isSinking))
        {
            if (ui != null) ui.Toast("호위할 배가 가라앉았습니다 — 다시", 3.5f);
            _restartAt = Time.time + 6f;
            EndEscort(false);
            return;
        }
        var dest = QuestMarker.Find(o.targetId);
        bool arrived = _escortAI != null ? _escortAI.Arrived : (dest != null && Flat(_escort.transform.position - dest.transform.position).magnitude <= dest.radius);
        if (arrived) { CompleteObjective(); return; }
        // 습격
        if (o.spawnTemplates != null && o.spawnTemplates.Length > 0 && Time.time >= _nextWaveCheck && _escortAttackersSpawned < o.count)
        {
            _nextWaveCheck = Time.time + 3f;
            _spawned.RemoveAll(s => s == null);
            int alive = 0;
            foreach (var s in _spawned) { var h = s.GetComponent<ShipHealth>(); if (h == null || !h.isDead) alive++; }
            int want = Mathf.Min(Mathf.Max(1, o.keepAlive), o.count - _escortAttackersSpawned);
            for (int i = alive; i < want && _escortAttackersSpawned < o.count; i++)
            {
                var tpl = FindTemplate(o.spawnTemplates[_templateCursor++ % o.spawnTemplates.Length]);
                if (tpl == null) continue;
                _spawned.AddRange(spawner.SpawnForQuest(tpl, 1, o.spawnDistance, 40f, true, _escort.transform, -1));
                _escortAttackersSpawned++;
            }
        }
    }

    /// <summary>호위 배 정리. destroyNow 면 바로 지우고, 아니면 남은 배 목록으로(멀어지면 지움).</summary>
    void EndEscort(bool destroyNow)
    {
        if (_escort != null)
        {
            if (_escortAI != null) _escortAI.hasDestination = false;
            _escort.name = "Quest_Escort_Done";
            if (destroyNow) Destroy(_escort); else _leftovers.Add(_escort);
        }
        _escort = null; _escortAI = null; _escortHp = null;
    }

    // ---------------- 미행 ----------------

    void BeginFollow(QuestObjective o)
    {
        _walker = null; _followStarted = false;
        foreach (var t in QuestTag.FindAll(o.targetId))
        {
            t.SetShown(true);
            var w = t.GetComponent<QuestWalker>();
            if (w != null) { _walker = w; break; }
        }
        if (_walker == null) { Debug.LogWarning("[Quest] 미행 대상 없음: " + o.targetId); return; }
        _walker.ResetToStart();
    }

    void TickFollow(QuestObjective o)
    {
        if (_walker == null || _teleporting) return;
        float d = Flat(PlayerPosition() - _walker.transform.position).magnitude;
        if (!_followStarted)
        {
            if (d <= o.followStartRange) { _followStarted = true; _walker.Begin(); if (ui != null) ui.Toast("들키지 않게 따라가십시오", 3f); }
            return;
        }
        if (_walker.Arrived)
        {
            if (ui != null) ui.QueueSubtitles(o.dialogue);
            CompleteObjective();
            return;
        }
        float maxD = o.radius > 0f ? o.radius : 45f;
        if (d > maxD || d < o.minDistance)
        {
            if (ui != null) ui.Toast(d > maxD ? "놓쳤습니다 — 처음부터 다시" : "들켰습니다 — 처음부터 다시", 3.5f);
            _walker.ResetToStart(); _followStarted = false;
        }
    }

    // ---------------- 제압 ----------------

    void BeginDefeat(QuestObjective o)
    {
        _defeatTargets.Clear();
        foreach (var t in QuestTag.FindAll(o.targetId))
        {
            t.SetShown(true);
            foreach (var ai in t.GetComponentsInChildren<SoldierAI>(true)) { ai.questNoRespawn = true; if (!_defeatTargets.Contains(ai)) _defeatTargets.Add(ai); }
        }
        if (_defeatTargets.Count == 0) Debug.LogWarning("[Quest] 제압 대상 없음: " + o.targetId);
    }

    void CleanupLeftovers()
    {
        _leftovers.RemoveAll(s => s == null);
        Vector3 p = PlayerPosition();
        for (int i = _leftovers.Count - 1; i >= 0; i--)
        {
            var s = _leftovers[i];
            if ((s.transform.position - p).sqrMagnitude > 1000f * 1000f) { Destroy(s); _leftovers.RemoveAt(i); }
        }
    }

    /// <summary>추적 UI 에 덧붙이는 목표 상태(호위 배 체력, 미행 거리, 남은 제압 대상).</summary>
    public string ObjectiveStatus()
    {
        var o = CurrentObjective;
        if (o == null) return "";
        switch (o.type)
        {
            case QuestObjectiveType.Escort:
                if (_restartAt >= 0f) return "(다시 준비 중)";
                if (_escortHp != null) return "(호위 배 " + Mathf.RoundToInt(100f * _escortHp.currentHealth / Mathf.Max(1f, _escortHp.maxHealth)) + "%)";
                return "";
            case QuestObjectiveType.Follow:
                if (_walker == null) return "";
                if (!_followStarted) return "(가까이 가면 움직임)";
                return "(" + Mathf.RoundToInt(Flat(PlayerPosition() - _walker.transform.position).magnitude) + " m)";
            case QuestObjectiveType.Defeat:
            {
                int left = 0; foreach (var s in _defeatTargets) if (s != null && !s.IsDead) left++;
                return _defeatTargets.Count > 1 ? "(남은 " + left + ")" : "";
            }
        }
        return "";
    }

    // ================= 대화 =================

    /// <summary>걸어 다니는 중 talkRange 안의 가장 가까운 NPC(없으면 null).</summary>
    public QuestNPC NearbyNPC()
    {
        if (boarding == null || !boarding.IsOnFoot || boarding.onFootRoot == null) return null;
        Vector3 p = boarding.onFootRoot.transform.position;
        QuestNPC best = null; float bd = float.MaxValue;
        foreach (var n in QuestNPC.All)
        {
            if (n == null || !n.isActiveAndEnabled) continue;
            float d = Vector3.Distance(p, n.transform.position);
            if (d <= n.talkRange && d < bd) { bd = d; best = n; }
        }
        return best;
    }

    /// <summary>이 NPC 가 지금 목표의 대화 상대인가.</summary>
    public bool IsQuestTarget(QuestNPC n)
    {
        var o = CurrentObjective;
        return n != null && o != null && o.type == QuestObjectiveType.Talk && o.targetId == n.npcId;
    }

    void HandleTalkInput()
    {
        if (ui == null) return;
        if (IsTalking)
        {
            // 너무 멀어지면 대화를 접음(퀘스트 대화는 끝나지 않은 채로 — 다시 말을 걸면 처음부터)
            if (boarding != null && (!boarding.IsOnFoot || Vector3.Distance(boarding.onFootRoot.transform.position, _talkingTo.transform.position) > _talkingTo.talkRange + 4f))
            { ui.CloseDialogue(); EndTalk(); return; }
            if (Input.GetKeyDown(talkKey)) ui.Advance();
            return;
        }
        if (GameMenuPause.IsPaused) return;
        var npc = NearbyNPC();
        ui.SetPrompt(npc != null ? "F  대화 — " + npc.displayName + (IsQuestTarget(npc) ? "  ◆" : "") : null);
        if (npc != null && Input.GetKeyDown(talkKey)) BeginTalk(npc);
    }

    /// <summary>NPC 와 대화를 시작(시험용으로 MCP 에서도 부름).</summary>
    public void BeginTalk(QuestNPC npc)
    {
        if (npc == null || ui == null || IsTalking) return;
        _talkingTo = npc;
        if (boarding != null && boarding.onFootRoot != null) npc.FaceTowards(boarding.onFootRoot.transform);
        ui.SetPrompt(null);
        var o = CurrentObjective;
        if (IsQuestTarget(npc))
        {
            int qi = objectiveIndex; QuestData q = current;
            ui.ShowDialogue(o.dialogue, o.choices, choice =>
            {
                EndTalk();
                if (current != q || objectiveIndex != qi) return; // 그새 바뀜
                if (choice >= 0 && o.choiceFlags != null && choice < o.choiceFlags.Length) SetFlag(o.choiceFlags[choice]);
                CompleteObjective();
            });
        }
        else
        {
            DialogueLine[] lines = npc.idleLines != null && npc.idleLines.Length > 0
                ? new[] { npc.idleLines[Random.Range(0, npc.idleLines.Length)] }
                : new[] { new DialogueLine(npc.displayName, "…") };
            ui.ShowDialogue(lines, null, choice => EndTalk());
        }
    }

    void EndTalk()
    {
        if (_talkingTo != null) _talkingTo.FaceTowards(null);
        _talkingTo = null;
    }

    // ================= 도우미 =================

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    /// <summary>걷는 중이면 캐릭터, 아니면 플레이어 배 위치.</summary>
    public Vector3 PlayerPosition()
    {
        if (boarding != null && boarding.IsOnFoot && boarding.onFootRoot != null) return boarding.onFootRoot.transform.position;
        if (_playerShip == null) { var p = GameObject.FindGameObjectWithTag("Player"); if (p != null) _playerShip = p.transform; }
        return _playerShip != null ? _playerShip.position : Vector3.zero;
    }

    /// <summary>지금 목표가 가리키는 자리와 이름. 없으면 false.</summary>
    public bool TryGetTarget(out Vector3 pos, out string label)
    {
        pos = Vector3.zero; label = "";
        var o = CurrentObjective;
        if (o == null) return false;
        switch (o.type)
        {
            case QuestObjectiveType.Talk:
            {
                var n = QuestNPC.Find(o.targetId);
                if (n == null) return false;
                pos = n.transform.position; label = n.displayName; return true;
            }
            case QuestObjectiveType.Reach:
            {
                var m = QuestMarker.Find(o.targetId);
                if (m == null) return false;
                pos = m.transform.position; label = m.displayName; return true;
            }
            case QuestObjectiveType.SinkShips:
            {
                Vector3 p = PlayerPosition(); float bd = float.MaxValue; bool found = false;
                foreach (var s in _spawned)
                {
                    if (s == null) continue;
                    var h = s.GetComponent<ShipHealth>(); if (h != null && h.isDead) continue;
                    float d = (s.transform.position - p).sqrMagnitude;
                    if (d < bd) { bd = d; pos = s.transform.position; found = true; }
                }
                if (!found && !string.IsNullOrEmpty(o.marker)) { var m = QuestMarker.Find(o.marker); if (m != null) { pos = m.transform.position; found = true; } }
                label = "표적";
                return found;
            }
            case QuestObjectiveType.Escort:
                if (_escort == null) return false;
                pos = _escort.transform.position; label = "호위할 배"; return true;
            case QuestObjectiveType.Follow:
                if (_walker == null) return false;
                pos = _walker.transform.position; label = "미행 대상"; return true;
            case QuestObjectiveType.Defeat:
            {
                Vector3 p = PlayerPosition(); float bd = float.MaxValue; bool found = false;
                foreach (var s in _defeatTargets)
                {
                    if (s == null || s.IsDead) continue;
                    float d = (s.transform.position - p).sqrMagnitude;
                    if (d < bd) { bd = d; pos = s.transform.position; found = true; label = s.displayName; }
                }
                return found;
            }
        }
        return false;
    }

    void UpdateMapTarget()
    {
        Vector3 pos; string label;
        bool on = TryGetTarget(out pos, out label);
        MapSystem.questTargetActive = on;
        if (on) { MapSystem.questTarget = pos; MapSystem.questTargetLabel = label; }
    }

    /// <summary>씬 루트의 원본 배(이름). 꺼져 있어도 찾음.</summary>
    static GameObject FindTemplate(string n)
    {
        if (string.IsNullOrEmpty(n)) return null;
        foreach (var sf in Resources.FindObjectsOfTypeAll<ShipFaction>())
        {
            if (sf == null || !sf.gameObject.scene.IsValid() || sf.transform.parent != null) continue;
            if (sf.gameObject.name == n) return sf.gameObject;
        }
        return null;
    }
}
