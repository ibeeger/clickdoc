import AppKit
import ApplicationServices
import RecordCore

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

    private static func name(of element: AXUIElement) -> String? {
        pickElementName(fields(of: element), parent: parentOf(element).map(fields(of:)))
    }

    private static func fields(of e: AXUIElement) -> AXFields {
        AXFields(role: string(e, kAXRoleAttribute), title: string(e, kAXTitleAttribute),
                 description: string(e, kAXDescriptionAttribute), value: string(e, kAXValueAttribute),
                 help: string(e, kAXHelpAttribute), roleDescription: string(e, kAXRoleDescriptionAttribute))
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
