# record_to_docs Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 一个 macOS 菜单栏应用：录制鼠标点击，每次点击截图并标出点击位置，录完可编辑步骤，导出为 Markdown + PNG。

**Architecture:** Swift Package 分三个 target。`RecordCore` 是只依赖 Foundation/CoreGraphics/ImageIO 的纯逻辑库（模型、描述文字、点击归并、坐标换算、高亮绘制、导出），全部可测。`RecordToDocs` 是 SwiftUI 应用（菜单栏、权限、事件监听、截图、编辑窗口）。`CoreTests` 是自制的测试可执行文件，因为本机无 XCTest。

**Tech Stack:** Swift 5.9.2（Command Line Tools，无 Xcode），SwiftUI，ScreenCaptureKit（`SCScreenshotManager`），CGEventTap，Accessibility API（AXUIElement），ImageIO。

**Spec:** `docs/superpowers/specs/2026-10-27-record-to-docs-design.md`

## Global Constraints

- 最低系统版本 macOS 14.0；`Package.swift` 中 `platforms: [.macOS(.v14)]`，`swift-tools-version:5.9`。
- 只能用 Command Line Tools 构建：不得依赖 Xcode、XCTest、Swift Testing、`.xcodeproj`。
- 不引入任何第三方依赖。
- 导出结构：`<所选目录>/<教程文件夹>/tutorial.md` 与 `<教程文件夹>/images/step-NN.png`，按最终顺序从 01 编号，图片用相对路径。
- Markdown 格式固定为：`# <标题>`，每步 `## 步骤 N`、描述段落、`![步骤 N](images/step-NN.png)`，各块之间空一行。
- 描述文案固定：单击 `在 <应用> 中点击「<控件>」`，双击 `在 <应用> 中双击「<控件>」`，右键 `在 <应用> 中右键点击「<控件>」`；无控件名时去掉 `「…」`；无应用名时去掉 `在 <应用> 中`。
- 高亮：半透明红色实心圆（红色 alpha 0.3）+ 红色描边圆环（alpha 1.0，线宽 4pt），直径 60pt，按显示器缩放系数换算为像素。
- 点击本应用自身窗口、菜单栏图标、菜单不计入步骤。
- 截图在点击后立即单次截取，只截点击所在显示器，截图中排除本应用窗口。
- 再次开始录制时追加到当前教程；"新建教程"在有未导出步骤时先确认再清空。
- 数据只在内存中，不做持久化。
- 所有界面文案用简体中文。

## Review Focus

1. **副屏或位于主屏左侧/上方的显示器（全局坐标为负）上点击**：高亮必须落在点击处。→ Task 4 `testHighlightRectOnNegativeOriginDisplay`。
2. **标题含 `/`、`:` 或为空/全空白**：导出文件夹名必须合法，空标题回退为 `未命名教程`。→ Task 3 `testSanitizeFolderName`。
3. **导出目标已存在同名文件夹**：不得覆盖，改用 `<名> 2`、`<名> 3`。→ Task 3 `testUniqueFolderURL`。
4. **步骤描述被清空**：该步仍输出标题和图片，不输出空段落。→ Task 3 `testMarkdownSkipsEmptyDescription`。
5. **快速连续点击，截图尚未完成就来了下一次点击或双击**：步骤顺序必须与点击顺序一致，双击合并到正确的那一步。→ Task 6 通过串行处理队列保证，手动验证步骤覆盖。

---

## File Structure

```
Package.swift
Sources/RecordCore/
  Tutorial.swift          Step、Tutorial 模型及编辑操作
  ClickModels.swift       MouseButton、ClickAction、StepDecision、decideStep
  DescriptionBuilder.swift 描述文案
  Exporter.swift          文件名、Markdown 生成、文件夹命名、写盘
  Geometry.swift          显示器选择、高亮矩形换算
  HighlightRenderer.swift 在 CGImage 上画高亮并编码 PNG
Sources/RecordToDocs/
  RecordToDocsApp.swift   @main，MenuBarExtra + 编辑窗口
  AppState.swift          录制状态、点击处理队列、教程数据
  Permissions.swift       辅助功能/屏幕录制权限检查与跳转
  ClickMonitor.swift      CGEventTap 监听
  ElementInspector.swift  AX 获取应用名、控件名、所属进程
  ScreenCapturer.swift    ScreenCaptureKit 截图
  EditorView.swift        编辑窗口 UI
  PermissionView.swift    权限提示 UI
Sources/CoreTests/
  main.swift              测试运行器
  Harness.swift           expect / expectEqual
  TutorialTests.swift, DescriptionTests.swift, ExporterTests.swift, GeometryTests.swift, RendererTests.swift
scripts/build-app.sh      组装并签名 .app
```

