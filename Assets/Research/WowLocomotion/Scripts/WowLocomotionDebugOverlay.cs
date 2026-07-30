using UnityEngine;
using UnityEngine.InputSystem;

namespace WowLocomotionResearch
{
    /// <summary>
    /// Optional OnGUI overlay for inspecting the WoW-like locomotion prototype state.
    /// </summary>
    public sealed class WowLocomotionDebugOverlay : MonoBehaviour
    {
        [Tooltip("Locomotion prototype to read debug state from.")]
        [SerializeField] private WowLikeAnimancerLocomotionPrototype locomotionPrototype;

        [Tooltip("Show or hide this overlay. Disable to remove the extra debug panel from the Game view.")]
        [SerializeField] private bool visible = true;

        [Tooltip("Keyboard key that toggles this overlay during Play Mode. Use None to disable runtime toggling.")]
        [SerializeField] private Key toggleKey = Key.F8;

        [Tooltip("Draw the original verbose text block. Disable if you only want the visual landing timelines.")]
        [SerializeField] private bool drawStateText = true;

        [Tooltip("Draw landing timelines and layer weights. These bars show lower-body landing and upper-body follow-through separately.")]
        [SerializeField] private bool drawLandingTimelines = true;

        [Tooltip("Top-left pixel position of the overlay. Lower X/Y moves it left/up; higher X/Y moves it right/down.")]
        [SerializeField] private Vector2 position = new Vector2(12f, 420f);

        [Tooltip("Pixel size of the overlay. Lower values make it smaller and may clip text; higher values give the debug text more room.")]
        [SerializeField] private Vector2 size = new Vector2(520f, 590f);

        [Tooltip("Pixel height of each timeline bar. Lower is more compact; higher makes progress/exit markers easier to read.")]
        [SerializeField] private float barHeight = 16f;

        [Tooltip("Normalized-time range shown by clip timelines. 1 shows the whole clip; higher values leave room to see overshoot past the exit marker.")]
        [SerializeField] private float timelineNormalizedRange = 1.0f;

        [Tooltip("Draw the panel background. Disable if the debug UI blocks too much of the Game view.")]
        [SerializeField] private bool drawBackground = true;

        [Tooltip("Panel background alpha. Lower is more transparent; higher hides more of the Game view behind the overlay.")]
        [SerializeField, Range(0f, 1f)] private float backgroundAlpha = 0.78f;

        private GUIStyle labelStyle;
        private GUIStyle smallLabelStyle;
        private GUIStyle headerStyle;

        private void Reset()
        {
            locomotionPrototype = GetComponent<WowLikeAnimancerLocomotionPrototype>();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || toggleKey == Key.None)
                return;

            if (keyboard[toggleKey].wasPressedThisFrame)
                visible = !visible;
        }

