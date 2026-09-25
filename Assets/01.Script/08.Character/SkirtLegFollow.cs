using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 치마 가닥의 뿌리 본(Skirt_X_01)이 허벅지 흔들림(비틀림 제외)을 weight 만큼 따라가게 하고,
/// 빠른 동작 뒤 자락이 다리 뒤(몸 쪽)에 끼어 남으면 잠깐 다리 충돌을 꺼서 풀어 준다.
///
/// 따라가기: Magica Cloth 2 BoneCloth 의 고정 본을 LateUpdate 에서 돌려 두면, Magica 는 LateUpdate 뒤(afterLateUpdate)에
/// 그 자세를 기준으로 계산한다. 휴식 자세는 Awake 시점(씬에 놓인 기본 자세).
///
/// 끼임 풀기: 빠르게 다리를 내딛으면(공격 런지 등) 다리 콜라이더가 자락을 지나쳐 반대쪽(몸 쪽)에서 밀어내고,
/// 그 뒤로는 충돌 때문에 다시 앞으로 나오지 못해 "다리가 치마 앞으로 튀어나온" 모습으로 굳는다(Play 에서 확인).
/// 렌더 직전(시뮬레이션 결과)에 한 가닥 허벅지를 따라가는 가닥(FL·FR·L·R)의 본이 허벅지 축보다 몸 쪽에 있으면
/// untrapDuration 동안 치마의 콜라이더 충돌을 끈 뒤 되돌린다.
/// </summary>
[DisallowMultipleComponent]
public class SkirtLegFollow : MonoBehaviour
{
    [System.Serializable]
    public class Entry
    {
        [Tooltip("치마 가닥 뿌리 본 (예: Skirt_FR_01).")] public Transform bone;
        [Tooltip("따라갈 허벅지 본(여러 개면 평균).")] public Transform[] follow;
        [Tooltip("따라가는 정도 0~1.")] [Range(0f, 1f)] public float weight = 0.7f;
        [Tooltip("다리 뒤 끼임 검사(허벅지 하나를 따라가는 가닥만 검사한다).")] public bool checkTrap = true;
        [System.NonSerialized] public Quaternion restLocal;
        [System.NonSerialized] public Transform[] followEnd;
        [System.NonSerialized] public Vector3[] restDir;
        [System.NonSerialized] public Transform[] strand;
    }

    public Entry[] entries = new Entry[0];
    [Tooltip("따라가기 전체 배율(0 이면 따라가기 끔). 끼임 풀기와는 별개.")] [Range(0f, 1f)] public float globalWeight = 1f;
    [Tooltip("뿌리 아래 첫 본(_02)이 따라가는 허벅지 축에서 바깥쪽으로 최소 이 거리(m) 떨어지도록 뿌리를 더 벌린다(허벅지 콜라이더 반지름 0.09 보다 크게). 0 이면 끔.")] public float clearance = 0.12f;
    [Tooltip("벌리는 각도 최대(도).")] public float maxClearanceAngle = 45f;

    [Header("다리 뒤 끼임 풀기 (Magica Cloth 2)")]
    [Tooltip("치마 MagicaCloth. 비우면 자식 중 이름이 Cloth_Skirt 인 것을 찾는다.")] public MagicaCloth2.MagicaCloth cloth;
    public bool untrap = true;
    [Tooltip("가닥 본이 허벅지 축보다 몸 쪽으로 이 거리(m) 넘게 들어가면 낀 것으로 본다.")] public float trapDepth = 0.03f;
    [Tooltip("낀 것을 찾으면 이 시간(초) 동안 치마 콜라이더 충돌을 끈다.")] public float untrapDuration = 0.35f;
    [Tooltip("충돌을 되돌린 뒤 다시 검사하기까지 쉬는 시간(초).")] public float untrapCooldown = 0.4f;
    [Tooltip("읽기 전용: 지금까지 푼 횟수.")] public int untrapCount;
    [Tooltip("읽기 전용: 마지막 검사에서 낀 가닥.")] public string lastTrapped = "";

    bool _trappedSeen; int _checkedFrame = -1;
    bool _untrapping; float _untrapUntil; float _cooldownUntil;
    MagicaCloth2.ColliderCollisionConstraint.Mode _savedMode;

    void Awake()
    {
        if (cloth == null)
            foreach (var c in GetComponentsInChildren<MagicaCloth2.MagicaCloth>(true)) if (c.name == "Cloth_Skirt") { cloth = c; break; }

        foreach (var e in entries)
        {
            if (e.bone == null || e.bone.parent == null || e.follow == null) continue;
            e.restLocal = e.bone.localRotation;
            e.followEnd = new Transform[e.follow.Length];
            e.restDir = new Vector3[e.follow.Length];
            Quaternion inv = Quaternion.Inverse(e.bone.parent.rotation);
            for (int i = 0; i < e.follow.Length; i++)
            {
                var f = e.follow[i];
                if (f == null || f.childCount == 0) continue;
                // 무릎 = 자손이 가장 많은 자식
                Transform best = f.GetChild(0);
                for (int c = 1; c < f.childCount; c++)
                    if (f.GetChild(c).GetComponentsInChildren<Transform>(true).Length > best.GetComponentsInChildren<Transform>(true).Length) best = f.GetChild(c);
                e.followEnd[i] = best;
                e.restDir[i] = inv * (best.position - f.position).normalized;
            }
            // 가닥의 뿌리 아래 본들(_02, _03, _04 …)
            var list = new List<Transform>();
            Transform t = e.bone;
            while (t.childCount > 0)
            {
                Transform next = null;
                for (int c = 0; c < t.childCount; c++) if (t.GetChild(c).name.StartsWith("Skirt")) { next = t.GetChild(c); break; }
                if (next == null) break;
                list.Add(next); t = next;
            }
            e.strand = list.ToArray();
        }
    }

