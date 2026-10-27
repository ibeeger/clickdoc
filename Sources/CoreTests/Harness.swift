import Foundation

var failures: [String] = []

func expect(_ cond: Bool, _ msg: String = "", file: StaticString = #file, line: UInt = #line) {
    if !cond { failures.append("\(file):\(line) expect failed \(msg)") }
}

func expectEqual<T: Equatable>(_ a: T, _ b: T, file: StaticString = #file, line: UInt = #line) {
    if a != b { failures.append("\(file):\(line) expected equal:\n    \(a)\n    \(b)") }
}

typealias TestCase = (String, () throws -> Void)
