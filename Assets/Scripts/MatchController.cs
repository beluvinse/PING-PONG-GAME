using System;
using UnityEngine;

public class MatchController : MonoBehaviour
{
    public enum Side
    {
        Player,
        AI
    }

    [Header("Score")]
    public int playerScore;
    public int aiScore;

    [Header("State")]
    public Side currentTurn;
    public Side lastHitter;
    public Side server;

    public bool ballBounced;
    public bool ballServed;
    public bool waitingServeBounce;

    public int bounceCount;

    public Side lastBounceSide;

    public Action<bool> OnBallServed;

    public int servesDone { get; set; }


    private void Awake()
    {
        Cursor.visible = false;
    }

    private void Start()
    {
        StartRally(Side.Player);
    }

    public void StartRally(Side newServer)
    {
        server = newServer;

        lastHitter = newServer;

        currentTurn = newServer == Side.Player ? Side.AI : Side.Player;

        ballBounced = false;
        ballServed = false;
        waitingServeBounce = true;

        bounceCount = 0;
        
        OnBallServed?.Invoke(false);
    }

    public bool IsServer(Side side)
    {
        return side == server;
    }

    public void SetBallServed()
    {
        ballServed = true;
        OnBallServed?.Invoke(true);
    }

    public void RegisterBounce(Side side)
    {
        bounceCount++;

        ballBounced = true;
        lastBounceSide = side;
        
        if (waitingServeBounce)
        {
            if (side != server)
            {
                Side winner =
                    side == Side.Player
                        ? Side.AI
                        : Side.Player;

                AwardPoint(winner);
                return;
            }

            Debug.Log("VALID SERVE BOUNCE");

            waitingServeBounce = false;

            ballBounced = false;
            bounceCount = 0;

            return;
        }

        if (bounceCount >= 2)
        {
            Side winner =
                side == Side.Player
                    ? Side.AI
                    : Side.Player;

            AwardPoint(winner);
            return;
        }

        if (side != currentTurn)
        {
            Side winner =
                side == Side.Player
                    ? Side.AI
                    : Side.Player;

            AwardPoint(winner);
        }
    }

    public bool CanHit(Side side)
    {
        Debug.Log(
            $"CanHit {side} | " +
            $"ballServed:{ballServed} | " +
            $"lastHitter:{lastHitter} | " +
            $"currentTurn:{currentTurn} | " +
            $"ballBounced:{ballBounced} | " +
            $"lastBounceSide:{lastBounceSide}"
        );
        
        
        // saque
        if (!ballServed)
            return true;

        // no puede pegar dos veces seguidas
        if (side == lastHitter)
            return false;

        // tiene que haber picado
        if (!ballBounced)
            return false;

        // tiene que haber picado en SU lado
        if (lastBounceSide != side)
            return false;

        // tiene que ser su turno
        if (currentTurn != side)
            return false;

        return true;
    }

    public void RegisterHit(Side side)
    {
        // =========================
        // GOLPE ANTES DEL PIQUE
        // =========================

        if (!waitingServeBounce && !ballBounced)
        {
            Debug.Log(
                side + " HIT BEFORE BOUNCE"
            );

            Side winner =
                side == Side.Player
                    ? Side.AI
                    : Side.Player;

            AwardPoint(winner);

            return;
        }

        lastHitter = side;

        currentTurn =
            side == Side.Player
                ? Side.AI
                : Side.Player;

        ballBounced = false;
        bounceCount = 0;

        Debug.Log(side + " HIT BALL");
    }

    void AwardPoint(Side winner)
    {
        if (winner == Side.Player)
            playerScore++;
        else
            aiScore++;

        Debug.Log("POINT FOR: " + winner);

        Debug.Log(
            $"PLAYER: {playerScore} | AI: {aiScore}"
        );

        servesDone++;
        
        if (servesDone >= 2)
        {
            servesDone = 0;

            server = server == Side.Player
                    ? Side.AI
                    : Side.Player;
        }

        StartRally(server);
    }

    public void RegisterBallOut(Side checkCurrentSide)
    {
        Side winner = checkCurrentSide == Side.Player ? Side.AI : Side.Player;

        AwardPoint(winner);    }
}