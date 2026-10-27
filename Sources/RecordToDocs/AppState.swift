import AppKit
import SwiftUI
import RecordCore

@MainActor
final class AppState: ObservableObject {
    @Published var tutorial = Tutorial()
    @Published private(set) var isRecording = false
    @Published var lastError: String?
    @Published var hasUnexportedChanges = false

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
        // Inspect synchronously, before the clicked app reacts and changes its UI.
        let info = ElementInspector.inspect(at: click.location)
        if isOwnUI(info: info, at: click.location) { return }
        let previous = chain
        chain = Task { @MainActor [weak self] in
            await previous?.value
            await self?.process(click, info)
        }
    }

    private func isOwnUI(info: ElementInfo, at point: CGPoint) -> Bool {
        let me = getpid()
        if info.pid == me { return true }
        guard let windows = CGWindowListCopyWindowInfo([.optionOnScreenOnly], kCGNullWindowID) as? [[String: Any]] else {
            return false
        }
        // Front-to-back order: the first window containing the point is the one clicked.
        for w in windows {
            guard let dict = w[kCGWindowBounds as String] as? NSDictionary,
                  let bounds = CGRect(dictionaryRepresentation: dict), bounds.contains(point) else { continue }
            if (w[kCGWindowAlpha as String] as? Double ?? 1) == 0 { continue }
            return (w[kCGWindowOwnerPID as String] as? pid_t) == me
        }
        return false
    }

    /// A second click may only upgrade the previous step if it landed on the same spot moments later.
    private func canUpgradeLastStep(with click: RawClick) -> Bool {
        guard let last = lastStepClick, !tutorial.isEmpty else { return false }
        let dx = click.location.x - last.location.x, dy = click.location.y - last.location.y
        return dx * dx + dy * dy <= 25 && click.timestamp.timeIntervalSince(last.time) <= max(NSEvent.doubleClickInterval, 0.5) + 0.2
    }

    private func process(_ click: RawClick, _ info: ElementInfo) async {
        switch decideStep(button: click.button, clickCount: click.clickCount,
                          hasPreviousStep: canUpgradeLastStep(with: click)) {
        case .ignore:
            return
        case .upgradeLastToDoubleClick:
            guard let last = tutorial.steps.last else { return }
            let i = stepInfo[last.id]
            tutorial.updateLastStep {
                $0.description = describeStep(action: .doubleClick, appName: i?.appName, elementName: i?.elementName)
            }
            lastStepClick = nil
        case .newStep(let action):
            do {
                let png = try await capturer.highlightedPNG(for: click.location)
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
        lastStepClick = nil
        hasUnexportedChanges = false
    }
}
