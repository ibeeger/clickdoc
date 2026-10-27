import Foundation
import RecordCore

private func steps(_ n: Int) -> [Step] { (1...n).map { Step(description: "\($0)", pngData: Data([UInt8($0)])) } }

let batchDeleteTests: [TestCase] = [
    ("testRemoveManyKeepsOrderOfTheRest", {
        let s = steps(5)
        var t = Tutorial(steps: s)
        t.remove(ids: [s[1].id, s[3].id])
        expectEqual(t.steps.map(\.description), ["1", "3", "5"])
        t.remove(ids: [])
        expectEqual(t.steps.count, 3)
    }),
    ("testDeletionTargetsFollowSelection", {
        let a = UUID(), b = UUID(), c = UUID()
        // Right-clicking a selected row deletes the whole selection; an unselected row deletes just itself.
        expectEqual(deletionTargets(clicked: a, selection: [a, b]), [a, b])
        expectEqual(deletionTargets(clicked: c, selection: [a, b]), [c])
    }),
    ("testSelectionAfterDeletingPicksNextThenPrevious", {
        let s = steps(5)
        // Deleting 2 and 3 selects 4 (the step now in their place).
        expectEqual(selectionAfterDeleting([s[1].id, s[2].id], from: s), s[3].id)
        // Deleting the tail selects the step before it.
        expectEqual(selectionAfterDeleting([s[3].id, s[4].id], from: s), s[2].id)
        // Deleting everything selects nothing.
        expectEqual(selectionAfterDeleting(Set(s.map(\.id)), from: s), nil)
    }),
]
