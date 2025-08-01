using NaughtyAttributes.Editor;
using UnityEditor;
using UnityEngine;

namespace AngryKoala.Pixelization
{
    [CustomEditor(typeof(Texturizer))]
    public class TexturizerEditor : NaughtyInspector
    {
        private Texturizer _texturizer;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (_texturizer == null)
            {
                _texturizer = (Texturizer)target;
            }

            EditorGUI.BeginDisabledGroup(!EditorApplication.isPlaying);
            
            if (GUILayout.Button("Texturize"))
            {
                _texturizer.Texturize(true);
            }
            
            EditorGUI.EndDisabledGroup();
        }
    }
}