测试约定：每个测试文件导出 `let xxxTests: [(String, () throws -> Void)]`，`main.swift` 汇总运行。全部通过时最后一行打印 `ALL PASSED (<n> tests)` 并退出码 0，否则打印失败项并 `exit(1)`。运行命令统一为 `swift run CoreTests`。

---

### Task 1: 工程骨架、测试运行器与 Tutorial 模型

**Files:**
- Create: `Package.swift`, `.gitignore`（`.build/`、`build/`）, `Sources/RecordCore/Tutorial.swift`, `Sources/CoreTests/main.swift`, `Sources/CoreTests/Harness.swift`, `Sources/CoreTests/TutorialTests.swift`
- 为让 package 能解析，先建占位 `Sources/RecordToDocs/RecordToDocsApp.swift`（`@main struct RecordToDocsApp: App` 加一个空 `MenuBarExtra("record_to_docs", systemImage: "record.circle") { Text("") }`），Task 5 替换。

**Interfaces:**
- Produces:
  - `public struct Step: Identifiable, Equatable { public let id: UUID; public var description: String; public var pngData: Data; public let capturedAt: Date; public init(id: UUID = UUID(), description: String, pngData: Data, capturedAt: Date = Date()) }`
  - `public struct Tutorial: Equatable { public var title: String; public private(set) var steps: [Step]; public init(title: String = "未命名教程", steps: [Step] = []) }`
  - 方法：`append(_ step: Step)`、`remove(id: UUID)`、`move(fromOffsets: IndexSet, toOffset: Int)`（语义同 SwiftUI `Array.move`，需自行实现）、`updateDescription(id: UUID, to: String)`、`updateLastStep(_ transform: (inout Step) -> Void)`、`var isEmpty: Bool`
  - 测试工具：`func expect(_ cond: Bool, _ msg: String = "", file: StaticString = #file, line: UInt = #line)`、`func expectEqual<T: Equatable>(_ a: T, _ b: T, file:line:)`，失败时记录 `文件:行` 与两个值。

- [ ] **Step 1: 初始化仓库**：`git init`，建 `.gitignore`。
- [ ] **Step 2: 写 Package.swift**：三个 target：`RecordCore`（library 内部 target）、`RecordToDocs`（executableTarget，依赖 RecordCore）、`CoreTests`（executableTarget，依赖 RecordCore）。
- [ ] **Step 3: 写测试工具与失败测试** `TutorialTests.swift`：
  - `testAppendAndRemove`：append 3 步后 `steps.count == 3`；remove 第 2 步的 id 后剩余 id 顺序为 [第1, 第3]。
  - `testMoveDownAndUp`：步骤 [A,B,C]，`move(fromOffsets: [0], toOffset: 3)` 得 [B,C,A]；再 `move(fromOffsets: [2], toOffset: 0)` 得 [A,B,C]。
  - `testUpdateDescription`：修改 B 的描述后仅 B 改变。
  - `testUpdateLastStepOnEmptyIsNoop`：空教程调用 `updateLastStep` 不崩溃，仍为空。
- [ ] **Step 4: 运行** `swift run CoreTests`，预期编译失败（`Tutorial` 未定义）。
- [ ] **Step 5: 实现** `Tutorial.swift`。
- [ ] **Step 6: 运行** `swift run CoreTests`，预期 `ALL PASSED (4 tests)`；`swift build` 也通过。
- [ ] **Step 7: 提交** `git add -A && git commit -m "feat: package skeleton, test runner, tutorial model"`

