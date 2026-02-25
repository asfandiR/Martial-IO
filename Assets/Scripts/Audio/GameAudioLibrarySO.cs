using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Audio/Game Audio Library", fileName = "GameAudioLibrary")]
public class GameAudioLibrarySO : ScriptableObject
{
    [Serializable]
    public class SfxGroup
    {
        public GameSfxId id = GameSfxId.None;
        [Range(0f, 1f)] public float baseVolume = 1f;
        [Min(0f)] public float minInterval = 0f;
        public AudioClip[] clips = new AudioClip[0];

        public bool HasClips => clips != null && clips.Length > 0;
    }

    [SerializeField] private List<SfxGroup> sfxGroups = new List<SfxGroup>();

    private Dictionary<GameSfxId, SfxGroup> cache;

    public IReadOnlyList<SfxGroup> SfxGroups => sfxGroups;

    public bool TryGetSfxGroup(GameSfxId id, out SfxGroup group)
    {
        if (cache == null)
            RebuildCache();

        return cache.TryGetValue(id, out group) && group != null && group.HasClips;
    }

    public void SetSfxGroups(List<SfxGroup> groups)
    {
        sfxGroups = groups ?? new List<SfxGroup>();
        cache = null;
    }

    private void OnValidate()
    {
        cache = null;
    }

    private void OnEnable()
    {
        cache = null;
    }

    private void RebuildCache()
    {
        cache = new Dictionary<GameSfxId, SfxGroup>();
        if (sfxGroups == null)
            return;

        for (int i = 0; i < sfxGroups.Count; i++)
        {
            SfxGroup group = sfxGroups[i];
            if (group == null || !group.HasClips || group.id == GameSfxId.None)
                continue;

            cache[group.id] = group;
        }
    }
}
