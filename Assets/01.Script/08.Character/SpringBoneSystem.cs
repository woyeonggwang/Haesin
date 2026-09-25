using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 치마·포니테일 같은 흔들리는 본 체인을 간단한 Verlet 스프링으로 움직이는 컴포넌트.
///
/// - 외부 에셋(Magica Cloth, Dynamic Bone 등) 없이 쓰려고 직접 작성함. 방식은 VRM SpringBone 과 같은 계열:
///   매 스텝 본을 휴식(바인드) 자세로 되돌린 뒤, 꼬리 위치를 관성 + 복원력 + 중력으로 옮기고
///   본 길이로 다시 맞춘 다음 콜라이더 밖으로 밀어내고, 본을 그 방향으로 돌린다.
/// - 본은 "이름"으로 찾는다. 머리카락 본(Hair_*)은 CharacterOutfit 이 입힐 때 만들어지므로
///   옷을 갈아입으면(OnOutfitChanged) 체인을 다시 구성한다.
/// - Animator 가 본을 움직인 뒤에 계산해야 하므로 LateUpdate 에서 돈다.
/// - 콜라이더는 양 끝 굵기가 다른 캡슐(허벅지처럼 위가 굵고 아래가 가는 모양)을 쓸 수 있고,
///   segmentCollision 이 켜져 있으면 본의 꼬리 점만이 아니라 본 전체(선분)가 콜라이더를 뚫지 않게 돌려 준다.
/// - restPoseAllowance: 휴식 자세에서 이미 콜라이더와 겹쳤던 깊이만큼은 허용(서 있기만 해도 치마가 벌어지는 것 방지).
///
/// 떨림 방지 (2026-09-24):
/// - 예전에는 콜라이더가 밀어낸 거리가 그대로 "속도"가 되어 다음 프레임에 튕겨 나갔다가 복원력·중력으로
///   다시 다리로 돌아와 부딪히는 것을 반복 → 치마가 떨렸다(콜라이더를 빼면 떨림이 거의 0 이었음).
///   이제 밀어낼 때는 다리 쪽으로 향하던 속도만 없애고(미끄러짐) 튕김은 collisionBounce 만큼만 준다.
/// - 화면 프레임(에디터에서 200fps 이상도 나옴)과 상관없이 초당 simulationRate 번 고정 간격으로 계산하고,
///   그 사이 프레임은 두 계산 결과를 보간해서 보여 준다. (프레임이 높을수록 더 떨리던 문제)
/// </summary>
[DefaultExecutionOrder(200)]
public class SpringBoneSystem : MonoBehaviour
{
    [System.Serializable]
    public class ChainSetting
    {
        [Tooltip("표시용 이름")] public string label = "Chain";
        [Tooltip("체인 시작 본 이름들. 각 본에서 자식을 한 줄로 따라 내려가며 체인을 만든다")]
        public string[] rootBones;
        [Tooltip("휴식 자세로 돌아가려는 힘 (클수록 뻣뻣하고 빨리 되돌아옴)")] public float stiffness = 1.5f;
        [Tooltip("속도 감쇠 0~1 (클수록 덜 출렁이고 천천히 움직임). 1/60초 기준")] [Range(0f, 1f)] public float drag = 0.35f;
        [Tooltip("중력 세기 (클수록 빨리 처짐)")] public float gravity = 0.3f;
        public Vector3 gravityDir = Vector3.down;
        [Tooltip("본 끝의 충돌 반지름 (m). 옷 두께 + 여유")] public float hitRadius = 0.02f;
        [Tooltip("휴식 방향에서 벗어날 수 있는 최대 각도 (도). 0 이면 제한 없음. 콜라이더 밀어내기가 이 제한보다 우선한다")] public float maxAngle = 60f;
        [Tooltip("이 체인이 부딪히는 콜라이더 그룹 이름")] public string colliderGroup = "";
        [Tooltip("(다리 따라가기) 체인 첫 마디의 휴식 방향을 이 본들(예: 허벅지)이 흔들리는 만큼 같이 돌린다. 여러 개면 평균.\n" +
                 "치마 앞자락이 허벅지에 계속 부딪혀 튕기는(펄럭이는) 것을 줄인다. 비우면 안 씀.")] public string[] followBones;
        [Tooltip("다리 따라가기 비율 0~1 (0 = 안 따라감, 1 = 허벅지와 같이 돔)")] [Range(0f, 1f)] public float followWeight = 0f;
    }

