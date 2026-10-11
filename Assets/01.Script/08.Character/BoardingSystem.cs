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
    [Tooltip("또는 배 선체(충돌체)에서 이 거리(m) 안이면 탈 수 있다(2026-10-06 — 선착장에 붙인 배 옆 어디서나). T 자 정박 때 하차 지점(판자 로컬 x 6)에서 선체까지 약 7.04 m 라 9.")] public float boardHullRange = 9f;
    [Tooltip("하차 시 캐릭터를 세울 오브젝트. 비워두면 자동 생성.")] public OnFootController onFootRoot;
    [Tooltip("하차 중 꺼둘 배 컴포넌트 이름.")] public string[] shipComponentsToDisable = { "CannonControl", "CameraFollow" }; // MoveControl 은 끄지 않고 정박(SetDocked) — 끄면 물 저항이 멈춰 배가 떠내려감

    public bool IsOnFoot { get; private set; }
    /// <summary>지금 E 로 승·하차할 수 있는 자리인지(PalaceGate 가 E 를 양보할 때 씀).</summary>
    public bool CanAct { get { return _canAct; } }
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
            if (d <= boardRange || HullDistance(onFootRoot.transform.position) <= boardHullRange) { _canAct = true; _hint = "E 를 " + holdSeconds.ToString("F0") + "초 눌러 승선"; }
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
        PlaceShipAtBerth(lp);
        var mc = ship.GetComponent<MoveControl>(); if (mc != null) mc.SetDocked(true);
        onFootRoot.SetYaw(root.eulerAngles.y);
        IsOnFoot = true;
    }

    /// <summary>
    /// 하차할 때 배를 선착장 옆에 붙여 둔다(2026-10-06 사용자 지시 "선착장으로 내리면 배가 선착장에 밀착되게 하여 다시 탈 수 있게").
    /// 선착장 = LandingPoint 의 부모(Dock) 로컬 좌표: 판자는 로컬 x 방향(바다 쪽 +x), 폭 z pierMinZ~pierMaxZ. 배는 로컬 x = berthX 에
    /// 판자와 나란히(지금 뱃머리에 가까운 쪽), 판자 가장자리에서 배 반폭 + berthGap 만큼(지금 배가 있는 쪽 면).
    /// 배 반폭은 배 충돌체에 옆에서 ClosestPoint 를 재서 구함. 부모가 없는 LandingPoint(베이스 선착장)는 그대로.
    /// </summary>
    void PlaceShipAtBerth(LandingPoint lp)
    {
        if (lp == null || !lp.snapShip || lp.transform.parent == null || ship == null) return;
        var dock = lp.transform.parent;
        var rb = ship.GetComponent<Rigidbody>();
        Vector3 local = dock.InverseTransformPoint(ship.position);
        bool plusSide = local.z > (lp.pierMinZ + lp.pierMaxZ) * 0.5f;
        Vector3 axis = dock.right; axis.y = 0f; axis.Normalize();
        Vector3 fwd = ship.forward; fwd.y = 0f;
        Vector3 head = Vector3.Dot(fwd, axis) >= 0f ? axis : -axis;
        float half = ShipHalfExtent(ship.right);
        float halfLen = ShipHalfExtent(ship.forward);
        // 양쪽 면 모두에서 배 밑이 충분히 깊은 가장 육지 쪽 x 를 찾고(얕으면 바다 쪽으로 1 m 씩 — 해변에 걸려 밀려나지 않게,
        // 2026-10-06 측정: 큰섬은 판자 옆이 얕아 12 m → 20 m 로 밀려났음), 더 가까운 쪽 면을 고름(같으면 배가 있던 쪽).
        float x0 = Mathf.Max(lp.berthX, lp.pierLandEndX + halfLen + lp.bowClearance);
        float zP = lp.pierMaxZ + half + lp.berthGap, zM = lp.pierMinZ - half - lp.berthGap;
        float xP = x0, xM = x0;
        for (int k = 0; k < 40 && !DeepEnough(dock, xP, zP, halfLen, half, lp.maxSeabedY); k++) xP += 1f;
        for (int k = 0; k < 40 && !DeepEnough(dock, xM, zM, halfLen, half, lp.maxSeabedY); k++) xM += 1f;
        if (xP < xM - 0.5f) plusSide = true; else if (xM < xP - 0.5f) plusSide = false;
        float z = plusSide ? zP : zM;
        float x = plusSide ? xP : xM;
        Vector3 w = dock.TransformPoint(new Vector3(x, 0f, z));
        if (lp.berthAtPierHead)
        {
            // T 자: 판자 바다 끝에 배 옆구리 가운데를 붙이고 배는 판자와 직각(뱃머리는 지금 가까운 쪽). 얕으면 바다 쪽으로 1 m 씩.
            Vector3 az = dock.forward; az.y = 0f; az.Normalize();
            head = Vector3.Dot(fwd, az) >= 0f ? az : -az;
            float zc = (lp.pierMinZ + lp.pierMaxZ) * 0.5f;
            float xh = lp.pierSeaEndX + lp.berthGap + half;
            for (int k = 0; k < 40; k++)
            {
                Vector3 c = dock.TransformPoint(new Vector3(xh, 0f, zc));
                if (DeepEnoughAt(c, head, Vector3.Cross(Vector3.up, head), halfLen, half, lp.maxSeabedY)) break;
                xh += 1f;
            }
            w = dock.TransformPoint(new Vector3(xh, 0f, zc));
        }
        Vector3 pos = new Vector3(w.x, ship.position.y, w.z);
        Quaternion rot = Quaternion.LookRotation(head, Vector3.up);
        if (rb != null) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; rb.position = pos; rb.rotation = rot; }
        ship.SetPositionAndRotation(pos, rot);
        Physics.SyncTransforms();
    }

    /// <summary>Dock 로컬 (x, z) 에 배를 두면 배 밑(길이 2·halfLen × 폭 2·half, 3 × 7 점) 땅이 모두 maxY 보다 낮은지(터레인만 봄).</summary>
    static bool DeepEnough(Transform dock, float x, float z, float halfLen, float half, float maxY)
    {
        for (int i = -3; i <= 3; i++)
            for (int j = -1; j <= 1; j++)
            {
                Vector3 w = dock.TransformPoint(new Vector3(x + i * halfLen / 3f, 0f, z + j * half * 0.8f));
                foreach (var t in Terrain.activeTerrains)
                {
                    var p = t.transform.position; var sz = t.terrainData.size;
                    if (w.x < p.x || w.x > p.x + sz.x || w.z < p.z || w.z > p.z + sz.z) continue;
                    if (t.SampleHeight(w) + p.y > maxY) return false;
                }
            }
        return true;
    }

    /// <summary>월드 중심 c, 뱃머리 f·옆 r 방향으로 놓인 배(길이 2·halfLen × 폭 2·half, 7 × 3 점) 밑 터레인이 모두 maxY 보다 낮은지.</summary>
    static bool DeepEnoughAt(Vector3 c, Vector3 f, Vector3 r, float halfLen, float half, float maxY)
    {
        for (int i = -3; i <= 3; i++)
            for (int j = -1; j <= 1; j++)
            {
                Vector3 w = c + f * (i * halfLen / 3f) + r * (j * half * 0.8f);
                foreach (var t in Terrain.activeTerrains)
                {
                    var p = t.transform.position; var sz = t.terrainData.size;
                    if (w.x < p.x || w.x > p.x + sz.x || w.z < p.z || w.z > p.z + sz.z) continue;
                    if (t.SampleHeight(w) + p.y > maxY) return false;
                }
            }
        return true;
    }

    /// <summary>배 원점에서 dir 방향(수평) 충돌체 끝까지 거리(양쪽 중 큰 값).</summary>
    float ShipHalfExtent(Vector3 dir)
    {
        Vector3 c = ship.position; Vector3 r = dir; r.y = 0f; r.Normalize();
        float best = 0f;
        foreach (var col in ship.GetComponentsInChildren<Collider>())
        {
            if (col.isTrigger || !col.enabled || col is CharacterController) continue;
            if (!(col is BoxCollider || col is CapsuleCollider || col is SphereCollider || (col is MeshCollider && ((MeshCollider)col).convex))) continue;
            foreach (float s in new[] { 1f, -1f })
            {
                Vector3 p = col.ClosestPoint(c + r * s * 60f);
                float d = Mathf.Abs(Vector3.Dot(p - c, r));
                if (d > best && d < 30f) best = d;
            }
        }
        return best > 0.5f ? best : 6.5f;
    }

    /// <summary>걷는 캐릭터와 배 선체(볼록 충돌체) 사이 가장 가까운 수평 거리.</summary>
    float HullDistance(Vector3 p)
    {
        float best = float.MaxValue;
        foreach (var col in ship.GetComponentsInChildren<Collider>())
        {
            if (col.isTrigger || !col.enabled || col is CharacterController) continue;
            if (!(col is BoxCollider || col is CapsuleCollider || col is SphereCollider || (col is MeshCollider && ((MeshCollider)col).convex))) continue;
            Vector3 q = col.ClosestPoint(p); q.y = p.y;
            best = Mathf.Min(best, Vector3.Distance(p, q));
        }
        return best;
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
