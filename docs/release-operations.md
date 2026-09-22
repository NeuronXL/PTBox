# PTBox 发布更新：实现与操作

当前版本为 1.1.4，源码与更新源固定为 `NeuronXL/PTBox`。1.1.1 至 1.1.3 已通过真实 GitHub 更新查询、签名与安装包下载校验，用户反馈可完成应用内更新。1.1.4 将快捷方式导入改为保存实际 EXE，并改善文件选择性能；干净 Windows 环境升级及失败恢复仍待专门验收。

## 客户端行为

设置 → 关于与更新：展示版本、更新说明、大小、进度、错误和重试入口，支持方向键导航。启动 8 秒后后台检查，每天最多一次；自动检查开关独立保存、立即生效。后台失败不抢焦点。手动检查不受每日频率限制，同一时刻只执行一次检查或下载。

仅支持 stable / win-x64 / 严格三段数字更高版本；草稿和预发布被忽略。latest 接口返回 404 时再确认仓库可访问，成功才显示“暂无正式版本”；仓库本身不可访问则报告失败。元数据支持 ETag，缓存仍需重新验签。检查总超时 40 秒；下载单次读取超时 45 秒、总超时 30 分钟。取消会删除未完成临时文件，坏缓存重新下载。暂缓会保留完整包，下次检查同版本并点击下载时重新校验、复用。首版无断点续传。

