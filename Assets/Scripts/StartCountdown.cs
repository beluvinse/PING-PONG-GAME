using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// "3, 2, 1, GO!" as four images shown one after another on a single Image,
/// driven by one DOTween sequence. Each step pops in, holds, and pops out; when
/// the last one clears, the finished callbacks fire so the match can start.
/// Drop it on the countdown object and hook <see cref="onFinished"/>.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class StartCountdown : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Leave empty to use an Image on this object or its children.")]
    [SerializeField] private Image _display;

    [Header("Steps")]
    [Tooltip("Shown in order: 3, 2, 1 and the GO! image last. An empty slot is a blank beat.")]
    [SerializeField] private Sprite[] _steps = new Sprite[4];
    [Tooltip("Each image takes its own pixel size, so a wide GO! is not squeezed into the same box as a 3.")]
    [SerializeField] private bool _useNativeSize = true;
    [Tooltip("Multiplies that native size, to resize every step at once.")]
    [SerializeField] private float _nativeSizeScale = 1f;

    [Header("Timing (seconds, per step)")]
    [Tooltip("Waits for the scene wipe to finish before counting.")]
    [SerializeField] private bool _waitForSceneTransition = true;
    [SerializeField] private float _startDelay = 0.15f;
    [SerializeField] private float _popIn = 0.25f;
    [SerializeField] private float _hold = 0.35f;
    [SerializeField] private float _popOut = 0.2f;

    [Header("Motion")]
    [SerializeField] private float _fromScale = 0.4f;
    [SerializeField] private float _toScale = 1.7f;
    [SerializeField] private Ease _popInEase = Ease.OutBack;
    [SerializeField] private Ease _popOutEase = Ease.InBack;
    [Tooltip("On means the countdown still runs while the game is frozen (timeScale 0).")]
    [SerializeField] private bool _useUnscaledTime = true;

    /// <summary>Named subclass so the sprite event shows up in the Inspector.</summary>
    [Serializable] public class StepEvent : UnityEvent<Sprite> { }

    [Header("Events")]
    [Tooltip("Fires once per step, with the image about to be shown.")]
    public StepEvent onStep;
    [Tooltip("Fires when 'GO!' has cleared the screen.")]
    public UnityEvent onFinished;

    /// <summary>Same as <see cref="onFinished"/>, for wiring from code.</summary>
    public event Action Finished;

    /// <summary>True from the first number until 'GO!' clears.</summary>
    public bool IsRunning { get; private set; }

    /// <summary>How long a full run takes, start delay included.</summary>
    public float TotalDuration =>
        _startDelay + (_steps == null ? 0 : _steps.Length) * (_popIn + _hold + _popOut);

    private CanvasGroup _group;
    private RectTransform _rect;
    private Sequence _sequence;

    private void Awake()
    {
        _group = GetComponent<CanvasGroup>();
        _rect = (RectTransform)transform;
        if (_display == null) _display = GetComponentInChildren<Image>(true);

        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;
    }

    private void OnEnable() => StartCoroutine(PlayWhenReady());

    private void OnDisable()
    {
        // Tweens outlive a disabled object otherwise, and would pop the label
        // back up on the next scene.
        _sequence?.Kill();
        _sequence = null;
        IsRunning = false;
        if (_group != null) _group.alpha = 0f;
    }

    private IEnumerator PlayWhenReady()
    {
        if (_waitForSceneTransition)
            while (SceneTransition.IsBusy) yield return null;

        Play();
    }

    /// <summary>Restarts the countdown from the first step.</summary>
    public void Play()
    {
        if (_display == null)
        {
            Debug.LogWarning($"{name}: StartCountdown has no Image to show the steps on.", this);
            return;
        }

        _sequence?.Kill();
        IsRunning = true;
        _group.alpha = 0f;

        _sequence = DOTween.Sequence()
            .SetUpdate(_useUnscaledTime)      // keeps counting while timeScale is 0
            .SetId(this)
            .AppendInterval(_startDelay);

        for (var i = 0; i < _steps.Length; i++)
            AppendStep(_sequence, _steps[i], last: i == _steps.Length - 1);

        _sequence.OnComplete(() =>
        {
            IsRunning = false;
            _sequence = null;
            onFinished?.Invoke();
            Finished?.Invoke();
        });
    }

    /// <summary>Stops the countdown where it is, without firing the callbacks.</summary>
    public void Cancel()
    {
        _sequence?.Kill();
        _sequence = null;
        IsRunning = false;
        _group.alpha = 0f;
    }

    private void AppendStep(Sequence sequence, Sprite sprite, bool last)
    {
        // The callback swaps the sprite, so every step reuses the same Image
        // and the sequence stays one object instead of four.
        sequence.AppendCallback(() =>
        {
            _display.sprite = sprite;
            _display.enabled = sprite != null;           // an Image with no sprite draws a white box
            if (sprite != null && _useNativeSize)
            {
                _display.SetNativeSize();
                _display.rectTransform.sizeDelta *= _nativeSizeScale;
            }

            AudioManager.Play(last ? SoundId.CountdownGo : SoundId.CountdownTick);
            _rect.localScale = Vector3.one * _fromScale;
            onStep?.Invoke(sprite);
        });

        sequence.Append(_rect.DOScale(1f, _popIn).SetEase(_popInEase))
                .Join(_group.DOFade(1f, _popIn * 0.6f));

        sequence.AppendInterval(_hold);

        sequence.Append(_rect.DOScale(_toScale, _popOut).SetEase(_popOutEase))
                .Join(_group.DOFade(0f, _popOut));
    }
}
