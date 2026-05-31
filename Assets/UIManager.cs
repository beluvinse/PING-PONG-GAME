using System;
using TMPro;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MatchController _matchController;

    [SerializeField] private GameObject _scoresMainPanel;
    [SerializeField] private GameObject _scoresSidePanel;
    
    [Header("Score UI")]
    [SerializeField] private TextMeshProUGUI[] playerScoreText;
    [SerializeField] private TextMeshProUGUI[] aiScoreText;
    [SerializeField] private TextMeshProUGUI _scorerText;
    [SerializeField] private TextMeshProUGUI _serverText;
    [SerializeField] private TextMeshProUGUI _gameText;

    private void Awake()
    {
        _matchController.OnBallServed += OnBallServed;
    }

    private void OnBallServed(bool ballServed)
    {
        UpdateScoreUI();
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

    private void OnPlayerScored()
    {
        //si scoreo el player
        var scorerPlayer = "You scored!";
        var scorerAI = "Opponent scored!";
        
        _scorerText.text = scorerPlayer;
    }

    private void SetGameText()
    {
        _gameText.text = "FIRST TO 5";
    }

    private void OnDestroy()
    {
        _matchController.OnBallServed -= OnBallServed;
    }
}
