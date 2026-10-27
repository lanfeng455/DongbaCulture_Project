# 我们怎么一起改

以后只在各自克隆的 DongbaCulture_Project 里改代码和正式素材。不要在旧工程改完再复制，也不要互发整个 Unity 工程覆盖。

## GitHub Desktop

1. 开工先 Fetch origin，有更新再 Pull；先处理好未提交修改，不强制覆盖。
2. 从团队确认的共同基线创建分支，例如 feature/animal-behavior、feature/story-opening、art/plant-assets。
3. 改一件事，在 Unity 运行检查。检查 Changes，不应出现 Library、exe 或录屏帧图。
4. 填具体说明后 Commit，再 Push；第一次上传分支显示 Publish branch。
5. 发起 Pull Request，由另一位组员检查后合并。

本次接入分支是 `chore/forest-collaboration-20261027`，从已有 `import/forest-experience` 创建。本次上传使用此独立分支，不直接合并 main。队友 Fetch 后切换到该分支即可查看新版森林；团队确认后再合并。

## 避免冲突

- 同一个场景、Prefab 和 ProjectSettings 尽量同时只由一个人修改，开工前群里说明。
- 程序优先分脚本修改。当前地图由脚本生成，布局在 ObliqueForest.Layout.cs，动物行为在 ObliqueForest.AnimalEcology.cs，不是在 Hierarchy 中摆满静态对象。
- 素材连同 .meta 一起提交；移动和改名优先在 Unity 中完成，不删除 .meta 重建 GUID。
- 不直接用“全部保留我的”解决场景冲突，避免覆盖队友内容。
- Unity、插件和 Package 升级先统一，不各自用不同版本保存项目。
- ObliqueForest.cs 承担较多系统，后续可按模块拆分；本次只整理协作入口，不同时重构玩法。

运行素材放 Assets/DongbaPrototype/Resources，避免破坏 Resources.Load 路径。叙事 Markdown 可在 Docs/Narrative 下新增；原书、素材原件和大文件放网盘，文档记录文件名、页码、来源状态和用途。不要把全部原始字典拖进 Assets。

## 这台电脑的分工

- `E:\DongbaCulture_Project`：团队协作工程，以后代码和正式素材在此继续。
- `E:\dongba`：旧开发工程及研究资料区，不再作为另一份同步开发主线。ArtPreview、Narrative、Presentation、文字字形素材暂不搬动，避免历史链接失效。
- `F:\DongbaBuilds`：已有运行版和验证证据，当前可运行 Ecology-14。
- `F:\DongbaBackups`：清理前备份，不是 Git 仓库。

盘符只是本机位置，队友可克隆到任意目录。不要把整个备份目录提交 Git。

