using UnityEngine;
namespace DongbaPrototype {
// Input runs before controllers; presentation runs after their pose/camera animations.
[DefaultExecutionOrder(20000)]
public sealed class MRPuzzleVisualSync:MonoBehaviour {
    public MRPuzzleStage Stage;
    void LateUpdate(){if(Stage!=null)Stage.SynchronizeVisuals();}
}}
