import Foundation
import RecordCore

private func step(_ d: String) -> Step { Step(description: d, pngData: Data([1])) }

let tutorialTests: [TestCase] = [
    ("testAppendAndRemove", {
        var t = Tutorial()
        let a = step("A"), b = step("B"), c = step("C")
        t.append(a); t.append(b); t.append(c)
        expectEqual(t.steps.count, 3)
        t.remove(id: b.id)
        expectEqual(t.steps.map(\.id), [a.id, c.id])
    }),
    ("testMoveDownAndUp", {
        let a = step("A"), b = step("B"), c = step("C")
        var t = Tutorial(steps: [a, b, c])
        t.move(fromOffsets: IndexSet([0]), toOffset: 3)
        expectEqual(t.steps.map(\.description), ["B", "C", "A"])
        t.move(fromOffsets: IndexSet([2]), toOffset: 0)
        expectEqual(t.steps.map(\.description), ["A", "B", "C"])
    }),
    ("testUpdateDescription", {
        let a = step("A"), b = step("B"), c = step("C")
        var t = Tutorial(steps: [a, b, c])
        t.updateDescription(id: b.id, to: "B2")
        expectEqual(t.steps.map(\.description), ["A", "B2", "C"])
    }),
    ("testUpdateLastStepOnEmptyIsNoop", {
        var t = Tutorial()
        t.updateLastStep { $0.description = "x" }
        expect(t.isEmpty)
        expectEqual(t.title, "未命名教程")
    }),
]
