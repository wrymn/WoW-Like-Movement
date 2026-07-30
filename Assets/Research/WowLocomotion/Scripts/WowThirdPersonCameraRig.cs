using UnityEngine;
using UnityEngine.InputSystem;

namespace WowLocomotionResearch
{
    /// <summary>
    /// WoW-like third-person camera rig that follows a stable character-root pivot and orbits from raw mouse-look delta.
    /// </summary>
    public sealed class WowThirdPersonCameraRig : MonoBehaviour
    {
        [Tooltip("Canonical camera tuning asset read every frame. Editing this asset during Play Mode changes camera behavior immediately.")]
        [SerializeField] private WowCameraSettings settings;

        [Tooltip("Character root to follow. Drag HumanCharacter here; the rig samples bone_Head once to calculate stable height while keeping X/Z centered on this root.")]
        [SerializeField] private Transform targetRoot;

        [Tooltip("Optional pivot sampled once for height instead of auto-finding bone_Head. X/Z still stay centered on Target Root unless Pivot Offset shifts them.")]
        [SerializeField] private Transform explicitPivot;

        [Tooltip("Camera moved by this rig. Usually the child Camera in WowCameraRig.prefab.")]
        [SerializeField] private Camera controlledCamera;

        private bool hasStablePivotOffset;
        private Vector3 stablePivotOffset;
        private Vector3 followVelocity;
        private float yawOffset;
        private float pitchOffset;
        private float zoomOffset;
        private float currentCameraDistance;
        private float zoomVelocity;
        private float yawFollowVelocity;
        private bool wasRotatingCamera;
        private int rotationWarmupFramesRemaining;
        private bool hasAppliedRotationSinceBegin;
        private bool hasCurrentCameraDistance;
        private bool hasPreviousTargetRootPosition;
        private bool cursorLockedForRotation;
        private bool previousCursorVisible;
        private CursorLockMode previousCursorLockMode;
        private Vector3 previousTargetRootPosition;
        private int lastRotationInputFrame = -1;
        private InputAction mouseDeltaAction;
        private Vector2 accumulatedMouseDelta;

        /// <summary>Canonical settings asset read by the rig every frame.</summary>
        public WowCameraSettings Settings
        {
            get { return settings; }
            set { settings = value; }
        }

        /// <summary>Character root followed by this camera rig.</summary>
        public Transform TargetRoot
        {
            get { return targetRoot; }
            set
            {
                if (targetRoot == value)
                    return;

                targetRoot = value;
                ResetStablePivotOffset();
            }
        }

        /// <summary>Optional explicit pivot. When null, the rig searches for the character head bone.</summary>
        public Transform ExplicitPivot
        {
            get { return explicitPivot; }
            set
            {
                if (explicitPivot == value)
                    return;

                explicitPivot = value;
                ResetStablePivotOffset();
            }
        }

        /// <summary>Camera controlled by this rig.</summary>
        public Camera ControlledCamera
        {
            get { return controlledCamera; }
        }

        /// <summary>True while RMB camera rotation is active.</summary>
        public bool IsRotatingCamera
        {
            get { return wasRotatingCamera; }
        }

        /// <summary>True after cursor-capture warmup frames have been skipped.</summary>
        public bool IsRotationInputReady
        {
            get { return wasRotatingCamera && rotationWarmupFramesRemaining <= 0; }
        }

        /// <summary>True after non-zero RMB mouse delta has rotated the camera during the current RMB hold.</summary>
        public bool HasAppliedRotationSinceBegin
        {
            get { return hasAppliedRotationSinceBegin; }
        }

        /// <summary>Current world yaw after applying runtime mouse input.</summary>
        public float CurrentYaw
        {
            get { return settings != null ? settings.CameraRotation.y + yawOffset : transform.eulerAngles.y; }
        }

        /// <summary>Current pitch after applying runtime mouse input and settings limits.</summary>
        public float CurrentPitch
        {
            get { return settings != null ? ClampPitch(settings.CameraRotation.x + pitchOffset) : transform.eulerAngles.x; }
        }

