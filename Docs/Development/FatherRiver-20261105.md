# 父亲过河跳跃修复（2026-11-05）

原因：之前跨河根节点插值在MoveStoryFatherToward调用中推进，字形抬升只在UpdateStoryFatherFollow的普通跟随分支执行。找刀状态为CaptureBeat，不走跟随分支；狩猎AI也可能提前返回。所以根节点虽跨河，画面上没有腾空。此外AI换目标或跟随距离过近停止调用移动时，跳跃进度也可能停住。

修复：新增ObliqueForest.FatherRiver.cs，UpdateStoryMode每帧独立推进已有跳跃，LateUpdate统一更新父亲字形的腾空高度/待机和遮挡排序。MoveStoryFatherToward只决定起跳、落点或步行，不重复推进空中进度。起跳后即使目标变化仍先完成原落点。步行逐0.1单位采样检查河面与现有障碍，不能用地面移动跨河。保持原0.85秒跳跃、1.45单位最高抬升与岸边落点。

验证：复用既有6000.3.0b1兼容环境构建成功，固定输出Builds/Current，未复制新工程或新版本包。Verification/father-river/checks.txt七项True：西向东、东向西、找刀、空中中断AI四种情况均起跳/腾空/落地，峰值1.449；地面路径不能穿河；重开清除跳跃。此专项测试使用清出的落点隔离障碍，验证跳跃与呈现独立于调用者；另跑现有story-journey实际剧情回归，父亲自主狩猎、跟随跨河、猎猪、返西岸、找刀、署抓走离屏与重开均通过。正常镜头apex-2.png确认找刀时字形抬起。短录屏father-river-preview.mp4连续展示四种跳跃（约4.3秒、30fps）。相关日志无Exception/Error匹配。团队6000.3.12f1仍未验证。

沿用已有生态、角色尺寸、地图、日月吊牌和空纸转场。此轮源码仍依赖未提交的故事工作树，未把前置内容混进commit，未push。