    [System.Serializable]
    public class CapsuleSetting
    {
        public string group = "";
        [Tooltip("캡슐 한쪽 끝 본")] public string boneA;
        [Tooltip("캡슐 다른 끝 본 (비우면 boneA 위치의 구)")] public string boneB;
        [Tooltip("boneA 로컬 오프셋")] public Vector3 offsetA;
        [Tooltip("boneB 로컬 오프셋")] public Vector3 offsetB;
        [Tooltip("boneA 쪽 반지름 (m)")] public float radius = 0.07f;
        [Tooltip("boneB 쪽 반지름 (m). 0 이하면 radius 와 같음 (굵기가 일정한 캡슐)")] public float radiusB = -1f;
    }

    public List<ChainSetting> chains = new List<ChainSetting>();
    public List<CapsuleSetting> colliders = new List<CapsuleSetting>();
    [Tooltip("물리를 초당 몇 번 계산할지. 화면 프레임과 상관없이 일정하게 돌고, 사이 프레임은 보간한다")]
    [Range(20f, 120f)] public float simulationRate = 60f;
    [Tooltip("한 프레임에 몰아서 계산할 최대 횟수 (프레임이 끊겼을 때 폭주 방지)")] public int maxSubSteps = 4;
    [Tooltip("다리 등 콜라이더에 부딪혔을 때 튕겨 나가는 정도 0~1.\n0 = 튕김 없이 미끄러짐(떨림 없음), 1 = 들어오던 속도만큼 반대로 튕김.\n크게 하면 다리에 부딪혔다 돌아오기를 반복해 떨릴 수 있다")]
    [Range(0f, 1f)] public float collisionBounce = 0.1f;
    [Tooltip("콜라이더에 닿아 미끄러질 때 속도를 줄이는 정도 0~1 (클수록 다리에 닿은 자락이 덜 미끄러짐)")]
    [Range(0f, 1f)] public float collisionFriction = 0.2f;
    [Tooltip("캐릭터가 이동·회전할 때 그 움직임을 물리에 얼마나 반영할지 0~1.\n" +
             "1 = 월드 기준(이동하면 계속 뒤로 끌림), 0 = 캐릭터에 붙어서 따라감(이동 영향 없음, 뼈 움직임만 반영).\n" +
             "drag 가 월드 속도를 깎는 방식이라 1 이면 걷기만 해도 자락이 계속 뒤로 누워 있어 낮춤")]
    [Range(0f, 1f)] public float worldInfluence = 0.35f;
    [Tooltip("켜면 본 전체(선분)로 충돌 검사. 끄면 예전처럼 꼬리 점만 검사")] public bool segmentCollision = true;
    [Tooltip("콜라이더 밀어내기 반복 횟수 (여러 콜라이더가 겹칠 때 안정화)")] [Range(1, 4)] public int collisionIterations = 2;
    [Tooltip("휴식 자세에서 이미 콜라이더와 겹친 깊이만큼은 허용 (서 있기만 해도 치마가 벌어지는 것 방지)")] public bool restPoseAllowance = true;
    [Tooltip("위 허용 깊이의 최대값 (m). 너무 크면 그만큼 뚫려도 밀어내지 않는다")] public float maxAllowance = 0.04f;
    public bool drawGizmos = true;

    class Joint
    {
        public Transform t;
        public Quaternion initLocalRot;
        public Vector3 boneAxis;   // 로컬 공간의 꼬리 방향 (단위 벡터)
        public float length;
        public Vector3 curTail, prevTail;        // 월드 (Verlet 용)
        public Vector3 curLocal, prevLocal;      // 캐릭터 루트 기준 (Verlet 용)
        public Vector3 showPrev, showCur;        // 캐릭터 루트 기준, 직전/최신 계산 결과 (보간 표시용)
        public float[] allow;                    // ch.cols 순서대로, 휴식 자세에서 겹쳐 있던 깊이
        public bool isRoot;                      // 체인 첫 마디 (다리 따라가기 적용 대상)
    }