        /// <summary>Assigns the followed character root and clears any cached head-bone lookup.</summary>
        /// <param name="target">Character root to follow.</param>
        public void SetTarget(Transform target)
        {
            TargetRoot = target;
        }

        /// <summary>
        /// Clears the cached root-local camera pivot so it is recalculated from the explicit pivot or head bone.
        /// </summary>
        public void RecalculatePivotOffset()
        {
            ResetStablePivotOffset();
        }

        /// <summary>
        /// Immediately places the rig at the current target pivot and applies the current camera settings without follow or zoom smoothing.
        /// </summary>
        public void SnapToTargetImmediate()
        {
            if (settings == null || targetRoot == null)
                return;

            if (controlledCamera == null)
                controlledCamera = GetComponentInChildren<Camera>();

            transform.position = ResolvePivotPosition();
            followVelocity = Vector3.zero;

            currentCameraDistance = ClampDistance(settings.CameraDistance + zoomOffset);
            hasCurrentCameraDistance = true;
            zoomVelocity = 0f;
            yawFollowVelocity = 0f;

            ApplyCameraTransform();
            CacheTargetRootPosition();
        }

        /// <summary>
        /// Adds a world-space yaw delta to the camera orbit without changing pitch, zoom, or follow pivot.
        /// </summary>
        /// <param name="yawDeltaDegrees">Signed yaw delta in degrees. Positive turns camera right; negative turns camera left.</param>
        public void AddYawDelta(float yawDeltaDegrees)
        {
            yawOffset += yawDeltaDegrees;
            yawFollowVelocity = 0f;
        }

        /// <summary>
        /// Applies this frame's mouse-look input to the camera yaw and pitch once, allowing character-facing code to read current camera yaw before LateUpdate.
        /// </summary>
        /// <returns>True when rotation input was processed by this call; false when it was already processed this frame or settings are unavailable.</returns>
        public bool ProcessRotationInputForFrame()
        {
            if (settings == null || lastRotationInputFrame == Time.frameCount)
                return false;

            lastRotationInputFrame = Time.frameCount;
            UpdateRotationInput();
            return true;
        }

        private void Reset()
        {
            controlledCamera = GetComponentInChildren<Camera>();
        }

        private void Awake()
        {
            if (controlledCamera == null)
                controlledCamera = GetComponentInChildren<Camera>();

            EnsureMouseDeltaAction();
            SnapToTargetImmediate();
        }

        private void OnEnable()
        {
            EnsureMouseDeltaAction();
            mouseDeltaAction.Enable();
        }

        private void Start()
        {
            SnapToTargetImmediate();
        }

        private void OnDisable()
        {
            EndCameraRotation();
            accumulatedMouseDelta = Vector2.zero;
            if (mouseDeltaAction != null)
                mouseDeltaAction.Disable();
        }

        private void OnDestroy()
        {
            if (mouseDeltaAction == null)
                return;

            mouseDeltaAction.performed -= OnMouseDeltaPerformed;
            mouseDeltaAction.Dispose();
            mouseDeltaAction = null;
        }

        private void LateUpdate()
        {
            if (settings == null || targetRoot == null)
            {
                EndCameraRotation();
                return;
            }

            ProcessRotationInputForFrame();
            UpdateZoomInput();
            UpdateYawFollow(Time.deltaTime);
            UpdateFollowPosition();
            ApplyCameraTransform();
            CacheTargetRootPosition();
        }

        private void UpdateRotationInput()
        {
            if (!IsMouseLookPressed())
            {
                EndCameraRotation();
                return;
            }

            if (!wasRotatingCamera)
            {
                if (!IsMouseInsideGameViewport())
                    return;

                BeginCameraRotation();
                return;
            }

            if (rotationWarmupFramesRemaining > 0)
            {
                rotationWarmupFramesRemaining--;
                accumulatedMouseDelta = Vector2.zero;
                return;
            }

            var mouseDelta = ConsumeMouseDelta();
            if (mouseDelta.sqrMagnitude <= 0f)
                return;

            yawOffset += mouseDelta.x * settings.YawDegreesPerPoint * settings.CameraSpeedMultiplier;

            var targetPitch = settings.CameraRotation.x + pitchOffset;
            targetPitch -= mouseDelta.y * settings.PitchDegreesPerPoint * settings.CameraSpeedMultiplier;
            targetPitch = ClampPitch(targetPitch);
            pitchOffset = targetPitch - settings.CameraRotation.x;
            hasAppliedRotationSinceBegin = true;
        }

