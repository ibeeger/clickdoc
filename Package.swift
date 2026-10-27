// swift-tools-version:5.9
import PackageDescription

let package = Package(
    name: "ClickDoc",
    platforms: [.macOS(.v14)],
    targets: [
        .target(name: "RecordCore"),
        .executableTarget(name: "ClickDoc", dependencies: ["RecordCore"]),
        .executableTarget(name: "CoreTests", dependencies: ["RecordCore"]),
    ]
)
