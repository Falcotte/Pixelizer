using System.Collections.Generic;
using System.Linq;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Events;

namespace AngryKoala.Pixelization
{
    public class Colorizer : MonoBehaviour
    {
        [SerializeField] private Pixelizer pixelizer;

        [SerializeField] private ColorPalette colorPalette;
        public ColorPalette ColorPalette => colorPalette;

        [SerializeField][OnValueChanged("OnColorPaletteColorCountChanged")][Range(1, 20)] private int colorPaletteColorCount;

        [SerializeField] private bool createNewColorPalette;

        private enum ColorizationStyle { Replace, ReplaceWithOriginalSaturation, ReplaceWithOriginalValue }
        [SerializeField] private ColorizationStyle colorizationStyle;

        private bool showUseRamp => (colorizationStyle == ColorizationStyle.ReplaceWithOriginalSaturation || colorizationStyle == ColorizationStyle.ReplaceWithOriginalValue);
        [SerializeField][ShowIf("showUseRamp")] private bool useRamp;
        private bool showRampCount => (colorizationStyle == ColorizationStyle.ReplaceWithOriginalSaturation || colorizationStyle == ColorizationStyle.ReplaceWithOriginalValue) && useRamp;
        [SerializeField][ShowIf("showRampCount")][Range(1, 20)] private int rampCount = 1;

        private enum ReplacementStyle { ReplaceUsingHue, ReplaceUsingSaturation, ReplaceUsingValue }
        [SerializeField] private ReplacementStyle replacementStyle;

        [SerializeField] private bool useColorGroups;

        private List<Color> colorGroupsColors = new List<Color>();
        private List<Color> sortedColorPaletteColors = new List<Color>();

        public static UnityAction OnColorize;

        public void Colorize()
        {
            if(pixelizer.PixCollection.Length == 0)
            {
                Debug.LogWarning("Pixelize a texture first");
                return;
            }

            if(createNewColorPalette)
            {
                ColorPalette newColorPalette = ScriptableObject.CreateInstance<ColorPalette>();

                colorGroupsColors = GetColorPalette(colorPaletteColorCount);

                foreach(var color in colorGroupsColors)
                {
                    newColorPalette.Colors.Add(color);
                }

                colorPalette = newColorPalette;
            }

            if(colorPalette == null)
            {
                Debug.LogWarning("Color palette is not assigned");
                return;
            }

            if(colorPalette.Colors.Count == 0)
            {
                Debug.LogWarning("No colors selected");
                return;
            }

            if(useColorGroups)
            {
                if(!createNewColorPalette)
                {
                    colorGroupsColors = GetColorPalette(colorPalette.Colors.Count);
                }
                MapColorPaletteColorsToColorGroupsColors();
            }

            for(int i = 0; i < pixelizer.PixCollection.Length; i++)
            {
                switch(colorizationStyle)
                {
                    case ColorizationStyle.Replace:
                        if(useColorGroups)
                        {
                            Color closestColor = GetClosestColor(pixelizer.PixCollection[i].Color, colorGroupsColors, replacementStyle);
                            pixelizer.PixCollection[i].ColorIndex = colorGroupsColors.IndexOf(closestColor);
                            pixelizer.PixCollection[i].SetColor(sortedColorPaletteColors[pixelizer.PixCollection[i].ColorIndex]);
                        }
                        else
                        {
                            pixelizer.PixCollection[i].SetColor(GetClosestColor(pixelizer.PixCollection[i].Color, colorPalette.Colors, replacementStyle));
                        }
                        break;

                    case ColorizationStyle.ReplaceWithOriginalSaturation:
                        {
                            Color originalColor = pixelizer.PixCollection[i].Color;
                            Color adjustedColor;

                            if(useColorGroups)
                            {
                                adjustedColor = GetClosestColor(originalColor, colorGroupsColors, replacementStyle);
                                pixelizer.PixCollection[i].ColorIndex = colorGroupsColors.IndexOf(adjustedColor);
                                adjustedColor = sortedColorPaletteColors[pixelizer.PixCollection[i].ColorIndex];
                            }
                            else
                            {
                                adjustedColor = GetClosestColor(originalColor, colorPalette.Colors, replacementStyle);
                            }

                            float hue, saturation, value;
                            Color.RGBToHSV(adjustedColor, out hue, out saturation, out value);

                            float originalSaturation = originalColor.Saturation();

                            if(useRamp)
                            {
                                adjustedColor = Color.HSVToRGB(hue, (20f / rampCount) * (Mathf.RoundToInt((originalSaturation * 20f) / (20f / rampCount))) / 20f, value);
                            }
                            else
                            {
                                adjustedColor = Color.HSVToRGB(hue, originalSaturation, value);
                            }

                            pixelizer.PixCollection[i].SetColor(adjustedColor);
                        }
                        break;

                    case ColorizationStyle.ReplaceWithOriginalValue:
                        {
                            Color originalColor = pixelizer.PixCollection[i].Color;
                            Color adjustedColor;

                            if(useColorGroups)
                            {
                                adjustedColor = GetClosestColor(originalColor, colorGroupsColors, replacementStyle);
                                pixelizer.PixCollection[i].ColorIndex = colorGroupsColors.IndexOf(adjustedColor);
                                adjustedColor = sortedColorPaletteColors[pixelizer.PixCollection[i].ColorIndex];
                            }
                            else
                            {
                                adjustedColor = GetClosestColor(originalColor, colorPalette.Colors, replacementStyle);
                            }

                            float hue, saturation, value;
                            Color.RGBToHSV(adjustedColor, out hue, out saturation, out value);

                            float originalValue = originalColor.Value();

                            if(useRamp)
                            {
                                adjustedColor = Color.HSVToRGB(hue, saturation, (20f / rampCount) * (Mathf.RoundToInt((originalValue * 20f) / (20f / rampCount))) / 20f);
                            }
                            else
                            {
                                adjustedColor = Color.HSVToRGB(hue, saturation, originalValue);
                            }

                            pixelizer.PixCollection[i].SetColor(adjustedColor);
                        }
                        break;
                }
            }

            pixelizer.Texturizer.Texturize();
            OnColorize?.Invoke();
        }

