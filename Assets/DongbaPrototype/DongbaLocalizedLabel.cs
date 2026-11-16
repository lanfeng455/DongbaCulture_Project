using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace DongbaPrototype {
// Existing gameplay writes source text. Only the presentation changes.
[DefaultExecutionOrder(10000)]
public sealed class DongbaLocalizedLabel : MonoBehaviour {
    Text legacy; TMP_Text tmp;
    Font originalFont; TMP_FontAsset originalTmpFont;
    VerticalWrapMode originalOverflow;
    string source,rendered; int revision=-1;
    public static void Bind(Text text){
        if(text.GetComponent<DongbaLocalizedLabel>()!=null)return;
        var b=text.gameObject.AddComponent<DongbaLocalizedLabel>();b.legacy=text;b.originalFont=text.font;b.originalOverflow=text.verticalOverflow;b.Register();
    }
    public static void Bind(TMP_Text text){
        if(text.GetComponent<DongbaLocalizedLabel>()!=null)return;
        var b=text.gameObject.AddComponent<DongbaLocalizedLabel>();b.tmp=text;b.originalTmpFont=text.font;b.Register();
    }
    void Register(){DongbaLanguage.Changed+=Refresh;Canvas.preWillRenderCanvases+=Refresh;Refresh();}
    void LateUpdate(){Refresh();}
    void OnDestroy(){DongbaLanguage.Changed-=Refresh;Canvas.preWillRenderCanvases-=Refresh;}
    static bool HasChinese(string value){if(value==null)return false;foreach(char c in value)if(c>=0x3400&&c<=0x9fff)return true;return false;}
    public void Refresh(){
        if(legacy==null&&tmp==null)return;
        // UI builders may assign a heading font after UILabel/Bind. Remember that style.
        if(legacy!=null&&legacy.font!=null&&legacy.font!=DongbaLanguage.ChineseUIFont)originalFont=legacy.font;
        string actual=legacy!=null?legacy.text:tmp.text;
        bool changed=actual!=rendered;
        if(changed||source==null)source=actual;
        bool chinese=HasChinese(DongbaLanguage.Text(source));
        var targetFont=chinese?DongbaLanguage.ChineseUIFont:originalFont;
        bool fontChanged=legacy!=null&&legacy.font!=targetFont;
        if(!changed&&revision==DongbaLanguage.Revision&&!fontChanged)return;
        revision=DongbaLanguage.Revision;rendered=DongbaLanguage.Text(source);
        if(legacy!=null){
            legacy.font=targetFont;
            // CJK ascenders exceed the old single-line Latin boxes; don't silently hide a line.
            legacy.verticalOverflow=chinese?VerticalWrapMode.Overflow:originalOverflow;
            if(legacy.text!=rendered)legacy.text=rendered;
        }else{
            tmp.font=chinese?DongbaLanguage.ChineseSubtitleFont:originalTmpFont;
            if(tmp.text!=rendered)tmp.text=rendered;
        }
    }
}
}
