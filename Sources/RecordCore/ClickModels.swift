import Foundation

public enum MouseButton: Equatable {
    case left, right
}

public enum ClickAction: Equatable {
    case click, doubleClick, rightClick
}

public enum StepDecision: Equatable {
    case newStep(ClickAction)
    case upgradeLastToDoubleClick
    case ignore
}

/// `clickCount` is the system click state (CGEvent `mouseEventClickState`).
public func decideStep(button: MouseButton, clickCount: Int, hasPreviousStep: Bool) -> StepDecision {
    switch button {
    case .right:
        return .newStep(.rightClick)
    case .left:
        if clickCount <= 1 { return .newStep(.click) }
        if clickCount == 2 { return hasPreviousStep ? .upgradeLastToDoubleClick : .newStep(.doubleClick) }
        return .ignore
    }
}
