using UnityEngine;
using System.Collections.Generic;

// Handles SFX and music.
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private GameAudioLibrarySO sfxLibrary;
    [SerializeField] private float masterSfxVolume = 1f;
    [SerializeField] private float masterMusicVolume = 1f;

    private readonly Dictionary<GameSfxId, float> sfxLastPlayTime = new Dictionary<GameSfxId, float>(32);
    private readonly Dictionary<GameSfxId, int> sfxLastClipIndex = new Dictionary<GameSfxId, int>(32);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
        }

        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
        }

        if (sfxLibrary == null)
            sfxLibrary = Resources.Load<GameAudioLibrarySO>("Audio/GameAudioLibrary");
    }

    public void PlaySfx(AudioClip clip, float volume = 1f)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume) * masterSfxVolume);
    }

    public bool PlaySfx(GameSfxId id, float volumeScale = 1f, bool ignoreInterval = false)
    {
        if (id == GameSfxId.None) return false;
        if (sfxLibrary == null) return false;
        if (!sfxLibrary.TryGetSfxGroup(id, out var group)) return false;

        if (!ignoreInterval && group.minInterval > 0f)
        {
            float now = Time.unscaledTime;
            if (sfxLastPlayTime.TryGetValue(id, out float lastTime) && (now - lastTime) < group.minInterval)
                return false;

            sfxLastPlayTime[id] = now;
        }

        AudioClip clip = PickClip(id, group.clips);
        if (clip == null) return false;

        float volume = Mathf.Clamp01(group.baseVolume) * Mathf.Clamp01(volumeScale);
        PlaySfx(clip, volume);
        return true;
    }

    public void SetSfxLibrary(GameAudioLibrarySO library)
    {
        sfxLibrary = library;
    }

    public void PlayMusic(AudioClip clip, bool loop = true)
    {
        if (musicSource == null) return;
        if (clip == null)
        {
            musicSource.Stop();
            musicSource.clip = null;
            return;
        }

        musicSource.loop = loop;
        musicSource.clip = clip;
        musicSource.volume = masterMusicVolume;
        musicSource.Play();
    }

    public void SetMasterSfxVolume(float volume)
    {
        masterSfxVolume = Mathf.Clamp01(volume);
    }

    public void SetMasterMusicVolume(float volume)
    {
        masterMusicVolume = Mathf.Clamp01(volume);
        if (musicSource != null)
            musicSource.volume = masterMusicVolume;
    }

    private AudioClip PickClip(GameSfxId id, AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0)
            return null;

        if (clips.Length == 1)
            return clips[0];

        int lastIndex = -1;
        sfxLastClipIndex.TryGetValue(id, out lastIndex);

        int nextIndex = Random.Range(0, clips.Length);
        if (clips.Length > 1 && nextIndex == lastIndex)
            nextIndex = (nextIndex + 1) % clips.Length;

        sfxLastClipIndex[id] = nextIndex;
        return clips[nextIndex];
    }
}
