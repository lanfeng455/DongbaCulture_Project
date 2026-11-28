using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace DongbaPrototype {
// One physical press belongs to either dialogue or gameplay, never both.
public static class DialogueAdvanceInput {
    static readonly List<KeyControl> heldDialogueKeys = new List<KeyControl>(128);
    static int consumedFrame = -1;
    static bool IsUsable(KeyControl key) => key != null && key.device != null && key.device.added && key.device.enabled;

    public static bool DialogueActive => DialogueDisplay.Instance != null && DialogueDisplay.Instance.AcceptsManualAdvance;
    public static bool GameplayInputBlocked {
        get {
            for (int i = heldDialogueKeys.Count - 1; i >= 0; --i)
                if (!IsUsable(heldDialogueKeys[i]) || !heldDialogueKeys[i].isPressed) heldDialogueKeys.RemoveAt(i);
            return MRPuzzleInput.MenuOpen || MRPuzzleInput.DialogueHold || DialogueActive || consumedFrame == Time.frameCount || heldDialogueKeys.Count > 0;
        }
    }

    internal static bool TryConsume(DialogueDisplay owner, int lineShownFrame) {
        if (MRPuzzleInput.MenuOpen || owner == null || !owner.AcceptsManualAdvance || DongbaInterface.GameplayBlocked ||
            Time.frameCount <= lineShownFrame || consumedFrame == Time.frameCount) return false;
        if(MRPuzzleInput.Active&&MRPuzzleInput.JumpPressed){consumedFrame=Time.frameCount;MRPuzzleInput.LatchDialogue();return true;}
        var keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.added || !keyboard.enabled) return false;
        // Keyboard.anyKey only has one aggregate edge: scan keys so a new press also
        // works while another key is held. Repeating a held key never advances again.
        bool pressed = false;
        foreach (var key in keyboard.allKeys) if (IsUsable(key) && key.wasPressedThisFrame) { pressed = true; break; }
        if (!pressed) return false;
        consumedFrame = Time.frameCount;
        foreach (var key in keyboard.allKeys)
            if (IsUsable(key) && key.isPressed && !heldDialogueKeys.Contains(key)) heldDialogueKeys.Add(key);
        return true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { consumedFrame = -1; heldDialogueKeys.Clear(); }
}
}
