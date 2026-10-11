using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 궁궐(광화문 ~ 뒤쪽 궁궐 = 씬 GyeongBok 에서 keepActive(육조거리) 를 뺀 자식 전부)을 평소엔 끄고,
/// 마을 시점에서 찍은 그림 한 장(card — 기본색 + 물체 공간 노멀, 알파 잘라내기, 실시간 조명을 받음)으로 대신 보여 준다.
/// 사용자 지시(2026-10-05): 궁은 옆·뒤에서는 볼 수 없고 마을 쪽에서만 보이므로 항상 그림으로 두고,
/// 나룻배로 육조거리에 들어갈 때만 켠다(FerryCrossing — 카메라가 배 측면에 고정된 동안이라 바뀌는 게 안 보임).
/// 켤 때 한 번에 켜면 오래 멈추므로 작은 묶음(unit — 렌더러 maxRenderersPerUnit 개 이하가 되게 쪼갠 하위 오브젝트) 단위로
/// 프레임당 budgetMs 안에서 나눠 켠다. 끌 때는 맨 위(holder)만 끈다(싸다).
/// 편집 모드에서는 아무것도 안 바꾼다(플레이 때만 — 플레이를 끄면 원래대로).
/// 자동(2026-10-05, 사용자 지적 "나룻배를 안 타고 육조거리로 넘어가면 광화문이 그림인 채"): 나룻배 말고 다른 길(배·걸어서)로도
/// 갈 수 있으므로 0.25 s 마다 카메라 위치를 보고, 궁·육조거리 구역(zoneMin~zoneMax, XZ) 안이면 Show(), 구역에서 hideMargin 넘게 나가면 HideNow().
/// 나룻배를 타는 동안은 FerryCrossing 이 autoSuspended 를 켜 두고 직접 켜고 끈다(호수 위 카메라는 구역 밖이라).
/// </summary>
[DefaultExecutionOrder(-100)] // 궁 오브젝트들의 Awake 보다 먼저 꺼서 초기화 비용도 아낌
public class PalaceVista : MonoBehaviour
{
    public static PalaceVista Instance { get; private set; }

    [Tooltip("궁궐 루트(씬 GyeongBok).")] public Transform palaceRoot;
    [Tooltip("끄지 않을 palaceRoot 직속 자식 이름(육조거리).")] public string[] keepActive = { "육조거리_Yukjo" };
    [Tooltip("마을 시점 그림 판.")] public GameObject card;
    [Tooltip("시작할 때 궁을 끄고 그림을 보일지.")] public bool startHidden = true;
    [Tooltip("궁을 켤 때 프레임당 쓰는 시간(ms).")] public float budgetMs = 4f;
    [Tooltip("한 번에 켜는 묶음의 최대 렌더러 수(넘으면 자식으로 쪼갬).")] public int maxRenderersPerUnit = 150;

    [Header("자동 (카메라 위치)")]
    [Tooltip("카메라가 궁·육조거리 구역에 들어가면 궁을 켜고, 나가면 끔.")] public bool autoByZone = true;
    [Tooltip("구역 최소 (월드 x, z). 북쪽 나룻터(z 3378.8)를 포함.")] public Vector2 zoneMin = new Vector2(-3110f, 3360f);
    [Tooltip("구역 최대 (월드 x, z).")] public Vector2 zoneMax = new Vector2(-2380f, 4510f);
    [Tooltip("구역 밖으로 이만큼(m) 나가야 끔(경계에서 깜빡임 방지).")] public float hideMargin = 40f;
    [Tooltip("켜져 있으면 자동을 멈춤(나룻배 타는 동안 FerryCrossing 이 켬).")] public bool autoSuspended;

    [Header("상태 (읽기 전용)")]
    public bool palaceActive = true;
    public bool busy;
    public int unitCount;
    [Tooltip("마지막으로 궁을 켜는 데 걸린 시간(초) / 프레임 수 / 한 프레임 최대(ms).")] public float lastShowSeconds; public int lastShowFrames; public float lastShowMaxFrameMs;

    readonly List<GameObject> _holders = new List<GameObject>();
    // 켜는 순서(부모 먼저). 원래 켜져 있던 것만.
    readonly List<GameObject> _units = new List<GameObject>();
    Coroutine _co;
    float _nextCheck;

