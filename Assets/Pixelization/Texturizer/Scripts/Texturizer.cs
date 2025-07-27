using System;
using System.IO;
using NaughtyAttributes;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Events;

namespace AngryKoala.Pixelization
{
    public class Texturizer : MonoBehaviour
    {
        [SerializeField] private Pixelizer _pixelizer;
        
        private MeshRenderer _visual;
        
        private Texture2D _newTexture;

        public Texture2D TexturizedTexture { get; set; }
        
        private enum TexturizationStyle
        {
            PixSize,
            CustomSize
        }

        [SerializeField] private TexturizationStyle _texturizationStyle;

        [SerializeField] [ShowIf("_texturizationStyle", TexturizationStyle.PixSize)]
        private int _pixSize;

        [SerializeField] [ShowIf("_texturizationStyle", TexturizationStyle.CustomSize)]
        private int _width;

        [SerializeField] [ShowIf("_texturizationStyle", TexturizationStyle.CustomSize)]
        private int _height;

        [SerializeField] private string _textureSavePath;
        
        private static readonly int MainTex = Shader.PropertyToID("_MainTex");

        public static UnityAction<float, float> VisualSizeUpdated;

        private void CreateVisual()
        {
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Quad);
            
            visual.name = "Visual";
            visual.transform.SetParent(transform, false);
            visual.transform.localPosition = Vector3.zero;
            
            _visual = visual.GetComponent<MeshRenderer>();
            _visual.sharedMaterial = new Material(Shader.Find("Unlit/Texture"));
        }
        
        public void SetVisualSize(int width, int height, float pixSize)
        {
            if (_visual == null)
            {
                CreateVisual();
            }
            
            _visual.transform.localScale = new Vector3(pixSize * width, pixSize * height, 1f);
            
            VisualSizeUpdated?.Invoke(pixSize * width, pixSize * height);
        }

        public void SetVisualTexture()
        {
            _visual.sharedMaterial.SetTexture(MainTex, TexturizedTexture);
        }
        
        public void Texturize(bool saveTexture = false, string customSavePath = "")
        {
            if (_pixelizer.PixCollection.Length == 0)
                return;

            if (_newTexture != null && !saveTexture)
            {
                DestroyImmediate(_newTexture);
            }

            if (_texturizationStyle == TexturizationStyle.PixSize)
            {
                _newTexture = new Texture2D(_pixelizer.CurrentWidth * _pixSize, _pixelizer.CurrentHeight * _pixSize,
                    TextureFormat.RGB24, false);

                int pixIndex = 0;

                for (int i = 0; i < _pixelizer.CurrentWidth; i++)
                {
                    for (int j = 0; j < _pixelizer.CurrentHeight; j++)
                    {
                        Color[] pixColor = new Color[_pixSize * _pixSize];

                        for (int k = 0; k < pixColor.Length; k++)
                        {
                            pixColor[k] = _pixelizer.PixCollection[pixIndex].Color;
                        }

                        _newTexture.SetPixels(i * _pixSize, j * _pixSize, _pixSize, _pixSize, pixColor);
                        pixIndex++;
                    }
                }
            }

            if (_texturizationStyle == TexturizationStyle.CustomSize)
            {
                _newTexture = new Texture2D(_pixelizer.CurrentWidth, _pixelizer.CurrentHeight, TextureFormat.RGB24,
                    false);

                int pixIndex = 0;

                for (int i = 0; i < _pixelizer.CurrentWidth; i++)
                {
                    for (int j = 0; j < _pixelizer.CurrentHeight; j++)
                    {
                        Color[] pixColor = new Color[1];

                        for (int k = 0; k < pixColor.Length; k++)
                        {
                            pixColor[k] = _pixelizer.PixCollection[pixIndex].Color;
                        }

                        _newTexture.SetPixels(i, j, 1, 1, pixColor);
                        pixIndex++;
                    }
                }

                Texture2D scaledTexture = new Texture2D(_width, _height, TextureFormat.RGB24, false);
                _newTexture.wrapMode = TextureWrapMode.Clamp;

                for (int i = 0; i < _width; i++)
                {
                    for (int j = 0; j < _height; j++)
                    {
                        Color color = _newTexture.GetPixelBilinear((float)i / _width, (float)j / _height);
                        scaledTexture.SetPixel(i, j, color);
                    }
                }

                _newTexture = scaledTexture;
            }

#if UNITY_EDITOR
            if (saveTexture)
            {
                _textureSavePath = string.IsNullOrEmpty(customSavePath) ? _textureSavePath : customSavePath;

                if (!AssetDatabase.IsValidFolder(_textureSavePath))
                {
                    Debug.LogWarning("Save path is not valid");
                    return;
                }

                string path = AssetDatabase.GenerateUniqueAssetPath($"{_textureSavePath}");

                byte[] bytes = _newTexture.EncodeToPNG();
                File.WriteAllBytes(path, bytes);

                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

                TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);

                importer.textureType = TextureImporterType.Default;

                TextureImporterSettings importerSettings = new TextureImporterSettings();
                importer.ReadTextureSettings(importerSettings);

                importerSettings.npotScale = TextureImporterNPOTScale.None;

                importer.SetTextureSettings(importerSettings);

                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
            }
#endif
            if (!saveTexture)
            {
                _newTexture.filterMode = FilterMode.Point;
                _newTexture.Apply();

                TexturizedTexture = _newTexture;
            }
        }

        private void OnValidate()
        {
            _width = Mathf.Max(_width, 1);
            _height = Mathf.Max(_height, 1);
        }
    }
}