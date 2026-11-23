using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DongbaPrototype
{
    public static class PuzzleGuidanceVerification
    {
        public static void Build()
        {
            const string path="Assets/Dialogues/Level 0.csv";
            var read=typeof(DialogueCsvImporter).GetMethod("ReadEntries",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static);
            var rows=(List<DialogueLibrarySO.DialogueEntry>)read.Invoke(null,new object[]{path});
            var library=AssetDatabase.LoadAssetAtPath<DialogueLibrarySO>("Assets/Dialogues/Level 0.asset");
            if(library.LinkedCsvPath!=path)throw new Exception("Puzzle library lost its editable CSV link");
            if(rows.Count!=43||library.Count!=rows.Count)throw new Exception("Puzzle dialogue count mismatch");
            for(int i=0;i<rows.Count;i++)
            {
                var stored=library.GetEntry(i);
                if(rows[i].content!=stored.content||rows[i].chineseContent!=stored.chineseContent||rows[i].speaker!=stored.speaker)
                    throw new Exception("CSV / library mismatch at "+i);
                if(!stored.content.StartsWith("@")&&string.IsNullOrWhiteSpace(stored.chineseContent))
                    throw new Exception("Missing Chinese translation "+i);
            }
            const string temp="Verification/puzzle-guidance-roundtrip.csv";
            Directory.CreateDirectory("Verification");
            typeof(DialogueCsvImporter).GetMethod("WriteCsv",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{library,temp});
            var reread=(List<DialogueLibrarySO.DialogueEntry>)read.Invoke(null,new object[]{temp});
            for(int i=0;i<rows.Count;i++)if(rows[i].chineseContent!=reread[i].chineseContent||rows[i].content!=reread[i].content)
                throw new Exception("Bilingual CSV round trip failed "+i);
            Debug.Log("Puzzle guidance: 43 bilingual entries match runtime asset; CSV round trip passed.");
            const string nextPath="Assets/Dialogues/Level 2.csv";
            var nextRows=DialogueCsvImporter.ReadEntries(nextPath);
            var nextLibrary=AssetDatabase.LoadAssetAtPath<DialogueLibrarySO>("Assets/Dialogues/Level 2.asset");
            if(nextRows.Count!=29||nextLibrary.Count!=29||nextLibrary.LinkedCsvPath!=nextPath)
                throw new Exception("Bear / pig library count or editable CSV link mismatch");
            for(int i=0;i<nextRows.Count;i++) {
                var a=nextRows[i];var b=nextLibrary.GetEntry(i);
                if(string.IsNullOrWhiteSpace(a.content)||string.IsNullOrWhiteSpace(a.chineseContent)||a.content!=b.content||a.chineseContent!=b.chineseContent||a.speaker!=b.speaker||a.manualContinue!=b.manualContinue)
                    throw new Exception("Bear / pig bilingual source mismatch "+i);
            }
            var nextGraph=AssetDatabase.LoadAssetAtPath<ForestDialogueGraph>("Assets/Ivan/Puzzle/Beat4DialogueGraph.asset");
            if(nextGraph==null||nextGraph.Validate().Count>0)throw new Exception("Bear / pig dialogue graph is incomplete: "+(nextGraph==null?"missing":string.Join("; ",nextGraph.Validate())));
            foreach(var entry in new[]{ForestDialogueGraph.Entry.BoarPass,ForestDialogueGraph.Entry.GrainGiveaway,ForestDialogueGraph.Entry.GrainPickup})
                if(nextGraph.StartFor(entry)==null)throw new Exception("Missing bear / pig entry "+entry);
            Debug.Log("Bear / pig: 29 bilingual entries match runtime; all three dialogue graph entries valid.");
            ForestDialogueGraphVerification.Build();
        }
    }
}