        private void UpdateZoomInput()
        {
            var scrollDelta = GetScrollDelta();
            if (Mathf.Approximately(scrollDelta.y, 0f))
                return;

            zoomOffset -= scrollDelta.y * settings.ZoomMetersPerScrollPoint;
            zoomOffset = ClampDistance(settings.CameraDistance + zoomOffset) - settings.CameraDistance;
        }

        private void UpdateYawFollow(float deltaTime)
        {
            if (wasRotatingCamera || settings.YawFollowDegreesPerSecond <= 0f || deltaTime <= 0f || !TargetRootIsMoving(deltaTime))
                return;

            var targetYawOffset = Mathf.DeltaAngle(settings.CameraRotation.y, targetRoot.eulerAngles.y);
            if (settings.YawFollowSmoothTime <= 0f)
            {
                yawFollowVelocity = 0f;
                yawOffset = Mathf.MoveTowardsAngle(
                    yawOffset,
                    targetYawOffset,
                    settings.YawFollowDegreesPerSecond * deltaTime);
                return;
            }

            yawOffset = Mathf.SmoothDampAngle(
                yawOffset,
                targetYawOffset,
                ref yawFollowVelocity,
                settings.YawFollowSmoothTime,
                settings.YawFollowDegreesPerSecond,
                deltaTime);
        }

        private void BeginCameraRotation()
        {
            wasRotatingCamera = true;
            rotationWarmupFramesRemaining = Mathf.Max(0, settings.CursorCaptureWarmupFrames);
            accumulatedMouseDelta = Vector2.zero;
            hasAppliedRotationSinceBegin = false;

            if (!settings.LockCursorOnRotate)
                return;

            previousCursorVisible = Cursor.visible;
            previousCursorLockMode = Cursor.lockState;
            cursorLockedForRotation = true;
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        private void EndCameraRotation()
        {
            if (!wasRotatingCamera)
                return;

            wasRotatingCamera = false;
            rotationWarmupFramesRemaining = 0;
            accumulatedMouseDelta = Vector2.zero;
            hasAppliedRotationSinceBegin = false;

            if (!cursorLockedForRotation)
                return;

            cursorLockedForRotation = false;
            Cursor.visible = previousCursorVisible;
            Cursor.lockState = previousCursorLockMode;
        }

        private void UpdateFollowPosition()
        {
            var targetPosition = ResolvePivotPosition();
            if (settings.FollowSmoothness <= 0f)
            {
                transform.position = targetPosition;
                followVelocity = Vector3.zero;
                return;
            }

            transform.position = Vector3.SmoothDamp(
                transform.position,
                targetPosition,
                ref followVelocity,
                settings.FollowSmoothness);
        }

        private void ApplyCameraTransform()
        {
            transform.rotation = Quaternion.Euler(CurrentPitch, CurrentYaw, 0f);

            if (controlledCamera == null)
                return;

            controlledCamera.fieldOfView = settings.FieldOfView;
            controlledCamera.transform.localPosition = new Vector3(0f, 0f, -ResolveCameraDistance());
            controlledCamera.transform.localRotation = Quaternion.identity;
        }

        private float ResolveCameraDistance()
        {
            var targetDistance = ClampDistance(settings.CameraDistance + zoomOffset);
            if (!hasCurrentCameraDistance)
            {
                currentCameraDistance = targetDistance;
                hasCurrentCameraDistance = true;
                zoomVelocity = 0f;
                return currentCameraDistance;
            }

            if (settings.ZoomSmoothTime <= 0f)
            {
                currentCameraDistance = targetDistance;
                zoomVelocity = 0f;
                return currentCameraDistance;
            }

            currentCameraDistance = Mathf.SmoothDamp(
                currentCameraDistance,
                targetDistance,
                ref zoomVelocity,
                settings.ZoomSmoothTime);
            return ClampDistance(currentCameraDistance);
        }

        private Vector3 ResolvePivotPosition()
        {
            if (!hasStablePivotOffset)
                CacheStablePivotOffset();

            return targetRoot.TransformPoint(stablePivotOffset + settings.PivotOffset);
        }

        private void CacheStablePivotOffset()
        {
            var pivot = explicitPivot != null ? explicitPivot : FindHeadPivot(targetRoot);
            stablePivotOffset = pivot != null
                ? targetRoot.InverseTransformPoint(pivot.position)
                : Vector3.up * settings.FallbackPivotHeight;
            stablePivotOffset.x = 0f;
            stablePivotOffset.z = 0f;
            hasStablePivotOffset = true;
        }

        private void ResetStablePivotOffset()
        {
            hasStablePivotOffset = false;
            stablePivotOffset = Vector3.zero;
            followVelocity = Vector3.zero;
            yawFollowVelocity = 0f;
            hasPreviousTargetRootPosition = false;
        }

        private bool TargetRootIsMoving(float deltaTime)
        {
            if (!hasPreviousTargetRootPosition || targetRoot == null)
                return false;

            var delta = targetRoot.position - previousTargetRootPosition;
            delta.y = 0f;
            return delta.magnitude / deltaTime > settings.YawFollowMovementThreshold;
        }

        private void CacheTargetRootPosition()
        {
            if (targetRoot == null)
            {
                hasPreviousTargetRootPosition = false;
                return;
            }

            previousTargetRootPosition = targetRoot.position;
            hasPreviousTargetRootPosition = true;
        }

        private static Transform FindHeadPivot(Transform root)
        {
            if (root == null)
                return null;

            return FindChild(root, "bone_Head") ??
                FindChild(root, "Head") ??
                FindChild(root, "head");
        }

        private static Transform FindChild(Transform root, string name)
        {
            if (root.name == name)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindChild(root.GetChild(i), name);
                if (found != null)
                    return found;
            }

            return null;
        }

