# ClickDoc Windows 版 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在 `windows/` 下做出与 Mac 版 v0.7.0 功能一致的 Windows 托盘应用：录制点击 → 截图标红圈 → 编辑（模糊/方框/箭头/文字）→ 导出 Markdown / Notion zip / PDF，并由 GitHub Actions 打出 Inno Setup 安装包。

**Architecture:** 三个项目。`ClickDoc.Core`（`net10.0`，依赖 SkiaSharp，不依赖 WPF）放全部逻辑和图像处理，在 Mac 上用 `dotnet test` 测试；`ClickDoc`（`net10.0-windows`，WPF）是外壳：托盘、鼠标钩子、GDI 截屏、UI Automation、窗口；Mac 上只能编译，运行验证在 UTM 虚拟机和作者的 Windows 真机上做。Mac 版 Swift 代码（`Sources/RecordCore`、`Sources/CoreTests`）是行为规格，每个 Core 任务都对照它移植。

**Tech Stack:** .NET 10、C#、WPF（Fluent 主题）、SkiaSharp 3.x、xUnit、Win32 P/Invoke（`LibraryImport`）、System.Windows.Automation、Inno Setup 6、GitHub Actions `windows-latest`。

**Spec:** `docs/superpowers/specs/2026-10-28-windows-port-design.md`

## Global Constraints

- 在分支 `windows-port` 上开发；提交作者 `sean <1124832+ibeeger@users.noreply.github.com>`（仓库已配置，不要改）；提交信息末尾加 `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`。
- 不修改 Mac 版文件：`Sources/`、`scripts/`、`Package.swift`、`Localization/`（只读引用）。
- 目标框架：Core 和 Tests 为 `net10.0`；外壳为 `net10.0-windows`，`UseWPF=true`，`EnableWindowsTargeting=true`，`RuntimeIdentifier=win-x64`。
- 最低系统 Windows 10 2004（build 19041）；低于此版本启动时提示并退出。
- 依赖只允许 MIT 兼容且免费的：SkiaSharp、xUnit 及其运行器。其余用 BCL 和 P/Invoke 自己写。
- 进程为 Per-Monitor V2 DPI 感知；外壳内所有屏幕坐标都是物理像素。Mac 规格里的“点”在这里一律是“逻辑像素 × 该显示器缩放比例（DPI/96）”。
- 所有界面文字经 `L("中文", args…)` 取得；中文即键。英文来自 Mac 的 `Localization/en.lproj/Localizable.strings`（直接链接，不复制）加 `windows/ClickDoc/Localization/windows.strings`。格式占位符沿用 Mac 的 `%lld`（整数）和 `%@`（其他）。
- 生成内容（步骤描述、Markdown、PDF、默认标题）的语言规则同 Mac `ContentLanguage`：`CultureInfo.CurrentUICulture.Name` 以 `zh` 开头为中文，否则英文。
- 草稿目录 `%LocalAppData%\ClickDoc\draft`；设置文件 `%LocalAppData%\ClickDoc\settings.json`。
- 版本号只来自仓库根目录 `VERSION`。
- Release 文件名：`ClickDoc-<版本>-Setup.exe` 和固定名 `ClickDoc-Setup.exe`。

## Review Focus

1. **主屏左侧/上方的副屏（虚拟屏幕坐标为负），且缩放比例与主屏不同（例如主屏 100%、副屏 150%）**：红圈和“当前窗口”裁剪都必须落在点击处。→ Task 3 `HighlightRect_NegativeOriginMonitorAt150Percent`、`Crop_ClampsToNegativeOriginMonitor`。
2. **教程标题是 Windows 不允许的文件名**（`CON`、`a?b`、`报告.`、200 个字的长标题）：导出必须成功且名称合法。→ Task 6 `Sanitize_WindowsRules`。
3. **PDF 描述是一长串没有空格的英文/URL，或纯中文长段落**：文字必须在内容区宽度内换行，不能溢出页面。→ Task 7 `WrapLines_BreaksLongWordAndCjk`。
4. **被点的程序卡死，UI Automation 查询不返回**：这一步仍要及时生成（只是没有控件名），后续点击不被拖住。→ Task 2 `RunWithTimeout_ReturnsDefaultWhenSlow`；Task 10 用它包住整个 `ElementInspector.Inspect` 调用。
5. **UWP/商店应用（设置、计算器）里点击**：应用名不能显示成 “Application Frame Host”。→ Task 2 `AppName_UsesWindowTitleForFrameHost`。

---

## File Structure

```
windows/
  ClickDoc.sln
  Directory.Build.props            # 读 ../VERSION 设 Version；Nullable/ImplicitUsings；LangVersion latest
  ClickDoc.Core/
    ClickDoc.Core.csproj
    Clicks.cs              # MouseButton, ClickAction, StepDecision, Steps.Decide, ClickCounter, CanUpgradeLastStep
    Tutorial.cs            # Step, Tutorial, Selection helpers
    ContentLanguage.cs     # 语言判定、描述文案、标题
    Naming.cs              # DescribeStep, UiaFields, PickElementName, AppNameRules, RunWithTimeout
    Geometry.cs            # 归一化、HighlightRect、WindowSnapshot、CaptureCropRect、PixelCropRect、TrailingShellSteps
    Imaging.cs             # Fonts, Png, Thumbnail, RenderHighlight, BlurSigma, ApplyBlur
    Annotations.cs         # Annotation/Mark 类型、AnnotationStyle、ArrowPath、TextSize、RenderAnnotations、ComposeStepImage
    AnnotationEditing.cs   # CanvasItem/Handle/Hit、HitTest、Translated、ResizedRect、TextFrame、ClampTextOrigin
    Export.cs              # Markdown、文件名清理与去重、ExportTutorial、ExportNotionZip、ExportImageData
    Pdf.cs                 # WrapLines、PdfLayout.Paginate、RenderPdf、ExportPdf
    DraftStore.cs
    Strings.cs             # StringsFile.Parse、Localizer.Format
  ClickDoc.Core.Tests/
    ClickDoc.Core.Tests.csproj
    *Tests.cs              # 每个 Core 文件一个测试文件
  ClickDoc/
    ClickDoc.csproj, app.manifest
    App.xaml, App.xaml.cs              # 启动、系统版本检查、单实例、主题
    AppState.cs                        # 教程状态、草稿自动保存、导出、新建
    Settings.cs
    Localization/L.cs, Localization/LExtension.cs, Localization/windows.strings
    Native/Win32.cs                    # 全部 P/Invoke 声明
    Recording/MouseHook.cs, WindowList.cs, ScreenCapture.cs, ElementInspector.cs,
              CaptureExclusion.cs, Recorder.cs
    Tray/TrayIcon.cs
    Editor/EditorWindow.xaml(.cs), StepListView.cs, AnnotationCanvas.cs, ExportMenu.cs
    Assets/ClickDoc.ico, tray-idle.ico, tray-recording.ico
  tools/MakeIcons/                     # net10.0 控制台，用 SkiaSharp 从 icon.png 生成 .ico
  installer/ClickDoc.iss, installer/ChineseSimplified.isl
  scripts/publish.sh                   # Mac 上发布到 windows/out/ClickDoc，供拷进虚拟机
.github/workflows/windows.yml
packaging/Windows 安装说明.txt, packaging/Windows Install Guide.txt
docs/windows-manual-test.md
```

---

### Task 1: 工程骨架、SDK、Mac 上可编译

**Files:**
- Create: `windows/ClickDoc.sln`, `windows/Directory.Build.props`, 三个 `.csproj`, `windows/ClickDoc/App.xaml(.cs)`（最小 Application，`ThemeMode="System"`）, `windows/ClickDoc/app.manifest`, `windows/ClickDoc.Core.Tests/SmokeTests.cs`
- Modify: `.gitignore`（加 `windows/**/bin/`、`windows/**/obj/`、`windows/out/`）

**Interfaces:**
- Produces: `dotnet test windows/ClickDoc.Core.Tests` 与 `dotnet build windows/ClickDoc` 两条命令在 Mac 上可用；之后所有任务以它们为验证命令。

