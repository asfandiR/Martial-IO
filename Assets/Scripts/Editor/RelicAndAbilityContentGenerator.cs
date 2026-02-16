using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class RelicAndAbilityContentGenerator
{
    private const string IconRootFolder = "Assets/Fantasy RPG Icons Pack";
    private const string AbilityDataFolder = "Assets/ScriptableObjects/Abilities";
    private const string RelicDataFolder = "Assets/Resources/Relics";
    private static readonly string[] ThematicFolderKeywords =
    {
        "armor", "axe", "belt", "blacksmith", "bones", "boot", "bow",
        "crushing weapon", "dagger", "demon loot", "dwarf loot", "fairy loot",
        "goblin loot", "magical artifacts", "greaves", "helmet", "hunting",
        "jewelry", "lance", "loot", "pirated", "runes", "scrolls", "shield",
        "staff", "swords", "undead loot", "treasure chests"
    };

    [MenuItem("Tools/Content/Generate Relics (Non-Skill/Without Background)")]
    public static void GenerateRelics()
    {
        EnsureFolder(RelicDataFolder);

        List<string> iconFolders = GetRelicIconFolders();
        if (iconFolders.Count == 0)
        {
            Debug.LogWarning("[RelicGenerator] No valid non-skill icon folders found.");
            return;
        }

        string[] spriteGuids = AssetDatabase.FindAssets("t:Sprite", iconFolders.ToArray());
        var spriteEntries = new List<SpriteEntry>(spriteGuids.Length);

        for (int i = 0; i < spriteGuids.Length; i++)
        {
            string spritePath = AssetDatabase.GUIDToAssetPath(spriteGuids[i]);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (sprite == null) continue;

            spriteEntries.Add(new SpriteEntry
            {
                sprite = sprite,
                sourceFolderName = GetPackFolderNameFromPath(spritePath)
            });
        }

        spriteEntries = spriteEntries
            .OrderBy(e => e.sourceFolderName)
            .ThenBy(e => e.sprite.name)
            .ToList();

        if (spriteEntries.Count == 0)
        {
            Debug.LogWarning("[RelicGenerator] No sprites found.");
            return;
        }

        var usedNames = new HashSet<string>();
        int generatedCount = 0;
        for (int i = 0; i < spriteEntries.Count; i++)
        {
            var entry = spriteEntries[i];
            string assetName = BuildUniqueName(entry.sourceFolderName, entry.sprite.name, usedNames);
            string assetPath = Path.Combine(RelicDataFolder, assetName + ".asset").Replace("\\", "/");

            var relic = AssetDatabase.LoadAssetAtPath<RelicData>(assetPath);
            if (relic == null)
            {
                relic = ScriptableObject.CreateInstance<RelicData>();
                AssetDatabase.CreateAsset(relic, assetPath);
            }

            relic.name = assetName;
            relic.relicName = BuildRelicName(entry.sourceFolderName);
            relic.icon = entry.sprite;
            ApplyDefaultStats(relic, i);
            EditorUtility.SetDirty(relic);
            generatedCount++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[RelicGenerator] Generated relic assets: {generatedCount}.");
    }

    [MenuItem("Tools/Content/Cleanup Abilities (Skill Icons Only)")]
    public static void CleanupAbilitiesToSkillIconsOnly()
    {
        if (!AssetDatabase.IsValidFolder(AbilityDataFolder))
        {
            Debug.LogWarning($"[AbilityCleanup] Folder not found: {AbilityDataFolder}");
            return;
        }

        string[] abilityGuids = AssetDatabase.FindAssets("t:AbilityData", new[] { AbilityDataFolder });
        int removed = 0;

        for (int i = 0; i < abilityGuids.Length; i++)
        {
            string abilityPath = AssetDatabase.GUIDToAssetPath(abilityGuids[i]);
            var ability = AssetDatabase.LoadAssetAtPath<AbilityData>(abilityPath);
            if (ability == null) continue;

            if (ability.icon == null)
                continue;

            string iconPath = AssetDatabase.GetAssetPath(ability.icon).Replace("\\", "/").ToLowerInvariant();
            bool isSkillIcon = iconPath.Contains("skill");
            if (isSkillIcon)
                continue;

            if (AssetDatabase.DeleteAsset(abilityPath))
                removed++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[AbilityCleanup] Removed non-skill abilities: {removed}");
    }

    [MenuItem("Tools/Content/Generate Relics + Cleanup Abilities")]
    public static void GenerateRelicsAndCleanupAbilities()
    {
        CleanupRelicsToThematicIconsOnly();
        GenerateRelics();
        CleanupAbilitiesToSkillIconsOnly();
    }

    [MenuItem("Tools/Content/Cleanup Relics (Thematic Icons Only)")]
    public static void CleanupRelicsToThematicIconsOnly()
    {
        if (!AssetDatabase.IsValidFolder(RelicDataFolder))
            return;

        string[] relicGuids = AssetDatabase.FindAssets("t:RelicData", new[] { RelicDataFolder });
        int removed = 0;

        for (int i = 0; i < relicGuids.Length; i++)
        {
            string relicPath = AssetDatabase.GUIDToAssetPath(relicGuids[i]);
            var relic = AssetDatabase.LoadAssetAtPath<RelicData>(relicPath);
            if (relic == null || relic.icon == null)
                continue;

            string iconPath = AssetDatabase.GetAssetPath(relic.icon).Replace("\\", "/");
            string folderName = GetPackFolderNameFromPath(iconPath);
            bool validTheme = IsThematicRelicFolder(folderName);
            bool noSkill = folderName.IndexOf("skill", System.StringComparison.OrdinalIgnoreCase) < 0;
            bool fromWithoutBackground = iconPath.Replace("\\", "/").ToLowerInvariant().Contains("/png/without background/");
            if (validTheme && noSkill && fromWithoutBackground)
                continue;

            if (AssetDatabase.DeleteAsset(relicPath))
                removed++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[RelicCleanup] Removed non-thematic relic assets: {removed}");
    }

    private static void ApplyDefaultStats(RelicData relic, int index)
    {
        int statCount = System.Enum.GetValues(typeof(RelicData.RelicStatType)).Length;
        int randomIndex = Random.Range(0, Mathf.Max(1, statCount));
        relic.boostedStat = (RelicData.RelicStatType)randomIndex;
    }

    private static List<string> GetRelicIconFolders()
    {
        var folders = new List<string>();
        if (!AssetDatabase.IsValidFolder(IconRootFolder))
            return folders;

        string[] packFolders = AssetDatabase.GetSubFolders(IconRootFolder);
        for (int i = 0; i < packFolders.Length; i++)
        {
            string packFolder = packFolders[i];
            string folderName = Path.GetFileName(packFolder).ToLowerInvariant();
            if (folderName.Contains("skill"))
                continue;
            if (!IsThematicRelicFolder(folderName))
                continue;

            string iconFolder = (packFolder + "/PNG/without background").Replace("\\", "/");
            if (AssetDatabase.IsValidFolder(iconFolder))
                folders.Add(iconFolder);
        }

        return folders;
    }

    private static string GetPackFolderNameFromPath(string spritePath)
    {
        string normalized = spritePath.Replace("\\", "/");
        string[] parts = normalized.Split('/');
        int idx = System.Array.IndexOf(parts, "Fantasy RPG Icons Pack");
        if (idx >= 0 && idx + 1 < parts.Length)
            return parts[idx + 1];

        return "Relic";
    }

    private static string BuildRelicName(string folderName)
    {
        string pretty = folderName
            .Replace("RPG ", "")
            .Replace(" Icons", "")
            .Replace(" icons", "")
            .Trim();

        if (string.IsNullOrWhiteSpace(pretty))
            pretty = "Relic";

        return pretty;
    }

    private static string BuildUniqueName(string folderName, string spriteName, HashSet<string> usedNames)
    {
        string baseName = Sanitize(folderName) + "_" + Sanitize(spriteName);
        string finalName = baseName;
        int suffix = 1;
        while (usedNames.Contains(finalName))
        {
            finalName = $"{baseName}_{suffix}";
            suffix++;
        }

        usedNames.Add(finalName);
        return finalName;
    }

    private static string Sanitize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "item";

        char[] chars = value.Where(char.IsLetterOrDigit).ToArray();
        return chars.Length == 0 ? "item" : new string(chars);
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
            return;

        string parent = Path.GetDirectoryName(folder).Replace("\\", "/");
        string leaf = Path.GetFileName(folder);
        if (!AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, leaf);
    }

    private struct SpriteEntry
    {
        public Sprite sprite;
        public string sourceFolderName;
    }

    private static bool IsThematicRelicFolder(string folderName)
    {
        if (string.IsNullOrWhiteSpace(folderName))
            return false;

        string normalized = folderName.ToLowerInvariant();
        for (int i = 0; i < ThematicFolderKeywords.Length; i++)
        {
            if (normalized.Contains(ThematicFolderKeywords[i]))
                return true;
        }

        return false;
    }
}
