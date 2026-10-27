import AppKit
import SwiftUI
import RecordCore

enum EditorTool: String, CaseIterable, Identifiable {
    case select, blur, rectangle, arrow, text
    var id: Self { self }

    var title: String {
        switch self {
        case .select: return "选择"
        case .blur: return "模糊"
        case .rectangle: return "方框"
        case .arrow: return "箭头"
        case .text: return "文字"
        }
    }

    var icon: String {
        switch self {
        case .select: return "cursorarrow"
        case .blur: return "eye.slash"
        case .rectangle: return "rectangle"
        case .arrow: return "arrow.up.right"
        case .text: return "textformat"
        }
    }

    var hint: String {
        switch self {
        case .select: return "点选标注可移动，拖动手柄可调整，按 Delete 删除；双击文字可修改"
        case .blur: return "拖动框选要模糊的区域"
        case .rectangle: return "拖动画出方框"
        case .arrow: return "从起点拖到终点画出箭头"
        case .text: return "在图上点一下输入文字，回车确认，Esc 取消"
        }
    }
}

/// A screenshot with live blur and annotations. Draws, selects, moves, resizes and deletes
/// blur regions and annotations; all model coordinates are normalized (top-left origin).
struct AnnotationCanvas: View {
    let image: NSImage
    let blurRegions: [CGRect]
    let annotations: [Annotation]
    let tool: EditorTool
    @Binding var selected: CanvasItem?
    let onCommitBlur: ([CGRect]) -> Void
    let onCommitAnnotations: ([Annotation]) -> Void

    private enum DragMode {
        case create(start: CGPoint)
        case moveBlur(Int, CGRect)
        case resizeBlur(Int, CGRect)
        case moveAnnotation(UUID, Annotation.Kind)
        case resizeBox(UUID, CGRect)
        case moveArrowEnd(UUID, isStart: Bool, Annotation.Kind)
        case idle
    }

    private struct TextEdit {
        var id: UUID?
        var origin: CGPoint  // normalized
        var text: String
    }

    @State private var mode: DragMode?
    @State private var workingBlur: [CGRect]?
    @State private var workingAnnotations: [Annotation]?
    @State private var textEdit: TextEdit?
    @State private var lastTap: (time: Date, point: CGPoint)?
    @FocusState private var canvasFocused: Bool
    @FocusState private var textFocused: Bool

    private static let draftID = UUID()
    private static let minSide: CGFloat = 4
    private static let red = Color(cgColor: AnnotationStyle.red)

    var body: some View {
        GeometryReader { geo in
            let fitted = fittedSize(in: geo.size)
            ZStack(alignment: .topLeading) {
                Image(nsImage: image).resizable().frame(width: fitted.width, height: fitted.height)
                blurLayer(fitted)
                Canvas { ctx, size in drawAnnotations(in: &ctx, size: size) }
                    .frame(width: fitted.width, height: fitted.height)
                    .allowsHitTesting(false)
                selectionLayer(fitted).allowsHitTesting(false)
                if let edit = textEdit { textField(edit, fitted: fitted) }
            }
            .frame(width: fitted.width, height: fitted.height, alignment: .topLeading)
            .contentShape(Rectangle())
            .gesture(drag(fitted: fitted))
            .focusable()
            .focused($canvasFocused)
            .focusEffectDisabled()
            .onKeyPress(keys: [.delete, .deleteForward]) { _ in
                guard textEdit == nil, selected != nil else { return .ignored }
                deleteSelected()
                return .handled
            }
            .position(x: geo.size.width / 2, y: geo.size.height / 2)
        }
    }

    private var shownBlur: [CGRect] { workingBlur ?? blurRegions }
    private var shownAnnotations: [Annotation] { workingAnnotations ?? annotations }

    // MARK: - Layers

    private func blurLayer(_ fitted: CGSize) -> some View {
        Image(nsImage: image)
            .resizable()
            .frame(width: fitted.width, height: fitted.height)
            .blur(radius: previewBlurRadius(fittedWidth: fitted.width), opaque: true)
            .mask(alignment: .topLeading) {
                ZStack(alignment: .topLeading) {
                    ForEach(Array(shownBlur.enumerated()), id: \.offset) { _, r in
                        let v = denormalizedRect(r, in: fitted)
                        Rectangle().frame(width: v.width, height: v.height).offset(x: v.minX, y: v.minY)
                    }
                }
                .frame(width: fitted.width, height: fitted.height, alignment: .topLeading)
            }
    }