        private Color GetClosestColor(Color color, List<Color> colorizerColors, ReplacementStyle replacementStyle)
        {
            float hue, saturation, value;
            Color.RGBToHSV(color, out hue, out saturation, out value);

            float colorDifference = Mathf.Infinity;

            Color closestColor = Color.white;

            switch(replacementStyle)
            {
                case ReplacementStyle.ReplaceUsingHue:
                    foreach(var colorizerColor in colorizerColors)
                    {
                        Vector3 colorHue = new Vector3(color.r, color.g, color.b);
                        Vector3 colorizerColorHue = new Vector3(colorizerColor.r, colorizerColor.g, colorizerColor.b);

                        float difference = Vector3.Distance(colorHue, colorizerColorHue);

                        if(difference < colorDifference)
                        {
                            closestColor = colorizerColor;
                            colorDifference = difference;
                        }
                    }
                    break;

                case ReplacementStyle.ReplaceUsingSaturation:
                    foreach(var colorizerColor in colorizerColors)
                    {
                        float colorizerHue, colorizerSaturation, colorizerValue;
                        Color.RGBToHSV(colorizerColor, out colorizerHue, out colorizerSaturation, out colorizerValue);

                        float difference = Mathf.Abs(saturation - colorizerSaturation);

                        if(difference < colorDifference)
                        {
                            closestColor = colorizerColor;
                            colorDifference = difference;
                        }
                    }
                    break;
                case ReplacementStyle.ReplaceUsingValue:
                    foreach(var colorizerColor in colorizerColors)
                    {
                        float colorizerHue, colorizerSaturation, colorizerValue;
                        Color.RGBToHSV(colorizerColor, out colorizerHue, out colorizerSaturation, out colorizerValue);

                        float difference = Mathf.Abs(value - colorizerValue);

                        if(difference < colorDifference)
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
            foreach(var pix in pixelizer.PixCollection)
            {
                pixels.Add(pix.OriginalColor);
            }
            int pixelCount = pixels.Count;

            Color[] centroids = new Color[colorCount];
            for(int i = 0; i < colorCount; i++)
            {
                centroids[i] = pixels[Random.Range(0, pixelCount)];
            }

            for(int i = 0; i < iterationCount; i++)
            {
                int[] nearestCentroidIndices = new int[pixelCount];

                for(int j = 0; j < pixelCount; j++)
                {
                    float nearestDistance = float.MaxValue;
                    int nearestCentroidIndex = 0;
                    for(int k = 0; k < colorCount; k++)
                    {
                        Vector3 colorA = new Vector3(pixels[j].r, pixels[j].g, pixels[j].b);
                        Vector3 colorB = new Vector3(centroids[k].r, centroids[k].g, centroids[k].b);

                        float distance = Vector3.Distance(colorA, colorB);
                        if(distance < nearestDistance)
                        {
                            nearestDistance = distance;
                            nearestCentroidIndex = k;
                        }
                    }
                    nearestCentroidIndices[j] = nearestCentroidIndex;
                }

                for(int j = 0; j < colorCount; j++)
                {
                    var pixelsInCluster =
                        from pixelIndex in Enumerable.Range(0, pixelCount)
                        where nearestCentroidIndices[pixelIndex] == j
                        select pixels[pixelIndex];

                    if(pixelsInCluster.Any())
                    {
                        centroids[j] = new Color(pixelsInCluster.Average(c => c.r), pixelsInCluster.Average(c => c.g), pixelsInCluster.Average(c => c.b));
                    }
                }
            }

            return centroids.ToList();
        }

        public void CreateNewColorPalette()
        {
#if UNITY_EDITOR
            if(colorPaletteColorCount <= 0)
            {
                Debug.LogWarning("Color palette color count must be greater than 0");
                return;
            }

            List<Color> centroids = GetColorPalette(colorPaletteColorCount);

            ColorPalette newColorPalette = ScriptableObject.CreateInstance<ColorPalette>();

            foreach(var centroid in centroids)
            {
                newColorPalette.Colors.Add(centroid);
            }
            colorPalette = newColorPalette;
#endif
        }
        
        public void AddToColorPalette()
        {
            List<Color> centroids = GetColorPalette(colorPaletteColorCount);
            
            foreach(var centroid in centroids)
            {
                colorPalette.Colors.Add(centroid);
            }
        }

        public void SaveColorPalette()
        {
#if UNITY_EDITOR
            if(colorPalette == null)
            {
                Debug.LogWarning("Color palette is not assigned");
                return;
            }

            string path = UnityEditor.AssetDatabase.GenerateUniqueAssetPath("Assets/Pixelization/Colorizer/ScriptableObjects/ColorPalette_.asset");

            UnityEditor.AssetDatabase.CreateAsset(colorPalette, path);
#endif
        }

        private void MapColorPaletteColorsToColorGroupsColors()
        {
            List<List<Color>> colorPermutations = GetAllColorPermutations(colorPalette.Colors);

            int closestColorPermutationIndex = 0;
            float difference = Mathf.Infinity;

            for(int i = 0; i < colorPermutations.Count; i++)
            {
                float currentDifference = 0f;

                for(int j = 0; j < colorPalette.Colors.Count; j++)
                {
                    currentDifference += GetColorDifference(colorGroupsColors[j], colorPermutations[i][j]);
                }

                if(currentDifference < difference)
                {
                    difference = currentDifference;
                    closestColorPermutationIndex = i;
                }
            }

            sortedColorPaletteColors = colorPermutations[closestColorPermutationIndex];
        }

        private List<List<Color>> GetAllColorPermutations(List<Color> colors)
        {
            List<List<Color>> colorPermutations = new List<List<Color>>();

            if(colors.Count == 0)
            {
                colorPermutations.Add(new List<Color>());
                return colorPermutations;
            }

            Color firstElement = colors[0];
            List<Color> remainingList = colors.GetRange(1, colors.Count - 1);
            List<List<Color>> subPermutations = GetAllColorPermutations(remainingList);

            foreach(List<Color> permutation in subPermutations)
            {
                for(int i = 0; i <= permutation.Count; i++)
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
            switch(replacementStyle)
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
            foreach(var pix in pixelizer.PixCollection)
            {
                pix.ComplementColor();
            }

            pixelizer.Texturizer.Texturize();
            OnColorize?.Invoke();
        }

        public void InvertColors()
        {
            foreach(var pix in pixelizer.PixCollection)
            {
                pix.InvertColor();
            }

            pixelizer.Texturizer.Texturize();
            OnColorize?.Invoke();
        }

        public void ResetColors()
        {
            foreach(var pix in pixelizer.PixCollection)
            {
                pix.ResetColor();
            }

            pixelizer.Texturizer.Texturize();
            OnColorize?.Invoke();
        }

        #endregion

        #region Validation

        private void OnColorPaletteColorCountChanged()
        {
            colorPaletteColorCount = Mathf.Max(colorPaletteColorCount, 1);
        }

        #endregion
    }
}