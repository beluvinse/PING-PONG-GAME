using UnityEngine;

/// <summary>
/// Recolors the table and both paddles by rebuilding the low-poly texture atlas
/// at runtime with a warmer palette that matches the 2D background.
///
/// Table, blue paddle and red paddle all share "low poly vhs textures.png", an
/// 8x8 grid of flat 16x16 swatches - each object just samples a different cell.
/// So one regenerated atlas recolors all three at once.
///
/// Bootstraps itself, runs in play mode only and assigns the new texture to
/// material instances, so the source PNG is never modified. Delete this script
/// to go back to the original colors.
/// </summary>
public class PaletteSkin : MonoBehaviour
{
    private const string ATLAS_NAME = "low poly vhs textures";
    private const int GRID = 8;
    private const int CELL = 16;

    [System.Serializable]
    public struct SwatchOverride
    {
        public string label;
        [Range(0, GRID - 1)] public int column;
        [Range(0, GRID - 1)] public int row;
        public Color color;
    }

    // Original atlas, sampled cell by cell. Row 0 is the top row of the PNG.
    private static readonly string[] OriginalPalette =
    {
        "B23737 E68627 E9E11F 6BB96C 4A79B8 7660AA 70553C 1D1712",
        "883131 C07021 B6B016 579458 3A5E8D 5A4981 4A3929 323232",
        "552020 90551A 8C8710 3F6B40 2D476B 3D3256 31261D 6B6B6B",
        "F88FA9 F4B176 FEFFC7 C6EDCD 9FC4EB C5BBDD C8B8A9 B9B9B9",
        "AAE1E6 7CC3C9 4E8A8F E397EA AA70B0 78856B C2CDB8 FFFFFF",
        "FFFFFF FFFFFF FFFFFF FFFFFF FFFFFF FFFFFF FFFFFF FFFFFF",
        "FFFFFF FFFFFF FFFFFF FFFFFF FFFFFF FFFFFF FFFFFF FFFFFF",
        "FFFFFF FFFFFF FFFFFF FFFFFF FFFFFF FFFFFF FFFFFF FFFFFF"
    };

    [Header("Palette overrides (column, row) into the 8x8 atlas")]
    [SerializeField]
    private SwatchOverride[] _overrides =
    {
        // Table: green -> soft blue. Complements the warm peach floor of the
        // background and makes the white ball read much better.
        MakeOverride("Table top",       3, 0, "4E90C2"),
        MakeOverride("Table mid",       3, 1, "3C74A0"),
        MakeOverride("Table edge",      3, 2, "2C5578"),

        // Opponent paddle: blue -> deep indigo, so it stays cool/"AI coloured"
        // but separates from the now-blue table.
        MakeOverride("AI paddle light", 4, 0, "5A5490"),
        MakeOverride("AI paddle mid",   4, 1, "413A6B"),
        MakeOverride("AI paddle dark",  4, 2, "2C2749"),

        // Player paddle: dark maroon -> warm coral red, matching the sunlit
        // background and popping against the blue table.
        MakeOverride("Player paddle light", 0, 0, "D9534A"),
        MakeOverride("Player paddle mid",   0, 1, "B03F3A"),
        MakeOverride("Player paddle dark",  0, 2, "7C2A2A")
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindObjectOfType<PaletteSkin>() != null) return;
        new GameObject("PaletteSkin").AddComponent<PaletteSkin>();
    }

    private void Start()
    {
        var atlas = BuildAtlas();
        var applied = 0;

        foreach (var renderer in FindObjectsOfType<Renderer>(true))
        {
            foreach (var material in renderer.materials)
            {
                // Reading mainTexture on a shader without a main texture logs an
                // error, so check first (the indicator quads use a texture-less
                // shader). URP shaders name it _BaseMap, built-in ones _MainTex.
                if (material == null || !(material.HasProperty("_BaseMap") || material.HasProperty("_MainTex"))) continue;
                if (material.mainTexture == null || material.mainTexture.name != ATLAS_NAME) continue;

                material.mainTexture = atlas;
                applied++;
            }
        }

        if (applied == 0)
            Debug.LogWarning($"PaletteSkin: no material using '{ATLAS_NAME}' found - colors unchanged.");
    }

    private Texture2D BuildAtlas()
    {
        var size = GRID * CELL;
        var atlas = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = ATLAS_NAME + " (recolored)",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        var colors = BuildPalette();
        var pixels = new Color32[size * size];

        for (var row = 0; row < GRID; row++)
        {
            for (var column = 0; column < GRID; column++)
            {
                var swatch = (Color32)colors[row, column];

                // Row 0 is the top of the PNG, but Texture2D's y grows upward.
                var yStart = (GRID - 1 - row) * CELL;
                var xStart = column * CELL;

                for (var y = yStart; y < yStart + CELL; y++)
                {
                    var rowOffset = y * size;
                    for (var x = xStart; x < xStart + CELL; x++)
                        pixels[rowOffset + x] = swatch;
                }
            }
        }

        atlas.SetPixels32(pixels);
        atlas.Apply();
        return atlas;
    }

    private Color[,] BuildPalette()
    {
        var colors = new Color[GRID, GRID];

        for (var row = 0; row < GRID; row++)
        {
            var cells = OriginalPalette[row].Split(' ');
            for (var column = 0; column < GRID; column++)
                colors[row, column] = ParseHex(cells[column]);
        }

        foreach (var swatch in _overrides)
        {
            if (swatch.column < 0 || swatch.column >= GRID) continue;
            if (swatch.row < 0 || swatch.row >= GRID) continue;
            colors[swatch.row, swatch.column] = swatch.color;
        }

        return colors;
    }

    private static SwatchOverride MakeOverride(string label, int column, int row, string hex)
    {
        return new SwatchOverride
        {
            label = label,
            column = column,
            row = row,
            color = ParseHex(hex)
        };
    }

    private static Color ParseHex(string hex)
    {
        return ColorUtility.TryParseHtmlString("#" + hex, out var color) ? color : Color.magenta;
    }
}