- [ ] **Step 1:** 安装 SDK：`brew install --cask dotnet-sdk`，然后 `dotnet --version` 应输出 `10.` 开头。若 cask 给出的不是 10.x，改用 https://dotnet.microsoft.com 的 macOS arm64 安装包（10.0 最新补丁）。
- [ ] **Step 2:** 建项目。`Directory.Build.props`：`Nullable=enable`、`ImplicitUsings=enable`、`TreatWarningsAsErrors=true`、`Version` 用 `$([System.IO.File]::ReadAllText('$(MSBuildThisFileDirectory)../VERSION').Trim())`。Core 引用 SkiaSharp（最新 3.x 稳定版，锁定具体版本号）。外壳 csproj：`OutputType=WinExe`、`UseWPF=true`、`EnableWindowsTargeting=true`、`RuntimeIdentifier=win-x64`、`ApplicationManifest=app.manifest`、`ApplicationIcon` 先留空、`NoWarn` 加 `WPF0001`（Fluent `ThemeMode` 仍是实验性 API）。`app.manifest` 声明 `<dpiAwareness>PerMonitorV2</dpiAwareness>` 和 Win10 `supportedOS` GUID `{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}`。
- [ ] **Step 3:** 写 `SmokeTests.Core_Loads`：断言 `typeof(ClickDoc.Core.Steps).Assembly` 不为 null（`Steps` 先建成空的 `public static class`）。
- [ ] **Step 4:** 运行 `dotnet test windows/ClickDoc.Core.Tests`，期望 `Passed: 1`；运行 `dotnet build windows/ClickDoc -c Release`，期望 `0 Warning(s) 0 Error(s)`。如果 WPF 在 Mac 上编不过，停下来报告错误，不要绕过。
- [ ] **Step 5:** 写 `windows/scripts/publish.sh`：`dotnet publish windows/ClickDoc -c Release -r win-x64 --self-contained -p:PublishSingleFile=false -o windows/out/ClickDoc`。运行一次，期望 `windows/out/ClickDoc/ClickDoc.exe` 存在。
- [ ] **Step 6:** 提交 `chore(windows): solution skeleton building on macOS`。

---

### Task 2: 点击判定、教程模型、描述与命名

对照 Mac：`ClickModels.swift`、`Tutorial.swift`、`ContentLanguage.swift`、`DescriptionBuilder.swift`、`ElementNaming.swift`、`AppState.canUpgradeLastStep`，以及对应测试 `testDecideStep`、`testAppendAndRemove`、`testMoveDownAndUp`、`testUpdateDescription`、`testUpdateLastStepOnEmptyIsNoop`、`testRemoveManyKeepsOrderOfTheRest`、`testDeletionTargetsFollowSelection`、`testSelectionAfterDeletingPicksNextThenPrevious`、`testLanguageResolution`、`testDescribe*`、`testEnglishDescriptions`、`testElementName*`。

**Files:**
- Create: `windows/ClickDoc.Core/{Clicks,Tutorial,ContentLanguage,Naming}.cs`
- Test: `windows/ClickDoc.Core.Tests/{Clicks,Tutorial,ContentLanguage,Naming}Tests.cs`

**Interfaces:**
- Produces:
  - `enum MouseButton { Left, Right }`、`enum ClickAction { Click, DoubleClick, RightClick }`
  - `enum StepDecisionKind { NewStep, UpgradeLastToDoubleClick, Ignore }`；`readonly record struct StepDecision(StepDecisionKind Kind, ClickAction Action = ClickAction.Click)`
  - `static StepDecision Steps.Decide(MouseButton button, int clickCount, bool hasPreviousStep)`
  - `sealed class ClickCounter(int doubleClickMs, int slopX, int slopY)`，`int Register(MouseButton b, SKPointI p, long timestampMs)`：只有左键累加；与上一次左键间隔 ≤ `doubleClickMs` 且 |dx| ≤ slopX/2、|dy| ≤ slopY/2 时 +1，否则返回 1；右键返回 1 并重置左键计数。
  - `static bool Steps.CanUpgradeLastStep(SKPointI? lastPos, DateTimeOffset? lastAt, SKPointI pos, DateTimeOffset at, int doubleClickMs, float scale)`：距离平方 ≤ (5·scale)²，且间隔 ≤ max(doubleClickMs, 500) + 200 ms。
  - `sealed record Step(Guid Id, string Description, byte[] Png, DateTimeOffset CapturedAt)`，另有 `IReadOnlyList<SKRect> BlurRegions { get; init; } = []`、`IReadOnlyList<Annotation> Annotations { get; init; } = []`（`Annotation` 在 Task 4 定义；本任务先建空的 `sealed record Annotation(Guid Id)` 占位，Task 4 替换）
  - `sealed class Tutorial`：`string Title`（默认 `ContentLanguage.Current.UntitledTitle`）、`IReadOnlyList<Step> Steps`、`bool IsEmpty`、`Append`、`Remove(IReadOnlySet<Guid>)`、`Move(IReadOnlyList<int> sourceIndices, int destination)`（语义同 SwiftUI `move(fromOffsets:toOffset:)`）、`UpdateDescription`、`SetBlurRegions`、`SetAnnotations`、`UpdateLastStep(Func<Step, Step>)`
  - `static IReadOnlySet<Guid> Selection.DeletionTargets(Guid clicked, IReadOnlySet<Guid> selection)`；`static Guid? Selection.AfterDeleting(IReadOnlySet<Guid> ids, IReadOnlyList<Step> steps)`
  - `enum ContentLanguage { Chinese, English }`，扩展/静态：`ContentLanguage.Current`（可由测试设置 `ContentLanguage.Override`）、`Resolve(string? cultureName)`、`UntitledTitle`、`StepHeading(int n)`、`ImageProcessingFailed`
  - `static string Naming.DescribeStep(ClickAction a, string? appName, string? elementName)`（40 字截断加 `…`，文案同 Mac）
  - `sealed record UiaFields(string? ControlType, string? Name, string? HelpText, string? LocalizedControlType)`；`static string? Naming.PickElementName(UiaFields element, UiaFields? parent)`：顺序 `Name` → `HelpText` → 父 `Name` → `LocalizedControlType`，全部 trim 后空串视为无
  - `static string? Naming.AppName(string? fileDescription, string? exePath, string? windowTitle)`：exe 文件名为 `ApplicationFrameHost.exe` 时用 `windowTitle`；否则 `fileDescription`，再否则去掉扩展名的 exe 文件名
  - `static T? Naming.RunWithTimeout<T>(Func<T> work, TimeSpan timeout)`：在独立线程（`new Thread{IsBackground=true}`，不用线程池，避免被卡死的调用占满线程池）运行，超时返回 `default`

- [ ] **Step 1: 写失败测试。** 逐个移植上面列出的 Mac 测试（数值、文案完全照抄），再加：

```csharp
[Fact] public void ClickCounter_CountsLeftWithinTimeAndSlop() {
    var c = new ClickCounter(500, 4, 4);
    Assert.Equal(1, c.Register(MouseButton.Left, new(100,100), 0));
    Assert.Equal(2, c.Register(MouseButton.Left, new(101,101), 300));
    Assert.Equal(3, c.Register(MouseButton.Left, new(101,101), 600));
    Assert.Equal(1, c.Register(MouseButton.Left, new(110,100), 700));  // 超出 slop
    Assert.Equal(1, c.Register(MouseButton.Left, new(110,100), 1300)); // 超时
    Assert.Equal(1, c.Register(MouseButton.Right, new(110,100), 1400));
    Assert.Equal(1, c.Register(MouseButton.Left, new(110,100), 1500)); // 右键打断
}
[Fact] public void CanUpgrade_ScalesDistance() {
    var t = DateTimeOffset.UnixEpoch;
    Assert.True(Steps.CanUpgradeLastStep(new(0,0), t, new(7,0), t.AddMilliseconds(400), 500, 1.5f));
    Assert.False(Steps.CanUpgradeLastStep(new(0,0), t, new(8,0), t.AddMilliseconds(400), 500, 1.5f));
    Assert.False(Steps.CanUpgradeLastStep(new(0,0), t, new(0,0), t.AddMilliseconds(701), 500, 1f));
    Assert.False(Steps.CanUpgradeLastStep(null, null, new(0,0), t, 500, 1f));
}
[Fact] public void AppName_UsesWindowTitleForFrameHost() {
    Assert.Equal("计算器", Naming.AppName("Application Frame Host", @"C:\Windows\System32\ApplicationFrameHost.exe", "计算器"));
    Assert.Equal("Google Chrome", Naming.AppName("Google Chrome", @"C:\x\chrome.exe", "t"));
    Assert.Equal("notepad", Naming.AppName("  ", @"C:\Windows\notepad.exe", "t"));
    Assert.Null(Naming.AppName(null, null, null));
}
[Fact] public void RunWithTimeout_ReturnsDefaultWhenSlow() {
    var sw = Stopwatch.StartNew();
    Assert.Null(Naming.RunWithTimeout<string>(() => { Thread.Sleep(2000); return "x"; }, TimeSpan.FromMilliseconds(250)));
    Assert.True(sw.ElapsedMilliseconds < 600);
    Assert.Equal("ok", Naming.RunWithTimeout(() => "ok", TimeSpan.FromMilliseconds(250)));
}
[Fact] public void PickElementName_PriorityAndParent() {
    Assert.Equal("保存", Naming.PickElementName(new("Button", " 保存 ", "帮助", "按钮"), null));
    Assert.Equal("帮助", Naming.PickElementName(new("Button", "", "帮助", "按钮"), null));
    Assert.Equal("工具栏", Naming.PickElementName(new("Image", null, null, "图像"), new("ToolBar", "工具栏", null, null)));
    Assert.Equal("图像", Naming.PickElementName(new("Image", null, null, "图像"), new("Pane", null, null, null)));
}
```

