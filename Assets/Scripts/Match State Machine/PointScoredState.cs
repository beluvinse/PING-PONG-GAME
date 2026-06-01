using System.Security.Cryptography;
using UnityEngine;

public class PointScoredState : BaseMatchState
{
    private readonly MatchController.Side _winner;

    public PointScoredState(MatchController match, MatchController.Side winner) : base(match)
    {
        _winner = winner;
    }

    public override void Enter()
    {
        if (_winner == MatchController.Side.Player)
            Match.playerScore++;
        else
            Match.aiScore++;

        Match.OnPointWon?.Invoke(_winner);

        if (Match.IsMatchOver())
        {
            Match.TransitionTo(new MatchOverState(Match, _winner));
            return;
        }

        Match.servesDone++;

        if (Match.servesDone >= 2)
        {
            Match.servesDone = 0;
            Match.server = GetOpponent(Match.server);
        }

        Match.TransitionTo(new ServingState(Match));
    }
}