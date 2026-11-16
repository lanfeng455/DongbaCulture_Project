using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
namespace DongbaPrototype {
public enum DongbaLocale { English, Chinese }
public static class DongbaLanguage {
    public const string PreferenceKey="Dongba.Language";
    [Serializable] public class Entry { public string id,en,zh; public string[] aliases; }
    [Serializable] class Catalogue { public Entry[] entries; }
    static readonly Dictionary<string,Entry> lookup=new Dictionary<string,Entry>(StringComparer.Ordinal);
    static readonly Dictionary<string,string> cache=new Dictionary<string,string>(StringComparer.Ordinal);
    static Entry[] entries; static bool initialized; static DongbaLocale current;
    static Font uiFont; static TMP_FontAsset subtitleFont;
    public static event Action Changed;
    public static int Revision {get;private set;}
    public static DongbaLocale Current {get {Initialize();return current;}}
    public static bool Chinese=>Current==DongbaLocale.Chinese;
    public static IReadOnlyList<Entry> Entries {get {Initialize();return entries;}}
    public static Font ChineseUIFont {get {if(uiFont==null)uiFont=Resources.Load<DongbaLanguageFonts>("DongbaLanguageFonts").uiFont;return uiFont;}}
    public static TMP_FontAsset ChineseSubtitleFont {get {
        if(subtitleFont==null){
            subtitleFont=Resources.Load<TMP_FontAsset>("Fonts/NotoSerifCJKsc-Black SDF");
            // The large atlas is shared separately. A clean clone can still use the tracked OTF.
            if(subtitleFont==null){subtitleFont=TMP_FontAsset.CreateFontAsset(ChineseUIFont);subtitleFont.isMultiAtlasTexturesEnabled=true;}
        }
        return subtitleFont;
    }}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset(){initialized=false;lookup.Clear();cache.Clear();entries=null;uiFont=null;subtitleFont=null;Changed=null;Revision=0;}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap(){Initialize();new GameObject("Dongba language / persistent preference").AddComponent<DongbaLanguageSwitch>();}
    static void Initialize(){
        if(initialized)return;initialized=true;
        var asset=Resources.Load<TextAsset>("DongbaLanguages");
        entries=JsonUtility.FromJson<Catalogue>(asset.text).entries;
        foreach(var e in entries){Add(e.en,e);Add(e.zh,e);if(e.aliases!=null)foreach(var alias in e.aliases)Add(alias,e);}
        var system=Application.systemLanguage;
        var fallback=system==SystemLanguage.Chinese||system==SystemLanguage.ChineseSimplified||system==SystemLanguage.ChineseTraditional?DongbaLocale.Chinese:DongbaLocale.English;
        current=(DongbaLocale)Mathf.Clamp(PlayerPrefs.GetInt(PreferenceKey,(int)fallback),0,1);
        foreach(var arg in Environment.GetCommandLineArgs()){
            if(arg=="--language=en")current=DongbaLocale.English;
            if(arg=="--language=zh")current=DongbaLocale.Chinese;
        }
    }
    static void Add(string value,Entry entry){if(!string.IsNullOrEmpty(value)&&!lookup.ContainsKey(value))lookup.Add(value,entry);}
    public static void Set(DongbaLocale value,bool persist=true){
        Initialize();value=value==DongbaLocale.Chinese?DongbaLocale.Chinese:DongbaLocale.English;
        if(persist){PlayerPrefs.SetInt(PreferenceKey,(int)value);PlayerPrefs.Save();}
        if(current==value)return;current=value;Revision++;cache.Clear();Changed?.Invoke();
    }
    public static string Text(string source){
        Initialize();if(string.IsNullOrEmpty(source)||source.StartsWith("@",StringComparison.Ordinal))return source??"";
        if(lookup.TryGetValue(source,out var entry))return current==DongbaLocale.Chinese?entry.zh:entry.en;
        if(cache.TryGetValue(source,out var result))return result;
        result=source;
        // Preserve numeric health and rich-text syntax; only translate displayed words.
        if(source.StartsWith("Great boar: ",StringComparison.Ordinal))result=Text("Great boar")+": "+source.Substring(12);
        else if(source.Contains("\n")){var lines=source.Split('\n');for(int i=0;i<lines.Length;i++)lines[i]=Text(lines[i]);result=string.Join("\n",lines);}
        else if(source.Contains("<")){
            var pieces=Regex.Split(source,"(<[^>]+>)");
            for(int i=0;i<pieces.Length;i+=2){var trimmed=pieces[i].Trim();if(lookup.TryGetValue(trimmed,out var term))pieces[i]=pieces[i].Replace(trimmed,current==DongbaLocale.Chinese?term.zh:term.en);}
            result=string.Concat(pieces);
        }
        cache[source]=result;return result;
    }
}
}
