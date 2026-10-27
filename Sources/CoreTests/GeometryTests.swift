import Foundation
import CoreGraphics
import RecordCore

let geometryTests: [TestCase] = [
    ("testDisplayIndex", {
        let displays = [CGRect(x: 0, y: 0, width: 1440, height: 900),
                        CGRect(x: -1920, y: -200, width: 1920, height: 1080)]
        expectEqual(displayIndex(containing: CGPoint(x: 100, y: 100), in: displays), 0)
        expectEqual(displayIndex(containing: CGPoint(x: -10, y: 0), in: displays), 1)
        expectEqual(displayIndex(containing: CGPoint(x: 5000, y: 0), in: displays), nil)
    }),
    ("testHighlightRectRetina", {
        let r = highlightRect(click: CGPoint(x: 100, y: 50),
                              displayFrame: CGRect(x: 0, y: 0, width: 1440, height: 900),
                              imagePixelSize: CGSize(width: 2880, height: 1800))
        expectEqual(r, CGRect(x: 140, y: 40, width: 120, height: 120))
    }),
    ("testHighlightRectOnNegativeOriginDisplay", {
        let r = highlightRect(click: CGPoint(x: -1820, y: -100),
                              displayFrame: CGRect(x: -1920, y: -200, width: 1920, height: 1080),
                              imagePixelSize: CGSize(width: 1920, height: 1080))
        expectEqual(r, CGRect(x: 70, y: 70, width: 60, height: 60))
    }),
]
