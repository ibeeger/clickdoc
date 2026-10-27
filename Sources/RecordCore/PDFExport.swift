import CoreGraphics
import CoreText
import Foundation
import ImageIO

public func jpegData(from image: CGImage, quality: Double = 0.8) -> Data? {
    let data = NSMutableData()
    guard let dest = CGImageDestinationCreateWithData(data, "public.jpeg" as CFString, 1, nil) else { return nil }
    CGImageDestinationAddImage(dest, image, [kCGImageDestinationLossyCompressionQuality: quality] as CFDictionary)
    guard CGImageDestinationFinalize(dest) else { return nil }
    return data as Data
}

private enum PDFLayout {
    static let page = CGRect(x: 0, y: 0, width: 595, height: 842)  // A4 in points
    static let margin: CGFloat = 50
    static var contentWidth: CGFloat { page.width - margin * 2 }
    static var contentHeight: CGFloat { page.height - margin * 2 }
    static let maxImagePixels = 1600
    /// Keeps two typical steps on one page.
    static let maxImageHeight: CGFloat = 270
    static let titleGap: CGFloat = 18
    static let headingGap: CGFloat = 6
    static let textGap: CGFloat = 8
    static let stepGap: CGFloat = 22
}

private struct TextBlock {
    let framesetter: CTFramesetter
    let height: CGFloat

    init(_ text: String, font: CTFont, color: CGColor) {
        let attrs: [NSAttributedString.Key: Any] = [
            NSAttributedString.Key(kCTFontAttributeName as String): font,
            NSAttributedString.Key(kCTForegroundColorAttributeName as String): color,
        ]
        framesetter = CTFramesetterCreateWithAttributedString(NSAttributedString(string: text, attributes: attrs))
        let size = CTFramesetterSuggestFrameSizeWithConstraints(
            framesetter, CFRange(location: 0, length: 0), nil,
            CGSize(width: PDFLayout.contentWidth, height: .greatestFiniteMagnitude), nil)
        height = ceil(size.height)
    }
}

/// Draws top-down onto A4 pages, starting a new page whenever a block would not fit.
private final class PDFWriter {
    let ctx: CGContext
    /// Distance from the top edge of the page.
    private var y: CGFloat = PDFLayout.margin
    private var pageOpen = false

    init(ctx: CGContext) { self.ctx = ctx }

    var remaining: CGFloat { PDFLayout.page.height - PDFLayout.margin - y }
    var atPageTop: Bool { y == PDFLayout.margin }

    func ensurePage() {
        if !pageOpen { ctx.beginPDFPage(nil); pageOpen = true; y = PDFLayout.margin }
    }

    func newPage() {
        if pageOpen { ctx.endPDFPage() }
        pageOpen = false
        ensurePage()
    }

    func finish() { if pageOpen { ctx.endPDFPage() } }

    func draw(_ block: TextBlock) {
        let rect = CGRect(x: PDFLayout.margin, y: PDFLayout.page.height - y - block.height,
                          width: PDFLayout.contentWidth, height: block.height)
        let frame = CTFramesetterCreateFrame(block.framesetter, CFRange(location: 0, length: 0), CGPath(rect: rect, transform: nil), nil)
        CTFrameDraw(frame, ctx)
        y += block.height
    }

    func draw(_ image: CGImage, size: CGSize) {
        let x = PDFLayout.margin + (PDFLayout.contentWidth - size.width) / 2  // centered
        ctx.draw(image, in: CGRect(x: x, y: PDFLayout.page.height - y - size.height,
                                   width: size.width, height: size.height))
        y += size.height
    }

    func space(_ h: CGFloat) { y += h }
}

/// Downscaled JPEG of the step's export image (blur burned in), decoded so Quartz embeds the JPEG stream.
private func pdfImage(for step: Step) throws -> CGImage {
    let full = try exportImageData(for: step)
    guard let small = thumbnailImage(fromPNG: full, maxPixelSize: PDFLayout.maxImagePixels),
          let jpeg = jpegData(from: small),
          let source = CGImageSourceCreateWithData(jpeg as CFData, nil),
          let image = CGImageSourceCreateImageAtIndex(source, 0, nil) else { throw ExportError.imageProcessingFailed }
    return image
}