    class Chain
    {
        public ChainSetting s;
        public readonly List<Joint> joints = new List<Joint>();
        public readonly List<Capsule> cols = new List<Capsule>();
        public readonly List<Transform> follow = new List<Transform>();      // 따라갈 본(허벅지)
        public readonly List<Transform> followChild = new List<Transform>(); // 그 본의 자식(무릎) — 방향 계산용
        public readonly List<Vector3> followRestDir = new List<Vector3>();   // 휴식 자세에서 루트 부모 기준 허벅지 방향
    }

    class Capsule
    {
        public CapsuleSetting s;
        public Transform a, b;
        public Vector3 wa, wb;
        public float RadiusAt(float u)
        {
            float rb = s.radiusB > 0f ? s.radiusB : s.radius;
            return Mathf.Lerp(s.radius, rb, u);
        }
    }

    readonly List<Chain> built = new List<Chain>();
    readonly List<Capsule> allCols = new List<Capsule>();
    readonly Dictionary<string, Transform> map = new Dictionary<string, Transform>();
    float accumulator;

    void Start() { Rebuild(); }

    void OnEnable() { if (built.Count > 0) ResetTails(); }

    /// <summary>CharacterOutfit 이 옷을 바꾼 뒤 SendMessage 로 부른다.</summary>
    public void OnOutfitChanged() { Rebuild(); }

    public void Rebuild()
    {
        // 먼저 이전 체인을 휴식 자세로 되돌려 둔다 (반쯤 휜 자세를 새 기준으로 잡지 않도록)
        foreach (var c in built)
            foreach (var j in c.joints)
                if (j.t != null) j.t.localRotation = j.initLocalRot;

        built.Clear(); allCols.Clear(); map.Clear();
        accumulator = 0f;
        foreach (var t in GetComponentsInChildren<Transform>(true))
            if (!map.ContainsKey(t.name)) map.Add(t.name, t);

        foreach (var cs in colliders)
        {
            Transform a;
            if (!map.TryGetValue(cs.boneA, out a)) continue;
            Transform b = null;
            if (!string.IsNullOrEmpty(cs.boneB)) map.TryGetValue(cs.boneB, out b);
            allCols.Add(new Capsule { s = cs, a = a, b = b });
        }
        UpdateCapsules();

        foreach (var cs in chains)
        {
            if (cs.rootBones == null) continue;
            foreach (var rootName in cs.rootBones)
            {
                Transform root;
                if (!map.TryGetValue(rootName, out root)) continue;   // 이 옷/머리엔 없는 체인
                var ch = new Chain { s = cs };
                if (cs.followBones != null && root.parent != null)
                    foreach (var fn in cs.followBones)
                    {
                        Transform f;
                        if (!map.TryGetValue(fn, out f) || f.childCount == 0) continue;
                        var fc = f.GetChild(0);
                        ch.follow.Add(f); ch.followChild.Add(fc);
                        ch.followRestDir.Add(Quaternion.Inverse(root.parent.rotation) * (fc.position - f.position).normalized);
                    }
                foreach (var c in allCols) if (c.s.group == cs.colliderGroup) ch.cols.Add(c);
                for (var t = root; t != null; t = t.childCount > 0 ? t.GetChild(0) : null)
                {
                    var j = new Joint { t = t, initLocalRot = t.localRotation };
                    Vector3 tailWorld;
                    if (t.childCount > 0) tailWorld = t.GetChild(0).position;
                    else tailWorld = t.position + (t.position - t.parent.position);   // 마지막 본: 앞 마디 방향으로 연장
                    j.length = Vector3.Distance(t.position, tailWorld);
                    if (j.length < 1e-4f) continue;
                    j.boneAxis = t.InverseTransformPoint(tailWorld).normalized;
                    j.curTail = j.prevTail = tailWorld;
                    j.curLocal = j.prevLocal = j.showPrev = j.showCur = transform.InverseTransformPoint(tailWorld);
                    j.allow = new float[ch.cols.Count];
                    if (restPoseAllowance)
                        for (int k = 0; k < ch.cols.Count; k++)
                            j.allow[k] = Mathf.Min(RestOverlap(ch.cols[k], t.position, tailWorld, cs.hitRadius), maxAllowance);
                    j.isRoot = ch.joints.Count == 0;
                    ch.joints.Add(j);
                }
                if (ch.joints.Count > 0) built.Add(ch);
            }
        }
    }

