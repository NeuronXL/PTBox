# MVP 验证记录

## 1.1.1 GitHub 发布前复核

- 在当前 Windows 开发环境恢复项目内 .NET SDK 8.0.425，重新执行 `scripts/test.ps1`：Release 构建 0 警告、0 错误，26 组全部通过。
- 使用当前源码构建的 ReleaseTool 重新核验归档 1.1.1 发行物：RSA-PSS 清单签名、安装包 105230413 字节及 SHA256 全部通过。
- 本次重新生成自包含便携目录，发行安装包沿用已归档的签名版本，不覆盖既有清单与签名。
- 便携 ZIP 已解压到隔离目录，在清除进程级 DOTNET_ROOT / DOTNET_ROOT_X64 后成功启动并正常关闭，日志无 ERROR；确认默认应用为空且自动启动关闭。
- 下方“未初始化 Git、未上传”等描述为此前历史记录，本次用户已授权源码上传与正式发布。
- 本机没有 Windows Sandbox；本次测试通过不代表已完成真实跨版本安装升级及故障恢复验收。

## 1.1.1 更新错误提示修复版（本地）

- `scripts/test.ps1`：完整 Release 构建 0 警告、0 错误，**26 组通过**。测试目录 `C:\Users\Neo\AppData\Local\Temp\PTBox.Tests\9a522bed3b19453d90d01724ef102c53`。
- 新增模拟 HTTP 验证：普通 403、额度耗尽 403、正文限流 403、429、Retry-After 秒数/日期、非法限流头、超长 HTML/非 JSON 错误、503、仓库不存在、仓库存在但无正式 Release、断网提示、诊断日志、冷却去重/过期/跨重启保存。
- 原有下载签名/哈希/缓存/取消、配置迁移、真实父进程及 WPF 布局/导航测试通过。网络响应测试均使用本地模拟，未据此认定用户原始 403 的实际原因。
- 未运行本次覆盖安装或线上升级，未修改现用安装；未初始化 Git、未上传。1.1.0 安装包作为历史版本保留。
- 本地安装包 `artifacts/installer/PTBox-Setup-1.1.1-win-x64.exe`：105230413 字节，SHA256 `AC24EA6882CBB37586E3C651EDB5C7FE9B5CA1A7F9E61299EEBCFDBA6E4F9529`。对应 `artifacts/releases/1.1.1` 的清单签名、版本、大小和哈希经 ReleaseTool 自验通过。

## 1.1.0 在线更新接入版（本地）

- 完整 Release 构建 0 警告、0 错误；`scripts/test.ps1` 当前 **25 组通过**，包含真实父进程路径/启动时间/版本身份校验和更新器失败窗口关闭行为。
- 新增协议与签名、版本/平台/大小拒绝、ETag、草稿/预发布过滤、404、限流、断网、取消、坏包及缓存恢复、包只读锁、重复操作与重试、更新偏好保存、迁移冲突/幂等/相对资源/未来格式保护。
- WPF 实际窗口验证更新页切换、方向键、模拟可用版本和下载完成状态；渲染图 `settings-updates.png`、`settings-update-available.png`、`settings-update-ready-portable.png` 位于 `artifacts/verification`。1.2.0 为模拟响应。
- 本次测试临时目录：`C:\Users\Neo\AppData\Local\Temp\PTBox.Tests\a6517de3c63e45eaab703268cc05d138`。独立失败窗口截图为 `artifacts/verification/updater-recovery.png`，长路径换行可见。
- 安装测试在任何安装操作之前检测到现有 PTBox 开始菜单目录并停止。系统无 WindowsSandbox.exe；没有绕过保护、修改现用安装或执行真实升级。
- 已提供 `scripts/test-update-sandbox.ps1` 生成离线沙盒验收配置，尚未运行沙盒。真实安装失败恢复、GitHub 真下载和客厅实测仍待完成。
- 更新源为 NeuronXL/PTBox；用户要求不初始化本地 Git、不上传，本次严格遵守。签名发行物及操作说明见 [发布更新操作](release-operations.md)。
- 最终本地安装包 `artifacts/installer/PTBox-Setup-1.1.0-win-x64.exe`：105240235 字节；SHA256 `F273D464CE0B7708CD7CD381B828C037D4BF4E838C0E326EE27E8C164880DF53`。`artifacts/releases/1.1.0` 包含相同安装包、SHA256、清单、RSA-PSS 签名和说明，ReleaseTool 自验通过。便携版同步到 1.1.0，个人配置字节未改变。
- 脚本语法和文档本地链接检查通过；安装暂存目录默认应用为空，无私钥、日志或个人更新偏好。签名私钥只保存在本机 DPAPI 加密文件中。

以下为旧版本历史记录，不能视为 1.1.0 的安装验收结果。

验证日期：2026-09-22。构建平台：Windows x64，项目内 .NET SDK 8.0.425。

