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
            var read=typeof(DialogueCsvImporter).GetMethod("ReadEntries",BindingFlags.NonPublic|BindingFlags.Static);
            var rows=(List<DialogueLibrarySO.DialogueEntry>)read.Invoke(null,new object[]{path});
            var library=AssetDatabase.LoadAssetAtPath<DialogueLibrarySO>("Assets/Dialogues/Level 0.asset");
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
            ForestDialogueGraphVerification.Build();
        }
    }
}
