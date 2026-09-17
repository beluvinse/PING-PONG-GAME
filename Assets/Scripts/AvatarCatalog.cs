using System;
using System.Linq;
using UnityEngine;

/// <summary>
/// Every avatar sprite the game can show. The options screen reads its art off
/// the swatches, but a scene without that screen - the match - needs somewhere
/// to turn the saved choice back into images.
///
/// Tools > Ping Pong > Avatar Catalog creates it and fills it from the avatar folders.
/// </summary>
[CreateAssetMenu(fileName = "AvatarCatalog", menuName = "Ping Pong/Avatar Catalog")]
public class AvatarCatalog : ScriptableObject
{
    private const string ASSET_PATH = "Assets/Settings/AvatarCatalog.asset";
    private const string SKINS_FOLDER = "Assets/Art/Icons/Avatars/bg";
    private const string EXPRESSIONS_FOLDER = "Assets/Art/Icons/Avatars";

    [SerializeField] private Sprite[] _skins = new Sprite[0];
    [SerializeField] private Sprite[] _expressions = new Sprite[0];

    public Sprite Skin(string spriteName, int fallbackIndex) => Find(_skins, spriteName, fallbackIndex);
    public Sprite Expression(string spriteName, int fallbackIndex) => Find(_expressions, spriteName, fallbackIndex);

    /// <summary>Any skin except <paramref name="excludeName"/>, unless that is the only one there is.</summary>
    public Sprite AnySkin(string excludeName = null) => PickAtRandom(_skins, excludeName);

    public Sprite AnyExpression() => PickAtRandom(_expressions, null);

    private static Sprite PickAtRandom(Sprite[] sprites, string excludeName)
    {
        if (sprites == null) return null;

        var pool = sprites.Where(s => s != null && s.name != excludeName).ToArray();
        if (pool.Length == 0) pool = sprites.Where(s => s != null).ToArray();

        return pool.Length == 0 ? null : pool[UnityEngine.Random.Range(0, pool.Length)];
    }

    /// <summary>By name; the index only covers saves made before names were stored.</summary>
    private static Sprite Find(Sprite[] sprites, string spriteName, int fallbackIndex)
    {
        if (sprites == null || sprites.Length == 0) return null;

        if (!string.IsNullOrEmpty(spriteName))
            foreach (var sprite in sprites)
                if (sprite != null && sprite.name == spriteName)
                    return sprite;

        return fallbackIndex >= 0 && fallbackIndex < sprites.Length ? sprites[fallbackIndex] : null;
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/Ping Pong/Avatar Catalog")]
    private static void CreateOrRefresh()
    {
        var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<AvatarCatalog>(ASSET_PATH);
        if (catalog == null)
        {
            catalog = CreateInstance<AvatarCatalog>();
            UnityEditor.AssetDatabase.CreateAsset(catalog, ASSET_PATH);
        }

        catalog.FillFromFolders();
        UnityEditor.AssetDatabase.SaveAssetIfDirty(catalog);
        UnityEditor.Selection.activeObject = catalog;
    }

    /// <summary>
    /// Skins: the sphere-* sprites in Avatars/bg. Expressions: the sprites directly
    /// in Avatars, minus the selection ring. Order does not matter - lookups go by name.
    /// </summary>
    [ContextMenu("Fill From Avatar Folders")]
    private void FillFromFolders()
    {
        UnityEditor.Undo.RecordObject(this, "Fill Avatar Catalog");
        _skins = LoadSprites(SKINS_FOLDER, n => n.StartsWith("sphere-", StringComparison.OrdinalIgnoreCase));
        _expressions = LoadSprites(EXPRESSIONS_FOLDER, n => n.IndexOf("ring", StringComparison.OrdinalIgnoreCase) < 0);
        UnityEditor.EditorUtility.SetDirty(this);

        Debug.Log($"AvatarCatalog: {_skins.Length} skins, {_expressions.Length} expressions.", this);
    }

    private static Sprite[] LoadSprites(string folder, Func<string, bool> accept) =>
        UnityEditor.AssetDatabase.FindAssets("t:Sprite", new[] { folder })
            .Select(UnityEditor.AssetDatabase.GUIDToAssetPath)
            .Where(p => System.IO.Path.GetDirectoryName(p)?.Replace('\\', '/') == folder)   // not subfolders
            .Select(UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>)
            .Where(s => s != null && accept(s.name))
            .OrderBy(s => s.name, StringComparer.Ordinal)
            .ToArray();
#endif
}
