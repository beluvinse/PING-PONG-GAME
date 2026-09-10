/// <summary>
/// Inert state used while the main menu is shown: ignores bounces, hits and outs,
/// mirroring the pre-match situation where no state has been entered yet.
/// </summary>
public class IdleState : BaseMatchState
{
    public IdleState(MatchController match) : base(match)
    {
    }
}
