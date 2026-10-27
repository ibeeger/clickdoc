import Foundation
import CoreGraphics
import ImageIO
import RecordCore

/// Black/white vertical stripes, 2px wide: maximum high-frequency detail, like text.
private func stripes(_ w: Int, _ h: Int) -> CGImage {
    let ctx = CGContext(data: nil, width: w, height: h, bitsPerComponent: 8, bytesPerRow: 0,
                        space: CGColorSpaceCreateDeviceRGB(),
                        bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
    ctx.setFillColor(red: 1, green: 1, blue: 1, alpha: 1)
    ctx.fill(CGRect(x: 0, y: 0, width: w, height: h))
    ctx.setFillColor(red: 0, green: 0, blue: 0, alpha: 1)
    for x in stride(from: 0, to: w, by: 4) { ctx.fill(CGRect(x: x, y: 0, width: 2, height: h)) }
    return ctx.makeImage()!
}

/// Red channel values in a row band (top-left origin).
private func reds(_ image: CGImage, xs: Range<Int>, y: Int) -> [Int] {
    let w = image.width, h = image.height
    var buf = [UInt8](repeating: 0, count: w * h * 4)
    let ctx = CGContext(data: &buf, width: w, height: h, bitsPerComponent: 8, bytesPerRow: w * 4,
                        space: CGColorSpaceCreateDeviceRGB(),
                        bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
    ctx.draw(image, in: CGRect(x: 0, y: 0, width: w, height: h))
    return xs.map { Int(buf[(y * w + $0) * 4]) }
}

private func spread(_ v: [Int]) -> Int { (v.max() ?? 0) - (v.min() ?? 0) }

let blurTests: [TestCase] = [
    ("testBlurFlattensDetailInsideRegionOnly", {
        let img = stripes(400, 200)
        // Left half of the image, top-left origin, normalized.
        guard let out = applyBlur(to: img, regions: [CGRect(x: 0, y: 0, width: 0.5, height: 1)]) else {
            return expect(false, "applyBlur nil")
        }
        expectEqual(out.width, 400); expectEqual(out.height, 200)
        let inside = reds(out, xs: 60..<140, y: 100)
        expect(spread(inside) < 40, "inside should be flat, spread \(spread(inside))")
        let outside = reds(out, xs: 300..<340, y: 100)
        expect(spread(outside) > 200, "outside keeps stripes, spread \(spread(outside))")
    }),
    ("testBlurRegionUsesTopLeftOrigin", {
        let img = stripes(200, 200)
        // Top quarter only: bottom rows must keep their stripes.
        let out = applyBlur(to: img, regions: [CGRect(x: 0, y: 0, width: 1, height: 0.25)])!
        expect(spread(reds(out, xs: 80..<120, y: 20)) < 40, "top blurred")
        expect(spread(reds(out, xs: 80..<120, y: 180)) > 200, "bottom sharp")
    }),
    ("testBlurNoRegionsReturnsSameSize", {
        let img = stripes(50, 40)
        let out = applyBlur(to: img, regions: [])!
        expectEqual(spread(reds(out, xs: 0..<20, y: 10)) > 200, true)
    }),
    ("testBlurSigmaScalesWithWidthAndHasFloor", {
        expectEqual(blurSigma(imageWidth: 3000), 20)
        expectEqual(blurSigma(imageWidth: 240), 10)
    }),
    ("testNormalizeAndClampRect", {
        let size = CGSize(width: 200, height: 100)
        expectEqual(normalizedRect(CGRect(x: 50, y: 25, width: 100, height: 50), in: size),
                    CGRect(x: 0.25, y: 0.25, width: 0.5, height: 0.5))
        // Dragged past the edges: clamped into the image.
        expectEqual(normalizedRect(CGRect(x: -20, y: 90, width: 100, height: 40), in: size),
                    CGRect(x: 0, y: 0.9, width: 0.4, height: 0.1))
        expectEqual(denormalizedRect(CGRect(x: 0.25, y: 0.25, width: 0.5, height: 0.5), in: size),
                    CGRect(x: 50, y: 25, width: 100, height: 50))
    }),
    ("testTutorialSetBlurRegions", {
        let s = Step(description: "a", pngData: Data([1]))
        var t = Tutorial(steps: [s])
        expectEqual(t.steps[0].blurRegions, [])
        t.setBlurRegions(id: s.id, [CGRect(x: 0, y: 0, width: 0.5, height: 0.5)])
        expectEqual(t.steps[0].blurRegions, [CGRect(x: 0, y: 0, width: 0.5, height: 0.5)])
    }),
    ("testExportBurnsInBlur", {
        let fm = FileManager.default
        let root = fm.temporaryDirectory.appendingPathComponent("rtd-blur-\(UUID().uuidString)")
        try fm.createDirectory(at: root, withIntermediateDirectories: true)
        defer { try? fm.removeItem(at: root) }
        let png = pngData(from: stripes(200, 100))!
        var blurred = Step(description: "blur", pngData: png)
        blurred.blurRegions = [CGRect(x: 0, y: 0, width: 1, height: 1)]
        let plain = Step(description: "plain", pngData: png)
        let out = try exportTutorial(Tutorial(title: "B", steps: [blurred, plain]), to: root)
        let first = try Data(contentsOf: out.appendingPathComponent("images/step-01.png"))
        let second = try Data(contentsOf: out.appendingPathComponent("images/step-02.png"))
        expectEqual(second, png)
        let src = CGImageSourceCreateWithData(first as CFData, nil)!
        let img = CGImageSourceCreateImageAtIndex(src, 0, nil)!
        expectEqual(img.width, 200)
        expect(spread(reds(img, xs: 60..<140, y: 50)) < 40, "exported image is blurred")
    }),
]
