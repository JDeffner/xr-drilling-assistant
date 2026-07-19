using System;
using System.Collections;
using UnityEngine;

namespace DrillingAssistant
{
    /// <summary>
    /// Cut + Fade transition between AR (passthrough) and VR: fade to black,
    /// apply the stage switch in one cut, fade back in. Uses OVRScreenFade on
    /// the CenterEyeAnchor.
    /// </summary>
    public class TransitionController : MonoBehaviour
    {
        public OVRPassthroughLayer PassthroughLayer;
        public OVRScreenFade ScreenFade;
        public Camera CenterCamera;

        [Tooltip("Fade duration per side (out and in), seconds.")]
        public float FadeDuration = 0.15f;

        public bool IsTransitioning { get; private set; }

        private static readonly Color VrBackground = new Color(0.05f, 0.06f, 0.1f, 1f);

        private void Start()
        {
            // OVRScreenFade only assigns its material on the first fade call;
            // until then its full-screen quad renders magenta (null material).
            SetFade(0f);
        }

        public void RunTransition(Action applyCut)
        {
            if (IsTransitioning) return;
            StartCoroutine(Transition(applyCut));
        }

        private IEnumerator Transition(Action applyCut)
        {
            IsTransitioning = true;
            yield return Fade(0f, 1f);
            try
            {
                applyCut?.Invoke();
            }
            catch (Exception e)
            {
                // The cut runs at full black; a throwing callback must never
                // strand the user there. Log it and fade back regardless.
                Debug.LogException(e);
            }
            yield return Fade(1f, 0f);
            IsTransitioning = false;
        }

        private IEnumerator Fade(float from, float to)
        {
            for (float t = 0f; t < FadeDuration; t += Time.deltaTime)
            {
                SetFade(Mathf.Lerp(from, to, t / FadeDuration));
                yield return null;
            }
            SetFade(to);
        }

        private void SetFade(float level)
        {
            if (ScreenFade != null) ScreenFade.SetExplicitFade(level);
        }

        /// <summary>The actual cut: passthrough + camera background.</summary>
        public void SetPassthrough(bool on)
        {
            if (PassthroughLayer != null) PassthroughLayer.enabled = on;
            if (CenterCamera != null)
            {
                CenterCamera.clearFlags = CameraClearFlags.SolidColor;
                CenterCamera.backgroundColor = on ? Color.clear : VrBackground;
            }
        }
    }
}
