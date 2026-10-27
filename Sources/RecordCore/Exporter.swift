import Foundation

public func imageFileName(index: Int) -> String {
    String(format: "step-%02d.png", index)
}

public func renderMarkdown(_ tutorial: Tutorial) -> String {
    var blocks = ["# \(tutorial.title)"]
    for (offset, step) in tutorial.steps.enumerated() {
        let n = offset + 1
        blocks.append("## 步骤 \(n)")
        let text = step.description.trimmingCharacters(in: .whitespacesAndNewlines)
        if !text.isEmpty { blocks.append(text) }
        blocks.append("![步骤 \(n)](images/\(imageFileName(index: n)))")
    }
    return blocks.joined(separator: "\n\n") + "\n"
}

public func sanitizeFolderName(_ title: String) -> String {
    let forbidden = CharacterSet(charactersIn: "/:\\").union(.controlCharacters).union(.newlines)
    let replaced = String(title.unicodeScalars.map { forbidden.contains($0) ? "-" : Character($0) })
    var name = replaced.trimmingCharacters(in: .whitespaces)
    while name.hasPrefix(".") { name.removeFirst() }
    name = name.trimmingCharacters(in: .whitespaces)
    return name.isEmpty ? "未命名教程" : name
}

public func uniqueFolderURL(in directory: URL, name: String, exists: (URL) -> Bool) -> URL {
    var candidate = directory.appendingPathComponent(name, isDirectory: true)
    var n = 2
    while exists(candidate) {
        candidate = directory.appendingPathComponent("\(name) \(n)", isDirectory: true)
        n += 1
    }
    return candidate
}

public func exportTutorial(_ tutorial: Tutorial, to directory: URL, fileManager: FileManager = .default) throws -> URL {
    let folder = uniqueFolderURL(in: directory, name: sanitizeFolderName(tutorial.title)) {
        fileManager.fileExists(atPath: $0.path)
    }
    let images = folder.appendingPathComponent("images", isDirectory: true)
    try fileManager.createDirectory(at: images, withIntermediateDirectories: true)
    for (offset, step) in tutorial.steps.enumerated() {
        try step.pngData.write(to: images.appendingPathComponent(imageFileName(index: offset + 1)))
    }
    try renderMarkdown(tutorial).write(to: folder.appendingPathComponent("tutorial.md"), atomically: true, encoding: .utf8)
    return folder
}
