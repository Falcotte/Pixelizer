using UnityEngine;

namespace AngryKoala.Pixelization
{
    public class Pix
    {
        public Pixelizer Pixelizer { get; set; }
        
        public Vector2Int Position { get; set; }
        
        public Color OriginalColor { get; set; }
        
        public Color Color { get; set; }
    }
}