# 电视盒子风格首页 · 实现记录

设计参考：`tv-home-v1.png`。实现仍为原生 WPF，没有引入浏览器或第三方 UI 框架。

- 沉浸式山景背景、顶部首页/游戏/串流/影视/应用分类、主推荐区、横排应用、动态快捷入口、底部设置/桌面/电源。
- 分类真实筛选配置中的应用；“应用”显示全部，游戏/串流/影视按 category 筛选。原有 JSON 缺少 category 时按已知 ID 自动分类，新自定义应用默认归入应用与工具。设置页可调整分类。
- 推荐区按当前应用排序取前三项，圆点数量随应用数量变化，少于两个应用时不显示圆点。应用名称、封面与启动目标同步变化；没有自动轮播。
- 应用图标优先使用自定义图片，否则读取程序或网站原图标；卡片和快捷入口使用统一深色底，移除应用插画封面。支持名称修改和排序。新默认配置为空；Steam、Moonlight 作为可选内置模板，其他程序和网页由用户添加。
- 横排超出屏幕时可左右键或鼠标滚轮浏览，当前卡片自动滚动至可见范围。
- 顶栏、推荐按钮、推荐页、应用、快捷入口、底栏都加入显式行列导航；鼠标悬停不会抢走键盘选择。
- 电源菜单包含返回、关机、重启和退出，默认返回；关机/重启仍需二次确认。
- 保留原有配置、自动启动、窗口模式、外部程序退出返回、热键、单实例和桌面安全后备。
- 顶栏显示本机图标、设置按钮、本地 P 标识与实时时钟，没有虚构 Wi-Fi 连接或用户账户。
- 设置页沿用山景、深色半透明面板、白色焦点与胶囊按钮，分为「应用管理」和「首页与系统」；输入框、下拉菜单、开关和滚动条使用同套自定义样式。
- 应用管理提供原图标深色应用列表和独立编辑区；系统页提供开关、主题选择与壁纸预览。切换页面保留未保存草稿，取消仍不会修改原配置。
- 首页与设置页共用四瓣描边方向键矢量图标，对齐概念图，不再依赖字体中的十字字符。
- 设置列表可向右进入编辑区；确认键展开下拉菜单、切换开关；下拉菜单打开时返回键优先收起菜单。
- 应用栏末尾保留“添加应用”卡片。删除应用后移除对应推荐和快捷入口，空分类隐藏；当前分类被删空时回首页。快捷区取前两个不同分类的首个应用并保留桌面，完全空白时显示添加引导。
- 添加选择器提供 Steam、Moonlight、本地程序、网页四种入口，检测常见安装路径并标记未安装状态；模板不自动加入首页，也不下载安装软件。
- 添加应用、程序/图标/壁纸选择、确认提示和电源菜单共用 `DialogStyles.xaml`，统一圆角山景面板、按钮、白色焦点和方向键提示。文件选择使用原生 WPF 深色窗口。

## 素材

使用内置 image_gen 生成两个本地 PNG 素材，已嵌入 Launcher 程序，不依赖网络或生成工具安装目录：

- `src/PTBox.Launcher/Assets/Artwork/home-landscape.png`：无文字的山湖与暖光玻璃小屋。
- `src/PTBox.Launcher/Assets/Artwork/app-scenes.png`：3×3 应用封面图集，由 WPF ImageBrush.Viewbox 选区显示。

系统操作图标使用系统字体；应用图标从程序或网站读取。默认背景素材不含界面文字，4K 缩放时卡片边框和文字仍由 WPF 渲染。生成的栅格素材仅用于山景和推荐背景，未声称是原生 4K 照片。

## 素材生成最终提示词

### Background (built-in image_gen; reference: tv-home-v1.png)

Create a clean background artwork asset for the PTBox TV launcher using the provided UI design as the visual reference. Recreate ONLY the photorealistic mountain lake scene behind the interface: dramatic alpine mountains at blue hour, sunset horizon, drifting low fog, reflections on a tranquil lake, elegant small warmly lit modern glass cabin on the FAR RIGHT. Landscape 16:9 2560x1440 or larger. Full bleed clean scenic photograph, NO text, NO UI, NO cards, NO icons, NO logos, NO borders. The left half is naturally darker forest/mountains and negative space so a white headline can be overlaid by real application code. Right half with cabin is warmly inviting. Bottom darker water and forest. Very close to the mood and composition of the supplied design, luxury cinematic television wallpaper, realistic fine detail.

### Card atlas (built-in image_gen)

Create one precisely aligned 3-column by 3-row image atlas for a TV launcher application. Nine equally sized landscape panels, no gutters, no borders, no rounded corners, no text, no lettering, no symbols, no logos, no UI. Overall image square 2048x2048 or 2304x2304, each panel fills exactly one third width and height. Each panel is a cinematic artwork suitable behind an app icon. Row1 col1: lone cloaked adventurer looking across golden misty fantasy mountains. Row1 col2: sleek gaming monitor setup in dark blue room with giant moon seen through window, blue streaming atmosphere. Row1 col3: vibrant pastel pink and turquoise anime city street under cherry blossoms at twilight. Row2 col1: elegant deep navy sci-fi city with blue-lit architectural forms at night. Row2 col2: warm amber vintage film projector and film reels in luxurious dark screening room. Row2 col3: beautiful turquoise blue ocean with rocky island beneath sky. Row3 col1: golden autumn forest sun shafts with fern floor. Row3 col2: cozy movie-night living room in warm low light, sofa and cinematic light with no people. Row3 col3: abstract rich blue flowing silky folded ribbon sculpture on dark navy background, premium operating system wallpaper style. All nine panels completely distinct and separated only by exact grid boundaries. Excellent detailed imagery, consistent premium cinematic quality, low contrast in center to allow real icons and typography to be overlaid later.
