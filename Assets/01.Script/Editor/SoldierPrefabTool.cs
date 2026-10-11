using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 육상 병사 프리팹 만들기(2026-10-06 — 관군·일본 병사·일본 장수, 칼싸움). MCP 에서 정적 메서드로 부름.
///   MakeGwanGunMaterials(): 조선 수군 재질을 복사해 빨간 텍스처(T_GwanGun_LOD0~2_BaseColor — 파랑을 진홍으로 바꾼 것)로.
///   BuildAll(해신 플레이어): GwanGun_Soldier / JPSoldier_Sword / Shogun_Sword 프리팹(`06.Character/Soldiers/`).
/// 프리팹 구조: 뿌리(NavMeshAgent·CapsuleCollider·SoldierAI, 층 Ignore Raycast) → 자식 모델(FBX 인스턴스 + Animator(해신 컨트롤러 Haesin_TestLocomotion)
///   + CombatStats + SwordCombatant(해신 플레이어 값 복사, team 1) + WeaponHolder + HwandoGripIK + SwordAudio(해신 플레이어 값 복사)).
///   환도(`Hwando_Sheathed` 프리팹)를 Hips 아래 `Socket_Hwando_Hip_L` 에, 오른손 소켓 `Socket_Hwando_Hand_R` 을 RightHand 아래에 —
///   두 몸을 같은 휴머노이드 자세(머슬 0)로 두고 해신의 소켓이 손·골반에 대해 놓인 세계 방향을 그대로 옮겨 계산(뼈 축이 달라도 맞음).
///   골반 소켓의 거리는 두 몸 골반 폭(양 허벅지 뿌리 사이) 비율로 늘림. 일본 병사·장수도 같은 환도 모델을 씀(일본도 모델 없음).
/// 왜구(2026-10-07): MakeWokouBossTexture(lod) — 일본 장수 텍스처의 붉은 칠(색상 ±14°, 채도 0.3 이상)을 검은 옻칠로(사용자 지시 "일본장수의 갑주를 검은색으로"),
///   MakeWokouBossMaterials() — 재질 M_WokouBoss_LOD0~2, BuildWokou(해신) — Wokou_Boss(일본 장수 모델 + 검은 재질) / Wokou_Sword(일본 병사 모델 그대로 — 2026-10-09 BuildQuestSoldiers 가 왜구 병사 모델로 바꿈).
/// 퀘스트 병사(2026-10-09): BuildQuestSoldiers(해신) — Wokou_Sword(WokouSoldier 모델) / PalaceGuard_Sword / Kurose_Armor_Sword.
/// </summary>
public static class SoldierPrefabTool
{
    public const string CharDir = "Assets/02.Resources/00.Component/06.Character";
    public const string OutDir = CharDir + "/Soldiers";
    public const string CtrlPath = CharDir + "/Haesin/Animations/Haesin_TestLocomotion.controller";

    public static string MakeGwanGunMaterials()
    {
        var sb = new System.Text.StringBuilder();
        string dir = CharDir + "/GwanGun";
        if (!AssetDatabase.IsValidFolder(dir + "/Materials")) AssetDatabase.CreateFolder(dir, "Materials");
        int[] maxSize = { 4096, 2048, 1024 };
        for (int i = 0; i < 3; i++)
        {
            string texPath = dir + "/T_GwanGun_LOD" + i + "_BaseColor.jpg";
            var ti = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (ti != null)
            {
                ti.sRGBTexture = true; ti.maxTextureSize = maxSize[i]; ti.streamingMipmaps = true; ti.mipmapEnabled = true;
                ti.SaveAndReimport();
            }
            var src = AssetDatabase.LoadAssetAtPath<Material>(CharDir + "/JoseonNavy/Materials/M_JoseonNavy_LOD" + i + ".mat");
            string matPath = dir + "/Materials/M_GwanGun_LOD" + i + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (m == null) { m = new Material(src); AssetDatabase.CreateAsset(m, matPath); }
            else m.CopyPropertiesFromMaterial(src);
            m.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            EditorUtility.SetDirty(m);
            sb.AppendLine(matPath + " tex=" + (m.GetTexture("_BaseColorMap") != null));
        }
        AssetDatabase.SaveAssets();
        return sb.ToString();
    }

