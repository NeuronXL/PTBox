# 工程转移说明

清理日期：2026-09-22。源码版本：1.1.1。

## 已清理

- 所有项目的 bin / obj。
- 本地 .NET SDK、SDK 压缩包和 CLI 缓存。
- Inno Setup 下载包、解压工具、日志及安装暂存目录。
- 便携构建输出、临时安装测试目录和开发过程中的重复发行构建。
- artifacts/installer 下与正式发行目录字节相同的 1.1.0 / 1.1.1 安装包及其重复校验文件。

清理前约 4.75 GiB，清理后约 336 MiB；共释放约 4.42 GiB。145 个保留文件经过逐文件 SHA256 校验，内容未变。该统计不包括此后新增的本说明。

## 已保留

- 源码、解决方案、构建/测试/发布脚本、默认配置、图片图标资源、设计和开发文档。
- artifacts/releases/1.1.0 与 artifacts/releases/1.1.1：正式签名发行文件，供发布及跨版本升级验收使用。
- artifacts/installer 中无重复副本的 1.0.0 / 1.0.1 历史安装包。
- artifacts/verification：文档引用的验证截图。
- .tools/release-secrets/release.private.bin：本机加密的发布私钥。
- .tools/transfer-backup/portable-config：原便携版的个人配置及旧配置备份，复制后已校验哈希。

当前 1.1.1 安装包保留位置：artifacts/releases/1.1.1/PTBox-Setup-1.1.1-win-x64.exe。文档里旧的 artifacts/installer 输出路径会在重新打包时生成。

没有修改 D:\PTBox 或 Windows 用户数据目录。正在使用的安装版配置位于 %LOCALAPPDATA%\PTBox，不属于本工程压缩包；如还需要迁移实际应用设置，须另行备份该目录。

## 在新电脑恢复开发

1. 解压完整工程，包含隐藏的 .tools 目录。
2. 安装 Windows x64 .NET 8 SDK。global.json 允许 8.0 的后续功能版本；之前验证使用 8.0.425。仅装运行时不足以编译。
3. 在工程根目录运行 scripts/test.ps1。bin、obj 和 .tools/cli 会重新生成。
4. 打包时运行 scripts/package.ps1。Inno Setup 工具会由脚本重新准备，首次需要联网。

已有版本的签名发行目录禁止覆盖；新发布请使用新版本号并准备对应 docs/releases/<版本>.md。

## 跨电脑发布密钥

保留下来的 release.private.bin 使用 Windows DPAPI 加密，绑定当前 Windows 账号环境。仅复制工程到另一台电脑不能保证解密，不能据此删除或重装原电脑。

更换发布电脑之前，需要在原电脑使用 ReleaseTool export-key 导出密钥备份，再在新电脑用 import-key 导入；详见 docs/release-operations.md 的“本地发布与密钥”。SDK 已作为可重建工具清理，运行 ReleaseTool 前需有可用的 .NET 8 SDK。

export-key 会产生明文私钥文件，应单独安全保存，不能上传到公开仓库。本次清理没有导出明文私钥，也没有替换公钥或生成新密钥。
