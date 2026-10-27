import AppKit
import SwiftUI
import RecordCore

struct EditorView: View {
    @EnvironmentObject var state: AppState
    @Environment(\.openWindow) private var openWindow
    @State private var selection: UUID?
    @State private var blurTool = false
    @State private var selectedRegion: Int?

    var body: some View {
        NavigationSplitView {
            stepList
                .navigationSplitViewColumnWidth(min: 220, ideal: 280)
        } detail: {
            detail
        }
        .toolbar {
            ToolbarItem(placement: .principal) {
                TextField("教程标题", text: titleBinding)
                    .textFieldStyle(.roundedBorder)
                    .frame(width: 260)
            }
            ToolbarItemGroup(placement: .primaryAction) {
                if state.isRecording {
                    Button { state.openWindowAction = openWindow; state.stopRecording() } label: { Label("停止录制", systemImage: "stop.circle") }
                } else {
                    Button { state.openWindowAction = openWindow; state.startRecording() } label: { Label("开始录制", systemImage: "record.circle") }
                }
                Button { state.export() } label: { Label("导出", systemImage: "square.and.arrow.up") }
                    .disabled(state.tutorial.isEmpty)
            }
        }
        .frame(minWidth: 760, minHeight: 480)
        .onAppear { state.openWindowAction = openWindow }
        .onChange(of: selection) { selectedRegion = nil }
    }

    private var stepList: some View {
        List(selection: $selection) {
            ForEach(Array(state.tutorial.steps.enumerated()), id: \.element.id) { index, step in
                StepRow(index: index + 1, step: step)
                    .tag(step.id)
                    .contextMenu { Button("删除") { delete(step.id) } }
            }
            .onMove { state.moveSteps(fromOffsets: $0, toOffset: $1) }
            .onDelete { offsets in
                offsets.map { state.tutorial.steps[$0].id }.forEach(delete)
            }
        }
        .onDeleteCommand { if let id = selection { delete(id) } }
        .overlay {
            if state.tutorial.isEmpty {
                Text("还没有步骤。从菜单栏开始录制。")
                    .foregroundStyle(.secondary)
                    .multilineTextAlignment(.center)
                    .padding()
            }
        }
    }

    @ViewBuilder
    private var detail: some View {
        if let id = selection, let step = state.tutorial.steps.first(where: { $0.id == id }) {
            VStack(alignment: .leading, spacing: 12) {
                blurTools(step)
                BlurCanvas(image: state.fullImage(for: step), regions: step.blurRegions, editing: blurTool,
                           selected: $selectedRegion,
                           onCommit: { state.setBlurRegions(id: id, $0) })
                    .id(id)
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
                Text("描述").font(.headline)
                TextEditor(text: descriptionBinding(id))
                    .id(id)  // fresh editor per step, so text never carries over between selections
                    .font(.body)
                    .frame(height: 80)
                    .overlay(RoundedRectangle(cornerRadius: 6).stroke(.quaternary))
            }
            .padding()
        } else {
            Text("选择一个步骤").foregroundStyle(.secondary)
        }
    }

    private func blurTools(_ step: Step) -> some View {
        HStack(spacing: 8) {
            Toggle(isOn: $blurTool) { Label("模糊", systemImage: "eye.slash") }
                .toggleStyle(.button)
            Button("删除选中区域") {
                guard let i = selectedRegion, step.blurRegions.indices.contains(i) else { return }
                var next = step.blurRegions
                next.remove(at: i)
                selectedRegion = nil
                state.setBlurRegions(id: step.id, next)
            }
            .disabled(!blurTool || selectedRegion == nil)
            Button("清除全部模糊") {
                selectedRegion = nil
                state.setBlurRegions(id: step.id, [])
            }
            .disabled(step.blurRegions.isEmpty)
            if blurTool {
                Text("在图上拖动框选要模糊的区域；拖动框可移动，拖右下角可调整大小，按 Delete 删除")
                    .font(.caption)
                    .foregroundStyle(.secondary)
                    .lineLimit(1)
            }
        }
    }

    private var titleBinding: Binding<String> {
        Binding(get: { state.tutorial.title }, set: { state.setTitle($0) })
    }

    private func descriptionBinding(_ id: UUID) -> Binding<String> {
        Binding(get: { state.tutorial.steps.first(where: { $0.id == id })?.description ?? "" },
                set: { state.setDescription(id: id, to: $0) })
    }

    private func delete(_ id: UUID) {
        if selection == id { selection = nil }
        state.deleteStep(id: id)
    }
}

private struct StepRow: View {
    @EnvironmentObject var state: AppState
    let index: Int
    let step: Step

    var body: some View {
        HStack(spacing: 10) {
            Image(nsImage: state.thumbnail(for: step))
                .resizable()
                .scaledToFit()
                .frame(width: 80, height: 48)
                .clipShape(RoundedRectangle(cornerRadius: 4))
            VStack(alignment: .leading, spacing: 2) {
                Text("步骤 \(index)").font(.caption).foregroundStyle(.secondary)
                Text(step.description.split(separator: "\n", omittingEmptySubsequences: false).first.map(String.init) ?? "")
                    .lineLimit(1)
            }
        }
        .padding(.vertical, 2)
    }
}