    public void ResetTails()
    {
        accumulator = 0f;
        foreach (var c in built)
            foreach (var j in c.joints)
            {
                if (j.t == null) continue;
                j.t.localRotation = j.initLocalRot;
                Vector3 w = j.t.position + j.t.rotation * j.boneAxis * j.length;
                j.curTail = j.prevTail = w;
                j.curLocal = j.prevLocal = j.showPrev = j.showCur = transform.InverseTransformPoint(w);
            }
    }

    void LateUpdate()
    {
        Tick(Time.deltaTime);
    }

    /// <summary>
    /// 프레임마다 부른다: 쌓인 시간만큼 고정 간격(1/simulationRate) 계산을 하고, 남는 시간만큼 보간해서 본을 둔다.
    /// </summary>
    public void Tick(float frameDt)
    {
        if (frameDt <= 0f || built.Count == 0) return;
        float h = 1f / Mathf.Max(1f, simulationRate);
        accumulator += Mathf.Min(frameDt, h * maxSubSteps);
        int steps = 0;
        while (accumulator >= h && steps < maxSubSteps)
        {
            Simulate(h);
            accumulator -= h;
            steps++;
        }
        if (accumulator > h) accumulator = h;
        ApplyPose(Mathf.Clamp01(accumulator / h));
    }

    /// <summary>고정 간격 한 번 계산하고 그 결과 그대로 본을 둔다 (에디터 검증 코드용).</summary>
    public void Step(float dt)
    {
        Simulate(dt);
        ApplyPose(1f);
    }

    /// <summary>휴식 회전. 체인 첫 마디는 followBones(허벅지)의 흔들림(비틀림 제외)을 followWeight 만큼 더한다.</summary>
    static Quaternion RestRotation(Chain ch, Joint j)
    {
        if (!j.isRoot || ch.follow.Count == 0 || ch.s.followWeight <= 0f || j.t.parent == null) return j.initLocalRot;
        Quaternion inv = Quaternion.Inverse(j.t.parent.rotation);
        Quaternion avg = Quaternion.identity;
        for (int i = 0; i < ch.follow.Count; i++)
        {
            Vector3 cur = inv * (ch.followChild[i].position - ch.follow[i].position).normalized;
            Quaternion swing = Quaternion.FromToRotation(ch.followRestDir[i], cur);
            avg = i == 0 ? swing : Quaternion.Slerp(avg, swing, 1f / (i + 1));
        }
        return Quaternion.Slerp(Quaternion.identity, avg, ch.s.followWeight) * j.initLocalRot;
    }

    void UpdateCapsules()
    {
        foreach (var c in allCols)
        {
            c.wa = c.a.TransformPoint(c.s.offsetA);
            c.wb = c.b != null ? c.b.TransformPoint(c.s.offsetB) : c.wa;
        }
    }

