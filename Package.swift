// swift-tools-version:5.9
import PackageDescription

let package = Package(
    name: "RecordToDocs",
    platforms: [.macOS(.v14)],
    targets: [
        .target(name: "RecordCore"),
        .executableTarget(name: "RecordToDocs", dependencies: ["RecordCore"]),
        .executableTarget(name: "CoreTests", dependencies: ["RecordCore"]),
    ]
)
