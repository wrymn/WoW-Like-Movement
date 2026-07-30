using UnityEngine;
using UnityEngine.InputSystem;

namespace WowLocomotionResearch
{
    /// <summary>
    /// Runtime OnGUI overlay that shows player frame rate and exposes a VSync toggle in builds.
    /// </summary>
    public sealed class WowRuntimePerformanceOverlay : MonoBehaviour
    {
        private const string VSyncPlayerPrefsKey = "WowLocomotion.VSyncEnabled";

        [Tooltip("Show or hide the runtime performance overlay.")]
        [SerializeField] private bool visible = true;

        [Tooltip("Keyboard key that toggles this overlay at runtime. Use None to leave it always controlled by the Visible field.")]
        [SerializeField] private Key toggleKey = Key.F1;

        [Tooltip("Keyboard key that toggles VSync at runtime. Use None to disable keyboard VSync toggling.")]
        [SerializeField] private Key vSyncToggleKey = Key.F2;

        [Tooltip("Top-left pixel position of the overlay.")]
        [SerializeField] private Vector2 position = new Vector2(12f, 12f);

        [Tooltip("Pixel size of the overlay.")]
        [SerializeField] private Vector2 size = new Vector2(190f, 104f);

        [Tooltip("Seconds used to smooth displayed FPS. Lower values react faster; higher values are steadier.")]
        [SerializeField] private float fpsSmoothingSeconds = 0.25f;

        private GUIStyle labelStyle;
        private float smoothedDeltaTime;

        /// <summary>True when the performance overlay is currently visible.</summary>
        public bool Visible
        {
            get { return visible; }
            set { visible = value; }
        }

        /// <summary>True when runtime VSync is currently enabled.</summary>
        public bool VSyncEnabled
        {
            get { return QualitySettings.vSyncCount > 0; }
        }

        /// <summary>Enables or disables VSync for the active quality level and stores the player preference.</summary>
        /// <param name="enabled">True to enable VSync; false to run uncapped by VSync.</param>
        public void SetVSyncEnabled(bool enabled)
        {
            QualitySettings.vSyncCount = enabled ? 1 : 0;
            Application.targetFrameRate = -1;
            PlayerPrefs.SetInt(VSyncPlayerPrefsKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void Awake()
        {
            smoothedDeltaTime = Time.unscaledDeltaTime;
            if (PlayerPrefs.HasKey(VSyncPlayerPrefsKey))
                SetVSyncEnabled(PlayerPrefs.GetInt(VSyncPlayerPrefsKey) != 0);
        }

        private void Update()
        {
            UpdateFps();
            UpdateToggleInput();
        }

        private void OnGUI()
        {
            if (!visible)
                return;

            EnsureStyles();

            var previousDepth = GUI.depth;
            GUI.depth = -1000;

            var rect = new Rect(position, size);
            DrawRect(rect, new Color(0.03f, 0.035f, 0.04f, 0.78f));

            var contentX = rect.x + 10f;
            var contentY = rect.y + 8f;
            var contentWidth = rect.width - 20f;
            const float lineHeight = 20f;

            GUI.Label(new Rect(contentX, contentY, contentWidth, lineHeight), $"FPS: {GetFramesPerSecond():0}", labelStyle);
            GUI.Label(new Rect(contentX, contentY + lineHeight, contentWidth, lineHeight), $"Frame: {GetFrameMilliseconds():0.0} ms", labelStyle);
            GUI.Label(new Rect(contentX, contentY + lineHeight * 2f, contentWidth, lineHeight), VSyncEnabled ? "VSync: On" : "VSync: Off", labelStyle);
            GUI.Label(new Rect(contentX, contentY + lineHeight * 3f, contentWidth, lineHeight), "F1: Panel   F2: VSync", labelStyle);

            GUI.depth = previousDepth;
        }

        private void UpdateFps()
        {
            var deltaTime = Time.unscaledDeltaTime;
            if (deltaTime <= 0f)
                return;

            if (smoothedDeltaTime <= 0f)
            {
                smoothedDeltaTime = deltaTime;
                return;
            }

            var smoothingSeconds = Mathf.Max(0.0001f, fpsSmoothingSeconds);
            var alpha = 1f - Mathf.Exp(-deltaTime / smoothingSeconds);
            smoothedDeltaTime = Mathf.Lerp(smoothedDeltaTime, deltaTime, alpha);
        }

        private static void DrawRect(Rect rect, Color color)
        {
            var previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        private void UpdateToggleInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || toggleKey == Key.None)
                return;

            if (toggleKey != Key.None && keyboard[toggleKey].wasPressedThisFrame)
                visible = !visible;

            if (vSyncToggleKey != Key.None && keyboard[vSyncToggleKey].wasPressedThisFrame)
                SetVSyncEnabled(!VSyncEnabled);
        }

        private float GetFramesPerSecond()
        {
            return smoothedDeltaTime > 0f ? 1f / smoothedDeltaTime : 0f;
        }

        private float GetFrameMilliseconds()
        {
            return smoothedDeltaTime * 1000f;
        }

        private void EnsureStyles()
        {
            if (labelStyle != null)
                return;

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = new Color(0.92f, 0.95f, 1f, 1f) }
            };
        }
    }
}
