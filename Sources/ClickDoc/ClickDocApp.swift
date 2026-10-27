import SwiftUI

@main
struct ClickDocApp: App {
    @StateObject private var state = AppState()

    var body: some Scene {
        MenuBarExtra("ClickDoc", systemImage: state.isRecording ? "record.circle.fill" : "record.circle") {
            MenuContent().environmentObject(state)
        }
        Window("编辑教程", id: "editor") {
            EditorView().environmentObject(state)
        }
        Window("需要权限", id: "permissions") {
            PermissionView()
        }
        .windowResizability(.contentSize)
    }
}

private struct MenuContent: View {
    @EnvironmentObject var state: AppState
    @Environment(\.openWindow) private var openWindow

    var body: some View {
        Group {
            if let error = state.lastError {
                Text(error)
                Divider()
            }
            if state.isRecording {
                Button("停止录制") { inject(); state.stopRecording() }
            } else {
                Button("开始录制") { inject(); state.startRecording() }
            }
            Button("打开编辑器") { inject(); state.show(window: "editor") }
            Button("新建教程") { state.newTutorial() }
            Divider()
            Button("退出") { NSApp.terminate(nil) }
        }
        .onAppear(perform: inject)
    }

    /// Menu-style MenuBarExtra content may never get onAppear, so inject on every action too.
    private func inject() { state.openWindowAction = openWindow }
}