`ContentLanguage.Resolve`：`"zh-CN"`、`"zh-Hans"`、`"zh-TW"` → Chinese；`"en-US"`、`"ja-JP"`、`""`、`null` → English。

- [ ] **Step 2:** `dotnet test windows/ClickDoc.Core.Tests`，期望编译失败（类型不存在）。
- [ ] **Step 3:** 按 Interfaces 实现四个文件。
- [ ] **Step 4:** `dotnet test windows/ClickDoc.Core.Tests`，期望全部通过。
- [ ] **Step 5:** 提交 `feat(windows): click decisions, tutorial model, descriptions`。

---

### Task 3: 几何、截图裁剪、任务栏尾步裁剪

对照 Mac：`Geometry.swift`、`WindowHitTest.swift`、`Blur.swift` 里的 `normalizedRect/denormalizedRect`，测试 `testHighlightRect*`、`testCrop*`、`testFullScreen*`、`testPixelCropRect`、`testNormalizeAndClampRect`。

**Files:**
- Create: `windows/ClickDoc.Core/Geometry.cs`
- Test: `windows/ClickDoc.Core.Tests/GeometryTests.cs`

**Interfaces:**
- Produces:
  - `static SKRect Geometry.Normalize(SKRect r, SKSize size)` / `Denormalize(SKRect r, SKSize size)`（顶左原点，裁到边界内，空交集返回 `SKRect.Empty`）
  - `static SKRect Geometry.HighlightRect(SKPointI click, SKPointI imageOrigin, float scale, float diameterLogical = 60)`：返回图像像素内的圆外接矩形；`imageOrigin` 是图像左上角在虚拟屏幕中的物理坐标（整屏时是显示器左上角，裁剪后是裁剪框左上角）
  - `sealed record WindowSnapshot(nint Handle, int Pid, SKRectI Bounds, bool IsPopup, bool IsShell)`，列表顺序为由前到后，只含可见、未被 cloak 的顶层窗口
  - `static SKRectI? Geometry.CaptureCropRect(SKPointI click, IReadOnlyList<WindowSnapshot> windows, SKRectI monitor, float scale, nint? hitWindow = null)`：算法同 Mac `captureCropRect`，对应关系：`layer == 0` ↔ `!IsPopup && !IsShell`；`layer > 0` ↔ `IsPopup`；Dock/桌面 ↔ `IsShell`（命中 Shell 窗口直接返回 null）；`margin = 8·scale`、`popupReach = 200·scale`、`highlightReach = 32·scale`；面积 ≥ 显示器 95% 返回 null；结果取整到整数像素
  - `static SKRectI Geometry.ToImagePixels(SKRectI crop, SKRectI monitor)`：返回相对显示器左上角的像素矩形，裁到显示器内（物理像素下就是平移，替代 Mac 的 `pixelCropRect`）
  - `static int Geometry.TrailingShellSteps(IReadOnlyList<(DateTimeOffset At, bool IsShell)> steps, DateTimeOffset stoppedAt, TimeSpan window)`：从末尾往前数，连续满足 `IsShell && At >= stoppedAt - window` 的步数

- [ ] **Step 1: 写失败测试。** 移植上面列出的 Mac 测试（把 Mac 的点坐标乘以测试里的 `scale`，`layer` 按上面的对应关系换算），再加：

```csharp
[Fact] public void HighlightRect_NegativeOriginMonitorAt150Percent() {
    // 主屏左侧的 150% 副屏：虚拟坐标 x ∈ [-2880, 0)
    var r = Geometry.HighlightRect(new(-1440, 300), new(-2880, 0), 1.5f);
    Assert.Equal(new SKRect(1440 - 45, 300 - 45, 1440 + 45, 300 + 45), r);
}
[Fact] public void Crop_ClampsToNegativeOriginMonitor() {
    var monitor = new SKRectI(-2880, -200, 0, 1420);
    var win = new WindowSnapshot(1, 10, new SKRectI(-2900, 100, -2000, 700), false, false);
    var crop = Geometry.CaptureCropRect(new(-2500, 400), [win], monitor, 1.5f);
    Assert.Equal(new SKRectI(-2880, 88, -1988, 712), crop);
}
[Fact] public void Crop_ShellWindowMeansFullScreen() {
    var bar = new WindowSnapshot(2, 4, new SKRectI(0, 1040, 1920, 1080), false, true);
    Assert.Null(Geometry.CaptureCropRect(new(900, 1060), [bar], new SKRectI(0, 0, 1920, 1080), 1f));
}
[Fact] public void TrailingShellSteps_CountsOnlyRecentTail() {
    var t = DateTimeOffset.UnixEpoch.AddSeconds(100);
    var steps = new[] { (t.AddSeconds(-9), true), (t.AddSeconds(-8), false), (t.AddSeconds(-3), true), (t.AddSeconds(-1), true) };
    Assert.Equal(2, Geometry.TrailingShellSteps(steps, t, TimeSpan.FromSeconds(5)));
    Assert.Equal(0, Geometry.TrailingShellSteps(steps[..2], t, TimeSpan.FromSeconds(5)));
}
```

- [ ] **Step 2:** 运行测试，期望失败。
- [ ] **Step 3:** 实现 `Geometry.cs`。
- [ ] **Step 4:** 运行测试，期望全部通过。
- [ ] **Step 5:** 提交 `feat(windows): capture geometry and crop rules`。

---

### Task 4: 图像处理与标注渲染（SkiaSharp）

对照 Mac：`HighlightRenderer.swift`、`Blur.swift`、`Annotation.swift`，测试 `testRenderHighlight*`、`testPNGRoundTrip`、`testThumbnailIsBounded`、`testBlur*`、`testRectangleAnnotationStrokesEdgeOnly`、`testArrowAnnotationDrawsShaftAndHead`、`testTextAnnotationDrawsNearItsOrigin`、`testAnnotationsNoneReturnsImage`。

**Files:**
- Create: `windows/ClickDoc.Core/{Imaging,Annotations}.cs`（替换 Task 2 的 `Annotation` 占位）
- Test: `windows/ClickDoc.Core.Tests/{Imaging,Annotations}Tests.cs`

**Interfaces:**
- Produces:
  - `abstract record Mark`；`sealed record RectangleMark(SKRect Rect) : Mark`；`sealed record ArrowMark(SKPoint From, SKPoint To) : Mark`；`sealed record TextMark(SKPoint Origin, string Text) : Mark`（坐标均为 0…1 归一化、顶左原点；`Origin` 是文字框左上角）
  - `sealed record Annotation(Guid Id, Mark Mark)`
  - `static class AnnotationStyle`：`LineWidth(float imageWidth) = max(1.5, w·0.0028)`、`FontSize(w) = max(10, w·0.022)`、`ArrowHeadLength(lw) = lw·8`、`ArrowHeadHalfWidth(lw) = lw·4.5`、`TextOutlineFraction = 0.18f`（白色描边总宽 = 字号 × 0.18，先描边后填充）、`Red = new SKColor(237, 28, 36)`（sRGB 0.93/0.11/0.14）
  - `static class Fonts`：`SKTypeface Get(bool bold)`，依次尝试 `"Microsoft YaHei UI"`、`"Microsoft YaHei"`、`"PingFang SC"`（返回的 `FamilyName` 必须等于所请求的名字才算命中），都没有则 `SKFontManager.Default.MatchCharacter('中')`，再没有则 `SKTypeface.Default`；结果缓存
  - `static byte[] Imaging.EncodePng(SKBitmap)`、`static SKBitmap Imaging.DecodePng(byte[])`、`static SKBitmap Imaging.Thumbnail(byte[] png, int maxPixelSize)`
  - `static SKBitmap Imaging.RenderHighlight(SKBitmap image, SKRect circle, float scale)`：填充红 alpha 0.3，描边红 alpha 1、线宽 `4·scale`，抗锯齿
  - `static float Imaging.BlurSigma(float imageWidth) = max(w/150, 10)`
  - `static SKBitmap Imaging.ApplyBlur(SKBitmap image, IReadOnlyList<SKRect> regions, float? sigma = null)`：用 `SKImageFilter.CreateBlur(σ, σ, SKShaderTileMode.Clamp)` 对整图模糊一次，再按区域裁剪贴回；无区域时返回原对象
  - `static (SKPoint ShaftEnd, SKPath Head)? Annotations.ArrowPath(SKPoint from, SKPoint tip, float lineWidth)`（长度 ≤ 0.5 返回 null；头长 = min(头长, 长度)）
  - `static SKSize Annotations.TextSize(string text, float styleWidth)`：单行，宽 = 测量宽度，高 = ascent+descent（不含描边）
  - `static SKBitmap Annotations.Render(SKBitmap image, IReadOnlyList<Annotation> marks, float? styleWidth = null)`：`styleWidth` 默认等于图像宽度；尺寸按 `styleWidth` 计算后乘以 `image.Width / styleWidth`，让缩小后的预览图和导出图比例一致；无标注时返回原对象
  - `static SKBitmap Annotations.Compose(SKBitmap image, Step step, float? styleWidth = null)`：先模糊（σ 同样按比例缩放）再画标注

