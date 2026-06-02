using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MatchController _matchController;
    [SerializeField] private GameObject _scoresMainPanel;
    [SerializeField] private GameObject _scoresSidePanel;
    [SerializeField] private GameObject _matchEndedPanel;
    [SerializeField] private GameObject _pausePanel;
    [SerializeField] private Image _scorerPanel;
    [SerializeField] private Color _redColor; 
    [SerializeField] private Color _blueColor;
    
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
    
    //TEXTS
    private const string PLAYER_SCORED_TEXT = "You scored!";
    private const string AI_SCORED_TEXT = "Opponent scored!";
    private const string VICTORY_TEXT = "VICTORY";
    private const string DEFEAT_TEXT = "DEFEAT";
    private const string PLAYER_SERVES_TEXT = "Your serve";
    private const string OPPONENT_SERVES_TEXT = "Opponent to serve";
    
    
    
    private void Awake()
    {
        _matchController.OnBallServed += OnBallServed;
        _matchController.OnServerAnnounced += OnServerAnnounced;
        _matchController.OnPointWon += OnPointWon;
        _matchController.OnRallyStarted += OnRallyStarted;
        _matchController.OnMatchOver += OnMatchOver;
        
        _rematchButton.onClick.AddListener(OnRematchClicked);
        _quitButton.onClick.AddListener(OnQuitClicked);
        
        _scoresMainPanel.SetActive(false);
        _scoresSidePanel.SetActive(false);
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
        UpdateScoreUI();
        SetGameText();
        _matchController.RestartGame();
        _matchEndedPanel.SetActive(false);
    }

    private void OnMatchOver(MatchController.Side winner)
    {
        _matchOverText.text = winner == MatchController.Side.Player ? VICTORY_TEXT : DEFEAT_TEXT;
        _matchEndedPanel.SetActive(true);
    }

    private void OnRallyStarted()
    {
        _scoresMainPanel.SetActive(true);
        _scoresSidePanel.SetActive(false);
    }

    private void OnPointWon(MatchController.Side scorerSide)
    {
        var newText = scorerSide.Equals(MatchController.Side.Player) ? PLAYER_SCORED_TEXT : AI_SCORED_TEXT;
        _scorerText.text = newText;
        StartCoroutine(HideGOAfterXSeconds(_scorerText.gameObject, 1.5f));
        UpdateScoreUI();
    }
    private IEnumerator HideGOAfterXSeconds(GameObject text, float seconds)
    {
        text.SetActive(true);
        yield return new WaitForSeconds(seconds);
        text.gameObject.SetActive(false);
    }

    private void OnServerAnnounced(MatchController.Side serverSide)
    {
        _serverText.text = serverSide.Equals(MatchController.Side.Player) ? PLAYER_SERVES_TEXT : OPPONENT_SERVES_TEXT;
        _scorerPanel.color = serverSide.Equals(MatchController.Side.Player) ? _redColor : _blueColor;
        StartCoroutine(HideGOAfterXSeconds(_scorerPanel.gameObject, 1.5f));
    }

    private void OnBallServed(bool ballServed)
    {
        if (!ballServed) return;
        _scoresMainPanel.SetActive(false);
        _scoresSidePanel.SetActive(true);
        _scorerText.gameObject.SetActive(false);
    }

    private void UpdateScoreUI()
    {
        foreach (var t in playerScoreText)
        {
            t.text = _matchController.playerScore.ToString();
        }
        foreach (var t in aiScoreText)
        {
            t.text = _matchController.aiScore.ToString();
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
