using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tools > Ping Pong > Build Match End Screen. Builds the end-of-match modal in
/// the open Game scene - result title, the player's avatar, both scores, Rematch
/// and Menu - and wires it into UIManager.
///
/// Everything is a plain coloured placeholder, so final art drops straight onto
/// the Images; layout groups keep things arranged when sizes change.
/// </summary>
public static class MatchEndScreenBuilder
{
    private const string SCREEN_NAME = "MatchEndScreen";

    private static readonly Color Dim = new Color(0f, 0f, 0f, 0.55f);
    private static readonly Color Navy = new Color(0.13f, 0.18f, 0.44f);
    private static readonly Color NavyDeep = new Color(0.10f, 0.14f, 0.36f);
    private static readonly Color Yellow = new Color(0.96f, 0.78f, 0.26f);
    private static readonly Color Red = new Color(0.86f, 0.25f, 0.25f);
    private static readonly Color Blue = new Color(0.30f, 0.36f, 0.85f);
    private static readonly Color Purple = new Color(0.42f, 0.40f, 0.85f);
    private static readonly Color Cream = new Color(1f, 0.97f, 0.88f);

    [MenuItem("Tools/Ping Pong/Build Match End Screen")]
    private static void Build()
    {
        var ui = Object.FindObjectOfType<UIManager>(true);
        if (ui == null)
        {
            EditorUtility.DisplayDialog("Match end screen", "No UIManager in the open scene. Open Game.unity and run this again.", "OK");
            return;
        }

        var existing = ui.transform.Find(SCREEN_NAME);
        if (existing != null &&
            !EditorUtility.DisplayDialog("Match end screen", "A MatchEndScreen already exists. Replace it?", "Replace", "Cancel"))
            return;
        if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);

        // Full-screen dimmer, last under UI so it draws over the rest of the HUD.
        var screen = Panel(SCREEN_NAME, ui.transform, Dim, rounded: false);
        Stretch(RectOf(screen));
        screen.AddComponent<CanvasGroup>();
        screen.transform.SetAsLastSibling();

        var card = Panel("Card", screen.transform, Navy);
        Place(RectOf(card), Vector2.zero, new Vector2(900f, 780f));

        var title = Text("Title", "YOU WIN!", card.transform, 110, Yellow);
        title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        title.rectTransform.pivot = new Vector2(0.5f, 1f);
        title.rectTransform.anchoredPosition = new Vector2(0f, -40f);
        title.rectTransform.sizeDelta = new Vector2(820f, 150f);

        var avatar = Avatar(card.transform);

        var scores = Row("Scores", card.transform, new Vector2(0f, -95f), new Vector2(700f, 140f), 30f);
        var playerScore = ScorePill("PlayerScore", scores.transform, Red);
        var vs = Text("Vs", "VS", scores.transform, 64, Color.white);
        vs.rectTransform.sizeDelta = new Vector2(120f, 140f);
        var opponentScore = ScorePill("OpponentScore", scores.transform, Blue);

        var buttons = Row("Buttons", card.transform, new Vector2(0f, -275f), new Vector2(820f, 110f), 40f);
        var rematch = Button("RematchButton", "REMATCH", buttons.transform, Yellow, NavyDeep);
        var menu = Button("MenuButton", "MENU", buttons.transform, Purple, Color.white);

        if (!Wire(ui, screen, RectOf(card), title, playerScore, opponentScore, rematch, menu))
        {
            Object.DestroyImmediate(screen);
            return;
        }

