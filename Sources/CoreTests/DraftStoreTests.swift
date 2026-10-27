import Foundation
import CoreGraphics
import RecordCore

private func tempStore() throws -> (DraftStore, URL) {
    let dir = FileManager.default.temporaryDirectory.appendingPathComponent("rtd-draft-\(UUID().uuidString)")
    return (DraftStore(directory: dir), dir)
}

private func imageFiles(_ dir: URL) -> [String] {
    ((try? FileManager.default.contentsOfDirectory(atPath: dir.appendingPathComponent("images").path)) ?? []).sorted()
}

let draftStoreTests: [TestCase] = [
    ("testDraftRoundTrip", {
        let (store, dir) = try tempStore()
        defer { try? FileManager.default.removeItem(at: dir) }
        var a = Step(description: "第一步", pngData: Data([1, 2, 3]), capturedAt: Date(timeIntervalSince1970: 1000))
        a.blurRegions = [CGRect(x: 0.1, y: 0.2, width: 0.3, height: 0.4)]
        let b = Step(description: "第二步", pngData: Data([4, 5]), capturedAt: Date(timeIntervalSince1970: 2000))
        let t = Tutorial(title: "草稿", steps: [a, b])
        try store.save(t, hasUnexportedChanges: true)
        guard let loaded = try store.load() else { return expect(false, "nothing loaded") }
        expectEqual(loaded.tutorial, t)
        expectEqual(loaded.hasUnexportedChanges, true)
    }),
    ("testDraftLoadEmptyIsNil", {
        let (store, dir) = try tempStore()
        defer { try? FileManager.default.removeItem(at: dir) }
        expect(try store.load() == nil)
    }),
    ("testDraftRemovesImagesOfDeletedSteps", {
        let (store, dir) = try tempStore()
        defer { try? FileManager.default.removeItem(at: dir) }
        let a = Step(description: "a", pngData: Data([1]))
        let b = Step(description: "b", pngData: Data([2]))
        var t = Tutorial(steps: [a, b])
        try store.save(t, hasUnexportedChanges: true)
        expectEqual(imageFiles(dir).count, 2)
        t.remove(id: a.id)
        try store.save(t, hasUnexportedChanges: false)
        expectEqual(imageFiles(dir), ["\(b.id.uuidString).png"])
        expectEqual(try store.load()?.hasUnexportedChanges, false)
    }),
    ("testDraftSkipsStepWithMissingImage", {
        let (store, dir) = try tempStore()
        defer { try? FileManager.default.removeItem(at: dir) }
        let a = Step(description: "a", pngData: Data([1]))
        let b = Step(description: "b", pngData: Data([2]))
        try store.save(Tutorial(title: "T", steps: [a, b]), hasUnexportedChanges: true)
        try FileManager.default.removeItem(at: dir.appendingPathComponent("images/\(a.id.uuidString).png"))
        expectEqual(try store.load()?.tutorial.steps.map(\.id), [b.id])
    }),
    ("testDraftCorruptManifestThrows", {
        let (store, dir) = try tempStore()
        defer { try? FileManager.default.removeItem(at: dir) }
        try FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        try Data("not json".utf8).write(to: dir.appendingPathComponent("manifest.json"))
        var threw = false
        do { _ = try store.load() } catch { threw = true }
        expect(threw, "corrupt manifest must throw, not silently drop the draft")
    }),
    ("testDraftClear", {
        let (store, dir) = try tempStore()
        defer { try? FileManager.default.removeItem(at: dir) }
        try store.save(Tutorial(steps: [Step(description: "a", pngData: Data([1]))]), hasUnexportedChanges: true)
        try store.clear()
        expect(try store.load() == nil)
        expectEqual(imageFiles(dir), [])
    }),
    ("testMigrateLegacyDraftMovesWhenNewMissing", {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent("rtd-mig-\(UUID().uuidString)")
        defer { try? FileManager.default.removeItem(at: root) }
        let legacy = root.appendingPathComponent("RecordToDocs/draft"), new = root.appendingPathComponent("ClickDoc/draft")
        let step = Step(description: "旧草稿", pngData: Data([9]))
        try DraftStore(directory: legacy).save(Tutorial(title: "旧", steps: [step]), hasUnexportedChanges: true)
        DraftStore.migrateLegacyDraft(from: legacy, to: new)
        expectEqual(try DraftStore(directory: new).load()?.tutorial.steps.map(\.description), ["旧草稿"])
        expect(!FileManager.default.fileExists(atPath: legacy.path), "legacy moved")
    }),
    ("testMigrateLegacyDraftKeepsExistingNewDraft", {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent("rtd-mig-\(UUID().uuidString)")
        defer { try? FileManager.default.removeItem(at: root) }
        let legacy = root.appendingPathComponent("RecordToDocs/draft"), new = root.appendingPathComponent("ClickDoc/draft")
        try DraftStore(directory: legacy).save(Tutorial(title: "旧"), hasUnexportedChanges: true)
        try DraftStore(directory: new).save(Tutorial(title: "新"), hasUnexportedChanges: true)
        DraftStore.migrateLegacyDraft(from: legacy, to: new)
        expectEqual(try DraftStore(directory: new).load()?.tutorial.title, "新")
        expect(FileManager.default.fileExists(atPath: legacy.path), "legacy left alone")
    }),
]
