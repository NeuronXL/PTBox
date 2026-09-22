# 开发、构建、测试与交付手册

基线：2026-09-22。命令从项目根目录 `D:\dev\PTBox` 执行，其他机器替换为实际路径。

1.1.0 新增 UpdateCore、Updater、ReleaseTool 项目，本地签名发布命令为 `scripts/release.ps1`。2026-09-22 已获准将源码与 1.1.1 发行物发布到 NeuronXL/PTBox。当前流程与沙盒验收说明见 [发布更新操作](release-operations.md)，后文的 1.0.0 安装记录为历史基线。

## 1. 开发环境

- Windows 11 x64，.NET 8 SDK；可用 `setup-sdk.ps1` 准备项目内 `.tools/dotnet/`，SDK 不提交到仓库。本次验证使用 SDK 8.0.425。
- WPF 开发可使用 Visual Studio 的 .NET 桌面开发工作负载，打开 `PTBox.sln`。
- PowerShell 脚本通过 `scripts/common.ps1` 优先选择项目 SDK，配置进程级 DOTNET_ROOT 和项目内 CLI 缓存。
- `global.json` 与 `Directory.Build.props` 管理 SDK 和公共编译参数；应用 manifest 指定普通用户权限与 PerMonitorV2。
- 首次在新环境准备 SDK：`powershell -ExecutionPolicy Bypass -File .\scripts\setup-sdk.ps1`。

源码仓库为 `https://github.com/NeuronXL/PTBox`。不要把 `.tools/`、`artifacts/`、`bin/`、`obj/` 或个人配置备份提交为源码。

## 2. 常用命令

```powershell
# 编译、开发运行、应用测试
powershell -ExecutionPolicy Bypass -File .\scripts\build.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\run.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\test.ps1

# 生成可运行的便携目录
powershell -ExecutionPolicy Bypass -File .\scripts\publish.ps1

# 生成安装包，可指定下次版本号
powershell -ExecutionPolicy Bypass -File .\scripts\package.ps1 -Version 1.0.0

# 验证安装包（无已安装 PTBox、已退出 Launcher 的环境）
powershell -ExecutionPolicy Bypass -File .\scripts\test-installer.ps1 -Version 1.0.0
```

`package.ps1` 不会自动执行应用测试；发布前需先运行 `test.ps1`。`release.ps1` 可依次测试、打包、签名、自验，但不上传 GitHub；正式发布由维护者上传完整附件并核对后执行。

## 3. 交付产物

| 路径 | 内容 | 注意 |
| --- | --- | --- |
| `artifacts/PTBox-win-x64/` | 自包含便携版 | 可含个人配置，分发前检查内容 |
| `artifacts/installer/` | 安装 EXE 和 `.sha256` | 从干净发布目录打包，不读取个人便携目录 |
| `artifacts/verification/` | 应用 WPF 渲染图 | 用于视觉检查，不等同于实机截图 |
| `artifacts/installer-tests/<随机 ID>/` | 安装测试报告和日志 | 测试安装已卸载，保留配置用于验证 |
| `.tools/installer-stage/<随机 ID>/` | 安装包干净暂存目录 | 每次构建生成独立目录，当前脚本不自动清理 |

`publish.ps1` 在发布前读取已有便携版配置，最后恢复原字节；其目的在于保护当前使用的配置。不要用直接 `dotnet publish -o artifacts/PTBox-win-x64` 替代它并假定同样保留配置。

`package.ps1 -Version x.y.z` 使用指定版本编译到新暂存目录，检查默认应用为空，调用 Inno Setup 生成版本化文件，并写 SHA256。版本号格式目前只接受三段数字，不支持 prerelease 字符串。

## 4. 安装器约定

脚本：`installer/PTBox.iss`。AppId 固定为 `{84089F79-568E-4B87-A01C-9FE490CAB973}`，后续升级不要更换。

默认位置 `%LOCALAPPDATA%\Programs\PTBox`；`PrivilegesRequired=lowest`，Windows 11 x64，自包含运行环境。默认创建开始菜单，桌面快捷方式可选；安装完成后启动应用为可选且默认不勾选。

