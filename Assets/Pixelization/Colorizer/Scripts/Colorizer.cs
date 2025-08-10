using System.Collections.Generic;
using System.Linq;
using NaughtyAttributes;
using UnityEngine;

namespace AngryKoala.Pixelization
{
    public class Colorizer : MonoBehaviour
    {
        [SerializeField] private Pixelizer _pixelizer;

        [SerializeField] private ColorPalette _colorPalette;
        public ColorPalette ColorPalette => _colorPalette;

        [SerializeField] private bool _createNewColorPalette;

        [SerializeField]
        [ShowIf("_createNewColorPalette")]
        [OnValueChanged("OnColorPaletteColorCountChanged")]
        [Range(1, 10)]
        private int _newColorPaletteColorCount = 1;

        private List<Color> _newColorPaletteColors = new();

        private enum ColorizationStyle
        {
            Replace,
            ReplaceWithOriginalSaturationAndValue
        }

        [SerializeField] private ColorizationStyle _colorizationStyle;

        private enum ReplacementStyle
        {
            ReplaceUsingHue,
            ReplaceUsingSaturation,
            ReplaceUsingValue
        }

        [SerializeField] private ReplacementStyle _replacementStyle;

        [SerializeField] private bool _useColorGroups;

        private List<Color> _colorGroupsColors = new();
        private List<Color> _sortedColorPaletteColors = new();

        public void SetPixColors(Texture2D sourceTexture, int width, int height)
        {
            float textureAreaX = (float)sourceTexture.width / width;
            float textureAreaY = (float)sourceTexture.height / height;

            for (int i = 0; i < width * height; i++)
            {
                Color color = GetAverageColor(sourceTexture.GetPixels(Mathf.FloorToInt((i / height) * textureAreaX),
                    Mathf.FloorToInt(i % height * textureAreaY), Mathf.FloorToInt(textureAreaX),
                    Mathf.FloorToInt(textureAreaY)));

                _pixelizer.PixCollection[i].OriginalColor = color;
                _pixelizer.PixCollection[i].Color = color;
            }
        }

        private Color GetAverageColor(Color[] colors)
        {
            float r = 0f;
            float g = 0f;
            float b = 0f;

            for (int i = 0; i < colors.Length; i++)
            {
                r += colors[i].r;
                g += colors[i].g;
                b += colors[i].b;
            }

            r /= colors.Length;
            g /= colors.Length;
            b /= colors.Length;

            return new Color(r, g, b);
        }

        public void Colorize()
        {
            if (_pixelizer.PixCollection == null || _pixelizer.PixCollection.Length == 0)
            {
                Debug.LogWarning("Pixelize a texture first");
                return;
            }

            if (_createNewColorPalette)
            {
                ColorPalette newColorPalette = ScriptableObject.CreateInstance<ColorPalette>();

                _newColorPaletteColors = GetColorPalette(_newColorPaletteColorCount);

                if (_useColorGroups)
                {
                    _colorGroupsColors = _newColorPaletteColors;
                }

                foreach (var color in _newColorPaletteColors)
                {
                    newColorPalette.Colors.Add(color);
                }

                _colorPalette = newColorPalette;
            }

            if (_colorPalette == null)
            {
                Debug.LogWarning("Color palette is not assigned");
                return;
            }

            if (_colorPalette.Colors.Count == 0)
            {
                Debug.LogWarning("No colors selected");
                return;
            }

            if (_useColorGroups)
            {
                if (!_createNewColorPalette)
                {
                    _colorGroupsColors = GetColorPalette(_colorPalette.Colors.Count);
                }

                MapColorPaletteColorsToColorGroupsColors();
            }

            for (int i = 0; i < _pixelizer.PixCollection.Length; i++)
            {
                switch (_colorizationStyle)
                {
                    case ColorizationStyle.Replace:
                        if (_useColorGroups)
                        {
                            Color closestColor = GetClosestColor(_pixelizer.PixCollection[i].Color, _colorGroupsColors,
                                _replacementStyle);

                            _pixelizer.PixCollection[i].ColorIndex = _colorGroupsColors.IndexOf(closestColor);
                            _pixelizer.PixCollection[i].Color =
                                _sortedColorPaletteColors[_pixelizer.PixCollection[i].ColorIndex];
                        }
                        else
                        {
                            Color closestColor = GetClosestColor(_pixelizer.PixCollection[i].Color,
                                _colorPalette.Colors, _replacementStyle);

                            _pixelizer.PixCollection[i].Color = closestColor;
                        }

                        break;

                    case ColorizationStyle.ReplaceWithOriginalSaturationAndValue:
                    {
                        Color originalColor = _pixelizer.PixCollection[i].Color;
                        Color adjustedColor;

                        if (_useColorGroups)
                        {
                            adjustedColor = GetClosestColor(originalColor, _colorGroupsColors, _replacementStyle);
                            _pixelizer.PixCollection[i].ColorIndex = _colorGroupsColors.IndexOf(adjustedColor);
                            adjustedColor = _sortedColorPaletteColors[_pixelizer.PixCollection[i].ColorIndex];
                        }
                        else
                        {
                            adjustedColor = GetClosestColor(originalColor, _colorPalette.Colors, _replacementStyle);
                        }

                        float hue, saturation, value;
                        Color.RGBToHSV(adjustedColor, out hue, out saturation, out value);

                        float originalValue = originalColor.Value();
                        float originalSaturation = originalColor.Saturation();
                        
                        adjustedColor = Color.HSVToRGB(hue, originalSaturation, originalValue);

                        _pixelizer.PixCollection[i].Color = adjustedColor;
                    }
                        break;
                }
            }

            _pixelizer.Texturizer.Texturize();
            _pixelizer.Texturizer.SetVisualTexture();
        }

