using System.Collections.Generic;
using UnityEngine;

namespace AngryKoala.Pixelization
{
    public class PixPool : MonoBehaviour
    {
        [SerializeField] private int _initialSize = 1048576;
        
        private Queue<Pix> _pixQueue;
        
        private void Awake()
        {
            Initialize();
        }

        private void Initialize()
        {
            _pixQueue = new(_initialSize);
            
            for (int i = 0; i < _initialSize; i++)
            {
                Add();
            }
        }

        private void Add()
        {
            Pix pix = new();
                
            _pixQueue.Enqueue(pix);
        }

        public Pix Get()
        {
            if (_pixQueue.Count == 0)
            {
                Add();
            }
            
            return _pixQueue.Dequeue();
        }
        
        public void Return(Pix pix)
        {
            _pixQueue.Enqueue(pix);
        }
    }
}

