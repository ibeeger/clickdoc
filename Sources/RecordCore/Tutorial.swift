import CoreGraphics
import Foundation

public struct Step: Identifiable, Equatable {
    public let id: UUID
    public var description: String
    public var pngData: Data
    public let capturedAt: Date
    /// Areas to blur, normalized to the image (0...1, top-left origin). Applied on display and export;
    /// `pngData` always keeps the unblurred screenshot so regions stay editable.
    public var blurRegions: [CGRect] = []
    /// Boxes, arrows and text drawn above the blur, also kept separate from `pngData`.
    public var annotations: [Annotation] = []

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

    public init(title: String = ContentLanguage.current.untitledTitle, steps: [Step] = []) {
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

    public mutating func remove(ids: Set<UUID>) {
        steps.removeAll { ids.contains($0.id) }
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

    public mutating func setBlurRegions(id: UUID, _ regions: [CGRect]) {
        guard let i = steps.firstIndex(where: { $0.id == id }) else { return }
        steps[i].blurRegions = regions
    }

    public mutating func setAnnotations(id: UUID, _ annotations: [Annotation]) {
        guard let i = steps.firstIndex(where: { $0.id == id }) else { return }
        steps[i].annotations = annotations
    }

    public mutating func updateLastStep(_ transform: (inout Step) -> Void) {
        guard !steps.isEmpty else { return }
        transform(&steps[steps.count - 1])
    }
}

/// Steps a "delete" on `clicked` applies to: the whole selection if the row is part of it.
public func deletionTargets(clicked: UUID, selection: Set<UUID>) -> Set<UUID> {
    selection.contains(clicked) ? selection : [clicked]
}

/// The step to select after deleting `ids`: the first survivor after the first deleted step,
/// otherwise the last survivor before it.
public func selectionAfterDeleting(_ ids: Set<UUID>, from steps: [Step]) -> UUID? {
    guard let first = steps.firstIndex(where: { ids.contains($0.id) }) else { return nil }
    if let after = steps[first...].first(where: { !ids.contains($0.id) }) { return after.id }
    return steps[..<first].last(where: { !ids.contains($0.id) })?.id
}
