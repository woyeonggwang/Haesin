using System.Collections.Generic;
using System.IO;
using MagicaCloth2;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 해신 한복 치마(Hanbok_Skirt)를 Magica Cloth 2 MeshCloth 로 바꾸는 도구(2026-10-05, 사용자 선택 "3번 — 메시 자체를 천으로").
/// 예전: BoneCloth(치마 뼈 8가닥) — 가닥 사이(밑단에서 약 30 cm)로 다리가 빠져나오고, 가닥 위 1/3 을 묶어 엉덩이에서 옷이 꺾여 보였음.
/// 지금: 치마 메시 정점 전체를 천으로(줄인 대리 메시 = reduction), 허리띠만 고정, 엉덩이 충돌체 추가.
///  - MakePaintMap: 캐릭터 루트 기준 높이 fixedAboveY 위 정점 = 고정(빨강), 아래 = 움직임(초록)을 UV 로 그린 그림(Voronoi — 삼각형 안 각 점은 가장 가까운 꼭짓점 색).
///  - Apply: 옛 BoneCloth(Cloth_Skirt) 지우고 MeshCloth(Cloth_Skirt) 만들기 + 엉덩이 캡슐(Hips 자식 SkirtHipCollider) + SkirtLegFollow 연결.
/// 메뉴: HaeSin/치마 MeshCloth 적용 (선택한 캐릭터).
/// </summary>
public static class SkirtMeshClothTool
{
    public const string PaintPath = "Assets/02.Resources/00.Component/06.Character/Haesin/Cloth/Hanbok_Skirt_Paint.png";

    [MenuItem("HaeSin/치마 MeshCloth 적용 (선택한 캐릭터)")]
    static void MenuApply()
    {
        var go = Selection.activeGameObject;
        if (go == null) { Debug.LogWarning("[SkirtMeshClothTool] 캐릭터(Haesin_Player 등)를 선택하세요."); return; }
        Debug.Log(ApplyAll(go));
    }

