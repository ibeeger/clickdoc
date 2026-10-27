import AppKit
import ApplicationServices

struct ElementInfo {
    let pid: pid_t?
    let appName: String?
    let elementName: String?
}

enum ElementInspector {
    private static let systemWide: AXUIElement = {
        let e = AXUIElementCreateSystemWide()
        AXUIElementSetMessagingTimeout(e, 0.25)  // never stall on a busy app
        return e
    }()

    /// `point` is in global display coordinates (top-left origin), same as CGEvent.location.
    static func inspect(at point: CGPoint) -> ElementInfo {
        var element: AXUIElement?
        guard AXUIElementCopyElementAtPosition(systemWide, Float(point.x), Float(point.y), &element) == .success,
              let element else {
            return ElementInfo(pid: nil, appName: nil, elementName: nil)
        }
        var pid: pid_t = 0
        let hasPid = AXUIElementGetPid(element, &pid) == .success
        let app = hasPid ? NSRunningApplication(processIdentifier: pid)?.localizedName : nil
        return ElementInfo(pid: hasPid ? pid : nil, appName: app, elementName: name(of: element))
    }

    private static let nameAttributes = [kAXTitleAttribute, kAXDescriptionAttribute, kAXValueAttribute, kAXHelpAttribute]

    private static func name(of element: AXUIElement) -> String? {
        for attr in nameAttributes {
            if let s = string(element, attr) { return s }
        }
        // Labels often sit on the enclosing control (e.g. text inside a button).
        if let parent = parentOf(element) {
            for attr in [kAXTitleAttribute, kAXDescriptionAttribute] {
                if let s = string(parent, attr) { return s }
            }
        }
        return string(element, kAXRoleDescriptionAttribute)
    }

    private static func parentOf(_ element: AXUIElement) -> AXUIElement? {
        var value: CFTypeRef?
        guard AXUIElementCopyAttributeValue(element, kAXParentAttribute as CFString, &value) == .success,
              let value, CFGetTypeID(value) == AXUIElementGetTypeID() else { return nil }
        return (value as! AXUIElement)
    }

    private static func string(_ element: AXUIElement, _ attr: String) -> String? {
        var value: CFTypeRef?
        guard AXUIElementCopyAttributeValue(element, attr as CFString, &value) == .success,
              let s = value as? String else { return nil }
        let t = s.trimmingCharacters(in: .whitespacesAndNewlines)
        return t.isEmpty ? nil : t
    }
}
