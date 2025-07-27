using UnityEditor;
using UnityEngine;

namespace AngryKoala.Pixelization
{
    [CustomEditor(typeof(ColorPalette))]
    public class ColorPaletteEditor : Editor
    {
        private ColorPalette _colorPalette;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (_colorPalette == null)
            {
                _colorPalette = (ColorPalette)target;
            }

            if (GUILayout.Button("Sort Colors By Hue"))
            {
                _colorPalette.SortColorsByHue();
            }

            if (GUILayout.Button("Sort Colors By Saturation"))
            {
                _colorPalette.SortColorsBySaturation();
            }

            if (GUILayout.Button("Sort Colors By Value"))
            {
                _colorPalette.SortColorsByValue();
            }
        }
    }
}