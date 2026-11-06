# 共用字幕

森林剧情现在复用组员的 DialogueDisplay（TextMesh Pro 字幕），不是重做另一套。开场按 E / Enter：正在打字时先显示完整一句，再按进入下一句。找刀、等待等剧情也使用同一组件。原有 HUD、交互提示不变。

打开项目：E:/DongbaCulture_Project，使用团队指定 Unity 6000.3.12f1。打开森林后运行 Story Mode；UseSharedSubtitles 可退回旧显示。森林字幕打字速度默认 48 字符/秒，其他场景组件原默认仍为 18。

字体已放到 Assets/Resources/Fonts/NotoSerifCJKsc-Black SDF.asset，沿用仓库已有 .meta，GUID 没变。131 MB 字体不提交 Git；其他成员需先关闭 Unity，放入同路径，保留仓库的 .meta 再打开工程。不要重新生成 meta，也不要强行提交大文件。

编辑组员的字幕库：修改 Assets/Dialogues 中相应 CSV，Unity Project 里右键 Dongba/同步为 DialogueLibrary，将生成/更新的库资产赋给 DialogueDisplay。PlayRange("0/2") 播第 0 至第 2 句，PlayAll() 播全部，HideDialogue() 隐藏。森林当前已有剧情仍保留在原代码，通过 PlayLine(speaker, content) 接到共用组件；没有将谜题 CSV 当成森林开场，也没有改变组员原台词。

支持中文、富文本着色、逐字显示；字幕高度随内容调整。重开、切换 Sandbox 和向东走入空白纸面时清除字幕。没有完成盒景关卡与森林之间的正式场景衔接。

本次先合并 origin/main 的组员提交，再加入森林接线；不自动 push。原备份标签 backup/pre-import-20261106 保留。已有兼容验证工程使用本机 6000.3.0b1 构建，不能代替尚未安装的团队 6000.3.12f1 验证；组员其余关卡并未全部验收。

验证：Windows 实机字幕检查 12 项通过；父亲被抓后到卷轴的原有流程检查全部通过。中文/富文本、补全/下一句、重开、模式切换、空白纸面清理已运行检查，截图在 Verification/shared-subtitles（本地验证资料，不提交）。构建更新到 Builds/Current，不新增版本压缩包。
