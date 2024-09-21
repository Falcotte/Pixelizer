using System.Collections;
using System.IO;
using NaughtyAttributes;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;

namespace AngryKoala.Pixelization
{
    public class Framizer : MonoBehaviour
    {
        [SerializeField] private Pixelizer pixelizer;

        [SerializeField] private VideoPlayer videoPlayer;

        [SerializeField] private bool customStartFrame;

        [SerializeField] [ShowIf("customStartFrame")] [OnValueChanged("OnFrameChanged")]
        private int startFrame;

        [SerializeField] private bool customEndFrame;

        [SerializeField] [ShowIf("customEndFrame")] [OnValueChanged("OnFrameChanged")]
        private int endFrame;

        [SerializeField] private int frameRate;

        [SerializeField] private string frameSavePath;

        private Texture2D[] textures;
        private int currentFrame;

        private bool isSeeking;

        private void Start()
        {
            RenderTexture.active = videoPlayer.targetTexture;

            videoPlayer.sendFrameReadyEvents = true;
            videoPlayer.Pause();
        }

        public void Framize()
        {
            if (videoPlayer.clip == null)
            {
                Debug.LogWarning("No video clip found to framize");
                return;
            }

            if (startFrame < 1 || endFrame > (int)videoPlayer.frameCount)
            {
                Debug.LogWarning("Target frames out of bounds");
                return;
            }

            currentFrame = customStartFrame ? startFrame : 1;
            endFrame = customEndFrame ? endFrame : (int)videoPlayer.frameCount;

            if (!AssetDatabase.IsValidFolder(frameSavePath))
            {
                Debug.LogWarning("Save path is not valid");
                return;
            }

            videoPlayer.frame = currentFrame;
            StartCoroutine(WaitForStartFrame());
        }

        private IEnumerator WaitForStartFrame()
        {
            yield return new WaitForSeconds(0.2f);
            
            UpdateVideoPlayerToFrame();
        }

        private void UpdateVideoPlayerToFrame()
        {
            if (isSeeking)
                return;

            videoPlayer.seekCompleted += SeekCompleted;

            videoPlayer.frame = currentFrame;
            isSeeking = true;
        }

        private IEnumerator WaitToUpdateRenderTextureBeforeEndingSeek()
        {
            yield return new WaitUntil(() => !isSeeking);
            yield return new WaitForEndOfFrame();

            if (currentFrame < endFrame)
            {
                SaveFrame();

                currentFrame += frameRate;
                currentFrame = Mathf.Clamp(currentFrame, 0, (int)videoPlayer.clip.frameCount);

                UpdateVideoPlayerToFrame();
            }

            videoPlayer.seekCompleted -= SeekCompleted;
        }

        private void SaveFrame()
        {
            Texture2D texture =
                new Texture2D(videoPlayer.texture.width, videoPlayer.texture.height,
                    TextureFormat.RGB24, false);

            texture.ReadPixels(new Rect(0, 0, videoPlayer.texture.width, videoPlayer.texture.height), 0,
                0, false);
            texture.Apply();

            string path =
                AssetDatabase.GenerateUniqueAssetPath(
                    $"{frameSavePath}/Frame_{currentFrame}.png");

                byte[] bytes = texture.EncodeToPNG();
                File.WriteAllBytes(path, bytes);

                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

                TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);

                importer.textureType = TextureImporterType.Default;

                TextureImporterSettings importerSettings = new TextureImporterSettings();
                importer.ReadTextureSettings(importerSettings);

                importerSettings.npotScale = TextureImporterNPOTScale.None;

                importer.SetTextureSettings(importerSettings);

                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
        }

        private void SeekCompleted(VideoPlayer videoPlayer)
        {
            isSeeking = false;
            StartCoroutine(WaitToUpdateRenderTextureBeforeEndingSeek());
        }

        #region Validation

        private void OnFrameChanged()
        {
            startFrame = Mathf.Max(startFrame, 1);
            endFrame = Mathf.Max(endFrame, 1);

            if (videoPlayer == null || videoPlayer.clip == null)
            {
                return;
            }

            startFrame = Mathf.Clamp(startFrame, 1, (int)videoPlayer.clip.frameCount - 1);
            endFrame = Mathf.Clamp(endFrame, startFrame + 1, (int)videoPlayer.clip.frameCount);
        }

        #endregion
    }
}