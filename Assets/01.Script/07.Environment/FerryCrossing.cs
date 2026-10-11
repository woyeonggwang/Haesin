using System.Collections;
using UnityEngine;

/// <summary>
/// 나룻배로 호수 건너기. 호수 양쪽 선착장 두 곳(pointA = 육조거리 쪽, pointB = 마을 쪽)을 잇는다.
/// 배에서 내린 상태(OnFoot)에서 한쪽 선착장 서는 자리(point)의 range(m, 수평) 안에 서서 E 를 holdSeconds(3초) 누르면
///   화면을 잠깐 어둡게 하고 → 플레이어를 나룻배(boat)의 passengerSpot 에 태움(걷기·카메라 조작 끔) → 밝힘
///   → 사공(boatman) 이 노를 저으며 rideSeconds(40초) 동안 건너편 배 대는 자리(moor)까지 감(출발·도착 easeSeconds 가감속)
///   → 어둡게 → 건너편 서는 자리에 내림(조작 복구) → 밝힘.
/// 타고 가는 동안 카메라는 배 옆(sideCamera*)에 고정 — 돌릴 수 없고 측면만 본다(사용자 지시: 궁이 켜지고 꺼지는 걸 안 보이게).
/// 궁(PalaceVista): 육조거리(A) 쪽으로 갈 때 출발하면서 궁을 나눠 켜고 그림을 끔, 마을(B) 쪽으로 갈 때 출발하면서 궁을 끄고 그림을 켬.
/// 배는 늘 플레이어가 있는 쪽에 있다(호수를 건너는 길이 이것뿐). 건너편에 있으면 어두운 동안 옮겨 온다.
/// 승·하차(BoardingSystem)가 E 를 쓰는 자리(배 25 m 안)에서는 양보. 안내·진행 바는 BoardingSystem·PalaceGate 와 같은 모양(OnGUI, 임시).
/// 예전(2026-10-04): E 짧게 → 화면 어둡게 했다가 건너편으로 순간이동(배 모델 없음).
/// 2026-10-05 고침(사용자 지적): ① 나룻배를 안 타고 건너간 뒤 그쪽 나룻터에 가면 배가 없는데 E 로 갑자기 탐 → 걸어서 선착장 summonDistance 안에
/// 오면 배가 반대쪽에 있고 꺼져 있을 때(카메라에서 멀어 안 보일 때) 이쪽으로 옮겨 둠. ② 타면 포니테일이 뒤로 뻗음(SpringBoneSystem 이 배 이동을
/// worldInfluence 만큼 바람처럼 받음 + 태울 때 순간이동) → 타는 동안 worldInfluence 0, 태우고 내릴 때 ResetTails. ③ 타는 동안 궁 자동 켜고 끄기(PalaceVista) 멈춤.
/// </summary>
[DefaultExecutionOrder(1000)] // 카메라를 다른 LateUpdate(PlayerOrbitCamera 등) 뒤에 덮어씀
public class FerryCrossing : MonoBehaviour
{
    [Header("선착장")]
    [Tooltip("A 쪽(육조거리) 선착장에 서는 자리. 내릴 때 이 위치·방향(Y 회전)으로 선다.")] public Transform pointA;
    public string nameA = "육조거리";
    [Tooltip("B 쪽(마을) 선착장에 서는 자리.")] public Transform pointB;
    public string nameB = "마을";
    [Tooltip("A 쪽에 배를 대는 자리(위치만 씀 — 배 방향은 길 방향으로 정함).")] public Transform moorA;
    [Tooltip("B 쪽에 배를 대는 자리.")] public Transform moorB;
    [Tooltip("서는 자리에서 이 거리(m, 수평) 안이면 E 로 탄다.")] public float range = 8f;
    [Tooltip("서는 자리와 캐릭터 발 높이 차가 이 값(m) 안이어야 함.")] public float heightRange = 3f;
    public float holdSeconds = 3f;

