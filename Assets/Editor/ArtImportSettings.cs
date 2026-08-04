using UnityEditor;
using UnityEngine;

namespace BlackjackGame.EditorTools
{
    /// <summary>
    /// Forces everything under Assets/Art to import as a UI sprite.
    ///
    /// Without this the textures import with the project's default texture type (which is
    /// "Default", not "Sprite", in a non-2D project), so <c>LoadAssetAtPath&lt;Sprite&gt;</c>
    /// returns null and the card library silently comes up empty. Doing it here rather than
    /// by hand keeps the settings in source control and survives a Library wipe.
    /// </summary>
    public sealed class ArtImportSettings : AssetPostprocessor
    {
        private const string ArtRoot = "Assets/Art/";

        private void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');
            if (!path.StartsWith(ArtRoot)) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.spritePixelsPerUnit = 100f;
            importer.maxTextureSize = 2048;
            // UI art is read at close to 1:1, so block compression artefacts would show.
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }
}