文档整理检查：扫描 14 份 Markdown，39 处本地链接均可解析，2 份 JSON 示例可解析；已统一“已实现”和“下一阶段计划”的表述，在线更新列为下一目标。此次仅修改文档，未重新运行应用/安装测试；下述结果为对应现有交付物的已完成验证。安装包 SHA256 复核未改变。

## 1.0.1 网页地址修正版

交付：`artifacts/installer/PTBox-Setup-1.0.1-win-x64.exe`。SHA256：`F830FEFE43EDEFDECA806812FA724DCEE5D4D2317FFD72CD3919EE7F1EF4DE44`。便携目录同时更新为 1.0.1，个人配置按原字节保留。

标准 `https://www.baidu.com` 在真实 WPF 输入框中输入、切换焦点和保存均通过；由于用户现场窗口已退出，未读取到触发截图报错的原始字符串，不能认定具体根因就是全角字符。

本次增加 HTTP(S) 协议前缀全角分隔符容错、地址边缘空白/不可见标记清理、空网址保存拦截、具名错误和错误应用自动定位、编辑地址后清除旧报错，以及网页/协议模式隐藏 EXE 浏览按钮。应用测试扩为 **20 组**，包含实际输入控件写入、跨应用校验错误、失败不落盘、全角修正保存及旧提示清理。截图：`artifacts/verification/settings-address-validation.png`。

1.0.1 安装测试在开始安装前检测到现有 PTBox 开始菜单目录，按保护规则停止；没有绕过保护或移除用户目录。下方 16 项安装检查对应历史 1.0.0，不代表本次修正版重新通过安装测试。当前修正版仍为本地交付，未上传 GitHub，现用 `D:\PTBox` 未被修改。

## 1.0.0 安装包验证（历史基线）

已生成 `artifacts/installer/PTBox-Setup-1.0.0-win-x64.exe`（约 53.8 MiB），中文版深色向导，当前用户安装，无需管理员权限，包含 .NET 运行环境。SHA256：`078EFD327F513580E9CCBDC775206B04AB16B7B23FEA4E8D685FA588F315A7E1`。安装包尚未代码签名。

`scripts/test-installer.ps1` 对该最终文件完成 **16 项实际安装检查**：安装程序与运行环境、Windows 卸载登记、开始菜单快捷方式及目标路径、空应用列表、无开发日志/缓存/备份、安装后 WPF 程序启动、覆盖升级逐字节保留自定义配置、卸载删除程序/登记/快捷方式、卸载保留配置、原有自动启动状态和便携版配置不变。使用项目内独立目录和唯一测试菜单分组，测试安装已卸载；未启用开机启动，也未保留正式安装。

报告：`artifacts/installer-tests/6656dbc1e0fe45c0ae64a2e217ba663e/result.json`；同目录保留安装、升级、卸载日志。安装器使用独立干净的发布目录，不从个人便携版目录打包。

## 已完成

- 完整 Solution Release 构建：**0 警告，0 错误**。
- 独立 x64 发布版启动检查：清除进程级 DOTNET_ROOT / DOTNET_ROOT_X64 后启动，成功达到输入就绪；第二次运行在 10 秒内以退出码 0 返回，第一实例保持运行。验证结束后清理测试实例。
- `scripts/test.ps1`：**19 组全部通过**。测试程序不依赖第三方测试框架，失败返回非零退出码。
- 电视盒子首页新增：内嵌素材实际加载、顶部分类筛选、自定义应用分类、快捷入口目标、推荐页切换、全页面焦点图可达性。
- 实际 WPF 窗口中追加 12 个应用并连续方向键导航至末项，验证横向滚动且末项完整可见；验证新增电源菜单默认返回、方向键及 Esc 取消。
- JSON 往返、中文、参数、顺序、最后选中项；非法 ID、损坏配置备份及默认恢复。
- 导航边缘停止、上下同列、末行缺项、底部电源区；图标 / 颜色回退；设置草稿隔离。
- EXE 路径和环境变量解析，URI 协议校验。
- 单实例互斥和再次启动通知信号。
- 实际创建独立 WPF 测试进程，按完整路径复用同一 PID、关闭并等待退出。
- 实际创建首页 WPF 窗口，通过 routed key events 验证右 / 下 / Esc / Backspace / BrowserBack；调用真实按钮点击事件验证桌面最小化和恢复选择。
- 从首页启动短时测试进程，验证进程退出触发首页恢复，保留之前卡片。
- 电源模态框默认取消，右 / 左导航和 Esc 取消。**未执行实际关机或重启**。
- 测量并渲染真实 XAML 布局，检查首页固定区域按钮在 1920×1080、2560×1440、3840×2160 与 100% / 125% / 150% / 200% 的 12 种尺寸组合中不越界；应用横向滚动栏另行检查焦点可见性。渲染由对应逻辑尺寸和 RenderTargetBitmap DPI 完成，不冒充真实 OS DPI 切换。
- 人工查看首页、设置和电源框的 WPF 渲染图片，修复深色文字对比度问题。
- 设置页改版验证：从应用列表向右进入编辑区、确认键展开下拉菜单、焦点位于下拉选项时 Esc 仅关闭菜单、分类绑定写入草稿、页面切换保留草稿、确认键切换全屏开关且原配置不变。截图渲染宿主保留窗口资源，保证呈现实际控件样式。
- 动态首页验证：删除/改名/重排同步推荐与快捷入口，删除最后一个分类应用时回首页；0/1/2/3/7 应用的全部入口方向键可达；空配置保存后重新加载仍为空，旧配置不清空；Steam/Moonlight 模板去重和移除后重新添加。
- 原图标验证：读取真实 Explorer EXE 图标、自定义图标优先、损坏图片回退、已知品牌 ID 也不使用仿制图标、未知协议通用回退；模拟 HTTP 响应验证网站图标、HTML 链接发现、同源复用、磁盘离线缓存、超限和失败回退，不依赖外部网站可用性。
- 添加流程验证：从空首页点击添加，打开设置及选择器，选择网页、填写并保存，立即在首页和临时 JSON 中看到新应用；已添加模板禁用，返回不添加。外部进程测试通过文件信号要求测试程序在自己的 WPF Dispatcher 中正常关闭窗口，避免桌面消息关闭的时序波动。

