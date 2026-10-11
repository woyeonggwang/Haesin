using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 퀘스트 화면(2026-10-07). QuestManager 와 같은 오브젝트. 코드로 uGUI 를 만든다(UIKit).
///   - 추적(오른쪽 위): 막·제목, 지금 목표 글 + 진행(격침 3/10, 현상금 단계) + 목표까지 거리.
///   - 알림(위 가운데): 퀘스트 시작·완료·새 목표·현상금 단계 변화, 몇 초 뒤 사라짐.
///   - 자막(아래 가운데): 퀘스트 시작·끝 대사가 한 줄씩 흘러감(멈추지 않음).
///   - 대화창(아래 가운데): F 로 한 줄씩 넘김. 선택지가 있으면 마지막 줄 뒤 버튼(마우스 — 그동안 커서 보임).
///   - 안내(가운데 아래): "F 대화 — 이름".
/// 전체화면 메뉴(GameMenuPause)·지도가 열려 있으면 숨김.
/// </summary>
public class QuestUI : MonoBehaviour
{
    public QuestManager manager;
    [Tooltip("자막 한 줄이 떠 있는 시간(초, 글자 수에 따라 조금 늘어남).")] public float subtitleSeconds = 3.5f;

    Canvas _canvas;
    RectTransform _tracker; Text _trTitle, _trObjective, _trDistance;
    Text _toast; float _toastUntil;
    RectTransform _subBox; Text _subSpeaker, _subText; readonly Queue<DialogueLine> _subQueue = new Queue<DialogueLine>(); float _subUntil;
    RectTransform _dlgBox; Text _dlgSpeaker, _dlgText, _dlgHint; RectTransform _choiceHolder; readonly List<Button> _choiceButtons = new List<Button>();
    Text _prompt;
    Image _fade; float _fadeAlpha, _fadeTarget, _fadeSpeed = 2f;
    DialogueLine[] _lines; string[] _choices; System.Action<int> _onDone; int _lineIndex; bool _choosing;
    bool _prevCursorVisible; CursorLockMode _prevCursorLock;
    MapSystem _map;

    static readonly Color Gold = new Color(1f, 0.84f, 0.42f);
    static readonly Color Ivory = new Color(0.95f, 0.93f, 0.86f);
    static readonly Color Panel = new Color(0.04f, 0.05f, 0.07f, 0.82f);

    public bool DialogueOpen { get { return _lines != null; } }

    void Awake() { if (manager == null) manager = GetComponent<QuestManager>(); }

    void Start()
    {
        Build();
        if (ReputationSystem.Instance != null) ReputationSystem.Instance.OnLevelChanged += OnBountyLevel;
        _map = FindFirstObjectByType<MapSystem>();
    }

    void OnDestroy() { if (ReputationSystem.Instance != null) ReputationSystem.Instance.OnLevelChanged -= OnBountyLevel; }

    void OnBountyLevel(Faction f, int from, int to)
    {
        string who = f == Faction.Navy ? "관군" : "일본";
        if (to > from) Toast(who + " 현상금 " + to + "단계" + (from == 0 ? " — " + who + "이 공격합니다" : ""), 3f);
        else if (to == 0) Toast(who + " 현상금이 사라졌습니다", 3f);
        else Toast(who + " 현상금 " + to + "단계로 내려감", 2.5f);
    }

    // ================= 만들기 =================

