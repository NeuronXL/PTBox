图标可放在此目录，配置 icon 为相对于数据目录的路径（例如 Assets/moonlight.png），也支持绝对路径。
支持 PNG、JPEG、BMP、ICO。自定义图片优先；未指定或读取失败时提取本地程序 / 协议的原图标，网页获取网站图标，仍失败时显示通用字体图标。
应用卡片统一为深色底。默认山景和推荐背景图集保存在 Artwork/，由内置 image_gen 生成并嵌入程序集，离线可用。网页图标在 Cache/Icons/ 缓存，首次读取需要联网。素材提示词记录在 docs/design/ui-implementation-v1.md。
