using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 퀘스트 NPC 모델(06.Character/<이름>/ — Blender 에서 만든 FBX + 4096 아틀라스 3장)을 가져오기 설정하는 도구.
// 한양 조력자 때 MCP 로 한 설정을 이름만 바꿔 되풀이하려고 만듦(2026-10-09).
// Setup: 텍스처(BaseColor sRGB, Normal 노멀맵, Mask 선형, 최대 4096·스트리밍 밉맵) → HDRP/Lit 재질 2개(Skin 한 면, Cloth 양면) → Humanoid + AddRemap.
// MakePrefab: FBX 프리팹 변형(LODGroup 1.85 · (0,0.85,0) · CrossFade 0.1 · 15/40/150 m, Animator 컨트롤러 없음·루트모션 끔·CullUpdateTransforms).
// Capture: 빈 추가 씬에서 기본(T) 자세와 걷기 클립을 샘플한 LOD0/1/2 를 BakeMesh 로 찍어 Captures/npc/<이름>_lods.png (씬은 저장 안 함).
public static class NpcModelSetupTool
{
    const string Root = "Assets/02.Resources/00.Component/06.Character/";
    const string WalkClip = "Assets/02.Resources/00.Component/07.Animation/ARPGSamurai/Animations/Humanoid/rig_Walk_no_weapon_IPC.anim";

