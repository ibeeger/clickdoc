namespace ClickDoc.Core.Tests;

public class ClicksTests
{
    static StepDecision New(ClickAction a) => new(StepDecisionKind.NewStep, a);

    [Fact]
    public void DecideStep()
    {
        Assert.Equal(New(ClickAction.RightClick), Steps.Decide(MouseButton.Right, 1, false));
        Assert.Equal(New(ClickAction.RightClick), Steps.Decide(MouseButton.Right, 2, true));
        Assert.Equal(New(ClickAction.Click), Steps.Decide(MouseButton.Left, 1, true));
        Assert.Equal(New(ClickAction.Click), Steps.Decide(MouseButton.Left, 0, false));
        Assert.Equal(StepDecisionKind.UpgradeLastToDoubleClick, Steps.Decide(MouseButton.Left, 2, true).Kind);
        Assert.Equal(New(ClickAction.DoubleClick), Steps.Decide(MouseButton.Left, 2, false));
        Assert.Equal(StepDecisionKind.Ignore, Steps.Decide(MouseButton.Left, 3, true).Kind);
    }

    [Fact]
    public void ClickCounter_CountsLeftWithinTimeAndSlop()
    {
        var c = new ClickCounter(500, 4, 4);
        Assert.Equal(1, c.Register(MouseButton.Left, new(100, 100), 0));
        Assert.Equal(2, c.Register(MouseButton.Left, new(101, 101), 300));
        Assert.Equal(3, c.Register(MouseButton.Left, new(101, 101), 600));
        Assert.Equal(1, c.Register(MouseButton.Left, new(110, 100), 700));  // outside the slop
        Assert.Equal(1, c.Register(MouseButton.Left, new(110, 100), 1300)); // too late
        Assert.Equal(1, c.Register(MouseButton.Right, new(110, 100), 1400));
        Assert.Equal(1, c.Register(MouseButton.Left, new(110, 100), 1500)); // a right click breaks the run
    }

    [Fact]
    public void CanUpgrade_ScalesDistance()
    {
        var t = DateTimeOffset.UnixEpoch;
        Assert.True(Steps.CanUpgradeLastStep(new(0, 0), t, new(7, 0), t.AddMilliseconds(400), 500, 1.5f));
        Assert.False(Steps.CanUpgradeLastStep(new(0, 0), t, new(8, 0), t.AddMilliseconds(400), 500, 1.5f));
        Assert.False(Steps.CanUpgradeLastStep(new(0, 0), t, new(0, 0), t.AddMilliseconds(701), 500, 1f));
        Assert.False(Steps.CanUpgradeLastStep(null, null, new(0, 0), t, 500, 1f));
    }
}
