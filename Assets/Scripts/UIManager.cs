using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MatchController _matchController;
    [SerializeField] private GameObject _matchEndedPanel;
    [SerializeField] private GameObject _pausePanel;
    [SerializeField] private Image _scorerPanel;
    [SerializeField] private Color _redColor; 
    [SerializeField] private Color _blueColor;
    [SerializeField] private Animator _scoresAnimator;
    [SerializeField] private Animator _matchAnimator;
    
    [Header("Score UI")]
    [SerializeField] private TextMeshProUGUI[] playerScoreText;
    [SerializeField] private TextMeshProUGUI[] aiScoreText;
    [SerializeField] private TextMeshProUGUI _scorerText;
    [SerializeField] private TextMeshProUGUI _serverText;
    [SerializeField] private TextMeshProUGUI _gameText;
    [SerializeField] private TextMeshProUGUI _matchOverText;

    [Header("Buttons")]
    [SerializeField] private Button _rematchButton;
    [SerializeField] private Button _quitButton;
    
    private const string PLAYER_SCORED_TEXT = "You scored!";
    private const string AI_SCORED_TEXT = "Opponent scored!";
    private const string VICTORY_TEXT = "VICTORY";
    private const string DEFEAT_TEXT = "DEFEAT";
    private const string PLAYER_SERVES_TEXT = "Your serve";
    private const string OPPONENT_SERVES_TEXT = "Opponent to serve";
    private const string SCORES_ANIMATOR_SHOWMAINPANEL = "showMainPanel";
    private const string SCORES_ANIMATOR_MATCHOVER = "matchOver";
    
    
    private void Awake()
    {
        _matchController.OnBallServed += OnBallServed;
        _matchController.OnServerAnnounced += OnServerAnnounced;
        _matchController.OnPointWon += OnPointWon;
        _matchController.OnRallyStarted += OnRallyStarted;
        _matchController.OnMatchOver += OnMatchOver;
        
        _rematchButton.onClick.AddListener(OnRematchClicked);
        _quitButton.onClick.AddListener(OnQuitClicked);
       
        _matchEndedPanel.SetActive(false);
        _pausePanel.SetActive(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            TogglePause();
    }

    private void TogglePause()
    {
        _pausePanel.SetActive(!_pausePanel.activeSelf);
        Time.timeScale = _pausePanel.activeSelf ? 0 : 1;
    }

    private void OnQuitClicked()
    {
        UnityEditor.EditorApplication.isPlaying = false;
    }

    private void OnRematchClicked()
    {
        _matchController.playerScore = 0;
        _matchController.aiScore = 0;
        foreach (var t in playerScoreText)
            t.text = "0";
        foreach (var t in aiScoreText)
            t.text = "0";
        SetGameText();
        _matchController.RestartGame();
        _matchEndedPanel.SetActive(false);
    }

    private void OnMatchOver(MatchController.Side winner)
    {
        _matchOverText.text = winner == MatchController.Side.Player ? VICTORY_TEXT : DEFEAT_TEXT;
        _scoresAnimator.SetTrigger(SCORES_ANIMATOR_MATCHOVER);
        _matchEndedPanel.SetActive(true);
    }

    private void OnRallyStarted()
    {
        _scoresAnimator.SetBool(SCORES_ANIMATOR_SHOWMAINPANEL, true);
        _matchAnimator.SetTrigger("show");
    }
    
    private void OnPointWon(MatchController.Side scorerSide)
    {
        var newText = scorerSide.Equals(MatchController.Side.Player) ? PLAYER_SCORED_TEXT : AI_SCORED_TEXT;
        _scorerText.text = newText;
        
        StartCoroutine(TypeText(newText));
        StartCoroutine(UpdateScore(scorerSide, .5f));
    }
   
    private IEnumerator UpdateScore(MatchController.Side scoreSide, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (scoreSide == MatchController.Side.Player)
        {
            StartCoroutine(AnimateScore(playerScoreText[0], _matchController.playerScore.ToString()));
            playerScoreText[1].text = _matchController.playerScore.ToString();
        }
        else if (scoreSide == MatchController.Side.AI)
        {
            StartCoroutine(AnimateScore(aiScoreText[0], _matchController.aiScore.ToString()));
            aiScoreText[1].text = _matchController.aiScore.ToString();
        }
    }
    private IEnumerator AnimateScore(TextMeshProUGUI text, string newValue)
    {
        var rect = text.rectTransform;
        var originalPos = rect.anchoredPosition;
        var topPos = originalPos + Vector2.up * 40f;
        var duration = 0.15f;
        var t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            rect.anchoredPosition = Vector2.Lerp(originalPos, topPos, t / duration);
            yield return null;
        }
        text.text = newValue;
        t = 0;
        while (t < duration)
        {
            t += Time.deltaTime;
            rect.anchoredPosition = Vector2.Lerp(topPos, originalPos, t / duration);
            yield return null;
        }
        rect.anchoredPosition = originalPos;
    }
    private void OnServerAnnounced(MatchController.Side serverSide)
    {
        _serverText.text = serverSide.Equals(MatchController.Side.Player) ? PLAYER_SERVES_TEXT : OPPONENT_SERVES_TEXT;
        _scorerPanel.color = serverSide.Equals(MatchController.Side.Player) ? _redColor : _blueColor;
    }

    private void OnBallServed(bool ballServed)
    {
        if (!ballServed) return;
        _scoresAnimator.SetBool(SCORES_ANIMATOR_SHOWMAINPANEL, false);
        _scorerText.text = "";
    }

    private IEnumerator TypeText(string message)
    {
        _scorerText.text = message;

        _scorerText.maxVisibleCharacters = 0;

        while (_scorerText.maxVisibleCharacters < message.Length)
        {
            _scorerText.maxVisibleCharacters++;
            yield return new WaitForSeconds(0.05f);
        }
        
        yield return new WaitForSeconds(1.5f);
        
        while (_scorerText.maxVisibleCharacters > 0)
        {
            _scorerText.maxVisibleCharacters--;
            yield return new WaitForSeconds(0.03f);
        }
    }
    private void SetGameText()
    {
        _gameText.text = "FIRST TO 5";
    }

    private void OnDestroy()
    {
        _matchController.OnBallServed -= OnBallServed;
        _matchController.OnServerAnnounced -= OnServerAnnounced;
        _matchController.OnPointWon -= OnPointWon;
    }
}
