import Foundation
import RecordCore

let exporterTests: [TestCase] = [
    ("testImageFileName", {
        expectEqual(imageFileName(index: 1), "step-01.png")
        expectEqual(imageFileName(index: 9), "step-09.png")
        expectEqual(imageFileName(index: 12), "step-12.png")
        expectEqual(imageFileName(index: 100), "step-100.png")
    }),
    ("testRenderMarkdown", {
        let t = Tutorial(title: "演示", steps: [
            Step(description: "第一步", pngData: Data([1])),
            Step(description: "第二步", pngData: Data([2])),
        ])
        expectEqual(renderMarkdown(t),
            "# 演示\n\n## 步骤 1\n\n第一步\n\n![步骤 1](images/step-01.png)\n\n## 步骤 2\n\n第二步\n\n![步骤 2](images/step-02.png)\n")
    }),
    ("testMarkdownSkipsEmptyDescription", {
        let t = Tutorial(title: "T", steps: [Step(description: "  ", pngData: Data([1]))])
        expectEqual(renderMarkdown(t), "# T\n\n## 步骤 1\n\n![步骤 1](images/step-01.png)\n")
    }),
    ("testSanitizeFolderName", {
        expectEqual(sanitizeFolderName("a/b:c"), "a-b-c")
        expectEqual(sanitizeFolderName("a\\b"), "a-b")
        expectEqual(sanitizeFolderName("   "), "未命名教程")
        expectEqual(sanitizeFolderName(""), "未命名教程")
        expectEqual(sanitizeFolderName(".hidden"), "hidden")
        expectEqual(sanitizeFolderName("  我的教程 "), "我的教程")
        expectEqual(sanitizeFolderName("a\nb"), "a-b")
    }),
    ("testUniqueFolderURL", {
        let dir = URL(fileURLWithPath: "/tmp/x")
        let taken: Set<String> = ["X", "X 2"]
        let u = uniqueFolderURL(in: dir, name: "X") { taken.contains($0.lastPathComponent) }
        expectEqual(u.lastPathComponent, "X 3")
        let free = uniqueFolderURL(in: dir, name: "Y") { taken.contains($0.lastPathComponent) }
        expectEqual(free.lastPathComponent, "Y")
    }),
    ("testExportWritesFiles", {
        let fm = FileManager.default
        let root = fm.temporaryDirectory.appendingPathComponent("rtd-test-\(UUID().uuidString)")
        try fm.createDirectory(at: root, withIntermediateDirectories: true)
        defer { try? fm.removeItem(at: root) }
        let t = Tutorial(title: "导出/测试", steps: [
            Step(description: "一", pngData: Data([1, 2, 3])),
            Step(description: "二", pngData: Data([4, 5])),
        ])
        let out = try exportTutorial(t, to: root)
        expectEqual(out.lastPathComponent, "导出-测试")
        let md = try String(contentsOf: out.appendingPathComponent("tutorial.md"), encoding: .utf8)
        expectEqual(md, renderMarkdown(t))
        expectEqual(try Data(contentsOf: out.appendingPathComponent("images/step-01.png")), Data([1, 2, 3]))
        expectEqual(try Data(contentsOf: out.appendingPathComponent("images/step-02.png")), Data([4, 5]))
        let again = try exportTutorial(t, to: root)
        expect(again.lastPathComponent.hasSuffix(" 2"), again.lastPathComponent)
        expectEqual(try Data(contentsOf: out.appendingPathComponent("images/step-01.png")), Data([1, 2, 3]))
    }),
]
