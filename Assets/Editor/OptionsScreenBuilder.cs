using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the options screen hierarchy in the open scene: layout, sliders,
/// swatch rows and buttons, all wired to <see cref="OptionsMenu"/>.
///
/// Everything is plain coloured Images with no sprites, so the art can be
/// dropped straight onto them afterwards. Layout groups do the arranging, so
/// swapping a placeholder for a real sprite does not move anything else.
/// </summary>
public static class OptionsScreenBuilder
{
    private const int SKIN_COUNT = 6;
    private const int EXPRESSION_COUNT = 6;

    private static readonly Color Navy = new Color(0.13f, 0.18f, 0.44f);
    private static readonly Color NavyDeep = new Color(0.10f, 0.14f, 0.36f);
    private static readonly Color Row = new Color(0.16f, 0.22f, 0.50f);
    private static readonly Color Yellow = new Color(0.96f, 0.78f, 0.26f);
    private static readonly Color Cream = new Color(1f, 0.97f, 0.88f);
    private static readonly Color Purple = new Color(0.42f, 0.40f, 0.85f);
    private static readonly Color Track = new Color(0.08f, 0.11f, 0.28f);

    [MenuItem("Tools/Ping Pong/Build Options Screen")]
    private static void Build()
    {
        var canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Options screen",
                "No Canvas in the open scene. Open Menu.unity and run this again.", "OK");
            return;
        }

        var existing = canvas.transform.Find("OptionsScreen");
        if (existing != null &&
            !EditorUtility.DisplayDialog("Options screen",
                "An OptionsScreen already exists. Replace it?", "Replace", "Cancel"))
            return;

        if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);

        var screen = BuildScreen(canvas);
        Undo.RegisterCreatedObjectUndo(screen, "Build Options Screen");

        Selection.activeGameObject = screen;
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);

        EditorUtility.DisplayDialog("Options screen",
            "Built under the Canvas as 'OptionsScreen', wired and ready.\n\n"
            + "It starts switched off - call OptionsMenu.Open() from your menu button.", "OK");
    }

    private static GameObject BuildScreen(Canvas canvas)
    {
        // Full-screen dimmer so the screen reads as a modal over the menu.
        var screen = Panel("OptionsScreen", canvas.transform, new Color(0f, 0f, 0f, 0.5f));
        Stretch(screen.GetComponent<RectTransform>());

        var panel = Panel("Panel", screen.transform, Navy);
        var panelRect = panel.GetComponent<RectTransform>();
        Center(panelRect, 1240, 760);

        Title(panel.transform);

        var audioColumn = Column("AudioColumn", panel.transform, new Vector2(-300f, -30f), 540f);
        Pill("AUDIO", audioColumn.transform);
        var master = SliderRow("Master Volume", audioColumn.transform);
        var music = SliderRow("Music", audioColumn.transform);
        var sfx = SliderRow("SFX", audioColumn.transform);
        var mute = ToggleRow("Mute All", audioColumn.transform);

        var avatarColumn = Column("AvatarColumn", panel.transform, new Vector2(300f, -30f), 540f);
        Pill("PLAYER AVATAR", avatarColumn.transform);
        var (body, face) = Preview(avatarColumn.transform);
        Label("SKIN COLOR", avatarColumn.transform);
        var skin = SwatchRow("SkinSwatches", avatarColumn.transform, SKIN_COUNT, Cream);
        Label("EXPRESSION", avatarColumn.transform);
        var expression = SwatchRow("ExpressionSwatches", avatarColumn.transform, EXPRESSION_COUNT, Cream);

        var back = Button("BackButton", "BACK", panel.transform, Purple, Color.white);
        Anchor(back.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f),
               new Vector2(60f, 60f), new Vector2(320f, 92f), new Vector2(0f, 0f));

        var apply = Button("ApplyButton", "APPLY", panel.transform, Yellow, NavyDeep);
        Anchor(apply.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f),
               new Vector2(-60f, 60f), new Vector2(320f, 92f), new Vector2(1f, 0f));

        Wire(screen, master, music, sfx, mute, skin, expression, body, face, back, apply);

        screen.SetActive(false);
        return screen;
    }

    private static void Wire(GameObject screen, Slider master, Slider music, Slider sfx, Toggle mute,
                             OptionSwatchGroup skin, OptionSwatchGroup expression,
                             Image body, Image face, Button back, Button apply)
    {
        var menu = screen.AddComponent<OptionsMenu>();
        var so = new SerializedObject(menu);

        so.FindProperty("_screen").objectReferenceValue = screen;
        so.FindProperty("_masterSlider").objectReferenceValue = master;
        so.FindProperty("_musicSlider").objectReferenceValue = music;
        so.FindProperty("_sfxSlider").objectReferenceValue = sfx;
        so.FindProperty("_muteToggle").objectReferenceValue = mute;
        so.FindProperty("_skinGroup").objectReferenceValue = skin;
        so.FindProperty("_expressionGroup").objectReferenceValue = expression;
        so.FindProperty("_previewBody").objectReferenceValue = body;
        so.FindProperty("_previewFace").objectReferenceValue = face;
        so.FindProperty("_backButton").objectReferenceValue = back;
        so.FindProperty("_applyButton").objectReferenceValue = apply;

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------------------------------------------------------------- pieces

    private static void Title(Transform parent)
    {
        var title = Text("Title", "OPTIONS", parent, 96, Yellow, FontStyles.Bold);
        Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
               new Vector2(0f, -20f), new Vector2(800f, 140f), new Vector2(0.5f, 1f));
    }

    private static GameObject Column(string name, Transform parent, Vector2 position, float width)
    {
        var column = new GameObject(name, typeof(RectTransform));
        column.transform.SetParent(parent, false);

        var rect = column.GetComponent<RectTransform>();
        Anchor(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position,
               new Vector2(width, 560f), new Vector2(0.5f, 0.5f));

        var layout = column.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 18f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childAlignment = TextAnchor.UpperCenter;

        return column;
    }

    private static void Pill(string label, Transform parent)
    {
        var pill = Panel("Header", parent, Yellow);
        Fixed(pill, 76f);
        Round(pill);

        var text = Text("Label", label, pill.transform, 40, NavyDeep, FontStyles.Bold);
        Stretch(text.rectTransform);
    }

    private static void Label(string label, Transform parent)
    {
        var text = Text(label.Replace(" ", "") + "Label", label, parent, 30, Color.white, FontStyles.Bold);
        text.alignment = TextAlignmentOptions.Left;
        Fixed(text.gameObject, 40f);
    }

    private static Slider SliderRow(string label, Transform parent)
    {
        var row = Panel(label.Replace(" ", "") + "Row", parent, Row);
        Fixed(row, 108f);
        Round(row);

        var caption = Text("Label", label.ToUpperInvariant(), row.transform, 28, Color.white, FontStyles.Bold);
        caption.alignment = TextAlignmentOptions.Left;
        Anchor(caption.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
               new Vector2(96f, -14f), new Vector2(360f, 34f), new Vector2(0f, 1f));

        // Placeholder for the icon on the left of each row in the mockup.
        var icon = Panel("Icon", row.transform, Yellow);
        Anchor(icon.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
               new Vector2(20f, 0f), new Vector2(56f, 56f), new Vector2(0f, 0.5f));

        return SliderWidget(row.transform);
    }

    private static Slider SliderWidget(Transform parent)
    {
        var go = new GameObject("Slider", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Anchor(go.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f),
               new Vector2(0f, 30f), new Vector2(-116f, 26f), new Vector2(0f, 0f));
        var rect = go.GetComponent<RectTransform>();
        rect.offsetMin = new Vector2(96f, 18f);
        rect.offsetMax = new Vector2(-24f, 44f);

        var background = Panel("Background", go.transform, Track);
        Stretch(background.GetComponent<RectTransform>());
        Round(background);

        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(go.transform, false);
        var fillAreaRect = fillArea.GetComponent<RectTransform>();
        Stretch(fillAreaRect);

        var fill = Panel("Fill", fillArea.transform, Yellow);
        var fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.sizeDelta = new Vector2(20f, 0f);
        Round(fill);

        var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(go.transform, false);
        Stretch(handleArea.GetComponent<RectTransform>());

        var handle = Panel("Handle", handleArea.transform, Cream);
        var handleRect = handle.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0f, 0.5f);
        handleRect.anchorMax = new Vector2(0f, 0.5f);
        handleRect.sizeDelta = new Vector2(38f, 38f);
        Round(handle);

        var slider = go.AddComponent<Slider>();
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handle.GetComponent<Image>();
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;

        return slider;
    }

    private static Toggle ToggleRow(string label, Transform parent)
    {
        var row = Panel(label.Replace(" ", "") + "Row", parent, Row);
        Fixed(row, 92f);
        Round(row);

        var caption = Text("Label", label.ToUpperInvariant(), row.transform, 28, Color.white, FontStyles.Bold);
        caption.alignment = TextAlignmentOptions.Left;
        Anchor(caption.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
               new Vector2(96f, 0f), new Vector2(340f, 40f), new Vector2(0f, 0.5f));

        var icon = Panel("Icon", row.transform, Yellow);
        Anchor(icon.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
               new Vector2(20f, 0f), new Vector2(56f, 56f), new Vector2(0f, 0.5f));

        var track = Panel("Track", row.transform, Track);
        Anchor(track.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
               new Vector2(-24f, 0f), new Vector2(112f, 52f), new Vector2(1f, 0.5f));
        Round(track);

        // The knob doubles as the checkmark: on means it slides right, which the
        // Toggle does by simply showing this graphic.
        var knob = Panel("Knob", track.transform, Cream);
        Anchor(knob.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
               new Vector2(-6f, 0f), new Vector2(40f, 40f), new Vector2(1f, 0.5f));
        Round(knob);

        var off = Panel("Knob Off", track.transform, Cream);
        Anchor(off.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
               new Vector2(6f, 0f), new Vector2(40f, 40f), new Vector2(0f, 0.5f));
        Round(off);
        off.transform.SetSiblingIndex(0);

        var toggle = row.AddComponent<Toggle>();
        toggle.targetGraphic = track.GetComponent<Image>();
        toggle.graphic = knob.GetComponent<Image>();
        toggle.isOn = false;

        return toggle;
    }

    private static (Image body, Image face) Preview(Transform parent)
    {
        var holder = new GameObject("Preview", typeof(RectTransform));
        holder.transform.SetParent(parent, false);
        Fixed(holder, 230f);

        var body = Panel("Body", holder.transform, Cream);
        Anchor(body.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
               Vector2.zero, new Vector2(210f, 210f), new Vector2(0.5f, 0.5f));
        Round(body);

        var face = Panel("Face", body.transform, Color.white);
        Stretch(face.GetComponent<RectTransform>());
        face.GetComponent<Image>().enabled = false;      // nothing to show until art lands

        return (body.GetComponent<Image>(), face.GetComponent<Image>());
    }

    private static OptionSwatchGroup SwatchRow(string name, Transform parent, int count, Color fillColor)
    {
        var row = new GameObject(name, typeof(RectTransform));
        row.transform.SetParent(parent, false);
        Fixed(row, 92f);

        var layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 12f;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.childAlignment = TextAnchor.MiddleCenter;

        var group = row.AddComponent<OptionSwatchGroup>();
        var swatches = new List<(Button, Image, Image)>();

        for (var i = 0; i < count; i++)
        {
            var swatch = Panel($"Swatch{i}", row.transform, fillColor);
            var rect = swatch.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(76f, 76f);
            Round(swatch);

            // Ring sits behind the fill and is a little bigger, so it reads as
            // an outline without needing a sprite.
            var highlight = Panel("Highlight", swatch.transform, Yellow);
            Anchor(highlight.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                   Vector2.zero, new Vector2(88f, 88f), new Vector2(0.5f, 0.5f));
            Round(highlight);
            highlight.transform.SetSiblingIndex(0);
            highlight.GetComponent<Image>().enabled = false;   // only the Image toggles; the GameObject stays active

            var button = swatch.AddComponent<Button>();
            button.targetGraphic = swatch.GetComponent<Image>();

            swatches.Add((button, highlight.GetComponent<Image>(), swatch.GetComponent<Image>()));
        }

        WriteSwatches(group, swatches);
        return group;
    }

    private static void WriteSwatches(OptionSwatchGroup group, List<(Button button, Image highlight, Image fill)> swatches)
    {
        var so = new SerializedObject(group);

        // The swatches are the row's own children, so new ones are picked up
        // just by duplicating one. The list below is the fallback.
        so.FindProperty("_container").objectReferenceValue = group.transform;

        var array = so.FindProperty("_swatches");
        array.arraySize = swatches.Count;

        for (var i = 0; i < swatches.Count; i++)
        {
            var element = array.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("button").objectReferenceValue = swatches[i].button;
            element.FindPropertyRelative("highlight").objectReferenceValue = swatches[i].highlight;
            element.FindPropertyRelative("fill").objectReferenceValue = swatches[i].fill;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // --------------------------------------------------------------- helpers

    private static GameObject Panel(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        return go;
    }

    private static Button Button(string name, string label, Transform parent, Color fill, Color textColor)
    {
        var go = Panel(name, parent, fill);
        Round(go);

        var button = go.AddComponent<Button>();
        button.targetGraphic = go.GetComponent<Image>();

        var text = Text("Label", label, go.transform, 44, textColor, FontStyles.Bold);
        Stretch(text.rectTransform);

        return button;
    }

    private static TextMeshProUGUI Text(string name, string content, Transform parent,
                                        float size, Color color, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var text = go.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = size;
        text.color = color;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;

        return text;
    }

    /// <summary>
    /// Unity's built-in rounded sprite, so the placeholders are not hard squares.
    /// Replacing it with real art is just dropping a sprite on the Image.
    /// </summary>
    private static void Round(GameObject go)
    {
        var image = go.GetComponent<Image>();
        if (image == null) return;

        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        image.type = Image.Type.Sliced;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Center(RectTransform rect, float width, float height)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void Anchor(RectTransform rect, Vector2 min, Vector2 max,
                               Vector2 position, Vector2 size, Vector2 pivot)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    /// <summary>Pins a fixed height inside a vertical layout group.</summary>
    private static void Fixed(GameObject go, float height)
    {
        var element = go.AddComponent<LayoutElement>();
        element.minHeight = height;
        element.preferredHeight = height;
    }
}
