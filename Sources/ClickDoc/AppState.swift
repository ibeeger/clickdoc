import AppKit
import Combine
import SwiftUI
import RecordCore

@MainActor
final class AppState: ObservableObject {
    @Published var tutorial = Tutorial()
    @Published private(set) var isRecording = false
    @Published var lastError: String?
    @Published var hasUnexportedChanges = false

    enum CaptureScope: String, CaseIterable {
        case window, screen
        var title: String { self == .window ? "当前窗口" : "整个屏幕" }
    }

    @Published var captureScope: CaptureScope =
        CaptureScope(rawValue: UserDefaults.standard.string(forKey: "captureScope") ?? "") ?? .window {
        didSet { UserDefaults.standard.set(captureScope.rawValue, forKey: "captureScope") }
    }

    /// Injected from a view, since SwiftUI only exposes `openWindow` through the environment.
    var openWindowAction: OpenWindowAction?

    private var monitor: ClickMonitor?
    private let capturer = ScreenCapturer()
    /// Tail of the serial processing chain; every click waits for the one before it.
    private var chain: Task<Void, Never>?
    /// App and element names per step, kept so a double-click can relabel the step.
    private var stepInfo: [UUID: ElementInfo] = [:]
    /// Where and when the most recent step's click happened.
    private var lastStepClick: (location: CGPoint, time: Date)?
    private let draftStore = DraftStore.standard()
    /// Serializes draft writes off the main thread.
    private let saveQueue = DispatchQueue(label: "ClickDoc.draft-save")
    private var cancellables: Set<AnyCancellable> = []

    init() {
        restoreDraft()
        Publishers.CombineLatest($tutorial, $hasUnexportedChanges)
            .dropFirst()
            .debounce(for: .milliseconds(400), scheduler: RunLoop.main)
            .sink { [weak self] tutorial, dirty in self?.saveDraft(tutorial, dirty: dirty) }
            .store(in: &cancellables)
        NotificationCenter.default.publisher(for: NSApplication.willTerminateNotification)
            .sink { [weak self] _ in
                guard let self else { return }
                let store = draftStore, tutorial = tutorial, dirty = hasUnexportedChanges
                saveQueue.sync { try? store.save(tutorial, hasUnexportedChanges: dirty) }
            }
            .store(in: &cancellables)
    }

    private func restoreDraft() {
        do {
            if let draft = try draftStore.load() {
                tutorial = draft.tutorial
                hasUnexportedChanges = draft.hasUnexportedChanges
            }
        } catch {
            // Keep the unreadable draft for manual recovery instead of overwriting it.
            let aside = draftStore.directory.deletingLastPathComponent()
                .appendingPathComponent("draft-unreadable-\(Int(Date().timeIntervalSince1970))")
            try? FileManager.default.moveItem(at: draftStore.directory, to: aside)
            lastError = "上次的草稿无法读取，已另存到 \(aside.path)"
        }
    }

    private func saveDraft(_ tutorial: Tutorial, dirty: Bool) {
        let store = draftStore
        saveQueue.async {
            do {
                try store.save(tutorial, hasUnexportedChanges: dirty)
            } catch {
                DispatchQueue.main.async { [weak self] in
                    self?.lastError = "自动保存草稿失败：\(error.localizedDescription)"
                }
            }
        }
    }

    /// Small decoded thumbnails per step; full-size images are never cached for the list.
    private var thumbnailCache: [UUID: NSImage] = [:]
    /// Only the most recently viewed full-size image is kept.
    private var fullImageCache: (id: UUID, image: NSImage)?

    func show(window id: String) {
        NSApp.activate(ignoringOtherApps: true)
        openWindowAction?(id: id)
    }

    func startRecording() {
        guard !isRecording else { return }
        guard Permissions.allGranted else {
            lastError = "缺少录制所需的系统权限"
            show(window: "permissions")
            return
        }
        let monitor = ClickMonitor { [weak self] click in
            MainActor.assumeIsolated { self?.receive(click) }
        }
        guard monitor.start() else {
            lastError = "无法监听鼠标点击，请检查辅助功能权限"
            show(window: "permissions")
            return
        }
        self.monitor = monitor
        lastError = nil
        lastStepClick = nil
        isRecording = true
        let capturer = capturer
        Task { try? await capturer.refresh() }
    }

    func stopRecording() {
        guard isRecording else { return }
        monitor?.stop()
        monitor = nil
        isRecording = false
        show(window: "editor")
    }

    // MARK: - Recording pipeline

