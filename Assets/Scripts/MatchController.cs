using System;
using System.Collections;
using UnityEngine;

public class MatchController : MonoBehaviour
{
    public enum Side { Player, AI }

    [Header("Score")]
    public int playerScore;
    public int aiScore;

    [Header("State")]
    public Side currentTurn;
    public Side lastHitter;
    public Side server;

    public bool ballBounced;
    public bool ballServed;
    public int bounceCount;
    public Side lastBounceSide;
    public int servesDone;

    public Action<Side> OnPointWon;
    public Action<Side> OnServerAnnounced;
    public Action<bool> OnBallServed;
    public Action OnRallyStarted;

    private BaseMatchState _currentState;
    
    private void Start()
    {
        RestartGame();
    }

    public void RestartGame()
    {
        server = Side.Player;
        playerScore = 0;
        aiScore = 0;
        servesDone = 0;
        OnServerAnnounced?.Invoke(server);
        TransitionTo(new ServingState(this));
    }
    

    public void TransitionTo(BaseMatchState newState)
    {
        _currentState?.Exit();
        _currentState = newState;
        _currentState.Enter();
    }

    public bool IsServer(Side side) => side == server;

    public void SetBallServed()
    {
        ballServed = true;
        OnBallServed?.Invoke(true);
    }

    public bool CanHit(Side side)
    {
        if (!ballServed && !isServeReady) return false;
        if (!ballServed) return true;
        if (side == lastHitter) return false;
        if (!ballBounced) return false;
        if (lastBounceSide != side) return false;
        if (currentTurn != side) return false;
        return true;
    }

    public void RegisterBounce(Side side) => _currentState?.OnBounce(side);
    public void RegisterHit(Side side) => _currentState?.OnHit(side);
    public void RegisterBallOut() => _currentState?.OnBallOut();

    public bool isServeReady { get; private set; }

    public void StartServeDelay(float delay, Action onReady)
    {
        isServeReady = false;
        StartCoroutine(ServeDelayRoutine(delay, onReady));
    }
    private IEnumerator ServeDelayRoutine(float delay, Action onReady)
    {
        yield return new WaitForSeconds(delay);
        isServeReady = true;
        onReady?.Invoke();
    }
    
    public Action<Side> OnMatchOver;

    public bool IsMatchOver()
    {
        const int pointsToWin = 5;

        bool playerReached =
            playerScore >= pointsToWin;

        bool aiReached =
            aiScore >= pointsToWin;

        if (!playerReached && !aiReached)
            return false;

        return playerScore - aiScore >= 2;
    }
}