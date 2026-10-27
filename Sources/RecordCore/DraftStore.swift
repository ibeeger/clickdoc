import CoreGraphics
import Foundation

/// Persists the single current tutorial so it survives quitting the app.
/// Layout: `<directory>/manifest.json` and `<directory>/images/<step id>.png` (unblurred originals).
public struct DraftStore {
    public let directory: URL
    private let fileManager: FileManager

    public init(directory: URL, fileManager: FileManager = .default) {
        self.directory = directory
        self.fileManager = fileManager
    }

    /// `~/Library/Application Support/ClickDoc/draft`, adopting a draft left by the app's former name.
    public static func standard() -> DraftStore {
        let base = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
        let directory = base.appendingPathComponent("ClickDoc/draft", isDirectory: true)
        migrateLegacyDraft(from: base.appendingPathComponent("RecordToDocs/draft", isDirectory: true), to: directory)
        return DraftStore(directory: directory)
    }

    /// Moves a draft from `legacy` to `new` unless a draft already exists at `new`.
    public static func migrateLegacyDraft(from legacy: URL, to new: URL, fileManager: FileManager = .default) {
        guard fileManager.fileExists(atPath: legacy.path), !fileManager.fileExists(atPath: new.path) else { return }
        try? fileManager.createDirectory(at: new.deletingLastPathComponent(), withIntermediateDirectories: true)
        try? fileManager.moveItem(at: legacy, to: new)
    }

    private struct Manifest: Codable {
        var version = 1
        var title: String
        var hasUnexportedChanges: Bool
        var steps: [StepRecord]
    }

    private struct StepRecord: Codable {
        var id: UUID
        var description: String
        var capturedAt: Date
        var blurRegions: [CGRect]
    }

    private var manifestURL: URL { directory.appendingPathComponent("manifest.json") }
    private var imagesURL: URL { directory.appendingPathComponent("images", isDirectory: true) }
    private func imageURL(_ id: UUID) -> URL { imagesURL.appendingPathComponent("\(id.uuidString).png") }

    public func save(_ tutorial: Tutorial, hasUnexportedChanges: Bool) throws {
        try fileManager.createDirectory(at: imagesURL, withIntermediateDirectories: true)
        // Screenshots never change after capture, so each is written once.
        for step in tutorial.steps where !fileManager.fileExists(atPath: imageURL(step.id).path) {
            try step.pngData.write(to: imageURL(step.id), options: .atomic)
        }
        let manifest = Manifest(title: tutorial.title, hasUnexportedChanges: hasUnexportedChanges,
                                steps: tutorial.steps.map {
                                    StepRecord(id: $0.id, description: $0.description,
                                               capturedAt: $0.capturedAt, blurRegions: $0.blurRegions)
                                })
        // Manifest last: a crash mid-save leaves the previous manifest pointing at existing images.
        try JSONEncoder().encode(manifest).write(to: manifestURL, options: .atomic)
        let keep = Set(tutorial.steps.map { "\($0.id.uuidString).png" })
        for name in try fileManager.contentsOfDirectory(atPath: imagesURL.path) where !keep.contains(name) {
            try? fileManager.removeItem(at: imagesURL.appendingPathComponent(name))
        }
    }

    /// nil when there is no draft. Throws on an unreadable manifest so the caller can keep the files.
    public func load() throws -> (tutorial: Tutorial, hasUnexportedChanges: Bool)? {
        guard fileManager.fileExists(atPath: manifestURL.path) else { return nil }
        let manifest = try JSONDecoder().decode(Manifest.self, from: Data(contentsOf: manifestURL))
        var tutorial = Tutorial(title: manifest.title)
        for record in manifest.steps {
            guard let png = try? Data(contentsOf: imageURL(record.id)) else { continue }
            var step = Step(id: record.id, description: record.description, pngData: png, capturedAt: record.capturedAt)
            step.blurRegions = record.blurRegions
            tutorial.append(step)
        }
        return (tutorial, manifest.hasUnexportedChanges)
    }

    public func clear() throws {
        if fileManager.fileExists(atPath: directory.path) { try fileManager.removeItem(at: directory) }
    }
}
