using System.IO;
using NaughtyAttributes;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace AngryKoala.Pixelization
{
    public class Texturizer : MonoBehaviour
    {
        [SerializeField] private Pixelizer pixelizer;

        private Texture2D newTexture;

        private enum TexturizationStyle
        {
            PixSize,
            CustomSize
        }

        [SerializeField] private TexturizationStyle texturizationStyle;

        [SerializeField] [ShowIf("texturizationStyle", TexturizationStyle.PixSize)]
        private int pixSize;

        [SerializeField] [ShowIf("texturizationStyle", TexturizationStyle.CustomSize)]
        private int width;

        [SerializeField] [ShowIf("texturizationStyle", TexturizationStyle.CustomSize)]
        private int height;

        [SerializeField] private string textureSavePath;

        public void Texturize(bool saveTexture = false, string customSavePath = "")
        {
            if (pixelizer.PixCollection.Length == 0)
            {
                return;
            }

            if (newTexture != null && !saveTexture)
            {
                DestroyImmediate(newTexture);
            }

            if (texturizationStyle == TexturizationStyle.PixSize)
            {
                newTexture = new Texture2D(pixelizer.CurrentWidth * pixSize, pixelizer.CurrentHeight * pixSize,
                    TextureFormat.RGB24, false);

                int pixIndex = 0;

                for (int i = 0; i < pixelizer.CurrentWidth; i++)
                {
                    for (int j = 0; j < pixelizer.CurrentHeight; j++)
                    {
                        Color[] pixColor = new Color[pixSize * pixSize];

                        for (int k = 0; k < pixColor.Length; k++)
                        {
                            pixColor[k] = pixelizer.PixCollection[pixIndex].Color;
                        }

                        newTexture.SetPixels(i * pixSize, j * pixSize, pixSize, pixSize, pixColor);
                        pixIndex++;
                    }
                }
            }

            if (texturizationStyle == TexturizationStyle.CustomSize)
            {
                newTexture = new Texture2D(pixelizer.CurrentWidth, pixelizer.CurrentHeight, TextureFormat.RGB24, false);

                int pixIndex = 0;

                for (int i = 0; i < pixelizer.CurrentWidth; i++)
                {
                    for (int j = 0; j < pixelizer.CurrentHeight; j++)
                    {
                        Color[] pixColor = new Color[1];

                        for (int k = 0; k < pixColor.Length; k++)
                        {
                            pixColor[k] = pixelizer.PixCollection[pixIndex].Color;
                        }

                        newTexture.SetPixels(i, j, 1, 1, pixColor);
                        pixIndex++;
                    }
                }

                Texture2D scaledTexture = new Texture2D(width, height, TextureFormat.RGB24, false);
                newTexture.wrapMode = TextureWrapMode.Clamp;

                for (int i = 0; i < width; i++)
                {
                    for (int j = 0; j < height; j++)
                    {
                        Color color = newTexture.GetPixelBilinear((float)i / width, (float)j / height);
                        scaledTexture.SetPixel(i, j, color);
                    }
                }

                newTexture = scaledTexture;
            }

#if UNITY_EDITOR
            if (saveTexture)
            {
                textureSavePath = string.IsNullOrEmpty(customSavePath) ? textureSavePath : customSavePath;

                // if (!AssetDatabase.IsValidFolder(textureSavePath))
                // {
                //     Debug.LogWarning("Save path is not valid");
                //     return;
                // }

                string path = AssetDatabase.GenerateUniqueAssetPath($"{textureSavePath}");

                byte[] bytes = newTexture.EncodeToPNG();
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
                newTexture.filterMode = FilterMode.Point;
                newTexture.Apply();

                pixelizer.TexturizedTexture = newTexture;
            }
        }

        private void OnValidate()
        {
            width = Mathf.Max(width, 1);
            height = Mathf.Max(height, 1);
        }
    }
}