    public static string ApplyAll(GameObject character)
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(PaintPath);
        string r = "";
        if (tex == null) { r += MakePaintMap(character, 0.995f, PaintPath, 2048) + "\n"; tex = AssetDatabase.LoadAssetAtPath<Texture2D>(PaintPath); }
        r += Apply(character, tex);
        return r;
    }

    static SkinnedMeshRenderer FindSkirt(GameObject character)
    {
        foreach (var s in character.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if (s.name == "Hanbok_Skirt") return s;
        return null;
    }

    /// <summary>고정/움직임 그림을 만든다. 반환 = 보고(정점 수, 겹친 UV 충돌 수).</summary>
    public static string MakePaintMap(GameObject character, float fixedAboveY, string path, int size)
    {
        var smr = FindSkirt(character);
        if (smr == null) return "Hanbok_Skirt 없음";
        var m = smr.sharedMesh;
        var baked = new Mesh(); smr.BakeMesh(baked, true);
        var bv = baked.vertices; var uv = m.uv; var tri = m.triangles;
        Transform R = character.transform;
        var fixedV = new bool[bv.Length]; int nf = 0;
        for (int i = 0; i < bv.Length; i++) { float y = R.InverseTransformPoint(smr.transform.TransformPoint(bv[i])).y; fixedV[i] = y >= fixedAboveY; if (fixedV[i]) nf++; }
        Object.DestroyImmediate(baked);

        var px = new Color32[size * size];
        var owner = new int[size * size];          // 0 = 비어 있음, 1 = 고정, 2 = 움직임
        int conflicts = 0;
        Color32 red = new Color32(255, 0, 0, 255), green = new Color32(0, 255, 0, 255), black = new Color32(0, 0, 0, 255);
        for (int i = 0; i < px.Length; i++) px[i] = black;
        for (int t = 0; t < tri.Length; t += 3)
        {
            int a = tri[t], b = tri[t + 1], c = tri[t + 2];
            Vector2 A = uv[a] * size, B = uv[b] * size, C = uv[c] * size;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(A.x, Mathf.Min(B.x, C.x))) - 1), x1 = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(A.x, Mathf.Max(B.x, C.x))) + 1);
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(A.y, Mathf.Min(B.y, C.y))) - 1), y1 = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(A.y, Mathf.Max(B.y, C.y))) + 1);
            float area = (B.x - A.x) * (C.y - A.y) - (C.x - A.x) * (B.y - A.y);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    Vector2 P = new Vector2(x + 0.5f, y + 0.5f);
                    bool inside;
                    if (Mathf.Abs(area) < 1e-6f) inside = (P - A).sqrMagnitude < 2f;
                    else
                    {
                        float w0 = ((B.x - P.x) * (C.y - P.y) - (C.x - P.x) * (B.y - P.y)) / area;
                        float w1 = ((C.x - P.x) * (A.y - P.y) - (A.x - P.x) * (C.y - P.y)) / area;
                        float w2 = 1f - w0 - w1;
                        float eps = 1.5f / Mathf.Max(1f, Mathf.Sqrt(Mathf.Abs(area)));   // 가장자리 1~2 텍셀 여유
                        inside = w0 >= -eps && w1 >= -eps && w2 >= -eps;
                    }
                    if (!inside) continue;
                    float da = (P - A).sqrMagnitude, db = (P - B).sqrMagnitude, dc = (P - C).sqrMagnitude;
                    int v = da <= db && da <= dc ? a : (db <= dc ? b : c);
                    int o = fixedV[v] ? 1 : 2;
                    int idx = y * size + x;
                    if (owner[idx] != 0 && owner[idx] != o) { conflicts++; if (o == 1) continue; } // 겹치면 움직임 우선(고정이 번지지 않게)
                    owner[idx] = o; px[idx] = o == 1 ? red : green;
                }
        }
        // 꼭짓점 UV 자리는 반드시 그 꼭짓점 색(겹친 UV 끼리 다르면 셈)
        int vconf = 0;
        var vown = new Dictionary<int, int>();
        for (int i = 0; i < uv.Length; i++)
        {
            int x = Mathf.Clamp((int)(uv[i].x * size), 0, size - 1), y = Mathf.Clamp((int)(uv[i].y * size), 0, size - 1);
            int idx = y * size + x; int o = fixedV[i] ? 1 : 2;
            int prev; if (vown.TryGetValue(idx, out prev) && prev != o) vconf++;
            vown[idx] = o;
        }
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
        tex.SetPixels32(px); tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.isReadable = true; imp.sRGBTexture = false; imp.mipmapEnabled = false; imp.filterMode = FilterMode.Point;
        imp.textureCompression = TextureImporterCompression.Uncompressed; imp.npotScale = TextureImporterNPOTScale.None; imp.maxTextureSize = size;
        imp.SaveAndReimport();
        return "paint: 정점 " + bv.Length + " 고정 " + nf + " / 텍셀 겹침 " + conflicts + " / 같은 UV 다른 속성 정점 " + vconf;
    }

    static CurveSerializeData Curve(float value, params Keyframe[] keys)
    {
        var c = new CurveSerializeData(value, new AnimationCurve(keys));
        return c;
    }

    /// <summary>MeshCloth 로 바꾼다. 반환 = 보고.</summary>
    public static string Apply(GameObject character, Texture2D paint)
    {
        var smr = FindSkirt(character);
        if (smr == null) return "Hanbok_Skirt 없음";
        var an = character.GetComponent<Animator>();
        Transform hips = an != null ? an.GetBoneTransform(HumanBodyBones.Hips) : null;
        if (hips == null) return "Hips 없음";
        Transform R = character.transform;

        // 옛 치마 천(BoneCloth) 지움
        var old = R.Find("Cloth_Skirt");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        // 엉덩이 캡슐(좌우로 누움) — 몸 단면(측정): 루트 y 0.88 에서 x ±0.16 · z −0.07~0.11
        var hipCol = hips.Find("SkirtHipCollider");
        if (hipCol == null) { hipCol = new GameObject("SkirtHipCollider").transform; Undo.RegisterCreatedObjectUndo(hipCol.gameObject, "skirt"); hipCol.SetParent(hips, false); }
        hipCol.position = R.TransformPoint(new Vector3(0f, 0.88f, 0.015f));
        hipCol.rotation = R.rotation;
        var hc = hipCol.GetComponent<MagicaCapsuleCollider>();
        if (hc == null) hc = Undo.AddComponent<MagicaCapsuleCollider>(hipCol.gameObject);
        hc.direction = MagicaCapsuleCollider.Direction.X;
        hc.radiusSeparation = false;
        hc.SetSize(0.095f, 0.095f, 0.34f);
        hc.center = Vector3.zero;

        // 발 캡슐(발목 → 발끝) — 달릴 때 뒤로 찬 발·부츠가 치마 뒷자락을 뚫지 않게
        var feet = new List<MagicaCapsuleCollider>();
        foreach (var pair in new[] { new[] { HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes }, new[] { HumanBodyBones.RightFoot, HumanBodyBones.RightToes } })
        {
            var foot = an.GetBoneTransform(pair[0]); var toe = an.GetBoneTransform(pair[1]);
            if (foot == null) continue;
            var fc = foot.Find("SkirtFootCollider");
            if (fc == null) { fc = new GameObject("SkirtFootCollider").transform; Undo.RegisterCreatedObjectUndo(fc.gameObject, "skirt"); fc.SetParent(foot, false); }
            Vector3 a = foot.position, b = toe != null ? toe.position : foot.position + R.forward * 0.15f;
            Vector3 dir = b - a; float len = dir.magnitude + 0.08f;
            fc.position = (a + b) * 0.5f;
            fc.rotation = Quaternion.LookRotation(dir.normalized, R.up);
            var cap = fc.GetComponent<MagicaCapsuleCollider>(); if (cap == null) cap = Undo.AddComponent<MagicaCapsuleCollider>(fc.gameObject);
            cap.direction = MagicaCapsuleCollider.Direction.Z; cap.radiusSeparation = true; cap.SetSize(0.06f, 0.045f, len); cap.center = Vector3.zero;
            feet.Add(cap);
        }

        var go = new GameObject("Cloth_Skirt");
        Undo.RegisterCreatedObjectUndo(go, "skirt");
        go.transform.SetParent(R, false);
        var cloth = go.AddComponent<MagicaCloth>();
        var sd = cloth.SerializeData;
        sd.clothType = ClothProcess.ClothType.MeshCloth;
        sd.sourceRenderers.Clear(); sd.sourceRenderers.Add(smr);
        sd.meshWriteMode = ClothMeshWriteMode.PositionAndNormal;
        sd.paintMode = ClothSerializeData.PaintMode.Texture_Fixed_Move;
        sd.paintMaps.Clear(); sd.paintMaps.Add(paint);
        sd.paintMapUvChannel = 0;
        sd.updateMode = ClothUpdateMode.AnimatorLinkage;
        // 대리 메시: 겉·안 두 겹(두께 1 cm 안팎)과 잘게 나뉜 정점을 합침
        sd.reductionSetting.simpleDistance = 0.025f;
        sd.reductionSetting.shapeDistance = 0.035f;
        // 법선을 엉덩이 중심 바깥쪽으로 맞춤(백스톱 방향)
        sd.normalAlignmentSetting.alignmentMode = NormalAlignmentSettings.AlignmentMode.Transform;
        sd.normalAlignmentSetting.adjustmentTransform = hips;
        sd.gravity = 5f;
        sd.damping = new CurveSerializeData(0.08f);
        sd.radius = new CurveSerializeData(0.015f);
        sd.inertiaConstraint.worldInertia = 0.4f;
        sd.inertiaConstraint.depthInertia = 0.5f;
        sd.inertiaConstraint.particleSpeedLimit.value = 10f;
        sd.inertiaConstraint.particleSpeedLimit.use = true;
        sd.inertiaConstraint.teleportMode = InertiaConstraint.TeleportMode.Reset;
        sd.inertiaConstraint.teleportDistance = 0.5f;
        sd.tetherConstraint.distanceCompression = 0.4f;
        sd.triangleBendingConstraint.stiffness = 0.6f;
        sd.angleRestorationConstraint.useAngleRestoration = true;
        sd.angleRestorationConstraint.stiffness = Curve(0.2f, new Keyframe(0f, 1f), new Keyframe(1f, 0.5f));
        sd.angleLimitConstraint.useAngleLimit = false;
        // 허리에서 멀수록 더 멀리 움직일 수 있게(허리 0 → 밑단 0.4 m — 0.5 m 는 달려 베기 때 종처럼 너무 부풀었음)
        sd.motionConstraint.useMaxDistance = true;
        sd.motionConstraint.maxDistance = Curve(0.4f, new Keyframe(0f, 0f), new Keyframe(0.15f, 0.08f), new Keyframe(1f, 1f));
        sd.motionConstraint.useBackstop = true;
        sd.motionConstraint.backstopRadius = 10f;
        sd.motionConstraint.backstopDistance = new CurveSerializeData(0.015f);
        // 충돌: 허벅지·정강이·엉덩이·발, 선분(Edge) — 정점 사이로 빠져나가지 않게
        sd.colliderCollisionConstraint.mode = ColliderCollisionConstraint.Mode.Edge;
        sd.colliderCollisionConstraint.friction = 0.05f;
        sd.colliderCollisionConstraint.colliderList.Clear();
        foreach (var bn in new[] { HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg })
        {
            var t = an.GetBoneTransform(bn);
            var cc = t != null ? t.GetComponent<MagicaCapsuleCollider>() : null;
            if (cc != null) sd.colliderCollisionConstraint.colliderList.Add(cc);
        }
        sd.colliderCollisionConstraint.colliderList.Add(hc);
        foreach (var fcap in feet) sd.colliderCollisionConstraint.colliderList.Add(fcap);
        sd.selfCollisionConstraint.selfMode = SelfCollisionConstraint.SelfCollisionMode.FullMesh;
        sd.selfCollisionConstraint.surfaceThickness = new CurveSerializeData(0.008f);
        sd.springConstraint.useSpring = false;
        EditorUtility.SetDirty(cloth);

        // 치마 뼈 뿌리가 허벅지를 따라가는 것(기준 자세)은 그대로, 가닥 끼임 풀기는 끔(뼈가 이제 계산되지 않음)
        var follow = character.GetComponent<SkirtLegFollow>();
        if (follow != null) { Undo.RecordObject(follow, "skirt"); follow.cloth = cloth; follow.untrap = false; EditorUtility.SetDirty(follow); }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(character.scene);
        return "MeshCloth 적용: " + character.name + " (충돌체 " + sd.colliderCollisionConstraint.colliderList.Count + ")";
    }
}
