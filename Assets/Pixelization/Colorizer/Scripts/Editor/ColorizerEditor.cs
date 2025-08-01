using NaughtyAttributes.Editor;
using UnityEditor;
using UnityEngine;

namespace AngryKoala.Pixelization
{
    [CustomEditor(typeof(Colorizer))]
    public class ColorizerEditor : NaughtyInspector
    {
        private Colorizer _colorizer;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (_colorizer == null)
            {
                _colorizer = (Colorizer)target;
            }

            EditorGUI.BeginDisabledGroup(!EditorApplication.isPlaying);

            if (GUILayout.Button("Colorize"))
            {
                _colorizer.Colorize();
            }

            if (GUILayout.Button("Recolorize"))
            {
                _colorizer.ResetColors();
                _colorizer.Colorize();
            }

            if (GUILayout.Button("Create New Color Palette"))
            {
                _colorizer.CreateNewColorPalette();
            }

            if (GUILayout.Button("Save Color Palette"))
            {
                _colorizer.SaveColorPalette();
            }
            
            if(GUILayout.Button("Clear Color Palette"))
            {
                _colorizer.ColorPalette.Colors.Clear();
            }

            if (GUILayout.Button("Complement Colors"))
            {
                _colorizer.ComplementColors();
            }

            if (GUILayout.Button("Invert Colors"))
            {
                _colorizer.InvertColors();
            }

            if (GUILayout.Button("Reset Colors"))
            {
                _colorizer.ResetColors();
            }

            EditorGUI.EndDisabledGroup();
        }
    }
}