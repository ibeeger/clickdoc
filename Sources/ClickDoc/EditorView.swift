import AppKit
import SwiftUI
import RecordCore

struct EditorView: View {
    @EnvironmentObject var state: AppState
    @Environment(\.openWindow) private var openWindow
    @State private var selection: Set<UUID> = []
    /// Steps waiting for the user to confirm a multi-step delete.
    @State private var pendingDelete: Set<UUID>?
    @State private var tool: EditorTool = .select
    @State private var selectedItem: CanvasItem?

    var body: some View {
        NavigationSplitView {
            stepList
                .navigationSplitViewColumnWidth(min: 220, ideal: 280)
        } detail: {
            detail
        }
        .toolbar {
            ToolbarItem(placement: .principal) {
                TextField(L("教程标题"), text: titleBinding)
                    .textFieldStyle(.roundedBorder)
                    .frame(width: 260)
            }
            ToolbarItemGroup(placement: .primaryAction) {
                if state.isRecording {
                    Button { state.openWindowAction = openWindow; state.stopRecording() } label: { Label(L("停止录制"), systemImage: "stop.circle") }
                } else {
                    Button { state.openWindowAction = openWindow; state.startRecording() } label: { Label(L("开始录制"), systemImage: "record.circle") }
                }
                Menu {
                    Button(L("Markdown 文件夹…")) { state.export(.markdownFolder) }
                    Button(L("Notion 导入包（.zip）…")) { state.export(.notionZip) }
                    Button("PDF…") { state.export(.pdf) }
                } label: {
                    Label(L("导出"), systemImage: "square.and.arrow.up")
                } primaryAction: {
                    state.export(.markdownFolder)
                }
                .disabled(state.tutorial.isEmpty)
            }
        }
        .frame(minWidth: 760, minHeight: 480)
        .onAppear { state.openWindowAction = openWindow }
        .onChange(of: selection) { selectedItem = nil }
        .confirmationDialog(L("删除 \(pendingDelete?.count ?? 0) 个步骤？"),
                            isPresented: Binding(get: { pendingDelete != nil }, set: { if !$0 { pendingDelete = nil } }),
                            presenting: pendingDelete) { ids in
            Button(L("删除"), role: .destructive) { performDelete(ids) }
            Button(L("取消"), role: .cancel) {}
        } message: { _ in
            Text(L("删除后无法恢复。"))
        }
    }

    private var stepList: some View {
        List(selection: $selection) {
            ForEach(Array(state.tutorial.steps.enumerated()), id: \.element.id) { index, step in
                StepRow(index: index + 1, step: step)
                    .tag(step.id)
                    .contextMenu {
                        let targets = deletionTargets(clicked: step.id, selection: selection)
                        Button(targets.count > 1 ? L("删除所选的 \(targets.count) 个步骤") : L("删除")) { requestDelete(targets) }
                        Divider()
                        Button(L("全选")) { selectAll() }
                    }
            }
            .onMove { state.moveSteps(fromOffsets: $0, toOffset: $1) }
            .onDelete { offsets in
                requestDelete(Set(offsets.map { state.tutorial.steps[$0].id }))
            }
        }
        .onDeleteCommand { requestDelete(selection) }
        .onKeyPress(keys: ["a"]) { press in
            guard press.modifiers.contains(.command) else { return .ignored }
            selectAll()
            return .handled
        }
        .overlay {
            if state.tutorial.isEmpty {
                Text(L("还没有步骤。从菜单栏开始录制。"))
                    .foregroundStyle(.secondary)
                    .multilineTextAlignment(.center)
                    .padding()
            }
        }
    }

    @ViewBuilder
    private var detail: some View {
        if selection.count > 1 {
            VStack(spacing: 12) {
                Text(L("已选择 \(selection.count) 个步骤")).font(.title3)
                Button(role: .destructive) { requestDelete(selection) } label: {
                    Label(L("删除这 \(selection.count) 个步骤"), systemImage: "trash")
                }
                Text(L("按住 ⌘ 点选多个，按住 ⇧ 连续选择，⌘A 全选"))
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
        } else if let id = selection.first, let step = state.tutorial.steps.first(where: { $0.id == id }) {
            VStack(alignment: .leading, spacing: 12) {
                markupTools(step)
                AnnotationCanvas(image: state.fullImage(for: step), blurRegions: step.blurRegions,
                                 annotations: step.annotations, tool: tool, selected: $selectedItem,
                                 onCommitBlur: { state.setBlurRegions(id: id, $0) },
                                 onCommitAnnotations: { state.setAnnotations(id: id, $0) })
                    .id(id)
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
                Text(L("描述")).font(.headline)
                TextEditor(text: descriptionBinding(id))
                    .id(id)  // fresh editor per step, so text never carries over between selections
                    .font(.body)
                    .frame(height: 80)
                    .overlay(RoundedRectangle(cornerRadius: 6).stroke(.quaternary))
            }
            .padding()
        } else {
            Text(L("选择一个步骤")).foregroundStyle(.secondary)
        }
    }

    private func markupTools(_ step: Step) -> some View {
        HStack(spacing: 8) {
            Picker(L("工具"), selection: $tool) {
                ForEach(EditorTool.allCases) { Label($0.title, systemImage: $0.icon).tag($0) }
            }
            .pickerStyle(.segmented)
            .labelsHidden()
            .fixedSize()
            Button(L("删除选中")) { deleteSelected(in: step) }
                .disabled(selectedItem == nil)
            Button(L("清除全部")) {
                selectedItem = nil
                state.setBlurRegions(id: step.id, [])
                state.setAnnotations(id: step.id, [])
            }
            .disabled(step.blurRegions.isEmpty && step.annotations.isEmpty)
            Text(tool.hint)
                .font(.caption)
                .foregroundStyle(.secondary)
                .lineLimit(1)
        }
    }

    private func deleteSelected(in step: Step) {
        switch selectedItem {
        case .blur(let i)? where step.blurRegions.indices.contains(i):
            var next = step.blurRegions
            next.remove(at: i)
            state.setBlurRegions(id: step.id, next)
        case .annotation(let id)?:
            state.setAnnotations(id: step.id, step.annotations.filter { $0.id != id })
        default:
            break
        }
        selectedItem = nil
    }

    private var titleBinding: Binding<String> {
        Binding(get: { state.tutorial.title }, set: { state.setTitle($0) })
    }

    private func descriptionBinding(_ id: UUID) -> Binding<String> {
        Binding(get: { state.tutorial.steps.first(where: { $0.id == id })?.description ?? "" },
                set: { state.setDescription(id: id, to: $0) })
    }

    private func selectAll() {
        selection = Set(state.tutorial.steps.map(\.id))
    }

    /// Deleting several steps asks first; there is no undo.
    private func requestDelete(_ ids: Set<UUID>) {
        guard !ids.isEmpty else { return }
        if ids.count > 1 { pendingDelete = ids } else { performDelete(ids) }
    }

    private func performDelete(_ ids: Set<UUID>) {
        let next = selectionAfterDeleting(ids, from: state.tutorial.steps)
        state.deleteSteps(ids: ids)
        selection = next.map { [$0] } ?? []
        pendingDelete = nil
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
                Text(L("步骤 \(index)")).font(.caption).foregroundStyle(.secondary)
                Text(step.description.split(separator: "\n", omittingEmptySubsequences: false).first.map(String.init) ?? "")
                    .lineLimit(1)
            }
        }
        .padding(.vertical, 2)
    }
}
