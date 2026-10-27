import Foundation
import RecordCore

let descriptionTests: [TestCase] = [
    ("testDecideStep", {
        expectEqual(decideStep(button: .right, clickCount: 1, hasPreviousStep: false), .newStep(.rightClick))
        expectEqual(decideStep(button: .right, clickCount: 2, hasPreviousStep: true), .newStep(.rightClick))
        expectEqual(decideStep(button: .left, clickCount: 1, hasPreviousStep: true), .newStep(.click))
        expectEqual(decideStep(button: .left, clickCount: 0, hasPreviousStep: false), .newStep(.click))
        expectEqual(decideStep(button: .left, clickCount: 2, hasPreviousStep: true), .upgradeLastToDoubleClick)
        expectEqual(decideStep(button: .left, clickCount: 2, hasPreviousStep: false), .newStep(.doubleClick))
        expectEqual(decideStep(button: .left, clickCount: 3, hasPreviousStep: true), .ignore)
    }),
    ("testDescribeFull", {
        expectEqual(describeStep(action: .click, appName: "Safari", elementName: "新建标签页"), "在 Safari 中点击「新建标签页」")
        expectEqual(describeStep(action: .doubleClick, appName: "Finder", elementName: "文稿"), "在 Finder 中双击「文稿」")
        expectEqual(describeStep(action: .rightClick, appName: "Finder", elementName: "文稿"), "在 Finder 中右键点击「文稿」")
    }),
    ("testDescribeMissingParts", {
        expectEqual(describeStep(action: .click, appName: "Safari", elementName: nil), "在 Safari 中点击")
        expectEqual(describeStep(action: .click, appName: "Safari", elementName: "  "), "在 Safari 中点击")
        expectEqual(describeStep(action: .click, appName: nil, elementName: "确定"), "点击「确定」")
        expectEqual(describeStep(action: .click, appName: "", elementName: nil), "点击")
        expectEqual(describeStep(action: .click, appName: nil, elementName: nil), "点击")
    }),
    ("testDescribeTruncates", {
        let long = String(repeating: "a", count: 50)
        expectEqual(describeStep(action: .click, appName: "X", elementName: long),
                    "在 X 中点击「" + String(repeating: "a", count: 40) + "…」")
        let forty = String(repeating: "b", count: 40)
        expectEqual(describeStep(action: .click, appName: "X", elementName: forty), "在 X 中点击「\(forty)」")
    }),
]
