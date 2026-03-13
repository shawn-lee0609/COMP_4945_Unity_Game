// Assets/Scripts/AudioManager.cs
// Singleton audio manager for handling all game sounds

using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("Music Clips")]
    [SerializeField] private AudioClip lobbyMusic;
    [SerializeField] private AudioClip gameMusic;

    [Header("UI Sound Effects")]
    [SerializeField] private AudioClip buttonClickSound;
    [SerializeField] private AudioClip playerJoinSound;
    [SerializeField] private AudioClip gameStartSound;

    [Header("Gameplay Sound Effects")]
    [SerializeField] private AudioClip bombPlaceSound;
    [SerializeField] private AudioClip bombExplodeSound;
    [SerializeField] private AudioClip playerMoveSound;
    [SerializeField] private AudioClip playerDeathSound;
    [SerializeField] private AudioClip wallDestroySound;

    [Header("Win/Lose Sounds")]
    [SerializeField] private AudioClip victorySound;
    [SerializeField] private AudioClip defeatSound;

    [Header("Volume Settings")]
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float musicVolume = 0.5f;
    [Range(0f, 1f)] public float sfxVolume = 0.7f;

    void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Setup audio sources if not assigned
        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
        }

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
        }

        UpdateVolumes();
    }

    void Update()
    {
        UpdateVolumes();
    }

    private void UpdateVolumes()
    {
        musicSource.volume = musicVolume * masterVolume;
        sfxSource.volume = sfxVolume * masterVolume;
    }

    // ══════════════════════════════════════════════
    // MUSIC CONTROLS
    // ══════════════════════════════════════════════

    public void PlayLobbyMusic()
    {
        if (lobbyMusic != null && musicSource.clip != lobbyMusic)
        {
            musicSource.clip = lobbyMusic;
            musicSource.Play();
        }
    }

    public void PlayGameMusic()
    {
        if (gameMusic != null && musicSource.clip != gameMusic)
        {
            musicSource.clip = gameMusic;
            musicSource.Play();
        }
    }

    public void StopMusic()
    {
        musicSource.Stop();
    }

    public void PauseMusic()
    {
        musicSource.Pause();
    }

    public void ResumeMusic()
    {
        musicSource.UnPause();
    }

    // ══════════════════════════════════════════════
    // UI SOUND EFFECTS
    // ══════════════════════════════════════════════

    public void PlayButtonClick()
    {
        PlaySFX(buttonClickSound);
    }

    public void PlayPlayerJoin()
    {
        PlaySFX(playerJoinSound);
    }

    public void PlayGameStart()
    {
        PlaySFX(gameStartSound);
    }

    // ══════════════════════════════════════════════
    // GAMEPLAY SOUND EFFECTS
    // ══════════════════════════════════════════════

    public void PlayBombPlace()
    {
        PlaySFX(bombPlaceSound);
    }

    public void PlayBombExplode()
    {
        PlaySFX(bombExplodeSound);
    }

    public void PlayPlayerMove()
    {
        PlaySFX(playerMoveSound, 0.3f); // Lower volume for footsteps
    }

    public void PlayPlayerDeath()
    {
        PlaySFX(playerDeathSound);
    }

    public void PlayWallDestroy()
    {
        PlaySFX(wallDestroySound);
    }

    // ══════════════════════════════════════════════
    // WIN/LOSE SOUNDS
    // ══════════════════════════════════════════════

    public void PlayVictory()
    {
        PlaySFX(victorySound);
    }

    public void PlayDefeat()
    {
        PlaySFX(defeatSound);
    }

    // ══════════════════════════════════════════════
    // HELPER METHODS
    // ══════════════════════════════════════════════

    private void PlaySFX(AudioClip clip, float volumeMultiplier = 1f)
    {
        if (clip != null)
        {
            sfxSource.PlayOneShot(clip, volumeMultiplier);
        }
    }

    /// <summary>
    /// Play a sound effect at a specific position in 3D space (optional feature)
    /// </summary>
    public void PlaySFXAtPosition(AudioClip clip, Vector3 position, float volumeMultiplier = 1f)
    {
        if (clip != null)
        {
            AudioSource.PlayClipAtPoint(clip, position, sfxVolume * masterVolume * volumeMultiplier);
        }
    }
}