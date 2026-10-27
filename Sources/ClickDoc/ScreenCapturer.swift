import CoreGraphics
import Foundation
import ScreenCaptureKit
import RecordCore

enum CaptureError: LocalizedError {
    case noDisplay, displayNotShareable, renderFailed
    var errorDescription: String? {
        switch self {
        case .noDisplay: return "点击位置不在任何显示器上"
        case .displayNotShareable: return "无法获取该显示器的画面"
        case .renderFailed: return "生成截图失败"
        }
    }
}

/// Captures the display under a point and burns in the click highlight, off the main thread.
actor ScreenCapturer {
    private var content: SCShareableContent?

    func refresh() async throws {
        content = try await SCShareableContent.excludingDesktopWindows(false, onScreenWindowsOnly: true)
    }

    func capture(displayContaining point: CGPoint) async throws -> (image: CGImage, displayFrame: CGRect) {
        var displayID: CGDirectDisplayID = 0
        var count: UInt32 = 0
        guard CGGetDisplaysWithPoint(point, 1, &displayID, &count) == .success, count > 0 else {
            throw CaptureError.noDisplay
        }
        if content == nil { try await refresh() }
        do {
            return try await shot(displayID)
        } catch {
            try await refresh()  // displays or apps may have changed since the cache was filled
            return try await shot(displayID)
        }
    }

    /// Full pipeline for one step: capture, optionally crop to `crop` (global points), draw highlight, encode PNG.
    /// `refine` runs right after the screenshot and may widen the crop (e.g. to a menu the click opened).
    func highlightedPNG(for click: CGPoint, crop initialCrop: CGRect? = nil,
                        refine: (@Sendable () -> CGRect?)? = nil) async throws -> Data {
        var (image, frame) = try await capture(displayContaining: click)
        var crop = initialCrop
        if let base = crop, let wider = refine?() { crop = base.union(wider).intersection(frame) }
        if let crop {
            let px = pixelCropRect(crop: crop, displayFrame: frame,
                                   imagePixelSize: CGSize(width: image.width, height: image.height))
            if let cropped = image.cropping(to: px) {
                image = cropped
                frame = crop
            }
        }
        let size = CGSize(width: image.width, height: image.height)
        let rect = highlightRect(click: click, displayFrame: frame, imagePixelSize: size)
        let scale = size.width / frame.width
        guard let marked = renderHighlight(on: image, circleIn: rect, scale: scale),
              let data = pngData(from: marked) else { throw CaptureError.renderFailed }
        return data
    }

    private func shot(_ displayID: CGDirectDisplayID) async throws -> (image: CGImage, displayFrame: CGRect) {
        guard let content, let display = content.displays.first(where: { $0.displayID == displayID }) else {
            throw CaptureError.displayNotShareable
        }
        let me = content.applications.filter { $0.processID == getpid() }
        let filter = SCContentFilter(display: display, excludingApplications: me, exceptingWindows: [])
        let config = SCStreamConfiguration()
        if let mode = CGDisplayCopyDisplayMode(displayID) {
            config.width = mode.pixelWidth
            config.height = mode.pixelHeight
        }
        config.showsCursor = true
        let image = try await SCScreenshotManager.captureImage(contentFilter: filter, configuration: config)
        return (image, CGDisplayBounds(displayID))
    }
}