    private func receive(_ click: RawClick) {
        // Decide about our own UI first: running AX against our own (busy) main thread would stall.
        let windows = Self.windowSnapshots()
        let hitNumber = Self.windowNumberHit(at: click.location)
        if isOwnUI(at: click.location, windows: windows, hitNumber: hitNumber) { return }
        // Inspect synchronously, before the clicked app reacts and changes its UI.
        let inspected = ElementInspector.inspect(at: click.location)
        let appName = inspected.appName
            ?? topmostWindow(at: click.location, in: windows)?.ownerName
            ?? NSWorkspace.shared.frontmostApplication?.localizedName
        let info = ElementInfo(pid: inspected.pid, appName: appName, elementName: inspected.elementName)
        // Start the screenshot now so it reflects this click, not later ones; only appending is serialized.
        let needsCapture = !(click.button == .left && click.clickCount >= 3)
        let location = click.location
        let crop = captureScope == .window ? Self.windowCrop(at: location, windows: windows, hitNumber: hitNumber) : nil
        // Menus and popovers opened by this very click appear after mouse-down: widen the crop
        // with a window snapshot taken right after the screenshot.
        var refine: (@Sendable () -> CGRect?)?
        if crop != nil {
            refine = { @Sendable () -> CGRect? in
                AppState.windowCrop(at: location, windows: AppState.windowSnapshots(), hitNumber: nil)
            }
        }
        let capturer = capturer
        let capture: Task<Data, Error>? = needsCapture
            ? Task { [refine] in try await capturer.highlightedPNG(for: location, crop: crop, refine: refine) }
            : nil
        let previous = chain
        chain = Task { @MainActor [weak self] in
            await previous?.value
            await self?.process(click, info, capture)
        }
    }

    /// Crop for "当前窗口" mode, decided at click time so it matches what was on screen.
    nonisolated private static func windowCrop(at point: CGPoint, windows: [WindowSnapshot], hitNumber: Int?) -> CGRect? {
        var displayID: CGDirectDisplayID = 0
        var count: UInt32 = 0
        guard CGGetDisplaysWithPoint(point, 1, &displayID, &count) == .success, count > 0 else { return nil }
        return captureCropRect(click: point, windows: windows, displayFrame: CGDisplayBounds(displayID),
                               hitWindowNumber: hitNumber)
    }

    /// The window a mouse-down at `point` actually hits; respects click-through overlays.
    private static func windowNumberHit(at point: CGPoint) -> Int? {
        let mainHeight = CGDisplayBounds(CGMainDisplayID()).height
        let n = NSWindow.windowNumber(at: cocoaPoint(fromGlobal: point, mainDisplayHeight: mainHeight),
                                      belowWindowWithWindowNumber: 0)
        return n > 0 ? n : nil
    }

    nonisolated private static func windowSnapshots() -> [WindowSnapshot] {
        guard let list = CGWindowListCopyWindowInfo([.optionOnScreenOnly], kCGNullWindowID) as? [[String: Any]] else {
            return []
        }
        return list.compactMap { w in
            guard let dict = w[kCGWindowBounds as String] as? NSDictionary,
                  let bounds = CGRect(dictionaryRepresentation: dict),
                  let pid = w[kCGWindowOwnerPID as String] as? pid_t else { return nil }
            return WindowSnapshot(bounds: bounds, ownerPID: pid,
                                  ownerName: w[kCGWindowOwnerName as String] as? String,
                                  alpha: w[kCGWindowAlpha as String] as? Double ?? 1,
                                  layer: w[kCGWindowLayer as String] as? Int ?? 0,
                                  number: w[kCGWindowNumber as String] as? Int ?? 0)
        }
    }

    private func isOwnUI(at point: CGPoint, windows: [WindowSnapshot], hitNumber: Int?) -> Bool {
        let me = getpid()
        if let hit = hitNumber {
            if NSApp.windows.contains(where: { $0.windowNumber == hit }) { return true }
            if let owner = windows.first(where: { $0.number == hit }) { return owner.ownerPID == me }
        }
        return topmostWindow(at: point, in: windows)?.ownerPID == me
    }

    /// A second click may only upgrade the previous step if it landed on the same spot moments later.
    private func canUpgradeLastStep(with click: RawClick) -> Bool {
        guard let last = lastStepClick, !tutorial.isEmpty else { return false }
        let dx = click.location.x - last.location.x, dy = click.location.y - last.location.y
        return dx * dx + dy * dy <= 25 && click.timestamp.timeIntervalSince(last.time) <= max(NSEvent.doubleClickInterval, 0.5) + 0.2
    }

    private func process(_ click: RawClick, _ info: ElementInfo, _ capture: Task<Data, Error>?) async {
        switch decideStep(button: click.button, clickCount: click.clickCount,
                          hasPreviousStep: canUpgradeLastStep(with: click)) {
        case .ignore:
            capture?.cancel()
            return
        case .upgradeLastToDoubleClick:
            capture?.cancel()
            guard let last = tutorial.steps.last else { return }
            let i = stepInfo[last.id]
            tutorial.updateLastStep {
                $0.description = describeStep(action: .doubleClick, appName: i?.appName, elementName: i?.elementName)
            }
            lastStepClick = nil
        case .newStep(let action):
            do {
                guard let capture else { throw CaptureError.renderFailed }
                let png = try await capture.value
                let step = Step(description: describeStep(action: action, appName: info.appName, elementName: info.elementName),
                                pngData: png, capturedAt: click.timestamp)
                tutorial.append(step)
                stepInfo[step.id] = info
                lastStepClick = (click.location, click.timestamp)
                hasUnexportedChanges = true
            } catch {
                lastError = "截图失败：\(error.localizedDescription)"
            }
        }
    }

