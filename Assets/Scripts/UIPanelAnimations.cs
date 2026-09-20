using DG.Tweening;
using UnityEngine;

public class UIPanelAnimations : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform _scoresMainPanel;
    [Tooltip("The FIRST TO 5 sign - the panel, not the text inside it.")]
    [SerializeField] private RectTransform _firstTo5Panel;
    [SerializeField] private RectTransform _gameEndedPanel;
    [Tooltip("The whole Server Info panel, border included. A CanvasGroup is added if it has none.")]
    [SerializeField] private RectTransform _serverInfo;

    [Header("Anchored positions")]
    [Tooltip("How far above its resting place each sign parks while hidden. Both rest wherever the scene puts them.")]
    [SerializeField] private float _signHiddenOffset = 420f;
    [SerializeField] private float _gameEndedShownY = -610f;
    [SerializeField] private float _gameEndedHiddenY = -1100f;

    [Header("Durations")]
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
    [Tooltip("Dropping in: OutBack lands with a small bounce.")]
    [SerializeField] private Ease _dropEase = Ease.OutBack;
    [Tooltip("Riding back up: InBack dips before it leaves.")]
    [SerializeField] private Ease _raiseEase = Ease.InBack;

    private Sequence _serverSequence;
    private CanvasGroup _serverGroup;
    private float _scoresRestY, _firstTo5RestY, _serverRestY;
    private bool _signsShown;

    private void Awake()
    {
        // Where the signs rest is read from the scene, so moving them in the editor
        // is all it takes. These used to be numbers copied from the animation clips,
        // which stayed behind when a panel moved and pushed it off the top.
        if (_scoresMainPanel != null) _scoresRestY = _scoresMainPanel.anchoredPosition.y;
        if (_firstTo5Panel != null) _firstTo5RestY = _firstTo5Panel.anchoredPosition.y;
        if (_serverInfo != null) _serverRestY = _serverInfo.anchoredPosition.y;

        // The border is on the panel itself, so the panel is what fades: fading only
        // the contents left the border sitting there.
        if (_serverInfo != null)
        {
            _serverGroup = _serverInfo.GetComponent<CanvasGroup>();
            if (_serverGroup == null) _serverGroup = _serverInfo.gameObject.AddComponent<CanvasGroup>();
            _serverGroup.alpha = 0f;                     // nothing to announce until a serve
        }
    }

    private void OnDestroy()
    {
        KillAll();
    }

    /// <summary>
    /// Leaves the end-of-match screen: the panel drops away and is switched off
    /// once it is gone. The signs are not touched here - a serve is announced the
    /// moment a match starts, which would put them on screen during the countdown.
    /// </summary>
    public void StartRally()
    {
        SlideOut(_gameEndedPanel, _gameEndedHiddenY);
    }

    /// <summary>
    /// Parks the score panel and the FIRST TO 5 sign above the screen with no
    /// animation. A match begins with them out of the way, and the countdown
    /// drops them in.
    /// </summary>
    public void ParkMatchSigns()
    {
        _signsShown = false;
        Park(_scoresMainPanel, _scoresRestY);
        Park(_firstTo5Panel, _firstTo5RestY);
    }

    /// <summary>
    /// Clears the court for the pause menu and brings it all back afterwards. Each
    /// piece leaves by the edge it is nearest: the signs ride up the way they came
    /// in, and the server panel, which sits low, drops out of the bottom.
    /// </summary>
    public void ShowHud(bool shown)
    {
        SlideServerInfo(shown);

        if (!shown)
        {
            // Not HideMatchSigns: a pause is not the match ending, so whether the
            // signs were on screen has to survive it.
            Raise(_scoresMainPanel, _scoresRestY, 0f);
            Raise(_firstTo5Panel, _firstTo5RestY, 0f);
            return;
        }

        // Pausing during the countdown must not let them in early.
        if (!_signsShown) return;

        Drop(_scoresMainPanel, _scoresRestY);
        Drop(_firstTo5Panel, _firstTo5RestY);
    }

    private void SlideServerInfo(bool shown)
    {
        if (_serverInfo == null) return;

        _serverInfo.DOKill();                                // the fade lives on the CanvasGroup, untouched
        _serverInfo.DOAnchorPosY(shown ? _serverRestY : _serverRestY - _signHiddenOffset, _startRallyDuration)
            .SetEase(shown ? _dropEase : _raiseEase)
            .SetUpdate(true);                                // pausing freezes time: this still has to move
    }

    /// <summary>Drops both signs from above into the place the scene gives them.</summary>
    public void ShowMatchSigns()
    {
        _signsShown = true;
        Drop(_scoresMainPanel, _scoresRestY);
        Drop(_firstTo5Panel, _firstTo5RestY);
    }

    /// <summary>
    /// Sends both signs back up out of the screen. The delay lets the last point
    /// be read before the scores leave.
    /// </summary>
    public void HideMatchSigns(float delay = 0f)
    {
        _signsShown = false;
        Raise(_scoresMainPanel, _scoresRestY, delay);
        Raise(_firstTo5Panel, _firstTo5RestY, delay);
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
        if (_serverGroup == null) return;

        _serverSequence?.Kill();
        _serverGroup.alpha = 0f;

        _serverSequence = DOTween.Sequence()
            .AppendInterval(_serverFadeInDelay)
            .Append(_serverGroup.DOFade(1f, _serverFadeInDuration))
            .AppendInterval(_serverHoldDuration)
            .Append(_serverGroup.DOFade(0f, _serverFadeOutDuration))
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
        ParkMatchSigns();
        if (_gameEndedPanel != null) _gameEndedPanel.anchoredPosition = WithY(_gameEndedPanel, _gameEndedHiddenY);
        if (_serverGroup != null) _serverGroup.alpha = 0f;
    }

    private void Park(RectTransform rect, float restY)
    {
        if (rect == null) return;

        rect.DOKill();
        rect.gameObject.SetActive(true);
        rect.localScale = Vector3.one;                   // a bounce cut short would have left it off
        rect.anchoredPosition = WithY(rect, restY + _signHiddenOffset);
    }

    private void Drop(RectTransform rect, float restY)
    {
        if (rect == null) return;

        // Down already, or on its way: the countdown ending and the ball being
        // placed land within a frame of each other, and every serve asks again.
        if (Mathf.Approximately(rect.anchoredPosition.y, restY) || DOTween.IsTweening(rect)) return;

        rect.DOKill();
        rect.gameObject.SetActive(true);
        rect.DOAnchorPosY(restY, _startRallyDuration).SetEase(_dropEase).SetUpdate(true);
    }

    private void Raise(RectTransform rect, float restY, float delay)
    {
        if (rect == null) return;

        rect.DOKill();
        rect.DOAnchorPosY(restY + _signHiddenOffset, _startRallyDuration)
            .SetDelay(delay)
            .SetEase(_raiseEase)
            .SetUpdate(true);
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
        if (_firstTo5Panel != null) _firstTo5Panel.DOKill();
        if (_gameEndedPanel != null) _gameEndedPanel.DOKill();
        if (_serverGroup != null) _serverGroup.DOKill();
    }

    private static Vector2 WithY(RectTransform rect, float y) => new Vector2(rect.anchoredPosition.x, y);
}
