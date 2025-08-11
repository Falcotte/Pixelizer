using System.Collections.Generic;
using System.IO;
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

        [SerializeField] private bool _createNewColorPaletteOnColorize;

        [SerializeField] [OnValueChanged("OnColorPaletteColorCountChanged")] [Range(1, 10)]
        private int _newColorPaletteColorCount = 1;

        private List<Color> _newColorPaletteColors = new();

        private enum ColorizationStyle
        {
            Replace,
            ReplaceWithOriginalSaturationAndValue
        }

        [SerializeField] private ColorizationStyle _colorizationStyle;

        [SerializeField] private bool _useColorGroups;

        [SerializeField] private bool _useValueRamp;

        [SerializeField] [ShowIf("_useValueRamp")] [Range(2, 10)]
        private int _rampCount = 2;

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

            if (_createNewColorPaletteOnColorize)
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
                if (!_createNewColorPaletteOnColorize)
                {
                    _colorGroupsColors = GetColorPalette(_colorPalette.Colors.Count);
                }

                MapColorPaletteColorsToColorGroupsColors();
            }

            for (int i = 0; i < _pixelizer.PixCollection.Length; i++)
            {
                float rampValue = 0f;

                if (_useValueRamp)
                {
                    rampValue = SnapToNearestRamp(_pixelizer.PixCollection[i].Color.Value(), _rampCount);
                }

                switch (_colorizationStyle)
                {
                    case ColorizationStyle.Replace:
                    {
                        Color closestColor;

                        if (_useColorGroups)
                        {
                            closestColor = GetClosestColor(_pixelizer.PixCollection[i].Color, _colorGroupsColors);

                            _pixelizer.PixCollection[i].ColorIndex = _colorGroupsColors.IndexOf(closestColor);

                            Color color = _sortedColorPaletteColors[_pixelizer.PixCollection[i].ColorIndex];

                            if (_useValueRamp)
                            {
                                Color.RGBToHSV(color, out float hue, out float saturation, out float value);
                                _pixelizer.PixCollection[i].Color = Color.HSVToRGB(hue, saturation, rampValue);
                            }
                            else
                            {
                                _pixelizer.PixCollection[i].Color = color;
                            }
                        }
                        else
                        {
                            closestColor = GetClosestColor(_pixelizer.PixCollection[i].Color,
                                _colorPalette.Colors);

                            if (_useValueRamp)
                            {
                                Color.RGBToHSV(closestColor, out float hue, out float saturation, out float value);

                                closestColor = Color.HSVToRGB(hue, saturation, rampValue);
                            }

                            _pixelizer.PixCollection[i].Color = closestColor;
                        }
                    }
                        break;

                    case ColorizationStyle.ReplaceWithOriginalSaturationAndValue:
                    {
                        Color originalColor = _pixelizer.PixCollection[i].Color;
                        Color adjustedColor;

                        if (_useColorGroups)
                        {
                            adjustedColor = GetClosestColor(originalColor, _colorGroupsColors);
                            _pixelizer.PixCollection[i].ColorIndex = _colorGroupsColors.IndexOf(adjustedColor);

                            if (_useValueRamp)
                            {
                                Color.RGBToHSV(adjustedColor, out float hue, out float saturation, out float value);
                                adjustedColor = Color.HSVToRGB(hue, originalColor.Saturation(), rampValue);
                            }
                            else
                            {
                                adjustedColor = _sortedColorPaletteColors[_pixelizer.PixCollection[i].ColorIndex];
                            }
                        }
                        else
                        {
                            adjustedColor = GetClosestColor(originalColor, _colorPalette.Colors);

                            if (_useValueRamp)
                            {
                                adjustedColor = Color.HSVToRGB(adjustedColor.Hue(), originalColor.Saturation(),
                                    rampValue);
                            }
                            else
                            {
                                adjustedColor = Color.HSVToRGB(adjustedColor.Hue(), originalColor.Saturation(),
                                    originalColor.Value());
                            }
                        }

                        _pixelizer.PixCollection[i].Color = adjustedColor;
                    }
                        break;
                }
            }

            _pixelizer.Texturizer.Texturize();
            _pixelizer.Texturizer.SetVisualTexture();
        }

        /// <summary>
        /// Finds the color from a provided list that is most similar to the specified target color,
        /// using <see cref="GetColorDifference(Color, Color)"/> as the similarity metric.
        /// </summary>
        /// <param name="color">The target color to match.</param>
        /// <param name="colorizerColors">The list of colors to search through.</param>
        /// <returns>
        /// The <see cref="Color"/> from <paramref name="colorizerColors"/> that has the smallest
        /// perceptual difference to <paramref name="color"/>.
        /// </returns>
        private Color GetClosestColor(Color color, List<Color> colorizerColors)
        {
            float hue, saturation, value;
            Color.RGBToHSV(color, out hue, out saturation, out value);

            float colorDifference = Mathf.Infinity;

            Color closestColor = Color.white;

            foreach (var colorizerColor in colorizerColors)
            {
                Vector3 colorHue = new Vector3(color.r, color.g, color.b);
                Vector3 colorizerColorHue = new Vector3(colorizerColor.r, colorizerColor.g, colorizerColor.b);

                float difference = GetColorDifference(colorizerColor, color);

                if (difference < colorDifference)
                {
                    closestColor = colorizerColor;
                    colorDifference = difference;
                }
            }

            return closestColor;
        }

        private float SnapToNearestRamp(float value, int rampCount)
        {
            float step = 1f / (rampCount - 1);
            int rampIndex = (int)Mathf.Round(value / step);

            return rampIndex * step;
        }

        #region Color Palette

        /// <summary>
        /// Generates a representative color palette of the specified size from the pixel data
        /// in <c>PixCollection</c> using a k-means–like clustering algorithm.
        /// </summary>
        /// <param name="colorCount">The number of colors to include in the generated palette.</param>
        /// <returns>A list of <see cref="Color"/> objects representing the final palette.</returns>
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
            if (_pixelizer.PixCollection == null)
            {
                Debug.LogWarning("Pixelize a texture first");
                return;
            }

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

            ColorPalette colorPalette = UnityEditor.AssetDatabase.Contains(_colorPalette)
                ? Instantiate(_colorPalette)
                : _colorPalette;

            colorPalette.name = Path.GetFileNameWithoutExtension(path);

            UnityEditor.AssetDatabase.CreateAsset(colorPalette, path);
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.Refresh(UnityEditor.ImportAssetOptions.ForceUpdate);
            
            Debug.Log($"Color palette saved as {colorPalette.name}");
            
            UnityEditor.EditorGUIUtility.PingObject(colorPalette);
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

        /// <summary>
        /// Computes a perceptual difference between two colors in HSV space, normalized to [0,1].
        /// The difference is calculated using weighted hue, saturation, and value distances,
        /// where weights adapt dynamically. Hue influence increases for vivid mid-bright colors and decreases for 
        /// dark or desaturated colors, while value influence increases in darker ranges.
        /// </summary>
        /// <param name="color1"></param>
        /// <param name="color2"></param>
        /// <returns>Returns 0 for identical colors and 1 for maximally different colors given these perceptual rules.</returns>
        private float GetColorDifference(Color color1, Color color2)
        {
            Color.RGBToHSV(color1, out float color1Hue, out float color1Saturation, out float color1Value);
            Color.RGBToHSV(color2, out float color2Hue, out float color2Saturation, out float color2Value);

            float hueDifference = Mathf.Abs(color1Hue - color2Hue);
            hueDifference = Mathf.Min(hueDifference, 1f - hueDifference) * 2f;
            float saturationDifference = Mathf.Abs(color1Saturation - color2Saturation);
            float valueDifference = Mathf.Abs(color1Value - color2Value);

            float minSaturation = Mathf.Min(color1Saturation, color2Saturation);
            float minValue = Mathf.Min(color1Value, color2Value);

            float darkness = Mathf.SmoothStep(0f, 1f, 1f - minValue);

            float midSaturation = SmoothRamp(minSaturation, 0.15f, 0.30f);
            float midValue = SmoothRamp(minValue, 0.15f, 0.30f);

            float vividness = minSaturation * minValue;

            float hueCurve = SmoothRamp(vividness, 0.35f, 0.85f);

            float hueDrive = Mathf.Clamp01(0.8f * (midSaturation * midValue) + 0.4f * hueCurve);

            float hueWeight = Mathf.Lerp(0f, 4.0f, hueDrive);
            float saturationWeight = Mathf.Lerp(0.3f, 1.0f, minValue);
            float valueWeight = Mathf.Lerp(1.0f, 0.4f, minSaturation) * Mathf.Lerp(0.4f, 1.0f, minValue);

            float hueBrightnessBoost = SmoothRamp(vividness, 0.40f, 0.90f);
            hueWeight *= (1f + 2f * hueBrightnessBoost);

            hueWeight *= (1f - darkness);
            saturationWeight *= (1f - 0.8f * darkness);
            valueWeight = Mathf.Lerp(valueWeight, 3.0f, darkness);

            float activeHueRange = hueDifference > Mathf.Epsilon ? 1f : 0f;
            float activeSaturationRange = saturationDifference > Mathf.Epsilon ? 1f : 0f;
            float activeValueRange = valueDifference > Mathf.Epsilon ? 1f : 0f;

            float numerator = hueWeight * hueDifference + saturationWeight * saturationDifference +
                              valueWeight * valueDifference;
            float denominator = hueWeight * activeHueRange + saturationWeight * activeSaturationRange +
                                valueWeight * activeValueRange;

            if (denominator <= 1e-6f)
                return 0f;

            return Mathf.Clamp01(numerator / denominator);
        }

        private float SmoothRamp(float value, float edge0, float edge1)
        {
            if (edge1 <= edge0)
                return value >= edge1 ? 1f : 0f;

            float step = Mathf.Clamp01((value - edge0) / (edge1 - edge0));

            return step * step * (3f - 2f * step);
        }

        #endregion

        #region Color Operations

        public void ComplementColors()
        {
            if (_pixelizer.PixCollection == null)
            {
                Debug.LogWarning("Pixelize a texture first");
                return;
            }

            foreach (var pix in _pixelizer.PixCollection)
            {
                pix.ComplementColor();
            }

            _pixelizer.Texturizer.Texturize();
            _pixelizer.Texturizer.SetVisualTexture();
        }

        public void InvertColors()
        {
            if (_pixelizer.PixCollection == null)
            {
                Debug.LogWarning("Pixelize a texture first");
                return;
            }

            foreach (var pix in _pixelizer.PixCollection)
            {
                pix.InvertColor();
            }

            _pixelizer.Texturizer.Texturize();
            _pixelizer.Texturizer.SetVisualTexture();
        }

        public bool ResetColors()
        {
            if (_pixelizer.PixCollection == null)
            {
                Debug.LogWarning("Pixelize a texture first");
                return false;
            }

            foreach (var pix in _pixelizer.PixCollection)
            {
                pix.ResetColor();
            }

            _pixelizer.Texturizer.Texturize();
            _pixelizer.Texturizer.SetVisualTexture();

            return true;
        }

        private void OnColorPaletteColorCountChanged()
        {
            _newColorPaletteColorCount = Mathf.Max(_newColorPaletteColorCount, 1);
        }

        #endregion
    }
}