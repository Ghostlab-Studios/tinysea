#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using System.Globalization;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class TinySeaSpeciesExport
{
    // --- CONFIG ---
    private const string OutputFolderUnderAssets = "textdata"; // Assets/textdata
    private const string CsvFileName = "species.csv";
    private const string IconsFolderName = "icons";            // Assets/textdata/icons

    [MenuItem("Tools/TinySea/Export Species CSV + Icons (Legacy)")]
    public static void Export()
    {
        // 1) Find PlayerManager (source root)
        PlayerManager pm = UnityEngine.Object.FindObjectOfType<PlayerManager>();
        if (pm == null)
        {
            Debug.LogError("TinySeaSpeciesExport: No PlayerManager found in the active scene.");
            return;
        }

        // 2) Build species list:
        //    Prefer PlayerManager.species if filled, else walk children (depth-first, hierarchy order)
        List<CharacterManager> speciesList = new List<CharacterManager>();
        if (pm.species != null && pm.species.Count > 0)
        {
            // Use the configured order on PlayerManager
            for (int k = 0; k < pm.species.Count; k++)
            {
                if (pm.species[k] != null) speciesList.Add(pm.species[k]);
            }
        }
        else
        {
            // Walk the hierarchy under PlayerManager and collect CharacterManager components
            TraverseForCharacters(pm.transform, speciesList);
            if (speciesList.Count == 0)
            {
                Debug.LogError("TinySeaSpeciesExport: PlayerManager found, but no CharacterManager components under it.");
                return;
            }
        }

        // 3) Prep output paths (Assets/textdata, Assets/textdata/icons)
        string assetsPath = Application.dataPath; // .../YourProj/Assets
        string exportDir = Path.Combine(assetsPath, OutputFolderUnderAssets);   // 2-arg Combine (legacy-safe)
        string iconsDir = Path.Combine(exportDir, IconsFolderName);
        if (!Directory.Exists(exportDir)) Directory.CreateDirectory(exportDir);
        if (!Directory.Exists(iconsDir)) Directory.CreateDirectory(iconsDir);

        // 4) CSV header
        string[] header = new string[] {
            "Index","Name","Variant","Tier",
            "EatingAmount","ReproductionMultiplier",
            "DeathThreshold","DeathRate","MinimumDeaths","ReproThreshold",
            "EatingStars","ReproductionStars","DeathThresholdStars","DeathRateStars","ThermalBreadthStars",
            "Description","TemperatureThresholdText","ReproductionRateText",
            "SpeciesAmount",
            "OptimalTempK","ArrhenBreadth","ArrhenLower","ArrhenUpper","LowerBoundK","UpperBoundK",
            "IconFile"
        };

        StringBuilder sb = new StringBuilder(64 * 1024);
        sb.AppendLine(String.Join(",", header));
        CultureInfo inv = CultureInfo.InvariantCulture;

        // 5) Export each species (order = PlayerManager.species OR hierarchy order)
        for (int i = 0; i < speciesList.Count; i++)
        {
            CharacterManager c = speciesList[i];
            if (c == null) continue;

            // Thermal curve (may be null)
            double optimalK = Double.NaN, arrhenBreadth = Double.NaN, arrhenLower = Double.NaN,
                   arrhenUpper = Double.NaN, lowerK = Double.NaN, upperK = Double.NaN;

            if (c.thermalcurve != null)
            {
                optimalK = c.thermalcurve.optimalTemp;
                arrhenBreadth = c.thermalcurve.arrhenBreadth;
                arrhenLower = c.thermalcurve.arrhenLower;
                arrhenUpper = c.thermalcurve.arrhenUpper;
                lowerK = c.thermalcurve.lowerBound;
                upperK = c.thermalcurve.upperBound;
            }

            // Icon export (PNG)
            string baseName = Pad2(i) + "_" + MakeSafe(c.uniqueName) + "_" + MakeSafe(c.variant.ToString());
            string iconFileRel = TryExportIcon(c.icon, iconsDir, baseName); // returns "icons/XX_Name_Variant.png" or ""

            // CSV row
            List<string> row = new List<string>(32);
            row.Add(i.ToString(inv));
            row.Add(Csv(c.uniqueName));
            row.Add(Csv(c.variant.ToString()));
            row.Add(c.foodChainLevel.ToString(inv));

            row.Add(c.eatingAmount.ToString(inv));
            row.Add(c.reproductionMultiplier.ToString(inv));

            row.Add(c.deathThreashold.ToString(inv));
            row.Add(c.deathRate.ToString(inv));
            row.Add(c.minimumDeaths.ToString(inv));
            row.Add(c.reproThreshold.ToString(inv));

            row.Add(c.eatingStars.ToString(inv));
            row.Add(c.reproductionStars.ToString(inv));
            row.Add(c.deathThreasholdStars.ToString(inv));
            row.Add(c.deathRateStars.ToString(inv));
            row.Add(c.thermalBreadthStars.ToString(inv));

            row.Add(Csv(c.description));
            row.Add(Csv(c.temperatureThresholdText));
            row.Add(Csv(c.reproductionRateText));

            row.Add(c.speciesAmount.ToString(inv));

            row.Add(optimalK.ToString(inv));
            row.Add(arrhenBreadth.ToString(inv));
            row.Add(arrhenLower.ToString(inv));
            row.Add(arrhenUpper.ToString(inv));
            row.Add(lowerK.ToString(inv));
            row.Add(upperK.ToString(inv));

            row.Add(Csv(iconFileRel));

            sb.AppendLine(String.Join(",", row.ToArray())); // old overload needs string[]
        }

        // 6) Write CSV (UTF-8, no BOM)
        string csvPath = Path.Combine(exportDir, CsvFileName);
        File.WriteAllText(csvPath, sb.ToString(), new UTF8Encoding(false));

        // 7) Show result
        EditorUtility.RevealInFinder(csvPath);
        Debug.Log("TinySeaSpeciesExport: Wrote CSV → " + csvPath + "  |  Icons → " + iconsDir);
    }

    // Depth-first traversal collecting CharacterManager components in hierarchy order
    private static void TraverseForCharacters(Transform root, List<CharacterManager> outList)
    {
        if (root == null || outList == null) return;

        CharacterManager self = root.GetComponent<CharacterManager>();
        if (self != null) outList.Add(self);

        int childCount = root.childCount;
        for (int i = 0; i < childCount; i++)
        {
            Transform child = root.GetChild(i);
            TraverseForCharacters(child, outList);
        }
    }

    // --- Helpers ---

    private static string TryExportIcon(Sprite sprite, string iconsDir, string baseName)
    {
        try
        {
            if (sprite == null || sprite.texture == null) return "";

            Texture2D tex = sprite.texture;
            string assetPath = AssetDatabase.GetAssetPath(tex);
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;

            bool changedReadable = false;
            bool prevReadable = false;

            if (importer != null)
            {
                prevReadable = importer.isReadable;
                if (!prevReadable)
                {
                    importer.isReadable = true;
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                    changedReadable = true;
                }
            }

            // Crop to sprite rect
            Rect r = sprite.rect;
            int w = (int)r.width;
            int h = (int)r.height;
            int x = (int)r.x;
            int y = (int)r.y;

            Texture2D tmp = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color[] pixels = tex.GetPixels(x, y, w, h);
            tmp.SetPixels(pixels);
            tmp.Apply();

            byte[] png = tmp.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tmp);

            string safeBase = MakeSafe(baseName);
            string fileName = safeBase + ".png";
            string outPath = Path.Combine(iconsDir, fileName);
            File.WriteAllBytes(outPath, png);

            // restore readable flag
            if (changedReadable && importer != null)
            {
                importer.isReadable = prevReadable;
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            }

            // Return relative path used in CSV (icons/file.png)
            return Path.Combine(IconsFolderName, fileName).Replace("\\", "/");
        }
        catch (Exception e)
        {
            Debug.LogWarning("TinySeaSpeciesExport: Could not export icon: " + e.Message);
            return "";
        }
    }

    private static string Csv(string s)
    {
        if (String.IsNullOrEmpty(s)) return "";
        return "\"" + s.Replace("\"", "\"\"") + "\"";
    }

    private static string MakeSafe(string name)
    {
        if (String.IsNullOrEmpty(name)) return "";
        char[] invalid = Path.GetInvalidFileNameChars();
        StringBuilder sb = new StringBuilder(name.Length);
        for (int i = 0; i < name.Length; i++)
        {
            char ch = name[i];
            if (Array.IndexOf<char>(invalid, ch) >= 0) sb.Append('_');
            else sb.Append(ch);
        }
        return sb.ToString();
    }

    private static string Pad2(int i)
    {
        if (i < 0) return "-" + Pad2(-i);
        if (i < 10) return "0" + i.ToString();
        return i.ToString();
    }
}
#endif
