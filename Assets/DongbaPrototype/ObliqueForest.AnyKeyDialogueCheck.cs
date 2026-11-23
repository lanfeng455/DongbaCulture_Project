using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace DongbaPrototype { public partial class ObliqueForest {
    // Explicit opt-in: exercises real Input System events and both dialogue owners.
    IEnumerator CheckAnyKeyDialogue() {
        const string folder = "Verification/dialogue-any-key";
        Directory.CreateDirectory(folder);
        var results = new List<string>(); int failures = 0;
        Action<string, bool> check = (name, ok) => { results.Add((ok ? "PASS " : "FAIL ") + name); if (!ok) failures++; };
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        var keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
        yield return new WaitForSeconds(1);
        DongbaInterface.Instance?.Close(false);
        SetStoryModeEnabled(true); RestartRun(); SkipOpeningForVerification();
        Key[] inputs = { Key.J, Key.D, Key.Space, Key.E, Key.H, Key.Escape, Key.F8, Key.R, Key.Tab };
        string[] lines = new string[inputs.Length + 1];
        for (int i = 0; i < lines.Length; i++) lines[i] = "Gaolequ: Any-key input verification " + i;
        BeginManualStoryDialogue(lines);
        yield return null; yield return null;
        var feet = Feet; var locale = DongbaLanguage.Current;
        for (int i = 0; i < inputs.Length; i++) {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(inputs[i]));
            yield return null; yield return null;
            check(inputs[i] + " advances one forest line", manualStoryIndex == i + 1);
            check(inputs[i] + " does not move, jump, attack or open UI", Vector3.Distance(Feet, feet) < .01f && jumpAge < 0 && attackAge < 0 && !DongbaInterface.GameplayBlocked && DongbaLanguage.Current == locale);
            for (int f = 0; f < 6; f++) yield return null;
            check(inputs[i] + " held does not repeat", manualStoryIndex == i + 1);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null; yield return null;
        }
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D, Key.J));
        yield return null; yield return null;
        check("Simultaneous keys finish only one remaining line", !ManualStoryDialogueActive);
        for (int f = 0; f < 12; f++) yield return null;
        check("Final advance remains consumed until release", Vector3.Distance(Feet, feet) < .01f && attackAge < 0);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return null; yield return null;
        check("Release clears the dialogue input gate", !DialogueAdvanceInput.GameplayInputBlocked);

        BeginManualStoryDialogue(new[] { "Gaolequ: Menu input verification.", "Gaolequ: Still waiting." });
        yield return null; yield return null;
        DongbaInterface.Instance.Open(DongbaInterface.Page.Help);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
        yield return null; yield return null;
        check("Open menu does not advance dialogue", manualStoryIndex == 0);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        DongbaInterface.Instance.Close(false);
        yield return null; yield return null;
        ResetForestSubtitles();

        // Suspend the forest presenter while the same display runs a teammate-style
        // manual row. This leaves the actual menu/language listeners running.
        enabled = false;
        forestSubtitles.SetExternalManualAdvance(false);
        forestSubtitles.SetCharactersPerSecond(1);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
        yield return null;
        forestSubtitles.PlayBilingualLine("Gaolequ", "A long line for reveal and fresh-press verification.", "用于检查逐字显示与重新按键的一句对白。", true);
        check("Key that opens a line cannot consume that same frame", !forestSubtitles.TryConsumeAdvanceInput());
        yield return null; yield return null;
        check("Holding opening key does not reveal or finish", forestSubtitles.IsTyping && forestSubtitles.IsPlaying);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D, Key.Space));
        yield return null; yield return null;
        check("A fresh second key reveals while another key remains held", !forestSubtitles.IsTyping && forestSubtitles.IsPlaying);
        for (int f = 0; f < 10; f++) yield return null;
        check("Revealing a row does not acknowledge it or auto-repeat", forestSubtitles.IsPlaying);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return null; yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
        yield return null; yield return null;
        check("Fresh press after reveal acknowledges the row", !forestSubtitles.IsPlaying);
        check("Held final key is still consumed after acknowledgement", DialogueAdvanceInput.GameplayInputBlocked);
        forestSubtitles.HideDialogue();
        // A disconnected keyboard cannot keep a stale pressed KeyControl latched.
        InputSystem.RemoveDevice(keyboard);
        yield return null; yield return null;
        check("Removing a held-key device releases input ownership", !DialogueAdvanceInput.GameplayInputBlocked);
        File.WriteAllLines(folder + "/runtime.txt", results);
        Application.Quit(failures == 0 ? 0 : 1);
    }
}}