    [Header("배")]
    [Tooltip("나룻배 루트(앞 = 로컬 +X, 원점 = 배 바닥 가운데).")] public Transform boat;
    [Tooltip("배 위 플레이어 자리(배의 자식, 앞 = 로컬 +Z).")] public Transform passengerSpot;
    [Tooltip("사공 Animator(bool Rowing).")] public Animator boatman;
    [Tooltip("건너는 시간(초).")] public float rideSeconds = 40f;
    [Tooltip("출발·도착 가감속 시간(초).")] public float easeSeconds = 5f;
    [Tooltip("배 원점에서 물 높이까지(m) — 배 원점 = 물 높이 − draft.")] public float draft = 0.22f;
    [Tooltip("물 높이를 HDRP 물에서 읽음(실패하면 waterLevel).")] public bool sampleWater = true;
    public float waterLevel = 0f;
    [Tooltip("물결 따라 기울기 세기(0 = 안 기욺).")] public float tiltFollow = 1f;
    [Tooltip("배가 처음 있는 쪽(true = A).")] public bool boatAtA = false;
    [Tooltip("타고 있지 않을 때 카메라에서 배까지 이 거리(m) 밖이면 배(사공 포함) 오브젝트를 끔(사용자 지정 60 m).")] public float hideDistance = 60f;
    [Tooltip("다시 켤 때는 hideDistance − 이 값 안.")] public float hideHysteresis = 5f;
    [Tooltip("걸어서 서는 자리 이 거리(m, 수평) 안에 오면 반대쪽에 있던 배를 이쪽으로 옮겨 둠(배가 안 보일 때만).")] public float summonDistance = 90f;

    [Header("카메라 (타고 가는 동안 고정)")]
    [Tooltip("카메라를 둘 쪽(월드 방향). 가는 방향과 상관없이 이쪽 옆에서 본다 — 선착장이 배 서쪽(−X)에 있어 동쪽(+X)이면 출발·도착 때 선착장 너머로 보지 않음.")] public Vector3 sideCameraWorldSide = Vector3.right;
    [Tooltip("배에서 옆으로 떨어진 거리(m).")] public float sideCameraDistance = 9f;
    [Tooltip("물 위 카메라 높이(m).")] public float sideCameraHeight = 2.6f;
    [Tooltip("바라보는 곳: 배 원점 위 높이(m).")] public float sideCameraLookHeight = 1.0f;
    [Tooltip("바라보는 곳을 배 앞쪽으로(m).")] public float sideCameraLookAhead = 0f;

    [Header("궁 (PalaceVista)")]
    public PalaceVista vista;
    [Tooltip("A 쪽이 궁 쪽인지.")] public bool palaceSideIsA = true;

    [Header("화면 전환")]
    public float fadeSeconds = 0.35f;
    public float holdDark = 0.25f;

    [Header("상태 (읽기 전용)")]
    public bool riding;
    public float rideProgress;

    BoardingSystem _boarding;
    Transform _from, _to; string _toName; bool _toIsA;
    bool _busy; float _fade; float _hold;
    Vector3 _rideDir; Camera _cam;
    static readonly int RowingHash = Animator.StringToHash("Rowing");

    void Start()
    {
        _boarding = FindFirstObjectByType<BoardingSystem>();
        if (vista == null) vista = FindFirstObjectByType<PalaceVista>();
        PlaceBoatAtDock(boatAtA);
    }

    void Update()
    {
        UpdateBoatVisible();
        SummonBoat();
        _from = null; _to = null;
        if (_busy || pointA == null || pointB == null) { _hold = 0f; return; }
        if (_boarding == null) _boarding = FindFirstObjectByType<BoardingSystem>();
        if (_boarding == null || !_boarding.IsOnFoot || _boarding.onFootRoot == null || _boarding.CanAct) { _hold = 0f; return; }
        if (Time.timeScale <= 0f) return; // 메뉴(일시정지) 중

        Vector3 p = _boarding.onFootRoot.transform.position;
        float da = Near(pointA, p), db = Near(pointB, p);
        if (da <= range && da <= db) { _from = pointA; _to = pointB; _toName = nameB; _toIsA = false; }
        else if (db <= range) { _from = pointB; _to = pointA; _toName = nameA; _toIsA = true; }

        if (_to != null && Input.GetKey(KeyCode.E))
        {
            _hold += Time.deltaTime;
            if (_hold >= holdSeconds) { _hold = 0f; StartCoroutine(Ride(_toIsA)); }
        }
        else _hold = 0f;
    }