    func newTutorial() {
        if hasUnexportedChanges && !tutorial.isEmpty {
            NSApp.activate(ignoringOtherApps: true)
            let alert = NSAlert()
            alert.messageText = "当前教程尚未导出，新建将丢失这些步骤。确定继续？"
            alert.addButton(withTitle: "新建")
            alert.addButton(withTitle: "取消")
            guard alert.runModal() == .alertFirstButtonReturn else { return }
        }
        tutorial = Tutorial()
        stepInfo = [:]
        thumbnailCache = [:]
        fullImageCache = nil
        lastStepClick = nil
        hasUnexportedChanges = false
    }

    // MARK: - Editing

    func thumbnail(for step: Step) -> NSImage {
        if let cached = thumbnailCache[step.id] { return cached }
        let image = thumbnailImage(fromPNG: step.pngData, maxPixelSize: 240)
            .flatMap { composeStepImage($0, step: step) }
            .map { NSImage(cgImage: $0, size: .zero) } ?? NSImage()
        thumbnailCache[step.id] = image
        return image
    }

    func fullImage(for step: Step) -> NSImage {
        if let cached = fullImageCache, cached.id == step.id { return cached.image }
        let image = NSImage(data: step.pngData) ?? NSImage()
        fullImageCache = (step.id, image)
        return image
    }

    func setTitle(_ title: String) {
        tutorial.title = title
        hasUnexportedChanges = true
    }

    func setDescription(id: UUID, to text: String) {
        tutorial.updateDescription(id: id, to: text)
        hasUnexportedChanges = true
    }

    func setBlurRegions(id: UUID, _ regions: [CGRect]) {
        tutorial.setBlurRegions(id: id, regions)
        thumbnailCache[id] = nil
        hasUnexportedChanges = true
    }

    func setAnnotations(id: UUID, _ annotations: [Annotation]) {
        tutorial.setAnnotations(id: id, annotations)
        thumbnailCache[id] = nil
        hasUnexportedChanges = true
    }

    func moveSteps(fromOffsets source: IndexSet, toOffset destination: Int) {
        tutorial.move(fromOffsets: source, toOffset: destination)
        hasUnexportedChanges = true
        lastStepClick = nil  // the last step may have changed; never double-click-upgrade a moved step
    }

    func deleteSteps(ids: Set<UUID>) {
        guard !ids.isEmpty else { return }
        tutorial.remove(ids: ids)
        for id in ids {
            stepInfo[id] = nil
            thumbnailCache[id] = nil
        }
        if let cached = fullImageCache, ids.contains(cached.id) { fullImageCache = nil }
        hasUnexportedChanges = true
        lastStepClick = nil
    }

    // MARK: - Export

    enum ExportFormat {
        case markdownFolder, notionZip, pdf

        var panelMessage: String {
            switch self {
            case .markdownFolder: return "选择导出位置，将在其中新建一个以教程标题命名的文件夹"
            case .notionZip: return "选择导出位置，将生成一个 zip，可在 Notion 中用「导入 > Markdown」导入"
            case .pdf: return "选择导出位置，将生成一个以教程标题命名的 PDF"
            }
        }
    }

    func export(_ format: ExportFormat = .markdownFolder) {
        NotificationCenter.default.post(name: .commitPendingEdits, object: nil)
        guard !tutorial.isEmpty else { return }
        NSApp.activate(ignoringOtherApps: true)
        let panel = NSOpenPanel()
        panel.canChooseDirectories = true
        panel.canChooseFiles = false
        panel.canCreateDirectories = true
        panel.allowsMultipleSelection = false
        panel.prompt = "导出到此处"
        panel.message = format.panelMessage
        guard panel.runModal() == .OK, let directory = panel.url else { return }
        do {
            let result: URL
            switch format {
            case .markdownFolder: result = try exportTutorial(tutorial, to: directory)
            case .notionZip: result = try exportNotionZip(tutorial, to: directory)
            case .pdf: result = try exportPDF(tutorial, to: directory)
            }
            hasUnexportedChanges = false
            NSWorkspace.shared.activateFileViewerSelecting([result])
        } catch {
            let alert = NSAlert(error: error)
            alert.messageText = "导出失败"
            alert.informativeText = error.localizedDescription
            alert.runModal()
        }
    }
}
