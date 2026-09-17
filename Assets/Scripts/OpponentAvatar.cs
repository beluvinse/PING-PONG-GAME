using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gives the opponent a random skin and expression from the AvatarCatalog, rolled
/// again every time a match starts - arriving from the menu, a rematch, or playing
/// again from the end screen - and kept for the rest of that match.
///
/// Add it to the opponent's copy of Player.prefab, not to the prefab itself.
/// </summary>
public class OpponentAvatar : MonoBehaviour
{
    [SerializeField] private AvatarCatalog _catalog;
    [Tooltip("A new opponent is rolled each time this starts a match. Found automatically when the component is added.")]
    [SerializeField] private MatchController _matchController;
    [Tooltip("Gets the skin sprite.")]
    [SerializeField] private Image _body;
    [Tooltip("Gets the expression sprite.")]
    [SerializeField] private Image _face;
    [Tooltip("Never roll the skin the player chose, so the two avatars are easy to tell apart.")]
    [SerializeField] private bool _avoidPlayerSkin = true;

    private int _rolledForMatch = -1;

    private void OnEnable()
    {
        if (_matchController == null) return;

        _matchController.OnMatchStarted += OnMatchStarted;

        // Switched on after a match already began (its panel was off, say): catch up.
        if (_matchController.MatchesStarted > 0 && _rolledForMatch != _matchController.MatchesStarted)
            OnMatchStarted();
    }

    private void OnDisable()
    {
        if (_matchController != null) _matchController.OnMatchStarted -= OnMatchStarted;
    }

    // Without a MatchController there is no match start to wait for.
    private void Start()
    {
        if (_matchController == null) Randomize();
    }

    private void OnMatchStarted()
    {
        _rolledForMatch = _matchController.MatchesStarted;
        Randomize();
    }

    public void Randomize()
    {
        if (_catalog == null)
        {
            Debug.LogWarning($"{name}: OpponentAvatar has no AvatarCatalog, so it keeps the prefab's art. "
                             + "Create one with Tools > Ping Pong > Avatar Catalog.", this);
            return;
        }

        // What the player's avatar actually shows, old saves included.
        var playerSkin = _avoidPlayerSkin ? _catalog.Skin(GameOptions.SkinSprite, GameOptions.SkinIndex) : null;

        SetSprite(_body, _catalog.AnySkin(playerSkin != null ? playerSkin.name : null));
        SetSprite(_face, _catalog.AnyExpression());
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

        _matchController = FindObjectOfType<MatchController>();

        var guids = UnityEditor.AssetDatabase.FindAssets("t:AvatarCatalog");
        if (guids.Length > 0)
            _catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<AvatarCatalog>(UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    [ContextMenu("Auto Assign")]
    private void AutoAssign()
    {
        UnityEditor.Undo.RecordObject(this, "Auto Assign Opponent Avatar");
        Reset();
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
