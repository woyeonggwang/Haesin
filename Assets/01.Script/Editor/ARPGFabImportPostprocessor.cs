using UnityEditor;
using UnityEngine;

/// <summary>
/// Fab 판 ARPG Samurai(Kevin Animation, UE4 마네킹 뼈대) FBX 를 Unity 로 가져올 때 자동 설정.
/// - 폴더: Assets/02.Resources/00.Component/07.Animation/ARPG_Samurai_Fab/
/// - 아바타 원본: ARPG_Samurai_Anim_UE_A_Bindpose.fbx (Humanoid, Create From This Model, Chest=spine_02, UpperChest=spine_03 수동 지정)
/// - 나머지: Humanoid + Copy From Other(위 아바타), 머티리얼 가져오지 않음, 반복 동작(IPC/Rootmotion 이동, Idle, Guard 등)은 Loop Time.
/// 처음 가져올 때만 적용(userData 표시) — 이후 사람이 바꾼 설정은 덮어쓰지 않는다.
/// </summary>
public class ARPGFabImportPostprocessor : AssetPostprocessor
{
    const string Folder = "Assets/02.Resources/00.Component/07.Animation/ARPG_Samurai_Fab/";
    const string AvatarSrc = Folder + "Game/ARPG_Samurai/Animations/ARPG_Samurai_Anim_UE_A_Bindpose.fbx";
    const string Mark = "haesin-fab-humanoid-v1";

    static bool Target(string path)
    {
        return path.StartsWith(Folder) && path.ToLowerInvariant().EndsWith(".fbx") && !string.Equals(path, AvatarSrc, System.StringComparison.OrdinalIgnoreCase);
    }

    void OnPreprocessModel()
    {
        if (!Target(assetPath)) return;
        var mi = (ModelImporter)assetImporter;
        if (mi.userData == Mark) return;
        Avatar av = null;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(AvatarSrc)) { var a = o as Avatar; if (a != null) { av = a; break; } }
        if (av == null) { Debug.LogWarning("[ARPGFab] 아바타 원본이 아직 없음: " + AvatarSrc); return; }
        mi.animationType = ModelImporterAnimationType.Human;
        mi.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
        mi.sourceAvatar = av;
        mi.importAnimation = true;
        mi.materialImportMode = ModelImporterMaterialImportMode.None;
        mi.userData = Mark;
    }

    void OnPreprocessAnimation()
    {
        if (!Target(assetPath)) return;
        var mi = (ModelImporter)assetImporter;
        if (mi.clipAnimations != null && mi.clipAnimations.Length > 0) return; // 이미 설정됨
        var clips = mi.defaultClipAnimations;
        string file = System.IO.Path.GetFileNameWithoutExtension(assetPath);
        string shortName = file.Replace("ARPG_Samurai_Anim_UE_", "");
        foreach (var c in clips) { c.loopTime = ShouldLoop(shortName); c.name = shortName; BakeRoot(c); }
        mi.clipAnimations = clips;
    }

    /// <summary>Unity 판 rig_ 클립과 같은 루트 설정: 회전·높이는 포즈에 굽고(원본 기준), XZ 이동만 루트 모션으로. 안 하면 몸통 비틀기가 루트 회전으로 빠져 사라짐(SwordCombatant 는 deltaPosition 만 적용).</summary>
    public static void BakeRoot(ModelImporterClipAnimation c)
    {
        c.lockRootRotation = true; c.keepOriginalOrientation = true;
        c.lockRootHeightY = true; c.keepOriginalPositionY = true;
        c.lockRootPositionXZ = false; c.keepOriginalPositionXZ = false;
    }

    static bool ShouldLoop(string n)
    {
        if (n.Contains("_Start") || n.Contains("Turn") || n.Contains("_To_")) return false;
        if (n.EndsWith("_IPC") || n.EndsWith("_Rootmotion")) return true;
        string[] loops = { "Idle1", "Idle2", "Idle_no_weapon", "Guard", "Guard_B", "Airborne", "Airborne_no_weapon", "Crouch", "Crouch_no_weapon", "Stun", "Climb_Down", "Climb_Down_Fast" };
        foreach (var l in loops) if (n == l) return true;
        return false;
    }
}
