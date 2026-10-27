import Foundation
import CoreGraphics
import PDFKit
import RecordCore

/// Runs `body` with English generated content, restoring the suite default (Chinese) afterwards.
private func inEnglish(_ body: () throws -> Void) rethrows {
    ContentLanguage.override = .english
    defer { ContentLanguage.override = .chinese }
    try body()
}

private func onePixelPNG() -> Data {
    let ctx = CGContext(data: nil, width: 40, height: 30, bitsPerComponent: 8, bytesPerRow: 0,
                        space: CGColorSpaceCreateDeviceRGB(), bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
    return pngData(from: ctx.makeImage()!)!
}

let englishTests: [TestCase] = [
    ("testLanguageResolution", {
        expectEqual(ContentLanguage.resolve(preferredLocalization: "zh-Hans"), .chinese)
        expectEqual(ContentLanguage.resolve(preferredLocalization: "zh_CN"), .chinese)
        expectEqual(ContentLanguage.resolve(preferredLocalization: "en"), .english)
        expectEqual(ContentLanguage.resolve(preferredLocalization: nil), .english)
    }),
    ("testEnglishDescriptions", {
        inEnglish {
            expectEqual(describeStep(action: .click, appName: "Safari", elementName: "New Tab"), "Click \"New Tab\" in Safari")
            expectEqual(describeStep(action: .doubleClick, appName: "Finder", elementName: "Documents"), "Double-click \"Documents\" in Finder")
            expectEqual(describeStep(action: .rightClick, appName: "Finder", elementName: nil), "Right-click in Finder")
            expectEqual(describeStep(action: .click, appName: nil, elementName: "OK"), "Click \"OK\"")
            expectEqual(describeStep(action: .click, appName: nil, elementName: nil), "Click")
        }
    }),
    ("testEnglishMarkdownAndDefaults", {
        inEnglish {
            let t = Tutorial(title: "Demo", steps: [Step(description: "First", pngData: Data([1]))])
            expectEqual(renderMarkdown(t), "# Demo\n\n## Step 1\n\nFirst\n\n![Step 1](images/step-01.png)\n")
            expectEqual(Tutorial().title, "Untitled Tutorial")
            expectEqual(sanitizeFolderName("  "), "Untitled Tutorial")
        }
    }),
    ("testEnglishPDF", {
        try inEnglish {
            let t = Tutorial(title: "  ", steps: [Step(description: "First", pngData: onePixelPNG())])
            let text = PDFDocument(data: try renderPDF(t))?.string ?? ""
            expect(text.contains("Step 1"), "heading: \(text)")
            expect(text.contains("Untitled Tutorial"), "title fallback: \(text)")
        }
    }),
]
