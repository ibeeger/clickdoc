using SkiaSharp;

namespace ClickDoc.Core;

public enum MouseButton { Left, Right }

public enum ClickAction { Click, DoubleClick, RightClick }

public enum StepDecisionKind { NewStep, UpgradeLastToDoubleClick, Ignore }

public readonly record struct StepDecision(StepDecisionKind Kind, ClickAction Action = ClickAction.Click);

public static class Steps
{
    public static StepDecision Decide(MouseButton button, int clickCount, bool hasPreviousStep)
    {
        if (button == MouseButton.Right) return new(StepDecisionKind.NewStep, ClickAction.RightClick);
        if (clickCount <= 1) return new(StepDecisionKind.NewStep, ClickAction.Click);
        if (clickCount == 2)
            return hasPreviousStep ? new(StepDecisionKind.UpgradeLastToDoubleClick) : new(StepDecisionKind.NewStep, ClickAction.DoubleClick);
        return new(StepDecisionKind.Ignore);
    }

    /// A second click may only upgrade the previous step if it landed on the same spot moments later.
    /// `scale` converts the 5-point radius to physical pixels.
    public static bool CanUpgradeLastStep(SKPointI? lastPos, DateTimeOffset? lastAt, SKPointI pos, DateTimeOffset at,
                                          int doubleClickMs, float scale)
    {
        if (lastPos is not { } last || lastAt is not { } lastTime) return false;
        float dx = pos.X - last.X, dy = pos.Y - last.Y, r = 5 * scale;
        return dx * dx + dy * dy <= r * r && (at - lastTime).TotalMilliseconds <= Math.Max(doubleClickMs, 500) + 200;
    }
}

/// Counts consecutive left clicks the way the system does, since the low-level mouse hook
/// does not report a click count. `slopX`/`slopY` are SM_CXDOUBLECLK/SM_CYDOUBLECLK.
public sealed class ClickCounter(int doubleClickMs, int slopX, int slopY)
{
    SKPointI _lastPos;
    long _lastMs;
    int _count;

    public int Register(MouseButton button, SKPointI pos, long timestampMs)
    {
        if (button != MouseButton.Left)
        {
            _count = 0;
            return 1;
        }
        bool continues = _count > 0
            && timestampMs - _lastMs <= doubleClickMs
            && Math.Abs(pos.X - _lastPos.X) <= slopX / 2
            && Math.Abs(pos.Y - _lastPos.Y) <= slopY / 2;
        _count = continues ? _count + 1 : 1;
        _lastPos = pos;
        _lastMs = timestampMs;
        return _count;
    }
}