    private func drawAnnotations(in ctx: inout GraphicsContext, size: CGSize) {
        let lw = AnnotationStyle.lineWidth(imageWidth: size.width)
        let style = StrokeStyle(lineWidth: lw, lineCap: .round, lineJoin: .round)
        for annotation in shownAnnotations where annotation.id != textEdit?.id {
            switch annotation.kind {
            case .rectangle(let r):
                ctx.stroke(Path(denormalizedRect(r, in: size)), with: .color(Self.red), style: style)
            case .arrow(let from, let to):
                let a = CGPoint(x: from.x * size.width, y: from.y * size.height)
                let b = CGPoint(x: to.x * size.width, y: to.y * size.height)
                guard let arrow = arrowPath(from: a, to: b, lineWidth: lw) else { continue }
                var shaft = Path()
                shaft.move(to: a)
                shaft.addLine(to: arrow.shaftEnd)
                ctx.stroke(shaft, with: .color(Self.red), style: style)
                ctx.fill(Path(arrow.head), with: .color(Self.red))
            case .text(let origin, let string):
                let fontSize = AnnotationStyle.fontSize(imageWidth: size.width)
                let p = CGPoint(x: origin.x * size.width, y: origin.y * size.height)
                let outline = fontSize * 0.07
                let font = Font.system(size: fontSize, weight: .bold)
                for dx in [-outline, 0, outline] {
                    for dy in [-outline, 0, outline] where dx != 0 || dy != 0 {
                        ctx.draw(Text(string).font(font).foregroundColor(.white),
                                 at: CGPoint(x: p.x + dx, y: p.y + dy), anchor: .topLeading)
                    }
                }
                ctx.draw(Text(string).font(font).foregroundColor(Self.red), at: p, anchor: .topLeading)
            }
        }
    }

    @ViewBuilder
    private func selectionLayer(_ fitted: CGSize) -> some View {
        ZStack(alignment: .topLeading) {
            // Blur regions are invisible on flat backgrounds; always outline them in the editor.
            ForEach(Array(shownBlur.enumerated()), id: \.offset) { i, r in
                let v = denormalizedRect(r, in: fitted)
                Rectangle()
                    .strokeBorder(Color.accentColor.opacity(selected == .blur(i) ? 1 : 0.6),
                                  style: StrokeStyle(lineWidth: 1, dash: selected == .blur(i) ? [] : [5, 3]))
                    .frame(width: v.width, height: v.height)
                    .offset(x: v.minX, y: v.minY)
                if selected == .blur(i) { handle(CGPoint(x: v.maxX, y: v.maxY), round: false) }
            }
            if case .annotation(let id)? = selected, let annotation = shownAnnotations.first(where: { $0.id == id }) {
                switch annotation.kind {
                case .rectangle(let r):
                    let v = denormalizedRect(r, in: fitted)
                    handle(CGPoint(x: v.maxX, y: v.maxY), round: false)
                case .arrow(let from, let to):
                    handle(CGPoint(x: from.x * fitted.width, y: from.y * fitted.height), round: true)
                    handle(CGPoint(x: to.x * fitted.width, y: to.y * fitted.height), round: true)
                case .text(let origin, let string):
                    let f = textFrame(origin: origin, string: string, canvasSize: fitted).insetBy(dx: -3, dy: -3)
                    Rectangle()
                        .strokeBorder(Color.accentColor, style: StrokeStyle(lineWidth: 1, dash: [4, 3]))
                        .frame(width: f.width, height: f.height)
                        .offset(x: f.minX, y: f.minY)
                }
            }
        }
        .frame(width: fitted.width, height: fitted.height, alignment: .topLeading)
    }

    private func handle(_ center: CGPoint, round: Bool) -> some View {
        let size: CGFloat = 10
        return Group {
            if round {
                Circle().fill(.white).overlay(Circle().stroke(Color.accentColor, lineWidth: 1.5))
            } else {
                Rectangle().fill(.white).overlay(Rectangle().stroke(Color.accentColor, lineWidth: 1.5))
            }
        }
        .frame(width: size, height: size)
        .offset(x: center.x - size / 2, y: center.y - size / 2)
    }