### Task 2: 点击归并与描述文案

**Files:**
- Create: `Sources/RecordCore/ClickModels.swift`, `Sources/RecordCore/DescriptionBuilder.swift`, `Sources/CoreTests/DescriptionTests.swift`

**Interfaces:**
- Produces:
  - `public enum MouseButton { case left, right }`
  - `public enum ClickAction: Equatable { case click, doubleClick, rightClick }`
  - `public enum StepDecision: Equatable { case newStep(ClickAction), upgradeLastToDoubleClick, ignore }`
  - `public func decideStep(button: MouseButton, clickCount: Int, hasPreviousStep: Bool) -> StepDecision`
    规则：右键（任意 clickCount）→ `.newStep(.rightClick)`；左键 clickCount ≤ 1 → `.newStep(.click)`；左键 clickCount == 2 且有上一步 → `.upgradeLastToDoubleClick`；左键 clickCount == 2 且无上一步 → `.newStep(.doubleClick)`；左键 clickCount ≥ 3 → `.ignore`。clickCount 取自系统（CGEvent 的 `mouseEventClickState`），本函数不计时。
  - `public func describeStep(action: ClickAction, appName: String?, elementName: String?) -> String`
    nil、空串、纯空白视为缺失；elementName 先去首尾空白，超过 40 个字符截断为前 40 个加 `…`。

- [ ] **Step 1: 写失败测试** `DescriptionTests.swift`：
  - `testDecideStep`：逐条断言上面 5 条规则，另加 `(.right, 2, true) → .newStep(.rightClick)`。
  - `testDescribeFull`：`(.click, "Safari", "新建标签页")` → `在 Safari 中点击「新建标签页」`；`(.doubleClick, "Finder", "文稿")` → `在 Finder 中双击「文稿」`；`(.rightClick, "Finder", "文稿")` → `在 Finder 中右键点击「文稿」`。
  - `testDescribeMissingParts`：`(.click, "Safari", nil)` → `在 Safari 中点击`；`(.click, "Safari", "  ")` → `在 Safari 中点击`；`(.click, nil, "确定")` → `点击「确定」`；`(.click, nil, nil)` → `点击`。
  - `testDescribeTruncates`：elementName 为 50 个 `a` → `在 X 中点击「` + 40 个 `a` + `…」`。
- [ ] **Step 2: 运行** `swift run CoreTests`，预期编译失败。
- [ ] **Step 3: 实现** 两个文件。
- [ ] **Step 4: 运行** `swift run CoreTests`，预期 `ALL PASSED`。
- [ ] **Step 5: 提交** `git commit -am "feat: click decision and step descriptions"`（新文件先 `git add`）。

### Task 3: Markdown 生成与导出

**Files:**
- Create: `Sources/RecordCore/Exporter.swift`, `Sources/CoreTests/ExporterTests.swift`

**Interfaces:**
- Consumes: `Tutorial`, `Step`（Task 1）
- Produces:
  - `public func imageFileName(index: Int) -> String`：index 从 1 开始，至少两位补零：1 → `step-01.png`，12 → `step-12.png`，100 → `step-100.png`。
  - `public func renderMarkdown(_ tutorial: Tutorial) -> String`
  - `public func sanitizeFolderName(_ title: String) -> String`：`/`、`:`、`\` 及控制字符替换为 `-`；去首尾空白和开头的 `.`；结果为空则返回 `未命名教程`。
  - `public func uniqueFolderURL(in directory: URL, name: String, exists: (URL) -> Bool) -> URL`：`name` 不存在则用它，否则依次试 `name 2`、`name 3`…
  - `public func exportTutorial(_ tutorial: Tutorial, to directory: URL, fileManager: FileManager = .default) throws -> URL`：建文件夹与 `images/`，写每步 `pngData` 和 `tutorial.md`（UTF-8），返回教程文件夹 URL。

- [ ] **Step 1: 写失败测试** `ExporterTests.swift`：
  - `testImageFileName`：1、9、12、100 的结果如上。
  - `testRenderMarkdown`：标题 `演示`，两步描述 `第一步`、`第二步`，结果严格等于：
    ```
    # 演示\n\n## 步骤 1\n\n第一步\n\n![步骤 1](images/step-01.png)\n\n## 步骤 2\n\n第二步\n\n![步骤 2](images/step-02.png)\n
    ```
  - `testMarkdownSkipsEmptyDescription`：描述为 `"  "` 的步骤输出 `## 步骤 1\n\n![步骤 1](images/step-01.png)`，中间没有空段落。
  - `testSanitizeFolderName`：`a/b:c` → `a-b-c`；`"   "` → `未命名教程`；`.hidden` → `hidden`；`我的教程` 不变。
  - `testUniqueFolderURL`：exists 对 `X`、`X 2` 返回 true → 结果末段为 `X 3`。
  - `testExportWritesFiles`：在 `FileManager.default.temporaryDirectory` 下的唯一子目录导出两步（pngData 用任意非空字节），断言 `tutorial.md` 内容等于 `renderMarkdown` 结果，`images/step-01.png`、`images/step-02.png` 的字节与输入一致；再导出一次，返回的文件夹名以 ` 2` 结尾；最后删除临时目录。
