using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
// using Unity.Collections;
using UnityEditor;
using UnityEditor.Media;
using UnityEngine;

namespace AngryKoala.Pixelization
{
    public class Encodizer : MonoBehaviour
    {
        [SerializeField] private string framesPath;

        [SerializeField] private string videoName;
        [SerializeField] private string videoSavePath;

        private List<Texture2D> frames = new();

        public void Encodize()
        {
            SetFrames();

            StartCoroutine(RecordMovie());
        }

        private void SetFrames()
        {
            frames.Clear();

            DirectoryInfo info = new DirectoryInfo(framesPath);
            FileInfo[] files = info.GetFiles("*.png", SearchOption.TopDirectoryOnly).OrderBy(p => p.CreationTime)
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

            var audioAttr = new AudioTrackAttributes
            {
                sampleRate = new MediaRational(48000),
                channelCount = 2
                // language = "en"
            };

            int sampleFramesPerVideoFrame = audioAttr.channelCount *
                audioAttr.sampleRate.numerator / videoAttr.frameRate.numerator;

            string path =
                AssetDatabase.GenerateUniqueAssetPath(
                    $"{videoSavePath}/{videoName}.mp4");

            var encoder = new MediaEncoder(path, videoAttr, audioAttr);
            // var audioBuffer = new NativeArray<float>(sampleFramesPerVideoFrame, Allocator.Persistent);

            for (int i = 0; i < frames.Count; i++)
            {
                Texture2D frame = new Texture2D((int)videoAttr.width, (int)videoAttr.height, TextureFormat.RGBA32,
                    false);
                Color[] colors = frames[i].GetPixels();

                frame.SetPixels(colors);
                frame.Apply(false);

                encoder.AddFrame(frame);

                // encoder.AddSamples(audioBuffer);

                yield return null;
            }

            if (encoder != null)
                ((IDisposable)encoder).Dispose();

            // if (audioBuffer != null)
            //     ((IDisposable)audioBuffer).Dispose();
            
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        }
    }
}