    void Build()
    {
        _canvas = UIKit.NewCanvas("QuestHUD", 110);
        _canvas.transform.SetParent(transform, false);
        var root = _canvas.transform;

        // 추적
        _tracker = UIKit.NewRect("Tracker", root);
        UIKit.Place(_tracker, new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(380f, 96f)); // 오른쪽 위(미니맵은 왼쪽 위)
        var trBg = UIKit.NewImage("Bg", _tracker, new Color(0.04f, 0.05f, 0.07f, 0.55f)); trBg.raycastTarget = false; UIKit.Stretch(trBg.rectTransform, 0f);
        var bar = UIKit.NewImage("Bar", _tracker, Gold); bar.raycastTarget = false;
        bar.rectTransform.anchorMin = new Vector2(0f, 0f); bar.rectTransform.anchorMax = new Vector2(0f, 1f); bar.rectTransform.pivot = new Vector2(0f, 0.5f);
        bar.rectTransform.sizeDelta = new Vector2(3f, 0f); bar.rectTransform.anchoredPosition = Vector2.zero;
        _trTitle = Txt("Title", _tracker, 17, TextAnchor.UpperLeft, Gold, new Vector2(14f, -8f), new Vector2(356f, 22f));
        _trObjective = Txt("Objective", _tracker, 15, TextAnchor.UpperLeft, Ivory, new Vector2(14f, -34f), new Vector2(356f, 40f));
        _trDistance = Txt("Distance", _tracker, 13, TextAnchor.UpperLeft, new Color(0.75f, 0.78f, 0.8f), new Vector2(14f, -72f), new Vector2(356f, 18f));

        // 알림
        _toast = UIKit.NewText("Toast", root, "", 26, TextAnchor.MiddleCenter, Ivory);
        UIKit.Place(_toast.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(1100f, 40f));
        _toast.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.85f);
        _toast.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2f, -2f);

        // 자막
        _subBox = UIKit.NewRect("Subtitle", root);
        UIKit.Place(_subBox, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(1000f, 60f));
        _subSpeaker = Txt("Speaker", _subBox, 17, TextAnchor.UpperCenter, Gold, new Vector2(0f, 0f), new Vector2(1000f, 22f), true);
        _subText = Txt("Text", _subBox, 19, TextAnchor.UpperCenter, Ivory, new Vector2(0f, -22f), new Vector2(1000f, 40f), true);
        _subSpeaker.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);
        _subText.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);
        _subBox.gameObject.SetActive(false);

        // 대화창
        _dlgBox = UIKit.NewRect("Dialogue", root);
        UIKit.Place(_dlgBox, new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(980f, 150f));
        var dbg = UIKit.NewImage("Bg", _dlgBox, Panel); dbg.raycastTarget = false; UIKit.Stretch(dbg.rectTransform, 0f);
        var line = UIKit.NewImage("Line", _dlgBox, new Color(Gold.r, Gold.g, Gold.b, 0.7f)); line.raycastTarget = false;
        line.rectTransform.anchorMin = new Vector2(0f, 1f); line.rectTransform.anchorMax = new Vector2(1f, 1f); line.rectTransform.pivot = new Vector2(0.5f, 1f);
        line.rectTransform.sizeDelta = new Vector2(0f, 2f); line.rectTransform.anchoredPosition = Vector2.zero;
        _dlgSpeaker = Txt("Speaker", _dlgBox, 20, TextAnchor.UpperLeft, Gold, new Vector2(28f, -14f), new Vector2(900f, 26f));
        _dlgText = Txt("Text", _dlgBox, 20, TextAnchor.UpperLeft, Ivory, new Vector2(28f, -46f), new Vector2(924f, 76f));
        _dlgHint = Txt("Hint", _dlgBox, 13, TextAnchor.LowerRight, new Color(0.7f, 0.72f, 0.75f), new Vector2(-20f, 10f), new Vector2(300f, 18f));
        var hrt = _dlgHint.rectTransform; hrt.anchorMin = hrt.anchorMax = hrt.pivot = new Vector2(1f, 0f); hrt.anchoredPosition = new Vector2(-20f, 10f);
        _choiceHolder = UIKit.NewRect("Choices", _dlgBox);
        UIKit.Place(_choiceHolder, new Vector2(0.5f, 1f), new Vector2(0f, 12f), new Vector2(980f, 10f));
        _dlgBox.gameObject.SetActive(false);

        // 안내
        _prompt = UIKit.NewText("Prompt", root, "", 18, TextAnchor.MiddleCenter, Ivory);
        UIKit.Place(_prompt.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 300f), new Vector2(600f, 28f));
        _prompt.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);
        _prompt.gameObject.SetActive(false);

        // 화면 어둡게(장면 옮김, 2026-10-09) — 맨 위
        _fade = UIKit.NewImage("Fade", root, new Color(0f, 0f, 0f, 0f)); _fade.raycastTarget = false; UIKit.Stretch(_fade.rectTransform, 0f);
        _fade.transform.SetAsLastSibling();
        _fade.gameObject.SetActive(false);
    }

    /// <summary>화면을 seconds 동안 검게(target 1) 또는 밝게(target 0).</summary>
    public void FadeTo(float target, float seconds)
    {
        _fadeTarget = Mathf.Clamp01(target);
        _fadeSpeed = seconds > 0.01f ? 1f / seconds : 100f;
    }

    public float FadeAlpha { get { return _fadeAlpha; } }

    Text Txt(string name, RectTransform parent, int size, TextAnchor align, Color c, Vector2 pos, Vector2 sizeDelta, bool centerAnchor = false)
    {
        var t = UIKit.NewText(name, parent, "", size, align, c);
        var rt = t.rectTransform;
        if (centerAnchor) { rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f); }
        else { rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f); }
        rt.anchoredPosition = pos; rt.sizeDelta = sizeDelta;
        return t;
    }

    // ================= 바깥에서 부르는 것 =================

    public void Toast(string s, float seconds)
    {
        if (_toast == null || string.IsNullOrEmpty(s)) return;
        _toast.text = s; _toastUntil = Time.unscaledTime + seconds;
    }

    public void QueueSubtitles(DialogueLine[] lines)
    {
        if (lines == null) return;
        foreach (var l in lines) if (l != null && !string.IsNullOrEmpty(l.text)) _subQueue.Enqueue(l);
    }

    public void SetPrompt(string s)
    {
        if (_prompt == null) return;
        bool on = !string.IsNullOrEmpty(s);
        if (_prompt.gameObject.activeSelf != on) _prompt.gameObject.SetActive(on);
        if (on && _prompt.text != s) _prompt.text = s;
    }

    /// <summary>대화창을 연다. 끝나면 onDone(선택지 순번, 선택지가 없으면 −1).</summary>
    public void ShowDialogue(DialogueLine[] lines, string[] choices, System.Action<int> onDone)
    {
        _lines = lines != null && lines.Length > 0 ? lines : new[] { new DialogueLine("", "…") };
        _choices = choices != null && choices.Length > 0 ? choices : null;
        _onDone = onDone; _lineIndex = 0; _choosing = false;
        _subQueue.Clear(); _subBox.gameObject.SetActive(false); // 대화 중엔 자막을 비움
        _dlgBox.gameObject.SetActive(true);
        ShowLine();
    }

    /// <summary>다음 줄(F). 선택지를 고르는 중이면 아무것도 안 함.</summary>
    public void Advance()
    {
        if (_lines == null || _choosing) return;
        _lineIndex++;
        if (_lineIndex < _lines.Length) { ShowLine(); return; }
        if (_choices != null) { ShowChoices(); return; }
        Finish(-1);
    }

    /// <summary>선택지 고르기(버튼, 시험용으로 MCP 에서도 부름).</summary>
    public void Choose(int i) { if (_choosing) Finish(i); }

    public void CloseDialogue() { if (_lines != null) { ClearChoices(); _lines = null; _dlgBox.gameObject.SetActive(false); } }

    void ShowLine()
    {
        var l = _lines[_lineIndex];
        _dlgSpeaker.text = l.speaker;
        _dlgText.text = l.text;
        bool last = _lineIndex == _lines.Length - 1;
        _dlgHint.text = last && _choices == null ? "F  닫기" : "F  다음 ▶";
    }

    void ShowChoices()
    {
        _choosing = true;
        _dlgHint.text = "마우스로 고르기";
        ClearChoices();
        for (int i = 0; i < _choices.Length; i++)
        {
            int idx = i;
            var b = UIKit.NewButton("Choice" + i, _choiceHolder, (i + 1) + ".  " + _choices[i], 18, new Color(0.10f, 0.11f, 0.14f, 0.95f), Ivory, () => Choose(idx));
            var rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(620f, 38f);
            rt.anchoredPosition = new Vector2(0f, (_choices.Length - 1 - i) * 44f);
            _choiceButtons.Add(b);
        }
        _prevCursorVisible = Cursor.visible; _prevCursorLock = Cursor.lockState;
        Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
    }

    void ClearChoices()
    {
        foreach (var b in _choiceButtons) if (b != null) Destroy(b.gameObject);
        if (_choiceButtons.Count > 0) { Cursor.visible = _prevCursorVisible; Cursor.lockState = _prevCursorLock; }
        _choiceButtons.Clear();
    }

    void Finish(int choice)
    {
        ClearChoices();
        _choosing = false;
        var cb = _onDone; _onDone = null; _lines = null;
        _dlgBox.gameObject.SetActive(false);
        if (cb != null) cb(choice);
    }

    // ================= 매 프레임 =================

    void Update()
    {
        if (_canvas == null) return;
        bool hidden = GameMenuPause.IsPaused || (_map != null && _map.isOpen);
        if (_canvas.enabled == hidden) _canvas.enabled = !hidden;

        // 알림
        bool toastOn = Time.unscaledTime < _toastUntil;
        if (_toast.gameObject.activeSelf != toastOn) _toast.gameObject.SetActive(toastOn);
        if (toastOn) { var c = _toast.color; c.a = Mathf.Clamp01((_toastUntil - Time.unscaledTime) / 0.6f); _toast.color = c; }

        // 자막
        if (!DialogueOpen)
        {
            if (Time.unscaledTime >= _subUntil)
            {
                if (_subQueue.Count > 0)
                {
                    var l = _subQueue.Dequeue();
                    _subSpeaker.text = l.speaker; _subText.text = l.text;
                    _subUntil = Time.unscaledTime + subtitleSeconds + l.text.Length * 0.04f;
                    _subBox.gameObject.SetActive(true);
                }
                else if (_subBox.gameObject.activeSelf) _subBox.gameObject.SetActive(false);
            }
        }

        // 화면 어둡게
        if (_fade != null)
        {
            _fadeAlpha = Mathf.MoveTowards(_fadeAlpha, _fadeTarget, Time.unscaledDeltaTime * _fadeSpeed);
            bool fOn = _fadeAlpha > 0.001f;
            if (_fade.gameObject.activeSelf != fOn) _fade.gameObject.SetActive(fOn);
            if (fOn) _fade.color = new Color(0f, 0f, 0f, _fadeAlpha);
        }

        UpdateTracker();
    }

    void UpdateTracker()
    {
        var q = manager != null ? manager.current : null;
        var o = manager != null ? manager.CurrentObjective : null;
        bool on = q != null && o != null;
        if (_tracker.gameObject.activeSelf != on) _tracker.gameObject.SetActive(on);
        if (!on) return;
        string title = q.act + " · " + q.title;
        if (_trTitle.text != title) _trTitle.text = title;
        string obj = "◆ " + o.description;
        if (o.type == QuestObjectiveType.SinkShips) obj += "  (" + manager.progress + "/" + o.count + ")";
        else if (o.type == QuestObjectiveType.BountyAtLeast || o.type == QuestObjectiveType.BountyAtMost)
            obj += "  (지금 " + ReputationSystem.Level(o.faction) + "단계)";
        else { string st = manager.ObjectiveStatus(); if (!string.IsNullOrEmpty(st)) obj += "  " + st; }
        if (_trObjective.text != obj) _trObjective.text = obj;
        Vector3 pos; string label; string dist = "";
        if (manager.TryGetTarget(out pos, out label))
        {
            Vector3 d = pos - manager.PlayerPosition(); d.y = 0f;
            float m = d.magnitude;
            dist = (string.IsNullOrEmpty(label) ? "" : label + "까지 ") + (m >= 1000f ? (m / 1000f).ToString("F1") + " km" : Mathf.RoundToInt(m) + " m");
        }
        if (_trDistance.text != dist) _trDistance.text = dist;
    }
}
