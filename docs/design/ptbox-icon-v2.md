# PTBox 图标 v2 · 简洁版

![简洁版图标](ptbox-icon-v2.png)

使用内置 image_gen 编辑 v1：保留 P 与播放键，去掉立体、玻璃与发光效果。已采用为程序图标，源图保存在 `src/PTBox.Launcher/Assets/Brand/ptbox-icon.png`，多尺寸 Windows 图标为同目录下的 `ptbox.ico`，供 EXE 和 WPF 窗口共用。

运行 `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build-icon.ps1` 可以从已选 PNG 重新生成 ICO，包含 16、20、24、32、40、48、64、128、256 像素尺寸并保留透明通道。

## 最终提示词

Use case: logo-brand / style-transfer.
Edit the supplied PTBox app icon to be much simpler and completely flat. Preserve the basic identity: a bold capital P with a small right-pointing play triangle inside its upper counter, centered on a rounded-square navy tile.
Replace all 3D material, bevels, highlights, texture, glow, gradients and shadows with perfectly uniform solid fills and crisp clean geometric edges. Exactly three colors: deep navy #14253D tile, warm white #F7F6F2 P, muted amber #E8AD52 play triangle. The P should have a simple strong geometric silhouette, a softly rounded upper bowl and straight stem, with generous negative space. Keep the play triangle small and centered inside the counter. No outlines, no extruded edges, no decorative details.
Composition: one centered icon, frontal and symmetrical tile, ample even margins, logo about 65 percent of tile height. Transparent alpha outside the rounded square. Square canvas. Designed to be clearly recognizable at 32 pixels.
No words, no caption, no additional marks, no presentation board or mockups. Output just the clean finished flat icon.
