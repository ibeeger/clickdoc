# ClickDoc

一个类似 Folge 的 macOS 菜单栏小工具：录制鼠标点击，自动截图并标出点击位置，录完可编辑步骤、添加方框/箭头/文字标注、模糊敏感信息，导出为 Markdown、Notion 导入包或 PDF 教程。

截图范围可在菜单栏菜单的「截图范围」中切换：「当前窗口」只截被点击的窗口（连同它弹出的菜单），「整个屏幕」截整块显示器。

系统要求：macOS 14 或更高版本，Apple 芯片或 Intel 芯片。

## 导出格式

编辑窗口的「导出」按钮直接点击导出 Markdown 文件夹，点右侧箭头可选择其他格式：

| 格式 | 用途 |
|---|---|
| Markdown 文件夹 | `tutorial.md` + `images/`，适合放进代码仓库或用 Markdown 编辑器查看 |
| Notion 导入包（.zip） | 在 Notion 中选「导入 > Markdown」，选这个 zip，图片会一起导入 |
| PDF | A4 分页，适合发给别人阅读或打印 |

所有格式都会把模糊区域合成进图片，不含原始内容。

## 草稿

当前教程会自动保存到 `~/Library/Application Support/ClickDoc/draft/`，退出后重新打开会自动恢复。旧版本（RecordToDocs）留下的草稿会在首次启动时自动迁移过来。
草稿里保存的是原始截图和模糊区域，导出时才合成模糊。点「新建教程」会清空草稿（未导出时会先确认）。

## 安装（未公证版本）

1. 下载 `ClickDoc-<版本>.dmg`，打开后把 ClickDoc 拖进“应用程序”。
2. 本应用未经苹果公证，首次打开会被系统拦截。打开“终端”执行：

   ```
   xattr -dr com.apple.quarantine /Applications/ClickDoc.app
   ```

   或者在“系统设置 > 隐私与安全性”底部点“仍要打开”。
3. 首次录制时，按提示授予“辅助功能”和“屏幕录制”权限，然后重启应用。

dmg 里的 `安装说明.txt` 有更详细的步骤。

## 开发

只需要 Command Line Tools，不需要 Xcode。

```
swift run CoreTests          # 运行逻辑层测试
./scripts/build-app.sh       # 构建 build/ClickDoc.app（通用二进制）
./scripts/release.sh         # 生成 build/release/ClickDoc-<版本>.dmg
```

版本号写在 `VERSION` 文件里。应用图标来自根目录的 `icon.png`：构建时自动居中裁成正方形、套上 macOS 圆角外形并生成 `.icns`，只在 `icon.png` 更新后才重新生成。构建脚本支持以下环境变量：

| 变量 | 作用 | 默认值 |
|---|---|---|
| `VERSION` | 版本号 | `VERSION` 文件内容 |
| `BUNDLE_ID` | 应用标识，发布后不要再改 | `com.local.clickdoc` |
| `SIGN_IDENTITY` | 钥匙串里的代码签名证书名 | ad-hoc 签名 |

## 发布新版本

1. 修改 `VERSION`。
2. 运行 `./scripts/release.sh`，记下输出的 SHA-256。
3. 在 GitHub Releases 新建版本，上传 dmg，在说明里附上 SHA-256 和上面的安装步骤。
