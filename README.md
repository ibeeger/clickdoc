# RecordToDocs

一个类似 Folge 的 macOS 菜单栏小工具：录制鼠标点击，自动截图并标出点击位置，录完可编辑步骤、模糊敏感信息，导出为 Markdown + PNG 教程。

系统要求：macOS 14 或更高版本，Apple 芯片或 Intel 芯片。

## 安装（未公证版本）

1. 下载 `RecordToDocs-<版本>.dmg`，打开后把 RecordToDocs 拖进“应用程序”。
2. 本应用未经苹果公证，首次打开会被系统拦截。打开“终端”执行：

   ```
   xattr -dr com.apple.quarantine /Applications/RecordToDocs.app
   ```

   或者在“系统设置 > 隐私与安全性”底部点“仍要打开”。
3. 首次录制时，按提示授予“辅助功能”和“屏幕录制”权限，然后重启应用。

dmg 里的 `安装说明.txt` 有更详细的步骤。

## 开发

只需要 Command Line Tools，不需要 Xcode。

```
swift run CoreTests          # 运行逻辑层测试
./scripts/build-app.sh       # 构建 build/RecordToDocs.app（通用二进制）
./scripts/release.sh         # 生成 build/release/RecordToDocs-<版本>.dmg
```

版本号写在 `VERSION` 文件里。构建脚本支持以下环境变量：

| 变量 | 作用 | 默认值 |
|---|---|---|
| `VERSION` | 版本号 | `VERSION` 文件内容 |
| `BUNDLE_ID` | 应用标识，发布后不要再改 | `com.local.recordtodocs` |
| `SIGN_IDENTITY` | 钥匙串里的代码签名证书名 | ad-hoc 签名 |

## 发布新版本

1. 修改 `VERSION`。
2. 运行 `./scripts/release.sh`，记下输出的 SHA-256。
3. 在 GitHub Releases 新建版本，上传 dmg，在说明里附上 SHA-256 和上面的安装步骤。
