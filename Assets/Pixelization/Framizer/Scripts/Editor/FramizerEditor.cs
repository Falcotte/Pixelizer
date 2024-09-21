using NaughtyAttributes.Editor;
using UnityEditor;
using UnityEngine;

namespace AngryKoala.Pixelization
{
    [CustomEditor(typeof(Framizer))]
    public class FramizerEditor : NaughtyInspector
    {
        private Framizer framizer;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if(framizer == null)
                framizer = (Framizer)target;

            if(GUILayout.Button("Framize"))
            {
                framizer.Framize();
            }
        }
    }
}