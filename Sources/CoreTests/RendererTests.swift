import Foundation
import CoreGraphics
import ImageIO
import RecordCore

/// Builds a solid white RGBA image.
private func whiteImage(_ w: Int, _ h: Int) -> CGImage {
    let ctx = CGContext(data: nil, width: w, height: h, bitsPerComponent: 8, bytesPerRow: 0,
                        space: CGColorSpaceCreateDeviceRGB(),
                        bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
    ctx.setFillColor(red: 1, green: 1, blue: 1, alpha: 1)
    ctx.fill(CGRect(x: 0, y: 0, width: w, height: h))
    return ctx.makeImage()!
}

/// Returns (r, g, b) at pixel (x, y) with a top-left origin.
private func pixel(_ image: CGImage, _ x: Int, _ y: Int) -> (Int, Int, Int) {
    let w = image.width, h = image.height
    var buf = [UInt8](repeating: 0, count: w * h * 4)
    let ctx = CGContext(data: &buf, width: w, height: h, bitsPerComponent: 8, bytesPerRow: w * 4,
                        space: CGColorSpaceCreateDeviceRGB(),
                        bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
    ctx.draw(image, in: CGRect(x: 0, y: 0, width: w, height: h))
    let i = (y * w + x) * 4  // bitmap memory rows run top to bottom
    return (Int(buf[i]), Int(buf[i + 1]), Int(buf[i + 2]))
}

private func isRed(_ p: (Int, Int, Int)) -> Bool { p.0 > 200 && p.1 < 80 && p.2 < 80 }

let rendererTests: [TestCase] = [
    ("testRenderHighlightPixels", {
        let base = whiteImage(200, 200)
        guard let out = renderHighlight(on: base, circleIn: CGRect(x: 50, y: 50, width: 100, height: 100), scale: 1) else {
            return expect(false, "renderHighlight returned nil")
        }
        expectEqual(out.width, 200); expectEqual(out.height, 200)
        let center = pixel(out, 100, 100)
        expect(center.0 > 200 && center.1 < 220 && center.2 < 220, "center tinted red: \(center)")
        let corner = pixel(out, 5, 5)
        expect(corner == (255, 255, 255), "corner untouched: \(corner)")
        expect(isRed(pixel(out, 100, 51)), "top stroke: \(pixel(out, 100, 51))")
        expect(isRed(pixel(out, 100, 148)), "bottom stroke: \(pixel(out, 100, 148))")
    }),
    ("testRenderHighlightOffCenterYFlip", {
        // Circle near the top of the image must stay near the top (top-left origin).
        let base = whiteImage(200, 200)
        let out = renderHighlight(on: base, circleIn: CGRect(x: 0, y: 0, width: 40, height: 40), scale: 1)!
        let topArea = pixel(out, 20, 20)
        expect(topArea.1 < 220, "top circle filled: \(topArea)")
        let bottomArea = pixel(out, 20, 180)
        expect(bottomArea == (255, 255, 255), "bottom untouched: \(bottomArea)")
    }),
    ("testPNGRoundTrip", {
        let img = whiteImage(30, 20)
        guard let data = pngData(from: img) else { return expect(false, "pngData nil") }
        expect(!data.isEmpty)
        let src = CGImageSourceCreateWithData(data as CFData, nil)!
        let back = CGImageSourceCreateImageAtIndex(src, 0, nil)!
        expectEqual(back.width, 30); expectEqual(back.height, 20)
    }),
]
