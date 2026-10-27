import AppKit
import SwiftUI
import RecordCore

/// Shows a screenshot with live blur over its regions; in editing mode, regions can be
/// drawn (drag on empty area), moved (drag inside), resized (drag the bottom-right handle)
/// and deleted (Delete key on the selected region).
struct BlurCanvas: View {
    let image: NSImage
    /// Normalized, top-left origin.
    let regions: [CGRect]
    let editing: Bool
    @Binding var selected: Int?
    let onCommit: ([CGRect]) -> Void

    private enum DragMode {
        case create(start: CGPoint)
        case move(index: Int, original: CGRect)
        case resize(index: Int, original: CGRect)
    }

    @State private var mode: DragMode?
    /// Regions while a drag is in progress; committed on drag end.
    @State private var working: [CGRect]?
    @FocusState private var focused: Bool

    private static let handleSize: CGFloat = 10
    private static let minSidePoints: CGFloat = 4

    var body: some View {
        GeometryReader { geo in
            let fitted = fittedSize(in: geo.size)
            let shown = working ?? regions
            ZStack(alignment: .topLeading) {
                Image(nsImage: image)
                    .resizable()
                    .frame(width: fitted.width, height: fitted.height)
                Image(nsImage: image)
                    .resizable()
                    .frame(width: fitted.width, height: fitted.height)
                    .blur(radius: previewRadius(fittedWidth: fitted.width), opaque: true)
                    .mask(alignment: .topLeading) {
                        ZStack(alignment: .topLeading) {
                            ForEach(Array(shown.enumerated()), id: \.offset) { _, r in
                                let v = denormalizedRect(r, in: fitted)
                                Rectangle().frame(width: v.width, height: v.height).offset(x: v.minX, y: v.minY)
                            }
                        }
                        .frame(width: fitted.width, height: fitted.height, alignment: .topLeading)
                    }
                if editing {
                    ForEach(Array(shown.enumerated()), id: \.offset) { i, r in
                        outline(denormalizedRect(r, in: fitted), isSelected: i == selected)
                    }
                }
            }
            .frame(width: fitted.width, height: fitted.height, alignment: .topLeading)
            .contentShape(Rectangle())
            .gesture(drag(fitted: fitted), including: editing ? .all : .subviews)
            .focusable(editing)
            .focused($focused)
            .focusEffectDisabled()
            .onKeyPress(keys: [.delete, .deleteForward]) { _ in
                guard editing, let i = selected, regions.indices.contains(i) else { return .ignored }
                var next = regions
                next.remove(at: i)
                selected = nil
                onCommit(next)
                return .handled
            }
            .position(x: geo.size.width / 2, y: geo.size.height / 2)
        }
    }

    // MARK: - Drawing

    private func outline(_ rect: CGRect, isSelected: Bool) -> some View {
        ZStack(alignment: .bottomTrailing) {
            Rectangle()
                .strokeBorder(Color.accentColor, style: StrokeStyle(lineWidth: 1.5, dash: isSelected ? [] : [5, 3]))
            if isSelected {
                Rectangle()
                    .fill(.white)
                    .overlay(Rectangle().stroke(Color.accentColor, lineWidth: 1.5))
                    .frame(width: Self.handleSize, height: Self.handleSize)
                    .offset(x: Self.handleSize / 2, y: Self.handleSize / 2)
            }
        }
        .frame(width: rect.width, height: rect.height)
        .offset(x: rect.minX, y: rect.minY)
        .allowsHitTesting(false)
    }

    private var pixelWidth: CGFloat {
        CGFloat(image.representations.first?.pixelsWide ?? Int(image.size.width))
    }

    /// Same visual strength as the exported blur, scaled to the on-screen size.
    private func previewRadius(fittedWidth: CGFloat) -> CGFloat {
        guard pixelWidth > 0 else { return 10 }
        return blurSigma(imageWidth: Int(pixelWidth)) * fittedWidth / pixelWidth
    }

    private func fittedSize(in available: CGSize) -> CGSize {
        let size = image.size
        guard size.width > 0, size.height > 0, available.width > 0, available.height > 0 else { return .zero }
        let scale = min(available.width / size.width, available.height / size.height)
        return CGSize(width: size.width * scale, height: size.height * scale)
    }

    // MARK: - Editing

    private func drag(fitted: CGSize) -> some Gesture {
        DragGesture(minimumDistance: 0, coordinateSpace: .local)
            .onChanged { value in
                if mode == nil { mode = beginMode(at: value.startLocation, fitted: fitted) }
                working = updated(regions, mode: mode, value: value, fitted: fitted)
            }
            .onEnded { value in
                defer { mode = nil; working = nil; focused = true }
                let result = updated(regions, mode: mode, value: value, fitted: fitted)
                if case .create = mode {
                    let added = denormalizedRect(result.last ?? .zero, in: fitted)
                    guard added.width >= Self.minSidePoints, added.height >= Self.minSidePoints else { return }
                    selected = result.count - 1
                }
                if result != regions { onCommit(result) }
            }
    }

    private func beginMode(at point: CGPoint, fitted: CGSize) -> DragMode {
        if let i = selected, regions.indices.contains(i) {
            let v = denormalizedRect(regions[i], in: fitted)
            let handle = CGRect(x: v.maxX - Self.handleSize, y: v.maxY - Self.handleSize,
                                width: Self.handleSize * 2, height: Self.handleSize * 2)
            if handle.contains(point) { return .resize(index: i, original: regions[i]) }
        }
        // Topmost (last drawn) region under the point wins.
        if let i = regions.indices.last(where: { denormalizedRect(regions[$0], in: fitted).contains(point) }) {
            selected = i
            return .move(index: i, original: regions[i])
        }
        selected = nil
        return .create(start: point)
    }

    private func updated(_ base: [CGRect], mode: DragMode?, value: DragGesture.Value, fitted: CGSize) -> [CGRect] {
        guard let mode, fitted.width > 0, fitted.height > 0 else { return base }
        var next = base
        switch mode {
        case .create(let start):
            let rect = CGRect(x: min(start.x, value.location.x), y: min(start.y, value.location.y),
                              width: abs(value.location.x - start.x), height: abs(value.location.y - start.y))
            next.append(normalizedRect(rect, in: fitted))
        case .move(let i, let original):
            let dx = value.translation.width / fitted.width, dy = value.translation.height / fitted.height
            var r = original
            r.origin.x = min(max(0, original.minX + dx), 1 - original.width)
            r.origin.y = min(max(0, original.minY + dy), 1 - original.height)
            next[i] = r
        case .resize(let i, let original):
            let minW = Self.minSidePoints / fitted.width, minH = Self.minSidePoints / fitted.height
            var r = original
            r.size.width = min(max(minW, original.width + value.translation.width / fitted.width), 1 - original.minX)
            r.size.height = min(max(minH, original.height + value.translation.height / fitted.height), 1 - original.minY)
            next[i] = r
        }
        return next
    }
}
