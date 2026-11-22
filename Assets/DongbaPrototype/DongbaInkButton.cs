using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace DongbaPrototype {
public sealed class DongbaInkButton:MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,ISelectHandler,IDeselectHandler,IPointerDownHandler,IPointerUpHandler {
    Image wash,mark,rule;Text label;Vector2 origin;bool hover,selected,pressed;float amount;
    public void Initialize(Image w,Text t,Image m,Image r){wash=w;label=t;mark=m;rule=r;origin=t.rectTransform.anchoredPosition;Render();}
    void OnEnable(){amount=0;pressed=hover=selected=false;}
    void Update(){amount=Mathf.Lerp(amount,hover||selected?1:0,1-Mathf.Exp(-Time.unscaledDeltaTime*18));Render();}
    void Render(){if(label==null)return;wash.color=new Color(.58f,.25f,.12f,amount*(pressed?.20f:.10f));mark.color=new Color(.57f,.235f,.14f,Mathf.Lerp(.12f,1,amount));label.color=Color.Lerp(DongbaUIStyle.Ink,DongbaUIStyle.Accent,amount);label.rectTransform.anchoredPosition=origin+Vector2.right*(amount*5);rule.color=new Color(.35f,.25f,.15f,Mathf.Lerp(.2f,.42f,amount));}
    public void OnPointerEnter(PointerEventData e){hover=true;}public void OnPointerExit(PointerEventData e){hover=false;pressed=false;}
    public void OnSelect(BaseEventData e){selected=true;}public void OnDeselect(BaseEventData e){selected=false;}
    public void OnPointerDown(PointerEventData e){pressed=true;}public void OnPointerUp(PointerEventData e){pressed=false;}
}
}
