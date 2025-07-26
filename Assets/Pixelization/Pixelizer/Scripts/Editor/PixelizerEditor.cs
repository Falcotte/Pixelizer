using NaughtyAttributes.Editor;
using UnityEditor;
using UnityEngine;

namespace AngryKoala.Pixelization
{
    [CustomEditor(typeof(Pixelizer))]
    public class PixelizerEditor : NaughtyInspector
    {
        private Pixelizer _pixelizer;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (_pixelizer == null)
            {
                _pixelizer = (Pixelizer)target;
            }

            EditorGUI.BeginDisabledGroup(!EditorApplication.isPlaying);
            
            if (GUILayout.Button("Pixelize"))
            {
                if (_pixelizer.SourceTexture == null)
                {
                    Debug.LogWarning("No texture found to pixelize");
                    return;
                }

                if (!_pixelizer.SourceTexture.isReadable)
                {
                    SetTextureReadability(_pixelizer.SourceTexture);
                }

                _pixelizer.Pixelize();
            }
            
            EditorGUI.EndDisabledGroup();
        }

        private void SetTextureReadability(Texture2D texture)
        {
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture));

            importer.textureType = TextureImporterType.Default;
            importer.isReadable = true;

            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }
    }
}