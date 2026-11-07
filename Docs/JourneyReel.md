# 森林进入卷轴与净秽关卡（第一版）

当前接法：森林东行时字形逐渐退去，用户提供的连续卷轴逐渐显现；按D/右方向键继续走，移动距离驱动画面展开，停步则卷轴停止。经过前3幅后轻微推近并用纸色遮罩过渡，加载组员的 Beat_2_Cleansing_Impurity。关卡的 PuzzleDirectorBase.PuzzleCompleted 事件触发后返回同一卷轴位置，继续向右，不重复触发已完成的入口。

这是用户确认的临时演示对应关系，不表示前3幅原图在文化内容上就是此谜题。其余入口尚未配置，不自行编造图意或关卡关系。

素材：Bianca_Dongba_Trimmed_Reel/Dongba_Trimmed_Reel/Assets/DongbaReel/Resources/DongbaReel 的26张透明PNG与timeline.json，原样复制到 Assets/DongbaPrototype/Resources/JourneyReel。保留长宽比、透明边缘、原顺序和原图，不重画图中内容。原顺序为提供者的临时顺序，未经独立核定；出版使用授权仍待核实。完整源文件仍在用户提供目录。仅同时显示相邻3张，切换时加载邻图，避免整张超宽纹理。

修改入口：ObliqueForest.JourneyReel.cs 中 JourneyReelStart=30、JourneyReelPixelsPerUnit=180；当前按第四幅的起点触发首个入口。ReelPortalHost负责纸色淡入淡出、异步加载、暂停原场景和通关后恢复。保留森林中的对象状态和脚底坐标，不重置生态、人物或卷轴进度。场景字幕单例在切换时交接，关卡使用它自己的字幕。

构建菜单已将净秽场景一并纳入。--reel-preview 可直接从东行入口附近开始手动测试；正常Story Mode仍从火塘开始。第一版仅适配Windows；移动端/XR控制未在本轮接入。

验证中的关卡完成使用真正的PuzzleCompleted事件进行接线测试，并不等于人工解开了全部谜题。组员的关卡规则和台词保持原样。该关卡已有ScrollDebugProbe缺失脚本警告，需组员后续核对调试组件。

实机结果：Windows x64构建成功，7项进出检查通过；26张原图SHA256逐项一致，GUID/meta检查通过。正常速度预览已更新为约15秒，30fps录制，仅验证视觉衔接，不作为游戏性能测量。运行使用本机6000.3.0b1兼容工程，团队6000.3.12f1未安装，尚未在该版本验证。预览在Verification/journey-reel/preview.mp4，构建更新Builds/Current，未生成独立留档包。

2026-11-07 视觉与时长修正：首次入口步行约减半（约7秒）；卷轴高度由视窗83%改为105%，等比覆盖边缘。独立纸纹底层随卷轴一起渐入暖褐色，透明缺口不再露出亮白纸底。主角仅在卷轴段渐变为暗朱褐，增加细浅边并等比放大22%，退出或重开恢复原材质和尺寸。录屏使用实机原生1280×720尺寸渲染后等比导出960×540，避免不同画面裁切。实机7项复测通过。
