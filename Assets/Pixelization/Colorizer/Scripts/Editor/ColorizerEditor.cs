using NaughtyAttributes.Editor;
using UnityEditor;
using UnityEngine;

namespace AngryKoala.Pixelization
{
    [CustomEditor(typeof(Colorizer))]
    public class ColorizerEditor : NaughtyInspector
    {
        private Colorizer colorizer;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if(colorizer == null)
                colorizer = (Colorizer)target;

            if(GUILayout.Button("Colorize"))
            {
                colorizer.Colorize();
            }

            if(GUILayout.Button("Recolorize"))
            {
                colorizer.ResetColors();
                colorizer.Colorize();
            }

            if(GUILayout.Button("Create New Color Palette"))
            {
                colorizer.CreateNewColorPalette();
            }
            
            if(GUILayout.Button("Add To Color Palette"))
            {
                colorizer.AddToColorPalette();
            }

            if(GUILayout.Button("Save Color Palette"))
            {
                colorizer.SaveColorPalette();
            }

            if(GUILayout.Button("Clear Color Collection"))
            {
                colorizer.ColorPalette.Colors.Clear();
            }

            if(GUILayout.Button("Complement Colors"))
            {
                colorizer.ComplementColors();
            }

            if(GUILayout.Button("Invert Colors"))
            {
                colorizer.InvertColors();
            }

            if(GUILayout.Button("Reset Colors"))
            {
                colorizer.ResetColors();
            }
        }
    }
}