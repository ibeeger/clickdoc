import Foundation
import CoreGraphics
import RecordCore

private let display = CGRect(x: 0, y: 0, width: 1440, height: 900)
private func w(_ x: CGFloat, _ y: CGFloat, _ width: CGFloat, _ h: CGFloat, pid: Int32, layer: Int, alpha: Double = 1) -> WindowSnapshot {
    WindowSnapshot(bounds: CGRect(x: x, y: y, width: width, height: h), ownerPID: pid, ownerName: "p\(pid)", alpha: alpha, layer: layer)
}

let captureScopeTests: [TestCase] = [
    ("testCropToClickedWindowWithMargin", {
        let windows = [w(100, 100, 600, 400, pid: 7, layer: 0), w(0, 0, 1440, 900, pid: 8, layer: 0)]
        expectEqual(captureCropRect(click: CGPoint(x: 300, y: 300), windows: windows, displayFrame: display),
                    CGRect(x: 92, y: 92, width: 616, height: 416))
    }),
    ("testCropIncludesMenuOfSameApp", {
        // A context menu (layer 101) sticking out of the window's right edge.
        let windows = [w(650, 200, 200, 300, pid: 7, layer: 101), w(100, 100, 600, 400, pid: 7, layer: 0)]
        expectEqual(captureCropRect(click: CGPoint(x: 700, y: 250), windows: windows, displayFrame: display),
                    CGRect(x: 92, y: 92, width: 766, height: 416))
    }),
    ("testCropIgnoresFarAwayStatusItemOfSameApp", {
        let windows = [w(1300, 0, 30, 24, pid: 7, layer: 25), w(100, 100, 600, 400, pid: 7, layer: 0)]
        expectEqual(captureCropRect(click: CGPoint(x: 300, y: 300), windows: windows, displayFrame: display),
                    CGRect(x: 92, y: 92, width: 616, height: 416))
    }),
    ("testFullScreenForDockAndDesktop", {
        let dock = [w(300, 820, 800, 80, pid: 9, layer: 20)]
        expect(captureCropRect(click: CGPoint(x: 400, y: 850), windows: dock, displayFrame: display) == nil, "dock")
        let desktop = [w(0, 0, 1440, 900, pid: 10, layer: -2147483603)]
        expect(captureCropRect(click: CGPoint(x: 400, y: 400), windows: desktop, displayFrame: display) == nil, "desktop")
        expect(captureCropRect(click: CGPoint(x: 400, y: 400), windows: [], displayFrame: display) == nil, "nothing")
    }),
    ("testFullScreenWhenWindowNearlyFillsDisplay", {
        let windows = [w(0, 25, 1440, 875, pid: 7, layer: 0)]
        expect(captureCropRect(click: CGPoint(x: 400, y: 400), windows: windows, displayFrame: display) == nil)
    }),
    ("testCropClampsToDisplayAboveMain", {
        let upper = CGRect(x: 0, y: -1080, width: 1920, height: 1080)
        let windows = [w(-50, -1000, 800, 500, pid: 7, layer: 0)]
        expectEqual(captureCropRect(click: CGPoint(x: 100, y: -900), windows: windows, displayFrame: upper),
                    CGRect(x: 0, y: -1008, width: 758, height: 516))
    }),
    ("testCropSkipsInvisibleOverlay", {
        let windows = [w(0, 0, 1440, 900, pid: 3, layer: 0, alpha: 0), w(100, 100, 600, 400, pid: 7, layer: 0)]
        expectEqual(captureCropRect(click: CGPoint(x: 300, y: 300), windows: windows, displayFrame: display),
                    CGRect(x: 92, y: 92, width: 616, height: 416))
    }),
    ("testPixelCropRect", {
        // Retina: 2 px per point, display at negative origin.
        let r = pixelCropRect(crop: CGRect(x: -1900, y: -100, width: 100.25, height: 50),
                              displayFrame: CGRect(x: -1920, y: -200, width: 1920, height: 1080),
                              imagePixelSize: CGSize(width: 3840, height: 2160))
        expectEqual(r, CGRect(x: 40, y: 200, width: 201, height: 100))
    }),
    ("testCropIncludesClickedMenuFarFromWindow", {
        // Finder's menu-bar menu at top-left while its only window is at the right.
        let windows = [w(10, 24, 220, 300, pid: 7, layer: 101), w(800, 300, 500, 400, pid: 7, layer: 0)]
        let crop = captureCropRect(click: CGPoint(x: 50, y: 60), windows: windows, displayFrame: display)
        expect(crop?.contains(CGPoint(x: 50, y: 60)) == true, "click inside crop: \(String(describing: crop))")
        expect(crop?.contains(CGRect(x: 10, y: 24, width: 220, height: 300)) == true, "menu inside crop")
    }),
    ("testCropKeepsWholeHighlightNearWindowEdge", {
        let windows = [w(100, 100, 600, 400, pid: 7, layer: 0)]
        let crop = captureCropRect(click: CGPoint(x: 104, y: 104), windows: windows, displayFrame: display)!
        // The 60pt highlight circle around the click must fit.
        expect(crop.contains(CGRect(x: 74, y: 74, width: 60, height: 60)), "\(crop)")
    }),
    ("testCropPrefersWindowActuallyHitByMouse", {
        // A click-through overlay (another app) sits on top; the OS says window 42 got the click.
        var overlay = w(0, 0, 1000, 800, pid: 3, layer: 0)
        overlay.number = 1
        var target = w(100, 100, 600, 400, pid: 7, layer: 0)
        target.number = 42
        let crop = captureCropRect(click: CGPoint(x: 300, y: 300), windows: [overlay, target], displayFrame: display, hitWindowNumber: 42)
        expectEqual(crop, CGRect(x: 92, y: 92, width: 616, height: 416))
    }),
]
