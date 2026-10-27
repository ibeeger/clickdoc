import Foundation

/// Language of generated content (step descriptions, Markdown/PDF text). Follows the app's
/// UI language: Chinese when macOS picked the Chinese localization, English otherwise.
public enum ContentLanguage: Equatable {
    case chinese, english

    /// Set by tests; nil in the app.
    public static var override: ContentLanguage?

    public static var current: ContentLanguage {
        override ?? resolve(preferredLocalization: Bundle.main.preferredLocalizations.first)
    }

    public static func resolve(preferredLocalization: String?) -> ContentLanguage {
        preferredLocalization?.lowercased().hasPrefix("zh") == true ? .chinese : .english
    }

    public var untitledTitle: String { self == .chinese ? "未命名教程" : "Untitled Tutorial" }

    public func stepHeading(_ n: Int) -> String { self == .chinese ? "步骤 \(n)" : "Step \(n)" }

    public var imageProcessingFailed: String { self == .chinese ? "处理截图失败" : "Could not process the screenshot" }

    func describe(action: ClickAction, app: String?, element: String?) -> String {
        switch self {
        case .chinese:
            let verb: String
            switch action {
            case .click: verb = "点击"
            case .doubleClick: verb = "双击"
            case .rightClick: verb = "右键点击"
            }
            return (app.map { "在 \($0) 中" } ?? "") + verb + (element.map { "「\($0)」" } ?? "")
        case .english:
            let verb: String
            switch action {
            case .click: verb = "Click"
            case .doubleClick: verb = "Double-click"
            case .rightClick: verb = "Right-click"
            }
            return verb + (element.map { " \"\($0)\"" } ?? "") + (app.map { " in \($0)" } ?? "")
        }
    }
}