        private static bool IsRightMousePressed()
        {
            var mouse = Mouse.current;
            return mouse != null && mouse.rightButton.isPressed;
        }

        private bool IsMouseLookPressed()
        {
            var mouse = Mouse.current;
            if (mouse == null)
                return false;

            return mouse.rightButton.isPressed ||
                (settings.LeftMouseLookEnabled && mouse.leftButton.isPressed);
        }

        private static bool IsMouseInsideGameViewport()
        {
            var mouse = Mouse.current;
            if (mouse == null)
                return false;

            var position = mouse.position.ReadValue();
            return position.x >= 0f &&
                position.y >= 0f &&
                position.x <= Screen.width &&
                position.y <= Screen.height;
        }

        private void EnsureMouseDeltaAction()
        {
            if (mouseDeltaAction != null)
                return;

            mouseDeltaAction = new InputAction(
                "WoW Camera Mouse Delta",
                InputActionType.PassThrough,
                "<Mouse>/delta");
            mouseDeltaAction.performed += OnMouseDeltaPerformed;
        }

        private void OnMouseDeltaPerformed(InputAction.CallbackContext context)
        {
            accumulatedMouseDelta += context.ReadValue<Vector2>();
        }

        private Vector2 ConsumeMouseDelta()
        {
            var mouseDelta = accumulatedMouseDelta;
            accumulatedMouseDelta = Vector2.zero;
            return mouseDelta;
        }

        private static Vector2 GetScrollDelta()
        {
            var mouse = Mouse.current;
            return mouse != null ? mouse.scroll.ReadValue() : Vector2.zero;
        }

        private float ClampPitch(float pitch)
        {
            var limits = settings.PitchLimits;
            var min = Mathf.Min(limits.x, limits.y);
            var max = Mathf.Max(limits.x, limits.y);
            return Mathf.Clamp(pitch, min, max);
        }

        private float ClampDistance(float distance)
        {
            var min = Mathf.Min(settings.MinCameraDistance, settings.MaxCameraDistance);
            var max = Mathf.Max(settings.MinCameraDistance, settings.MaxCameraDistance);
            return Mathf.Clamp(distance, min, max);
        }
    }
}
