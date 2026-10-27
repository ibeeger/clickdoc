import Foundation

/// Every user-facing string goes through here so the English table can be checked for gaps.
/// Keys are the Chinese text; Localization/en.lproj/Localizable.strings holds the English.
func L(_ key: String.LocalizationValue) -> String {
    String(localized: key)
}
