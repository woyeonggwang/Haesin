// 경복궁 아틀라스(_Baked) 가져오기 설정 — 파일을 복사해 넣을 때 바로 맞는 설정으로 가져오게 해서 두 번 가져오기를 피한다.
#if UNITY_EDITOR
using UnityEditor;

public class PalaceBakedImportPostprocessor : AssetPostprocessor
{
    static bool IsBaked(string p) { return p.Replace('\\', '/').Contains("/Gyeongbokgung/_Baked/"); }

    void OnPreprocessTexture()
    {
        if (!IsBaked(assetPath)) return;
        var ti = (TextureImporter)assetImporter;
        ti.maxTextureSize = 4096;
        ti.streamingMipmaps = true;
        if (assetPath.EndsWith("_N.png")) { ti.textureType = TextureImporterType.NormalMap; }
        else if (assetPath.EndsWith("_BC.png")) { ti.textureType = TextureImporterType.Default; ti.sRGBTexture = true; }
    }

    void OnPreprocessModel()
    {
        if (!IsBaked(assetPath)) return;
        var mi = (ModelImporter)assetImporter;
        mi.materialImportMode = ModelImporterMaterialImportMode.None;
    }
}
#endif
