using UnityEngine;

namespace AngryKoala.Pixelization
{
    public class Pix
    {
        public Pixelizer Pixelizer { get; set; }

        public Color OriginalColor { get; set; }

        public Color Color { get; set; }

        public void Reset()
        {
            Pixelizer = null;
            
            OriginalColor = Color.clear;
            Color = Color.clear;
        }
        
        public void ResetColor()
        {
            Color = OriginalColor;
        }

        public void ComplementColor()
        {
            float maxValue = 0f;
            float minValue = 1f;

            for (int i = 0; i < 3; i++)
            {
                if (Color[i] >= maxValue)
                {
                    maxValue = Color[i];
                }

                if (Color[i] <= minValue)
                {
                    minValue = Color[i];
                }
            }

            Color = new Color(maxValue + minValue - Color.r, maxValue + minValue - Color.g,
                maxValue + minValue - Color.b);
        }

        public void InvertColor()
        {
            Color = new Color(1 - Color.r, 1 - Color.g, 1 - Color.b);
        }
    }
}