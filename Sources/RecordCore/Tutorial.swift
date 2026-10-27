import Foundation

public struct Step: Identifiable, Equatable {
    public let id: UUID
    public var description: String
    public var pngData: Data
    public let capturedAt: Date

    public init(id: UUID = UUID(), description: String, pngData: Data, capturedAt: Date = Date()) {
        self.id = id
        self.description = description
        self.pngData = pngData
        self.capturedAt = capturedAt
    }
}

public struct Tutorial: Equatable {
    public var title: String
    public private(set) var steps: [Step]

    public init(title: String = "未命名教程", steps: [Step] = []) {
        self.title = title
        self.steps = steps
    }

    public var isEmpty: Bool { steps.isEmpty }

    public mutating func append(_ step: Step) {
        steps.append(step)
    }

    public mutating func remove(id: UUID) {
        steps.removeAll { $0.id == id }
    }

    /// Same semantics as SwiftUI's `Array.move(fromOffsets:toOffset:)`.
    public mutating func move(fromOffsets source: IndexSet, toOffset destination: Int) {
        let moving = source.map { steps[$0] }
        let insertAt = destination - source.filter { $0 < destination }.count
        for index in source.reversed() { steps.remove(at: index) }
        steps.insert(contentsOf: moving, at: insertAt)
    }

    public mutating func updateDescription(id: UUID, to text: String) {
        guard let i = steps.firstIndex(where: { $0.id == id }) else { return }
        steps[i].description = text
    }

    public mutating func updateLastStep(_ transform: (inout Step) -> Void) {
        guard !steps.isEmpty else { return }
        transform(&steps[steps.count - 1])
    }
}
