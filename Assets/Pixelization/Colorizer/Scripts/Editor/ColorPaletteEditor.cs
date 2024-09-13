using UnityEditor;
using UnityEngine;

namespace AngryKoala.Pixelization
{
    [CustomEditor(typeof(ColorPalette))]
    public class ColorPaletteEditor : Editor
    {
        private ColorPalette colorPalette;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (colorPalette == null)
                colorPalette = (ColorPalette)target;

            if(GUILayout.Button("Sort Colors By Hue"))
            {
                colorPalette.SortColorsByHue();
            }
            
            if(GUILayout.Button("Sort Colors By Saturation"))
            {
                colorPalette.SortColorsBySaturation();
            }
            
            if(GUILayout.Button("Sort Colors By Value"))
            {
                colorPalette.SortColorsByValue();
            }
        }
    }
}