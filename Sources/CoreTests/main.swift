import Foundation

let allTests: [TestCase] = tutorialTests + descriptionTests + exporterTests

var failedTests = 0
for (name, body) in allTests {
    let before = failures.count
    do { try body() } catch { failures.append("\(name) threw \(error)") }
    if failures.count > before {
        failedTests += 1
        print("FAIL \(name)")
        for f in failures[before...] { print("  " + f) }
    } else {
        print("ok   \(name)")
    }
}
if failedTests > 0 {
    print("FAILED \(failedTests) of \(allTests.count) tests")
    exit(1)
}
print("ALL PASSED (\(allTests.count) tests)")
