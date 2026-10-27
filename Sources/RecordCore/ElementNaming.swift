import Foundation

/// The accessibility attributes of one element that matter for naming it.
public struct AXFields: Equatable {
    public var role: String?
    public var title: String?
    public var description: String?
    public var value: String?
    public var help: String?
    public var roleDescription: String?

    public init(role: String?, title: String?, description: String?, value: String?, help: String?, roleDescription: String?) {
        self.role = role
        self.title = title
        self.description = description
        self.value = value
        self.help = help
        self.roleDescription = roleDescription
    }
}

private func nonBlank(_ s: String?) -> String? {
    guard let t = s?.trimmingCharacters(in: .whitespacesAndNewlines), !t.isEmpty else { return nil }
    return t
}

/// Picks a human-readable name for a clicked element.
/// AXValue is only used for static text: on text inputs it holds the user's content, not a label.
public func pickElementName(_ element: AXFields, parent: AXFields?) -> String? {
    let value = element.role == "AXStaticText" ? element.value : nil
    for candidate in [element.title, element.description, value, element.help] {
        if let s = nonBlank(candidate) { return s }
    }
    if let parent {
        for candidate in [parent.title, parent.description] {
            if let s = nonBlank(candidate) { return s }
        }
    }
    return nonBlank(element.roleDescription)
}
