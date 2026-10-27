import Foundation

private let maxElementNameLength = 40

private func cleaned(_ s: String?) -> String? {
    guard let t = s?.trimmingCharacters(in: .whitespacesAndNewlines), !t.isEmpty else { return nil }
    return t
}

public func describeStep(action: ClickAction, appName: String?, elementName: String?) -> String {
    let verb: String
    switch action {
    case .click: verb = "点击"
    case .doubleClick: verb = "双击"
    case .rightClick: verb = "右键点击"
    }
    var text = ""
    if let app = cleaned(appName) { text += "在 \(app) 中" }
    text += verb
    if var name = cleaned(elementName) {
        if name.count > maxElementNameLength { name = String(name.prefix(maxElementNameLength)) + "…" }
        text += "「\(name)」"
    }
    return text
}