配置文件仅首次不存在时写入，卸载保留。普通程序和运行环境文件升级时替换。卸载仅删除完全匹配当前安装 EXE 路径的 HKCU Run 项，不处理其他位置的 PTBox 副本。

`setup-installer.ps1` 固定下载 Inno Setup 6.7.3，验证下载 SHA256 和 Authenticode 发布者后便携解压到 `.tools`；不注册文件关联或系统软件。工具与语言文件来源见 [安装器说明](../installer/README.md)。PTBox 输出安装包本身目前未代码签名。

## 5. 测试分层

| 层级 | 入口 | 验证重点 |
| --- | --- | --- |
| 配置 / 业务 / 文件选择 | `tests/PTBox.Tests/Program.cs`、`FilePickerTests.cs` | 校验、恢复、导航、动态首页、模板、路径和文件类型 |
| 图标 | `tests/PTBox.Tests/IconTests.cs` | 本地原图标、网站模拟响应、缓存和失败回退 |
| WPF 集成 | `tests/PTBox.Tests/UiSmoke.cs` | 实际窗口、路由输入、模态流程、设置草稿、布局和渲染 |
| 进程生命周期 | `tests/PTBox.TestApp/` | 真实外部测试窗口、复用、退出后返回 |
| 安装 / 升级 / 卸载 | `scripts/test-installer.ps1` | 干净安装、启动、逐字节保留配置、快捷方式与清理 |
| 客厅实机 | [验收清单](verification.md) | 遥控器、电视、混合 DPI、HDMI、前台行为 |

应用测试目前 26 组；历史 1.0.0 安装器检查为 16 项，1.1.x 安装升级未由自动测试在本机执行。测试使用临时数据，不加载个人便携配置。历史七应用配置只在 `tests/PTBox.Tests/Fixtures/legacy-config.json` 中用作兼容性测试，不能作为新安装默认值。

应用测试会短暂显示 WPF 窗口，应先退出正式 Launcher。安装测试拒绝已有安装和冲突菜单目录，在项目内唯一目录安装，临时写当前用户开始菜单与卸载登记，然后卸载；不会启用自动启动。它会结束自己启动的测试进程，不得扩展成按名称批量结束进程。

CI 接入时须确认 Windows runner 对 WPF 集成测试的支持；不能因桌面不可用而跳过测试后仍报告全部通过。关机、重启和真实登录启动由用户在专门验收环境确认执行。

## 6. 当前手动升级流程

1. 记录版本和修改内容，退出待覆盖的开发/发布版本。
2. 运行应用测试，检查相关界面渲染。
3. 用新的 `-Version` 构建安装包，在干净环境运行安装测试。
4. 确认配置保留、默认列表为空、没有个人日志与备份。
5. 用户退出 PTBox 后运行新版安装包，选择已有安装位置。
6. 更新验证记录，记录该文件 SHA256；重新打包会生成新的校验值。

## 7. 常见问题

| 现象 | 排查方式 |
| --- | --- |
| 构建或升级提示文件占用 | 从电源菜单退出对应 Launcher，确认运行路径，不强制关闭用户的其他应用 |
| 首页仍有旧应用 | 查看实际数据根里的配置；发布保护旧配置，不会自动清空个人列表 |
| 图标或壁纸消失 | 检查保存的是绝对路径还是相对数据根路径，以及文件是否随迁移保留 |
| 应用退出后没有回来 | 检查是否常驻、转交子进程或使用 URL/URI；必要时改手动返回 |
| 热键无效 | 查看首页提示及日志，检查热键冲突；再次运行 EXE 可以唤回 |
| 配置异常被重置 | 保留日志与 `.broken` 副本，检查版本、唯一 ID 和字段取值 |
| 安装版和便携版互相唤回 | 当前按用户单实例；两种形态不能独立并行运行 |
| 收不到在线更新 | 先手动安装最新接入版；仓库必须已有更高正式版本及签名附件。1.1.1 会区分访问拒绝、限流及无正式版本，失败诊断见数据目录 Logs/launcher.log，参见 [发布更新操作](release-operations.md) |
