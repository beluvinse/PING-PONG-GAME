using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class OptionsMenu : MonoBehaviour
{
    [Header("Screen")]
    [SerializeField] private GameObject _screen;

    [Header("Audio")]
    [SerializeField] private Slider _masterSlider;
    [SerializeField] private Slider _musicSlider;
    [SerializeField] private Slider _sfxSlider;

    [Header("Avatar")]
    [SerializeField] private OptionSwatchGroup _skinGroup;
    [SerializeField] private OptionSwatchGroup _expressionGroup;
    [SerializeField] private Image _previewBody;
    [SerializeField] private Image _previewFace;

    [Header("Buttons")]
    [SerializeField] private Button _backButton;
    [SerializeField] private Button _applyButton;

    private void Awake()
    {
        if (_screen == null) _screen = gameObject;
        
        HookUp();
    }

    private void OnEnable()
    {
        ReadFromOptions();
    }

    private void OnDisable()
    {
        GameOptions.Save();
    }

    /// <summary>Call this from the menu's Options button.</summary>
    public void Open()
    {
        _screen.SetActive(true);
        ReadFromOptions();
    }

    /// <summary>Raised after Back or Apply closes the screen. MenuNavigator listens.</summary>
    public event System.Action Closed;

    private void Close()
    {
        _screen.SetActive(false);
        Closed?.Invoke();
    }
    
    private void HookUp()
    {
        if (_masterSlider != null) _masterSlider.onValueChanged.AddListener(v => GameOptions.MasterVolume = v);
        if (_musicSlider != null) _musicSlider.onValueChanged.AddListener(v => GameOptions.MusicVolume = v);
        if (_sfxSlider != null) _sfxSlider.onValueChanged.AddListener(v => GameOptions.SfxVolume = v);

        if (_skinGroup != null) _skinGroup.SelectionChanged += OnSkinPicked;
        if (_expressionGroup != null) _expressionGroup.SelectionChanged += OnExpressionPicked;

        if (_applyButton != null) _applyButton.onClick.AddListener(Close);
        if (_backButton != null) _backButton.onClick.AddListener(Close);
    }

    private void ReadFromOptions()
    {
        // SetValueWithoutNotify, or the listeners above would write the value
        // straight back and the screen would fight itself while it opens.
        if (_masterSlider != null) _masterSlider.SetValueWithoutNotify(GameOptions.MasterVolume);
        if (_musicSlider != null) _musicSlider.SetValueWithoutNotify(GameOptions.MusicVolume);
        if (_sfxSlider != null) _sfxSlider.SetValueWithoutNotify(GameOptions.SfxVolume);
        
        if (_skinGroup != null) _skinGroup.Select(GameOptions.SkinIndex, notify: false);
        if (_expressionGroup != null) _expressionGroup.Select(GameOptions.ExpressionIndex, notify: false);

        RefreshPreview();
    }

    private void OnSkinPicked(int index)
    {
        GameOptions.SkinIndex = index;
        GameOptions.Save();
        RefreshPreview();
    }

    private void OnExpressionPicked(int index)
    {
        GameOptions.ExpressionIndex = index;
        GameOptions.Save();
        RefreshPreview();
    }

    private void RefreshPreview()
    {
        var skin = _skinGroup != null ? _skinGroup.Selected : GameOptions.SkinIndex;
        var expression = _expressionGroup != null ? _expressionGroup.Selected : GameOptions.ExpressionIndex;

        ShowSprite(_previewBody, SwatchSprite(_skinGroup, skin));
        ShowSprite(_previewFace, SwatchSprite(_expressionGroup, expression));
    }

    private static void ShowSprite(Image image, Sprite sprite)
    {
        if (image == null) return;

        image.sprite = sprite;
        image.enabled = sprite != null;
    }

    private static Sprite SwatchSprite(OptionSwatchGroup group, int index)
    {
        var fill = group != null ? group.FillAt(index) : null;
        return fill.sprite;
    }

    private void OnDestroy()
    {
        if (_skinGroup != null) _skinGroup.SelectionChanged -= OnSkinPicked;
        if (_expressionGroup != null) _expressionGroup.SelectionChanged -= OnExpressionPicked;
    }
}
