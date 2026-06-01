public class MatchOverState : BaseMatchState
{
    private readonly MatchController.Side winner;

    public MatchOverState(MatchController match, MatchController.Side winner) : base(match)
    {
        this.winner = winner;
    }

    public override void Enter()
    {
        Match.OnMatchOver?.Invoke(winner);
    }
}