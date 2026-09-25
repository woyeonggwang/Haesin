using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 승·하차. E 를 holdSeconds(3초) 이상 누르면
///   - 탑승 중: 배가 어느 LandingPoint 의 dockRange 안에 있으면 그 지점에 내린다 (배 조종·배 카메라 끔, OnFootRoot 켬).
///   - 하차 중: 캐릭터가 배(CharacterSeat) 에서 boardRange 안에 있으면 다시 탄다.
/// 캐릭터 외형(PlayerCharacter)은 탑승 땐 CharacterSeat 아래, 하차 땐 OnFootRoot(CharacterController + OnFootController) 아래로 옮긴다.
/// 화면에 안내와 누르고 있는 진행 바를 그린다(OnGUI, 임시).
/// </summary>
public class BoardingSystem : MonoBehaviour
{
    public PlayerCharacter character;
    public Transform ship;              // panok
    public Transform seat;              // CharacterSeat
    public Transform cameraTransform;   // Main Camera
    public float holdSeconds = 3f;
    [Tooltip("다시 타려면 캐릭터가 배 중심에서 이 거리(m) 안에 있어야 한다. 선착장 끝에서 정박한 판옥선 중심까지 약 11 m.")] public float boardRange = 25f;
    [Tooltip("하차 시 캐릭터를 세울 오브젝트. 비워두면 자동 생성.")] public OnFootController onFootRoot;
    [Tooltip("하차 중 꺼둘 배 컴포넌트 이름.")] public string[] shipComponentsToDisable = { "CannonControl", "CameraFollow" }; // MoveControl 은 끄지 않고 정박(SetDocked) — 끄면 물 저항이 멈춰 배가 떠내려감

    public bool IsOnFoot { get; private set; }
    float _hold; LandingPoint _nearest; bool _canAct; string _hint = "";
    readonly List<Behaviour> _disabled = new List<Behaviour>();
    // 배 카메라(Main Camera)는 panok/CameraPivot 의 자식이라, 하차 중 PlayerOrbitCamera 가 월드 위치·회전을 덮어쓰면 로컬 자세가 망가진다.
    // (2026-09-25 확인: 승선 후 카메라가 기울어 물속을 비춤) → 하차 때 로컬 자세를 기억했다가 승선 때 되돌린다.
    Transform _camParent; Vector3 _camLocalPos; Quaternion _camLocalRot; bool _camSaved;

    void Start()
    {
        if (character == null) character = FindFirstObjectByType<PlayerCharacter>();
        if (ship == null) { var p = GameObject.FindGameObjectWithTag("Player"); if (p != null) ship = p.transform; }
        if (seat == null && ship != null) seat = ship.Find("CharacterSeat");
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        if (onFootRoot == null)
        {
            var go = new GameObject("OnFootRoot");
            var cc = go.AddComponent<CharacterController>(); cc.radius = 0.32f; cc.height = 1.75f; cc.center = new Vector3(0f, 0.9f, 0f); cc.stepOffset = 0.4f; cc.slopeLimit = 50f;
            onFootRoot = go.AddComponent<OnFootController>();
            go.SetActive(false);
        }
        onFootRoot.cameraTransform = cameraTransform;
    }

    void Update()
    {
        _canAct = false; _hint = "";
        if (character == null || ship == null || seat == null) return;

        if (!IsOnFoot)
        {
            _nearest = NearestLanding(ship.position, true);
            if (_nearest != null) { _canAct = true; _hint = "E 를 " + holdSeconds.ToString("F0") + "초 눌러 하차 (" + _nearest.name + ")"; }
        }
        else
        {
            float d = Vector3.Distance(onFootRoot.transform.position, ship.position);
            if (d <= boardRange) { _canAct = true; _hint = "E 를 " + holdSeconds.ToString("F0") + "초 눌러 승선"; }
        }

        if (_canAct && Input.GetKey(KeyCode.E))
        {
            _hold += Time.deltaTime;
            if (_hold >= holdSeconds) { _hold = 0f; if (IsOnFoot) Board(); else Disembark(_nearest); }
        }
        else _hold = 0f;
    }

