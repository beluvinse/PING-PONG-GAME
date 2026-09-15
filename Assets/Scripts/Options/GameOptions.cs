using System;
using UnityEngine;

/// <summary>
/// Every value the options screen owns, backed by PlayerPrefs.
///
/// Static on purpose: gameplay reads it without needing a reference, and the
/// choices survive the trip from the menu scene into the game scene.
///
/// Changing a value applies it straight away so the player hears and sees the
/// result; <see cref="Save"/> is what writes it to disk, and <see cref="Revert"/>
/// goes back to the last saved state.
/// </summary>
public static class GameOptions
{
    private const string KEY_MASTER = "options.master";
    private const string KEY_MUSIC = "options.music";
    private const string KEY_SFX = "options.sfx";
    private const string KEY_MUTE = "options.mute";
    private const string KEY_SKIN = "options.skin";
    private const string KEY_EXPRESSION = "options.expression";

    /// <summary>Raised on every change, saved or not. Hook audio and avatars here.</summary>
    public static event Action Changed;

    private static float _master = 1f;
    private static float _music = 0.7f;
    private static float _sfx = 1f;
    private static bool _mute;
    private static int _skin;
    private static int _expression;

    public static float MasterVolume
    {
        get => _master;
        set => Apply(ref _master, Mathf.Clamp01(value));
    }

    public static float MusicVolume
    {
        get => _music;
        set => Apply(ref _music, Mathf.Clamp01(value));
    }

    public static float SfxVolume
    {
        get => _sfx;
        set => Apply(ref _sfx, Mathf.Clamp01(value));
    }

    public static bool MuteAll
    {
        get => _mute;
        set => Apply(ref _mute, value);
    }

    public static int SkinIndex
    {
        get => _skin;
        set => Apply(ref _skin, Mathf.Max(0, value));
    }

    public static int ExpressionIndex
    {
        get => _expression;
        set => Apply(ref _expression, Mathf.Max(0, value));
    }

    /// <summary>What an AudioSource should actually play at, mute included.</summary>
    public static float EffectiveMusic => _mute ? 0f : _music * _master;

    /// <summary>Same for one-shot sounds.</summary>
    public static float EffectiveSfx => _mute ? 0f : _sfx * _master;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Load()
    {
        _master = PlayerPrefs.GetFloat(KEY_MASTER, 1f);
        _music = PlayerPrefs.GetFloat(KEY_MUSIC, 0.7f);
        _sfx = PlayerPrefs.GetFloat(KEY_SFX, 1f);
        _mute = PlayerPrefs.GetInt(KEY_MUTE, 0) == 1;
        _skin = PlayerPrefs.GetInt(KEY_SKIN, 0);
        _expression = PlayerPrefs.GetInt(KEY_EXPRESSION, 0);

        ApplyAudio();
        Changed?.Invoke();
    }

    public static void Save()
    {
        PlayerPrefs.SetFloat(KEY_MASTER, _master);
        PlayerPrefs.SetFloat(KEY_MUSIC, _music);
        PlayerPrefs.SetFloat(KEY_SFX, _sfx);
        PlayerPrefs.SetInt(KEY_MUTE, _mute ? 1 : 0);
        PlayerPrefs.SetInt(KEY_SKIN, _skin);
        PlayerPrefs.SetInt(KEY_EXPRESSION, _expression);
        PlayerPrefs.Save();
    }

    /// <summary>Throws away unsaved edits - what Back does.</summary>
    public static void Revert() => Load();

    private static void Apply<T>(ref T field, T value)
    {
        if (Equals(field, value)) return;

        field = value;
        ApplyAudio();
        Changed?.Invoke();
    }

    private static void ApplyAudio()
    {
        // The only global knob that works with no audio assets in the project.
        // Music and SFX need their own AudioSources reading the Effective*
        // properties, or an AudioMixer, once there is sound to play.
        AudioListener.volume = _mute ? 0f : _master;
    }
}
