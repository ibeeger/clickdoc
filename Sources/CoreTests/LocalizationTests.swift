import Foundation

private let repoRoot = URL(fileURLWithPath: #filePath).deletingLastPathComponent()
    .deletingLastPathComponent().deletingLastPathComponent()

/// Keys looked up at runtime: every `L("…")` literal in the app, with interpolations turned
/// into the format specifiers `String(localized:)` uses (%lld for counts, %@ otherwise).
private func appKeys() throws -> Set<String> {
    let dir = repoRoot.appendingPathComponent("Sources/ClickDoc")
    var keys = Set<String>()
    let literal = try NSRegularExpression(pattern: #"\bL\("((?:[^"\\]|\\.)*)"\)"#)
    let interpolation = try NSRegularExpression(pattern: #"\\\(([^)]*\)?)\)"#)
    for name in try FileManager.default.contentsOfDirectory(atPath: dir.path) where name.hasSuffix(".swift") {
        let source = try String(contentsOf: dir.appendingPathComponent(name), encoding: .utf8)
        for m in literal.matches(in: source, range: NSRange(source.startIndex..., in: source)) {
            var key = String(source[Range(m.range(at: 1), in: source)!])
            for im in interpolation.matches(in: key, range: NSRange(key.startIndex..., in: key)).reversed() {
                let expr = String(key[Range(im.range(at: 1), in: key)!])
                let spec = (expr.contains("count") || expr.contains("index")) ? "%lld" : "%@"
                key.replaceSubrange(Range(im.range, in: key)!, with: spec)
            }
            keys.insert(key)
        }
    }
    return keys
}

private func strings(_ lang: String) throws -> [String: String] {
    let url = repoRoot.appendingPathComponent("Localization/\(lang).lproj/Localizable.strings")
    guard let dict = NSDictionary(contentsOf: url) as? [String: String] else { throw CocoaError(.fileReadCorruptFile) }
    return dict
}

let localizationTests: [TestCase] = [
    ("testEveryUIStringHasEnglish", {
        let keys = try appKeys()
        expect(keys.count >= 50, "found only \(keys.count) L() keys")
        let en = try strings("en")
        let missing = keys.subtracting(en.keys).sorted()
        expect(missing.isEmpty, "missing English for: \(missing)")
        let unused = Set(en.keys).subtracting(keys).sorted()
        expect(unused.isEmpty, "unused English entries: \(unused)")
    }),
    ("testEnglishKeepsPlaceholders", {
        for (key, value) in try strings("en") {
            for spec in ["%lld", "%@"] {
                expectEqual(key.components(separatedBy: spec).count, value.components(separatedBy: spec).count)
            }
        }
    }),
    ("testNoUnwrappedChineseInApp", {
        // Chinese text outside L("…") and comments would never be translated.
        let dir = repoRoot.appendingPathComponent("Sources/ClickDoc")
        let bare = try NSRegularExpression(pattern: #"(?<!L\()"(?:[^"\\]|\\.)*[\x{4e00}-\x{9fa5}](?:[^"\\]|\\.)*""#)
        var offenders: [String] = []
        for name in try FileManager.default.contentsOfDirectory(atPath: dir.path) where name.hasSuffix(".swift") {
            let source = try String(contentsOf: dir.appendingPathComponent(name), encoding: .utf8)
            for (i, line) in source.components(separatedBy: "\n").enumerated()
            where !line.trimmingCharacters(in: .whitespaces).hasPrefix("//") {
                let code = line.components(separatedBy: " // ").first ?? line
                if bare.firstMatch(in: code, range: NSRange(code.startIndex..., in: code)) != nil {
                    offenders.append("\(name):\(i + 1)")
                }
            }
        }
        expect(offenders.isEmpty, "untranslatable Chinese literals at \(offenders)")
    }),
]