    LandingPoint NearestLanding(Vector3 from, bool requireRange)
    {
        LandingPoint best = null; float bestD = float.MaxValue;
        foreach (var lp in FindObjectsByType<LandingPoint>(FindObjectsSortMode.None))
        {
            float d = Vector3.Distance(from, lp.transform.position);
            if (requireRange && d > lp.dockRange) continue;
            if (d < bestD) { bestD = d; best = lp; }
        }
        return best;
    }

    public void Disembark(LandingPoint lp)
    {
        if (lp == null || IsOnFoot) return;
        // 착지점: 마커에서 아래로 레이를 쏴 바닥(선착장 판자·지형) 위에 세운다
        Vector3 pos = lp.transform.position;
        RaycastHit hit;
        if (Physics.Raycast(pos + Vector3.up * 3f, Vector3.down, out hit, 20f, ~0, QueryTriggerInteraction.Ignore)) pos = hit.point + Vector3.up * 0.05f;

        var root = onFootRoot.transform;
        root.position = pos;
        Vector3 face = ship.position - pos; face.y = 0f;
        root.rotation = face.sqrMagnitude > 0.01f ? Quaternion.LookRotation(-face.normalized, Vector3.up) : Quaternion.identity;
        // 외형을 먼저 옮기고 나서 켠다 — OnFootController.OnEnable 이 자식 Animator 를 찾기 때문(예전엔 켠 뒤 옮겨서 모델 Animator 에 Speed 가 안 갔음)
        character.transform.SetParent(root, false);
        character.transform.localPosition = character.standOffset;
        character.transform.localRotation = Quaternion.identity;
        onFootRoot.gameObject.SetActive(true);
        character.SetOnFoot();

        if (cameraTransform != null)
        {
            _camParent = cameraTransform.parent; _camLocalPos = cameraTransform.localPosition; _camLocalRot = cameraTransform.localRotation; _camSaved = true;
        }

        // 배 조종·배 카메라 끄기, 배 정지
        _disabled.Clear();
        foreach (var mb in ship.GetComponentsInChildren<MonoBehaviour>())
        {
            if (mb == null || !mb.enabled) continue;
            string n = mb.GetType().Name;
            for (int i = 0; i < shipComponentsToDisable.Length; i++) if (n == shipComponentsToDisable[i]) { mb.enabled = false; _disabled.Add(mb); break; }
        }
        var mc = ship.GetComponent<MoveControl>(); if (mc != null) mc.SetDocked(true);
        onFootRoot.SetYaw(root.eulerAngles.y);
        IsOnFoot = true;
    }

    public void Board()
    {
        if (!IsOnFoot) return;
        character.Board(seat);
        onFootRoot.gameObject.SetActive(false);
        if (_camSaved && cameraTransform != null)
        {
            if (cameraTransform.parent != _camParent) cameraTransform.SetParent(_camParent, false);
            cameraTransform.localPosition = _camLocalPos; cameraTransform.localRotation = _camLocalRot;
            _camSaved = false;
        }
        for (int i = 0; i < _disabled.Count; i++) if (_disabled[i] != null) _disabled[i].enabled = true;
        _disabled.Clear();
        var mc = ship.GetComponent<MoveControl>(); if (mc != null) mc.SetDocked(false);
        IsOnFoot = false;
    }

    void OnGUI()
    {
        if (!_canAct) return;
        float W = Screen.width, H = Screen.height;
        var st = new GUIStyle(GUI.skin.label); st.fontSize = 20; st.alignment = TextAnchor.MiddleCenter; st.normal.textColor = Color.white;
        GUI.Label(new Rect(0f, H * 0.72f, W, 30f), _hint, st);
        if (_hold > 0f)
        {
            float w = 260f, x = (W - w) * 0.5f, y = H * 0.72f + 34f;
            GUI.color = new Color(0f, 0f, 0f, 0.6f); GUI.DrawTexture(new Rect(x, y, w, 10f), Texture2D.whiteTexture);
            GUI.color = new Color(0.4f, 0.9f, 1f); GUI.DrawTexture(new Rect(x, y, w * Mathf.Clamp01(_hold / holdSeconds), 10f), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
