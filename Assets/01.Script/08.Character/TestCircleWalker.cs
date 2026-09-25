using UnityEngine;

/// <summary>
/// [테스트용] 캐릭터를 원을 그리며 걷게 한다. 제자리 걷기 클립만으로는 몸이 이동하지 않아
/// 치마·머리카락 흔들림(관성)을 확인하기 어려워서 만든 것. 게임 로직에는 쓰지 않는다.
/// </summary>
public class TestCircleWalker : MonoBehaviour
{
    public Vector3 center = Vector3.zero;
    public float radius = 2f;
    [Tooltip("걷는 속도 m/s (테스트 걷기 클립 보폭 기준 약 0.85)")] public float speed = 0.85f;
    public bool clockwise = false;

    float angle;

    void Start()
    {
        Vector3 d = transform.position - center; d.y = 0f;
        if (d.sqrMagnitude < 1e-4f) { transform.position = center + new Vector3(radius, 0f, 0f); d = new Vector3(radius, 0f, 0f); }
        angle = Mathf.Atan2(d.z, d.x);
    }

    void Update()
    {
        float sign = clockwise ? -1f : 1f;
        angle += sign * speed / Mathf.Max(0.1f, radius) * Time.deltaTime;
        Vector3 p = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
        Vector3 fwd = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * sign;
        transform.SetPositionAndRotation(p, Quaternion.LookRotation(fwd, Vector3.up));
    }
}
