using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 해신 환도 뽑기·넣기 동작 클립을 만든다(2026-10-05, 사용자 지시 "허리·등은 곧게, 칼을 오른쪽으로 뽑고 앞으로 옮겨 두 손으로 잡기",
/// 넣기도 같은 느낌, 상체만, 약 1초). 예전: ARPG 팩 Equip(0.82 s)·Unequip(0.65 s) — 몸을 왼쪽으로 틀며 뽑았음.
/// 방법: 캐릭터 복제본에 기준 자세(칼 없는 대기 rig_Idle_no_weapon ↔ 칼 든 대기 rig_Idle1)를 깔고, 프레임마다 칼의 자리를 정한 뒤
/// 오른팔(쇄골 보조 + 두 뼈 IK, 손목 비틀림은 아래팔에 나눔)을 칼 손잡이에, 왼팔을 칼집(뽑는 동안) → 손잡이(rig_Idle1 의 왼손-칼 상대 자리)에 맞추고,
/// HumanPoseHandler 로 근육 값을 읽어 휴머노이드 클립(.anim, 30 fps)으로 저장(근육 범위에 잘린 만큼은 목표를 옮겨 다시 — 최대 5번).
/// 칼 길: 칼집 축으로 빼기 → 칼끝을 칼집 입구에 둔 채 손잡이를 오른쪽 앞으로 → 오른쪽 옆으로 뻗기 → 앞으로 가져와 rig_Idle1 자세.
/// 칼집 돌리기: 이 캐릭터는 팔이 짧고(어깨→손잡이 최대 0.477 m) 칼집이 그대로면 손목이 꺾여 못 잡음 → 왼손이 칼집을 돌리고 미는 것처럼
/// 칼집 홀더(Hwando_Sheathed)를 잠깐 돌림(WeaponHolder 가 같은 시각표로 돌림 — TurnWeight). 돌리는 양은 OptimizeTurn 이 찾음.
/// 칼은 WeaponHolder 가 attach 시각에 칼집 ↔ 손 소켓으로 옮김. MCP 에서 Build(캐릭터 이름) 를 부름.
/// </summary>
public static class SwordDrawClipTool
{
    public const string Folder = "Assets/02.Resources/00.Component/06.Character/Haesin/Animations/Combat/Custom";
    const string IdlePath = "Assets/02.Resources/00.Component/06.Character/Haesin/Animations/Combat/ARPG/rig_Idle_no_weapon.anim";
    const string Idle1Path = "Assets/02.Resources/00.Component/06.Character/Haesin/Animations/Combat/ARPG/rig_Idle1.anim";
    public const float Fps = 30f;
    public const float DrawLength = 1.0f, DrawAttach = 0.26f;
    public const float SheatheLength = 1.0f, SheatheAttach = 0.70f;

    // 칼 길 (캐릭터 루트 기준)
    public static float PullDistance = 0.22f;
    public static Vector3 GuardB = new Vector3(0.34f, 1.16f, 0.34f);
    public static Vector3 GuardC = new Vector3(0.40f, 1.02f, 0.18f);
    public static Vector3 BladeC = new Vector3(0.85f, 0.15f, 0.50f);
    public static float LeftOnScabbard = 0.20f;
    // 칼집 돌리기(홀더 로컬, 다 돌렸을 때) — OptimizeTurn 결과를 넣음
    public static Vector3 TurnLocalPos = Vector3.zero;
    public static Vector3 TurnLocalEuler = new Vector3(32.13f, 246.02f, 331.64f);   // OptimizeTurn(2026-10-05): 루트 기준 yaw 40 · pitch 20 · 칼집 축 roll −90

    /// <summary>칼집 돌림 가중(0~1). WeaponHolder 와 같은 시각표.</summary>
    public static float TurnWeight(bool draw, float t)
    {
        if (draw) return Smooth(0.04f, 0.22f, t) * (1f - Smooth(0.60f, 0.85f, t));
        return Smooth(0.30f, 0.46f, t) * (1f - Smooth(0.74f, 0.92f, t));
    }

    struct Frame { public Vector3 pos; public Quaternion rot; }

    class Rig
    {
        public GameObject go; public Transform root; public Animator an; public HumanPoseHandler hph;
        public Transform sock, rUp, rLo, rHand, rClav, lUp, lLo, lHand, lClav, holder;
        public Vector3 holderPos0; public Quaternion holderRot0;
        public Quaternion sheathedSwordLocal = Quaternion.Euler(270f, 0f, 0f);
    }

    static Rig MakeRig(GameObject src)
    {
        var r = new Rig();
        r.go = Object.Instantiate(src);
        r.go.name = "__SwordDrawRig";
        r.go.hideFlags = HideFlags.DontSave;
        foreach (var mb in r.go.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
        r.root = r.go.transform; r.root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        r.an = r.go.GetComponent<Animator>();
        r.hph = new HumanPoseHandler(r.an.avatar, r.root);
        foreach (var t in r.go.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Socket_Hwando_Hand_R") r.sock = t;
            if (t.name == "Hwando_Sheathed") r.holder = t;
        }
        r.holderPos0 = r.holder.localPosition; r.holderRot0 = r.holder.localRotation;
        r.rUp = r.an.GetBoneTransform(HumanBodyBones.RightUpperArm); r.rLo = r.an.GetBoneTransform(HumanBodyBones.RightLowerArm); r.rHand = r.an.GetBoneTransform(HumanBodyBones.RightHand); r.rClav = r.an.GetBoneTransform(HumanBodyBones.RightShoulder);
        r.lUp = r.an.GetBoneTransform(HumanBodyBones.LeftUpperArm); r.lLo = r.an.GetBoneTransform(HumanBodyBones.LeftLowerArm); r.lHand = r.an.GetBoneTransform(HumanBodyBones.LeftHand); r.lClav = r.an.GetBoneTransform(HumanBodyBones.LeftShoulder);
        return r;
    }

    static GameObject Find(string name)
    {
        foreach (var a in Object.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (a.name == name) return a.gameObject;
        return null;
    }

    static float[] Muscles(Rig r, out Vector3 bp, out Quaternion br)
    {
        var hp = new HumanPose(); r.hph.GetHumanPose(ref hp);
        bp = hp.bodyPosition; br = hp.bodyRotation; return hp.muscles;
    }
    static void SetMuscles(Rig r, float[] m, Vector3 bp, Quaternion br)
    {
        var hp = new HumanPose { muscles = (float[])m.Clone(), bodyPosition = bp, bodyRotation = br };
        r.hph.SetHumanPose(ref hp);
        r.root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
    }
    static float[] Sample(Rig r, AnimationClip c, float t, out Vector3 bp, out Quaternion br)
    {
        c.SampleAnimation(r.go, t);
        r.root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        return Muscles(r, out bp, out br);
    }

    // ---------- IK ----------
    static void SolveArm(Transform up, Transform lo, Transform hand, Transform clav, Vector3 target, Quaternion handRot, Vector3 poleWorld)
    {
        float a = (lo.position - up.position).magnitude, b = (hand.position - lo.position).magnitude;
        if (clav != null)
        {
            Quaternion clav0 = clav.rotation;
            for (int i = 0; i < 12; i++)
            {
                if ((target - up.position).magnitude <= (a + b) * 0.97f) break;
                Quaternion q = Quaternion.FromToRotation(up.position - clav.position, target - clav.position);
                clav.rotation = Quaternion.Slerp(Quaternion.identity, q, 0.25f) * clav.rotation;
                if (Quaternion.Angle(clav0, clav.rotation) > 18f) break;
            }
        }
        Vector3 A = up.position;
        Vector3 AT = target - A;
        float d = Mathf.Clamp(AT.magnitude, 0.02f, (a + b) * 0.999f);
        Vector3 n = AT.normalized;
        float x = (a * a - b * b + d * d) / (2f * d);
        float h = Mathf.Sqrt(Mathf.Max(0f, a * a - x * x));
        Vector3 pole = Vector3.ProjectOnPlane(poleWorld, n);
        if (pole.sqrMagnitude < 1e-6f) pole = Vector3.ProjectOnPlane(Vector3.down, n);
        pole.Normalize();
        Vector3 E = A + n * x + pole * h;
        up.rotation = Quaternion.FromToRotation(lo.position - up.position, E - A) * up.rotation;
        Vector3 T = A + n * d;
        lo.rotation = Quaternion.FromToRotation(hand.position - lo.position, T - lo.position) * lo.rotation;
        Vector3 axis = (hand.position - lo.position).normalized;
        Quaternion rel = handRot * Quaternion.Inverse(hand.rotation);
        Vector3 proj = Vector3.Project(new Vector3(rel.x, rel.y, rel.z), axis);
        Quaternion twist = new Quaternion(proj.x, proj.y, proj.z, rel.w);
        float mag = Mathf.Sqrt(twist.x * twist.x + twist.y * twist.y + twist.z * twist.z + twist.w * twist.w);
        if (mag > 1e-4f)
        {
            twist = new Quaternion(twist.x / mag, twist.y / mag, twist.z / mag, twist.w / mag);
            lo.rotation = Quaternion.Slerp(Quaternion.identity, twist, 0.6f) * lo.rotation;
        }
        hand.rotation = handRot;
    }

    static void PlaceRightOnSword(Rig r, Frame sw)
    {
        Quaternion handRot = sw.rot * Quaternion.Inverse(r.sock.localRotation);
        Quaternion keep = r.rHand.rotation;
        r.rHand.rotation = handRot;
        Vector3 off = r.sock.position - r.rHand.position;
        r.rHand.rotation = keep;
        SolveArm(r.rUp, r.rLo, r.rHand, r.rClav, sw.pos - off, handRot, r.root.TransformDirection(new Vector3(0.45f, -0.8f, -0.35f)));
    }
    static void PlaceLeft(Rig r, Vector3 pos, Quaternion rot)
    {
        SolveArm(r.lUp, r.lLo, r.lHand, r.lClav, pos, rot, r.root.TransformDirection(new Vector3(-0.45f, -0.8f, -0.3f)));
    }

    /// <summary>오른팔을 칼에 맞추고(목표 보정 반복) 근육으로 다시 세운 뒤의 손잡이 오차(m). 근육은 out.</summary>
    static float FitRight(Rig r, float[] baseM, Vector3 bp, Quaternion br, Frame sw, out float[] ik, bool alsoLeft, Vector3 lp, Quaternion lr, out float leftErr)
    {
        Vector3 corrR = Vector3.zero, corrL = Vector3.zero; ik = null; float e = 0f; leftErr = 0f;
        for (int it = 0; it < 5; it++)
        {
            SetMuscles(r, baseM, bp, br);
            PlaceRightOnSword(r, new Frame { pos = sw.pos + corrR, rot = sw.rot });
            if (alsoLeft) PlaceLeft(r, lp + corrL, lr);
            Vector3 ibp; Quaternion ibr;
            ik = Muscles(r, out ibp, out ibr);
            SetMuscles(r, ik, bp, br);
            Vector3 eR = sw.pos - r.sock.position, eL = alsoLeft ? lp - r.lHand.position : Vector3.zero;
            e = eR.magnitude; leftErr = eL.magnitude;
            if (e < 0.004f && leftErr < 0.004f) break;
            corrR += eR; corrL += eL;
        }
        return e;
    }

    // ---------- 칼 길 ----------
    public static float Smooth(float a, float b, float t) { float k = Mathf.Clamp01((t - a) / (b - a)); return k * k * (3f - 2f * k); }

    /// <summary>지금 기준 자세에서 칼집(돌림 w) 속 칼의 자리.</summary>
    static Frame SheathFrame(Rig r, float w)
    {
        Transform sock = r.holder.parent;
        Vector3 lp = Vector3.Lerp(r.holderPos0, r.holderPos0 + TurnLocalPos, w);
        Quaternion lq = Quaternion.Slerp(r.holderRot0, Quaternion.Euler(TurnLocalEuler) * r.holderRot0, w);
        Vector3 pos = sock.TransformPoint(lp);
        Quaternion rot = sock.rotation * lq * r.sheathedSwordLocal;
        return new Frame { pos = pos, rot = rot };
    }

    static Frame PivotFrame(Frame sheath, Vector3 g)
    {
        Vector3 dir = (sheath.pos - g).normalized;
        Vector3 up = Vector3.ProjectOnPlane(sheath.rot * Vector3.up, dir).normalized;
        return new Frame { pos = g, rot = Quaternion.LookRotation(dir, up) };
    }
    static Frame Lerp(Frame a, Frame b, float k, Vector3 arc)
    {
        return new Frame { pos = Vector3.Lerp(a.pos, b.pos, k) + arc * (4f * k * (1f - k)), rot = Quaternion.Slerp(a.rot, b.rot, k) };
    }
    static Frame Pulled(Frame sh, float s) { return new Frame { pos = sh.pos - (sh.rot * Vector3.forward) * s, rot = sh.rot }; }

    static Frame SwordAt(Rig r, bool draw, float t, Frame FC, Frame FD)
    {
        Frame sh = SheathFrame(r, TurnWeight(draw, t));
        Vector3 gB = r.root.TransformPoint(GuardB);
        if (draw)
        {
            if (t <= DrawAttach) return sh;
            if (t <= 0.44f) return Pulled(sh, Smooth(DrawAttach, 0.44f, t) * PullDistance);
            if (t <= 0.57f) return PivotFrame(sh, Vector3.Lerp(Pulled(sh, PullDistance).pos, gB, Smooth(0.44f, 0.57f, t)));
            if (t <= 0.72f) return Lerp(PivotFrame(sh, gB), FC, Smooth(0.57f, 0.72f, t), Vector3.zero);
            if (t <= 0.92f) return Lerp(FC, FD, Smooth(0.72f, 0.92f, t), new Vector3(0f, 0.04f, 0.04f));
            return FD;
        }
        if (t <= 0.28f) return Lerp(FD, FC, Smooth(0f, 0.28f, t), new Vector3(0f, 0.04f, 0.04f));
        if (t <= 0.45f) return Lerp(FC, PivotFrame(sh, gB), Smooth(0.28f, 0.45f, t), Vector3.zero);
        if (t <= 0.58f) return PivotFrame(sh, Vector3.Lerp(gB, Pulled(sh, PullDistance).pos, Smooth(0.45f, 0.58f, t)));
        if (t <= SheatheAttach) return Pulled(sh, (1f - Smooth(0.58f, SheatheAttach, t)) * PullDistance);
        return sh;
    }

    // ---------- 근육 ----------
    static bool IsFinger(int i, bool left)
    {
        string n = HumanTrait.MuscleName[i];
        bool f = n.Contains("Thumb") || n.Contains("Index") || n.Contains("Middle") || n.Contains("Ring") || n.Contains("Little");
        return f && n.StartsWith(left ? "Left" : "Right");
    }
    static bool IsArm(int i, bool left)
    {
        string n = HumanTrait.MuscleName[i];
        if (IsFinger(i, left)) return false;
        return n.StartsWith(left ? "Left" : "Right") && (n.Contains("Shoulder") || n.Contains("Arm") || n.Contains("Forearm") || n.Contains("Hand"));
    }
    static float[] Mix(float[] a, float[] b, float k) { var o = new float[a.Length]; for (int i = 0; i < a.Length; i++) o[i] = Mathf.Lerp(a[i], b[i], k); return o; }
    static float Excess(float[] m, bool left)
    {
        float c = 0f;
        for (int i = 0; i < m.Length; i++) if (IsArm(i, left)) { float v = Mathf.Abs(m[i]); if (v > 1f) c += (v - 1f) * 10f; if (v > 0.8f) c += (v - 0.8f); }
        return c;
    }

    /// <summary>칼집 돌리는 양 찾기: 잡는 순간·다 뺀 순간의 오른손 오차(cm) + 근육 넘침 + 돌린 각도. 결과를 TurnLocal* 에 넣고 보고.</summary>
    public static string OptimizeTurn(string characterName)
    {
        var src = Find(characterName); if (src == null) return "캐릭터 없음";
        var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(IdlePath);
        var r = MakeRig(src); var sb = new StringBuilder();
        try
        {
            Vector3 bp0; Quaternion br0; float[] m0 = Sample(r, idle, 0f, out bp0, out br0);
            SetMuscles(r, m0, bp0, br0);
            Transform sock = r.holder.parent;
            Vector3 axisUp = sock.InverseTransformDirection(r.root.up), axisRight = sock.InverseTransformDirection(r.root.right);
            Vector3 bladeLocal = r.holderRot0 * (r.sheathedSwordLocal * Vector3.forward);
            float best = float.MaxValue; Vector3 bestPos = Vector3.zero, bestEuler = Vector3.zero; string bestInfo = "";
            Vector3[] offs = { Vector3.zero, sock.InverseTransformVector(r.root.TransformVector(new Vector3(0.02f, 0.02f, 0.05f))) };
            for (int oi = 0; oi < offs.Length; oi++)
                for (int yaw = -40; yaw <= 40; yaw += 10)
                    for (int pitch = -40; pitch <= 40; pitch += 10)
                        for (int roll = -90; roll <= 90; roll += 15)
                        {
                            Quaternion q = Quaternion.AngleAxis(yaw, axisUp) * Quaternion.AngleAxis(pitch, axisRight) * Quaternion.AngleAxis(roll, bladeLocal);
                            TurnLocalPos = offs[oi]; TurnLocalEuler = q.eulerAngles;
                            float cost = 0f; string info = "";
                            for (int k = 0; k < 2; k++)
                            {
                                SetMuscles(r, m0, bp0, br0);
                                Frame sw = SheathFrame(r, 1f);
                                if (k == 1) sw = Pulled(sw, PullDistance);
                                float[] ik; float le;
                                float e = FitRight(r, m0, bp0, br0, sw, out ik, false, Vector3.zero, Quaternion.identity, out le);
                                float ex = Excess(ik, false);
                                cost += e * 100f + ex;
                                info += (k == 0 ? "잡기 " : "빼기 ") + (e * 100f).ToString("F1") + "cm 넘침 " + ex.ToString("F2") + " / ";
                            }
                            cost += (Mathf.Abs(yaw) + Mathf.Abs(pitch)) / 40f + Mathf.Abs(roll) / 90f + oi * 0.5f;
                            if (cost < best) { best = cost; bestPos = TurnLocalPos; bestEuler = TurnLocalEuler; bestInfo = "yaw " + yaw + " pitch " + pitch + " roll " + roll + " 밀기 " + oi + " : " + info; }
                        }
            TurnLocalPos = bestPos; TurnLocalEuler = bestEuler;
            sb.Append("최적 " + bestInfo + " cost " + best.ToString("F2") + " → 홀더 로컬 pos " + bestPos.ToString("F4") + " euler " + bestEuler.ToString("F2"));
        }
        finally { Object.DestroyImmediate(r.go); }
        return sb.ToString();
    }

    /// <summary>뽑기·넣기 클립을 만든다.</summary>
    public static string Build(string characterName)
    {
        var src = Find(characterName); if (src == null) return "캐릭터 없음: " + characterName;
        var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(IdlePath);
        var idle1 = AssetDatabase.LoadAssetAtPath<AnimationClip>(Idle1Path);
        var rep = new StringBuilder();
        var r = MakeRig(src);
        try
        {
            Vector3 bp0, bp1; Quaternion br0, br1;
            float[] m0 = Sample(r, idle, 0f, out bp0, out br0);
            float[] m1 = Sample(r, idle1, 0f, out bp1, out br1);
            SetMuscles(r, m1, bp1, br1);
            Frame FD = new Frame { pos = r.sock.position, rot = r.sock.rotation };
            Quaternion lRelRot = Quaternion.Inverse(FD.rot) * r.lHand.rotation;
            Vector3 lRelPos = Quaternion.Inverse(FD.rot) * (r.lHand.position - FD.pos);
            Vector3 dC = r.root.TransformDirection(BladeC.normalized);
            Vector3 edgeC = Vector3.ProjectOnPlane(r.root.forward, dC).normalized;
            Frame FC = new Frame { pos = r.root.TransformPoint(GuardC), rot = Quaternion.LookRotation(dC, -edgeC) };
            rep.AppendLine("칼집 돌림 홀더 로컬 pos " + TurnLocalPos.ToString("F4") + " euler " + TurnLocalEuler.ToString("F2"));
            rep.Append(MakeClip(r, "Haesin_Draw", true, m0, m1, bp0, br0, bp1, br1, FC, FD, lRelPos, lRelRot));
            rep.Append(MakeClip(r, "Haesin_Sheathe", false, m0, m1, bp0, br0, bp1, br1, FC, FD, lRelPos, lRelRot));
        }
        finally { Object.DestroyImmediate(r.go); }
        return rep.ToString();
    }

    static string MakeClip(Rig r, string name, bool draw, float[] m0, float[] m1, Vector3 bp0, Quaternion br0, Vector3 bp1, Quaternion br1,
                           Frame FC, Frame FD, Vector3 lRelPos, Quaternion lRelRot)
    {
        float length = draw ? DrawLength : SheatheLength;
        int n = Mathf.RoundToInt(length * Fps) + 1;
        int mc = HumanTrait.MuscleCount;
        var curves = new AnimationCurve[mc];
        for (int i = 0; i < mc; i++) curves[i] = new AnimationCurve();
        var rootT = new AnimationCurve[3]; var rootQ = new AnimationCurve[4];
        for (int i = 0; i < 3; i++) rootT[i] = new AnimationCurve();
        for (int i = 0; i < 4; i++) rootQ[i] = new AnimationCurve();
        float maxErr = 0f, maxErrT = 0f, maxLErr = 0f, maxEx = 0f;
        var sb = new StringBuilder();

        for (int f = 0; f < n; f++)
        {
            float t = f / Fps;
            float kb = draw ? Smooth(0.55f, 0.92f, t) : 1f - Smooth(0.30f, 0.75f, t);
            float[] baseM = Mix(m0, m1, kb);
            Vector3 bp = Vector3.Lerp(bp0, bp1, kb); Quaternion br = Quaternion.Slerp(br0, br1, kb);
            float wR = draw ? Smooth(0.04f, 0.20f, t) * (1f - Smooth(0.92f, 1.0f, t)) : 1f - Smooth(0.74f, 0.95f, t);
            float wL = draw ? Smooth(0.04f, 0.20f, t) * (1f - Smooth(0.92f, 1.0f, t)) : 1f - Smooth(0.74f, 0.95f, t);
            SetMuscles(r, baseM, bp, br);
            Frame sw = SwordAt(r, draw, t, FC, FD);
            Frame sh = SheathFrame(r, TurnWeight(draw, t));
            Vector3 lScabP = sh.pos + sh.rot * (lRelPos + Vector3.forward * LeftOnScabbard); Quaternion lScabR = sh.rot * lRelRot;
            float kHandle = draw ? Smooth(0.62f, 0.86f, t) : 1f - Smooth(0.02f, 0.22f, t);
            Vector3 lp = Vector3.Lerp(lScabP, sw.pos + sw.rot * lRelPos, kHandle) + Vector3.up * (0.05f * 4f * kHandle * (1f - kHandle));
            Quaternion lr = Quaternion.Slerp(lScabR, sw.rot * lRelRot, kHandle);

            float[] ik; float le;
            float e = FitRight(r, baseM, bp, br, sw, out ik, true, lp, lr, out le);

            var outM = new float[mc];
            for (int i = 0; i < mc; i++)
            {
                float v = baseM[i];
                if (IsArm(i, false)) v = Mathf.Lerp(baseM[i], ik[i], wR);
                else if (IsArm(i, true)) v = Mathf.Lerp(baseM[i], ik[i], wL);
                if (IsFinger(i, false)) { float kf = draw ? Smooth(0.14f, 0.24f, t) : 1f - Smooth(0.74f, 0.86f, t); v = Mathf.Lerp(m0[i], m1[i], kf); }
                if (IsFinger(i, true)) { float kf = draw ? Smooth(0.10f, 0.22f, t) : 1f - Smooth(0.74f, 0.90f, t); v = Mathf.Lerp(m0[i], m1[i], kf); }
                outM[i] = v;
                curves[i].AddKey(new Keyframe(t, v));
            }
            rootT[0].AddKey(t, bp.x); rootT[1].AddKey(t, bp.y); rootT[2].AddKey(t, bp.z);
            rootQ[0].AddKey(t, br.x); rootQ[1].AddKey(t, br.y); rootQ[2].AddKey(t, br.z); rootQ[3].AddKey(t, br.w);

            SetMuscles(r, outM, bp, br);
            float ex = Excess(outM, false) + Excess(outM, true); if (ex > maxEx) maxEx = ex;
            if (wR > 0.99f)
            {
                float ee = (r.sock.position - sw.pos).magnitude;
                if (ee > maxErr) { maxErr = ee; maxErrT = t; }
                if (f % 3 == 0) sb.AppendLine("  t " + t.ToString("F2") + " 손잡이 오차 " + (ee * 100f).ToString("F1") + " cm · " + Quaternion.Angle(r.sock.rotation, sw.rot).ToString("F0") + "° · 넘침 " + ex.ToString("F2"));
            }
            if (wL > 0.99f) { float l2 = (r.lHand.position - lp).magnitude; if (l2 > maxLErr) maxLErr = l2; }
        }

        var clip = new AnimationClip { name = name, frameRate = Fps };
        var bindings = MuscleBindings();
        for (int i = 0; i < mc; i++)
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), bindings[i]), curves[i]);
        string[] tn = { "RootT.x", "RootT.y", "RootT.z" }; string[] qn = { "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w" };
        for (int i = 0; i < 3; i++) AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), tn[i]), rootT[i]);
        for (int i = 0; i < 4; i++) AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), qn[i]), rootQ[i]);
        var st = AnimationUtility.GetAnimationClipSettings(clip);
        st.loopTime = false; st.keepOriginalOrientation = true; st.keepOriginalPositionY = true; st.keepOriginalPositionXZ = true;
        st.loopBlendOrientation = true; st.loopBlendPositionY = true; st.loopBlendPositionXZ = true;
        AnimationUtility.SetAnimationClipSettings(clip, st);
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/02.Resources/00.Component/06.Character/Haesin/Animations/Combat", "Custom");
        string pth = Folder + "/" + name + ".anim";
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(pth);
        if (existing != null) { EditorUtility.CopySerialized(clip, existing); Object.DestroyImmediate(clip); clip = existing; EditorUtility.SetDirty(clip); }
        else AssetDatabase.CreateAsset(clip, pth);
        AssetDatabase.SaveAssets();
        return name + ": " + n + " 프레임, 오른손 손잡이 최대 오차 " + (maxErr * 100f).ToString("F1") + " cm (t " + maxErrT.ToString("F2") + "), 왼손 최대 " + (maxLErr * 100f).ToString("F1") + " cm, 근육 넘침 최대 " + maxEx.ToString("F2") + "\n" + sb;
    }

    /// <summary>확인 그림: 클립을 복제본에 틀고 칼(attach 전 = 돌린 칼집 속, 뒤 = 손)을 두고 앞·오른쪽 옆·왼쪽 비스듬히에서 찍음. 반환 = 파일·팔 근육 넘침.</summary>
    public static string RenderClip(string characterName, bool draw, string outFolder, int stepFrames)
    {
        var src = Find(characterName); if (src == null) return "캐릭터 없음";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/" + (draw ? "Haesin_Draw" : "Haesin_Sheathe") + ".anim");
        var r = MakeRig(src); var sb = new StringBuilder();
        var camGo = new GameObject("__SwordDrawCam"); camGo.hideFlags = HideFlags.DontSave;
        var cam = camGo.AddComponent<Camera>(); cam.fieldOfView = 30f; cam.nearClipPlane = 0.05f; cam.enabled = false;
        var rt = new RenderTexture(640, 800, 24); cam.targetTexture = rt;
        var lightGo = new GameObject("__SwordDrawLight"); lightGo.hideFlags = HideFlags.DontSave;
        var lt = lightGo.AddComponent<Light>(); lt.type = LightType.Directional; lightGo.transform.rotation = Quaternion.Euler(35f, 150f, 0f);
        Transform sword = null; foreach (var t in r.go.GetComponentsInChildren<Transform>(true)) if (t.name == "Hwando_Sword") sword = t;
        System.IO.Directory.CreateDirectory(outFolder);
        try
        {
            int n = Mathf.RoundToInt(clip.length * Fps);
            for (int f = 0; f <= n; f += stepFrames)
            {
                float t = f / Fps;
                clip.SampleAnimation(r.go, t);
                r.root.SetPositionAndRotation(new Vector3(50f, 0f, 0f), Quaternion.identity);   // 씬의 원래 캐릭터와 겹치지 않게
                float w = TurnWeight(draw, t);
                r.holder.localPosition = Vector3.Lerp(r.holderPos0, r.holderPos0 + TurnLocalPos, w);
                r.holder.localRotation = Quaternion.Slerp(r.holderRot0, Quaternion.Euler(TurnLocalEuler) * r.holderRot0, w);
                bool inHand = draw ? t >= DrawAttach : t < SheatheAttach;
                if (inHand) { sword.SetParent(r.sock, false); sword.localPosition = Vector3.zero; sword.localRotation = Quaternion.identity; }
                else { sword.SetParent(r.holder, false); sword.localPosition = Vector3.zero; sword.localRotation = r.sheathedSwordLocal; }
                Vector3 bpx; Quaternion brx; float[] m = Muscles(r, out bpx, out brx);
                sb.Append("t " + t.ToString("F2") + " 넘침 R " + Excess(m, false).ToString("F1") + " L " + Excess(m, true).ToString("F1") + " | ");
                // 편집 모드에서는 SkinnedMeshRenderer 스키닝이 이 호출 안에 갱신되지 않아(관찰) 구운 메시로 찍음
                var baked = new List<GameObject>(); var hidden = new List<SkinnedMeshRenderer>();
                foreach (var smr in r.go.GetComponentsInChildren<SkinnedMeshRenderer>(false))
                {
                    if (!smr.enabled) continue;
                    var bm = new Mesh(); smr.BakeMesh(bm, true);
                    var bg = new GameObject("__baked"); bg.hideFlags = HideFlags.DontSave;
                    bg.transform.SetPositionAndRotation(smr.transform.position, smr.transform.rotation);
                    bg.AddComponent<MeshFilter>().sharedMesh = bm;
                    bg.AddComponent<MeshRenderer>().sharedMaterials = smr.sharedMaterials;
                    smr.enabled = false; hidden.Add(smr); baked.Add(bg);
                }
                Vector3 c = r.root.TransformPoint(new Vector3(0f, 1.05f, 0.05f));
                Vector3[] views = { new Vector3(0f, 0.25f, 2.6f), new Vector3(2.6f, 0.25f, 0.2f), new Vector3(-1.8f, 0.5f, 1.9f) };
                string[] vn = { "front", "right", "leftq" };
                for (int v = 0; v < views.Length; v++)
                {
                    cam.transform.position = c + views[v]; cam.transform.LookAt(c);
                    cam.Render(); cam.Render();
                    RenderTexture.active = rt;
                    var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
                    RenderTexture.active = null;
                    System.IO.File.WriteAllBytes(outFolder + "/" + (draw ? "draw" : "sheathe") + "_" + f.ToString("00") + "_" + vn[v] + ".png", tex.EncodeToPNG());
                    Object.DestroyImmediate(tex);
                }
                foreach (var bg in baked) { Object.DestroyImmediate(bg.GetComponent<MeshFilter>().sharedMesh); Object.DestroyImmediate(bg); }
                foreach (var smr in hidden) smr.enabled = true;
            }
        }
        finally { Object.DestroyImmediate(r.go); Object.DestroyImmediate(camGo); Object.DestroyImmediate(lightGo); rt.Release(); }
        return sb.ToString();
    }

    static string[] MuscleBindings()
    {
        var o = new string[HumanTrait.MuscleCount];
        string[] fingers = { "Thumb", "Index", "Middle", "Ring", "Little" };
        for (int i = 0; i < o.Length; i++)
        {
            string n = HumanTrait.MuscleName[i];
            o[i] = n;
            foreach (var side in new[] { "Left", "Right" })
                foreach (var fg in fingers)
                {
                    string pre = side + " " + fg + " ";
                    if (n.StartsWith(pre)) o[i] = side + "Hand." + fg + "." + n.Substring(pre.Length).Replace(" ", ".").Replace(".Stretched", " Stretched");
                }
        }
        return o;
    }

    public static string CheckBindings()
    {
        var idle1 = AssetDatabase.LoadAssetAtPath<AnimationClip>(Idle1Path);
        var have = new HashSet<string>();
        foreach (var b in AnimationUtility.GetCurveBindings(idle1)) have.Add(b.propertyName);
        int miss = 0; foreach (var m in MuscleBindings()) if (!have.Contains(m)) miss++;
        return "없는 것 " + miss + " / " + HumanTrait.MuscleCount;
    }
}