    void UpdateBoatVisible()
    {
        if (boat == null) return;
        if (_busy) { if (!boat.gameObject.activeSelf) boat.gameObject.SetActive(true); return; } // 타는 중엔 늘 켬
        var cam = Camera.main; if (cam == null) return;
        float d2 = (cam.transform.position - boat.position).sqrMagnitude;
        bool act = boat.gameObject.activeSelf;
        if (act && d2 > hideDistance * hideDistance) boat.gameObject.SetActive(false);
        else if (!act) { float on = Mathf.Max(0f, hideDistance - hideHysteresis); if (d2 < on * on) boat.gameObject.SetActive(true); }
    }

    /// <summary>걸어서 한쪽 나룻터에 다가오는데 배가 반대쪽에 있으면(그리고 꺼져 있어 안 보이면) 이쪽에 댄다.</summary>
    void SummonBoat()
    {
        if (_busy || boat == null || pointA == null || pointB == null) return;
        if (_boarding == null) _boarding = FindFirstObjectByType<BoardingSystem>();
        if (_boarding == null || !_boarding.IsOnFoot || _boarding.onFootRoot == null) return;
        if (boat.gameObject.activeSelf) return; // 보이는 동안은 옮기지 않음
        Vector3 p = _boarding.onFootRoot.transform.position;
        float da = Vector2.Distance(new Vector2(pointA.position.x, pointA.position.z), new Vector2(p.x, p.z));
        float db = Vector2.Distance(new Vector2(pointB.position.x, pointB.position.z), new Vector2(p.x, p.z));
        if (da <= summonDistance && da < db && !boatAtA) PlaceBoatAtDock(true);
        else if (db <= summonDistance && db < da && boatAtA) PlaceBoatAtDock(false);
    }

    float Near(Transform t, Vector3 p)
    {
        if (Mathf.Abs(t.position.y - p.y) > heightRange) return 1e9f;
        return Vector2.Distance(new Vector2(t.position.x, t.position.z), new Vector2(p.x, p.z));
    }

    Vector3 MoorPos(bool a) { var m = a ? moorA : moorB; return m != null ? m.position : (a ? pointA.position : pointB.position); }

    /// <summary>배를 한쪽 선착장에 댄다(뱃머리 = 그 선착장 쪽).</summary>
    public void PlaceBoatAtDock(bool atA)
    {
        if (boat == null) return;
        Vector3 at = MoorPos(atA), other = MoorPos(!atA);
        Vector3 dir = at - other; dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
        SetBoatPose(at, dir.normalized, 0f);
        boatAtA = atA;
        if (boatman != null && boatman.isActiveAndEnabled) boatman.SetBool(RowingHash, false);
    }