    public static string BuildAll(GameObject haesin)
    {
        var sb = new System.Text.StringBuilder();
        if (!AssetDatabase.IsValidFolder(OutDir)) AssetDatabase.CreateFolder(CharDir, "Soldiers");
        var red = new Material[3];
        for (int i = 0; i < 3; i++) red[i] = AssetDatabase.LoadAssetAtPath<Material>(CharDir + "/GwanGun/Materials/M_GwanGun_LOD" + i + ".mat");
        sb.AppendLine(Build("GwanGun_Soldier", CharDir + "/JoseonNavy/JoseonNavy.fbx", red, "관군", 80f, 100f, haesin, false));
        sb.AppendLine(Build("JPSoldier_Sword", CharDir + "/JPSoldier/JPSoldier.fbx", null, "일본 병사", 70f, 100f, haesin, false));
        sb.AppendLine(Build("Shogun_Sword", CharDir + "/Shogun/Shogun.fbx", null, "일본 장수", 180f, 160f, haesin, true));
        AssetDatabase.SaveAssets();
        return sb.ToString();
    }

    public const string WokouDir = CharDir + "/WokouBoss";

    /// <summary>
    /// 일본 장수 LOD 텍스처 하나를 검은 갑주로 바꿔 `WokouBoss/T_WokouBoss_LOD{lod}_BaseColor.jpg` 로 저장(원본 텍스처는 그대로).
    /// 붉은 칠 = 색상이 빨강에서 14° 안(28° 까지 서서히), 채도 0.30~0.45 이상 → 검정(sRGB 0.075·0.07·0.075 × (밝기 / 붉은 칠 밝기 가운데값)^0.6 — 결은 남김).
    /// 금색 장식·피부(황갈색)·갈색 가죽은 그대로. 4096² 는 한 번에 몇 초 걸림.
    /// </summary>
    public static string MakeWokouBossTexture(int lod)
    {
        if (!AssetDatabase.IsValidFolder(WokouDir)) AssetDatabase.CreateFolder(CharDir, "WokouBoss");
        string root = System.IO.Directory.GetParent(Application.dataPath).FullName;
        string src = CharDir + "/Shogun/T_Shogun_LOD" + lod + "_BaseColor.jpg";
        var tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
        tex.LoadImage(System.IO.File.ReadAllBytes(System.IO.Path.Combine(root, src)));
        var px = tex.GetPixels32();
        int n = px.Length;
        var w = new float[n]; var lum = new float[n];
        var hist = new int[256]; int redCount = 0;
        for (int i = 0; i < n; i++)
        {
            float r = px[i].r / 255f, g = px[i].g / 255f, b = px[i].b / 255f;
            float mx = Mathf.Max(r, Mathf.Max(g, b)), mn = Mathf.Min(r, Mathf.Min(g, b));
            float s = mx > 1e-4f ? (mx - mn) / mx : 0f;
            float d = Mathf.Max(mx - mn, 1e-6f), h;
            if (mx == r) { h = ((g - b) / d) % 6f; if (h < 0f) h += 6f; } else if (mx == g) h = (b - r) / d + 2f; else h = (r - g) / d + 4f;
            h *= 60f;
            float hd = Mathf.Min(Mathf.Abs(h), Mathf.Abs(h - 360f));
            float wt = (1f - Smooth(14f, 28f, hd)) * Smooth(0.30f, 0.45f, s);
            w[i] = wt;
            float l = 0.2126f * r + 0.7152f * g + 0.0722f * b; lum[i] = l;
            if (wt > 0.5f) { hist[Mathf.Clamp((int)(l * 255f), 0, 255)]++; redCount++; }
        }
        // 붉은 칠 밝기의 가운데값
        float med = 0.2f; int acc = 0;
        for (int k = 0; k < 256; k++) { acc += hist[k]; if (acc * 2 >= redCount) { med = Mathf.Max(0.02f, k / 255f); break; } }
        for (int i = 0; i < n; i++)
        {
            if (w[i] <= 0f) continue;
            float k = Mathf.Pow(lum[i] / med, 0.6f);
            float nr = 0.075f * k, ng = 0.070f * k, nb = 0.075f * k;
            float t = w[i];
            px[i] = new Color32((byte)Mathf.Clamp(Mathf.RoundToInt(255f * Mathf.Lerp(px[i].r / 255f, nr, t)), 0, 255),
                                (byte)Mathf.Clamp(Mathf.RoundToInt(255f * Mathf.Lerp(px[i].g / 255f, ng, t)), 0, 255),
                                (byte)Mathf.Clamp(Mathf.RoundToInt(255f * Mathf.Lerp(px[i].b / 255f, nb, t)), 0, 255), 255);
        }
        tex.SetPixels32(px); tex.Apply();
        string dst = WokouDir + "/T_WokouBoss_LOD" + lod + "_BaseColor.jpg";
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(root, dst), tex.EncodeToJPG(92));
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(dst);
        var ti = AssetImporter.GetAtPath(dst) as TextureImporter;
        if (ti != null)
        {
            int[] maxSize = { 4096, 2048, 1024 };
            ti.sRGBTexture = true; ti.maxTextureSize = maxSize[Mathf.Clamp(lod, 0, 2)]; ti.streamingMipmaps = true; ti.mipmapEnabled = true;
            ti.SaveAndReimport();
        }
        return dst + " 붉은 칠 " + (100f * redCount / n).ToString("F1") + " %, 밝기 가운데값 " + med.ToString("F3");
    }

    static float Smooth(float a, float b, float v) { float t = Mathf.Clamp01((v - a) / (b - a)); return t * t * (3f - 2f * t); }

    public static string MakeWokouBossMaterials()
    {
        var sb = new System.Text.StringBuilder();
        if (!AssetDatabase.IsValidFolder(WokouDir + "/Materials")) AssetDatabase.CreateFolder(WokouDir, "Materials");
        for (int i = 0; i < 3; i++)
        {
            var src = AssetDatabase.LoadAssetAtPath<Material>(CharDir + "/Shogun/Materials/M_Shogun_LOD" + i + ".mat");
            string matPath = WokouDir + "/Materials/M_WokouBoss_LOD" + i + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (m == null) { m = new Material(src); AssetDatabase.CreateAsset(m, matPath); }
            else m.CopyPropertiesFromMaterial(src);
            m.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(WokouDir + "/T_WokouBoss_LOD" + i + "_BaseColor.jpg"));
            m.SetFloat("_Smoothness", 0.45f); // 옻칠이라 장수(0.35)보다 조금 더 윤기
            EditorUtility.SetDirty(m);
            sb.AppendLine(matPath + " tex=" + (m.GetTexture("_BaseColorMap") != null));
        }
        AssetDatabase.SaveAssets();
        return sb.ToString();
    }

    /// <summary>왜구 프리팹 둘: Wokou_Boss(일본 장수 모델 + 검은 갑주, 체력 220·체간 180) / Wokou_Sword(일본 병사 모델, 체력 70·체간 100).</summary>
    public static string BuildWokou(GameObject haesin)
    {
        var sb = new System.Text.StringBuilder();
        if (!AssetDatabase.IsValidFolder(OutDir)) AssetDatabase.CreateFolder(CharDir, "Soldiers");
        var black = new Material[3];
        for (int i = 0; i < 3; i++) black[i] = AssetDatabase.LoadAssetAtPath<Material>(WokouDir + "/Materials/M_WokouBoss_LOD" + i + ".mat");
        sb.AppendLine(Build("Wokou_Boss", CharDir + "/Shogun/Shogun.fbx", black, "왜구 두목", 220f, 180f, haesin, true));
        sb.AppendLine(Build("Wokou_Sword", CharDir + "/JPSoldier/JPSoldier.fbx", null, "왜구", 70f, 100f, haesin, false));
        AssetDatabase.SaveAssets();
        return sb.ToString();
    }

    /// <summary>
    /// 퀘스트 NPC 모델로 싸우는 병사 프리팹(2026-10-09, 사용자 "인물 모델링이 추가되었으므로 퀘스트 모델링도 다 바꾸세요"):
    /// Wokou_Sword 를 왜구 병사 모델(WokouSoldier)로 다시 만듦(같은 경로 — 씬 배치 그대로), PalaceGuard_Sword(궁궐 호위, 4-4),
    /// Kurose_Armor_Sword(영주 구로세 갑주 — 2-8 암살 분기). 새 모델은 재질을 가져오기에서 붙였으므로 재질은 바꾸지 않음.
    /// </summary>
    public static string BuildQuestSoldiers(GameObject haesin)
    {
        var sb = new System.Text.StringBuilder();
        if (!AssetDatabase.IsValidFolder(OutDir)) AssetDatabase.CreateFolder(CharDir, "Soldiers");
        sb.AppendLine(Build("Wokou_Sword", CharDir + "/WokouSoldier/WokouSoldier.fbx", null, "왜구", 70f, 100f, haesin, false));
        sb.AppendLine(Build("PalaceGuard_Sword", CharDir + "/PalaceGuard/PalaceGuard.fbx", null, "궁궐 호위", 90f, 110f, haesin, false));
        sb.AppendLine(Build("Kurose_Armor_Sword", CharDir + "/KuroseMasatoki_Armor/KuroseMasatoki_Armor.fbx", null, "영주 구로세", 200f, 170f, haesin, false));
        AssetDatabase.SaveAssets();
        return sb.ToString();
    }

    /// <param name="modelHasSword">모델 메시에 이미 칼(칼집)이 붙어 있음(일본 장수) — 환도 칼집은 끄고, 검은 뽑았을 때만 보이게.</param>
    static string Build(string name, string fbxPath, Material[] lodMats, string displayName, float hp, float posture, GameObject haesin, bool modelHasSword)
    {
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (fbx == null) return "missing " + fbxPath;
        var root = new GameObject(name);
        root.layer = 2; // Ignore Raycast — 병사 높이 맞춤 레이·발 IK 레이에 안 걸리게
        var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        model.name = "Model";
        model.transform.SetParent(root.transform, false);

        // LOD 재질·LODGroup(다른 NPC 프리팹과 같은 값)
        var lg = model.GetComponent<LODGroup>();
        var rends = new List<Renderer>[3];
        for (int i = 0; i < 3; i++) rends[i] = new List<Renderer>();
        foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            for (int i = 0; i < 3; i++)
                if (r.name.EndsWith("_LOD" + i))
                {
                    rends[i].Add(r);
                    if (lodMats != null && lodMats[i] != null) r.sharedMaterial = lodMats[i];
                }
        }
        if (lg == null) lg = model.AddComponent<LODGroup>();
        float[] h = { 0.1068f, 0.0401f, 0.0107f };
        var lods = new LOD[3];
        for (int i = 0; i < 3; i++) lods[i] = new LOD(h[i], rends[i].ToArray());
        lg.SetLODs(lods); lg.size = 1.85f; lg.localReferencePoint = new Vector3(0f, 0.85f, 0f);
        lg.fadeMode = LODFadeMode.CrossFade; lg.animateCrossFading = true;

        // 애니메이터
        var an = model.GetComponent<Animator>();
        an.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CtrlPath);
        an.applyRootMotion = true;
        an.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

        // 소켓
        Vector3 hp_, hipP; Quaternion hr_, hipR;
        string err = ComputeSockets(haesin, model, out hp_, out hr_, out hipP, out hipR);
        if (err != null) { Object.DestroyImmediate(root); return name + ": " + err; }
        var rh = an.GetBoneTransform(HumanBodyBones.RightHand);
        var hips = an.GetBoneTransform(HumanBodyBones.Hips);
        var hand = new GameObject("Socket_Hwando_Hand_R").transform;
        hand.SetParent(rh, false); hand.localPosition = hp_; hand.localRotation = hr_;
        var hip = new GameObject("Socket_Hwando_Hip_L").transform;
        hip.SetParent(hips, false); hip.localPosition = hipP; hip.localRotation = hipR;

        Transform hsrcT = null;
        foreach (var t in haesin.GetComponentsInChildren<Transform>(true)) if (t.name == "Hwando_Sheathed") { hsrcT = t; break; }
        var hsrc = hsrcT != null ? PrefabUtility.GetCorrespondingObjectFromSource(hsrcT.gameObject) : null;
        if (hsrc == null) { Object.DestroyImmediate(root); return name + ": Hwando_Sheathed 원본 프리팹 없음"; }
        var sheathed = (GameObject)PrefabUtility.InstantiatePrefab(hsrc);
        sheathed.name = "Hwando_Sheathed";
        sheathed.transform.SetParent(hip, false);
        sheathed.transform.localPosition = Vector3.zero; sheathed.transform.localRotation = Quaternion.identity; sheathed.transform.localScale = Vector3.one;

        // 전투 컴포넌트
        var stats = model.AddComponent<CombatStats>();
        stats.maxHealth = hp; stats.health = hp; stats.maxPosture = posture;
        var sc = model.AddComponent<SwordCombatant>();
        var srcSc = haesin.GetComponent<SwordCombatant>();
        if (srcSc != null) EditorUtility.CopySerialized(srcSc, sc);
        sc.team = 1; sc.target = null; sc.animator = an; sc.stats = stats; sc.motionRoot = root.transform; sc.calm = true;
        var wh = model.AddComponent<WeaponHolder>();
        var srcWh = haesin.GetComponent<WeaponHolder>();
        if (srcWh != null) EditorUtility.CopySerialized(srcWh, wh);
        wh.animator = an; wh.handSocket = hand; wh.hipHolder = sheathed.transform; wh.drawnAtStart = false;
        foreach (var t in sheathed.GetComponentsInChildren<Transform>(true)) if (t.name == "Hwando_Sword") wh.sword = t;
        if (modelHasSword)
        {
            foreach (var t in sheathed.GetComponentsInChildren<Transform>(true))
                if (t.name == "Hwando_Scabbard") foreach (var r in t.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            wh.hideWhenSheathed = wh.sword != null ? wh.sword.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
        }
        else wh.hideWhenSheathed = new Renderer[0];
        var ik = model.AddComponent<HwandoGripIK>();
        var srcIk = haesin.GetComponent<HwandoGripIK>();
        if (srcIk != null) EditorUtility.CopySerialized(srcIk, ik);
        ik.handSocket = hand; ik.weapon = wh;
        var sa = model.AddComponent<SwordAudio>();
        var srcSa = haesin.GetComponent<SwordAudio>();
        if (srcSa != null) EditorUtility.CopySerialized(srcSa, sa);

        // 뿌리
        var agent = root.AddComponent<NavMeshAgent>();
        agent.radius = 0.35f; agent.height = 1.8f; agent.baseOffset = 0f; agent.speed = 3.3f; agent.angularSpeed = 360f; agent.acceleration = 12f;
        agent.stoppingDistance = 0.3f; agent.autoBraking = true; agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
        var cap = root.AddComponent<CapsuleCollider>();
        cap.center = new Vector3(0f, 0.9f, 0f); cap.radius = 0.3f; cap.height = 1.8f;
        var ai = root.AddComponent<SoldierAI>();
        ai.displayName = displayName; ai.combatant = sc; ai.weapon = wh;

        string path = OutDir + "/" + name + ".prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return name + " → " + path + " hand " + hp_.ToString("F4") + " / " + hr_.eulerAngles.ToString("F1") + " hip " + hipP.ToString("F4") + " / " + hipR.eulerAngles.ToString("F1");
    }

    /// <summary>두 몸을 원점·같은 휴머노이드 자세(머슬 0)로 두고 해신 소켓의 손·골반 기준 세계 관계를 NPC 뼈의 로컬로 옮김.</summary>
    public static string ComputeSockets(GameObject haesin, GameObject npcModel, out Vector3 handPos, out Quaternion handRot, out Vector3 hipPos, out Quaternion hipRot)
    {
        handPos = hipPos = Vector3.zero; handRot = hipRot = Quaternion.identity;
        var hc = Object.Instantiate(haesin);
        hc.hideFlags = HideFlags.DontSave;
        var nc = Object.Instantiate(npcModel);
        nc.hideFlags = HideFlags.DontSave;
        try
        {
            foreach (var mb in hc.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
            hc.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            nc.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var ha = hc.GetComponent<Animator>(); var na = nc.GetComponent<Animator>();
            if (ha == null || na == null || ha.avatar == null || na.avatar == null) return "Animator/아바타 없음";
            ZeroPose(ha); ZeroPose(na);
            Transform hs = null, hhip = null;
            foreach (var t in hc.GetComponentsInChildren<Transform>(true)) { if (t.name == "Socket_Hwando_Hand_R") hs = t; if (t.name == "Socket_Hwando_Hip_L") hhip = t; }
            if (hs == null || hhip == null) return "해신 소켓 없음";
            var hHand = ha.GetBoneTransform(HumanBodyBones.RightHand); var nHand = na.GetBoneTransform(HumanBodyBones.RightHand);
            var hHips = ha.GetBoneTransform(HumanBodyBones.Hips); var nHips = na.GetBoneTransform(HumanBodyBones.Hips);
            // 손
            Vector3 wp = nHand.position + (hs.position - hHand.position);
            handPos = Quaternion.Inverse(nHand.rotation) * (wp - nHand.position);
            handRot = Quaternion.Inverse(nHand.rotation) * hs.rotation;
            // 골반: 골반 폭 비율로 늘림
            float hw = Vector3.Distance(ha.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position, ha.GetBoneTransform(HumanBodyBones.RightUpperLeg).position);
            float nw = Vector3.Distance(na.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position, na.GetBoneTransform(HumanBodyBones.RightUpperLeg).position);
            float k = hw > 1e-4f ? Mathf.Clamp(nw / hw, 0.8f, 1.6f) : 1f;
            Vector3 off = hhip.position - hHips.position;
            off = new Vector3(off.x * k, off.y, off.z * k);
            Vector3 hipW = nHips.position + off;
            hipPos = Quaternion.Inverse(nHips.rotation) * (hipW - nHips.position);
            hipRot = Quaternion.Inverse(nHips.rotation) * hhip.rotation;
            return null;
        }
        finally { Object.DestroyImmediate(hc); Object.DestroyImmediate(nc); }
    }

    static void ZeroPose(Animator a)
    {
        var h = new HumanPoseHandler(a.avatar, a.transform);
        var pose = new HumanPose();
        h.GetHumanPose(ref pose);
        for (int i = 0; i < pose.muscles.Length; i++) pose.muscles[i] = 0f;
        pose.bodyRotation = Quaternion.identity;
        pose.bodyPosition = new Vector3(0f, 1f, 0f);
        h.SetHumanPose(ref pose);
        h.Dispose();
    }
}