- [ ] **Step 2: 运行** `swift run CoreTests`，预期编译失败。
- [ ] **Step 3: 实现** `Exporter.swift`。
- [ ] **Step 4: 运行** `swift run CoreTests`，预期 `ALL PASSED`。
- [ ] **Step 5: 提交** `feat: markdown export`。

### Task 4: 坐标换算与高亮绘制

**Files:**
- Create: `Sources/RecordCore/Geometry.swift`, `Sources/RecordCore/HighlightRenderer.swift`, `Sources/CoreTests/GeometryTests.swift`, `Sources/CoreTests/RendererTests.swift`

**Interfaces:**
- Produces:
  - 坐标约定：所有全局坐标都是 CoreGraphics 全局显示坐标（点，原点在主屏左上角，y 向下），即 `CGEvent.location` 和 `CGDisplayBounds` 使用的坐标系。图片像素坐标原点在左上角。
  - `public func displayIndex(containing point: CGPoint, in displayFrames: [CGRect]) -> Int?`
  - `public func highlightRect(click: CGPoint, displayFrame: CGRect, imagePixelSize: CGSize, diameterPoints: CGFloat = 60) -> CGRect`：缩放系数 = `imagePixelSize.width / displayFrame.width`；返回以 `(click - displayFrame.origin) * scale` 为圆心、边长 `diameterPoints * scale` 的正方形（像素，左上原点）。
  - `public func renderHighlight(on image: CGImage, circleIn rect: CGRect, scale: CGFloat) -> CGImage?`：建同尺寸 RGBA 位图上下文，画原图，再画填充（红 alpha 0.3）和描边（红 alpha 1.0，线宽 `4 * scale`）。CGContext 原点在左下，绘制前把 rect 的 y 翻转为 `height - rect.maxY`。
  - `public func pngData(from image: CGImage) -> Data?`（ImageIO，`public.png`）

- [ ] **Step 1: 写失败测试**：
  - `testDisplayIndex`：两块屏 `[(0,0,1440,900), (-1920,-200,1920,1080)]`，点 `(100,100)` → 0，点 `(-10,0)` → 1，点 `(5000,0)` → nil。
  - `testHighlightRectRetina`：displayFrame `(0,0,1440,900)`，imagePixelSize `2880×1800`，click `(100,50)` → rect `(140,40,120,120)`。
  - `testHighlightRectOnNegativeOriginDisplay`：displayFrame `(-1920,-200,1920,1080)`，imagePixelSize `1920×1080`，click `(-1820,-100)` → rect `(70,70,60,60)`。
  - `testRenderHighlightPixels`（RendererTests）：200×200 纯白图，rect `(50,50,100,100)`，scale 1。用位图读回像素：圆心 `(100,100)` 的 R 明显大于 G 和 B（R > 200，G < 220）；角落 `(5,5)` 仍为纯白；左上原点下 `(100, 52)`（描边上）红色，`(100, 148)` 也红色，确认 y 翻转正确且圆对称。
  - `testPNGRoundTrip`：`pngData` 非空，用 `CGImageSourceCreateWithData` 读回宽高相等。