- [ ] **Step 1: 写失败测试。** 移植上面列出的 Mac 像素测试（Mac 的 y 翻转相关测试在 Skia 里都按顶左原点理解）。再加：

```csharp
[Fact] public void Render_PreviewUsesStyleWidthPastTheFloor() {
    var text = new Annotation(Guid.NewGuid(), new TextMark(new SKPoint(0.1f, 0.1f), "测试文字"));
    // 全尺寸 1000 宽：字号 22。250 宽预览：按 styleWidth=1000 应为 5.5（不传则会被下限抬到 10）
    var full = InkBounds(Annotations.Render(Blank(1000, 500), [text]));
    var preview = InkBounds(Annotations.Render(Blank(250, 125), [text], styleWidth: 1000));
    Assert.InRange(preview.Width, full.Width / 4 - 2, full.Width / 4 + 2);
}
[Fact] public void Fonts_CanDrawChinese() => Assert.True(Fonts.Get(true).ContainsGlyph('中'));
```

`Blank(w, h)` 生成白色位图；`InkBounds(bitmap)` 返回所有非白像素的外接矩形。这两个都是测试里的辅助方法。

- [ ] **Step 2:** 运行测试，期望失败。
- [ ] **Step 3:** 实现两个文件。
- [ ] **Step 4:** 运行测试，期望全部通过。
- [ ] **Step 5:** 提交 `feat(windows): highlight, blur and annotation rendering`。

---

### Task 5: 画布编辑逻辑

对照 Mac：`AnnotationEditing.swift` 及其 10 个测试（`testHit*`、`testAnnotationsBeatBlurAndBlurHitsInside`、`testHandlesOnlyForSelection`、`testTranslateClampsInsideImage`、`testResizeKeepsMinimumAndBounds`、`testTextFrameScalesFromImagePixels`、`testClampTextOriginKeepsTextInsideImage`、`testHitTestUsesImageScaledText`、`testDrawingToolsIgnoreBlurBodies`）。

**Files:**
- Create: `windows/ClickDoc.Core/AnnotationEditing.cs`
- Test: `windows/ClickDoc.Core.Tests/AnnotationEditingTests.cs`

**Interfaces:**
- Consumes: Task 4 的 `Annotation`、`Mark` 系列、`AnnotationStyle`、`Annotations.TextSize`
- Produces:
  - `abstract record CanvasItem`；`sealed record BlurItem(int Index) : CanvasItem`；`sealed record AnnotationItem(Guid Id) : CanvasItem`
  - `enum CanvasHandle { Resize, ArrowStart, ArrowEnd }`；`sealed record CanvasHit(CanvasItem Item, CanvasHandle? Handle = null)`
  - `static CanvasHit? Editing.HitTest(SKPoint p, IReadOnlyList<SKRect> blur, IReadOnlyList<Annotation> marks, CanvasItem? selected, SKSize canvas, float? imageWidth = null, bool includeBlurBodies = true, float tolerance = 6, float handleRadius = 9)`
  - `static Mark Editing.Translated(Mark m, SKPoint delta)`、`static SKRect Editing.Resized(SKRect r, SKPoint delta, SKSize minSize)`
  - `static SKRect Editing.TextFrame(SKPoint origin, string text, SKSize canvas, float? imageWidth = null)`、`static SKPoint Editing.ClampTextOrigin(SKPoint origin, string text, SKSize imageSize)`

- [ ] **Step 1:** 移植 10 个 Mac 测试，数值不变。
- [ ] **Step 2:** 运行测试，期望失败。
- [ ] **Step 3:** 实现。
- [ ] **Step 4:** 运行测试，期望全部通过。
- [ ] **Step 5:** 提交 `feat(windows): canvas hit-testing and editing math`。

---

### Task 6: Markdown、Notion zip、文件名

对照 Mac：`Exporter.swift`、`PDFExport.swift` 中的 `uniqueFileURL` 和 `exportNotionZip`，测试 `testImageFileName`、`testRenderMarkdown`、`testMarkdownSkipsEmptyDescription`、`testSanitizeFolderName`、`testUniqueFolderURL`、`testExportWritesFiles`、`testExportBurnsInBlur`、`testExportImageAppliesAnnotations`、`testExportNotionZip`、`testExportNotionZipNameIsUnique`、`testEnglishMarkdownAndDefaults`。

**Files:**
- Create: `windows/ClickDoc.Core/Export.cs`
- Test: `windows/ClickDoc.Core.Tests/ExportTests.cs`

**Interfaces:**
- Consumes: `Tutorial`、`Step`、`Annotations.Compose`、`Imaging.EncodePng/DecodePng`、`ContentLanguage`
- Produces:
  - `static string Export.ImageFileName(int n)` → `step-01.png`
  - `static string Export.RenderMarkdown(Tutorial t)`（格式同 Mac，块之间空一行，末尾换行）
  - `static string Export.SanitizeName(string title)`
  - `static string Export.UniqueFolder(string dir, string name, Func<string,bool> exists)`、`static string Export.UniqueFile(string dir, string name, string ext, Func<string,bool> exists)`（`name 2`、`name 3`…）
  - `static byte[] Export.ImageData(Step s)`（没有模糊和标注时原样返回 `s.Png`）
  - `static string Export.ExportTutorial(Tutorial t, string dir)`（返回新文件夹路径）、`static string Export.ExportNotionZip(Tutorial t, string dir)`（返回 zip 路径，用 `System.IO.Compression.ZipArchive`，条目路径用 `/` 分隔）
  - `sealed class ExportException(string message) : Exception`，失败时以 `ContentLanguage.Current.ImageProcessingFailed` 作为消息抛出

- [ ] **Step 1: 写失败测试。** 移植上面的 Mac 测试，再加：

```csharp
[Theory]
[InlineData("CON", "CON_")] [InlineData("nul", "nul_")] [InlineData("com1", "com1_")] [InlineData("LPT9", "LPT9_")]
[InlineData("a?b*c", "a-b-c")] [InlineData("x<y>z|\"q\"", "x-y-z--q-")]
[InlineData("报告.", "报告")] [InlineData("  报告 . . ", "报告")] [InlineData("a/b:c\\d", "a-b-c-d")]
public void Sanitize_WindowsRules(string input, string expected) => Assert.Equal(expected, Export.SanitizeName(input));
[Fact] public void Sanitize_TruncatesTo100() => Assert.Equal(100, Export.SanitizeName(new string('长', 200)).Length);
[Fact] public void Sanitize_EmptyFallsBack() => Assert.Equal(ContentLanguage.Current.UntitledTitle, Export.SanitizeName(" ... "));
```

规则顺序：替换 `/:\<>"|?*`、控制字符和换行为 `-` → 去掉首尾空格 → 去掉开头的 `.` → 去掉结尾的 `.` 和空格（反复执行直到稳定）→ 截到 100 个字符后再去一次结尾的 `.` 和空格 → 主名（第一个 `.` 之前的部分）不区分大小写等于 `CON PRN AUX NUL COM1-9 LPT1-9` 时在末尾加 `_` → 为空则用默认标题。

- [ ] **Step 2:** 运行测试，期望失败。
- [ ] **Step 3:** 实现。
- [ ] **Step 4:** 运行测试，期望全部通过。
- [ ] **Step 5:** 提交 `feat(windows): markdown and notion zip export`。

---

### Task 7: PDF

对照 Mac：`PDFExport.swift`，测试 `testPDFContainsTitleAndDescriptions`、`testPDFPaginatesTallContent`、`testPDFScalesVeryTallImageToFitPage`、`testPDFAppliesBlur`、`testExportPDFFileNaming`、`testEnglishPDF`。

**Files:**
- Create: `windows/ClickDoc.Core/Pdf.cs`
- Test: `windows/ClickDoc.Core.Tests/PdfTests.cs`

**Interfaces:**
- Consumes: `Export.ImageData`、`Export.SanitizeName`、`Export.UniqueFile`、`Imaging.Thumbnail`、`Fonts`
- Produces:
  - `static IReadOnlyList<string> Pdf.WrapLines(string text, SKFont font, float maxWidth)`：先按 `\n` 分段（空段保留为空行）；拉丁文在空格处断行；CJK 字符之间任意处可断；单个超宽的词按字符硬断
  - `sealed record StepBlock(float TextHeight, SKSize ImagePixels)`；`sealed record PlacedStep(int Page, float Top, SKSize ImageSize)`
  - `static IReadOnlyList<PlacedStep> PdfLayout.Paginate(float titleHeight, IReadOnlyList<StepBlock> steps)`：常量同 Mac `PDFLayout`（A4 595×842，边距 50，图片最大高 270，titleGap 18，stepGap 22；`TextHeight` 中已含 headingGap 6 和 textGap 8）；图片缩放到内容宽 495，高度上限 `min(270, 742 − TextHeight)`；放不下且不在页顶时换页
  - `static byte[] Pdf.Render(Tutorial t)`：`SKDocument.CreatePdf(stream, new SKDocumentPdfMetadata { Title = … })`；标题 24 粗、步骤标题 15 粗、正文 12 灰度 0.25；行高用 `font.Spacing`；每张图先缩到最长边 1600 像素以内，再编码为 JPEG 质量 80，用 `SKImage.FromEncodedData` 画上去，这样嵌入的是 JPEG 数据流
  - `static string Pdf.ExportPdf(Tutorial t, string dir)`

