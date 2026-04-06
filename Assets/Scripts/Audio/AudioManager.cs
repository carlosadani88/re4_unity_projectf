// ============================================================================
// VILL4GE — AudioManager.cs
// Centralized audio manager with one-shot SFX pooling and layered music.
// All AudioClips are optional – the system degrades gracefully when clips
// are not yet assigned (useful for a code-first placeholder setup).
//
// Usage: AudioManager.I.PlaySFX(AudioManager.SFX.Gunshot);
//        AudioManager.I.PlayMusic(AudioManager.Music.Combat);
// ============================================================================
using UnityEngine;
using System.Collections.Generic;

public class AudioManager : MonoBehaviour
{
    public static AudioManager I { get; private set; }

    // ── SFX categories ────────────────────────────────────────────────────
    public enum SFX
    {
        Gunshot, GunEmpty, Reload, Knife, KnifeHit,
        Footstep, Footstep2, Sprint,
        EnemyGroan, EnemyHurt, EnemyDeath, EnemyChainsaw,
        PlayerHurt, PlayerDeath,
        Grenade, Explosion,
        ItemPickup, AmmoPickup, PesetasPickup,
        MerchantGreet, MerchantBuy, MerchantSell,
        UIClick, UIOpen, UIClose,
        KickHit, Stagger,
        CheckpointSave
    }

    // ── Music categories ──────────────────────────────────────────────────
    public enum Music { None, Ambient, Combat, Boss, Merchant, Dead, WaveIntro }

    // ─────────────────────────────────────────────────────────────────────
    [System.Serializable]
    public class SFXEntry
    {
        public SFX id;
        public AudioClip[] clips;          // Multiple clips → random selection
        [Range(0f, 1f)] public float volume = 1f;
        [Range(0.8f, 1.2f)] public float pitchVariance = 0.1f;
    }

    [System.Serializable]
    public class MusicEntry
    {
        public Music id;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 0.6f;
        public bool loop = true;
    }

    [Header("SFX")]
    public SFXEntry[] sfxEntries;

    [Header("Music")]
    public MusicEntry[] musicEntries;

    [Header("Settings")]
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float sfxVolume    = 1f;
    [Range(0f, 1f)] public float musicVolume  = 0.6f;

    // ── Internal ──────────────────────────────────────────────────────────
    const int POOL_SIZE = 16;
    AudioSource[] _pool;
    int _poolIdx;
    AudioSource _musicSource;

    Dictionary<SFX, SFXEntry>     _sfxMap   = new Dictionary<SFX, SFXEntry>();
    Dictionary<Music, MusicEntry> _musicMap = new Dictionary<Music, MusicEntry>();
    Music _currentMusic = Music.None;

    // ─────────────────────────────────────────────────────────────────────
    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);

        // Build pool
        _pool = new AudioSource[POOL_SIZE];
        for (int i = 0; i < POOL_SIZE; i++)
        {
            var go = new GameObject($"SFXSource_{i}");
            go.transform.SetParent(transform);
            _pool[i] = go.AddComponent<AudioSource>();
            _pool[i].playOnAwake = false;
        }

        // Music source
        var mg = new GameObject("MusicSource");
        mg.transform.SetParent(transform);
        _musicSource = mg.AddComponent<AudioSource>();
        _musicSource.playOnAwake = false;
        _musicSource.loop = true;

        // Index entries
        if (sfxEntries != null)
            foreach (var e in sfxEntries)
                _sfxMap[e.id] = e;

        if (musicEntries != null)
            foreach (var e in musicEntries)
                _musicMap[e.id] = e;
    }

    // ─────────────────────────────────────────────────────────────────────
    /// <summary>Play a one-shot SFX, optionally at a world position.</summary>
    public void PlaySFX(SFX sfx, Vector3? position = null)
    {
        if (!_sfxMap.TryGetValue(sfx, out var entry)) return;
        if (entry.clips == null || entry.clips.Length == 0) return;

        var clip = entry.clips[Random.Range(0, entry.clips.Length)];
        if (!clip) return;

        var source = NextPoolSource();
        source.clip   = clip;
        source.volume = entry.volume * sfxVolume * masterVolume;
        source.pitch  = 1f + Random.Range(-entry.pitchVariance, entry.pitchVariance);

        if (position.HasValue)
        {
            source.transform.position = position.Value;
            source.spatialBlend = 1f;
            source.maxDistance  = 30f;
            source.rolloffMode  = AudioRolloffMode.Linear;
        }
        else
        {
            source.spatialBlend = 0f;
        }

        source.Play();
    }

    // ─────────────────────────────────────────────────────────────────────
    /// <summary>Cross-fade to a new music track.</summary>
    public void PlayMusic(Music music)
    {
        if (music == _currentMusic) return;
        _currentMusic = music;

        if (music == Music.None) { _musicSource.Stop(); return; }
        if (!_musicMap.TryGetValue(music, out var entry)) return;
        if (!entry.clip) return;

        _musicSource.clip   = entry.clip;
        _musicSource.loop   = entry.loop;
        _musicSource.volume = entry.volume * musicVolume * masterVolume;
        _musicSource.Play();
    }

    public void StopMusic() => PlayMusic(Music.None);

    // ─────────────────────────────────────────────────────────────────────
    /// <summary>Update master/sfx/music volumes at runtime.</summary>
    public void SetVolumes(float master, float sfx, float music)
    {
        masterVolume = Mathf.Clamp01(master);
        sfxVolume    = Mathf.Clamp01(sfx);
        musicVolume  = Mathf.Clamp01(music);
        if (_musicSource.isPlaying)
            _musicSource.volume = musicVolume * masterVolume;
    }

    // ─────────────────────────────────────────────────────────────────────
    AudioSource NextPoolSource()
    {
        var s = _pool[_poolIdx];
        _poolIdx = (_poolIdx + 1) % POOL_SIZE;
        return s;
    }
}
