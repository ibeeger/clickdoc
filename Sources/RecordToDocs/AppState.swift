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
        lastError = nil
        isRecording = true
    }

    func stopRecording() {
        guard isRecording else { return }
        isRecording = false
        show(window: "editor")
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
        hasUnexportedChanges = false
    }
}
