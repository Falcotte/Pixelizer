using NaughtyAttributes.Editor;
using UnityEditor;
using UnityEngine;

namespace AngryKoala.Pixelization
{
    [CustomEditor(typeof(Encodizer))]
    public class EncodizerEditor : NaughtyInspector
    {
        private Encodizer encodizer;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (encodizer == null)
                encodizer = (Encodizer)target;

            if (GUILayout.Button("Encodize"))
            {
                encodizer.Encodize();

                encodizer.OnProgressUpdate = UpdateProgressBar;
                encodizer.OnComplete = ClearProgressBar;
            }
        }

        private void UpdateProgressBar()
        {
            EditorUtility.DisplayProgressBar("Encoding", $"Frame {encodizer.CurrentFrame}/{encodizer.FrameCount}",
                encodizer.Progress);
        }

        private void ClearProgressBar()
        {
            EditorUtility.ClearProgressBar();
        }
    }
}