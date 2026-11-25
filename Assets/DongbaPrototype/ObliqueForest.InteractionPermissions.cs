namespace DongbaPrototype { public partial class ObliqueForest {
    // Story permissions follow actual journey state, never the ecological balance.
    // Sandbox retains both interactions, including after a restart or mode change.
    public bool ForestCareAvailable => !StoryModeEnabled || journeyStage==JourneyStage.RestorationPlay || journeyStage==JourneyStage.RestorationComplete;
    public bool ForestDamageAvailable => !StoryModeEnabled || journeyStage==JourneyStage.Hunting || journeyStage==JourneyStage.Boar;
    bool ForestTargetAvailable(Being target) => target!=null && (ForestCareAvailable || (ForestDamageAvailable && target.health>0));
    string XRForestActionGuide => (ForestDamageAvailable?DongbaInterface.Choose("Right Trigger   Axe","右扳机  挥斧"):DongbaInterface.Choose("Axe put away","此阶段收起斧头"))
        +"\n"+(ForestCareAvailable?DongbaInterface.Choose("X   Care / Feed / Replant","X  培育／喂养／补种"):DongbaInterface.Choose("Care unlocks on your return","归来后解锁培育修复"));
}}