        private Color GetClosestColor(Color color, List<Color> colorizerColors, ReplacementStyle replacementStyle)
        {
            float hue, saturation, value;
            Color.RGBToHSV(color, out hue, out saturation, out value);

            float colorDifference = Mathf.Infinity;

            Color closestColor = Color.white;

            switch (replacementStyle)
            {
                case ReplacementStyle.ReplaceUsingHue:
                    foreach (var colorizerColor in colorizerColors)
                    {
                        Vector3 colorHue = new Vector3(color.r, color.g, color.b);
                        Vector3 colorizerColorHue = new Vector3(colorizerColor.r, colorizerColor.g, colorizerColor.b);

                        float difference = Vector3.Distance(colorHue, colorizerColorHue);

                        if (difference < colorDifference)
                        {
                            closestColor = colorizerColor;
                            colorDifference = difference;
                        }
                    }

                    break;

                case ReplacementStyle.ReplaceUsingSaturation:
                    foreach (var colorizerColor in colorizerColors)
                    {
                        float colorizerHue, colorizerSaturation, colorizerValue;
                        Color.RGBToHSV(colorizerColor, out colorizerHue, out colorizerSaturation, out colorizerValue);

                        float difference = Mathf.Abs(saturation - colorizerSaturation);

                        if (difference < colorDifference)
                        {
                            closestColor = colorizerColor;
                            colorDifference = difference;
                        }
                    }

                    break;
                case ReplacementStyle.ReplaceUsingValue:
                    foreach (var colorizerColor in colorizerColors)
                    {
                        float colorizerHue, colorizerSaturation, colorizerValue;
                        Color.RGBToHSV(colorizerColor, out colorizerHue, out colorizerSaturation, out colorizerValue);

                        float difference = Mathf.Abs(value - colorizerValue);

                        if (difference < colorDifference)
                        {
                            closestColor = colorizerColor;
                            colorDifference = difference;
                        }
                    }

                    break;
            }

            return closestColor;
        }

        #region Color Palette

        private List<Color> GetColorPalette(int colorCount)
        {
            int iterationCount = 10;

            List<Color> pixels = new List<Color>();
            foreach (var pix in _pixelizer.PixCollection)
            {
                pixels.Add(pix.Color);
            }

            int pixelCount = pixels.Count;

            Color[] centroids = new Color[colorCount];
            for (int i = 0; i < colorCount; i++)
            {
                centroids[i] = pixels[Random.Range(0, pixelCount)];
            }

            for (int i = 0; i < iterationCount; i++)
            {
                int[] nearestCentroidIndices = new int[pixelCount];

                for (int j = 0; j < pixelCount; j++)
                {
                    float nearestDistance = float.MaxValue;
                    int nearestCentroidIndex = 0;
                    for (int k = 0; k < colorCount; k++)
                    {
                        Vector3 colorA = new Vector3(pixels[j].r, pixels[j].g, pixels[j].b);
                        Vector3 colorB = new Vector3(centroids[k].r, centroids[k].g, centroids[k].b);

                        float distance = Vector3.Distance(colorA, colorB);
                        if (distance < nearestDistance)
                        {
                            nearestDistance = distance;
                            nearestCentroidIndex = k;
                        }
                    }

                    nearestCentroidIndices[j] = nearestCentroidIndex;
                }

                for (int j = 0; j < colorCount; j++)
                {
                    var pixelsInCluster =
                        from pixelIndex in Enumerable.Range(0, pixelCount)
                        where nearestCentroidIndices[pixelIndex] == j
                        select pixels[pixelIndex];

                    if (pixelsInCluster.Any())
                    {
                        centroids[j] = new Color(pixelsInCluster.Average(c => c.r), pixelsInCluster.Average(c => c.g),
                            pixelsInCluster.Average(c => c.b));
                    }
                }
            }

            return centroids.ToList();
        }

