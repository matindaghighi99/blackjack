using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using Object = UnityEngine.Object;

namespace BlackjackGame.EditorTools
{
    /// <summary>
    /// Builds the TextMeshPro font assets and their gold material presets from the raw
    /// font files in Assets/Fonts.
    ///
    /// Done in script rather than through <b>Window ▸ TextMeshPro ▸ Font Asset Creator</b>
    /// so the whole look is reproducible: delete Assets/Settings/Fonts and re-run, and you
    /// get byte-for-byte the same atlases and materials. Run via
    /// <b>Blackjack ▸ Rebuild Font Assets</b> or the command-line entry point.
    /// </summary>
    public static class FontAssetBuilder
    {
        public const string DisplayFontPath = "Assets/Fonts/TeXGyreBonum-Bold.otf";
        public const string BodyBoldFontPath = "Assets/Fonts/Lato-Bold.ttf";
        public const string BodyFontPath = "Assets/Fonts/Lato-Regular.ttf";

        private const string OutputFolder = "Assets/Settings/Fonts";

        public const string DisplayAssetPath = OutputFolder + "/Display SDF.asset";
        public const string BodyBoldAssetPath = OutputFolder + "/Body Bold SDF.asset";
        public const string BodyAssetPath = OutputFolder + "/Body SDF.asset";

        public const string GoldMaterialPath = OutputFolder + "/Display SDF - Gold.mat";
        public const string GoldSmallMaterialPath = OutputFolder + "/Body Bold SDF - Gold.mat";
        public const string InkMaterialPath = OutputFolder + "/Body SDF - Ink.mat";

        public const string GoldGradientPath = OutputFolder + "/Gold Gradient.asset";

        // Matches the sampled palette in art-source/generate_ui_kit.py.
        private static readonly Color GoldFace = new Color32(247, 226, 160, 255);
        private static readonly Color GoldOutline = new Color32(74, 48, 12, 255);
        private static readonly Color InkFace = new Color32(232, 224, 202, 255);

        [MenuItem("Blackjack/Rebuild Font Assets", priority = 10)]
        public static void RebuildMenu()
        {
            BuildAll();
            EditorUtility.DisplayDialog("Blackjack",
                "TextMeshPro font assets and gold materials rebuilt.", "Nice");
        }

        public static void BuildFromCommandLine()
        {
            try
            {
                BuildAll();
                Debug.Log("[FontAssetBuilder] SUCCESS");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError($"[FontAssetBuilder] FAILED — {e}");
                EditorApplication.Exit(1);
            }
        }

        public static void BuildAll()
        {
            EnsureFolder(OutputFolder);

            // 90pt sampling with SDF gives clean edges from ~20px to ~200px, which covers
            // everything from the subtitle rows to the wordmark.
            TMP_FontAsset display = BuildFontAsset(DisplayFontPath, DisplayAssetPath, 90, 1024);
            TMP_FontAsset bodyBold = BuildFontAsset(BodyBoldFontPath, BodyBoldAssetPath, 90, 1024);
            TMP_FontAsset body = BuildFontAsset(BodyFontPath, BodyAssetPath, 90, 1024);

            BuildMaterial(display, GoldMaterialPath, GoldFace, GoldOutline,
                outlineWidth: 0.09f, bevel: true, glow: true);
            BuildMaterial(bodyBold, GoldSmallMaterialPath, GoldFace, GoldOutline,
                outlineWidth: 0.07f, bevel: false, glow: false);
            BuildMaterial(body, InkMaterialPath, InkFace, new Color(0f, 0f, 0f, 0.55f),
                outlineWidth: 0.05f, bevel: false, glow: false);

            BuildGoldGradient();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[FontAssetBuilder] Built 3 font assets and 3 materials in " + OutputFolder);
        }

        private static TMP_FontAsset BuildFontAsset(string sourcePath, string assetPath,
            int samplingPointSize, int atlasSize)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
            if (font == null)
                throw new InvalidOperationException(
                    $"Font missing at '{sourcePath}'. See Assets/Fonts/README.md.");

            // Dynamic population: glyphs are rasterised into the atlas on demand, so the
            // asset stays small and never misses a character we forgot to include.
            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(
                font, samplingPointSize, 9, GlyphRenderMode.SDFAA,
                atlasSize, atlasSize, AtlasPopulationMode.Dynamic, true);

            if (asset == null)
                throw new InvalidOperationException($"CreateFontAsset returned null for {sourcePath}");

            asset.name = Path.GetFileNameWithoutExtension(assetPath);

            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null) AssetDatabase.DeleteAsset(assetPath);

