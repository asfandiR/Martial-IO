using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ContentAssetGenerator
{
    private const string IconRootFolder = "Assets/Fantasy RPG Icons Pack";
    private const string EnemyPrefabFolder = "Assets/Pixel Monsters Vol2/Prefabs";
    private const string EnemyDataFolder = "Assets/ScriptableObjects/Enemies";
    private const string EnemyDatabasePath = "Assets/ScriptableObjects/Enemy Database.asset";
    private const string AbilityDataFolder = "Assets/ScriptableObjects/Abilities";
    private const string AbilityIconRootFolder = "Assets/Fantasy RPG Icons Pack";
    private const string DefaultProjectilePrefabPath = "Assets/Prefabs/P_Projectile.prefab";
    private const string RelicDataFolder = "Assets/Resources/Relics";
    private const string EffectorDataFolder = "Assets/Resources/Effectors";

    private const int EnemiesPerDifficultyStep = 3;
    private const float BaseHp = 10f;
    private const float HpPerIndex = 2f;
    private const float BaseSpeed = 2f;
    private const float SpeedPerIndex = 0.05f;
    private const float BaseDamage = 1f;
    private const float DamagePerIndex = 0.2f;

    private static readonly string[] ThematicFolderKeywords =
    {
        "armor", "axe", "belt", "blacksmith", "bones", "boot", "bow",
        "crushing weapon", "dagger", "demon loot", "dwarf loot", "fairy loot",
        "goblin loot", "magical artifacts", "greaves", "helmet", "hunting",
        "jewelry", "lance", "loot", "pirated", "runes", "scrolls", "shield",
        "staff", "swords", "undead loot", "treasure chests"
    };

    private static readonly string[] EffectorPackFolders =
    {
        "Alchemical Herbs Icons",
        "Alchemical Potions Icons",
        "Berries and seeds icons",
        "RPG Cooking ingredients icons",
        "RPG Elixir icons",
        "RPG Medieval food icons",
        "RPG Mushrooms icons",
        "RPG Runes Icons"
    };

    [MenuItem("Tools/Content/Generate")]
    public static void Generate()
    {
        EnsureFolder(EnemyDataFolder);
        EnsureFolder(AbilityDataFolder);
        EnsureFolder(RelicDataFolder);
        EnsureFolder(EffectorDataFolder);

        GenerateEnemies();
        GenerateAbilities();
        GenerateRelics();
        GenerateEffectors();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[ContentAssetGenerator] Generate completed.");
    }

    [MenuItem("Tools/Content/Update")]
    public static void Update()
    {
        CleanupAbilitiesToSkillIconsOnly();
        CleanupRelicsToThematicIconsOnly();
        CleanupRelicsFromEffectorPacks();
        Generate();
        Debug.Log("[ContentAssetGenerator] Update completed.");
    }

    private static void GenerateEnemies()
    {
        var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { EnemyPrefabFolder });
        var prefabs = new List<GameObject>();
        for (int i = 0; i < prefabGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null || !prefab.name.StartsWith("monster"))
                continue;
            prefabs.Add(prefab);
        }

        prefabs = prefabs.OrderBy(p => p.name).ToList();

        var enemyDatabase = AssetDatabase.LoadAssetAtPath<EnemyDatabase>(EnemyDatabasePath);
        if (enemyDatabase == null)
        {
            enemyDatabase = ScriptableObject.CreateInstance<EnemyDatabase>();
            AssetDatabase.CreateAsset(enemyDatabase, EnemyDatabasePath);
        }

        enemyDatabase.enemies.Clear();

        for (int i = 0; i < prefabs.Count; i++)
        {
            var prefab = prefabs[i];
            string assetPath = Path.Combine(EnemyDataFolder, prefab.name + ".asset").Replace("\\", "/");
            var data = AssetDatabase.LoadAssetAtPath<EnemyData>(assetPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<EnemyData>();
                AssetDatabase.CreateAsset(data, assetPath);
            }

            data.enemyId = prefab.name;
            data.prefab = prefab;
            data.baseHp = BaseHp + HpPerIndex * i;
            data.baseSpeed = BaseSpeed + SpeedPerIndex * i;
            data.baseDamage = BaseDamage + DamagePerIndex * i;
            data.weight = 1;
            data.minDifficultyStep = Mathf.Max(0, EnemiesPerDifficultyStep > 0 ? i / EnemiesPerDifficultyStep : 0);

            EditorUtility.SetDirty(data);
            enemyDatabase.enemies.Add(data);
        }

        EditorUtility.SetDirty(enemyDatabase);
    }

    private static void GenerateAbilities()
    {
        List<string> iconFolders = GetAbilityIconFolders();
        if (iconFolders.Count == 0)
            return;

        string[] spriteGuids = AssetDatabase.FindAssets("t:Sprite", iconFolders.ToArray());
        var entries = new List<AbilitySpriteEntry>(spriteGuids.Length);
        for (int i = 0; i < spriteGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(spriteGuids[i]);
            if (IsExplicitWithoutBackground(path))
                continue;

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                continue;

            entries.Add(new AbilitySpriteEntry
            {
                sprite = sprite,
                sourceFolder = ResolveSourceFolderName(path, iconFolders),
                assetPath = path
            });
        }

        var hasBackgroundByFolder = entries
            .GroupBy(e => e.sourceFolder)
            .ToDictionary(g => g.Key, g => g.Any(e => IsExplicitBackground(e.assetPath)));

        entries = entries
            .Where(e => !hasBackgroundByFolder[e.sourceFolder] || IsExplicitBackground(e.assetPath))
            .OrderBy(e => e.sprite.name)
            .ThenBy(e => e.assetPath)
            .ToList();

        if (entries.Count == 0)
            return;

        var projectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultProjectilePrefabPath);
        var usedNames = new HashSet<string>();
        int quarter = Mathf.Max(1, entries.Count / 4);

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            string assetName = BuildUniqueAbilityAssetName(entry.sourceFolder, entry.sprite.name, usedNames);
            string assetPath = Path.Combine(AbilityDataFolder, assetName + ".asset").Replace("\\", "/");
            var data = AssetDatabase.LoadAssetAtPath<AbilityData>(assetPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<AbilityData>();
                AssetDatabase.CreateAsset(data, assetPath);
            }

            data.icon = entry.sprite;
            data.projectilePrefab = projectilePrefab;
            data.rarity = IndexToRarity(i, quarter);
            data.abilityName = BuildAbilityName(entry.sourceFolder, data.rarity);
            EditorUtility.SetDirty(data);
        }
    }

    private static void GenerateRelics()
    {
        List<string> iconFolders = GetRelicIconFolders();
        if (iconFolders.Count == 0)
            return;

        string[] spriteGuids = AssetDatabase.FindAssets("t:Sprite", iconFolders.ToArray());
        var spriteEntries = new List<SpriteEntry>(spriteGuids.Length);
        for (int i = 0; i < spriteGuids.Length; i++)
        {
            string spritePath = AssetDatabase.GUIDToAssetPath(spriteGuids[i]);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (sprite == null)
                continue;

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

        var usedNames = new HashSet<string>();
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
            relic.relicName = BuildPrettyName(entry.sourceFolderName, "Relic");
            relic.icon = entry.sprite;
            ApplyDefaultRelicStats(relic);
            EditorUtility.SetDirty(relic);
        }
    }

    private static void GenerateEffectors()
    {
        List<string> iconFolders = GetEffectorIconFolders();
        if (iconFolders.Count == 0)
            return;

        string[] spriteGuids = AssetDatabase.FindAssets("t:Sprite", iconFolders.ToArray());
        var spriteEntries = new List<SpriteEntry>(spriteGuids.Length);
        for (int i = 0; i < spriteGuids.Length; i++)
        {
            string spritePath = AssetDatabase.GUIDToAssetPath(spriteGuids[i]);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (sprite == null)
                continue;

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

        var usedNames = new HashSet<string>();
        for (int i = 0; i < spriteEntries.Count; i++)
        {
            var entry = spriteEntries[i];
            string assetName = BuildUniqueName(entry.sourceFolderName, entry.sprite.name, usedNames);
            string assetPath = Path.Combine(EffectorDataFolder, assetName + ".asset").Replace("\\", "/");

            var effector = AssetDatabase.LoadAssetAtPath<EffectorSO>(assetPath);
            if (effector == null)
            {
                effector = ScriptableObject.CreateInstance<EffectorSO>();
                AssetDatabase.CreateAsset(effector, assetPath);
            }

            effector.name = assetName;
            effector.effectorName = BuildPrettyName(entry.sourceFolderName, "Effector");
            effector.icon = entry.sprite;
            ApplyDefaultEffectorStats(effector, assetName);
            EditorUtility.SetDirty(effector);
        }
    }

    private static void CleanupAbilitiesToSkillIconsOnly()
    {
        if (!AssetDatabase.IsValidFolder(AbilityDataFolder))
            return;

        string[] abilityGuids = AssetDatabase.FindAssets("t:AbilityData", new[] { AbilityDataFolder });
        for (int i = 0; i < abilityGuids.Length; i++)
        {
            string abilityPath = AssetDatabase.GUIDToAssetPath(abilityGuids[i]);
            var ability = AssetDatabase.LoadAssetAtPath<AbilityData>(abilityPath);
            if (ability == null || ability.icon == null)
                continue;

            string iconPath = AssetDatabase.GetAssetPath(ability.icon).Replace("\\", "/").ToLowerInvariant();
            if (iconPath.Contains("skill"))
                continue;

            AssetDatabase.DeleteAsset(abilityPath);
        }
    }

    private static void CleanupRelicsToThematicIconsOnly()
    {
        if (!AssetDatabase.IsValidFolder(RelicDataFolder))
            return;

        string[] relicGuids = AssetDatabase.FindAssets("t:RelicData", new[] { RelicDataFolder });
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
            bool fromWithoutBackground = IsFromWithoutBackground(iconPath);
            bool excludedFolder = IsEffectorPack(folderName);
            if (validTheme && noSkill && fromWithoutBackground && !excludedFolder)
                continue;

            AssetDatabase.DeleteAsset(relicPath);
        }
    }

    private static void CleanupRelicsFromEffectorPacks()
    {
        if (!AssetDatabase.IsValidFolder(RelicDataFolder))
            return;

        string[] relicGuids = AssetDatabase.FindAssets("t:RelicData", new[] { RelicDataFolder });
        for (int i = 0; i < relicGuids.Length; i++)
        {
            string relicPath = AssetDatabase.GUIDToAssetPath(relicGuids[i]);
            var relic = AssetDatabase.LoadAssetAtPath<RelicData>(relicPath);
            if (relic == null || relic.icon == null)
                continue;

            string iconPath = AssetDatabase.GetAssetPath(relic.icon).Replace("\\", "/");
            string folderName = GetPackFolderNameFromPath(iconPath);
            if (!IsEffectorPack(folderName))
                continue;

            AssetDatabase.DeleteAsset(relicPath);
        }
    }

    private static List<string> GetAbilityIconFolders()
    {
        var folders = new List<string>();
        if (!AssetDatabase.IsValidFolder(AbilityIconRootFolder))
            return folders;

        string[] subFolders = AssetDatabase.GetSubFolders(AbilityIconRootFolder);
        for (int i = 0; i < subFolders.Length; i++)
        {
            string folderPath = subFolders[i];
            string folderName = Path.GetFileName(folderPath).ToLowerInvariant();
            bool isSkillFolder = folderName.Contains("skill icons") || folderName.Contains("skills icons");
            bool isExcluded = folderName.Contains("people") || folderName.Contains("avatar");
            if (isSkillFolder && !isExcluded)
                folders.Add(folderPath);
        }

        return folders;
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
            if (IsEffectorPack(folderName))
                continue;
            if (!IsThematicRelicFolder(folderName))
                continue;

            string iconFolder = (packFolder + "/PNG/without background").Replace("\\", "/");
            if (AssetDatabase.IsValidFolder(iconFolder))
                folders.Add(iconFolder);
        }

        return folders;
    }

    private static List<string> GetEffectorIconFolders()
    {
        var folders = new List<string>();
        for (int i = 0; i < EffectorPackFolders.Length; i++)
        {
            string packFolder = (IconRootFolder + "/" + EffectorPackFolders[i]).Replace("\\", "/");
            if (!AssetDatabase.IsValidFolder(packFolder))
                continue;

            string iconFolder = FindWithoutBackgroundFolder(packFolder);
            if (!string.IsNullOrWhiteSpace(iconFolder))
                folders.Add(iconFolder);
        }

        return folders;
    }

    private static bool IsExplicitBackground(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            return false;

        string p = assetPath.Replace("\\", "/").ToLowerInvariant();
        return p.Contains("/background/");
    }

    private static bool IsExplicitWithoutBackground(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            return false;

        string p = assetPath.Replace("\\", "/").ToLowerInvariant();
        return p.Contains("/without background/");
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

    private static bool IsEffectorPack(string folderName)
    {
        if (string.IsNullOrWhiteSpace(folderName))
            return false;

        for (int i = 0; i < EffectorPackFolders.Length; i++)
        {
            if (string.Equals(folderName, EffectorPackFolders[i], System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool IsFromWithoutBackground(string iconPath)
    {
        string normalized = iconPath.Replace("\\", "/").ToLowerInvariant();
        return normalized.Contains("/png/without background/")
               || normalized.Contains("/png/withoutbackground/")
               || normalized.Contains("/png/without_back_ground/");
    }

    private static void ApplyDefaultRelicStats(RelicData relic)
    {
        int statCount = System.Enum.GetValues(typeof(RelicData.RelicStatType)).Length;
        int randomIndex = Random.Range(0, Mathf.Max(1, statCount));
        relic.boostedStat = (RelicData.RelicStatType)randomIndex;
    }

    private static void ApplyDefaultEffectorStats(EffectorSO effector, string stableSeed)
    {
        int seed = GetStableHash(stableSeed);
        var random = new System.Random(seed);
        int statCount = System.Enum.GetValues(typeof(EffectorSO.BoostedStat)).Length;
        effector.boostedStat = (EffectorSO.BoostedStat)random.Next(0, Mathf.Max(1, statCount));
        effector.rarity = RollRarity(random.NextDouble());
        if (effector.boostPercent <= 0f)
            effector.boostPercent = 10f;
    }

    private static EffectorSO.EffectorRarity RollRarity(double roll)
    {
        if (roll < 0.60d) return EffectorSO.EffectorRarity.Common;
        if (roll < 0.85d) return EffectorSO.EffectorRarity.Rare;
        if (roll < 0.97d) return EffectorSO.EffectorRarity.Epic;
        return EffectorSO.EffectorRarity.Legendary;
    }

    private static AbilityData.AbilityRarity IndexToRarity(int index, int quarter)
    {
        if (index < quarter) return AbilityData.AbilityRarity.Common;
        if (index < quarter * 2) return AbilityData.AbilityRarity.Rare;
        if (index < quarter * 3) return AbilityData.AbilityRarity.Epic;
        return AbilityData.AbilityRarity.Legendary;
    }

    private static string BuildAbilityName(string folderName, AbilityData.AbilityRarity rarity)
    {
        return $"{BuildPrettyName(folderName, "Ability")} {rarity}";
    }

    private static string BuildPrettyName(string folderName, string fallback)
    {
        string pretty = folderName
            .Replace("RPG ", "")
            .Replace(" Icons", "")
            .Replace(" icons", "")
            .Trim();

        if (string.IsNullOrWhiteSpace(pretty))
            pretty = fallback;

        return pretty;
    }

    private static string ResolveSourceFolderName(string spritePath, List<string> iconFolders)
    {
        if (string.IsNullOrWhiteSpace(spritePath))
            return "Ability";

        string normalized = spritePath.Replace("\\", "/");
        for (int i = 0; i < iconFolders.Count; i++)
        {
            string folder = iconFolders[i].Replace("\\", "/");
            if (normalized.StartsWith(folder + "/"))
                return Path.GetFileName(folder);
        }

        return Path.GetFileName(Path.GetDirectoryName(normalized));
    }

    private static string GetPackFolderNameFromPath(string spritePath)
    {
        string normalized = spritePath.Replace("\\", "/");
        string[] parts = normalized.Split('/');
        int idx = System.Array.IndexOf(parts, "Fantasy RPG Icons Pack");
        if (idx >= 0 && idx + 1 < parts.Length)
            return parts[idx + 1];

        return "Content";
    }

    private static string FindWithoutBackgroundFolder(string packFolder)
    {
        string pngRoot = (packFolder + "/PNG").Replace("\\", "/");
        if (!AssetDatabase.IsValidFolder(pngRoot))
            return string.Empty;

        string[] subFolders = AssetDatabase.GetSubFolders(pngRoot);
        for (int i = 0; i < subFolders.Length; i++)
        {
            string candidate = subFolders[i].Replace("\\", "/");
            string leaf = Path.GetFileName(candidate);
            if (string.IsNullOrWhiteSpace(leaf))
                continue;

            string normalized = leaf.Replace(" ", "").Replace("_", "").ToLowerInvariant();
            if (normalized == "withoutbackground")
                return candidate;
        }

        return string.Empty;
    }

    private static string BuildUniqueAbilityAssetName(string folderName, string spriteName, HashSet<string> used)
    {
        string baseName = $"{Sanitize(folderName)}_{Sanitize(spriteName)}";
        string finalName = baseName;
        int suffix = 1;
        while (used.Contains(finalName))
        {
            finalName = $"{baseName}_{suffix}";
            suffix++;
        }

        used.Add(finalName);
        return finalName;
    }

    private static string BuildUniqueName(string folderName, string spriteName, HashSet<string> usedNames)
    {
        string baseName = $"{Sanitize(folderName)}_{Sanitize(spriteName)}";
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

    private static int GetStableHash(string value)
    {
        if (string.IsNullOrEmpty(value))
            return 0;

        unchecked
        {
            int hash = 23;
            for (int i = 0; i < value.Length; i++)
                hash = (hash * 31) + value[i];
            return hash;
        }
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

    private class AbilitySpriteEntry
    {
        public Sprite sprite;
        public string sourceFolder;
        public string assetPath;
    }
}
