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

/// Global rect (points) to crop a screenshot to the window that was clicked, or nil for the
/// whole display (desktop, Dock, menu bar, or a window that nearly fills the screen).
/// Popups of the same app near the window (menus, popovers) are included for context.
public func captureCropRect(click: CGPoint, windows: [WindowSnapshot], displayFrame: CGRect,
                            margin: CGFloat = 8, popupReach: CGFloat = 200) -> CGRect? {
    let visible = windows.filter { $0.alpha > 0 }
    guard let hit = visible.first(where: { $0.bounds.contains(click) }) else { return nil }
    let base: WindowSnapshot
    if hit.layer == 0 {
        base = hit
    } else if hit.layer > 0, let main = visible.first(where: { $0.ownerPID == hit.ownerPID && $0.layer == 0 }) {
        base = main
    } else {
        return nil
    }
    let reach = base.bounds.insetBy(dx: -popupReach, dy: -popupReach)
    var rect = base.bounds
    for popup in visible where popup.ownerPID == base.ownerPID && popup.layer > 0 && popup.bounds.intersects(reach) {
        rect = rect.union(popup.bounds)
    }
    let clamped = rect.insetBy(dx: -margin, dy: -margin).intersection(displayFrame)
    guard !clamped.isNull, clamped.width * clamped.height < displayFrame.width * displayFrame.height * 0.95 else {
        return nil
    }
    return clamped
}

/// Converts a global crop rect to whole pixels of a screenshot of `displayFrame`.
public func pixelCropRect(crop: CGRect, displayFrame: CGRect, imagePixelSize: CGSize) -> CGRect {
    let scale = imagePixelSize.width / displayFrame.width
    let r = CGRect(x: (crop.minX - displayFrame.minX) * scale, y: (crop.minY - displayFrame.minY) * scale,
                   width: crop.width * scale, height: crop.height * scale)
    return r.integral.intersection(CGRect(origin: .zero, size: imagePixelSize))
}