public func renderPDF(_ tutorial: Tutorial) throws -> Data {
    let data = NSMutableData()
    var mediaBox = PDFLayout.page
    guard let consumer = CGDataConsumer(data: data as CFMutableData),
          let ctx = CGContext(consumer: consumer, mediaBox: &mediaBox,
                              [kCGPDFContextTitle: tutorial.title] as CFDictionary) else {
        throw ExportError.imageProcessingFailed
    }
    let black = CGColor(gray: 0, alpha: 1)
    let gray = CGColor(gray: 0.25, alpha: 1)
    let titleFont = CTFontCreateUIFontForLanguage(.emphasizedSystem, 24, nil)!
    let headingFont = CTFontCreateUIFontForLanguage(.emphasizedSystem, 15, nil)!
    let bodyFont = CTFontCreateUIFontForLanguage(.system, 12, nil)!

    let writer = PDFWriter(ctx: ctx)
    writer.ensurePage()
    let title = tutorial.title.trimmingCharacters(in: .whitespacesAndNewlines)
    let lang = ContentLanguage.current
    writer.draw(TextBlock(title.isEmpty ? lang.untitledTitle : title, font: titleFont, color: black))
    writer.space(PDFLayout.titleGap)

    for (offset, step) in tutorial.steps.enumerated() {
        let heading = TextBlock(lang.stepHeading(offset + 1), font: headingFont, color: black)
        let text = step.description.trimmingCharacters(in: .whitespacesAndNewlines)
        let body = text.isEmpty ? nil : TextBlock(text, font: bodyFont, color: gray)
        let image = try pdfImage(for: step)

        let textHeight = heading.height + PDFLayout.headingGap + (body.map { $0.height + PDFLayout.textGap } ?? 0)
        // Scale to content width, but never taller than what fits beside the text on one page.
        let width = PDFLayout.contentWidth
        var size = CGSize(width: width, height: width * CGFloat(image.height) / CGFloat(image.width))
        let maxImageHeight = min(PDFLayout.maxImageHeight, PDFLayout.contentHeight - textHeight)
        if size.height > maxImageHeight {
            size = CGSize(width: size.width * maxImageHeight / size.height, height: maxImageHeight)
        }
        // Keep heading, text and screenshot together.
        if textHeight + size.height > writer.remaining && !writer.atPageTop { writer.newPage() }

        writer.draw(heading)
        writer.space(PDFLayout.headingGap)
        if let body { writer.draw(body); writer.space(PDFLayout.textGap) }
        writer.draw(image, size: size)
        writer.space(PDFLayout.stepGap)
    }
    writer.finish()
    ctx.closePDF()
    return data as Data
}

/// `<dir>/<name>.<ext>`, or `<name> 2.<ext>`, `<name> 3.<ext>`… if taken.
public func uniqueFileURL(in directory: URL, name: String, ext: String, exists: (URL) -> Bool) -> URL {
    var candidate = directory.appendingPathComponent("\(name).\(ext)")
    var n = 2
    while exists(candidate) {
        candidate = directory.appendingPathComponent("\(name) \(n).\(ext)")
        n += 1
    }
    return candidate
}

public func exportPDF(_ tutorial: Tutorial, to directory: URL, fileManager: FileManager = .default) throws -> URL {
    let url = uniqueFileURL(in: directory, name: sanitizeFolderName(tutorial.title), ext: "pdf") {
        fileManager.fileExists(atPath: $0.path)
    }
    try renderPDF(tutorial).write(to: url, options: .atomic)
    return url
}

/// A zip for Notion's "Import > Markdown": `<title>.md` and `images/` at the zip root.
public func exportNotionZip(_ tutorial: Tutorial, to directory: URL, fileManager: FileManager = .default) throws -> URL {
    let name = sanitizeFolderName(tutorial.title)
    let zipURL = uniqueFileURL(in: directory, name: name, ext: "zip") { fileManager.fileExists(atPath: $0.path) }
    var entries: [(path: String, data: Data)] = [("\(name).md", Data(renderMarkdown(tutorial).utf8))]
    for (offset, step) in tutorial.steps.enumerated() {
        entries.append(("images/\(imageFileName(index: offset + 1))", try exportImageData(for: step)))
    }
    try ZipWriter.archive(entries).write(to: zipURL, options: .atomic)
    return zipURL
}