        public void CreateNewColorPalette()
        {
#if UNITY_EDITOR
            if (_newColorPaletteColorCount <= 0)
            {
                Debug.LogWarning("Color palette color count must be greater than 0");
                return;
            }

            List<Color> centroids = GetColorPalette(_newColorPaletteColorCount);

            ColorPalette newColorPalette = ScriptableObject.CreateInstance<ColorPalette>();

            foreach (var centroid in centroids)
            {
                newColorPalette.Colors.Add(centroid);
            }

            _colorPalette = newColorPalette;
#endif
        }

        public void SaveColorPalette()
        {
#if UNITY_EDITOR
            if (_colorPalette == null)
            {
                Debug.LogWarning("Color palette is not assigned");
                return;
            }

            string path =
                UnityEditor.AssetDatabase.GenerateUniqueAssetPath(
                    "Assets/Pixelization/Colorizer/ScriptableObjects/ColorPalette_.asset");

            UnityEditor.AssetDatabase.CreateAsset(_colorPalette, path);
#endif
        }

        private void MapColorPaletteColorsToColorGroupsColors()
        {
            List<List<Color>> colorPermutations = GetAllColorPermutations(_colorPalette.Colors);

            int closestColorPermutationIndex = 0;
            float difference = Mathf.Infinity;

            for (int i = 0; i < colorPermutations.Count; i++)
            {
                float currentDifference = 0f;

                for (int j = 0; j < _colorPalette.Colors.Count; j++)
                {
                    currentDifference += GetColorDifference(_colorGroupsColors[j], colorPermutations[i][j]);
                }

                if (currentDifference < difference)
                {
                    difference = currentDifference;
                    closestColorPermutationIndex = i;
                }
            }

            _sortedColorPaletteColors = colorPermutations[closestColorPermutationIndex];
        }

        private List<List<Color>> GetAllColorPermutations(List<Color> colors)
        {
            List<List<Color>> colorPermutations = new List<List<Color>>();

            if (colors.Count == 0)
            {
                colorPermutations.Add(new List<Color>());
                return colorPermutations;
            }

            Color firstElement = colors[0];
            List<Color> remainingList = colors.GetRange(1, colors.Count - 1);
            List<List<Color>> subPermutations = GetAllColorPermutations(remainingList);

            foreach (List<Color> permutation in subPermutations)
            {
                for (int i = 0; i <= permutation.Count; i++)
                {
                    List<Color> newPermutation = new List<Color>(permutation);
                    newPermutation.Insert(i, firstElement);
                    colorPermutations.Add(newPermutation);
                }
            }

            return colorPermutations;
        }

        private float GetColorDifference(Color color1, Color color2)
        {
            switch (_replacementStyle)
            {
                case ReplacementStyle.ReplaceUsingHue:
                    return color1.HueDifference(color2);
                case ReplacementStyle.ReplaceUsingSaturation:
                    return color1.SaturationDifference(color2);
                case ReplacementStyle.ReplaceUsingValue:
                    return color1.ValueDifference(color2);
            }

            return 0f;
        }

        #endregion

        #region Color Operations

        public void ComplementColors()
        {
            foreach (var pix in _pixelizer.PixCollection)
            {
                pix.ComplementColor();
            }

            _pixelizer.Texturizer.Texturize();
            _pixelizer.Texturizer.SetVisualTexture();
        }

        public void InvertColors()
        {
            foreach (var pix in _pixelizer.PixCollection)
            {
                pix.InvertColor();
            }

            _pixelizer.Texturizer.Texturize();
            _pixelizer.Texturizer.SetVisualTexture();
        }

        public void ResetColors()
        {
            foreach (var pix in _pixelizer.PixCollection)
            {
                pix.ResetColor();
            }

            _pixelizer.Texturizer.Texturize();
            _pixelizer.Texturizer.SetVisualTexture();
        }

        private void OnColorPaletteColorCountChanged()
        {
            _newColorPaletteColorCount = Mathf.Max(_newColorPaletteColorCount, 1);
        }

        #endregion
    }
}