    public static string Setup(string name)
    {
        var sb = new StringBuilder();
        string dir = Root + name;
        string[] tex = { "T_" + name + "_BaseColor.jpg", "T_" + name + "_Normal.png", "T_" + name + "_Mask.png" };
        foreach (var t in tex)
        {
            string tp = dir + "/Textures/" + t;
            var ti = AssetImporter.GetAtPath(tp) as TextureImporter;
            if (ti == null) { sb.AppendLine("텍스처 없음 " + tp); continue; }
            ti.textureType = t.Contains("Normal") ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.sRGBTexture = t.Contains("BaseColor");
            ti.maxTextureSize = 4096; ti.streamingMipmaps = true;
            if (t.Contains("Mask")) ti.alphaSource = TextureImporterAlphaSource.FromInput;
            ti.SaveAndReimport();
        }
        if (!AssetDatabase.IsValidFolder(dir + "/Materials")) AssetDatabase.CreateFolder(dir, "Materials");
        var sh = Shader.Find("HDRP/Lit");
        var bc = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "/Textures/" + tex[0]);
        var nm = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "/Textures/" + tex[1]);
        var mk = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "/Textures/" + tex[2]);
        System.Reflection.MethodInfo reset = null;
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            var tp = asm.GetType("UnityEditor.Rendering.HighDefinition.HDShaderUtils");
            if (tp == null) continue;
            foreach (var m in tp.GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
            {
                if (m.Name != "ResetMaterialKeywords") continue;
                var ps = m.GetParameters();
                if (ps.Length == 1 && ps[0].ParameterType == typeof(Material)) reset = m;
            }
        }
        string[] mnames = { "M_" + name + "_Skin", "M_" + name + "_Cloth" };
        foreach (var n in mnames)
        {
            string mp = dir + "/Materials/" + n + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(mp);
            if (mat == null) { mat = new Material(sh); AssetDatabase.CreateAsset(mat, mp); }
            mat.shader = sh;
            mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_BaseColorMap", bc);
            mat.SetTexture("_NormalMap", nm); mat.SetFloat("_NormalScale", 1f); mat.EnableKeyword("_NORMALMAP");
            mat.SetTexture("_MaskMap", mk); mat.EnableKeyword("_MASKMAP");
            mat.SetFloat("_MetallicRemapMin", 0f); mat.SetFloat("_MetallicRemapMax", 0f); mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_SmoothnessRemapMin", 0f); mat.SetFloat("_SmoothnessRemapMax", 1f);
            mat.SetFloat("_AORemapMin", 0f); mat.SetFloat("_AORemapMax", 1f);
            bool ds = n.EndsWith("Cloth");   // 옷·관모는 한 겹이라 양면
            mat.SetFloat("_DoubleSidedEnable", ds ? 1f : 0f); mat.SetFloat("_DoubleSidedNormalMode", 0f);
            mat.SetFloat("_CullMode", ds ? 0f : 2f); mat.SetFloat("_CullModeForward", ds ? 0f : 2f);
            if (ds) mat.EnableKeyword("_DOUBLESIDED_ON"); else mat.DisableKeyword("_DOUBLESIDED_ON");
            mat.doubleSidedGI = ds;
            if (reset != null) reset.Invoke(null, new object[] { mat });
            EditorUtility.SetDirty(mat);
        }
        AssetDatabase.SaveAssets();
        string p = dir + "/" + name + ".fbx";
        var imp = AssetImporter.GetAtPath(p) as ModelImporter;
        if (imp == null) return sb.AppendLine("FBX 없음 " + p).ToString();
        imp.animationType = ModelImporterAnimationType.Human;
        imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        imp.importAnimation = false;
        imp.importNormals = ModelImporterNormals.Import;
        imp.importTangents = ModelImporterTangents.CalculateMikk;
        imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        imp.importBlendShapes = false; imp.importCameras = false; imp.importLights = false;
        foreach (var n in mnames)
            imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), n), AssetDatabase.LoadAssetAtPath<Material>(dir + "/Materials/" + n + ".mat"));
        imp.SaveAndReimport();
        Avatar avatar = null;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(p)) if (o is Avatar) avatar = (Avatar)o;
        sb.AppendLine("avatar valid " + (avatar != null && avatar.isValid) + " human " + (avatar != null && avatar.isHuman));
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(p);
        foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            string mn = ""; foreach (var m in r.sharedMaterials) mn += (m != null ? m.name : "null") + " ";
            sb.AppendLine(r.name + " tris " + (r.sharedMesh.triangles.Length / 3) + " mats " + mn);
        }
        Transform la = null, lf = null;
        foreach (var t in model.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "mixamorig:LeftArm") la = t;
            if (t.name == "mixamorig:LeftForeArm") lf = t;
        }
        if (la != null && lf != null) sb.AppendLine("L upperarm dir " + (lf.position - la.position).normalized.ToString("F3"));
        return sb.ToString();
    }

    public static string MakePrefab(string name, float size = 1.85f, float refY = 0.85f)
    {
        string dir = Root + name;
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(dir + "/" + name + ".fbx");
        var active = SceneManager.GetActiveScene();
        var tmp = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        string res;
        try
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model, tmp);
            var lg = inst.GetComponent<LODGroup>();
            lg.size = size; lg.localReferencePoint = new Vector3(0f, refY, 0f);
            lg.fadeMode = LODFadeMode.CrossFade; lg.animateCrossFading = false;
            var lods = lg.GetLODs(); float[] h = { 0.1068f, 0.0401f, 0.0107f };
            for (int i = 0; i < lods.Length && i < 3; i++) { lods[i].screenRelativeTransitionHeight = h[i]; lods[i].fadeTransitionWidth = 0.1f; }
            lg.SetLODs(lods);
            var an = inst.GetComponent<Animator>(); if (an == null) an = inst.AddComponent<Animator>();
            an.runtimeAnimatorController = null; an.applyRootMotion = false; an.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            bool ok; var saved = PrefabUtility.SaveAsPrefabAsset(inst, dir + "/" + name + ".prefab", out ok);
            res = "prefab " + ok + " variant " + PrefabUtility.IsPartOfVariantPrefab(saved);
        }
        finally
        {
            EditorSceneManager.CloseScene(tmp, true);
            SceneManager.SetActiveScene(active);
        }
        return res;
    }

    public static string Capture(string name)
    {
        string dir = Root + name;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(dir + "/" + name + ".prefab");
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(WalkClip);
        var active = SceneManager.GetActiveScene();
        var tmp = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        string outp = System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, "Captures/npc/" + name + "_lods.png");
        try
        {
            int[] lodOf = { 0, 0, 1, 2 }; bool[] anim = { false, true, true, true };
            for (int k = 0; k < 4; k++)
            {
                // 휴머노이드 샘플은 루트 위치를 덮어쓰므로 부모를 두고 부모로 띄움
                var parent = new GameObject("P" + k); SceneManager.MoveGameObjectToScene(parent, tmp);
                parent.transform.position = new Vector3(-1.5f + k * 1.0f, 3000f, 0f);
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, tmp); inst.transform.SetParent(parent.transform, false);
                if (anim[k] && clip != null) clip.SampleAnimation(inst, clip.length * 0.25f);
                var smrs = inst.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var smr = smrs[Mathf.Min(lodOf[k], smrs.Length - 1)];
                var m = new Mesh(); smr.BakeMesh(m);
                var bgo = new GameObject("Baked" + k); SceneManager.MoveGameObjectToScene(bgo, tmp);
                bgo.transform.position = smr.transform.position; bgo.transform.rotation = smr.transform.rotation;
                bgo.AddComponent<MeshFilter>().sharedMesh = m; bgo.AddComponent<MeshRenderer>().sharedMaterials = smr.sharedMaterials;
                inst.SetActive(false);
            }
            var camGo = new GameObject("TmpCam"); SceneManager.MoveGameObjectToScene(camGo, tmp);
            var cam = camGo.AddComponent<Camera>();
            cam.transform.position = new Vector3(0f, 3000.95f, 4.6f); cam.transform.LookAt(new Vector3(0f, 3000.85f, 0f));
            cam.fieldOfView = 35f; cam.nearClipPlane = 0.1f; cam.farClipPlane = 50f;
            var rt = new RenderTexture(1600, 1000, 24, RenderTextureFormat.ARGB32); cam.targetTexture = rt;
            cam.Render(); cam.Render();
            RenderTexture.active = rt; var tx = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
            tx.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0); tx.Apply(); RenderTexture.active = null;
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outp));
            System.IO.File.WriteAllBytes(outp, tx.EncodeToPNG());
            cam.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(tx);
        }
        finally
        {
            EditorSceneManager.CloseScene(tmp, true);
            SceneManager.SetActiveScene(active);
        }
        return outp;
    }
}
