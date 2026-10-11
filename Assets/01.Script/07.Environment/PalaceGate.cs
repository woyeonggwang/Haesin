using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 궁궐 문(궐문·전각 문) 열고 닫기. 배에서 내린 상태(OnFoot)에서 문짝 가까이(range) 서서 E 를 holdSeconds(3초) 누르면
/// 이 묶음의 문짝(leaves) 전부를 안쪽(inward)으로 openAngle 만큼 돌려 연다. 다시 누르면 닫는다.
/// 문짝 = 경첩 피벗 오브젝트(DoorHinge_*, Y 축으로 돌리면 열림). 여는 방향(±)은 시작할 때 문짝 가운데가 안쪽으로 가는 쪽으로 정한다.
/// 여러 문 묶음이 겹치면 가장 가까운 묶음 하나만 반응한다. 승·하차(BoardingSystem)가 E 를 쓰는 곳에서는 양보한다.
/// 안내·진행 바는 BoardingSystem 과 같은 모양(OnGUI, 임시).
/// 2026-10-06(사용자 지적 "붙어서 문을 열면 끼임"): 문짝이 움직이는 동안 문짝 충돌체를 끄고, 다 움직인 뒤 걷는 캐릭터(CharacterController)가
/// 문짝 충돌체와 겹쳐 있으면 겹친 만큼 밀어낸 다음 충돌체를 다시 켬(예전엔 transform 으로 돌린 문짝이 캐릭터 안으로 들어와 갇힘).
/// </summary>
public class PalaceGate : MonoBehaviour
{
    public string displayName = "문";
    public Transform[] leaves;
    [Tooltip("문이 열리는 쪽(안쪽) 기준점. 비워두면 이 오브젝트 위치.")] public Transform inward;
    public float openAngle = 90f;
    public float openSeconds = 2.5f;
    public float holdSeconds = 3f;
    [Tooltip("문짝(경첩) 가운데에서 이 거리(m, 수평) 안에 있어야 반응.")] public float range = 6f;
    public float heightRange = 4f;

    static readonly List<PalaceGate> s_all = new List<PalaceGate>();
    static PalaceGate s_active;
    static BoardingSystem s_boarding;

    Quaternion[] _closed, _open; Vector3[] _centers;
    bool _isOpen; float _t; float _hold;
    Collider[] _cols; bool _moving;

    void OnEnable() { s_all.Add(this); }
    void OnDisable() { s_all.Remove(this); if (s_active == this) s_active = null; }

    void Start()
    {
        var cl = new List<Collider>();
        foreach (var h in leaves) if (h != null) foreach (var c in h.GetComponentsInChildren<Collider>(true)) if (c.enabled && !c.isTrigger) cl.Add(c);
        _cols = cl.ToArray();
        int n = leaves.Length;
        _closed = new Quaternion[n]; _open = new Quaternion[n]; _centers = new Vector3[n];
        Vector3 inPt = inward != null ? inward.position : transform.position;
        for (int i = 0; i < n; i++)
        {
            var h = leaves[i]; if (h == null) continue;
            _closed[i] = h.rotation;
            var b = new Bounds(h.position, Vector3.zero);
            foreach (var r in h.GetComponentsInChildren<Renderer>(true)) b.Encapsulate(r.bounds);
            _centers[i] = b.center;
            Vector3 arm = b.center - h.position; arm.y = 0f;
            Vector3 toIn = inPt - h.position; toIn.y = 0f;
            float best = -1e9f; float sign = 1f;
            foreach (float sg in new[] { 1f, -1f })
            {
                Vector3 a = Quaternion.AngleAxis(sg * openAngle, Vector3.up) * arm;
                float sc = Vector3.Dot(a, toIn.normalized);
                if (sc > best) { best = sc; sign = sg; }
            }
            _open[i] = Quaternion.AngleAxis(sign * openAngle, Vector3.up) * _closed[i];
        }
    }

    // 플레이어(걷는 캐릭터)에서 가장 가까운 문짝 가운데까지 수평 거리. 반응 못 하면 큰 값.
    float PlayerDistance()
    {
        if (s_boarding == null) s_boarding = FindFirstObjectByType<BoardingSystem>();
        if (s_boarding == null || !s_boarding.IsOnFoot || s_boarding.onFootRoot == null) return 1e9f;
        if (s_boarding.CanAct) return 1e9f;
        Vector3 p = s_boarding.onFootRoot.transform.position;
        float best = 1e9f;
        for (int i = 0; i < _centers.Length; i++)
        {
            Vector3 c = _centers[i];
            if (Mathf.Abs(c.y - p.y) > heightRange + 3f) continue;
            float d = Vector2.Distance(new Vector2(c.x, c.z), new Vector2(p.x, p.z));
            if (d < best) best = d;
        }
        return best;
    }

