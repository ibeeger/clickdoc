import Foundation
import CoreGraphics
import ImageIO
import RecordCore

private func white(_ w: Int, _ h: Int) -> CGImage {
    let ctx = CGContext(data: nil, width: w, height: h, bitsPerComponent: 8, bytesPerRow: 0,
                        space: CGColorSpaceCreateDeviceRGB(),
                        bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
    ctx.setFillColor(red: 1, green: 1, blue: 1, alpha: 1)
    ctx.fill(CGRect(x: 0, y: 0, width: w, height: h))
    return ctx.makeImage()!
}

/// Decoded RGBA pixels with a top-left origin; decode once, read many.
private struct Pixels {
    let w: Int, h: Int, buf: [UInt8]
    init(_ image: CGImage) {
        w = image.width; h = image.height
        var b = [UInt8](repeating: 0, count: w * h * 4)
        let ctx = CGContext(data: &b, width: w, height: h, bitsPerComponent: 8, bytesPerRow: w * 4,
                            space: CGColorSpaceCreateDeviceRGB(),
                            bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
        ctx.draw(image, in: CGRect(x: 0, y: 0, width: w, height: h))
        buf = b
    }
    func at(_ x: Int, _ y: Int) -> (Int, Int, Int) {
        let i = (y * w + x) * 4
        return (Int(buf[i]), Int(buf[i + 1]), Int(buf[i + 2]))
    }
    func redCount(in rect: CGRect) -> Int {
        var n = 0
        for y in Int(rect.minY)..<Int(rect.maxY) { for x in Int(rect.minX)..<Int(rect.maxX) where isRed(at(x, y)) { n += 1 } }
        return n
    }
}

private func px(_ image: CGImage, _ x: Int, _ y: Int) -> (Int, Int, Int) { Pixels(image).at(x, y) }
private func isRed(_ p: (Int, Int, Int)) -> Bool { p.0 > 180 && p.1 < 90 && p.2 < 90 }
private func isWhite(_ p: (Int, Int, Int)) -> Bool { p.0 > 245 && p.1 > 245 && p.2 > 245 }
private func redCount(_ image: CGImage, in rect: CGRect) -> Int { Pixels(image).redCount(in: rect) }

let annotationTests: [TestCase] = [
    ("testRectangleAnnotationStrokesEdgeOnly", {
        // 1000px wide image: stroke ~ 2.8px. Box at (100,100)-(500,300), top-left origin.
        let a = Annotation(kind: .rectangle(CGRect(x: 0.1, y: 0.2, width: 0.4, height: 0.4)))
        let out = renderAnnotations(on: white(1000, 500), [a])!
        expect(isRed(px(out, 300, 100)), "top edge \(px(out, 300, 100))")
        expect(isRed(px(out, 100, 200)), "left edge")
        expect(isRed(px(out, 300, 300)), "bottom edge")
        expect(isWhite(px(out, 300, 200)), "interior untouched")
        expect(isWhite(px(out, 300, 450)), "outside untouched")
    }),
    ("testArrowAnnotationDrawsShaftAndHead", {
        let a = Annotation(kind: .arrow(from: CGPoint(x: 0.1, y: 0.5), to: CGPoint(x: 0.9, y: 0.5)))
        let out = renderAnnotations(on: white(1000, 400), [a])!
        expect(isRed(px(out, 300, 200)), "shaft")
        // The head is much wider than the shaft just before the tip.
        expect(isRed(px(out, 880, 190)) && isRed(px(out, 880, 210)), "head width")
        expect(!isRed(px(out, 300, 190)), "shaft is thin")
        expect(isWhite(px(out, 50, 200)), "nothing before the start")
    }),
    ("testTextAnnotationDrawsNearItsOrigin", {
        let a = Annotation(kind: .text(origin: CGPoint(x: 0.1, y: 0.1), string: "点这里"))
        let out = renderAnnotations(on: white(1000, 600), [a])!
        expect(redCount(out, in: CGRect(x: 90, y: 50, width: 200, height: 70)) > 50, "text drawn at top-left area")
        expectEqual(redCount(out, in: CGRect(x: 0, y: 300, width: 1000, height: 300)), 0)
    }),
    ("testAnnotationsNoneReturnsImage", {
        let img = white(20, 10)
        expectEqual(renderAnnotations(on: img, [])?.width, 20)
    }),
    ("testExportImageAppliesAnnotations", {
        var s = Step(description: "x", pngData: pngData(from: white(1000, 500))!)
        s.annotations = [Annotation(kind: .rectangle(CGRect(x: 0.25, y: 0.25, width: 0.5, height: 0.5)))]
        let data = try exportImageData(for: s)
        let img = CGImageSourceCreateImageAtIndex(CGImageSourceCreateWithData(data as CFData, nil)!, 0, nil)!
        expect(isRed(px(img, 500, 125)), "box burned into export: \(px(img, 500, 125))")
        expect(isWhite(px(img, 500, 250)), "box interior untouched")
    }),
    ("testTutorialSetAnnotationsAndDraftRoundTrip", {
        let dir = FileManager.default.temporaryDirectory.appendingPathComponent("rtd-ann-\(UUID().uuidString)")
        defer { try? FileManager.default.removeItem(at: dir) }
        let s = Step(description: "x", pngData: Data([1]))
        var t = Tutorial(steps: [s])
        let anns = [Annotation(kind: .rectangle(CGRect(x: 0, y: 0, width: 0.5, height: 0.5))),
                    Annotation(kind: .arrow(from: .zero, to: CGPoint(x: 1, y: 1))),
                    Annotation(kind: .text(origin: CGPoint(x: 0.2, y: 0.3), string: "提示"))]
        t.setAnnotations(id: s.id, anns)
        expectEqual(t.steps[0].annotations, anns)
        try DraftStore(directory: dir).save(t, hasUnexportedChanges: true)
        expectEqual(try DraftStore(directory: dir).load()?.tutorial, t)
    }),
    ("testDraftWithoutAnnotationsFieldStillLoads", {
        // Drafts saved by 0.4.0 have no "annotations" key.
        let dir = FileManager.default.temporaryDirectory.appendingPathComponent("rtd-ann-\(UUID().uuidString)")
        defer { try? FileManager.default.removeItem(at: dir) }
        let id = UUID()
        try FileManager.default.createDirectory(at: dir.appendingPathComponent("images"), withIntermediateDirectories: true)
        try Data([1]).write(to: dir.appendingPathComponent("images/\(id.uuidString).png"))
        let json = #"{"version":1,"title":"旧","hasUnexportedChanges":true,"steps":[{"id":"\#(id.uuidString)","description":"a","capturedAt":0,"blurRegions":[]}]}"#
        try Data(json.utf8).write(to: dir.appendingPathComponent("manifest.json"))
        let loaded = try DraftStore(directory: dir).load()
        expectEqual(loaded?.tutorial.steps.first?.annotations, [])
    }),
]