- [ ] **Step 2: 运行** `swift run CoreTests`，预期编译失败。
- [ ] **Step 3: 实现** 两个文件。
- [ ] **Step 4: 运行** `swift run CoreTests`，预期 `ALL PASSED`。
- [ ] **Step 5: 提交** `feat: click geometry and highlight rendering`。

### Task 5: 应用外壳、权限与打包脚本

**Files:**
- Create: `scripts/build-app.sh`, `Sources/RecordToDocs/Permissions.swift`, `Sources/RecordToDocs/PermissionView.swift`, `Sources/RecordToDocs/AppState.swift`（本任务只含状态骨架）
- Modify: `Sources/RecordToDocs/RecordToDocsApp.swift`（替换占位）

**Interfaces:**
- Consumes: `Tutorial`
- Produces:
  - `@MainActor final class AppState: ObservableObject { @Published var tutorial: Tutorial; @Published private(set) var isRecording: Bool; @Published var lastError: String?; @Published var hasUnexportedChanges: Bool; func startRecording(); func stopRecording(); func newTutorial() }`。本任务中 start/stop 只切换状态并检查权限，Task 6 接入实际录制。
  - `enum Permissions { static var accessibilityGranted: Bool; static var screenRecordingGranted: Bool; static func requestAccessibility(); static func requestScreenRecording(); static func openAccessibilitySettings(); static func openScreenRecordingSettings() }`。分别用 `AXIsProcessTrustedWithOptions`、`CGPreflightScreenCaptureAccess` / `CGRequestScreenCaptureAccess`、`x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility` 和 `?Privacy_ScreenCapture`。
  - 窗口 id：编辑窗口 `"editor"`，权限窗口 `"permissions"`。
  - `build-app.sh` 产物：`build/RecordToDocs.app`。

- [ ] **Step 1: 写 `RecordToDocsApp.swift`**：`MenuBarExtra("record_to_docs", systemImage:)`，录制中图标 `record.circle.fill`，否则 `record.circle`。菜单项：`开始录制` / `停止录制`（按状态切换）、`打开编辑器`、`新建教程`、分隔线、`退出`。另有 `Window("编辑教程", id: "editor")`（内容暂为占位 Text，Task 7 替换）和 `Window("需要权限", id: "permissions") { PermissionView() }`。
- [ ] **Step 2: 写 `Permissions.swift` 与 `PermissionView.swift`**：两行，每行显示权限名、状态（`已授权` / `未授权`）、`去设置` 按钮；底部提示 `授权后可能需要重启本应用`。窗口出现时每 1 秒刷新状态。
- [ ] **Step 3: 写 `AppState.swift` 骨架**：`startRecording()` 在任一权限缺失时设置 `lastError`、打开权限窗口并返回。打开窗口的方式由实现者选择，例如把 `openWindow` 动作注入 AppState。`newTutorial()` 在 `hasUnexportedChanges` 时用 `NSAlert` 确认，文案为 `当前教程尚未导出，新建将丢失这些步骤。确定继续？`。
- [ ] **Step 4: 写 `scripts/build-app.sh`**（`set -euo pipefail`）：
  - 运行 `swift build -c release --product RecordToDocs`。
  - 组装 `build/RecordToDocs.app/Contents/{MacOS,Resources}`，复制二进制。
  - 写 Info.plist：`CFBundleIdentifier=com.local.recordtodocs`、`CFBundleName=RecordToDocs`、`CFBundleExecutable=RecordToDocs`、`CFBundlePackageType=APPL`、`CFBundleShortVersionString=0.1.0`、`CFBundleVersion=1`、`LSMinimumSystemVersion=14.0`、`LSUIElement=true`、`NSHighResolutionCapable=true`。
  - 签名：`codesign --force --deep --sign "${SIGN_IDENTITY:--}" --identifier com.local.recordtodocs build/RecordToDocs.app`。
  - 脚本末尾打印产物路径；若 `SIGN_IDENTITY` 为空，提示可用自签名证书避免每次重新授权。
