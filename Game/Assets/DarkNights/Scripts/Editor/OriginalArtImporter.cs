using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor
{
    /// <summary>
    /// 只为首次导入的冻结像素素材设定无损、最近邻、左上原点和统一 PPU。
    /// 已有 importer 的人工设置保持不变，普通导入不重写 Prefab、动画或图片字节。
    /// </summary>
    public sealed class OriginalArtImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/DarkNights/Res/Art/Original/") || !assetImporter.importSettingsMissing) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(0, 1);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 4096;
        }
    }
}
