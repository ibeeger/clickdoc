import Foundation

private let maxElementNameLength = 40

private func cleaned(_ s: String?) -> String? {
    guard let t = s?.trimmingCharacters(in: .whitespacesAndNewlines), !t.isEmpty else { return nil }
    return t
}

public func describeStep(action: ClickAction, appName: String?, elementName: String?) -> String {
    var element = cleaned(elementName)
    if let name = element, name.count > maxElementNameLength {
        element = String(name.prefix(maxElementNameLength)) + "…"
    }
    return ContentLanguage.current.describe(action: action, app: cleaned(appName), element: element)
}
