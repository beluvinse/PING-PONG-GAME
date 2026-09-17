using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows the avatar saved from the options screen (skin and expression) on this
/// avatar's images, and follows it if it changes while the scene is open.
///
/// Add it to the player's copy of Player.prefab only - the opponent uses the
/// same prefab and keeps its own art.
/// </summary>
public class PlayerAvatar : MonoBehaviour
{
    [SerializeField] private AvatarCatalog _catalog;
    [Tooltip("Gets the skin sprite.")]
    [SerializeField] private Image _body;
    [Tooltip("Gets the expression sprite.")]
    [SerializeField] private Image _face;

    private void OnEnable()
    {
        if (_catalog == null)
            Debug.LogWarning($"{name}: PlayerAvatar has no AvatarCatalog, so it keeps the prefab's art. "
                             + "Create one with Tools > Ping Pong > Avatar Catalog.", this);

        GameOptions.Changed += Apply;
        Apply();
    }

    private void OnDisable() => GameOptions.Changed -= Apply;

    public void Apply()
    {
        if (_catalog == null) return;

        SetSprite(_body, _catalog.Skin(GameOptions.SkinSprite, GameOptions.SkinIndex));
        SetSprite(_face, _catalog.Expression(GameOptions.ExpressionSprite, GameOptions.ExpressionIndex));
    }

    // Nothing found keeps whatever the prefab shows instead of blanking the image.
    private static void SetSprite(Image image, Sprite sprite)
    {
        if (image != null && sprite != null) image.sprite = sprite;
    }

#if UNITY_EDITOR
    // Runs when the component is added: Player.prefab names these children
    // "Background" (the skin sphere) and "Face".
    private void Reset()
    {
        foreach (var image in GetComponentsInChildren<Image>(true))
        {
            if (image.name == "Background") _body = image;
            else if (image.name == "Face") _face = image;
        }

        var guids = UnityEditor.AssetDatabase.FindAssets("t:AvatarCatalog");
        if (guids.Length > 0)
            _catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<AvatarCatalog>(UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
    }
#endif
}