HTTP 403 只有存在限流证据（剩余额度为 0、Retry-After 或 GitHub JSON 限流消息）才按限流处理；429 按限流处理。确认限流后遵守 Retry-After / X-RateLimit-Reset，无有效时间时至少等待一分钟；冷却时间保存在 Updates/preferences.json，手动检查和下载也遵守，重启不能绕过。不会在后台连续重试。判断依据参见 [GitHub 官方限流说明](https://docs.github.com/en/rest/using-the-rest-api/rate-limits-for-the-rest-api)。

普通 403、401、404、5xx 和网络连接错误分别提示。检查/下载失败记录到数据目录 Logs/launcher.log，包含请求主机、路径、HTTP 状态、限流响应头和 GitHub 请求标识，不输出查询参数、原始 HTML 或其他原始错误正文。

安装版支持自动覆盖；便携版仅检查、下载和引导手动安装。确认安装时先保存设置并备份配置；独立更新器在用户暂存目录运行，重新校验清单、包、安装登记以及父进程路径/启动时间/当前版本。更新器就绪后 Launcher 才退出。验证后的安装包保持只读锁直到安装结束。固定参数包含 `/NORESTART /NOCLOSEAPPLICATIONS /NORESTARTAPPLICATIONS`，不结束游戏或 Moonlight，不重启 Windows。

安装后从原安装路径启动新版，等待 60 秒内确认目标版本和首页初始化。失败由独立更新窗口显示恢复提示及日志路径，不强杀运行中的安装器，不重复安装。首版提供重新安装修复说明，没有完整自动回滚。

## 代码模块

| 位置 | 职责 |
| --- | --- |
| `src/PTBox.UpdateCore/UpdateProtocol.cs` | 版本、清单、RSA-PSS / SHA256 签名、包大小和哈希 |
| `src/PTBox.UpdateCore/GitHubUpdateClient.cs` | GitHub API、ETag、受限 HTTPS 重定向、下载缓存 |
| `src/PTBox.UpdateCore/UpdateJob.cs` | 任务路径、安装记录、进程身份、固定安装参数、启动确认 |
| `src/PTBox.UpdateCore/ReleaseTrust.json` | 编入二进制的仓库和公钥，不含私钥 |
| `src/PTBox.Launcher/Services/UpdateService.cs` | 更新状态、操作互斥、偏好、独立进程交接 |
| `src/PTBox.Launcher/Services/DataMigrationService.cs` | 备份、迁移冲突、资源路径和幂等标记 |
| `src/PTBox.Updater/` | 独立 WPF 更新程序，复用设置按钮样式 |
| `tools/PTBox.ReleaseTool/` | 本地密钥、签名、发行文件生成和自验 |
| `scripts/release.ps1` | 测试 → 干净打包 → 签名 → 本地验证，无上传逻辑 |

## 安装模式与数据

安装器写入 `ptbox.install`（固定 AppId）。安装版使用 `%LOCALAPPDATA%\PTBox`；额外存在 `ptbox.portable` 时显式进入便携模式。旧目录没有安装标记时仍按便携模式处理。自动安装还要求当前用户卸载登记 InstallLocation 与实际位置相同，目录可写且不是重解析点。

首次迁移验证旧 `安装目录/Config/config.json` 并备份；相对图片复制到个人数据 `Resources` 下，相对程序路径转为原目录绝对路径。目标已有配置时明确选择，保留双方备份；按安装路径记录完成标记。失败继续使用原配置并提示。未知配置版本只读保护，不会重置或覆盖。卸载保留个人数据。

```text
%LOCALAPPDATA%/PTBox/
  Config/config.json
  Resources/<installation-id>/
  Backups/
  Migrations/<installation-id>.json
  Updates/
    preferences.json
    release-cache.json
    downloads/<version>/{package.exe,update.json,update.json.sig}
    jobs/<id>/{job.json,ready,go,started,result.json,setup.log,failure.log,runner/,...}
```

下载及任务日志暂不自动批量清理；排障后在 PTBox 和更新器均关闭时，可手动清理旧任务目录。

## 本地发布与密钥

客户端内嵌发行公钥。维护者私钥使用 Windows DPAPI 加密，保存在本机忽略目录，只能由对应 Windows 账号环境解密；不会提交到源码仓库或打包进安装器。普通源码构建不需要发布私钥。

`scripts/init-release-key.ps1` 仅供未配置信任根的新项目使用，已有公钥时拒绝覆盖。重装 Windows 或更换发布电脑前，用 ReleaseTool 的 `export-key ENCRYPTED_KEY BACKUP_FILE` 导出离线备份。该操作产生明文 PKCS#8 PEM，应安全保存，绝不能上传。新电脑用 `import-key BACKUP_FILE ENCRYPTED_KEY` 恢复，同现有公钥不匹配时会拒绝；不替换信任根。仅复制 DPAPI 文件到其他电脑通常无法恢复发布能力。

准备 `docs/releases/<version>.md` 后，在项目根目录运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/release.ps1 -Version 1.1.4
# 已对当前源码单独完成测试时，可显式加 -SkipTests。
```

相同版本发行目录存在时拒绝覆盖。版本参数同时传给 Launcher、Core、Updater 和安装器。`Directory.Build.props` 为开发版本来源。

`artifacts/releases/<version>/` 包含：安装 EXE、对应 `.sha256`、`update.json`、二进制 `update.json.sig` 和 `release-notes.md`。清单按原始 UTF-8 字节签名，不能编辑后复用签名。清单签名不同于 Windows Authenticode；本安装包尚未配置后者。

在 `NeuronXL/PTBox` 建立 `v<version>` Release 草稿，完整上传安装包、SHA256、清单、签名四个附件，把 notes 填入正文，再核对正式发布。可另附便携 ZIP 和其 SHA256。客户端不包含 GitHub Token；已有版本的签名清单与安装包保持原字节不变。

## 验证边界

`scripts/test.ps1` 覆盖签名/公钥/架构/版本/文件大小错误、ETag、预发布过滤、404、限流、断网、取消、损坏缓存、校验后文件锁、重复点击与重试、迁移冲突/相对资源/幂等/未来格式保护，以及真实 WPF 页面和方向键。HTTP 使用模拟响应。

渲染图位于 `artifacts/verification/settings-updates.png`、`settings-update-available.png` 和 `settings-update-ready-portable.png`。1.2.0 是本地测试数据，不是线上版本。

本机已有 PTBox 开始菜单目录，安装测试被保护规则阻止；Windows Sandbox 未安装。因此未执行实际安装升级。1.1.1 已完成 GitHub 真实发布下载验证，安装与跨版本升级仍须实机验收。

准备两个本地签名版本（例如 1.1.0 和 1.1.1）后：

```powershell
scripts/test-update-sandbox.ps1 -FromVersion 1.1.0 -ToVersion 1.1.1
```

该命令只生成 `.wsb`，不启用系统功能、不自动启动。沙盒禁网，只读映射发行物和脚本，唯一可写主机目录为独立结果目录。在支持 Windows Sandbox 的机器打开后，安装接入版、迁移测试配置、执行真实更新器安装下一版，核对启动确认、版本、配置和单实例，导出结果。内层脚本拒绝在普通账号运行。

正式发布前仍需完成干净环境安装、两版本升级、失败安装恢复、GitHub 真下载及客厅设备遥控器实测。
