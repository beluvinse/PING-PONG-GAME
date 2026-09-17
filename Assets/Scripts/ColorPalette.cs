using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The game's named colours, kept in the project instead of only in Figma.
/// Edit it through Tools > Ping Pong > Color Palette; code can read it as well.
/// </summary>
[CreateAssetMenu(fileName = "GamePalette", menuName = "Ping Pong/Color Palette")]
public class ColorPalette : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public string name = "New Color";
        public Color color = Color.white;
    }

    [SerializeField] private List<Entry> _colors = new List<Entry>();

    public IReadOnlyList<Entry> Colors => _colors;

    /// <summary>Looks a colour up by name, ignoring case.</summary>
    public bool TryGet(string colorName, out Color color)
    {
        foreach (var entry in _colors)
            if (string.Equals(entry.name, colorName, StringComparison.OrdinalIgnoreCase))
            {
                color = entry.color;
                return true;
            }

        color = Color.white;
        return false;
    }
}
