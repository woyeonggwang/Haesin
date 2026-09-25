using UnityEngine;

/// <summary>
/// 배의 손상 불·연기 파티클(DamageFX)을 카메라에서 멀어지면 끈다.
/// 파티클은 빌보드라 크기가 화면에서 줄지 않고 안개 처리도 약해서, 수백 m 밖 적함의 불이
/// 그대로 보였다. 렌더러만 끄므로 파티클 시뮬레이션·ShipBurnVisual 의 켜고 끄기에는 영향이 없다.
/// 히스테리시스: hideDistance 밖에서 끄고 showDistance 안으로 들어와야 다시 켠다.
/// </summary>
public class DamageFXDistanceCull : MonoBehaviour
{
    [Tooltip("이 거리(m)보다 멀어지면 불·연기를 그리지 않는다.")]
    public float hideDistance = 320f;
    [Tooltip("이 거리(m) 안으로 들어오면 다시 그린다 (hideDistance 보다 작게).")]
    public float showDistance = 280f;
    [Tooltip("거리 확인 간격(초).")]
    public float checkInterval = 0.5f;

    ParticleSystemRenderer[] _renderers;
    float _next;
    bool _visible = true;

    void Awake()
    {
        _renderers = GetComponentsInChildren<ParticleSystemRenderer>(true);
    }

    void Update()
    {
        if (Time.time < _next) return;
        _next = Time.time + checkInterval;
        Camera cam = Camera.main;
        if (cam == null) return;

        float d = Vector3.Distance(cam.transform.position, transform.position);
        bool want = _visible ? d < hideDistance : d < showDistance;
        if (want == _visible) return;
        _visible = want;
        for (int i = 0; i < _renderers.Length; i++)
            if (_renderers[i] != null) _renderers[i].enabled = want;
    }
}