- [ ] **Step 1: 写失败测试。** `Paginate` 用纯数值测试，覆盖 Mac 的三种情况：同页放两步；第三步换页；超高图片缩到能放进一页。`Render` 测试：页数用正则 `/Type\s*/Page\b(?!s)` 计数；输出以 `%PDF` 开头；模糊步骤与未模糊步骤生成的字节不同。另外：

```csharp
[Fact] public void WrapLines_BreaksLongWordAndCjk() {
    var font = new SKFont(Fonts.Get(false), 12);
    foreach (var text in new[] { new string('a', 400), "https://example.com/" + new string('x', 300), new string('中', 300), "hello 世界 " + new string('b', 200) })
        foreach (var line in Pdf.WrapLines(text, font, 495))
            Assert.True(font.MeasureText(line) <= 495.5f, line);
    Assert.Equal(["a", "", "b"], Pdf.WrapLines("a\n\nb", font, 495));
}
```

- [ ] **Step 2:** 运行测试，期望失败。
- [ ] **Step 3:** 实现。
- [ ] **Step 4:** 运行测试，期望全部通过。
- [ ] **Step 5:** 提交 `feat(windows): PDF export`。

---

### Task 8: 草稿存储与界面字符串

对照 Mac：`DraftStore.swift`、`AppState.restoreDraft`，测试 `testDraft*`、`testTutorialSetAnnotationsAndDraftRoundTrip`；`LocalizationTests.swift`。不移植旧名迁移（`migrateLegacyDraft`），Windows 上没有旧版本。

**Files:**
- Create: `windows/ClickDoc.Core/{DraftStore,Strings}.cs`
- Test: `windows/ClickDoc.Core.Tests/{DraftStore,Strings,Localization}Tests.cs`

**Interfaces:**
- Produces:
  - `sealed class DraftStore(string directory)`：`void Save(Tutorial t, bool hasUnexportedChanges)`、`(Tutorial Tutorial, bool HasUnexportedChanges)? Load()`（manifest 损坏时抛 `JsonException`）、`void Clear()`、`string MoveAside(DateTimeOffset now)`（移到同级目录 `draft-unreadable-<unix 秒>`，返回新路径）
  - 文件布局：`manifest.json`（`System.Text.Json`；版本号 1；rect 存成 `[x,y,w,h]`；标注带 `"type": "rect" | "arrow" | "text"` 判别字段）+ `images/<id>.png`。截图只写一次；最后写 manifest，并通过“写临时文件再 `File.Move(…, overwrite: true)`”保证原子替换；删除已不在教程里的图片
  - `static IReadOnlyDictionary<string,string> StringsFile.Parse(string content)`：解析 Mac `.strings` 格式（`"k" = "v";`、`/* */` 注释、`\"` 和 `\n` 转义）
  - `static string Localizer.Format(string template, params object[] args)`：按顺序把 `%lld` 和 `%@` 替换为参数

- [ ] **Step 1: 写失败测试。** 移植 Mac 的草稿测试（round trip、空目录 Load 返回 null、删除步骤后图片被清理、图片缺失时跳过该步、manifest 损坏时抛异常、Clear）。另加 `MoveAside_KeepsFilesAndReturnsPath`。`StringsTests`：解析 Mac 的 `Localization/en.lproj/Localizable.strings`，断言条目数 ≥ 60，且 `["停止录制"] == "Stop Recording"`；`Format("删除 %lld 个步骤？", 3) == "删除 3 个步骤？"`。`LocalizationTests`（Task 9 起才有被扫描的源码，所以先只写测试，并断言扫描到的键数 ≥ 0）：
  - 扫描 `windows/ClickDoc/**/*.cs` 中的 `L("…"` 字面量，以及 `*.xaml` 中的 `{l:L …}`
  - 每个键都必须出现在 Mac 英文表 ∪ `windows.strings` 中
  - 英文译文与中文键的 `%lld`/`%@` 个数相同
  - `.cs` 中不得有 `L(` 以外的带中文的字符串字面量（注释除外）
  - 测试通过 `[CallerFilePath]` 向上找到仓库根目录
- [ ] **Step 2:** 运行测试，期望失败。
- [ ] **Step 3:** 实现。
- [ ] **Step 4:** 运行测试，期望全部通过。
- [ ] **Step 5:** 提交 `feat(windows): draft store and string tables`。

---

### Task 9: 外壳：启动、单实例、托盘、AppState、编辑器空壳

**Files:**
- Create: `windows/ClickDoc/{App.xaml.cs,AppState.cs,Settings.cs}`、`Localization/{L.cs,LExtension.cs,windows.strings}`、`Native/Win32.cs`、`Tray/TrayIcon.cs`、`Editor/EditorWindow.xaml(.cs)`（本任务只放标题框、录制按钮和空状态）、`tools/MakeIcons/`、`ClickDoc/Assets/*.ico`
- Modify: `windows/ClickDoc/ClickDoc.csproj`（`ApplicationIcon`；用 `EmbeddedResource` 链接 `../../Localization/en.lproj/Localizable.strings` 和 `windows.strings`）

**Interfaces:**
- Consumes: Core 全部；`StringsFile`、`Localizer`、`DraftStore`
- Produces:
  - `static string L(string zh, params object[] args)`（`global using static ClickDoc.Localization.Strings;`）
  - XAML 写法 `{l:L 开始录制}`
  - `sealed class AppState : INotifyPropertyChanged`，成员：`Tutorial Tutorial`、`bool IsRecording`、`string? LastError`、`bool HasUnexportedChanges`、`CaptureScope Scope`（`enum CaptureScope { Window, Screen }`，持久化到 settings.json）、`event Action? TutorialChanged`、`void Touch()`（标记有改动并触发防抖保存）、`void ShowEditor()`、`void NewTutorial()`、`void StartRecording()`/`StopRecording()`（本任务先只切换状态，Task 10 接上录制）
  - `sealed class TrayIcon : IDisposable`：`TrayIcon(Action<Point> onClick)`、`void SetIcon(bool recording)`、`void SetTooltip(string)`、`SKRectI? GetIconRect()`（调用 `Shell_NotifyIconGetRect`）
  - `TrayIcon` 自己实现：通过 `Shell_NotifyIcon` + 一个 message-only `HwndSource`，`uID = 1`；处理 `WM_LBUTTONUP`/`WM_RBUTTONUP` 回调和 `TaskbarCreated` 消息（资源管理器重启后重新添加图标）

- [ ] **Step 1:** 写 `tools/MakeIcons`（net10.0 控制台，引用 SkiaSharp）：
  - 输入：仓库根目录的 `icon.png`
  - 处理：居中裁成正方形，加圆角（半径 = 边长 × 0.18）
  - 输出 `ClickDoc.ico`：16/24/32/48/64/128/256 七种尺寸，每种以 PNG 压缩条目写入
  - 输出 `tray-idle.ico` 和 `tray-recording.ico`：16/20/24/32 四种尺寸；`tray-recording.ico` 在右下角叠一个红色实心圆点（直径为边长的 45%，带 1px 白边）
  - 运行方式：`dotnet run --project windows/tools/MakeIcons -- icon.png windows/ClickDoc/Assets`，把生成的三个文件提交
- [ ] **Step 2:** 实现 `App.OnStartup`，按顺序做：
  1. `Environment.OSVersion.Version.Build < 19041` 时，弹 `MessageBox`：`L("ClickDoc 需要 Windows 10 2004 或更高版本。")`，然后退出。
  2. 单实例：创建 `Mutex("Local\\ClickDoc.SingleInstance")`。如果已被别的实例持有，就 `EventWaitHandle.OpenExisting("Local\\ClickDoc.ShowEditor").Set()` 后退出；否则创建这个事件，并开一个后台线程等待它，收到信号后通过 `Dispatcher` 调 `ShowEditor()`。
  3. `ShutdownMode = OnExplicitShutdown`。
  4. 创建 `AppState`，从草稿恢复。读取失败时：`MoveAside`，然后设置 `LastError = L("上次的草稿无法读取，已另存到 %@", path)`。
  5. 创建托盘图标，打开编辑器。
- [ ] **Step 3:** 草稿保存：
  - 每次 `Touch()` 后防抖 400ms，在单独的保存线程上串行写入。
  - 保存失败时设置 `LastError = L("自动保存草稿失败：%@", ex.Message)`。
  - `Application.Exit` 和 `SessionEnding` 时同步保存一次。
