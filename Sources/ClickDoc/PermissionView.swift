import SwiftUI

struct PermissionView: View {
    @State private var accessibility = Permissions.accessibilityGranted
    @State private var screen = Permissions.screenRecordingGranted
    private let timer = Timer.publish(every: 1, on: .main, in: .common).autoconnect()

    var body: some View {
        VStack(alignment: .leading, spacing: 14) {
            Text("录制需要以下两项系统权限").font(.headline)
            row("辅助功能", granted: accessibility) {
                Permissions.requestAccessibility()
                Permissions.openAccessibilitySettings()
            }
            row("屏幕录制", granted: screen) {
                Permissions.requestScreenRecording()
                Permissions.openScreenRecordingSettings()
            }
            Text("授权后可能需要重启本应用").font(.footnote).foregroundStyle(.secondary)
        }
        .padding(20)
        .frame(width: 360)
        .onReceive(timer) { _ in
            accessibility = Permissions.accessibilityGranted
            screen = Permissions.screenRecordingGranted
        }
    }

    private func row(_ name: String, granted: Bool, action: @escaping () -> Void) -> some View {
        HStack {
            Image(systemName: granted ? "checkmark.circle.fill" : "xmark.circle.fill")
                .foregroundStyle(granted ? .green : .red)
            Text(name)
            Spacer()
            Text(granted ? "已授权" : "未授权").foregroundStyle(.secondary)
            Button("去设置", action: action).disabled(granted)
        }
    }
}
