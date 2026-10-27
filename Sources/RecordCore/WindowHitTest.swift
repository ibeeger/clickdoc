import CoreGraphics

/// One entry of CGWindowListCopyWindowInfo, in front-to-back order.
public struct WindowSnapshot: Equatable {
    public var bounds: CGRect
    public var ownerPID: Int32
    public var ownerName: String?
    public var alpha: Double
    public var layer: Int
    public var number: Int

    public init(bounds: CGRect, ownerPID: Int32, ownerName: String?, alpha: Double, layer: Int, number: Int = 0) {
        self.number = number
        self.bounds = bounds
        self.ownerPID = ownerPID
        self.ownerName = ownerName
        self.alpha = alpha
        self.layer = layer
    }
}

/// First visible window (front to back) containing the point, in global top-left coordinates.
public func topmostWindow(at point: CGPoint, in windows: [WindowSnapshot]) -> WindowSnapshot? {
    windows.first { $0.alpha > 0 && $0.bounds.contains(point) }
}

/// Converts a global CoreGraphics point (top-left origin, y down) to Cocoa screen
/// coordinates (bottom-left origin of the main display, y up).
public func cocoaPoint(fromGlobal point: CGPoint, mainDisplayHeight: CGFloat) -> CGPoint {
    CGPoint(x: point.x, y: mainDisplayHeight - point.y)
}