            AssetDatabase.CreateAsset(asset, assetPath);

            // The atlas texture and material are sub-assets of the font asset.
            if (asset.atlasTextures != null)
            {
                foreach (Texture2D tex in asset.atlasTextures)
                {
                    if (tex == null) continue;
                    tex.name = asset.name + " Atlas";
                    AssetDatabase.AddObjectToAsset(tex, asset);
                }
            }
            if (asset.material != null)
            {
                asset.material.name = asset.name + " Material";
                AssetDatabase.AddObjectToAsset(asset.material, asset);
            }

            EditorUtility.SetDirty(asset);
            Debug.Log($"[FontAssetBuilder] {assetPath}");
            return asset;
        }

        /// <summary>
        /// A material preset derived from the font's own material. TMP keeps the face and
        /// outline in shader properties, so the gold treatment is live — no baked sprites,
        /// and it stays crisp at any size.
        /// </summary>
        private static void BuildMaterial(TMP_FontAsset fontAsset, string path,
            Color face, Color outline, float outlineWidth, bool bevel, bool glow)
        {
            var material = new Material(fontAsset.material) { name = Path.GetFileNameWithoutExtension(path) };

            material.SetColor(ShaderUtilities.ID_FaceColor, face);
            material.SetColor(ShaderUtilities.ID_OutlineColor, outline);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, outlineWidth);

            // Soft drop shadow lifts text off the felt.
            material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            material.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.45f));
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.45f);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.45f);
            material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.42f);

            // Bevel and specular exist on TextMeshPro/Distance Field but not on the Mobile
            // variants, and only ID_BevelAmount has a ShaderUtilities constant — the rest
            // are looked up by name and skipped when the shader doesn't declare them.
            if (bevel && material.HasProperty(ShaderUtilities.ID_BevelAmount))
            {
                material.EnableKeyword(ShaderUtilities.Keyword_Bevel);
                material.SetFloat(ShaderUtilities.ID_BevelAmount, 0.45f);
                material.SetFloat(ShaderUtilities.ID_LightAngle, Mathf.PI);
                SetFloatIfPresent(material, "_BevelWidth", 0.10f);
                SetFloatIfPresent(material, "_BevelRoundness", 0.35f);
                SetFloatIfPresent(material, "_SpecularPower", 2.2f);
                SetFloatIfPresent(material, "_Diffuse", 0.55f);
                SetFloatIfPresent(material, "_Reflectivity", 8f);
                SetColorIfPresent(material, "_SpecularColor", new Color32(255, 250, 224, 255));
            }

            if (glow && material.HasProperty(ShaderUtilities.ID_GlowColor))
            {
                material.EnableKeyword(ShaderUtilities.Keyword_Glow);
                material.SetColor(ShaderUtilities.ID_GlowColor, new Color32(255, 214, 120, 90));
                material.SetFloat(ShaderUtilities.ID_GlowPower, 0.25f);
                material.SetFloat(ShaderUtilities.ID_GlowOuter, 0.12f);
            }

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(material, path);
            Debug.Log($"[FontAssetBuilder] {path}");
        }

        /// <summary>
        /// Vertex-colour gradient applied on top of the face colour. TMP interpolates
        /// bilinearly between four corners, so this gives the light-to-deep gold fall-off
        /// that used to be baked into the label PNGs — but live, and crisp at any size.
        /// </summary>
        private static void BuildGoldGradient()
        {
            var gradient = ScriptableObject.CreateInstance<TMP_ColorGradient>();
            gradient.name = "Gold Gradient";
            gradient.topLeft = new Color32(255, 246, 206, 255);
            gradient.topRight = new Color32(255, 240, 190, 255);
            gradient.bottomLeft = new Color32(198, 150, 62, 255);
            gradient.bottomRight = new Color32(214, 170, 84, 255);

            var existing = AssetDatabase.LoadAssetAtPath<TMP_ColorGradient>(GoldGradientPath);
            if (existing != null) AssetDatabase.DeleteAsset(GoldGradientPath);
            AssetDatabase.CreateAsset(gradient, GoldGradientPath);
            Debug.Log($"[FontAssetBuilder] {GoldGradientPath}");
        }

        private static void SetFloatIfPresent(Material m, string property, float value)
        {
            if (m.HasProperty(property)) m.SetFloat(property, value);
        }

        private static void SetColorIfPresent(Material m, string property, Color value)
        {
            if (m.HasProperty(property)) m.SetColor(property, value);
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            string leaf = Path.GetFileName(folder);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf)) return;
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