- [ ] **Step 4:** 托盘菜单是一个 WPF `ContextMenu`，项目和顺序同 spec §5.3。
  - 弹出位置：鼠标位置。弹出前先对托盘消息窗口调用 `SetForegroundWindow`，否则菜单点到外面时不会自动关闭。
  - “截图范围”是带两个可勾选子项的子菜单。
  - 退出：先 `StopRecording()`，再同步保存，然后 `Shutdown()`。
- [ ] **Step 5:** `EditorWindow`：
  - 标题 `L("编辑教程")`，最小尺寸 760×480。
  - 顶部放标题输入框（宽 260）和开始/停止录制按钮。
  - 没有步骤时居中显示 `L("还没有步骤。点上方的「开始录制」，或从任务栏右下角的托盘图标开始。")`。
  - 关闭窗口只是隐藏，程序不退出。
  - `windows.strings` 加入这些新键的英文：`"ClickDoc requires Windows 10 version 2004 or later."`、`"No steps yet. Click Start Recording above, or use the tray icon at the bottom right of the taskbar."`。
- [ ] **Step 6:** 运行 `dotnet test windows/ClickDoc.Core.Tests`（本地化测试现在能扫描到真实的键，期望全部通过），再运行 `dotnet build windows/ClickDoc -c Release`（期望 0 警告 0 错误）。
- [ ] **Step 7:** 运行 `windows/scripts/publish.sh`，把 `windows/out/ClickDoc` 拷进虚拟机运行。逐项检查：
  - 编辑器自动打开，托盘图标出现
  - 托盘图标左键、右键都弹出菜单，点菜单外面菜单会关闭
  - 再次启动 exe 不会开第二个实例，而是把编辑器调到前台
  - 改一下标题后退出，再启动，标题被恢复
  - 系统显示语言为英文时界面是英文
  - 深色模式下界面跟着变深

  把结果记到提交说明里。
- [ ] **Step 8:** 提交 `feat(windows): app shell, tray, draft autosave`。

---

### Task 10: 录制管线

对照 Mac：`AppState.receive/process`、`ClickMonitor.swift`、`ScreenCapturer.swift`、`ElementInspector.swift`。

**Files:**
- Create: `windows/ClickDoc/Recording/{MouseHook,WindowList,ScreenCapture,ElementInspector,CaptureExclusion,Recorder}.cs`
- Modify: `AppState.cs`、`Native/Win32.cs`

**Interfaces:**
- Consumes: `ClickCounter`、`Steps.Decide`、`Steps.CanUpgradeLastStep`、`Geometry.*`、`Imaging.RenderHighlight`、`Naming.*`、`TrayIcon.GetIconRect`
- Produces:
  - `sealed class MouseHook : IDisposable`：`MouseHook(Action<RawClick> onClick)`，`bool Start()`，`void Stop()`。`readonly record struct RawClick(MouseButton Button, SKPointI Pos, DateTimeOffset At)`
  - `static class WindowList`：`IReadOnlyList<WindowSnapshot> Snapshot()`、`nint HitWindow(SKPointI p)`（`WindowFromPoint` 后 `GetAncestor(GA_ROOT)`）
  - `static class ScreenCapture`：`(SKBitmap Image, SKRectI Monitor, float Scale) CaptureMonitorAt(SKPointI p)`
  - `static class ElementInspector`：`(string? AppName, string? ElementName) Inspect(SKPointI p)`，整体用 `Naming.RunWithTimeout` 包住，超时 250ms
  - `static class CaptureExclusion`：`void Install()`，在 `App.OnStartup` 里调用
  - `sealed class Recorder`：`Recorder(AppState state, TrayIcon tray)`，`bool Start()`，`Task Stop(bool fromTray)`。`AppState.StopRecording(bool fromTray = false)` 调用它，托盘菜单传 `true`。

- [ ] **Step 1: `MouseHook`。**
  - 开一个专用后台线程，在线程里调用 `SetWindowsHookEx(WH_MOUSE_LL, proc, GetModuleHandle(null), 0)`，然后跑 `GetMessage` 循环。
  - 回调只处理 `WM_LBUTTONDOWN` 和 `WM_RBUTTONDOWN`：构造 `RawClick`，写入 `Channel<RawClick>`（无界、单读者），然后立刻 `CallNextHookEx` 返回。回调里不做任何其他事。
  - `Stop` 时用 `PostThreadMessage(WM_QUIT)` 结束线程，再 `UnhookWindowsHookEx`。
  - hook 委托要存在字段里，防止被 GC 回收。
- [ ] **Step 2: `WindowList.Snapshot`。** 用 `EnumWindows` 按由前到后的顺序枚举顶层窗口。
  - 跳过以下窗口：不可见的、`DWMWA_CLOAKED` 不为 0 的、透明度为 0 的分层窗口、带 `WS_EX_TRANSPARENT` 的穿透窗口。
  - 窗口边框用 `DWMWA_EXTENDED_FRAME_BOUNDS` 取，失败时退回 `GetWindowRect`。
  - `IsPopup` 为真的情况：带 `WS_POPUP` 样式且没有 `WS_CAPTION`，或者类名是 `#32768`。
  - `IsShell` 为真的情况：类名属于 `Shell_TrayWnd`、`Shell_SecondaryTrayWnd`、`Progman`、`WorkerW`、`NotifyIconOverflowWindow`、`TopLevelWindowForOverflowXamlIsland`、`Windows.UI.Core.CoreWindow`（开始菜单、通知中心）。
- [ ] **Step 3: `ScreenCapture`。**
  - 先用 `MonitorFromPoint(MONITOR_DEFAULTTONEAREST)` 找到点击所在的显示器，再用 `GetMonitorInfo` 取范围，用 `GetDpiForMonitor(MDT_EFFECTIVE_DPI)` 取缩放比例（DPI/96）。
  - 截图：从屏幕 DC `BitBlt(SRCCOPY | CAPTUREBLT)` 到 DIB section（32 位 BGRA，自顶向下）。
  - 画鼠标指针：`GetCursorInfo`（只在 `CURSOR_SHOWING` 时画）→ `GetIconInfo` 取热点 → `DrawIconEx` 画在 `指针位置 − 热点 − 显示器左上角` 处。画完记得释放 `GetIconInfo` 返回的位图。
  - 把像素复制进 `SKBitmap(BGRA8888, Premul)`，alpha 一律设为 255。
- [ ] **Step 4: `ElementInspector`。**
  - 取控件：`AutomationElement.FromPoint`。父控件用 `TreeWalker.ControlViewWalker.GetParent` 取。按 `Naming.PickElementName` 规则选名字。
  - 取应用名：
    1. 先由 `WindowFromPoint` 取到顶层窗口，再用 `GetWindowThreadProcessId` 得到 pid。
    2. 用 `OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION)` 加 `QueryFullProcessImageName` 得到 exe 路径。
    3. 用 `FileVersionInfo.GetVersionInfo(path).FileDescription` 取文件描述，用 `GetWindowText` 取窗口标题。
    4. 交给 `Naming.AppName`。
  - 缓存：按 exe 路径缓存文件描述。
- [ ] **Step 5: `CaptureExclusion.Install`。** 对 ClickDoc 自己的每个窗口句柄调用 `SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE)`。
  - 覆盖范围：所有 `Window`（用 `EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, …)` 挂接），以及 `ContextMenu.Opened`、`ToolTip.Opened`、`MenuItem.SubmenuOpened`、`ComboBox.DropDownOpened` 打开的弹出层（句柄用 `PresentationSource.FromVisual(…) as HwndSource` 取）。
  - 已经处理过的句柄记在 `HashSet<nint>` 里，不重复设置。
- [ ] **Step 6: `Recorder`。** 用一个读者线程按顺序消费 channel，每次点击按下面的顺序处理：
  1. `ClickCounter.Register` 得到点击次数。`ClickCounter` 的参数来自 `GetDoubleClickTime`、`SM_CXDOUBLECLK`、`SM_CYDOUBLECLK`。
  2. 取 `WindowList.Snapshot()` 和 `HitWindow`。
  3. 判断是不是点在 ClickDoc 自己身上，是就直接丢弃这次点击。满足任一条件即算：
     - 被点窗口的 pid 等于本进程 pid；
     - 点击位置落在 `tray.GetIconRect()` 范围内。
  4. 如果是左键三击或以上，不截图。否则马上启动截图任务（`Task.Run`），任务内部依次做：
     1. 截取显示器画面。
     2. 当截图范围是 `Window` 时：用这次点击时的窗口快照算一次 `CaptureCropRect`；截图完成后马上再取一次快照、再算一次（这次不传 `hitWindow`），两个矩形取并集，再裁到显示器内（对应 Mac 版的 `refine`）。如果第一次算出来是 null，就截整屏。
     3. 按裁剪范围裁切图像。
     4. 调 `RenderHighlight`，参数为：圆的位置 = `HighlightRect(click, 图像左上角, scale)`，线宽缩放 = `scale`。
     5. 编码成 PNG。
  5. 同时启动 `ElementInspector.Inspect`。
  6. 把“等待截图和控件信息、再决定新建一步还是升级为双击”的处理挂到串行链的末尾：前一次点击的处理完成之前，后一次点击不会追加步骤。
     - 决策调用 `Steps.Decide`，`hasPreviousStep` 参数传 `Steps.CanUpgradeLastStep(...)` 的结果。
     - 新建步骤时，通过 `Dispatcher.InvokeAsync` 回到界面线程追加，并记下每一步的 `(At, IsShell, AppName, ElementName)`，供升级双击和停止时裁尾使用。
     - 截图失败时设置 `LastError = L("截图失败：%@", …)`。