    void Update()
    {
        // 가장 가까운 묶음 하나만 입력을 받음 — 첫 번째로 Update 되는 문이 전체를 골라 둠
        if (s_all.Count > 0 && s_all[0] == this)
        {
            PalaceGate pick = null; float pd = 1e9f;
            foreach (var g in s_all) { float d = g.PlayerDistance(); if (d <= g.range && d < pd) { pd = d; pick = g; } }
            if (pick != s_active && s_active != null) s_active._hold = 0f;
            s_active = pick;
        }

        if (s_active == this && Input.GetKey(KeyCode.E))
        {
            _hold += Time.deltaTime;
            if (_hold >= holdSeconds) { _hold = 0f; _isOpen = !_isOpen; }
        }
        else _hold = 0f;

        float target = _isOpen ? 1f : 0f;
        if (!Mathf.Approximately(_t, target))
        {
            _t = Mathf.MoveTowards(_t, target, Time.deltaTime / Mathf.Max(0.01f, openSeconds));
            if (!_moving) { _moving = true; SetColliders(false); }
            float k = Mathf.SmoothStep(0f, 1f, _t);
            for (int i = 0; i < leaves.Length; i++)
                if (leaves[i] != null) leaves[i].rotation = Quaternion.Slerp(_closed[i], _open[i], k);
            if (Mathf.Approximately(_t, target)) { Physics.SyncTransforms(); PushPlayerOut(); SetColliders(true); _moving = false; }
        }
    }

    void SetColliders(bool on)
    {
        if (_cols == null) return;
        for (int i = 0; i < _cols.Length; i++) if (_cols[i] != null) _cols[i].enabled = on;
    }

    /// <summary>문짝 충돌체와 겹친 걷는 캐릭터를 겹친 만큼(+3 cm) 밀어냄 — 충돌체를 켜기 전에.</summary>
    void PushPlayerOut()
    {
        if (s_boarding == null) s_boarding = FindFirstObjectByType<BoardingSystem>();
        if (s_boarding == null || !s_boarding.IsOnFoot || s_boarding.onFootRoot == null || _cols == null) return;
        var cc = s_boarding.onFootRoot.GetComponent<CharacterController>();
        if (cc == null) return;
        for (int pass = 0; pass < 3; pass++)
        {
            bool moved = false;
            foreach (var c in _cols)
            {
                if (c == null) continue;
                bool was = c.enabled; c.enabled = true;
                Vector3 dir; float dist;
                bool hit = Physics.ComputePenetration(cc, cc.transform.position, cc.transform.rotation, c, c.transform.position, c.transform.rotation, out dir, out dist);
                c.enabled = was;
                if (!hit || dist <= 0f) continue;
                dir.y = 0f; if (dir.sqrMagnitude < 1e-6f) continue;
                cc.enabled = false;
                cc.transform.position += dir.normalized * (dist + 0.03f);
                cc.enabled = true;
                moved = true;
            }
            if (!moved) break;
        }
    }

    void OnGUI()
    {
        if (s_active != this) return;
        float W = Screen.width, H = Screen.height;
        var st = new GUIStyle(GUI.skin.label); st.fontSize = 20; st.alignment = TextAnchor.MiddleCenter; st.normal.textColor = Color.white;
        GUI.Label(new Rect(0f, H * 0.72f, W, 30f), "E 를 " + holdSeconds.ToString("F0") + "초 눌러 " + displayName + " " + (_isOpen ? "닫기" : "열기"), st);
        if (_hold > 0f)
        {
            float w = 260f, x = (W - w) * 0.5f, y = H * 0.72f + 34f;
            GUI.color = new Color(0f, 0f, 0f, 0.6f); GUI.DrawTexture(new Rect(x, y, w, 10f), Texture2D.whiteTexture);
            GUI.color = new Color(0.4f, 0.9f, 1f); GUI.DrawTexture(new Rect(x, y, w * Mathf.Clamp01(_hold / holdSeconds), 10f), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }

    // 시험용(MCP): 플레이 없이 바로 열고 닫기
    public void SetOpenImmediate(bool open)
    {
        if (_closed == null) Start();
        _isOpen = open; _t = open ? 1f : 0f; SetColliders(true); _moving = false;
        for (int i = 0; i < leaves.Length; i++) if (leaves[i] != null) leaves[i].rotation = open ? _open[i] : _closed[i];
    }
}
