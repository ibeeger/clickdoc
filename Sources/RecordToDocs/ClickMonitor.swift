import CoreGraphics
import Foundation
import RecordCore

struct RawClick {
    let button: MouseButton
    let clickCount: Int
    let location: CGPoint
    let timestamp: Date
}

/// Listen-only global mouse-down tap. Delivers clicks on the main run loop.
final class ClickMonitor {
    private let handler: (RawClick) -> Void
    private var tap: CFMachPort?
    private var source: CFRunLoopSource?

    init(handler: @escaping (RawClick) -> Void) {
        self.handler = handler
    }

    deinit { stop() }

    func start() -> Bool {
        guard tap == nil else { return true }
        let mask = (1 << CGEventType.leftMouseDown.rawValue) | (1 << CGEventType.rightMouseDown.rawValue)
        let refcon = Unmanaged.passUnretained(self).toOpaque()
        guard let tap = CGEvent.tapCreate(tap: .cgSessionEventTap, place: .headInsertEventTap,
                                          options: .listenOnly, eventsOfInterest: CGEventMask(mask),
                                          callback: clickTapCallback, userInfo: refcon) else { return false }
        let source = CFMachPortCreateRunLoopSource(kCFAllocatorDefault, tap, 0)
        CFRunLoopAddSource(CFRunLoopGetMain(), source, .commonModes)
        CGEvent.tapEnable(tap: tap, enable: true)
        self.tap = tap
        self.source = source
        return true
    }

    func stop() {
        if let tap { CGEvent.tapEnable(tap: tap, enable: false) }
        if let source { CFRunLoopRemoveSource(CFRunLoopGetMain(), source, .commonModes) }
        tap = nil
        source = nil
    }

    fileprivate func handle(type: CGEventType, event: CGEvent) {
        switch type {
        case .tapDisabledByTimeout, .tapDisabledByUserInput:
            if let tap { CGEvent.tapEnable(tap: tap, enable: true) }
        case .leftMouseDown, .rightMouseDown:
            handler(RawClick(button: type == .rightMouseDown ? .right : .left,
                             clickCount: Int(event.getIntegerValueField(.mouseEventClickState)),
                             location: event.location,
                             timestamp: Date()))
        default:
            break
        }
    }
}

private func clickTapCallback(proxy: CGEventTapProxy, type: CGEventType, event: CGEvent,
                              refcon: UnsafeMutableRawPointer?) -> Unmanaged<CGEvent>? {
    if let refcon {
        Unmanaged<ClickMonitor>.fromOpaque(refcon).takeUnretainedValue().handle(type: type, event: event)
    }
    return Unmanaged.passUnretained(event)
}
