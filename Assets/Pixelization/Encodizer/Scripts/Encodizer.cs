using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Media;
using UnityEngine;
using UnityEngine.Events;

namespace AngryKoala.Pixelization
{
    public class Encodizer : MonoBehaviour
    {
        [SerializeField] private string framesPath;

        [SerializeField] private string videoName;
        [SerializeField] private string videoSavePath;

        private List<Texture2D> frames = new();

        public int FrameCount { get; private set; }
        public int CurrentFrame { get; private set; }

        public float Progress { get; private set; }

        public UnityAction OnProgressUpdate;
        public UnityAction OnComplete;

        public void Encodize()
        {
            Progress = 0f;

            CurrentFrame = 0;

            SetFrames();

            StartCoroutine(RecordMovie());
        }

        private void SetFrames()
        {
            frames.Clear();

            DirectoryInfo info = new DirectoryInfo(framesPath);
            FileInfo[] files = info.GetFiles("*.png", SearchOption.TopDirectoryOnly).OrderBy(p =>
                    int.Parse(p.Name.Substring(p.Name.IndexOf('_') + 1,
                        p.Name.Length - (p.Name.IndexOf('_') + 1) - 4)))
                .ToArray();

            foreach (var file in files)
            {
                 string path = file.FullName.Replace('\\', '/');
                
                 if (path.StartsWith(Application.dataPath))
                 {
                     path = "Assets" + path.Substring(Application.dataPath.Length);
                 }
                
                 Texture2D frame = (Texture2D)AssetDatabase.LoadAssetAtPath(path, typeof(Texture2D));
                 frames.Add(frame);
            }

            FrameCount = frames.Count;
        }

        private IEnumerator RecordMovie()
        {
            var videoAttr = new VideoTrackAttributes
            {
                frameRate = new MediaRational(24),
                width = 1920,
                height = 1080,
                includeAlpha = false,
                bitRateMode = VideoBitrateMode.High
            };

            string path =
                AssetDatabase.GenerateUniqueAssetPath(
                    $"{videoSavePath}/{videoName}.mp4");

            var encoder = new MediaEncoder(path, videoAttr);

            for (int i = 0; i < frames.Count; i++)
            {
                encoder.AddFrame(frames[i]);

                CurrentFrame = i;
                Progress = (float)i / frames.Count;

                OnProgressUpdate?.Invoke();

                yield return null;
            }
            
            ((IDisposable)encoder).Dispose();

            OnComplete?.Invoke();

            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        }
    }
}