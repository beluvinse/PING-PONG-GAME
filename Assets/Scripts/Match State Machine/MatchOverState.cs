public class MatchOverState : BaseMatchState
{
    private readonly MatchController.Side winner;

    public MatchOverState(MatchController match, MatchController.Side winner) : base(match)
    {
        this.winner = winner;
    }

    public override void Enter()
    {
        Match.StartDelayMatchOver(2f, () => Match.OnMatchOver?.Invoke(winner));
    }
}