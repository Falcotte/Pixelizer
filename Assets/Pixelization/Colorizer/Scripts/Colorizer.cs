using System.Collections.Generic;
using UnityEngine;

namespace AngryKoala.Pixelization
{
    public class Colorizer : MonoBehaviour
    {
        [SerializeField] private Pixelizer _pixelizer;

        [SerializeField] private ColorPalette _colorPalette;
        public ColorPalette ColorPalette => _colorPalette;

        private enum ColorizationStyle
        {
            Replace,
            ReplaceWithOriginalSaturation,
            ReplaceWithOriginalValue
        }

        [SerializeField] private ColorizationStyle _colorizationStyle;

        private enum ReplacementStyle
        {
            ReplaceUsingHue,
            ReplaceUsingSaturation,
            ReplaceUsingValue
        }

        [SerializeField] private ReplacementStyle _replacementStyle;

        public void Colorize()
        {
            if (_pixelizer.PixCollection.Length == 0)
            {
                Debug.LogWarning("Pixelize a texture first");
                return;
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

            for (int i = 0; i < _pixelizer.PixCollection.Length; i++)
            {
                switch (_colorizationStyle)
                {
                    case ColorizationStyle.Replace:
                        Color closestColor = GetClosestColor(_pixelizer.PixCollection[i].Color,
                            _colorPalette.Colors, _replacementStyle);

                        _pixelizer.PixCollection[i].Color = closestColor;
                        break;

                    case ColorizationStyle.ReplaceWithOriginalSaturation:
                    {
                        Color originalColor = _pixelizer.PixCollection[i].Color;
                        Color adjustedColor;

                        adjustedColor = GetClosestColor(originalColor, _colorPalette.Colors, _replacementStyle);

                        float hue, saturation, value;
                        Color.RGBToHSV(adjustedColor, out hue, out saturation, out value);

                        float originalSaturation = originalColor.Saturation();

                        adjustedColor = Color.HSVToRGB(hue, originalSaturation, value);

                        _pixelizer.PixCollection[i].Color = adjustedColor;
                    }
                        break;

                    case ColorizationStyle.ReplaceWithOriginalValue:
                    {
                        Color originalColor = _pixelizer.PixCollection[i].Color;
                        Color adjustedColor;
                        
                        adjustedColor = GetClosestColor(originalColor, _colorPalette.Colors, _replacementStyle);

                        float hue, saturation, value;
                        Color.RGBToHSV(adjustedColor, out hue, out saturation, out value);

                        float originalValue = originalColor.Value();
                        
                        adjustedColor = Color.HSVToRGB(hue, saturation, originalValue);

                        _pixelizer.PixCollection[i].Color = adjustedColor;
                    }
                        break;
                }
            }

            _pixelizer.Texturizer.Texturize();
            _pixelizer.SetPixTextures();
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
        
        public void ResetColors()
        {
            foreach (var pix in _pixelizer.PixCollection)
            {
                pix.ResetColor();
            }

            _pixelizer.Texturizer.Texturize();
            _pixelizer.SetPixTextures();
        }
    }
}