        screen.SetActive(false);
        Undo.RegisterCreatedObjectUndo(screen, "Build Match End Screen");
        Selection.activeGameObject = screen;
        EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);

        EditorUtility.DisplayDialog("Match end screen",
            "Built under UI as 'MatchEndScreen' and wired into UIManager.\n\n"
            + $"The avatar {(avatar ? "shows the player's saved avatar" : "has no AvatarCatalog yet - create one with Tools > Ping Pong > Avatar Catalog")}.\n\n"
            + "It starts switched off; UIManager shows it when a match ends.", "OK");
    }

    private static bool Wire(UIManager ui, GameObject screen, RectTransform card, TextMeshProUGUI title,
                             TextMeshProUGUI playerScore, TextMeshProUGUI opponentScore, Button rematch, Button menu)
    {
        var so = new SerializedObject(ui);
        var fields = new (string name, Object value)[]
        {
            ("_matchEndScreen", screen), ("_matchEndCard", card), ("_matchEndTitle", title),
            ("_matchEndPlayerScore", playerScore), ("_matchEndOpponentScore", opponentScore),
            ("_matchEndRematchButton", rematch), ("_matchEndMenuButton", menu)
        };

        foreach (var (name, value) in fields)
        {
            var property = so.FindProperty(name);
            if (property == null)
            {
                EditorUtility.DisplayDialog("Match end screen",
                    $"UIManager has no '{name}' field. Let Unity finish compiling the scripts and run this again.", "OK");
                return false;
            }
            property.objectReferenceValue = value;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    // ---------------------------------------------------------------- pieces

    /// <summary>The player's avatar: Body tinted by nothing, Face on top, fed by PlayerAvatar.</summary>
    private static bool Avatar(Transform parent)
    {
        var body = Panel("Avatar", parent, Cream);
        Place(RectOf(body), new Vector2(0f, 110f), new Vector2(210f, 210f));

        var face = Panel("Face", body.transform, Color.white, rounded: false);
        Stretch(RectOf(face));
        face.GetComponent<Image>().preserveAspect = true;

        var guids = AssetDatabase.FindAssets("t:AvatarCatalog");
        var catalog = guids.Length > 0 ? AssetDatabase.LoadAssetAtPath<AvatarCatalog>(AssetDatabase.GUIDToAssetPath(guids[0])) : null;

        if (catalog == null)
        {
            face.GetComponent<Image>().enabled = false;      // a blank white square otherwise
            return false;
        }

        // Preview art in the editor; PlayerAvatar swaps in the saved choice at runtime.
        var bodyImage = body.GetComponent<Image>();
        var faceImage = face.GetComponent<Image>();
        bodyImage.sprite = catalog.Skin("", 0);
        bodyImage.color = Color.white;
        bodyImage.type = Image.Type.Simple;
        bodyImage.preserveAspect = true;
        faceImage.sprite = catalog.Expression("", 0);

        var playerAvatar = body.AddComponent<PlayerAvatar>();
        var so = new SerializedObject(playerAvatar);
        so.FindProperty("_catalog").objectReferenceValue = catalog;
        so.FindProperty("_body").objectReferenceValue = bodyImage;
        so.FindProperty("_face").objectReferenceValue = faceImage;
        so.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    private static TextMeshProUGUI ScorePill(string name, Transform parent, Color color)
    {
        var pill = Panel(name, parent, color);
        RectOf(pill).sizeDelta = new Vector2(190f, 140f);

        var value = Text("Value", "0", pill.transform, 96, Color.white);
        Stretch(value.rectTransform);
        return value;
    }

    private static Button Button(string name, string label, Transform parent, Color fill, Color textColor)
    {
        var go = Panel(name, parent, fill);
        RectOf(go).sizeDelta = new Vector2(340f, 110f);

        var button = go.AddComponent<Button>();
        button.targetGraphic = go.GetComponent<Image>();

        var text = Text("Label", label, go.transform, 48, textColor);
        Stretch(text.rectTransform);
        return button;
    }

    private static GameObject Row(string name, Transform parent, Vector2 position, Vector2 size, float spacing)
    {
        var row = new GameObject(name, typeof(RectTransform));
        row.transform.SetParent(parent, false);
        Place(RectOf(row), position, size);

        var layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        return row;
    }

    // --------------------------------------------------------------- helpers

    private static GameObject Panel(string name, Transform parent, Color color, bool rounded = true)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        var image = go.GetComponent<Image>();
        image.color = color;
        if (rounded)
        {
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;
        }
        return go;
    }

    private static TextMeshProUGUI Text(string name, string content, Transform parent, float size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var text = go.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = size;
        text.color = color;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }

    private static RectTransform RectOf(GameObject go) => go.GetComponent<RectTransform>();

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    /// <summary>Centre-anchored at <paramref name="position"/> from the parent's middle.</summary>
    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