- [ ] **Step 7: 开始与停止。**
  - `Start` 时：重置“上一步点击”的记录；如果 `MouseHook.Start()` 失败，设置 `LastError = L("无法监听鼠标点击")`（在 `windows.strings` 里加英文 `"Couldn't listen for mouse clicks"`）。
  - `Stop(fromTray)` 时：先停掉钩子，等串行链处理完。如果是从托盘停止的，删除 `TrailingShellSteps(..., stoppedAt, 5 秒)` 算出的那几步。最后打开编辑器。
  - 托盘图标随录制状态切换图标和提示文字。
  - 移动或删除步骤后，清空“上一步点击”的记录（同 Mac）。
- [ ] **Step 8:** 运行 `dotnet build windows/ClickDoc -c Release`，期望 0 警告 0 错误。然后运行 `dotnet test windows/ClickDoc.Core.Tests`，期望全部通过。
- [ ] **Step 9:** 在虚拟机里验证以下各项，结果写进提交说明：
  - 记事本：单击、双击、右键各录一次，分别生成 3 步，描述正确
  - 三击：不生成新步骤
  - 编辑器开着时点它：不被记录，也不出现在截图里
  - 打开托盘溢出区后点“停止录制”：溢出区相关的那几步被删掉
  - “当前窗口”模式点下拉菜单：截图包含菜单
  - 点桌面：截整屏
  - 设置应用：应用名不是 “Application Frame Host”
  - 连续快速点 10 次：生成 10 步，顺序正确
- [ ] **Step 10:** 提交 `feat(windows): recording pipeline`。

---

### Task 11: 编辑器：步骤列表与详情

对照 Mac：`EditorView.swift`。

**Files:**
- Modify: `windows/ClickDoc/Editor/EditorWindow.xaml(.cs)`
- Create: `windows/ClickDoc/Editor/StepListView.cs`

**Interfaces:**
- Consumes: `AppState`、`Selection.DeletionTargets/AfterDeleting`、`Imaging.Thumbnail`、`Annotations.Compose`
- Produces:
  - `EditorWindow` 对外暴露：`event Action? CommitPendingEdits`（导出前触发）
  - 选中单个步骤时，`EditorWindow` 往详情区放一个 `AnnotationCanvas`（Task 12）

- [ ] **Step 1: 布局。** 左右两栏，中间有可拖动的分隔条；左栏宽度最小 220，默认 280。
- [ ] **Step 2: 步骤列表。** 用 `ListBox`，`SelectionMode=Extended`。
  - 每行显示：
    - 缩略图：80×48，等比缩放、圆角 4。内容为 `Thumbnail(png, 240)` 再经 `Compose`（styleWidth 取原图宽）。按步骤 Id 缓存，该步的模糊区域或标注变化时作废。
    - 小号灰字 `L("步骤 %lld", n)`。
    - 描述的第一行，单行显示，超长省略。
  - 支持 Ctrl 点选、Shift 连选、Ctrl+A 全选（`ListBox` 自带）。
  - 按 Delete 删除所选步骤。
  - 右键菜单有两项：
    - 删除：只删一步时文字为 `L("删除")`，多步时为 `L("删除所选的 %lld 个步骤", n)`；删除对象由 `DeletionTargets` 决定。
    - `L("全选")`。
  - 拖动排序：拖动时显示插入位置的横线；落下后调用 `Tutorial.Move` 并 `Touch()`。
  - 一次删除多步时弹确认框：标题 `L("删除 %lld 个步骤？", n)`，正文 `L("删除后无法恢复。")`，按钮 `L("删除")` 和 `L("取消")`。删除后按 `AfterDeleting` 的结果设置选中项。
- [ ] **Step 3: 右侧详情区。**
  - 选中多个步骤时：显示 `L("已选择 %lld 个步骤", n)`、一个按钮 `L("删除这 %lld 个步骤", n)`，以及提示 `L("按住 Ctrl 点选多个，按住 Shift 连续选择，Ctrl+A 全选")`（英文写进 `windows.strings`：`"Hold Ctrl to pick several, Shift for a range, Ctrl+A for all"`）。
  - 选中一个步骤时：从上到下依次是工具栏（Task 12）、画布、`L("描述")` 标题、描述编辑框（高 80，多行）。描述每次改动都调用 `UpdateDescription` 和 `Touch()`。
  - 什么都没选时：显示 `L("选择一个步骤")`。
- [ ] **Step 4:** 运行 `dotnet build` 和 `dotnet test`，期望全部通过。
- [ ] **Step 5:** 在虚拟机里验证：多选；批量删除时弹确认，删除后自动选中正确的步骤；拖动排序；改描述后列表行同步更新；重启后改动还在。
- [ ] **Step 6:** 提交 `feat(windows): step list and detail pane`。

---

### Task 12: 标注画布与工具栏

对照 Mac：`AnnotationCanvas.swift`，以及 `EditorView.markupTools`。

**Files:**
- Create: `windows/ClickDoc/Editor/AnnotationCanvas.cs`（继承 `FrameworkElement`，在 `OnRender` 里绘制）
- Modify: `EditorWindow.xaml(.cs)`

**Interfaces:**
- Consumes: `Editing.*`、`Annotations.Render`、`Annotations.TextSize`、`Imaging.BlurSigma`
- Produces: `AnnotationCanvas(byte[] png, IReadOnlyList<SKRect> blur, IReadOnlyList<Annotation> marks, EditorTool tool)`，以及 `event Action<IReadOnlyList<SKRect>> BlurCommitted`、`event Action<IReadOnlyList<Annotation>> AnnotationsCommitted`、`CanvasItem? Selected`、`void CommitText()`、`void DeleteSelected()`；`enum EditorTool { Select, Blur, Rectangle, Arrow, Text }`

- [ ] **Step 1: 工具栏。**
  - 五个互斥的切换按钮：`L("选择")`、`L("模糊")`、`L("方框")`、`L("箭头")`、`L("文字")`。
  - 两个按钮：`L("删除选中")`（没有选中项时禁用）、`L("清除全部")`（没有模糊区域也没有标注时禁用）。
  - 旁边显示当前工具的提示文字，中文照抄 Mac 的 `EditorTool.hint`。这几条提示里没有 ⌘，所以 Mac 的英文翻译可以直接复用。
- [ ] **Step 2: 绘制。** 画布内的图像等比适应居中显示，从下到上依次画四层：
  1. 原图（`BitmapImage`，冻结后使用）。
  2. 模糊层：同一张图加 `BlurEffect`，`KernelType=Gaussian`，`Radius = 3 × BlurSigma(原图宽) × 显示宽 / 原图宽`；用所有模糊矩形组成的 `GeometryGroup` 作为裁剪区域。
  3. 标注层：每次标注变化时，先建一个与显示尺寸相同的透明 `SKBitmap`，用 `Annotations.Render(…, styleWidth: 原图宽)` 画上标注，再复制进 `WriteableBitmap` 显示。拖动过程中的草稿形状也画在这一层。
  4. 选中层：用 WPF 直接画，包括选中框，以及半径 4.5 的圆形手柄（白色填充，强调色描边）。
- [ ] **Step 3: 交互。** 规则同 Mac：
  - 鼠标按下时，用 `Editing.HitTest` 判断点中了什么。工具不是“选择”或“模糊”时，`includeBlurBodies` 传 false。
  - 拖动时，根据点中的对象进入不同模式：新建、移动、调整大小、移动箭头端点。
  - 新建的形状如果宽或高小于 4 DIP，直接丢弃。
  - 松开鼠标时触发对应的 Committed 事件。
  - 在文字上双击（`e.ClickCount == 2`）进入文字编辑；文字工具下单击图片也进入文字编辑。编辑框是一个叠在画布上的 `TextBox`，字号按比例缩放，红色字。
    - 回车：确认。
    - Esc：取消。
    - 失去焦点：确认。
    - 确认时用 `ClampTextOrigin` 修正位置，文字为空则删除这个标注。
  - 画布获得焦点时，按 Delete 删除选中项。
  - 切换到别的步骤或触发 `CommitPendingEdits` 时，先调用 `CommitText()`。
