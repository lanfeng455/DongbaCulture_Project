# Story Mode opening prototype

This is a small, optional opening-story layer on top of the existing ecology loop. It does not replace Sandbox Mode or add a second ecology counter.

## Try it

- Press **F5** to switch between Story Mode and Sandbox Mode.
- Press **Esc** to quit the standalone build or stop Play Mode in the Unity Editor.
- While Story Mode is on, the father marker follows behind the player.
- Each existing damage event lowers the current ecology balance. At `StoryCaptureBalanceThreshold` (default `-10`), the father leaves to retrieve the lost axe; after a short beat he disappears and the `FatherCaptured` event fires.
- Other story code can subscribe to `ObliqueForest.FatherCaptured` to begin the rescue section. No rescue puzzle or scene transition is included here.

## Content and limits

- The father sprite is intentionally a clearly tagged temporary marker. Assign a verified sprite to `StoryFatherGlyph` after the user supplies it; the marker is not a Dongba pictograph.
- The lost-axe departure and capture are the game's adaptation of the source story. The current trigger threshold, follow behavior, timed capture beat, and event hook are prototype mechanics.
- Story Mode starts disabled so the existing sandbox loop is unchanged. It can also be enabled in the component Inspector or with `--story-mode-preview` for a preview build.
- The father uses the existing forest walk map and clearance checks. He does not cross the river or tree obstacles intentionally; if no route can be found, he waits and retries.

2026-11-07 当前演示范围更新：高勒趣东行穿过森林，文字退为纸面，淡出后结束当前段落。暂不进入临时卷轴练习场，也不自动加载后续场景；R重开、F5切沙盒、Esc退出仍可用。

2026-11-07 找刀对峙：父亲到东岸刀旁后停下，镜头取父亲与署的中点。按Enter逐句推进：署问‘这是你的刀吗？’→父亲愣住、无法回答→署指责‘就是你杀了我的野猪！’。以上为按用户要求撰写的游戏改编台词，非原经文引文；沿用当前英文字幕。最后一句确认后，署短暂转为暗红表现怒意，再沿原动画抓走父亲；刀在对峙时保留，抓走时隐藏。
