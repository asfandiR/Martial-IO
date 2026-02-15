using UnityEngine;

// Handles SFX and music.
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private float masterSfxVolume = 1f;
    [SerializeField] private float masterMusicVolume = 1f;

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
    }

    public void PlaySfx(AudioClip clip, float volume = 1f)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume) * masterSfxVolume);
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
}
