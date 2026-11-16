using UnityEngine;
namespace DongbaPrototype {
    // Reuse the tracked source font; no dependency on an OS-installed Chinese font.
    public sealed class DongbaLanguageFonts : ScriptableObject {
        public Font uiFont;
    }
}