        private void OnGUI()
        {
            if (!visible || locomotionPrototype == null)
                return;

            EnsureStyles();

            var panelRect = new Rect(position, size);
            if (drawBackground)
                DrawRect(panelRect, new Color(0.03f, 0.035f, 0.04f, backgroundAlpha));

            GUILayout.BeginArea(panelRect);
            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            GUILayout.Space(10f);
            GUILayout.BeginVertical();

            if (drawStateText)
            {
                GUILayout.Label(locomotionPrototype.BuildDebugString(), labelStyle);
                GUILayout.Space(6f);
            }

            if (drawLandingTimelines)
                DrawLandingTimelinePanel(locomotionPrototype.BuildLandingDebugSnapshot());

            GUILayout.EndVertical();
            GUILayout.Space(10f);
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);
            GUILayout.EndArea();
        }

        private void DrawLandingTimelinePanel(WowLandingDebugSnapshot snapshot)
        {
            GUILayout.Label("Landing Timelines", headerStyle);
            GUILayout.Label(
                $"phase={snapshot.JumpPhase} grounded={snapshot.Grounded} moving={snapshot.Moving} landingMode={(snapshot.LandingStartedMoving ? "Moving" : "Standing")} autorun={snapshot.AutoRunActive}",
                smallLabelStyle);
            GUILayout.Label(
                $"raw={Format(snapshot.RawInput)} normalized={Format(snapshot.NormalizedInput)} mixer={Format(snapshot.VisualMixerParameter)} visualYaw={snapshot.VisualYaw:0.#}->{snapshot.TargetVisualYaw:0.#}",
                smallLabelStyle);

            GUILayout.Space(5f);
            DrawClipTimeline(
                "Lower body / base landing",
                snapshot.BaseStateName,
                snapshot.BaseNormalizedTime,
                snapshot.BaseExitNormalizedTime,
                snapshot.BaseSecondsRemaining,
                snapshot.BaseDuration,
                snapshot.BaseLayerWeight,
                new Color(0.23f, 0.54f, 1f, 1f));

            DrawClipTimeline(
                "Upper body follow-through",
                snapshot.LandingUpperStateName,
                snapshot.LandingUpperNormalizedTime,
                snapshot.LandingUpperExitNormalizedTime,
                snapshot.LandingUpperSecondsRemaining,
                snapshot.LandingUpperDuration,
                snapshot.LandingUpperLayerWeight,
                new Color(1f, 0.57f, 0.22f, 1f));

            GUILayout.Space(5f);
            DrawWeightBar("Base layer", snapshot.BaseLayerStateName, snapshot.BaseLayerWeight, new Color(0.24f, 0.72f, 1f, 1f));
            DrawWeightBar("Landing upper layer", snapshot.LandingUpperLayerStateName, snapshot.LandingUpperLayerWeight, new Color(1f, 0.66f, 0.24f, 1f));
            DrawWeightBar("Action upper layer", snapshot.UpperLayerStateName, snapshot.UpperLayerWeight, new Color(0.73f, 0.46f, 1f, 1f));
            GUILayout.Label($"locomotion={snapshot.LocomotionStateName}", smallLabelStyle);
        }

        private void DrawClipTimeline(
            string label,
            string stateName,
            float normalizedTime,
            float exitNormalizedTime,
            float secondsRemaining,
            float configuredDuration,
            float layerWeight,
            Color fillColor)
        {
            GUILayout.Label(
                $"{label}: {stateName}  n={FormatTime(normalizedTime)} exit={exitNormalizedTime:0.###} remaining={Mathf.Max(0f, secondsRemaining):0.###}s duration={configuredDuration:0.###}s weight={FormatWeight(layerWeight)}",
                smallLabelStyle);

            var rect = GUILayoutUtility.GetRect(Mathf.Max(1f, size.x - 28f), barHeight);
            DrawTimeline(rect, normalizedTime, exitNormalizedTime, fillColor);
            GUILayout.Space(4f);
        }

        private void DrawWeightBar(string label, string stateName, float weight, Color fillColor)
        {
            GUILayout.Label($"{label}: {stateName}  weight={FormatWeight(weight)}", smallLabelStyle);
            var rect = GUILayoutUtility.GetRect(Mathf.Max(1f, size.x - 28f), barHeight);
            DrawProgressBar(rect, Mathf.Clamp01(weight), fillColor);
            GUILayout.Space(3f);
        }

        private void DrawTimeline(Rect rect, float normalizedTime, float exitNormalizedTime, Color fillColor)
        {
            var range = Mathf.Max(0.1f, timelineNormalizedRange);
            var progress = normalizedTime >= 0f ? Mathf.Clamp01(normalizedTime / range) : 0f;
            var marker = Mathf.Clamp01(exitNormalizedTime / range);

            DrawProgressBar(rect, progress, fillColor);

            var markerRect = new Rect(rect.x + rect.width * marker - 1f, rect.y - 2f, 2f, rect.height + 4f);
            DrawRect(markerRect, Color.white);
        }

        private static void DrawProgressBar(Rect rect, float progress, Color fillColor)
        {
            DrawRect(rect, new Color(0.11f, 0.12f, 0.13f, 0.95f));
            DrawRect(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(progress), rect.height), fillColor);
            DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), new Color(1f, 1f, 1f, 0.25f));
            DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), new Color(0f, 0f, 0f, 0.45f));
        }

        private static void DrawRect(Rect rect, Color color)
        {
            var previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        private void EnsureStyles()
        {
            if (labelStyle != null)
                return;

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 12,
                normal = { textColor = new Color(0.88f, 0.91f, 0.95f, 1f) },
                wordWrap = false
            };

            smallLabelStyle = new GUIStyle(labelStyle)
            {
                fontSize = 11
            };

            headerStyle = new GUIStyle(labelStyle)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 13,
                normal = { textColor = new Color(1f, 1f, 1f, 1f) }
            };
        }

        private static string Format(Vector2 value)
        {
            return $"({value.x:0.###}, {value.y:0.###})";
        }

        private static string FormatTime(float normalizedTime)
        {
            return normalizedTime >= 0f ? normalizedTime.ToString("0.###") : "none";
        }

        private static string FormatWeight(float weight)
        {
            return weight >= 0f ? weight.ToString("0.###") : "none";
        }
    }

    /// <summary>
    /// Read-only landing and layer data used by runtime debug visualizers.
    /// </summary>
    public readonly struct WowLandingDebugSnapshot
    {
        /// <summary>Current jump state name used by the locomotion prototype.</summary>
        public readonly string JumpPhase;

        /// <summary>True when the character is currently grounded.</summary>
        public readonly bool Grounded;

        /// <summary>True when movement input is currently active.</summary>
        public readonly bool Moving;

        /// <summary>True when the active landing sequence has switched to moving landing tuning.</summary>
        public readonly bool LandingStartedMoving;

        /// <summary>True when autorun is currently active.</summary>
        public readonly bool AutoRunActive;

        /// <summary>Raw movement input before normalization.</summary>
        public readonly Vector2 RawInput;

        /// <summary>Normalized movement input after diagonal-speed handling.</summary>
        public readonly Vector2 NormalizedInput;

        /// <summary>Current visual locomotion mixer parameter.</summary>
        public readonly Vector2 VisualMixerParameter;

        /// <summary>Name of the tracked base landing state.</summary>
        public readonly string BaseStateName;

        /// <summary>Normalized time of the tracked base landing state.</summary>
        public readonly float BaseNormalizedTime;

        /// <summary>Current base layer weight.</summary>
        public readonly float BaseLayerWeight;

        /// <summary>Seconds remaining before the base landing state exits by duration.</summary>
        public readonly float BaseSecondsRemaining;

        /// <summary>Configured duration currently driving the base landing state.</summary>
        public readonly float BaseDuration;

        /// <summary>Normalized clip time where the base landing state should exit.</summary>
        public readonly float BaseExitNormalizedTime;

        /// <summary>Name of the current base layer state, whether landing or locomotion.</summary>
        public readonly string BaseLayerStateName;

        /// <summary>Name of the tracked upper-body landing follow-through state.</summary>
        public readonly string LandingUpperStateName;

        /// <summary>Normalized time of the tracked upper-body landing follow-through state.</summary>
        public readonly float LandingUpperNormalizedTime;

        /// <summary>Current landing upper-body layer weight.</summary>
        public readonly float LandingUpperLayerWeight;

        /// <summary>Seconds remaining before the upper-body landing follow-through exits by duration.</summary>
        public readonly float LandingUpperSecondsRemaining;

        /// <summary>Configured duration currently driving the upper-body landing follow-through.</summary>
        public readonly float LandingUpperDuration;

        /// <summary>Normalized clip time where the upper-body landing follow-through should exit.</summary>
        public readonly float LandingUpperExitNormalizedTime;

        /// <summary>Name of the current landing upper-body layer state.</summary>
        public readonly string LandingUpperLayerStateName;

        /// <summary>Name of the current regular upper-body action layer state.</summary>
        public readonly string UpperLayerStateName;

        /// <summary>Current regular upper-body action layer weight.</summary>
        public readonly float UpperLayerWeight;

        /// <summary>Name of the tracked locomotion mixer state.</summary>
        public readonly string LocomotionStateName;

        /// <summary>Current visual yaw offset applied to the model root.</summary>
        public readonly float VisualYaw;

        /// <summary>Target visual yaw offset being smoothed toward.</summary>
        public readonly float TargetVisualYaw;

        /// <summary>Creates a read-only landing debug snapshot.</summary>
        public WowLandingDebugSnapshot(
            string jumpPhase,
            bool grounded,
            bool moving,
            bool landingStartedMoving,
            bool autoRunActive,
            Vector2 rawInput,
            Vector2 normalizedInput,
            Vector2 visualMixerParameter,
            string baseStateName,
            float baseNormalizedTime,
            float baseLayerWeight,
            float baseSecondsRemaining,
            float baseDuration,
            float baseExitNormalizedTime,
            string baseLayerStateName,
            string landingUpperStateName,
            float landingUpperNormalizedTime,
            float landingUpperLayerWeight,
            float landingUpperSecondsRemaining,
            float landingUpperDuration,
            float landingUpperExitNormalizedTime,
            string landingUpperLayerStateName,
            string upperLayerStateName,
            float upperLayerWeight,
            string locomotionStateName,
            float visualYaw,
            float targetVisualYaw)
        {
            JumpPhase = jumpPhase;
            Grounded = grounded;
            Moving = moving;
            LandingStartedMoving = landingStartedMoving;
            AutoRunActive = autoRunActive;
            RawInput = rawInput;
            NormalizedInput = normalizedInput;
            VisualMixerParameter = visualMixerParameter;
            BaseStateName = baseStateName;
            BaseNormalizedTime = baseNormalizedTime;
            BaseLayerWeight = baseLayerWeight;
            BaseSecondsRemaining = baseSecondsRemaining;
            BaseDuration = baseDuration;
            BaseExitNormalizedTime = baseExitNormalizedTime;
            BaseLayerStateName = baseLayerStateName;
            LandingUpperStateName = landingUpperStateName;
            LandingUpperNormalizedTime = landingUpperNormalizedTime;
            LandingUpperLayerWeight = landingUpperLayerWeight;
            LandingUpperSecondsRemaining = landingUpperSecondsRemaining;
            LandingUpperDuration = landingUpperDuration;
            LandingUpperExitNormalizedTime = landingUpperExitNormalizedTime;
            LandingUpperLayerStateName = landingUpperLayerStateName;
            UpperLayerStateName = upperLayerStateName;
            UpperLayerWeight = upperLayerWeight;
            LocomotionStateName = locomotionStateName;
            VisualYaw = visualYaw;
            TargetVisualYaw = targetVisualYaw;
        }
    }
}
