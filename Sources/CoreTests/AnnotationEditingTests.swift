import Foundation
import CoreGraphics
import RecordCore

private let canvas = CGSize(width: 1000, height: 500)
private let box = Annotation(kind: .rectangle(CGRect(x: 0.1, y: 0.2, width: 0.4, height: 0.4)))  // (100,100)-(500,300)
private let arrow = Annotation(kind: .arrow(from: CGPoint(x: 0.6, y: 0.1), to: CGPoint(x: 0.9, y: 0.1)))  // y=50, x 600..900
private let label = Annotation(kind: .text(origin: CGPoint(x: 0.6, y: 0.8), string: "提示"))  // top-left (600,400)
private let blur = [CGRect(x: 0.0, y: 0.0, width: 0.3, height: 0.3)]  // (0,0)-(300,150)

private func hit(_ x: CGFloat, _ y: CGFloat, selected: CanvasItem? = nil) -> CanvasHit? {
    hitTest(CGPoint(x: x, y: y), blurRegions: blur, annotations: [box, arrow, label], selected: selected, canvasSize: canvas)
}

let annotationEditingTests: [TestCase] = [
    ("testHitRectangleBorderNotInterior", {
        expectEqual(hit(300, 102), .item(.annotation(box.id)))
        expectEqual(hit(498, 200), .item(.annotation(box.id)))
        expectEqual(hit(300, 220), nil)
    }),
    ("testHitArrowNearShaft", {
        expectEqual(hit(750, 54), .item(.annotation(arrow.id)))
        expectEqual(hit(750, 80), nil)
    }),
    ("testHitTextBox", {
        expectEqual(hit(610, 410), .item(.annotation(label.id)))
        expectEqual(hit(610, 480), nil)
    }),
    ("testAnnotationsBeatBlurAndBlurHitsInside", {
        // (120,100) is on the box's top edge and inside the blur region.
        expectEqual(hit(120, 100), .item(.annotation(box.id)))
        expectEqual(hit(50, 50), .item(.blur(0)))
    }),
    ("testHandlesOnlyForSelection", {
        expectEqual(hit(500, 300, selected: .annotation(box.id)), .handle(.annotation(box.id), .resize))
        expectEqual(hit(900, 50, selected: .annotation(arrow.id)), .handle(.annotation(arrow.id), .arrowEnd))
        expectEqual(hit(600, 50, selected: .annotation(arrow.id)), .handle(.annotation(arrow.id), .arrowStart))
        expectEqual(hit(300, 150, selected: .blur(0)), .handle(.blur(0), .resize))
        expectEqual(hit(900, 50), .item(.annotation(arrow.id)))
    }),
    ("testTranslateClampsInsideImage", {
        let moved = translated(.rectangle(CGRect(x: 0.1, y: 0.2, width: 0.4, height: 0.4)), by: CGVector(dx: 0.9, dy: -0.5))
        expectEqual(moved, .rectangle(CGRect(x: 0.6, y: 0, width: 0.4, height: 0.4)))
        // The arrow keeps its shape: the whole move is limited by the endpoint that hits the edge first.
        let a = translated(.arrow(from: CGPoint(x: 0.5, y: 0.5), to: CGPoint(x: 0.8, y: 0.6)), by: CGVector(dx: 0.5, dy: 0))
        expectEqual(a, .arrow(from: CGPoint(x: 0.7, y: 0.5), to: CGPoint(x: 1.0, y: 0.6)))
        expectEqual(translated(.text(origin: CGPoint(x: 0.5, y: 0.5), string: "t"), by: CGVector(dx: -1, dy: 0.2)),
                    .text(origin: CGPoint(x: 0, y: 0.7), string: "t"))
    }),
    ("testResizeKeepsMinimumAndBounds", {
        let r = resizedRect(CGRect(x: 0.5, y: 0.5, width: 0.2, height: 0.2), by: CGVector(dx: 0.6, dy: -0.5),
                            minSize: CGSize(width: 0.01, height: 0.02))
        expectEqual(r, CGRect(x: 0.5, y: 0.5, width: 0.5, height: 0.02))
    }),
    ("testTextFrameScalesFromImagePixels", {
        // A 600px-wide image shown 120pt wide: text keeps its size relative to the image.
        let f = textFrame(origin: CGPoint(x: 0.5, y: 0.5), string: "提示", canvasSize: CGSize(width: 120, height: 60), imageWidth: 600)
        let px = textSize("提示", imageWidth: 600)
        expectEqual(f.size.width, px.width * 0.2)
        expectEqual(f.origin, CGPoint(x: 60, y: 30))
    }),
    ("testClampTextOriginKeepsTextInsideImage", {
        let size = CGSize(width: 1000, height: 500)
        let t = textSize("很长的一段提示文字", imageWidth: 1000)
        let o = clampTextOrigin(CGPoint(x: 0.99, y: 0.99), string: "很长的一段提示文字", imageSize: size)
        expectEqual(o.x, 1 - t.width / 1000)
        expectEqual(o.y, 1 - t.height / 500)
        expectEqual(clampTextOrigin(CGPoint(x: 0.1, y: 0.1), string: "a", imageSize: size), CGPoint(x: 0.1, y: 0.1))
    }),
    ("testHitTestUsesImageScaledText", {
        let a = Annotation(kind: .text(origin: CGPoint(x: 0.1, y: 0.1), string: "提示"))
        let canvas = CGSize(width: 120, height: 60)
        // At export scale the text is ~2.2% of the width tall; far below it must not hit.
        let far = CGPoint(x: 13, y: 6 + 30)
        expectEqual(hitTest(far, blurRegions: [], annotations: [a], selected: nil, canvasSize: canvas, imageWidth: 600), nil)
        expectEqual(hitTest(CGPoint(x: 13, y: 7), blurRegions: [], annotations: [a], selected: nil, canvasSize: canvas, imageWidth: 600),
                    .item(.annotation(a.id)))
    }),
    ("testDrawingToolsIgnoreBlurBodies", {
        let blur = [CGRect(x: 0, y: 0, width: 0.5, height: 0.5)]
        let p = CGPoint(x: 100, y: 100)
        let size = CGSize(width: 1000, height: 500)
        expectEqual(hitTest(p, blurRegions: blur, annotations: [], selected: nil, canvasSize: size), .item(.blur(0)))
        expectEqual(hitTest(p, blurRegions: blur, annotations: [], selected: nil, canvasSize: size, includeBlurBodies: false), nil)
    }),
]
