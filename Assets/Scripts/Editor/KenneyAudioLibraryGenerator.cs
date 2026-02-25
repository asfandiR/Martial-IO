using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class KenneyAudioLibraryGenerator
{
    private const string SourceFolder = "Assets/kenney_rpg-audio/Audio";
    private const string OutputFolder = "Assets/Resources/Audio";
    private const string OutputAssetPath = OutputFolder + "/GameAudioLibrary.asset";

    [MenuItem("Tools/Audio/Generate Kenney RPG Audio Library")]
    public static void Generate()
    {
        if (!AssetDatabase.IsValidFolder(SourceFolder))
        {
            Debug.LogError("Audio source folder not found: " + SourceFolder);
            return;
        }

        EnsureFolder("Assets/Resources");
        EnsureFolder(OutputFolder);

        GameAudioLibrarySO library = AssetDatabase.LoadAssetAtPath<GameAudioLibrarySO>(OutputAssetPath);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<GameAudioLibrarySO>();
            AssetDatabase.CreateAsset(library, OutputAssetPath);
        }

        var filesByName = LoadAudioClipsByFileName(SourceFolder);
        var groups = BuildGroups(filesByName);

        library.SetSfxGroups(groups);
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(BuildSummary(groups), library);
    }

    private static Dictionary<string, AudioClip> LoadAudioClipsByFileName(string folder)
    {
        var result = new Dictionary<string, AudioClip>(System.StringComparer.OrdinalIgnoreCase);
        string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { folder });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (!path.EndsWith(".ogg", System.StringComparison.OrdinalIgnoreCase))
                continue;

            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
                continue;

            string fileName = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(fileName))
                result[fileName] = clip;
        }

        return result;
    }

    private static List<GameAudioLibrarySO.SfxGroup> BuildGroups(Dictionary<string, AudioClip> files)
    {
        var groups = new List<GameAudioLibrarySO.SfxGroup>(16)
        {
            MakeGroup(GameSfxId.UiClick, 0.85f, 0.03f, files, "metalClick.ogg", "metalLatch.ogg"),
            MakeGroup(GameSfxId.UiOpen, 0.9f, 0f, files, "bookOpen.ogg", "doorOpen_1.ogg", "doorOpen_2.ogg"),
            MakeGroup(GameSfxId.UiClose, 0.85f, 0f, files, "bookClose.ogg", "doorClose_1.ogg", "doorClose_2.ogg", "doorClose_3.ogg", "doorClose_4.ogg"),
            MakeGroup(GameSfxId.LevelUpOpen, 1f, 0f, files, "bookOpen.ogg", "bookFlip1.ogg", "bookFlip2.ogg", "bookFlip3.ogg"),
            MakeGroup(GameSfxId.LevelUpSelect, 1f, 0f, files, "bookPlace1.ogg", "bookPlace2.ogg", "bookPlace3.ogg", "metalClick.ogg"),

            MakeGroup(GameSfxId.PickupXp, 0.8f, 0.02f, files, "handleCoins.ogg", "handleCoins2.ogg"),
            MakeGroup(GameSfxId.PickupRelic, 0.9f, 0.03f, files, "bookPlace1.ogg", "bookPlace2.ogg", "bookPlace3.ogg", "handleSmallLeather.ogg", "handleSmallLeather2.ogg"),
            MakeGroup(GameSfxId.PickupEffector, 0.85f, 0.03f, files, "cloth1.ogg", "cloth2.ogg", "cloth3.ogg", "cloth4.ogg", "clothBelt.ogg", "clothBelt2.ogg", "beltHandle1.ogg", "beltHandle2.ogg"),
            MakeGroup(GameSfxId.Footstep, 0.7f, 0.04f, files, PrefixList("footstep", 10)),

            MakeGroup(GameSfxId.ProjectileFire, 0.8f, 0.04f, files, "drawKnife1.ogg", "drawKnife2.ogg", "drawKnife3.ogg", "knifeSlice.ogg", "knifeSlice2.ogg"),
            MakeGroup(GameSfxId.SwordHit, 0.9f, 0.05f, files, "knifeSlice.ogg", "knifeSlice2.ogg", "chop.ogg"),
            MakeGroup(GameSfxId.EnemyAttack, 0.75f, 0.04f, files, "chop.ogg", "knifeSlice.ogg"),
            MakeGroup(GameSfxId.EnemyHit, 0.75f, 0.03f, files, "chop.ogg", "cloth1.ogg", "cloth2.ogg", "cloth3.ogg", "cloth4.ogg"),
            MakeGroup(GameSfxId.EnemyDeath, 0.9f, 0.02f, files, "dropLeather.ogg", "metalPot1.ogg", "metalPot2.ogg", "metalPot3.ogg"),
            MakeGroup(GameSfxId.PlayerHit, 0.8f, 0.08f, files, "cloth1.ogg", "cloth2.ogg", "cloth3.ogg", "cloth4.ogg", "clothBelt.ogg"),
            MakeGroup(GameSfxId.PlayerDeath, 1f, 0f, files, "dropLeather.ogg", "clothBelt.ogg", "clothBelt2.ogg", "metalPot2.ogg")
        };

        groups.RemoveAll(g => g == null || g.clips == null || g.clips.Length == 0);
        return groups;
    }

    private static GameAudioLibrarySO.SfxGroup MakeGroup(
        GameSfxId id,
        float volume,
        float minInterval,
        Dictionary<string, AudioClip> files,
        params string[] fileNames)
    {
        var clips = new List<AudioClip>(fileNames.Length);
        for (int i = 0; i < fileNames.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(fileNames[i]))
                continue;

            if (files.TryGetValue(fileNames[i], out AudioClip clip) && clip != null)
                clips.Add(clip);
        }

        return new GameAudioLibrarySO.SfxGroup
        {
            id = id,
            baseVolume = Mathf.Clamp01(volume),
            minInterval = Mathf.Max(0f, minInterval),
            clips = clips.ToArray()
        };
    }

    private static string[] PrefixList(string prefix, int count)
    {
        var names = new string[count];
        for (int i = 0; i < count; i++)
            names[i] = prefix + i.ToString("00") + ".ogg";
        return names;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = Path.GetDirectoryName(path).Replace("\\", "/");
        string name = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, name);
    }

    private static string BuildSummary(List<GameAudioLibrarySO.SfxGroup> groups)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder(512);
        sb.AppendLine("Generated Kenney GameAudioLibrary:");
        for (int i = 0; i < groups.Count; i++)
        {
            var g = groups[i];
            if (g == null) continue;
            int clipCount = g.clips != null ? g.clips.Length : 0;
            sb.Append("- ").Append(g.id).Append(": ").Append(clipCount).AppendLine(" clip(s)");
        }

        return sb.ToString();
    }
}
