using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What every <see cref="SoundId"/> sounds like: the clips, how loud, how much the
/// pitch may wander and how often it is allowed to repeat.
///
/// AudioManager loads it from Resources, so there is one bank for the whole game
/// and no scene has to reference it. An id with no clips simply stays silent.
/// </summary>
[CreateAssetMenu(fileName = "SoundBank", menuName = "Ping Pong/Sound Bank")]
public class SoundBank : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public SoundId id;

        [Tooltip("Drop one clip, or several: a different one is picked each time, never the same twice in a row.")]
        public AudioClip[] clips;

        [Range(0f, 1f)]
        [Tooltip("Loudness of this sound before the options volumes are applied.")]
        public float volume = 1f;

        [Tooltip("Pitch is picked between these two. A little spread stops repeated hits sounding like a machine gun; keep both at 1 for a sound that must always be identical.")]
        public Vector2 pitch = new Vector2(0.97f, 1.03f);

        [Tooltip("Seconds this sound has to wait before it can play again. This is what keeps a dragged slider from firing every frame. 0 = no limit.")]
        public float minInterval;

        [Tooltip("On = follows the music volume instead of the effects volume.")]
        public bool music;
    }

    [Header("Background music")]
    [Tooltip("Loops from the moment the game starts and carries on across scenes. Empty = no music.")]
    [SerializeField] private AudioClip _music;

    [Range(0f, 1f)]
    [Tooltip("Loudness of the track before the options music volume is applied.")]
    [SerializeField] private float _musicVolume = 1f;

    [Tooltip("Seconds the music takes to fade up when it starts.")]
    [SerializeField] private float _musicFade = 1.5f;

    [Header("Sounds")]
    [SerializeField] private Entry[] _entries = new Entry[0];

    public Entry[] Entries => _entries;
    public AudioClip Music => _music;
    public float MusicVolume => _musicVolume;
    public float MusicFade => _musicFade;

    private Dictionary<SoundId, Entry> _byId;

    /// <summary>The entry for an id, or null when the bank says nothing about it.</summary>
    public Entry Find(SoundId id)
    {
        if (_byId == null)
        {
            _byId = new Dictionary<SoundId, Entry>(_entries.Length);
            foreach (var entry in _entries)
                if (entry != null) _byId[entry.id] = entry;        // a duplicated id: the last one wins
        }

        return _byId.TryGetValue(id, out var found) ? found : null;
    }

#if UNITY_EDITOR
    /// <summary>Editor only: how Tools > Ping Pong > Create Sound Bank fills the rows.</summary>
    public void SetEntries(Entry[] entries)
    {
        _entries = entries;
        _byId = null;
    }
#endif

    // Editing the asset while the game runs rebuilds the lookup on the next sound.
    private void OnEnable() => _byId = null;
    private void OnValidate() => _byId = null;
}
