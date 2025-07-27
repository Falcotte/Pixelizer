using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AngryKoala.Pixelization
{
    [CreateAssetMenu(fileName = "ColorPalette", menuName = "AngryKoala/Colorizer/ColorPalette")]
    public class ColorPalette : ScriptableObject
    {
        [SerializeField] private List<Color> _colors = new List<Color>();
        public List<Color> Colors => _colors;

        public void SortColorsByHue()
        {
            _colors = _colors.OrderBy(color => color.Hue() < .5f ? color.Hue() : 1 - color.Hue()).ToList();
        }

        public void SortColorsBySaturation()
        {
            _colors = _colors.OrderBy(color => color.Saturation()).ToList();
        }

        public void SortColorsByValue()
        {
            _colors = _colors.OrderBy(color => color.Value()).ToList();
        }
    }
}