- [ ] **Step 5: 验证构建**：`./scripts/build-app.sh` 成功；`codesign -dv build/RecordToDocs.app` 显示 identifier 为 `com.local.recordtodocs`；`swift run CoreTests` 仍 `ALL PASSED`。
- [ ] **Step 6: 手动验证**：`open build/RecordToDocs.app`，确认菜单栏出现图标且 Dock 中无图标；菜单项齐全；未授权时点 `开始录制` 会弹出权限窗口。
- [ ] **Step 7: 提交** `feat: menu bar app shell, permissions, build script`。

### Task 6: 录制管线（监听、识别、截图、入队）

**Files:**
- Create: `Sources/RecordToDocs/ClickMonitor.swift`, `Sources/RecordToDocs/ElementInspector.swift`, `Sources/RecordToDocs/ScreenCapturer.swift`
- Modify: `Sources/RecordToDocs/AppState.swift`

**Interfaces:**
- Consumes: `decideStep`、`describeStep`、`displayIndex`、`highlightRect`、`renderHighlight`、`pngData`、`Tutorial.append/updateLastStep`
- Produces:
  - `struct RawClick { let button: MouseButton; let clickCount: Int; let location: CGPoint; let timestamp: Date }`
  - `final class ClickMonitor { init(handler: @escaping (RawClick) -> Void); func start() -> Bool; func stop() }`：`CGEvent.tapCreate(tap: .cgSessionEventTap, place: .headInsertEventTap, options: .listenOnly, eventsOfInterest:` 左键按下和右键按下的掩码 `)`，加入主 run loop。回调中若收到 `.tapDisabledByTimeout` 或 `.tapDisabledByUserInput`，重新 enable。`clickCount` 取 `event.getIntegerValueField(.mouseEventClickState)`。
  - `struct ElementInfo { let pid: pid_t?; let appName: String?; let elementName: String? }`
  - `enum ElementInspector { static func inspect(at point: CGPoint) -> ElementInfo }`：`AXUIElementCreateSystemWide` + `AXUIElementCopyElementAtPosition`。控件名依次取 `kAXTitleAttribute`、`kAXDescriptionAttribute`、字符串型的 `kAXValueAttribute`、`kAXHelpAttribute`、`kAXRoleDescriptionAttribute` 中第一个非空值。用 `AXUIElementGetPid` 取 pid，应用名用 `NSRunningApplication(processIdentifier:)?.localizedName`。
  - `actor ScreenCapturer { func capture(displayContaining point: CGPoint) async throws -> (image: CGImage, displayFrame: CGRect) }`：缓存 `SCShareableContent`，只在开始录制和捕获失败时刷新。用 `CGGetDisplaysWithPoint` 得到 displayID，匹配 `SCDisplay.displayID`。`SCContentFilter` 排除本应用（`SCRunningApplication.processID == getpid()`）。配置宽高为该显示器的像素尺寸：`CGDisplayMode.pixelWidth/pixelHeight`。`displayFrame = CGDisplayBounds(displayID)`。

- [ ] **Step 1: 实现 ClickMonitor、ElementInspector、ScreenCapturer**。
- [ ] **Step 2: AppState 接入录制**：
  - 收到 RawClick 后，先同步调用 `ElementInspector.inspect`，赶在界面变化前取信息。
  - 自身过滤：`info.pid == getpid()`，或 `CGWindowListCopyWindowInfo(.optionOnScreenOnly)` 中包含该点的最上层窗口属于本进程时，直接丢弃，不进入后续步骤。
  - 用 `decideStep(hasPreviousStep: !tutorial.isEmpty)` 决策。`.ignore` 丢弃。`.upgradeLastToDoubleClick` 把最后一步的描述重新生成为双击文案。为此 AppState 为每一步保存其 `appName`/`elementName`，例如用 `[UUID: ElementInfo]` 字典；这个字典不进入 RecordCore 模型。
  - `.newStep` 交给截图，再执行 `highlightRect` → `renderHighlight` → `pngData` → `tutorial.append`。
  - **串行**：所有点击按到达顺序进入同一个处理链。例如每次点击都把新 Task 接在前一个 Task 的 `await` 之后，保证追加和双击升级的顺序与点击顺序一致。
  - 截图或渲染失败时跳过这一步，并设置 `lastError`，不崩溃。
  - `stopRecording()` 停止监听，然后打开编辑窗口并激活本应用。
  - 有新步骤时设 `hasUnexportedChanges = true`。
