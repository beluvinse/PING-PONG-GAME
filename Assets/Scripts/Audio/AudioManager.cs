using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// The one place that plays sound.
///
/// It builds itself the first time something asks for a sound and survives scene
/// loads, so nothing has to be dragged into a scene: <c>AudioManager.Play(
/// SoundId.ButtonClick)</c> is the whole API. What each id sounds like lives in
/// the SoundBank asset in Resources.
///
/// It also owns the background music: one looping track that starts with the game
/// and carries on across the menu / game change untouched, because this object
/// survives the load.
///
/// Volumes come from <see cref="GameOptions"/>, so the options sliders work with
/// no extra wiring, and a call for an id with no clips does nothing at all. That
/// is on purpose: the game can be wired for sound long before there is any.
/// </summary>
public class AudioManager : MonoBehaviour
{
    private const string BANK_RESOURCE = "SoundBank";

    // Enough for a rally plus UI on top; past this the oldest sound is cut.
    private const int VOICES = 8;

    private static AudioManager _instance;
    private static bool _quitting;

    private SoundBank _bank;
    private AudioSource _music;
    private AudioSource[] _voices;
    private int _oldest;
    private readonly Dictionary<SoundId, float> _lastPlayed = new Dictionary<SoundId, float>();
    private readonly Dictionary<SoundId, int> _lastClip = new Dictionary<SoundId, int>();

    public static AudioManager Instance
    {
        get
        {
            if (_instance == null && !_quitting)
            {
                var go = new GameObject("AudioManager");
                _instance = go.AddComponent<AudioManager>();
            }

            return _instance;
        }
    }

    /// <summary>Plays a sound. Silent, and free, while that id has no clips.</summary>
    public static void Play(SoundId id)
    {
        if (id == SoundId.None || _quitting) return;

        Instance.PlaySound(id);
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        _bank = Resources.Load<SoundBank>(BANK_RESOURCE);

        _music = gameObject.AddComponent<AudioSource>();
        _music.playOnAwake = false;
        _music.loop = true;
        _music.spatialBlend = 0f;
        _music.ignoreListenerPause = true;                  // a paused game still has music
        GameOptions.Changed += ApplyMusicVolume;

        // One AudioSource per voice: PlayOneShot shares the source's pitch, so
        // separate sources are what makes per-sound pitch spread possible.
        _voices = new AudioSource[VOICES];
        for (var i = 0; i < VOICES; i++)
        {
            var voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0f;                            // 2D: the court is always on screen
            voice.ignoreListenerPause = true;                   // menus still click while the game is paused
            _voices[i] = voice;
        }
    }

    /// <summary>
    /// Starts the background track once, at the start of the game. It is not
    /// needed again: the manager and its AudioSource survive a scene load, so the
    /// same track simply keeps playing from wherever it was.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartMusic()
    {
        if (_quitting) return;

        Instance.PlayMusic();
    }

    /// <summary>Plays the bank's track, fading it up. Does nothing if it is already on.</summary>
    public void PlayMusic()
    {
        var clip = _bank != null ? _bank.Music : null;
        if (clip == null || (_music.clip == clip && _music.isPlaying)) return;

        _music.clip = clip;
        _music.volume = 0f;
        _music.Play();

        if (_bank.MusicFade <= 0f) ApplyMusicVolume();
        else _music.DOFade(MusicTarget, _bank.MusicFade).SetUpdate(true);
    }

    /// <summary>What the track should be at right now, options included.</summary>
    private float MusicTarget => _bank != null ? _bank.MusicVolume * GameOptions.EffectiveMusic : 0f;

    /// <summary>Moving the options slider is heard straight away, mid-track.</summary>
    private void ApplyMusicVolume()
    {
        if (_music == null) return;

        _music.DOKill();                                    // a fade in progress would fight the slider
        _music.volume = MusicTarget;
    }

    private void OnDestroy()
    {
        GameOptions.Changed -= ApplyMusicVolume;
    }

    public void PlaySound(SoundId id)
    {
        var entry = _bank != null ? _bank.Find(id) : null;
        if (entry == null || entry.clips == null || entry.clips.Length == 0) return;

        // Unscaled: a paused game still has working menus.
        var now = Time.unscaledTime;
        if (entry.minInterval > 0f && _lastPlayed.TryGetValue(id, out var last) && now - last < entry.minInterval)
            return;

        var clip = PickClip(id, entry.clips);
        if (clip == null) return;

        _lastPlayed[id] = now;

        var voice = FreeVoice();
        voice.clip = clip;
        voice.volume = entry.volume * (entry.music ? GameOptions.EffectiveMusic : GameOptions.EffectiveSfx);
        voice.pitch = Random.Range(entry.pitch.x, entry.pitch.y);
        voice.Play();
    }

    /// <summary>
    /// Random, but never the same clip twice in a row: a paddle hit fires often
    /// enough that an immediate repeat is heard as a glitch rather than variety.
    /// </summary>
    private AudioClip PickClip(SoundId id, AudioClip[] clips)
    {
        if (clips.Length == 1) return clips[0];

        var index = Random.Range(0, clips.Length);
        if (_lastClip.TryGetValue(id, out var last) && index == last)
            index = (index + 1) % clips.Length;

        _lastClip[id] = index;
        return clips[index];
    }

    /// <summary>A voice that is free, or the oldest one when they are all busy.</summary>
    private AudioSource FreeVoice()
    {
        foreach (var voice in _voices)
            if (!voice.isPlaying) return voice;

        var stolen = _voices[_oldest];
        _oldest = (_oldest + 1) % _voices.Length;
        return stolen;
    }

    // Asking for a sound while the game is closing would build a fresh manager
    // that nothing ever hears.
    private void OnApplicationQuit() => _quitting = true;
}
