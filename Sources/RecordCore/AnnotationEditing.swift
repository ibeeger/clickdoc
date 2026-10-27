import CoreGraphics
import Foundation

/// Something editable on a step's screenshot.
public enum CanvasItem: Hashable {
    case blur(Int)
    case annotation(UUID)
}

public enum CanvasHandle: Equatable {
    /// Bottom-right corner of a box or blur region.
    case resize
    case arrowStart, arrowEnd
}

public enum CanvasHit: Equatable {
    case item(CanvasItem)
    case handle(CanvasItem, CanvasHandle)
}

private func distance(_ a: CGPoint, _ b: CGPoint) -> CGFloat {
    ((a.x - b.x) * (a.x - b.x) + (a.y - b.y) * (a.y - b.y)).squareRoot()
}

private func distance(_ p: CGPoint, toSegment a: CGPoint, _ b: CGPoint) -> CGFloat {
    let dx = b.x - a.x, dy = b.y - a.y
    let len2 = dx * dx + dy * dy
    guard len2 > 0 else { return distance(p, a) }
    let t = max(0, min(1, ((p.x - a.x) * dx + (p.y - a.y) * dy) / len2))
    return distance(p, CGPoint(x: a.x + t * dx, y: a.y + t * dy))
}

private func scaled(_ p: CGPoint, _ size: CGSize) -> CGPoint { CGPoint(x: p.x * size.width, y: p.y * size.height) }

/// The text's box on a canvas of `size` showing an image `imageWidth` pixels wide
/// (defaults to the canvas width). Sizes follow the exported image, scaled to the canvas.
public func textFrame(origin: CGPoint, string: String, canvasSize size: CGSize, imageWidth: CGFloat? = nil) -> CGRect {
    let w = imageWidth ?? size.width
    let px = textSize(string, imageWidth: w)
    let k = size.width / w
    return CGRect(origin: scaled(origin, size), size: CGSize(width: px.width * k, height: px.height * k))
}

/// Keeps a text's whole box inside the image.
public func clampTextOrigin(_ origin: CGPoint, string: String, imageSize: CGSize) -> CGPoint {
    let t = textSize(string, imageWidth: imageSize.width)
    let maxX = max(0, 1 - t.width / imageSize.width), maxY = max(0, 1 - t.height / imageSize.height)
    return CGPoint(x: min(max(origin.x, 0), maxX), y: min(max(origin.y, 0), maxY))
}

/// What is under `p` (canvas points, top-left origin). Handles of the selection win, then
/// annotations from the top down, then blur regions. Boxes are hit on their border only, so
/// new marks can still be drawn inside them. Drawing tools pass `includeBlurBodies: false`
/// so a drag inside a blur draws instead of moving the blur.
public func hitTest(_ p: CGPoint, blurRegions: [CGRect], annotations: [Annotation], selected: CanvasItem?,
                    canvasSize size: CGSize, imageWidth: CGFloat? = nil, includeBlurBodies: Bool = true,
                    tolerance: CGFloat = 6, handleRadius: CGFloat = 9) -> CanvasHit? {
    let imageW = imageWidth ?? size.width
    if let selected {
        switch selected {
        case .blur(let i) where blurRegions.indices.contains(i):
            let r = denormalizedRect(blurRegions[i], in: size)
            if distance(p, CGPoint(x: r.maxX, y: r.maxY)) <= handleRadius { return .handle(selected, .resize) }
        case .annotation(let id):
            switch annotations.first(where: { $0.id == id })?.kind {
            case .rectangle(let n)?:
                let r = denormalizedRect(n, in: size)
                if distance(p, CGPoint(x: r.maxX, y: r.maxY)) <= handleRadius { return .handle(selected, .resize) }
            case .arrow(let from, let to)?:
                if distance(p, scaled(to, size)) <= handleRadius { return .handle(selected, .arrowEnd) }
                if distance(p, scaled(from, size)) <= handleRadius { return .handle(selected, .arrowStart) }
            default:
                break
            }
        default:
            break
        }
    }
    for annotation in annotations.reversed() {
        let item = CanvasHit.item(.annotation(annotation.id))
        switch annotation.kind {
        case .rectangle(let n):
            let r = denormalizedRect(n, in: size)
            let outer = r.insetBy(dx: -tolerance, dy: -tolerance), inner = r.insetBy(dx: tolerance, dy: tolerance)
            if outer.contains(p) && (inner.isNull || !inner.contains(p)) { return item }
        case .arrow(let from, let to):
            let lw = AnnotationStyle.lineWidth(imageWidth: imageW) * size.width / imageW
            if distance(p, toSegment: scaled(from, size), scaled(to, size)) <= tolerance + lw { return item }
        case .text(let origin, let string):
            if textFrame(origin: origin, string: string, canvasSize: size, imageWidth: imageW).insetBy(dx: -tolerance, dy: -tolerance).contains(p) {
                return item
            }
        }
    }
    if includeBlurBodies, let i = blurRegions.indices.last(where: { denormalizedRect(blurRegions[$0], in: size).contains(p) }) {
        return .item(.blur(i))
    }
    return nil
}

private func clamp(_ v: CGFloat, _ lo: CGFloat, _ hi: CGFloat) -> CGFloat { min(max(v, lo), hi) }

/// Moves an annotation by a normalized delta, keeping it inside the image without changing its shape.
public func translated(_ kind: Annotation.Kind, by d: CGVector) -> Annotation.Kind {
    switch kind {
    case .rectangle(let r):
        return .rectangle(CGRect(x: clamp(r.minX + d.dx, 0, 1 - r.width), y: clamp(r.minY + d.dy, 0, 1 - r.height),
                                 width: r.width, height: r.height))
    case .arrow(let a, let b):
        let dx = clamp(d.dx, -min(a.x, b.x), 1 - max(a.x, b.x))
        let dy = clamp(d.dy, -min(a.y, b.y), 1 - max(a.y, b.y))
        return .arrow(from: CGPoint(x: a.x + dx, y: a.y + dy), to: CGPoint(x: b.x + dx, y: b.y + dy))
    case .text(let o, let s):
        return .text(origin: CGPoint(x: clamp(o.x + d.dx, 0, 1), y: clamp(o.y + d.dy, 0, 1)), string: s)
    }
}

/// Grows or shrinks a normalized rect from its bottom-right corner, within the image.
public func resizedRect(_ r: CGRect, by d: CGVector, minSize: CGSize) -> CGRect {
    CGRect(x: r.minX, y: r.minY,
           width: clamp(r.width + d.dx, minSize.width, 1 - r.minX),
           height: clamp(r.height + d.dy, minSize.height, 1 - r.minY))
}