- [ ] **Step 3: 验证** `swift build` 和 `swift run CoreTests` 通过，`./scripts/build-app.sh` 成功。
- [ ] **Step 4: 手动验证**，重新授权后逐项检查：
  1. 开始录制，在 Finder 里点 3 个不同位置后停止。得到 3 步，每张图的红圈都在点击处，描述包含 `Finder`。
  2. 双击 Finder 里的一个文件夹。只多出一步，描述为 `双击`。
  3. 右键点击桌面。得到一步，描述含 `右键点击`。
  4. 录制中点击菜单栏图标和菜单项。这些点击不产生步骤。
  5. 一秒内快速点击 5 个不同位置。得到 5 步，顺序与点击顺序一致。
  6. 若有副屏，在副屏上点击。截图是副屏画面，红圈位置正确。
  7. 录制过程中操作无明显卡顿。
- [ ] **Step 5: 提交** `feat: recording pipeline`。

### Task 7: 编辑窗口与导出

**Files:**
- Create: `Sources/RecordToDocs/EditorView.swift`
- Modify: `Sources/RecordToDocs/RecordToDocsApp.swift`（编辑窗口内容改为 `EditorView`）、`Sources/RecordToDocs/AppState.swift`（加导出方法）

**Interfaces:**
- Consumes: `Tutorial` 的编辑方法、`exportTutorial`
- Produces: `AppState.export()`：用 `NSOpenPanel` 选目录（`canChooseDirectories = true`、`canChooseFiles = false`、`canCreateDirectories = true`、prompt 为 `导出到此处`），再调用 `exportTutorial`。成功后清除 `hasUnexportedChanges`，并用 `NSWorkspace.shared.activateFileViewerSelecting` 在 Finder 中显示文件夹。失败时用 `NSAlert` 显示错误。

- [ ] **Step 1: 实现 `EditorView`**：
  - 使用 `NavigationSplitView`。
  - 左侧 `List(selection:)`：每行是缩略图（高 48）、`步骤 N` 和描述的第一行。支持 `.onMove`、`.onDelete`，右键菜单有 `删除`，并支持 Delete 键删除选中项。
  - 右侧：选中步骤的大图（`Image(nsImage:)`，等比缩放）和下方的 `TextEditor` 描述，编辑直接写回 `updateDescription`。
  - 未选中时显示 `选择一个步骤`。教程为空时显示 `还没有步骤。从菜单栏开始录制。`
  - 工具栏：标题 `TextField`（占位 `教程标题`）、`开始录制`/`停止录制` 按钮、`导出` 按钮。教程为空时导出按钮禁用。
  - 由 PNG 数据生成的 `NSImage` 需按步骤 id 缓存，避免列表滚动时反复解码。
- [ ] **Step 2: 验证** `./scripts/build-app.sh` 成功，`swift run CoreTests` 显示 `ALL PASSED`。
- [ ] **Step 3: 手动验证**：
  1. 录 4 步。删掉第 2 步，把最后一步拖到最前，改一条描述和标题后导出。Finder 打开的文件夹里 `tutorial.md` 的顺序、文字、编号都与界面一致，`images/` 有 3 张图。
  2. 用 VS Code 预览打开 `tutorial.md`，图片正常显示。
  3. 用同一标题再次导出到同一目录。生成 `<标题> 2`，原文件夹不变。
  4. 编辑窗口打开时再次开始录制并点击 2 处。新步骤追加到末尾，点击编辑窗口本身不计入步骤。
  5. 有未导出步骤时点 `新建教程`。出现确认框，取消后数据保留，确定后清空。
- [ ] **Step 4: 提交** `feat: editor window and export`。
