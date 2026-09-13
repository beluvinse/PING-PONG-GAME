using DG.Tweening;
using UnityEngine;

public class UIPanelAnimations : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform _scoresMainPanel;
    [SerializeField] private RectTransform _gameEndedPanel;
    [Tooltip("The 'Server' object the Match Info clips faded. Needs a CanvasGroup.")]
    [SerializeField] private CanvasGroup _serverInfo;

    [Header("Anchored positions (from the clips)")]
    [SerializeField] private float _mainPanelShownY = 82.75f;
    [SerializeField] private float _mainPanelHiddenY = 300f;
    [SerializeField] private float _sidePanelShownX = -130f;
    [SerializeField] private float _sidePanelHiddenX = 170f;
    [SerializeField] private float _gameEndedShownY = -610f;
    [SerializeField] private float _gameEndedHiddenY = -1100f;
    [SerializeField] private float _titleShownY = -18f;
    [SerializeField] private float _titleHiddenY = 354f;

    [Header("Durations")]
    [Tooltip("ShowMainPanel / ShowSidePanel: 1s clip at 3x speed.")]
    [SerializeField] private float _panelSwapDuration = 0.333f;
    [Tooltip("StartRally: 1s clip at 2x speed.")]
    [SerializeField] private float _startRallyDuration = 0.5f;
    [Tooltip("MatchOver: 1.05s clip at 1x speed.")]
    [SerializeField] private float _matchOverDuration = 1f;

    [Header("Server info fade (Match Info clips)")]
    [SerializeField] private float _serverFadeInDelay = 0.5f;
    [SerializeField] private float _serverFadeInDuration = 0.367f;
    [SerializeField] private float _serverHoldDuration = 1.216f;
    [SerializeField] private float _serverFadeOutDuration = 0.75f;

    [Header("Easing")]
    [Tooltip("The clips used Unity's smooth auto tangents, which read as an ease in/out.")]
    [SerializeField] private Ease _slideEase = Ease.InOutSine;

    private Sequence _serverSequence;

    private void OnDestroy()
    {
        KillAll();
    }

    /// <summary>
    /// Leaves the end-of-match screen: the panel drops away
    /// and the score panel arrives. Both get switched off once they are gone.
    /// </summary>
    public void StartRally()
    {
        if (_scoresMainPanel != null)
        {
            _scoresMainPanel.gameObject.SetActive(true);
            Slide(_scoresMainPanel, y: _mainPanelShownY, duration: _startRallyDuration);
        }

        SlideOut(_gameEndedPanel, _gameEndedHiddenY);
    }

    /// <summary>
    /// End of the match: the panel rides up and overshoots before settling, and
    /// the title drops in and bounces - that is what the stepped keys in the
    /// MatchOver clip were doing by hand.
    /// </summary>
    public void MatchOver()
    {
        if (_gameEndedPanel == null) return;
        _gameEndedPanel.gameObject.SetActive(true);
        _gameEndedPanel.DOKill();
        _gameEndedPanel.anchoredPosition = WithY(_gameEndedPanel, _gameEndedHiddenY);
        _gameEndedPanel.DOAnchorPosY(_gameEndedShownY, _matchOverDuration).SetEase(Ease.OutBack);
    }

    /// <summary>Fades the server announcement in, holds it, fades it back out.</summary>
    public void ShowServerInfo()
    {
        if (_serverInfo == null) return;

        _serverSequence?.Kill();
        _serverInfo.alpha = 0f;

        _serverSequence = DOTween.Sequence()
            .AppendInterval(_serverFadeInDelay)
            .Append(_serverInfo.DOFade(1f, _serverFadeInDuration))
            .AppendInterval(_serverHoldDuration)
            .Append(_serverInfo.DOFade(0f, _serverFadeOutDuration))
            .OnComplete(() => _serverSequence = null);
    }

    /// <summary>
    /// Puts the end-of-match screen back up with no animation. Needed when
    /// leaving a match from the pause menu: <see cref="StartRally"/> parked the
    /// panel and the title off-screen, so just switching them on would show
    /// nothing.
    /// </summary>
    public void ShowGameEndedImmediate()
    {
        if (_gameEndedPanel != null)
        {
            _gameEndedPanel.DOKill();
            _gameEndedPanel.anchoredPosition = WithY(_gameEndedPanel, _gameEndedShownY);
            _gameEndedPanel.gameObject.SetActive(true);
        }
    }

    /// <summary>Snaps everything to its hidden state, no animation.</summary>
    public void ResetToHidden()
    {
        KillAll();
        if (_scoresMainPanel != null) _scoresMainPanel.anchoredPosition = WithY(_scoresMainPanel, _mainPanelHiddenY);
        if (_gameEndedPanel != null) _gameEndedPanel.anchoredPosition = WithY(_gameEndedPanel, _gameEndedHiddenY);
        if (_serverInfo != null) _serverInfo.alpha = 0f;
    }

    private void Slide(RectTransform rect, float duration, float? x = null, float? y = null)
    {
        if (rect == null) return;

        rect.DOKill();
        if (x.HasValue) rect.DOAnchorPosX(x.Value, duration).SetEase(_slideEase);
        if (y.HasValue) rect.DOAnchorPosY(y.Value, duration).SetEase(_slideEase);
    }

    private void SlideOut(RectTransform rect, float hiddenY)
    {
        if (rect == null) return;

        rect.DOKill();
        rect.DOAnchorPosY(hiddenY, _startRallyDuration)
            .SetEase(_slideEase)
            .OnComplete(() => rect.gameObject.SetActive(false));
    }

    private void KillAll()
    {
        _serverSequence?.Kill();
        _serverSequence = null;

        if (_scoresMainPanel != null) _scoresMainPanel.DOKill();
        if (_gameEndedPanel != null) _gameEndedPanel.DOKill();
        if (_serverInfo != null) _serverInfo.DOKill();
    }

    private static Vector2 WithX(RectTransform rect, float x) => new Vector2(x, rect.anchoredPosition.y);
    private static Vector2 WithY(RectTransform rect, float y) => new Vector2(rect.anchoredPosition.x, y);
}
