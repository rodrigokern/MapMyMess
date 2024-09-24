namespace MapMyMess;

internal partial class Constants
{
    public enum GraphDirection
    {
        LR,
        TD,
        RL,
        BT,
    }

    public enum ReductionAction
    {
        Color,
        Remove,
        None,
    }

    public enum GraphMode
    {
        Projects,
        Complete, // projects + dependencies
    }
}
