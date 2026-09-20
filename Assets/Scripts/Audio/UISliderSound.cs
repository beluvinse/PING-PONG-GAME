using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The tick a slider makes while it is dragged.
///
/// A slider reports a new value every frame the mouse moves, which would fire
/// dozens of sounds a second. Two things hold it back: the handle has to have
/// moved <see cref="_step"/> of the whole bar since the last tick, and the bank's
/// own Min Interval stops it firing faster than that even on a wild drag.
/// </summary>
[RequireComponent(typeof(Slider))]
public class UISliderSound : MonoBehaviour
{
    [SerializeField] private SoundId _tick = SoundId.SliderTick;

    [Range(0.01f, 0.5f)]
    [Tooltip("How far the handle has to travel between ticks, as a share of the whole bar.")]
    [SerializeField] private float _step = 0.05f;

    private Slider _slider;
    private float _lastTickAt;

    private void Awake()
    {
        _slider = GetComponent<Slider>();
        _lastTickAt = Normalised;
    }

    private void OnEnable()
    {
        _lastTickAt = Normalised;                            // reopening the screen is not a drag
        _slider.onValueChanged.AddListener(OnValueChanged);
    }

    private void OnDisable() => _slider.onValueChanged.RemoveListener(OnValueChanged);

    private void OnValueChanged(float value)
    {
        var now = Normalised;
        if (Mathf.Abs(now - _lastTickAt) < _step) return;

        _lastTickAt = now;
        AudioManager.Play(_tick);
    }

    /// <summary>The handle's place along the bar, 0 to 1, whatever the range is.</summary>
    private float Normalised =>
        Mathf.Approximately(_slider.maxValue, _slider.minValue)
            ? 0f
            : Mathf.InverseLerp(_slider.minValue, _slider.maxValue, _slider.value);
}
