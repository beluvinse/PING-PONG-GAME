using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tools > Ping Pong > Color Palette. Keeps the game's colour codes inside Unity:
/// paste hex codes straight from Figma, copy them back out, apply one to the
/// selected UI, and publish the whole palette to the Swatches section of every
/// colour picker in the editor.
/// </summary>
public class ColorPaletteWindow : EditorWindow
{
    private const string DEFAULT_ASSET_PATH = "Assets/Settings/GamePalette.asset";

    // Colour pickers list every .colors library that sits in an Editor folder.
    private const string PICKER_LIBRARY_PATH = "Assets/Editor/GamePalette.colors";

    private static readonly char[] ClipboardSeparators = { ' ', '\n', '\r', '\t', ',', ';' };

    private ColorPalette _palette;
    private SerializedObject _serialized;
    private ReorderableList _list;
    private Vector2 _scroll;

    [MenuItem("Tools/Ping Pong/Color Palette")]
    private static void Open() => GetWindow<ColorPaletteWindow>("Color Palette");

    private void OnEnable() => Bind(FindPalette());

    private void OnFocus()
    {
        if (_palette == null) Bind(FindPalette());
    }

    // Edits go through SerializedObject, which only marks the asset dirty; write
    // it out whenever the window stops being used so nothing waits on Save Project.
    private void OnLostFocus() => Persist();
    private void OnDisable() => Persist();

    private void Persist()
    {
        if (_palette != null) AssetDatabase.SaveAssetIfDirty(_palette);
    }

    private static ColorPalette FindPalette()
    {
        var guid = AssetDatabase.FindAssets("t:ColorPalette").FirstOrDefault();
        return guid == null ? null : AssetDatabase.LoadAssetAtPath<ColorPalette>(AssetDatabase.GUIDToAssetPath(guid));
    }

    private void Bind(ColorPalette palette)
    {
        _palette = palette;
        if (palette == null)
        {
            _serialized = null;
            _list = null;
            return;
        }

        _serialized = new SerializedObject(palette);
        _list = new ReorderableList(_serialized, _serialized.FindProperty("_colors"), true, false, true, true)
        {
            elementHeight = EditorGUIUtility.singleLineHeight + 6f,
            drawElementCallback = DrawEntry,
            onAddCallback = _ => AddEntry("New Color", Color.white)
        };
    }

    private void OnGUI()
    {
        if (_palette == null)
        {
            DrawCreate();
            return;
        }

        _serialized.Update();

        DrawToolbar();
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        _list.DoLayoutList();
        EditorGUILayout.EndScrollView();

        _serialized.ApplyModifiedProperties();
    }

