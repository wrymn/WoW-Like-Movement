using Sirenix.OdinInspector;
using UnityEngine;

namespace WowLocomotionResearch
{
    /// <summary>
    /// Canonical runtime settings for the WoW-like third-person camera rig.
    /// </summary>
    [CreateAssetMenu(
        fileName = "WowCameraSettings",
        menuName = "Research/WoW Locomotion/Camera Settings")]
    [Searchable]
    public sealed class WowCameraSettings : ScriptableObject
    {
        [BoxGroup("Follow Pivot")]
        [Tooltip("How long the orbit pivot takes to catch up to the root plus cached pivot offset. Lower values follow more instantly; higher values lag/smooth more. Use 0 for exact WoW-like immediate follow.")]
        [SerializeField] private float followSmoothness;

        [BoxGroup("Zoom")]
        [Tooltip("Default distance in meters from the stable character pivot to the camera. Lower values zoom closer; higher values zoom farther away.")]
        [SerializeField] private float cameraDistance = 5f;

        [BoxGroup("Follow Pivot")]
        [Tooltip("Pivot height above the character root when no sampled pivot or head bone is found. Lower values orbit chest/waist; higher values orbit above the head.")]
        [SerializeField] private float fallbackPivotHeight = 1.6f;

        [BoxGroup("Follow Pivot")]
        [Tooltip("Target-local offset added after the initial head/pivot sample. Positive Y aims/orbits higher; negative Y lowers it. X shifts side-to-side; Z shifts forward/back relative to the character root.")]
        [SerializeField] private Vector3 pivotOffset = new Vector3(0f, 0.1f, 0f);

        [BoxGroup("Orbit Framing")]
        [Tooltip("Camera field of view in degrees. Lower values feel zoomed-in/narrow; higher values show more peripheral view and feel faster.")]
        [SerializeField] private float fieldOfView = 90f;

        [BoxGroup("Orbit Framing")]
        [Tooltip("Base orbit rotation in degrees. X is pitch: lower looks more upward/level, higher looks more downward. Y is starting yaw. Runtime RMB input adds on top.")]
        [SerializeField] private Vector3 cameraRotation = new Vector3(55f, 0f, 0f);

        [BoxGroup("Zoom")]
        [Tooltip("Closest allowed zoom distance. Lower lets the camera zoom closer to the head; higher prevents close zoom.")]
        [SerializeField] private float minCameraDistance = 3f;

        [BoxGroup("Zoom")]
        [Tooltip("Farthest allowed zoom distance. Lower caps the camera closer; higher allows farther zoom-out.")]
        [SerializeField] private float maxCameraDistance = 18f;

        [BoxGroup("Zoom")]
        [Tooltip("Zoom sensitivity per scroll delta unit. Lower scrolls zoom more slowly; higher scrolls zoom faster.")]
        [SerializeField] private float zoomMetersPerScrollPoint = 0.01f;

        [BoxGroup("Mouse Look")]
        [Tooltip("Raw yaw degrees per Unity mouse delta unit before the speed multiplier. Lower turns slower; higher turns faster. Keep this at 0.125 when using the WoW calibration multiplier.")]
        [SerializeField] private float yawDegreesPerPoint = 0.125f;

        [BoxGroup("Mouse Look")]
        [Tooltip("Raw pitch degrees per Unity mouse delta unit before the speed multiplier. Lower pitches slower; higher pitches faster. WoW-like default is half the yaw value.")]
        [SerializeField] private float pitchDegreesPerPoint = 0.0625f;

        [BoxGroup("Mouse Look")]
        [Tooltip("Global mouse sensitivity calibration. Lower makes RMB camera drag slower; higher makes it faster. Default maps measured Unity mouse deltas to WoW-like rotation.")]
        [SerializeField] private float cameraSpeedMultiplier = 4.5247445f;

        [BoxGroup("Orbit Framing")]
        [Tooltip("Pitch clamp in degrees. X is the lower/upward limit; Y is the upper/downward limit. Wider range allows more vertical camera rotation.")]
        [SerializeField] private Vector2 pitchLimits = new Vector2(-20f, 75f);

        [BoxGroup("Zoom")]
        [Tooltip("Smooth time for zoom changes after mouse wheel input. 0 applies zoom instantly; higher values make zoom glide more slowly.")]
        [SerializeField] private float zoomSmoothTime = 0.08f;

        [BoxGroup("Mouse Look")]
        [Tooltip("When enabled, holding LMB rotates only the camera around the character. Character facing is not changed by LMB.")]
        [SerializeField] private bool leftMouseLookEnabled = true;

        [BoxGroup("Yaw Auto Follow")]
        [Tooltip("How quickly camera yaw returns behind the character root while the character is moving and no mouse-look button is held. 0 disables auto-follow; higher values recenter faster.")]
        [SerializeField] private float yawFollowDegreesPerSecond = 120f;