    private func textField(_ edit: TextEdit, fitted: CGSize) -> some View {
        let fontSize = AnnotationStyle.fontSize(imageWidth: fitted.width)
        return TextField("输入文字", text: Binding(get: { textEdit?.text ?? "" }, set: { textEdit?.text = $0 }))
            .textFieldStyle(.plain)
            .font(.system(size: fontSize, weight: .bold))
            .foregroundColor(Self.red)
            .padding(.horizontal, 3)
            .background(Color.white.opacity(0.85))
            .overlay(Rectangle().stroke(Color.accentColor, lineWidth: 1))
            .frame(minWidth: 140)
            .fixedSize()
            .offset(x: edit.origin.x * fitted.width - 3, y: edit.origin.y * fitted.height)
            .focused($textFocused)
            .onSubmit(commitText)
            .onExitCommand { textEdit = nil; canvasFocused = true }
            .onAppear { textFocused = true }
    }

    // MARK: - Geometry

    private var pixelWidth: CGFloat { CGFloat(image.representations.first?.pixelsWide ?? Int(image.size.width)) }

    private func previewBlurRadius(fittedWidth: CGFloat) -> CGFloat {
        guard pixelWidth > 0 else { return 10 }
        return blurSigma(imageWidth: Int(pixelWidth)) * fittedWidth / pixelWidth
    }

    private func fittedSize(in available: CGSize) -> CGSize {
        let size = image.size
        guard size.width > 0, size.height > 0, available.width > 0, available.height > 0 else { return .zero }
        let scale = min(available.width / size.width, available.height / size.height)
        return CGSize(width: size.width * scale, height: size.height * scale)
    }

    private func normalized(_ p: CGPoint, _ fitted: CGSize) -> CGPoint {
        CGPoint(x: min(max(p.x / fitted.width, 0), 1), y: min(max(p.y / fitted.height, 0), 1))
    }

    // MARK: - Interaction

    private func drag(fitted: CGSize) -> some Gesture {
        DragGesture(minimumDistance: 0, coordinateSpace: .local)
            .onChanged { value in
                guard fitted.width > 0 else { return }
                if mode == nil {
                    if textEdit != nil { commitText() }
                    mode = begin(at: value.startLocation, fitted: fitted)
                }
                update(value, fitted: fitted)
            }
            .onEnded { value in
                defer { mode = nil; workingBlur = nil; workingAnnotations = nil }
                guard fitted.width > 0 else { return }
                let moved = abs(value.translation.width) + abs(value.translation.height)
                if moved < 3 {
                    tap(at: value.location, fitted: fitted)
                    return
                }
                finish(value, fitted: fitted)
                if textEdit == nil { canvasFocused = true }
            }
    }

    private func begin(at p: CGPoint, fitted: CGSize) -> DragMode {
        switch hitTest(p, blurRegions: blurRegions, annotations: annotations, selected: selected, canvasSize: fitted) {
        case .handle(.blur(let i), _)?:
            return .resizeBlur(i, blurRegions[i])
        case .handle(.annotation(let id), let h)?:
            guard let kind = annotations.first(where: { $0.id == id })?.kind else { return .idle }
            if case .rectangle(let r) = kind, h == .resize { return .resizeBox(id, r) }
            return .moveArrowEnd(id, isStart: h == .arrowStart, kind)
        case .item(.blur(let i))?:
            selected = .blur(i)
            return .moveBlur(i, blurRegions[i])
        case .item(.annotation(let id))?:
            selected = .annotation(id)
            guard let kind = annotations.first(where: { $0.id == id })?.kind else { return .idle }
            return .moveAnnotation(id, kind)
        case nil:
            selected = nil
            return [.blur, .rectangle, .arrow].contains(tool) ? .create(start: p) : .idle
        }
    }

    private func update(_ value: DragGesture.Value, fitted: CGSize) {
        let d = CGVector(dx: value.translation.width / fitted.width, dy: value.translation.height / fitted.height)
        let minSize = CGSize(width: Self.minSide / fitted.width, height: Self.minSide / fitted.height)
        switch mode {
        case .create(let start)?:
            let a = normalized(start, fitted), b = normalized(value.location, fitted)
            let rect = CGRect(x: min(a.x, b.x), y: min(a.y, b.y), width: abs(b.x - a.x), height: abs(b.y - a.y))
            switch tool {
            case .blur: workingBlur = blurRegions + [rect]
            case .rectangle: workingAnnotations = annotations + [Annotation(id: Self.draftID, kind: .rectangle(rect))]
            case .arrow: workingAnnotations = annotations + [Annotation(id: Self.draftID, kind: .arrow(from: a, to: b))]
            default: break
            }
        case .moveBlur(let i, let original)?:
            var next = blurRegions
            next[i] = CGRect(x: min(max(0, original.minX + d.dx), 1 - original.width),
                             y: min(max(0, original.minY + d.dy), 1 - original.height),
                             width: original.width, height: original.height)
            workingBlur = next
        case .resizeBlur(let i, let original)?:
            var next = blurRegions
            next[i] = resizedRect(original, by: d, minSize: minSize)
            workingBlur = next
        case .moveAnnotation(let id, let kind)?:
            workingAnnotations = replacing(id, with: translated(kind, by: d))
        case .resizeBox(let id, let r)?:
            workingAnnotations = replacing(id, with: .rectangle(resizedRect(r, by: d, minSize: minSize)))
        case .moveArrowEnd(let id, let isStart, let kind)?:
            guard case .arrow(let from, let to) = kind else { return }
            let p = normalized(value.location, fitted)
            workingAnnotations = replacing(id, with: isStart ? .arrow(from: p, to: to) : .arrow(from: from, to: p))
        case .idle?, nil:
            break
        }
    }