    void Awake()
    {
        Instance = this;
        if (!Application.isPlaying) return;
        Collect();
        if (startHidden) HideNow();
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Update()
    {
        if (!Application.isPlaying || !autoByZone || autoSuspended || Time.time < _nextCheck) return;
        _nextCheck = Time.time + 0.25f;
        var cam = Camera.main; if (cam == null) return;
        Vector3 p = cam.transform.position;
        bool inside = p.x >= zoneMin.x && p.x <= zoneMax.x && p.z >= zoneMin.y && p.z <= zoneMax.y;
        bool far = p.x < zoneMin.x - hideMargin || p.x > zoneMax.x + hideMargin || p.z < zoneMin.y - hideMargin || p.z > zoneMax.y + hideMargin;
        if (inside) Show();
        else if (far && (palaceActive || busy)) HideNow();
    }

    bool Keep(Transform t)
    {
        if (keepActive == null) return false;
        for (int i = 0; i < keepActive.Length; i++) if (t.name == keepActive[i]) return true;
        return false;
    }

    void Collect()
    {
        _holders.Clear(); _units.Clear();
        if (palaceRoot == null) { var g = GameObject.Find("GyeongBok"); if (g != null) palaceRoot = g.transform; }
        if (palaceRoot == null) return;
        for (int i = 0; i < palaceRoot.childCount; i++)
        {
            var h = palaceRoot.GetChild(i);
            if (Keep(h) || !h.gameObject.activeSelf) continue;
            if (card != null && (card.transform == h || card.transform.IsChildOf(h))) continue;
            _holders.Add(h.gameObject);
            for (int k = 0; k < h.childCount; k++) AddUnits(h.GetChild(k));
        }
        unitCount = _units.Count;
    }

    void AddUnits(Transform t)
    {
        if (!t.gameObject.activeSelf) return; // 원래 꺼져 있던 것(예전 단순화판 등)은 그대로 둠
        _units.Add(t.gameObject);
        if (t.childCount == 0) return;
        if (t.GetComponentsInChildren<Renderer>(true).Length <= maxRenderersPerUnit) return;
        for (int k = 0; k < t.childCount; k++) AddUnits(t.GetChild(k));
    }

    /// <summary>궁을 바로 끄고 그림을 켠다.</summary>
    public void HideNow()
    {
        if (_co != null) { StopCoroutine(_co); _co = null; }
        busy = false;
        for (int i = 0; i < _holders.Count; i++) if (_holders[i] != null) _holders[i].SetActive(false);
        if (card != null) card.SetActive(true);
        palaceActive = false;
    }

    /// <summary>궁을 나눠 켜고, 다 켜면 그림을 끈다.</summary>
    public void Show()
    {
        if (palaceActive || busy) return;
        _co = StartCoroutine(ShowRoutine());
    }

    IEnumerator ShowRoutine()
    {
        busy = true;
        float t0 = Time.realtimeSinceStartup; int frames = 0; float maxMs = 0f;
        // 1) 부모(holder)가 꺼진 동안 묶음을 모두 끄고(싸다) 2) 부모를 켜고 3) 묶음을 부모부터 차례로 시간 나눠 켬
        for (int i = _units.Count - 1; i >= 0; i--) if (_units[i] != null) _units[i].SetActive(false);
        for (int i = 0; i < _holders.Count; i++) if (_holders[i] != null) _holders[i].SetActive(true);
        float frameStart = Time.realtimeSinceStartup;
        for (int i = 0; i < _units.Count; i++)
        {
            if (_units[i] != null) _units[i].SetActive(true);
            float ms = (Time.realtimeSinceStartup - frameStart) * 1000f;
            if (ms >= budgetMs)
            {
                if (ms > maxMs) maxMs = ms;
                frames++;
                yield return null;
                frameStart = Time.realtimeSinceStartup;
            }
        }
        float last = (Time.realtimeSinceStartup - frameStart) * 1000f; if (last > maxMs) maxMs = last;
        frames++;
        if (card != null) card.SetActive(false);
        palaceActive = true; busy = false; _co = null;
        lastShowSeconds = Time.realtimeSinceStartup - t0; lastShowFrames = frames; lastShowMaxFrameMs = maxMs;
    }
}
