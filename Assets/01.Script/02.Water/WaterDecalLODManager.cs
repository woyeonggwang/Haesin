using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 물 데칼 총량 관리자. 일정 간격으로 배들을 카메라 거리순으로 훑어
/// 가까운 몇 척만 항적 거품과 뱃머리 V자를 켠다.
///
/// HDRP 의 워터 데칼은 한 장면에 96개까지만 그려진다. 배 한 척이 2개를 쓰므로
/// 함대가 커지면 금세 상한에 닿는다. 수평선 너머의 배가 만드는 항적은
/// 화면에 보이지도 않으니, 가까운 몇 척만 켜 두면 함대를 얼마든지 늘릴 수 있다.
///
/// 이 스크립트는 반드시 파일 이름과 클래스 이름이 같아야 한다.
/// (다른 스크립트 파일 안에 같이 넣어 두면 Unity 가 씬에 저장하지 못하고
///  플레이를 멈출 때마다 컴포넌트가 사라진다)
/// </summary>
[DefaultExecutionOrder(-100)]
public class WaterDecalLODManager : MonoBehaviour
{
    [Header("데칼 예산")]
    [Tooltip("이 거리보다 먼 배는 데칼을 끈다(m).")]
    public float maxDistance = 150f;
    [Tooltip("동시에 데칼을 켜 둘 최대 함선 수. 배 한 척이 데칼 2개를 쓴다.")]
    public int maxActiveShips = 8;
    [Tooltip("데칼을 다시 계산하는 간격(초).")]
    public float updateInterval = 0.25f;
    [Tooltip("켜고 끄는 경계에서 깜빡이는 것을 막는 여유 거리(m). 켜진 배는 이만큼 더 멀어져야 꺼진다.")]
    public float hysteresis = 15f;

    [Header("기준 카메라")]
    [Tooltip("거리 기준 카메라. 비우면 Camera.main 을 쓴다.")]
    public Camera referenceCamera;

    [Header("상태 (읽기 전용)")]
    [Tooltip("지금 데칼이 켜져 있는 함선 수.")]
    public int activeShipCount;
    [Tooltip("지금 쓰이는 워터 데칼 수 (함선당 2개). HDRP 상한은 96이다.")]
    public int activeDecalCount;
    [Tooltip("등록된 전체 함선 수.")]
    public int registeredShipCount;

    private static WaterDecalLODManager _instance;
    private float _nextUpdateTime;
    private readonly List<ShipDecalLOD> _sorted = new List<ShipDecalLOD>();

    /// <summary>
    /// 씬에 관리자가 없으면 기본값으로 하나 만든다.
    /// 씬 로드가 끝난 뒤에 한 번만 돈다. 로드 도중에 찾으면 씬에 있는 관리자를
    /// 아직 못 찾아서 쓸데없이 하나 더 만들어 버린다.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void EnsureExists()
    {
        if (_instance != null) return;
        if (!Application.isPlaying) return;

        _instance = FindFirstObjectByType<WaterDecalLODManager>();
        if (_instance != null) return;

        var go = new GameObject("WaterDecalLOD (auto)");
        _instance = go.AddComponent<WaterDecalLODManager>();
    }

    void OnEnable()
    {
        if (_instance == null) _instance = this;
    }

    void OnDisable()
    {
        if (_instance == this) _instance = null;
    }

    void LateUpdate()
    {
        if (Time.time < _nextUpdateTime) return;
        _nextUpdateTime = Time.time + Mathf.Max(0.05f, updateInterval);

        Camera cam = referenceCamera != null ? referenceCamera : Camera.main;
        if (cam == null) return;
        Vector3 eye = cam.transform.position;

        _sorted.Clear();
        for (int i = 0; i < ShipDecalLOD.All.Count; i++)
        {
            ShipDecalLOD s = ShipDecalLOD.All[i];
            if (s == null) continue;
            s.cameraDistance = Vector3.Distance(eye, s.transform.position);
            _sorted.Add(s);
        }
        registeredShipCount = _sorted.Count;

        _sorted.Sort(CompareByDistance);

        int on = 0;
        for (int i = 0; i < _sorted.Count; i++)
        {
            ShipDecalLOD s = _sorted[i];

            // 이미 켜져 있는 배는 조금 더 멀어져야 꺼진다. 경계에서 깜빡이는 것을 막는다.
            float limit = s.decalsOn ? maxDistance + hysteresis : maxDistance;
            bool want = on < maxActiveShips && s.cameraDistance <= limit;

            s.SetDecalsActive(want);
            if (want) on++;
        }

        activeShipCount = on;
        activeDecalCount = on * 2;
    }

    static int CompareByDistance(ShipDecalLOD a, ShipDecalLOD b)
    {
        return a.cameraDistance.CompareTo(b.cameraDistance);
    }
}