    void SetBoatPose(Vector3 pos, Vector3 dir, float dt)
    {
        // 뱃머리 = 로컬 +X → LookRotation(+Z = dir) 뒤에 Y −90°
        Quaternion yaw = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(0f, -90f, 0f);
        float y = waterLevel; Vector3 n = Vector3.up;
        if (sampleWater)
        {
            var s = WaterHeightUtil.FindSurface();
            if (s != null)
            {
                Vector3 sp, c;
                if (WaterHeightUtil.Sample(s, pos, out sp, out n, out c)) y = sp.y;
            }
        }
        pos.y = y - draft;
        Quaternion tilt = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(Vector3.up, n), tiltFollow);
        Quaternion rot = tilt * yaw;
        if (dt > 0f) // 물결 흔들림을 부드럽게
        {
            float k = 1f - Mathf.Exp(-dt * 4f);
            pos.y = Mathf.Lerp(boat.position.y, pos.y, k);
            rot = Quaternion.Slerp(boat.rotation, rot, k);
        }
        boat.SetPositionAndRotation(pos, rot);
    }

    // 사다리꼴 속도(가감속 e 비율)로 0~1 진행
    static float Ease(float u, float e)
    {
        u = Mathf.Clamp01(u); e = Mathf.Clamp(e, 0.001f, 0.5f);
        float v = 1f / (1f - e);
        if (u < e) return 0.5f * v * u * u / e;
        if (u > 1f - e) { float w = 1f - u; return 1f - 0.5f * v * w * w / e; }
        return 0.5f * v * e + v * (u - e);
    }

    IEnumerator Ride(bool toA)
    {
        _busy = true;
        if (boat != null && !boat.gameObject.activeSelf) boat.gameObject.SetActive(true);
        var root = _boarding.onFootRoot;
        var cc = root.GetComponent<CharacterController>();
        var orbit = root.GetComponent<PlayerOrbitCamera>();
        var character = root.GetComponentInChildren<PlayerCharacter>();
        _cam = Camera.main;

        yield return Fade(0f, 1f);

        // 배를 출발 쪽에 대고, 플레이어를 태움
        bool fromA = !toA;
        Vector3 start = MoorPos(fromA), end = MoorPos(toA);
        _rideDir = end - start; _rideDir.y = 0f; _rideDir.Normalize();
        SetBoatPose(start, _rideDir, 0f);

        var anim = root.GetComponentInChildren<Animator>();
        if (anim != null) anim.SetFloat("Speed", 0f);
        bool[] onFootWas = null;
        if (character != null && character.onFootOnly != null)
        {
            onFootWas = new bool[character.onFootOnly.Length];
            for (int i = 0; i < onFootWas.Length; i++) { var b = character.onFootOnly[i]; onFootWas[i] = b != null && b.enabled; if (b != null) b.enabled = false; }
        }
        if (cc != null) cc.enabled = false;
        root.enabled = false;            // OnFootController — 걷기 입력·중력 끔
        if (orbit != null) orbit.enabled = false;
        root.transform.SetParent(passengerSpot != null ? passengerSpot : boat, false);
        root.transform.localPosition = Vector3.zero; root.transform.localRotation = Quaternion.identity;
        // 흔들리는 본(포니테일): 배와 같이 움직이는 동안 배 이동을 바람처럼 받지 않게, 순간이동한 자리에서 새로 시작
        var springs = root.GetComponentsInChildren<SpringBoneSystem>();
        float[] springInfluence = new float[springs.Length];
        for (int i = 0; i < springs.Length; i++) { springInfluence[i] = springs[i].worldInfluence; springs[i].worldInfluence = 0f; springs[i].ResetTails(); }

        // 궁: 궁 쪽으로 가면 나눠 켜고(카메라가 측면이라 안 보임), 마을 쪽으로 가면 바로 끄고 그림을 켬
        bool toPalace = toA == palaceSideIsA;
        if (vista != null) { vista.autoSuspended = true; if (toPalace) vista.Show(); else vista.HideNow(); }

        riding = true; rideProgress = 0f;
        if (boatman != null) boatman.SetBool(RowingHash, true);
        yield return Fade(1f, 0f);

        float t = 0f, T = Mathf.Max(1f, rideSeconds);
        while (t < T)
        {
            t += Time.deltaTime;
            rideProgress = Mathf.Clamp01(t / T);
            float s = Ease(rideProgress, easeSeconds / T);
            SetBoatPose(Vector3.Lerp(start, end, s), _rideDir, Time.deltaTime);
            if (boatman != null && rideProgress > 1f - 2.5f / T) boatman.SetBool(RowingHash, false); // 닿기 전에 노를 멈춤
            yield return null;
        }
        boatAtA = toA;

        yield return Fade(0f, 1f);
        riding = false;
        root.transform.SetParent(null, true);
        root.enabled = true;
        if (cc != null) cc.enabled = true;
        MoveTo(toA ? pointA : pointB);
        for (int i = 0; i < springs.Length; i++) if (springs[i] != null) { springs[i].worldInfluence = springInfluence[i]; springs[i].ResetTails(); }
        if (vista != null) vista.autoSuspended = false;
        if (orbit != null) orbit.enabled = true;
        root.SetYaw((toA ? pointA : pointB).eulerAngles.y);
        if (character != null && onFootWas != null)
            for (int i = 0; i < onFootWas.Length; i++) if (character.onFootOnly[i] != null) character.onFootOnly[i].enabled = onFootWas[i];
        yield return new WaitForSecondsRealtime(holdDark);
        yield return Fade(1f, 0f);
        _busy = false;
    }

    IEnumerator Fade(float a, float b)
    {
        for (float x = 0f; x < fadeSeconds; x += Time.unscaledDeltaTime) { _fade = Mathf.Lerp(a, b, x / fadeSeconds); yield return null; }
        _fade = b;
    }

    void LateUpdate()
    {
        if (!riding || boat == null) return;
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;
        // 배 옆(길 방향 기준, 흔들림 무시)에서 배를 봄 — 늘 sideCameraWorldSide 쪽
        Vector3 right = Vector3.Cross(Vector3.up, _rideDir).normalized; // 길 방향을 볼 때 오른쪽
        if (Vector3.Dot(right, sideCameraWorldSide) < 0f) right = -right;
        Vector3 c = boat.position; c.y = boat.position.y + draft;      // 배 가운데 물 높이
        Vector3 look = c + Vector3.up * sideCameraLookHeight + _rideDir * sideCameraLookAhead;
        Vector3 pos = c + right * sideCameraDistance + Vector3.up * sideCameraHeight;
        _cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos, Vector3.up));
    }

    /// <summary>캐릭터(OnFootRoot)를 dest 에 세운다. 아래로 레이를 쏴 판자 위에 올린다. 시험용으로 MCP 에서도 부름.</summary>
    public void MoveTo(Transform dest)
    {
        if (_boarding == null) _boarding = FindFirstObjectByType<BoardingSystem>();
        if (_boarding == null || _boarding.onFootRoot == null || dest == null) return;
        var root = _boarding.onFootRoot.transform;
        Vector3 pos = dest.position;
        RaycastHit hit;
        if (Physics.Raycast(pos + Vector3.up * 3f, Vector3.down, out hit, 20f, ~0, QueryTriggerInteraction.Ignore)) pos = hit.point + Vector3.up * 0.05f;
        var cc = root.GetComponent<CharacterController>();
        bool ccOn = cc != null && cc.enabled;
        if (ccOn) cc.enabled = false; // 켜진 채로 position 을 바꾸면 CharacterController 가 되돌림
        float yaw = dest.eulerAngles.y;
        root.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        if (ccOn) cc.enabled = true;
        Physics.SyncTransforms();
        _boarding.onFootRoot.SetYaw(yaw);
    }

    /// <summary>시험용(MCP): 지금 서 있는 쪽에서 바로 출발.</summary>
    public bool TestRide(bool toA)
    {
        if (_busy || _boarding == null || !_boarding.IsOnFoot) return false;
        StartCoroutine(Ride(toA)); return true;
    }

    void OnGUI()
    {
        float W = Screen.width, H = Screen.height;
        if (_fade > 0f)
        {
            GUI.color = new Color(0f, 0f, 0f, Mathf.Clamp01(_fade));
            GUI.DrawTexture(new Rect(0f, 0f, W, H), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
        if (_to == null || _busy) return;
        var st = new GUIStyle(GUI.skin.label); st.fontSize = 20; st.alignment = TextAnchor.MiddleCenter; st.normal.textColor = Color.white;
        GUI.Label(new Rect(0f, H * 0.72f, W, 30f), "E 를 " + holdSeconds.ToString("F0") + "초 눌러 나룻배 타기 (" + _toName + ")", st);
        if (_hold > 0f)
        {
            float w = 260f, x = (W - w) * 0.5f, y = H * 0.72f + 34f;
            GUI.color = new Color(0f, 0f, 0f, 0.6f); GUI.DrawTexture(new Rect(x, y, w, 10f), Texture2D.whiteTexture);
            GUI.color = new Color(0.4f, 0.9f, 1f); GUI.DrawTexture(new Rect(x, y, w * Mathf.Clamp01(_hold / holdSeconds), 10f), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
