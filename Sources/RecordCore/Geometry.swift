import CoreGraphics

// Global coordinates: CoreGraphics global display space (points, origin at the
// main display's top-left, y down) — the space of CGEvent.location and CGDisplayBounds.
// Image pixel coordinates: origin at top-left.

public func displayIndex(containing point: CGPoint, in displayFrames: [CGRect]) -> Int? {
    displayFrames.firstIndex { $0.contains(point) }
}

public func highlightRect(click: CGPoint, displayFrame: CGRect, imagePixelSize: CGSize,
                          diameterPoints: CGFloat = 60) -> CGRect {
    let scale = imagePixelSize.width / displayFrame.width
    let cx = (click.x - displayFrame.minX) * scale
    let cy = (click.y - displayFrame.minY) * scale
    let d = diameterPoints * scale
    return CGRect(x: cx - d / 2, y: cy - d / 2, width: d, height: d)
}
