import Foundation
import CoreGraphics
import ImageIO
import RecordCore

private func pngOf(width: Int, height: Int) -> Data {
    let ctx = CGContext(data: nil, width: width, height: height, bitsPerComponent: 8, bytesPerRow: 0,
                        space: CGColorSpaceCreateDeviceRGB(),
                        bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
    ctx.setFillColor(red: 0, green: 0, blue: 1, alpha: 1)
    ctx.fill(CGRect(x: 0, y: 0, width: width, height: height))
    return pngData(from: ctx.makeImage()!)!
}

let fixTests: [TestCase] = [
    ("testElementNameIgnoresValueOfTextInputs", {
        let doc = AXFields(role: "AXTextArea", title: nil, description: nil, value: "第一行正文内容很长", help: nil,
                           roleDescription: "文本区域")
        expectEqual(pickElementName(doc, parent: nil), "文本区域")
        let field = AXFields(role: "AXTextField", title: nil, description: nil, value: "secret", help: nil,
                             roleDescription: "文本栏")
        expectEqual(pickElementName(field, parent: nil), "文本栏")
    }),
    ("testElementNameUsesValueOfStaticText", {
        let label = AXFields(role: "AXStaticText", title: nil, description: nil, value: "新建标签页", help: nil,
                             roleDescription: "文本")
        expectEqual(pickElementName(label, parent: nil), "新建标签页")
    }),
    ("testElementNamePriorityAndParentFallback", {
        let button = AXFields(role: "AXButton", title: "确定", description: "OK", value: nil, help: nil, roleDescription: "按钮")
        expectEqual(pickElementName(button, parent: nil), "确定")
        let image = AXFields(role: "AXImage", title: nil, description: nil, value: nil, help: nil, roleDescription: "图像")
        let parent = AXFields(role: "AXButton", title: nil, description: "关闭", value: nil, help: nil, roleDescription: "按钮")
        expectEqual(pickElementName(image, parent: parent), "关闭")
        let blank = AXFields(role: "AXGroup", title: "  ", description: nil, value: nil, help: nil, roleDescription: nil)
        expectEqual(pickElementName(blank, parent: nil), nil)
    }),
    ("testTopmostWindowAtPoint", {
        let windows = [
            WindowSnapshot(bounds: CGRect(x: 0, y: 0, width: 100, height: 100), ownerPID: 1, ownerName: "Overlay", alpha: 0, layer: 0),
            WindowSnapshot(bounds: CGRect(x: 0, y: 0, width: 50, height: 50), ownerPID: 2, ownerName: "Safari", alpha: 1, layer: 0),
            WindowSnapshot(bounds: CGRect(x: 0, y: 0, width: 500, height: 500), ownerPID: 3, ownerName: "Finder", alpha: 1, layer: 0),
        ]
        expectEqual(topmostWindow(at: CGPoint(x: 10, y: 10), in: windows)?.ownerName, "Safari")
        expectEqual(topmostWindow(at: CGPoint(x: 80, y: 80), in: windows)?.ownerName, "Finder")
        expect(topmostWindow(at: CGPoint(x: 900, y: 900), in: windows) == nil)
    }),
    ("testCocoaPointFlip", {
        // Main display 900pt tall; a display above it has negative global y.
        expectEqual(cocoaPoint(fromGlobal: CGPoint(x: 10, y: 0), mainDisplayHeight: 900), CGPoint(x: 10, y: 900))
        expectEqual(cocoaPoint(fromGlobal: CGPoint(x: 10, y: -100), mainDisplayHeight: 900), CGPoint(x: 10, y: 1000))
    }),
    ("testThumbnailIsBounded", {
        let data = pngOf(width: 2880, height: 1800)
        guard let thumb = thumbnailImage(fromPNG: data, maxPixelSize: 240) else { return expect(false, "nil thumb") }
        expectEqual(thumb.width, 240)
        expectEqual(thumb.height, 150)
        expect(thumbnailImage(fromPNG: Data([1, 2, 3]), maxPixelSize: 240) == nil)
    }),
]
