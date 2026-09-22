# 配置、资源与数据维护

本文描述当前配置格式 1。1.1.0 已实现安装版迁移与未来格式写回保护，详见 [发布更新操作](release-operations.md)。

## 1. 数据目录选择

存在有效 `ptbox.install` 标记时使用 `%LOCALAPPDATA%\PTBox`，首次启动迁移旧目录配置；`ptbox.portable` 优先指定便携模式。便携模式先尝试程序目录，不可写时回退到个人目录。设置页底部显示实际配置路径。

| 位置 | 内容 |
| --- | --- |
| `<数据根>/Config/config.json` | 应用列表与偏好 |
| `<数据根>/Config/config.json.<时间>.broken` | 配置损坏时尝试保留的原文件 |
| `<数据根>/Logs/launcher.log` | 日志；达到约 2 MB 后轮换，保留一份旧日志 |
| `<数据根>/Cache/Icons/` | 网站图标缓存，可重新生成 |
| 自定义图片路径 | 外部 EXE 图标、壁纸等；选择文件只保存路径，不会自动复制图片 |

安装器默认安装到 `%LOCALAPPDATA%\Programs\PTBox`，1.1.0 将个人数据与程序分开存放。迁移备份在 `Backups`，相对图片在 `Resources`，完成标记在 `Migrations`；`Updates` 存放更新偏好、下载和日志。迁移失败保留原配置并提示。

## 2. 根配置字段

| 字段 | 默认值 | 说明 |
| --- | --- | --- |
| `version` | `1` | 配置格式版本，只接受 1；不是程序版本号 |
| `apps` | `[]` | 按首页显示顺序保存的应用数组，0–99 项 |
| `lastSelectedTile` | `$add-app` | 最后选择的入口；失效时由首页恢复有效选择 |
| `autoStart` | `false` | 设置偏好；实际自动启动状态以注册表为准 |
| `startFullscreen` | `true` | 下次启动是否全屏；F11 为临时切换 |
| `theme` | `Midnight` | `Midnight` 午夜蓝或 `Charcoal` 深炭灰；其他值回退午夜蓝 |
| `background` | `""` | 本地图片路径；空值或读取失败回退内置背景 |

默认模板位于 `src/PTBox.Launcher/Config/config.json`。模板和 `DefaultConfig()` 保持一致，测试验证两者序列化结果。

## 3. 应用字段

| 字段 | 默认 / 可选值 | 说明 |
| --- | --- | --- |
| `id` | 新建时随机 GUID 字符串 | 非空、忽略大小写唯一，不能以 `$` 开头；`$` 留给系统入口 |
| `name` | `新应用` | 不允许空白名称 |
| `subtitle` | 空字符串 | 可选副标题 |
| `icon` | 空字符串 | 自定义图片；留空自动读取原图标 |
| `category` | `auto` | `auto/games/streaming/media/apps`；非法值回退 `auto` |
| `type` | `exe` | `exe/url/uri`，非法值校验失败 |
| `path` | 空字符串 | EXE 路径、HTTP(S) 地址或应用协议地址 |
| `arguments` | 空字符串 | EXE 启动参数；URL/URI 使用 `path` 本身 |
| `launchBehavior` | `waitForExit` | 或 `fireAndForget`；非 EXE 会规范化为 `fireAndForget` |
| `reuseExisting` | `true` | EXE 是否按完整路径复用现有进程 |
| `accent` | `#234B58` | 保留的颜色配置，不应理解为可以恢复旧版品牌插画卡片 |

`auto` 分类根据现有类别识别逻辑推导，不进行联网分类。数组顺序就是首页应用顺序。允许 EXE 路径暂时留空，例如先添加未安装的 Moonlight 模板；尝试启动时再提示修复路径。

## 4. 示例

```json
{
  "version": 1,
  "apps": [
    {
      "id": "living-room-app",
      "name": "我的程序",
      "subtitle": "本地应用",
      "icon": "",
      "category": "apps",
      "type": "exe",
      "path": "D:\\Apps\\MyApp\\MyApp.exe",
      "arguments": "",
      "launchBehavior": "waitForExit",
      "reuseExisting": true,
      "accent": "#234B58"
    }
  ],
  "lastSelectedTile": "living-room-app",
  "autoStart": false,
  "startFullscreen": true,
  "theme": "Midnight",
  "background": ""
}
```

示例路径需要换成真实文件。Steam URI 可用 `steam://open/bigpicture`；网页类型只接受 HTTP / HTTPS。`file/javascript/data/vbscript/shell` 协议被拒绝。

1.0.1 起，设置保存会拒绝空网页/协议地址，注明应用名称并定位对应地址框；EXE 路径仍可暂时为空。网页协议开头的全角冒号/斜杠会转为半角，地址两端空白和常见不可见标记会去除；路径、参数、片段内容不作这种转换。无效地址不会写入配置，启动时使用同一校验规则。

## 5. 路径和图标

EXE 支持环境变量、绝对路径、相对数据根目录的路径，以及裸程序名。裸名称通过 App Paths、数据目录、系统目录、Windows 目录和 PATH 查找；Edge 还有常见安装目录回退。启动文件必须为 `.exe`，工作目录使用该 EXE 的父目录。

自定义图标和壁纸的相对路径以数据根为基准。文件选择器支持 EXE 或 PNG/JPG/JPEG/BMP/ICO，验证类型和文件存在性。它不会安装程序，也不保证任意文件内容都是有效 EXE / 图片；图像解码失败有显示回退。

本地原图标由 Windows 提取；网页图标按源站缓存，缓存有效期 7 天，过期联网失败仍可使用已有缓存。获取网站图标不发送用户配置网址的路径、查询参数或凭据；请求可能跟随网站提供的图标链接和有限次数的 HTTP(S) 重定向。

## 6. 保存、恢复和迁移

设置编辑使用深拷贝草稿。保存时校验并写入同目录 `.tmp`，再替换正式 JSON；失败时清理临时文件。读取失败尝试备份 `.broken` 后恢复空应用默认配置；若备份或写入也失败，记录日志并在内存中使用默认值，不能保证每次失败都产生备份。

手工修改前先退出 PTBox。恢复时备份当前配置，再从已知正常副本恢复 `Config/config.json`，确保格式版本受支持。1.1.0 遇到未知格式会显示警告并进入只读保护，保持原文件不变；更早版本没有这项保护，不能盲目降级读取新版配置。

移动便携版时复制完整目录。迁移到安装版时退出两边程序，复制 `Config` 和相对路径引用的图片，保持结构；绝对路径资源仍依赖原位置。不要复制别人的日志和缓存作为默认发布数据。

安装器使用 `onlyifdoesntexist` 保留安装目录已有配置、`uninsneveruninstall` 在卸载后保留它。1.1.0 安装版首次启动再迁移至个人数据目录，带备份和幂等标记；不会扫描其他便携目录。详见 [发布更新操作](release-operations.md)。
