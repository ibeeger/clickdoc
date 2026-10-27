import CoreGraphics
import CoreText
import Foundation

/// A drawn mark on a screenshot. Coordinates are normalized to the image (0...1, top-left origin).
public struct Annotation: Codable, Equatable, Identifiable {
    public enum Kind: Codable, Equatable {
        case rectangle(CGRect)
        case arrow(from: CGPoint, to: CGPoint)
        /// `origin` is the top-left corner of the text.
        case text(origin: CGPoint, string: String)
    }

    public let id: UUID
    public var kind: Kind

    public init(id: UUID = UUID(), kind: Kind) {
        self.id = id
        self.kind = kind
    }
}

/// Sizes scale with the image, so marks look the same on thumbnails, exports and the editor.
public enum AnnotationStyle {
    public static func lineWidth(imageWidth: CGFloat) -> CGFloat { max(1.5, imageWidth * 0.0028) }
    public static func fontSize(imageWidth: CGFloat) -> CGFloat { max(10, imageWidth * 0.022) }
    public static func arrowHeadLength(lineWidth: CGFloat) -> CGFloat { lineWidth * 8 }
    public static func arrowHeadHalfWidth(lineWidth: CGFloat) -> CGFloat { lineWidth * 4.5 }
    /// Outline drawn around text, as a percentage of the font size (Core Text stroke width).
    public static let textOutlinePercent: CGFloat = 18
    public static let red = CGColor(srgbRed: 0.93, green: 0.11, blue: 0.14, alpha: 1)
}

/// Arrow outline (shaft + head) in the given coordinate space.
public func arrowPath(from start: CGPoint, to tip: CGPoint, lineWidth: CGFloat) -> (shaftEnd: CGPoint, head: CGPath)? {
    let dx = tip.x - start.x, dy = tip.y - start.y
    let length = (dx * dx + dy * dy).squareRoot()
    guard length > 0.5 else { return nil }
    let ux = dx / length, uy = dy / length
    let headLength = min(AnnotationStyle.arrowHeadLength(lineWidth: lineWidth), length)
    let half = AnnotationStyle.arrowHeadHalfWidth(lineWidth: lineWidth)
    let base = CGPoint(x: tip.x - ux * headLength, y: tip.y - uy * headLength)
    let head = CGMutablePath()
    head.move(to: tip)
    head.addLine(to: CGPoint(x: base.x - uy * half, y: base.y + ux * half))
    head.addLine(to: CGPoint(x: base.x + uy * half, y: base.y - ux * half))
    head.closeSubpath()
    return (base, head)
}

private let annotationContextSpace = CGColorSpace(name: CGColorSpace.sRGB)!

/// Returns `image` with the annotations drawn on top.
public func renderAnnotations(on image: CGImage, _ annotations: [Annotation]) -> CGImage? {
    guard !annotations.isEmpty else { return image }
    let w = CGFloat(image.width), h = CGFloat(image.height)
    guard let ctx = CGContext(data: nil, width: image.width, height: image.height, bitsPerComponent: 8, bytesPerRow: 0,
                              space: annotationContextSpace, bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)
    else { return nil }
    ctx.draw(image, in: CGRect(x: 0, y: 0, width: w, height: h))
    // Draw in top-left coordinates like the normalized model; text gets its own flip below.
    ctx.translateBy(x: 0, y: h)
    ctx.scaleBy(x: 1, y: -1)
    let lw = AnnotationStyle.lineWidth(imageWidth: w)
    ctx.setStrokeColor(AnnotationStyle.red)
    ctx.setFillColor(AnnotationStyle.red)
    ctx.setLineWidth(lw)
    ctx.setLineCap(.round)
    ctx.setLineJoin(.round)
    for annotation in annotations {
        switch annotation.kind {
        case .rectangle(let r):
            ctx.stroke(denormalizedRect(r, in: CGSize(width: w, height: h)))
        case .arrow(let from, let to):
            let a = CGPoint(x: from.x * w, y: from.y * h), b = CGPoint(x: to.x * w, y: to.y * h)
            guard let arrow = arrowPath(from: a, to: b, lineWidth: lw) else { continue }
            ctx.move(to: a)
            ctx.addLine(to: arrow.shaftEnd)
            ctx.strokePath()
            ctx.addPath(arrow.head)
            ctx.fillPath()
        case .text(let origin, let string):
            drawText(string, topLeft: CGPoint(x: origin.x * w, y: origin.y * h), imageWidth: w, in: ctx)
        }
    }
    return ctx.makeImage()
}

/// The text box size in image pixels, for hit-testing and layout.
public func textSize(_ string: String, imageWidth: CGFloat) -> CGSize {
    let line = textLine(string, imageWidth: imageWidth, outline: false)
    var ascent: CGFloat = 0, descent: CGFloat = 0, leading: CGFloat = 0
    let width = CTLineGetTypographicBounds(line, &ascent, &descent, &leading)
    return CGSize(width: CGFloat(width), height: ascent + descent)
}

private func textLine(_ string: String, imageWidth: CGFloat, outline: Bool) -> CTLine {
    let font = CTFontCreateUIFontForLanguage(.emphasizedSystem, AnnotationStyle.fontSize(imageWidth: imageWidth), nil)!
    var attrs: [NSAttributedString.Key: Any] = [NSAttributedString.Key(kCTFontAttributeName as String): font]
    if outline {
        attrs[NSAttributedString.Key(kCTStrokeColorAttributeName as String)] = CGColor(gray: 1, alpha: 1)
        attrs[NSAttributedString.Key(kCTStrokeWidthAttributeName as String)] = AnnotationStyle.textOutlinePercent
    } else {
        attrs[NSAttributedString.Key(kCTForegroundColorAttributeName as String)] = AnnotationStyle.red
    }
    return CTLineCreateWithAttributedString(NSAttributedString(string: string, attributes: attrs))
}

private func drawText(_ string: String, topLeft: CGPoint, imageWidth: CGFloat, in ctx: CGContext) {
    var ascent: CGFloat = 0, descent: CGFloat = 0, leading: CGFloat = 0
    let fill = textLine(string, imageWidth: imageWidth, outline: false)
    _ = CTLineGetTypographicBounds(fill, &ascent, &descent, &leading)
    ctx.saveGState()
    // Core Text draws upright in a y-up space: flip locally around the baseline.
    ctx.translateBy(x: topLeft.x, y: topLeft.y + ascent)
    ctx.scaleBy(x: 1, y: -1)
    ctx.textPosition = .zero
    CTLineDraw(textLine(string, imageWidth: imageWidth, outline: true), ctx)
    ctx.textPosition = .zero
    CTLineDraw(fill, ctx)
    ctx.restoreGState()
}