- [ ] **Step 4:** 运行 `dotnet build` 和 `dotnet test`，期望全部通过。
- [ ] **Step 5:** 在虚拟机里验证：
  - 各工具都能画，也都能被选中、移动、调整大小、删除。
  - 方框内部还能画新标注。
  - 文字支持新建、修改、取消。
  - 列表缩略图会同步更新。
  - 深色模式下手柄清楚可见。
  - 和 Task 13 导出的 PNG 对比：画布上的文字大小和位置一致。
- [ ] **Step 6:** 提交 `feat(windows): annotation canvas`。

---

### Task 13: 导出与新建教程

**Files:**
- Create: `windows/ClickDoc/Editor/ExportMenu.cs`
- Modify: `AppState.cs`、`EditorWindow.xaml(.cs)`

**Interfaces:**
- Consumes: `Export.ExportTutorial`、`Export.ExportNotionZip`、`Pdf.ExportPdf`
- Produces: `enum ExportFormat { MarkdownFolder, NotionZip, Pdf }`、`void AppState.Export(ExportFormat f)`

- [ ] **Step 1: 导出按钮。** 顶部工具栏放一个分体按钮：
  - 主体：文字 `L("导出")`，点击直接导出 Markdown 文件夹。
  - 箭头：点击弹出菜单，三项为 `L("Markdown 文件夹…")`、`L("Notion 导入包（.zip）…")`、`"PDF…"`。
  - 教程为空时整个按钮禁用。
- [ ] **Step 2: `AppState.Export`。** 依次执行：
  1. 触发 `CommitPendingEdits`。
  2. 弹出 `Microsoft.Win32.OpenFolderDialog`，标题用 Mac `panelMessage` 里对应格式的中文原文（经过 `L`）。
  3. 在后台线程执行导出，避免界面卡住。
  4. 成功：设置 `HasUnexportedChanges = false`，然后运行 `explorer.exe /select,"<结果路径>"` 打开资源管理器并选中导出结果。
  5. 失败：弹窗，标题 `L("导出失败")`，正文是异常信息。
- [ ] **Step 3: `NewTutorial`。** 如果有未导出的改动且教程不为空，先弹确认：内容 `L("当前教程尚未导出，新建将丢失这些步骤。确定继续？")`，按钮 `L("新建")` 和 `L("取消")`。确认后清空教程、清空各种缓存、清空草稿。
- [ ] **Step 4:** 把 `LocalizationTests` 里“扫描到的键数”的下限从 0 提高到 50，这时所有界面都已完成。然后运行 `dotnet build` 和 `dotnet test`，期望全部通过。
- [ ] **Step 5:** 在虚拟机里分别验证三种导出格式：
  - Markdown：在 VS Code 预览里能看到图片。
  - Notion zip：导入 Notion 后图片正常显示。
  - PDF：用 Edge 打开，中文显示正常，分页正确。
  - 标题为 `CON` 时导出也能成功。
  - 未导出时点“新建教程”会弹确认框。
- [ ] **Step 6:** 提交 `feat(windows): export and new tutorial`。

---

### Task 14: 安装包、CI、安装说明、手动测试清单

**Files:**
- Create: `windows/installer/ClickDoc.iss`、`windows/installer/ChineseSimplified.isl`、`.github/workflows/windows.yml`、`packaging/Windows 安装说明.txt`、`packaging/Windows Install Guide.txt`、`docs/windows-manual-test.md`
- Modify: `README.md`（加“Windows 版”一节：系统要求、安装、开发命令）

**Interfaces:**
- Produces: Release 上的 `ClickDoc-<版本>-Setup.exe` 和 `ClickDoc-Setup.exe`

- [ ] **Step 1: `ClickDoc.iss`。**
  - `AppId` 固定为一个新生成的 GUID，生成后不得再改。
  - `AppVersion` 由命令行 `/DAppVersion=` 传入。
  - 安装目录 `{localappdata}\Programs\ClickDoc`，`PrivilegesRequired=lowest`。
  - `ArchitecturesAllowed=x64compatible`，`ArchitecturesInstallIn64BitMode=x64compatible`。
  - 安装界面两种语言：`English` 和 `chinesesimplified`（`ChineseSimplified.isl` 从 jrsoftware/issrc 仓库 `Files/Languages/Unofficial/` 下载后提交）。
  - 创建开始菜单快捷方式，安装完成页提供“运行 ClickDoc”选项。
  - 安装和卸载时，如果 ClickDoc 正在运行，先提示用户关闭（`CloseApplications=yes`）。
  - 卸载时不删除 `%LocalAppData%\ClickDoc`。
  - 输出文件名 `ClickDoc-{版本}-Setup.exe`。
- [ ] **Step 2: `windows.yml`**，运行在 `windows-latest` 上。
  - 触发条件：
    - `push` 和 `pull_request`，只在 `windows/**`、`VERSION` 或这个工作流文件有改动时
    - `workflow_dispatch`
    - `release: types: [published]`
  - 每次都执行：`actions/setup-dotnet`（`dotnet-version: 10.0.x`）→ `dotnet test windows/ClickDoc.Core.Tests` → `dotnet publish`，参数同 `publish.sh`。
  - 手动触发和 Release 触发时，额外执行：
    1. `choco install innosetup -y`
    2. 用 `ISCC.exe` 编译安装包
    3. 复制一份，命名为 `ClickDoc-Setup.exe`
  - 手动触发时：把两个文件作为 Actions 产物上传。
  - Release 触发时，满足 `if: vars.WINDOWS_RELEASE_ENABLED == 'true'` 才执行。先检查 `github.event.release.tag_name` 是否等于 `v` + `VERSION` 的内容，不一致就报错停止；一致则运行 `gh release upload <tag> <两个文件> --clobber`。工作流权限设为 `contents: write`。
- [ ] **Step 3: 安装说明。** 中英文各一份，内容包括：
  - 下载后运行安装包。
  - 遇到 SmartScreen 拦截时，先点“更多信息”，再点“仍要运行”。
  - 托盘图标可能在任务栏右下角的 “^” 里，可以把它拖到任务栏上常驻。
  - 无法录制以管理员身份运行的程序。
  - 草稿位置。
  - 升级时直接运行新版安装包覆盖即可。
- [ ] **Step 4: `docs/windows-manual-test.md`。** 手动测试清单，每项写清楚操作步骤和预期结果，分两部分：
  - VM 部分：汇总 Task 9–13 的检查项。
  - 真机部分，由作者执行：
    - 100%、125%、150%、200% 四种缩放下，红圈位置都准确
    - 两块显示器缩放比例不同、副屏在主屏左侧或上方时，两块屏上的红圈和裁剪都正确
    - 在两块屏之间拖动编辑器窗口后，界面清晰、坐标正确
    - 右键菜单、下拉菜单的截图范围
    - 托盘溢出区
    - 单实例
    - 升级安装后草稿保留，卸载后草稿也保留
    - SmartScreen 提示的流程
    - 以管理员身份运行的程序不会被录到，录制也不会崩溃
- [ ] **Step 5:** 推送 `windows-port` 分支，确认 push 触发的工作流通过。再在该分支上手动触发一次工作流，下载产物，在虚拟机里完成安装、运行、卸载全流程。
- [ ] **Step 6:** 提交 `ci(windows): installer, workflow, install guides`。
- [ ] **Step 7:** 交给作者，由作者完成：
  - 按 `docs/windows-manual-test.md` 做真机测试。
  - 合并分支。
  - 在仓库变量中设置 `WINDOWS_RELEASE_ENABLED=true`。
  - 把 `VERSION` 改为 `0.8.0`，按原流程发布 Release。

---

### Task 15: 官网（等 0.8.0 的 Release 上已经有 `ClickDoc-Setup.exe` 后再做）

**Files:**
- Modify: `website/index.html`、`website/main.js`、`website/style.css`（如需要）

- [ ] **Step 1:** 先确认 `https://github.com/ibeeger/clickdoc/releases/latest/download/ClickDoc-Setup.exe` 返回 302，并且能跳转到真实文件（用 `curl -sI` 检查）。不满足就停在这一步，不往下做。
- [ ] **Step 2:** 在 `main.js` 里加 `detectOS()`：先读 `navigator.userAgentData?.platform`，取不到再看 `navigator.userAgent` 是否包含 `Windows`；返回 `"windows"` 或 `"mac"`，无法判断时返回 `"mac"`。
  - 所有下载按钮的链接和文字（`下载 Windows 版` / `Download for Windows`，或者原来的 macOS 文案）都按检测结果设置。
  - 每个主按钮下面加一行另一个平台的小链接。
- [ ] **Step 3:** 安装步骤部分分成 macOS 和 Windows 两块，各自有中英文文案。Windows 的内容与 Task 14 的安装说明一致。系统要求一并写明：macOS 14+ / Windows 10 2004+（x64）。
- [ ] **Step 4:** 本地预览：`cd website && python3 -m http.server`，检查两件事：
  - 在 Chrome 开发者工具里把 User-Agent 切换成 Windows，按钮应变为 Windows 版。
  - 中英文切换后文案都正确。
- [ ] **Step 5:** 提交 `feat(website): Windows download and install steps`，推送到 `main`。
