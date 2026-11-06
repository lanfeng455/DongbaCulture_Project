using UnityEngine;

// 瞄准系统眼中的"可瞄准目标"契约。
// 现有两类实现：
//   DongbaGlyph   —— 带字形的可组合文字，发射后走融合流程（含组词提示）
//   GlyphItemSlot —— 无字形的道具放置槽，发射后走放置/退回流程
// 瞄准圈、箭头、注册表都只认这个接口，以后新增目标类型无需改动瞄准系统本身。
public interface IGlyphAimTarget
{
    // 目标名，仅用于瞄准圈对象命名与调试定位
    string DisplayName { get; }

    // 是否可被瞄准（未启用、已集齐等情况下应为 false）
    bool IsAimable { get; }

    // 瞄准圈半径（米）
    float RingRadiusInMeters { get; }

    // 世界坐标：注册表按它找最近目标，瞄准圈和箭头按它定位
    Vector3 WorldPosition { get; }
}