        [BoxGroup("Yaw Auto Follow")]
        [Tooltip("Smooth time used near the end of yaw auto-follow. 0 keeps a constant-speed hard stop; higher values ease into the final behind-character angle more softly.")]
        [SerializeField] private float yawFollowSmoothTime = 0.12f;

        [BoxGroup("Yaw Auto Follow")]
        [Tooltip("Minimum horizontal root speed in meters per second before yaw auto-follow starts. Lower follows on tiny motion; higher ignores small jitter.")]
        [SerializeField] private float yawFollowMovementThreshold = 0.02f;

        [BoxGroup("Mouse Look")]
        [Tooltip("When enabled, mouse-look rotation hides and locks the cursor for RMB and LMB look. Disable to keep the cursor visible/free while rotating.")]
        [SerializeField] private bool lockCursorOnRotate = true;

        [BoxGroup("Mouse Look")]
        [Tooltip("Number of frames to ignore mouse delta after RMB starts rotating. Lower responds sooner but may include cursor-capture spikes; higher removes click-start snaps at the cost of a tiny delay.")]
        [SerializeField] private int cursorCaptureWarmupFrames = 2;

        /// <summary>Seconds of smoothing used while following the stable target pivot. Zero means instant follow.</summary>
        public float FollowSmoothness
        {
            get { return followSmoothness; }
        }

        /// <summary>Base camera distance behind the stable orbit pivot in meters.</summary>
        public float CameraDistance
        {
            get { return cameraDistance; }
        }

        /// <summary>Fallback local height used when no explicit pivot or target head bone can be resolved.</summary>
        public float FallbackPivotHeight
        {
            get { return fallbackPivotHeight; }
        }

        /// <summary>Target-local offset added after resolving the stable pivot sample.</summary>
        public Vector3 PivotOffset
        {
            get { return pivotOffset; }
        }

        /// <summary>Camera field of view applied every frame.</summary>
        public float FieldOfView
        {
            get { return fieldOfView; }
        }

        /// <summary>Base orbit rotation in degrees. Runtime mouse input is applied as an offset from this value.</summary>
        public Vector3 CameraRotation
        {
            get { return cameraRotation; }
        }

        /// <summary>Minimum zoom distance in meters.</summary>
        public float MinCameraDistance
        {
            get { return minCameraDistance; }
        }

        /// <summary>Maximum zoom distance in meters.</summary>
        public float MaxCameraDistance
        {
            get { return maxCameraDistance; }
        }

        /// <summary>Meters of zoom change per Unity Input System scroll delta unit.</summary>
        public float ZoomMetersPerScrollPoint
        {
            get { return zoomMetersPerScrollPoint; }
        }

        /// <summary>Smooth time used when applying zoom distance changes.</summary>
        public float ZoomSmoothTime
        {
            get { return zoomSmoothTime; }
        }

        /// <summary>True to let LMB rotate the camera without steering the character.</summary>
        public bool LeftMouseLookEnabled
        {
            get { return leftMouseLookEnabled; }
        }

        /// <summary>Degrees per second used to recenter camera yaw behind the moving character root.</summary>
        public float YawFollowDegreesPerSecond
        {
            get { return yawFollowDegreesPerSecond; }
        }

        /// <summary>Smooth time used to ease yaw auto-follow into its target angle.</summary>
        public float YawFollowSmoothTime
        {
            get { return yawFollowSmoothTime; }
        }

        /// <summary>Horizontal root speed required before camera yaw follow starts.</summary>
        public float YawFollowMovementThreshold
        {
            get { return yawFollowMovementThreshold; }
        }

        /// <summary>Raw yaw degrees per Unity mouse delta unit before <see cref="CameraSpeedMultiplier"/> is applied.</summary>
        public float YawDegreesPerPoint
        {
            get { return yawDegreesPerPoint; }
        }

        /// <summary>Raw pitch degrees per Unity mouse delta unit before <see cref="CameraSpeedMultiplier"/> is applied.</summary>
        public float PitchDegreesPerPoint
        {
            get { return pitchDegreesPerPoint; }
        }

        /// <summary>Calibration multiplier that converts the raw WoW mouse-point scalar to Unity Input System delta units.</summary>
        public float CameraSpeedMultiplier
        {
            get { return cameraSpeedMultiplier; }
        }

        /// <summary>Minimum and maximum pitch angles in degrees.</summary>
        public Vector2 PitchLimits
        {
            get { return pitchLimits; }
        }

        /// <summary>True to hide and lock the cursor while the right mouse button rotates the camera.</summary>
        public bool LockCursorOnRotate
        {
            get { return lockCursorOnRotate; }
        }

        /// <summary>Frames of mouse delta ignored after cursor capture starts.</summary>
        public int CursorCaptureWarmupFrames
        {
            get { return cursorCaptureWarmupFrames; }
        }
    }
}