    void OnEnable() { RenderPipelineManager.beginContextRendering += OnBeginRender; }

    void OnDisable()
    {
        RenderPipelineManager.beginContextRendering -= OnBeginRender;
        if (_untrapping) Restore();
    }

    void LateUpdate()
    {
        // 1) 따라가기
        foreach (var e in entries)
        {
            if (e.bone == null || e.bone.parent == null || e.followEnd == null) continue;
            Quaternion inv = Quaternion.Inverse(e.bone.parent.rotation);
            Quaternion avg = Quaternion.identity; int n = 0;
            for (int i = 0; i < e.follow.Length; i++)
            {
                if (e.follow[i] == null || e.followEnd[i] == null) continue;
                Vector3 cur = inv * (e.followEnd[i].position - e.follow[i].position).normalized;
                Quaternion swing = Quaternion.FromToRotation(e.restDir[i], cur);
                avg = n == 0 ? swing : Quaternion.Slerp(avg, swing, 1f / (n + 1));
                n++;
            }
            if (n == 0) continue;
            e.bone.localRotation = Quaternion.Slerp(Quaternion.identity, avg, e.weight * globalWeight) * e.restLocal;
            if (clearance > 0f) PushClear(e);
        }

        // 2) 끼임 풀기 (판정은 지난 프레임 렌더 직전의 시뮬레이션 결과)
        if (cloth == null) return;
        if (_untrapping)
        {
            if (Time.time >= _untrapUntil) Restore();
            return;
        }
        if (untrap && _trappedSeen && Time.time >= _cooldownUntil)
        {
            var sd = cloth.SerializeData;
            _savedMode = sd.colliderCollisionConstraint.mode;
            if (_savedMode == MagicaCloth2.ColliderCollisionConstraint.Mode.None) return;
            sd.colliderCollisionConstraint.mode = MagicaCloth2.ColliderCollisionConstraint.Mode.None;
            cloth.SetParameterChange();
            _untrapping = true; _untrapUntil = Time.time + untrapDuration; untrapCount++;
        }
    }

    /// <summary>허벅지 축에서 본 _02 의 바깥쪽 거리(+ = 바깥, - = 몸 쪽). 허벅지 구간(k 0~1) 밖이면 NaN.</summary>
    static float OutsideDist(Vector3 p, Vector3 a, Vector3 b, Vector3 outwardHint, out Vector3 outward)
    {
        Vector3 ab = b - a; float len2 = ab.sqrMagnitude; outward = Vector3.zero;
        if (len2 < 1e-6f) return float.NaN;
        Vector3 axis = ab / Mathf.Sqrt(len2);
        float k = Vector3.Dot(p - a, ab) / len2;
        if (k < 0f || k > 1f) return float.NaN;
        outward = Vector3.ProjectOnPlane(outwardHint, axis);
        if (outward.sqrMagnitude < 1e-6f) return float.NaN;
        outward.Normalize();
        return Vector3.Dot(Vector3.ProjectOnPlane(p - (a + ab * k), axis), outward);
    }

    /// <summary>가닥 기본 자세가 허벅지 안으로 들어가면 뿌리를 바깥쪽으로 더 벌린다 → Magica 충돌이 항상 바깥쪽으로 밀어낸다.</summary>
    void PushClear(Entry e)
    {
        if (e.strand == null || e.strand.Length == 0) return;
        for (int i = 0; i < e.follow.Length; i++)
        {
            if (e.follow[i] == null || e.followEnd[i] == null) continue;
            Vector3 p = e.strand[0].position;
            Vector3 outward;
            float cur = OutsideDist(p, e.follow[i].position, e.followEnd[i].position, e.bone.forward, out outward);
            if (float.IsNaN(cur) || cur >= clearance) continue;
            Vector3 arm = p - e.bone.position; float L = arm.magnitude; if (L < 1e-4f) continue;
            Vector3 dir = arm / L;
            Vector3 rotAxis = Vector3.Cross(dir, outward); if (rotAxis.sqrMagnitude < 1e-8f) continue;
            float ang = Mathf.Min(maxClearanceAngle, Mathf.Atan2(clearance - cur, L) * Mathf.Rad2Deg);
            e.bone.rotation = Quaternion.AngleAxis(ang, rotAxis.normalized) * e.bone.rotation;
        }
    }

    void Restore()
    {
        _untrapping = false; _trappedSeen = false;
        if (cloth != null)
        {
            cloth.SerializeData.colliderCollisionConstraint.mode = _savedMode;
            cloth.SetParameterChange();
        }
        _cooldownUntil = Time.time + untrapCooldown;
    }

    /// <summary>렌더 직전 = Magica 가 본에 결과를 쓴 뒤. 끼임 여부만 기록한다.</summary>
    void OnBeginRender(ScriptableRenderContext ctx, List<Camera> cams)
    {
        if (!Application.isPlaying || Time.frameCount == _checkedFrame) return;
        _checkedFrame = Time.frameCount;
        bool any = false; string who = "";
        foreach (var e in entries)
        {
            if (!e.checkTrap || e.bone == null || e.strand == null || e.strand.Length == 0 || e.follow == null || e.followEnd == null) continue;
            for (int i = 0; i < e.follow.Length && !any; i++)
            {
                if (e.follow[i] == null || e.followEnd[i] == null) continue;
                Vector3 o;
                float dd = OutsideDist(e.strand[0].position, e.follow[i].position, e.followEnd[i].position, e.bone.forward, out o);
                if (!float.IsNaN(dd) && dd < -trapDepth) { any = true; who = e.bone.name; }
            }
            if (any) break;
        }
        _trappedSeen = any;
        if (any) lastTrapped = who;
    }
}