生成的图片：`artifacts/verification/home-1920x1080-100pct.png`、`home-1920x1080-200pct.png`、`home-3840x2160-100pct.png`、`home-3840x2160-200pct.png`、`settings.png`、`settings-system.png`、`power-menu.png`、`power-confirmation.png`。

动态首页新增：`home-steam-moonlight.png`、`home-empty.png`、`add-app.png`。首页横向应用栏允许超出视口，验证其滚动到焦点项后完整可见；其他区域按钮在多分辨率/DPI 布局中不越界。

弹窗统一：添加应用、文件选择、确认提示和电源菜单使用共享 `DialogStyles.xaml`，与设置页共用控件样式。实际渲染检查 `add-app.png`、`file-picker.png`、`power-menu.png`、`power-confirmation.png`，确认圆角、文字、按钮与提示没有裁切。文件选择验证目录优先、扩展名大小写、图片与 EXE 过滤、相对路径、不存在文件和目录的提示、筛选及返回父目录；实际 WPF 模态流程验证“添加本地程序 → 选择文件 → 创建草稿”、错误类型不会关闭窗口、磁盘下拉框优先关闭以及返回取消图片选择。

## 本次旧配置清理

根据用户要求，对 `artifacts/PTBox-win-x64/Config/config.json` 做一次性清理。逐项比较原始默认配置，7 个入口均未修改，已移除；其他设置保留。原文件完整备份为 `Config/config.json.before-preset-cleanup-20260922-121935.bak`。常规加载和发布仍保留个人应用，不会在启动时按 ID 删除用户配置。

图标实现参考：[SHDefExtractIconW](https://learn.microsoft.com/zh-cn/windows/win32/api/shlobj_core/nf-shlobj_core-shdefextracticonw)、[AssocQueryStringW](https://learn.microsoft.com/en-us/windows/win32/api/shlwapi/nf-shlwapi-assocquerystringw)。使用 Windows 图标提取和协议关联查询，生成冻结的 WPF 位图并释放原生图标句柄。

## 环境限制及待实机验收

通过桌面自动化工具执行端到端按键验证时，工具返回 PTBox 标题却错误关联到其他 launcher.exe 路径，刷新选择后仍报 `window id ... no longer belongs to ...`。因此停止使用该窗口句柄，改用工程内 WPF 集成测试。代码测试覆盖并不代表真实遥控器和电视已验收。

请在客厅电脑完成以下一次性验收：

1. 选择真实 Playnite / Moonlight 路径，启动和退出各一次；再在已运行时切回窗口，确认无重复实例。
2. 确认 Bilibili / Edge / Steam URI 的协议注册和开启结果；浏览器用 Ctrl+Alt+H 回来。
3. 飞鼠逐个测试方向、OK、返回和鼠标点击；方向键和鼠标交替时焦点不跳动。
4. 桌面最小化后，分别测试热键和再次运行 EXE 唤回；若热键被占用使用第二种方式。
5. 在 Windows 显示设置中分别检查所用 DPI 档位；验证任务栏覆盖、多显示器主屏切换和 HDMI 重连。
6. 从设置启用自动启动，注销 / 登录验收，再关闭检查 Run 项已删除。此开发验证没有修改登录启动设置。
7. 有意进行系统关机 / 重启时才确认电源操作，验证返回键和默认取消。

测试没有下载安装 Playnite / Moonlight，也未修改 Windows Shell、关键系统文件或任何安全设置。
