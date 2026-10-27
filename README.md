# DONGBA: THE LIVING SCRIPT

团队协作用的 Unity 工程。当前森林以斜俯视地面和直立东巴字形呈现，包含移动、跳跃、砍树、补种、照料、动物成长繁殖及生态反馈。

## 打开项目

1. 用 GitHub Desktop 克隆仓库，再在 Unity Hub 添加仓库根目录，不是 Assets 或 exe 文件夹。
2. 使用团队指定的 **Unity 6000.3.12f1**，安装 Windows Build Support。
3. 打开 `Assets/DongbaPrototype/ObliqueForest.unity`，或菜单 `Dongba Prototype > Open Current Forest`，再点击 Play。

WASD / 方向键移动，Space 跳跃，J / Enter 挥斧，E 照料、喂养或补种，R 恢复角色位置和血量。

菜单 `Dongba Prototype > Build Current Forest (Windows x64)` 输出到 `Builds/Windows/ForestPrototype/ForestPrototype.exe`，不要求队友具有固定盘符。旧横版和旧构建菜单在 Legacy 下，不代表当前版本。

## 目录

| 内容 | 位置 | 是否提交 |
| --- | --- | --- |
| 当前原型脚本、场景、运行素材和 .meta | Assets/DongbaPrototype | 是 |
| 团队其他场景、插件 | Assets 下已有目录 | 是，本次未覆盖 |
| 项目依赖和设置 | Packages、ProjectSettings | 是，团队协商修改 |
| 协作说明、开发记录、叙事文本 | Docs | 是 |
| 工具脚本 | Tools | 是，不含生成结果 |
| exe、Data、验证截图和视频 | Builds、Verification | 否，完整运行包另传网盘 |
| 原书扫描、旧备份、大型 PPT 和视频 | 网盘资料库 | 否，在文档中记录来源 |
| Library、Logs、UserSettings | Unity 自动生成 | 否 |

运行素材保留现有 Resources 路径。字义和授权仍在核实，游戏图形不等同于权威文化释义。

详见 [协作流程](Docs/Collaboration.md) 和 [当前接入状态](Docs/Development/Current.md)。