    void Simulate(float dt)
    {
        UpdateCapsules();

        for (int ci = built.Count - 1; ci >= 0; ci--)
        {
            var ch = built[ci];
            var s = ch.s;
            // drag 는 1/60초 기준 값 → 계산 간격에 맞춰 환산
            float keep = Mathf.Pow(Mathf.Clamp01(1f - s.drag), dt * 60f);
            foreach (var j in ch.joints)
            {
                if (j.t == null) { built.RemoveAt(ci); break; }   // 옷을 벗어 본이 사라짐

                j.t.localRotation = RestRotation(ch, j);
                Vector3 head = j.t.position;
                Vector3 restDir = j.t.rotation * j.boneAxis;

                // 캐릭터 루트 이동은 worldInfluence 만큼만 반영 (나머지는 루트와 함께 움직인 것으로 본다)
                Vector3 cur = Vector3.Lerp(transform.TransformPoint(j.curLocal), j.curTail, worldInfluence);
                Vector3 prev = Vector3.Lerp(transform.TransformPoint(j.prevLocal), j.prevTail, worldInfluence);

                Vector3 next = cur
                             + (cur - prev) * keep
                             + restDir * (s.stiffness * dt)
                             + s.gravityDir.normalized * (s.gravity * dt);
                next = head + (next - head).normalized * j.length;

                // 각도 제한을 먼저 하고, 콜라이더 밀어내기를 마지막에 한다 (뚫리는 것보다 각도 초과가 낫다)
                if (s.maxAngle > 0f)
                {
                    Vector3 d = (next - head) / j.length;
                    float ang = Vector3.Angle(restDir, d);
                    if (ang > s.maxAngle)
                        next = head + Vector3.Slerp(restDir, d, s.maxAngle / ang) * j.length;
                }

                Vector3 free = next;
                for (int it = 0; it < collisionIterations; it++)
                    for (int k = 0; k < ch.cols.Count; k++)
                    {
                        float hit = s.hitRadius - (j.allow != null && k < j.allow.Length ? j.allow[k] : 0f);
                        next = segmentCollision ? CollideSegment(ch.cols[k], head, next, j.length, hit)
                                                : PushOut(ch.cols[k], next, hit);
                    }
                next = head + (next - head).normalized * j.length;

                // 밀어낸 거리는 속도로 바꾸지 않는다: 콜라이더 쪽으로 향하던 속도만 없애고(+튕김), 나머지는 마찰만큼 줄인다
                Vector3 newPrev = cur;
                Vector3 push = next - free;
                if (push.sqrMagnitude > 1e-12f)
                {
                    Vector3 nrm = push.normalized;
                    Vector3 vel = free - cur;
                    float vn = Vector3.Dot(vel, nrm);
                    if (vn < 0f) vel -= nrm * vn * (1f + collisionBounce);
                    vel *= 1f - collisionFriction;
                    newPrev = next - vel;
                }

                j.prevTail = newPrev;
                j.curTail = next;
                j.prevLocal = transform.InverseTransformPoint(newPrev);
                j.curLocal = transform.InverseTransformPoint(next);
                j.showPrev = j.showCur;
                j.showCur = j.curLocal;
                j.t.rotation = Quaternion.FromToRotation(restDir, next - head) * j.t.rotation;
            }
        }
    }

    /// <summary>직전·최신 계산 결과(캐릭터 루트 기준)를 alpha 로 보간해 본을 둔다. 애니메이션이 움직인 부모를 그대로 따라간다.</summary>
    void ApplyPose(float alpha)
    {
        for (int ci = built.Count - 1; ci >= 0; ci--)
        {
            var ch = built[ci];
            foreach (var j in ch.joints)
            {
                if (j.t == null) { built.RemoveAt(ci); break; }
                j.t.localRotation = RestRotation(ch, j);
                Vector3 head = j.t.position;
                Vector3 restDir = j.t.rotation * j.boneAxis;
                Vector3 target = transform.TransformPoint(Vector3.Lerp(j.showPrev, j.showCur, alpha));
                Vector3 dir = target - head;
                if (dir.sqrMagnitude < 1e-10f) continue;
                j.t.rotation = Quaternion.FromToRotation(restDir, dir) * j.t.rotation;
            }
        }
    }