    private func finish(_ value: DragGesture.Value, fitted: CGSize) {
        switch mode {
        case .create?:
            if tool == .blur, let next = workingBlur, let r = next.last {
                let v = denormalizedRect(r, in: fitted)
                guard v.width >= Self.minSide, v.height >= Self.minSide else { return }
                onCommitBlur(next)
                selected = .blur(next.count - 1)
            } else if var next = workingAnnotations, let draft = next.last {
                guard isBigEnough(draft.kind, fitted: fitted) else { return }
                let real = Annotation(kind: draft.kind)
                next[next.count - 1] = real
                onCommitAnnotations(next)
                selected = .annotation(real.id)
            }
        case .moveBlur?, .resizeBlur?:
            if let next = workingBlur, next != blurRegions { onCommitBlur(next) }
        case .moveAnnotation?, .resizeBox?, .moveArrowEnd?:
            if let next = workingAnnotations, next != annotations { onCommitAnnotations(next) }
        case .idle?, nil:
            break
        }
    }

    private func isBigEnough(_ kind: Annotation.Kind, fitted: CGSize) -> Bool {
        switch kind {
        case .rectangle(let r):
            let v = denormalizedRect(r, in: fitted)
            return v.width >= Self.minSide && v.height >= Self.minSide
        case .arrow(let a, let b):
            let dx = (b.x - a.x) * fitted.width, dy = (b.y - a.y) * fitted.height
            return dx * dx + dy * dy >= 64
        case .text:
            return true
        }
    }

    private func tap(at p: CGPoint, fitted: CGSize) {
        let now = Date()
        let isDouble = lastTap.map { now.timeIntervalSince($0.time) < 0.35 && abs($0.point.x - p.x) + abs($0.point.y - p.y) < 6 } ?? false
        lastTap = (now, p)
        if case .item(.annotation(let id))? = hitTest(p, blurRegions: blurRegions, annotations: annotations,
                                                     selected: nil, canvasSize: fitted),
           let annotation = annotations.first(where: { $0.id == id }),
           case .text(let origin, let string) = annotation.kind,
           isDouble || tool == .text {
            textEdit = TextEdit(id: id, origin: origin, text: string)
            return
        }
        if tool == .text, selected == nil {
            textEdit = TextEdit(id: nil, origin: normalized(p, fitted), text: "")
            return
        }
        canvasFocused = true
    }

    private func commitText() {
        guard let edit = textEdit else { return }
        textEdit = nil
        let text = edit.text.trimmingCharacters(in: .whitespacesAndNewlines)
        var next = annotations
        if let id = edit.id, let i = next.firstIndex(where: { $0.id == id }) {
            if text.isEmpty { next.remove(at: i); selected = nil } else { next[i].kind = .text(origin: edit.origin, string: text) }
        } else if !text.isEmpty {
            let created = Annotation(kind: .text(origin: edit.origin, string: text))
            next.append(created)
            selected = .annotation(created.id)
        }
        if next != annotations { onCommitAnnotations(next) }
        canvasFocused = true
    }

    private func replacing(_ id: UUID, with kind: Annotation.Kind) -> [Annotation] {
        annotations.map { $0.id == id ? Annotation(id: id, kind: kind) : $0 }
    }

    private func deleteSelected() {
        switch selected {
        case .blur(let i)? where blurRegions.indices.contains(i):
            var next = blurRegions
            next.remove(at: i)
            onCommitBlur(next)
        case .annotation(let id)?:
            onCommitAnnotations(annotations.filter { $0.id != id })
        default:
            break
        }
        selected = nil
    }
}
