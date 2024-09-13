using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AngryKoala.Pixelization
{
    [CreateAssetMenu(fileName = "ColorPalette", menuName = "AngryKoala/Colorizer/ColorPalette")]
    public class ColorPalette : ScriptableObject
    {
        [SerializeField] private List<Color> colors = new List<Color>();
        public List<Color> Colors => colors;

        public void SortColorsByHue()
        {
            colors = colors.OrderBy(color => color.Hue() < .5f ? color.Hue() : 1 - color.Hue()).ToList();
        }

        public void SortColorsBySaturation()
        {
            colors = colors.OrderBy(color => color.Saturation()).ToList();
        }

        public void SortColorsByValue()
        {
            colors = colors.OrderBy(color => color.Value()).ToList();
        }
    }
}