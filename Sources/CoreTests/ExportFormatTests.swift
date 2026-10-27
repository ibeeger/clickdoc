import Foundation
import CoreGraphics
import PDFKit
import RecordCore

private func solidPNG(_ w: Int, _ h: Int) -> Data {
    let ctx = CGContext(data: nil, width: w, height: h, bitsPerComponent: 8, bytesPerRow: 0,
                        space: CGColorSpaceCreateDeviceRGB(),
                        bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
    ctx.setFillColor(red: 0.2, green: 0.5, blue: 0.8, alpha: 1)
    ctx.fill(CGRect(x: 0, y: 0, width: w, height: h))
    return pngData(from: ctx.makeImage()!)!
}

private func stripesPNG(_ w: Int, _ h: Int) -> Data {
    let ctx = CGContext(data: nil, width: w, height: h, bitsPerComponent: 8, bytesPerRow: 0,
                        space: CGColorSpaceCreateDeviceRGB(),
                        bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
    ctx.setFillColor(red: 1, green: 1, blue: 1, alpha: 1)
    ctx.fill(CGRect(x: 0, y: 0, width: w, height: h))
    ctx.setFillColor(red: 0, green: 0, blue: 0, alpha: 1)
    for x in stride(from: 0, to: w, by: 6) { ctx.fill(CGRect(x: x, y: 0, width: 3, height: h)) }
    return pngData(from: ctx.makeImage()!)!
}

/// Rasterizes the first PDF page so tests compare what a reader sees, not file bytes.
private func firstPagePixels(_ data: Data) -> [UInt8] {
    let doc = CGPDFDocument(CGDataProvider(data: data as CFData)!)!
    let page = doc.page(at: 1)!
    let box = page.getBoxRect(.mediaBox)
    let w = Int(box.width), h = Int(box.height)
    var buf = [UInt8](repeating: 0, count: w * h * 4)
    let ctx = CGContext(data: &buf, width: w, height: h, bitsPerComponent: 8, bytesPerRow: w * 4,
                        space: CGColorSpaceCreateDeviceRGB(),
                        bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
    ctx.setFillColor(red: 1, green: 1, blue: 1, alpha: 1)
    ctx.fill(box)
    ctx.drawPDFPage(page)
    return buf
}

private func tempDir() throws -> URL {
    let u = FileManager.default.temporaryDirectory.appendingPathComponent("rtd-fmt-\(UUID().uuidString)")
    try FileManager.default.createDirectory(at: u, withIntermediateDirectories: true)
    return u
}

let exportFormatTests: [TestCase] = [
    ("testPDFContainsTitleAndDescriptions", {
        let t = Tutorial(title: "如何新建标签页", steps: [
            Step(description: "在 Safari 中点击「新建标签页」", pngData: solidPNG(1440, 900)),
            Step(description: "  ", pngData: solidPNG(1440, 900)),
        ])
        let data = try renderPDF(t)
        guard let doc = PDFDocument(data: data) else { return expect(false, "not a PDF") }
        let text = doc.string ?? ""
        expect(text.contains("如何新建标签页"), "title missing: \(text.prefix(200))")
        expect(text.contains("步骤 1") && text.contains("步骤 2"), "step headings missing")
        expect(text.contains("新建标签页」"), "description missing")
        let page = doc.page(at: 0)!.bounds(for: .mediaBox)
        expectEqual(page.size, CGSize(width: 595, height: 842))  // A4
    }),
    ("testPDFPaginatesTallContent", {
        let steps = (1...6).map { Step(description: "第 \($0) 步", pngData: solidPNG(1000, 1000)) }
        let doc = PDFDocument(data: try renderPDF(Tutorial(title: "T", steps: steps)))!
        // Screenshots are capped at 270pt tall, so two steps fit on each A4 page.
        expectEqual(doc.pageCount, 3)
        expect(doc.string?.contains("第 6 步") == true, "last step present")
    }),
    ("testPDFScalesVeryTallImageToFitPage", {
        let t = Tutorial(title: "T", steps: [Step(description: "长图", pngData: solidPNG(400, 6000))])
        let doc = PDFDocument(data: try renderPDF(t))!
        expect(doc.pageCount <= 2, "tall image should be scaled, pages \(doc.pageCount)")
    }),
    ("testPDFAppliesBlur", {
        // Rendering is deterministic, and burning in a blur changes what the page shows.
        var s = Step(description: "x", pngData: stripesPNG(600, 400))
        let plain = firstPagePixels(try renderPDF(Tutorial(title: "T", steps: [s])))
        let plainAgain = firstPagePixels(try renderPDF(Tutorial(title: "T", steps: [s])))
        expect(plain == plainAgain, "rendering should be deterministic")
        s.blurRegions = [CGRect(x: 0, y: 0, width: 0.5, height: 0.5)]
        let blurred = firstPagePixels(try renderPDF(Tutorial(title: "T", steps: [s])))
        expect(plain != blurred, "blur not burned into PDF")
    }),
    ("testExportPDFFileNaming", {
        let dir = try tempDir()
        defer { try? FileManager.default.removeItem(at: dir) }
        let t = Tutorial(title: "a/b", steps: [Step(description: "x", pngData: solidPNG(100, 100))])
        let first = try exportPDF(t, to: dir)
        expectEqual(first.lastPathComponent, "a-b.pdf")
        let second = try exportPDF(t, to: dir)
        expectEqual(second.lastPathComponent, "a-b 2.pdf")
        expect(PDFDocument(url: second) != nil)
    }),
    ("testExportNotionZip", {
        let dir = try tempDir()
        defer { try? FileManager.default.removeItem(at: dir) }
        let t = Tutorial(title: "演示", steps: [Step(description: "x", pngData: solidPNG(100, 100))])
        let zip = try exportNotionZip(t, to: dir)
        expectEqual(zip.lastPathComponent, "演示.zip")
        let left = try FileManager.default.contentsOfDirectory(atPath: dir.path)
        expectEqual(left, ["演示.zip"])
        let entries = centralDirectory(try Data(contentsOf: zip))
        expectEqual(entries.map(\.name), ["演示.md", "images/step-01.png"])
        expect(entries.allSatisfy { $0.flags & 0x0800 != 0 }, "UTF-8 name flag must be set")
        expectEqual(unzipTest(zip), 0)  // CRCs and structure valid
    }),
    ("testExportNotionZipNameIsUnique", {
        let dir = try tempDir()
        defer { try? FileManager.default.removeItem(at: dir) }
        let t = Tutorial(title: "Z", steps: [Step(description: "x", pngData: solidPNG(10, 10))])
        _ = try exportNotionZip(t, to: dir)
        expectEqual(try exportNotionZip(t, to: dir).lastPathComponent, "Z 2.zip")
    }),
]

/// Reads (name, general-purpose flags) for every central directory entry.
private func centralDirectory(_ d: Data) -> [(name: String, flags: UInt16)] {
    let b = [UInt8](d)
    func u16(_ i: Int) -> Int { Int(b[i]) | Int(b[i + 1]) << 8 }
    var out: [(String, UInt16)] = []
    var i = 0
    while i + 46 <= b.count {
        if b[i] == 0x50, b[i + 1] == 0x4b, b[i + 2] == 0x01, b[i + 3] == 0x02 {
            let flags = UInt16(u16(i + 8))
            let nameLen = u16(i + 28), extra = u16(i + 30), comment = u16(i + 32)
            out.append((String(decoding: b[(i + 46)..<(i + 46 + nameLen)], as: UTF8.self), flags))
            i += 46 + nameLen + extra + comment
        } else {
            i += 1
        }
    }
    return out
}

private func unzipTest(_ url: URL) -> Int32 {
    let p = Process()
    p.executableURL = URL(fileURLWithPath: "/usr/bin/unzip")
    p.arguments = ["-tq", url.path]
    p.standardOutput = FileHandle.nullDevice
    try? p.run()
    p.waitUntilExit()
    return p.terminationStatus
}
