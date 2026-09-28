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
    /// Builds the TextMeshPro font assets from the raw font files in Assets/Fonts.
    ///
    /// Two families, five faces: EB Garamond (display serif — the wordmark, headlines, felt
    /// print) and Inter (UI and every number; its figures are frozen to tabular so rolling
    /// balances don't jitter — see art-source/prepare_fonts.py).
    ///
    /// Materials are left plain: colour comes from the palette at runtime (vertex colour),
    /// so no per-colour material presets are needed. The only extra is a restrained gold
    /// gradient reserved for the wordmark.
    ///
    /// Done in script rather than through the Font Asset Creator so the look is
    /// reproducible: delete Assets/Settings/Fonts and re-run. Run via
    /// <b>Blackjack ▸ Rebuild Font Assets</b> or the command-line entry point.
    /// </summary>
    public static class FontAssetBuilder
    {
        public const string SerifFontPath = "Assets/Fonts/EBGaramond-Regular-Lining.otf";
        public const string SerifItalicFontPath = "Assets/Fonts/EBGaramond-Italic-Lining.otf";
        public const string SansFontPath = "Assets/Fonts/Inter-Regular-Tabular.otf";
        public const string SansMediumFontPath = "Assets/Fonts/Inter-Medium-Tabular.otf";
        public const string SansSemiBoldFontPath = "Assets/Fonts/Inter-SemiBold-Tabular.otf";

        private const string OutputFolder = "Assets/Settings/Fonts";

        public const string SerifAssetPath = OutputFolder + "/Serif SDF.asset";
        public const string SerifItalicAssetPath = OutputFolder + "/Serif Italic SDF.asset";
        public const string SansAssetPath = OutputFolder + "/Sans SDF.asset";
        public const string SansMediumAssetPath = OutputFolder + "/Sans Medium SDF.asset";
        public const string SansSemiBoldAssetPath = OutputFolder + "/Sans SemiBold SDF.asset";

        public const string GoldGradientPath = OutputFolder + "/Gold Gradient.asset";

        /// <summary>Every asset this builder produces; used by wiring verification.</summary>
        public static readonly string[] AllOutputs =
        {
            SerifAssetPath, SerifItalicAssetPath, SansAssetPath, SansMediumAssetPath, SansSemiBoldAssetPath,
            GoldGradientPath,
        };

        [MenuItem("Blackjack/Rebuild Font Assets", priority = 10)]
        public static void RebuildMenu()
        {
            BuildAll();
            EditorUtility.DisplayDialog("Blackjack", "TextMeshPro font assets rebuilt.", "Done");
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

        /// <summary>True when every output exists (the scene bootstrapper builds them if not).</summary>
        public static bool AllBuilt()
        {
            foreach (string path in AllOutputs)
                if (AssetDatabase.LoadAssetAtPath<Object>(path) == null) return false;
            return true;
        }

        public static void BuildAll()
        {
            EnsureFolder(OutputFolder);

            // 90pt sampling with SDF gives clean edges from ~16px captions to ~120px headlines.
            BuildFontAsset(SerifFontPath, SerifAssetPath);
            BuildFontAsset(SerifItalicFontPath, SerifItalicAssetPath);
            BuildFontAsset(SansFontPath, SansAssetPath);
            BuildFontAsset(SansMediumFontPath, SansMediumAssetPath);
            BuildFontAsset(SansSemiBoldFontPath, SansSemiBoldAssetPath);

            BuildGoldGradient();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[FontAssetBuilder] Built 5 font assets in " + OutputFolder);
        }

        private static TMP_FontAsset BuildFontAsset(string sourcePath, string assetPath)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
            if (font == null)
                throw new InvalidOperationException(
                    $"Font missing at '{sourcePath}'. See Assets/Fonts/README.md.");

            // Dynamic population: glyphs are rasterised into the atlas on demand, so the
            // asset stays small and never misses a character we forgot to include.
            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(
                font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);

            if (asset == null)
                throw new InvalidOperationException($"CreateFontAsset returned null for {sourcePath}");

            asset.name = Path.GetFileNameWithoutExtension(assetPath);

            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath) != null) AssetDatabase.DeleteAsset(assetPath);
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
        /// A quiet metallic fall-off for the wordmark only: light gold at the top of the
        /// letters to the accent gold at their feet. Everything else is a flat palette colour.
        /// </summary>
        private static void BuildGoldGradient()
        {
            var gradient = ScriptableObject.CreateInstance<TMP_ColorGradient>();
            gradient.name = "Gold Gradient";
            gradient.colorMode = ColorMode.FourCornersGradient;
            gradient.topLeft = new Color32(236, 214, 160, 255);
            gradient.topRight = new Color32(231, 207, 150, 255);
            gradient.bottomLeft = new Color32(186, 148, 80, 255);
            gradient.bottomRight = new Color32(196, 158, 88, 255);

            if (AssetDatabase.LoadAssetAtPath<TMP_ColorGradient>(GoldGradientPath) != null)
                AssetDatabase.DeleteAsset(GoldGradientPath);
            AssetDatabase.CreateAsset(gradient, GoldGradientPath);
            Debug.Log($"[FontAssetBuilder] {GoldGradientPath}");
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