    private void DrawCreate()
    {
        EditorGUILayout.HelpBox("There is no color palette in the project yet.", MessageType.Info);
        if (!GUILayout.Button("Create Palette")) return;

        var asset = CreateInstance<ColorPalette>();
        AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath(DEFAULT_ASSET_PATH));
        AssetDatabase.SaveAssets();
        Bind(asset);
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button(new GUIContent("Paste Hex", "Adds every hex code on the clipboard, e.g. F5C842 or #F5C842."),
                             EditorStyles.toolbarButton))
            PasteFromClipboard();

        if (GUILayout.Button(new GUIContent("Update Color Picker Swatches", "Publishes the palette to the Swatches of every color picker."),
                             EditorStyles.toolbarButton))
            WritePickerLibrary();

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Select Asset", EditorStyles.toolbarButton))
        {
            Selection.activeObject = _palette;
            EditorGUIUtility.PingObject(_palette);
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawEntry(Rect rect, int index, bool isActive, bool isFocused)
    {
        var entry = _list.serializedProperty.GetArrayElementAtIndex(index);
        var nameProp = entry.FindPropertyRelative("name");
        var colorProp = entry.FindPropertyRelative("color");

        const float swatchWidth = 60f, hexWidth = 90f, buttonWidth = 48f, gap = 4f;
        rect.y += 3f;
        rect.height = EditorGUIUtility.singleLineHeight;
        var nameWidth = Mathf.Max(60f, rect.width - swatchWidth - hexWidth - buttonWidth * 2f - gap * 4f);

        var x = rect.x;
        Rect Next(float width)
        {
            var cell = new Rect(x, rect.y, width, rect.height);
            x += width + gap;
            return cell;
        }

        colorProp.colorValue = EditorGUI.ColorField(Next(swatchWidth), GUIContent.none, colorProp.colorValue,
                                                    showEyedropper: true, showAlpha: true, hdr: false);
        nameProp.stringValue = EditorGUI.TextField(Next(nameWidth), nameProp.stringValue);

        // Delayed, so a half-typed code doesn't turn the swatch black mid-edit.
        var hex = ToHex(colorProp.colorValue);
        var typed = EditorGUI.DelayedTextField(Next(hexWidth), hex);
        if (typed != hex && TryParseHex(typed, out var parsed))
            colorProp.colorValue = parsed;

        if (GUI.Button(Next(buttonWidth), new GUIContent("Copy", "Copy the hex code, e.g. to paste into Figma.")))
        {
            EditorGUIUtility.systemCopyBuffer = hex;
            ShowNotification(new GUIContent($"Copied {hex}"));
        }

        if (GUI.Button(Next(buttonWidth), new GUIContent("Apply", "Set this color on the selected Images, texts and SpriteRenderers.")))
            ApplyToSelection(colorProp.colorValue, nameProp.stringValue);
    }

    private void AddEntry(string colorName, Color color)
    {
        var colors = _serialized.FindProperty("_colors");
        colors.arraySize++;

        var entry = colors.GetArrayElementAtIndex(colors.arraySize - 1);
        entry.FindPropertyRelative("name").stringValue = colorName;
        entry.FindPropertyRelative("color").colorValue = color;
    }

    private void PasteFromClipboard()
    {
        var added = 0;
        foreach (var token in EditorGUIUtility.systemCopyBuffer.Split(ClipboardSeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!TryParseHex(token, out var color)) continue;

            // The code doubles as a name until it is renamed.
            AddEntry(ToHex(color), color);
            added++;
        }

        ShowNotification(new GUIContent(added > 0 ? $"Added {added} color(s)" : "No hex codes on the clipboard"));
    }

    private void ApplyToSelection(Color color, string colorName)
    {
        var changed = 0;

        foreach (var go in Selection.gameObjects)
        {
            // Graphic covers Image, RawImage, legacy Text and TextMeshPro text.
            foreach (var graphic in go.GetComponents<Graphic>())
            {
                Undo.RecordObject(graphic, "Apply Palette Color");
                graphic.color = color;
                PrefabUtility.RecordPrefabInstancePropertyModifications(graphic);
                changed++;
            }

            foreach (var sprite in go.GetComponents<SpriteRenderer>())
            {
                Undo.RecordObject(sprite, "Apply Palette Color");
                sprite.color = color;
                PrefabUtility.RecordPrefabInstancePropertyModifications(sprite);
                changed++;
            }
        }

        ShowNotification(new GUIContent(changed > 0
            ? $"'{colorName}' applied to {changed} component(s)"
            : "Select an Image, a text or a SpriteRenderer first"));
    }

    /// <summary>
    /// Writes the palette as a Unity color preset library. Same layout as the
    /// editor's own Default.colors; the fileID is Unity's ColorPresetLibrary.
    /// </summary>
    private void WritePickerLibrary()
    {
        _serialized.ApplyModifiedProperties();

        var yaml = new StringBuilder()
            .Append("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &1\nMonoBehaviour:\n")
            .Append("  m_ObjectHideFlags: 52\n")
            .Append("  m_CorrespondingSourceObject: {fileID: 0}\n")
            .Append("  m_PrefabInstance: {fileID: 0}\n")
            .Append("  m_PrefabAsset: {fileID: 0}\n")
            .Append("  m_GameObject: {fileID: 0}\n")
            .Append("  m_Enabled: 1\n")
            .Append("  m_EditorHideFlags: 0\n")
            .Append("  m_Script: {fileID: 12323, guid: 0000000000000000e000000000000000, type: 0}\n")
            .Append("  m_Name: \n")
            .Append("  m_EditorClassIdentifier: \n");

        if (_palette.Colors.Count == 0)
        {
            yaml.Append("  m_Presets: []\n");
        }
        else
        {
            yaml.Append("  m_Presets:\n");
            foreach (var entry in _palette.Colors)
            {
                var c = entry.color;
                yaml.Append("  - m_Name: ").Append(Quote(entry.name)).Append('\n')
                    .Append($"    m_Color: {{r: {Number(c.r)}, g: {Number(c.g)}, b: {Number(c.b)}, a: {Number(c.a)}}}\n");
            }
        }

        File.WriteAllText(PICKER_LIBRARY_PATH, yaml.ToString());
        AssetDatabase.ImportAsset(PICKER_LIBRARY_PATH);

        ShowNotification(new GUIContent($"{_palette.Colors.Count} swatches published - reopen any color picker"));
    }

    private static string ToHex(Color color) =>
        "#" + (color.a < 1f ? ColorUtility.ToHtmlStringRGBA(color) : ColorUtility.ToHtmlStringRGB(color));

    /// <summary>Accepts RRGGBB or RRGGBBAA, with or without the leading #.</summary>
    private static bool TryParseHex(string text, out Color color)
    {
        color = default;
        var code = (text ?? "").Trim().TrimStart('#');
        if ((code.Length != 6 && code.Length != 8) || !code.All(Uri.IsHexDigit)) return false;

        return ColorUtility.TryParseHtmlString("#" + code, out color);
    }

    private static string Number(float value) => value.ToString("0.#######", CultureInfo.InvariantCulture);

    // YAML single quotes: a name with ':' or '#' would otherwise break the file.
    private static string Quote(string value) => "'" + (value ?? "").Replace("'", "''") + "'";
}