    /// <summary>두 선분(p1→q1, p2→q2)의 최근접점 매개변수 s(첫 선분), u(둘째 선분).</summary>
    static void ClosestParams(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2, out float s, out float u)
    {
        Vector3 d1 = q1 - p1, d2 = q2 - p2, r0 = p1 - p2;
        float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r0);
        float cc = Vector3.Dot(d1, r0);
        if (a < 1e-10f && e <= 1e-8f) { s = 0f; u = 0f; return; }
        if (a < 1e-10f) { s = 0f; u = Mathf.Clamp01(f / e); return; }
        if (e <= 1e-8f) { u = 0f; s = Mathf.Clamp01(-cc / a); return; }
        float b = Vector3.Dot(d1, d2);
        float denom = a * e - b * b;
        s = denom > 1e-8f ? Mathf.Clamp01((b * f - cc * e) / denom) : 0f;
        u = (b * s + f) / e;
        if (u < 0f) { u = 0f; s = Mathf.Clamp01(-cc / a); }
        else if (u > 1f) { u = 1f; s = Mathf.Clamp01((b - cc) / a); }
    }

    /// <summary>휴식 자세의 본 선분이 캡슐(+hit)에 얼마나 겹쳐 있는지 (겹치지 않으면 0).</summary>
    static float RestOverlap(Capsule c, Vector3 head, Vector3 tail, float hit)
    {
        float s, u;
        ClosestParams(head, tail, c.wa, c.wb, out s, out u);
        float dist = Vector3.Distance(Vector3.Lerp(head, tail, s), Vector3.Lerp(c.wa, c.wb, u));
        return Mathf.Max(0f, c.RadiusAt(u) + hit - dist);
    }

    /// <summary>점 하나(꼬리)를 캡슐 밖으로 밀어낸다. hit 는 캡슐 반지름에 더할 여유(음수 가능).</summary>
    static Vector3 PushOut(Capsule c, Vector3 p, float hit)
    {
        Vector3 ab = c.wb - c.wa;
        float len2 = ab.sqrMagnitude;
        float t = len2 > 1e-8f ? Mathf.Clamp01(Vector3.Dot(p - c.wa, ab) / len2) : 0f;
        Vector3 q = c.wa + ab * t;
        Vector3 d = p - q;
        float r = c.RadiusAt(t) + hit;
        float dist = d.magnitude;
        if (r <= 0f || dist >= r || dist < 1e-6f) return p;
        return q + d / dist * r;
    }

    /// <summary>
    /// 본 선분(head→tail) 과 캡슐 축 선분의 최근접점을 구해, 본이 캡슐 안을 지나가면
    /// head 를 중심으로 본을 돌려 그 점이 캡슐 표면 밖에 오도록 한다. 결과는 새 꼬리 위치.
    /// </summary>
    static Vector3 CollideSegment(Capsule c, Vector3 head, Vector3 tail, float len, float hit)
    {
        if ((tail - head).sqrMagnitude < 1e-10f) return tail;
        float s, u;
        ClosestParams(head, tail, c.wa, c.wb, out s, out u);
        Vector3 P = Vector3.Lerp(head, tail, s), Q = Vector3.Lerp(c.wa, c.wb, u);
        float rad = c.RadiusAt(u) + hit;
        if (rad <= 0f) return tail;
        Vector3 diff = P - Q;
        float dist = diff.magnitude;
        if (dist >= rad) return tail;
        if (s < 0.05f) return PushOut(c, tail, hit);        // 본 뿌리 자체가 안쪽: 꼬리만 밀어낸다
        Vector3 n = dist > 1e-6f ? diff / dist : Vector3.Cross(c.wb - c.wa, tail - head).normalized;
        if (n.sqrMagnitude < 1e-8f) return tail;
        Vector3 dir = Q + n * rad - head;
        if (dir.sqrMagnitude < 1e-10f) return tail;
        return head + dir.normalized * len;
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.6f);
        foreach (var c in allCols)
        {
            Gizmos.DrawWireSphere(c.wa, c.RadiusAt(0f));
            Gizmos.DrawWireSphere(c.wb, c.RadiusAt(1f));
            Gizmos.DrawLine(c.wa, c.wb);
        }
        Gizmos.color = Color.yellow;
        foreach (var ch in built)
            foreach (var j in ch.joints)
                if (j.t != null) Gizmos.DrawLine(j.t.position, j.curTail);
    }
}
