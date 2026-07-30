using System.Collections.Generic;
using System.Text;
using Animancer;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace WowLocomotionResearch
{
    /// <summary>
    /// Deterministic WoW-like third-person locomotion prototype using Generic rigs, Animancer, and weighted masks.
    /// </summary>
    public sealed class WowLikeAnimancerLocomotionPrototype : MonoBehaviour
    {
        private const int BaseLayerIndex = 0;
        private const int UpperLayerIndex = 1;
        private const int LandingUpperLayerIndex = 2;
        private const int StartupGroundingGraceFrameCount = 2;
        private const float FacingLockedStrafeDiagnosticInterval = 0.25f;
        private const float StandingJumpDiagnosticInterval = 0.25f;
        private const int LandingArmDiagnosticFrameBudget = 90;

        private enum JumpAnimationPhase
        {
            Grounded,
            Starting,
            Airborne,
            Landing
        }

        [Header("References")]
        [Tooltip("Animancer component that plays the root-bound Generic animation clips.")]
        [SerializeField] private AnimancerComponent animancer;

        [Tooltip("Animancer WeightedMaskLayers component used for upper-body/full-body action masks.")]
        [SerializeField] private WeightedMaskLayers weightedMaskLayers;

        [Tooltip("Animation clip set used by the locomotion mixer and upper-body actions.")]
        [SerializeField] private WowLocomotionAnimSet animSet;

        [Tooltip("Canonical runtime movement settings. When assigned, this asset overrides the fallback Movement and Animation values below and can be tweaked during Play Mode.")]
        [SerializeField] private WowMovementSettings movementSettings;

        [Tooltip("Generic bone profile used to classify upper body, lower body, and root-motion bones.")]
        [SerializeField] private WowGenericBoneProfile boneProfile;

        [Tooltip("Weighted mask profile used to build the layer masks for upper-body actions.")]
        [SerializeField] private WowWeightedMaskProfile weightedMaskProfile;

        [Tooltip("Character root to rotate and move. Usually the HumanCharacter prefab root.")]
        [SerializeField] private Transform characterRoot;

        [Tooltip("Visual model root under the character. Used for setup/debug only; root-bound clips play from Character Root.")]
        [SerializeField] private Transform modelRoot;

        [Tooltip("Camera transform used for RMB-facing movement. Use the Camera child from WowCameraRig.")]
        [SerializeField] private Transform cameraTransform;

        [Tooltip("Optional WoW camera rig. When assigned or found from Camera Transform, pure standing A/D keyboard turning rotates the camera with the character.")]
        [SerializeField] private WowThirdPersonCameraRig cameraRig;

        [Tooltip("CharacterController used for collision-aware script movement. If empty or disabled, movement falls back to transform position.")]
        [SerializeField] private CharacterController characterController;

        [Tooltip("Optional prebuilt Animancer 2D mixer. Leave empty/invalid to let the prototype build one from the animation set.")]
        [SerializeField] private MixerTransition2D locomotionMixerTransition = new MixerTransition2D();

        [Header("Movement")]
        [Tooltip("Fallback only when Movement Settings is unassigned. Use CharacterController.Move when possible; disable to move the transform directly.")]
        [SerializeField] private bool useCharacterController = true;

        [Tooltip("Fallback only when Movement Settings is unassigned. CharacterController skin width in meters. Lower reduces visible terrain hover; higher improves collision stability but increases the gap.")]
        [SerializeField] private float characterControllerSkinWidth = 0.025f;

        [Tooltip("Fallback only when Movement Settings is unassigned. Minimum CharacterController movement distance. 0 keeps small grounding moves active; higher values may ignore tiny floor-snapping motion.")]
        [SerializeField] private float characterControllerMinMoveDistance;

        [Tooltip("Fallback only when Movement Settings is unassigned. Enable for constant script velocity; disable when testing animation/root-motion driven movement.")]
        [SerializeField] private bool useScriptDrivenMotion = true;

        [Tooltip("Fallback only when Movement Settings is unassigned. Enable to face camera yaw while RMB is held; disable for camera-only RMB rotation.")]
        [SerializeField] private bool rightMouseHeldControlsFacing = true;

        [Tooltip("Fallback only when Movement Settings is unassigned. Enable to make A/D always strafe. When disabled, A/D turn only when pressed alone; W/S plus A/D still moves diagonally without rotating the root.")]
        [SerializeField] private bool alwaysStrafeAD;

        [Tooltip("Fallback only when Movement Settings is unassigned. Enable to keep diagonal speed equal to straight speed; disable to allow faster diagonals.")]
        [SerializeField] private bool normalizeDiagonalInput = true;

        [Tooltip("Fallback only when Movement Settings is unassigned. Enable for WoW-style both-mouse-buttons forward movement.")]
        [SerializeField] private bool leftAndRightMouseMoveForward = true;

        [Tooltip("Fallback only when Movement Settings is unassigned. Enable to let stationary RMB camera yaw twist head/upper body before the character root turns.")]
        [SerializeField] private bool rmbDelayedRootFacing = true;

        [Tooltip("Fallback only when Movement Settings is unassigned. Maximum RMB look yaw applied across neck/head in degrees.")]
        [SerializeField] private float rmbLookHeadMaxYawDegrees = 60f;

        [Tooltip("Fallback only when Movement Settings is unassigned. Maximum RMB look yaw applied across spine/chest in degrees.")]
        [SerializeField] private float rmbLookUpperBodyMaxYawDegrees = 40f;

        [Tooltip("Fallback only when Movement Settings is unassigned. Camera/root yaw difference before the root starts slowly turning during stationary RMB hold.")]
        [SerializeField] private float rmbLookRootTurnStartYawDegrees = 90f;

        [Tooltip("Fallback only when Movement Settings is unassigned. Maximum root turn speed while RMB remains held past the twist threshold.")]
        [SerializeField] private float rmbHeldRootTurnDegreesPerSecond = 45f;

        [Tooltip("Fallback only when Movement Settings is unassigned. Maximum root turn speed after RMB is released.")]
        [SerializeField] private float rmbReleaseRootTurnDegreesPerSecond = 540f;

        [Tooltip("Fallback only when Movement Settings is unassigned. Smoothing time for manual head and upper-body look twist.")]
        [SerializeField] private float rmbLookTwistSmoothTime = 0.06f;

        [Tooltip("Fallback only when Movement Settings is unassigned. Forward speed in meters per second. Lower is slower; higher is faster.")]
        [SerializeField] private float runSpeed = 7.0f;

        [Tooltip("Fallback only when Movement Settings is unassigned. Reserved walking speed. Lower is slower; higher is faster.")]
        [SerializeField] private float walkSpeed = 2.5f;

        [Tooltip("Fallback only when Movement Settings is unassigned. Backward speed multiplier. Lower slows backpedal; 1 equals forward speed; higher than 1 is faster than forward.")]
        [SerializeField] private float backwardSpeedMultiplier = 0.65f;

        [Tooltip("Fallback only when Movement Settings is unassigned. Playback speed multiplier for forward run animations only. Lower cycles slower; higher cycles faster. Physical movement speed is unchanged.")]
        [SerializeField] private float forwardRunAnimationSpeedMultiplier = 1.0f;

        [Tooltip("Fallback only when Movement Settings is unassigned. Playback speed multiplier for the backpedal animation only. Lower cycles slower; higher cycles faster. Physical movement speed is unchanged.")]
        [SerializeField] private float backwardAnimationSpeedMultiplier = 1.0f;

        [Tooltip("Fallback only when Movement Settings is unassigned. Maximum visual model yaw while RMB side-strafing. 90 lets pure A/D face fully sideways like WoW. Head/torso counter-yaw keeps aim toward camera.")]
        [SerializeField] private float rmbStrafeVisualYawDegrees = 90f;

        [Tooltip("Fallback only when Movement Settings is unassigned. Pure strafe speed multiplier. Lower slows strafing; 1 equals forward speed; higher than 1 is faster than forward.")]
        [SerializeField] private float strafeSpeedMultiplier = 0.90f;

        [Tooltip("Fallback only when Movement Settings is unassigned. Airborne horizontal speed multiplier for the first movement input after jumping from standstill. Running jumps lock takeoff velocity and ignore this.")]
        [SerializeField] private float standingJumpAirInputSpeedMultiplier = 0.35f;

        [Tooltip("Fallback only when Movement Settings is unassigned. Enable standing-jump mid-air lower-body facing toward the first committed movement direction.")]
        [SerializeField] private bool standingJumpAirFacing = true;

        [Tooltip("Fallback only when Movement Settings is unassigned. Root/lower-body turn speed after first movement input during a standing jump.")]
        [SerializeField] private float standingJumpAirRootTurnDegreesPerSecond = 360f;

        [Tooltip("Fallback only when Movement Settings is unassigned. Root turn speed after landing from a standing air-controlled jump.")]
        [SerializeField] private float standingJumpLandingRootCatchUpDegreesPerSecond = 720f;

        [Tooltip("Fallback only when Movement Settings is unassigned. A/D turn speed in degrees per second. Lower turns slower; higher turns faster.")]
        [SerializeField] private float keyboardTurnDegreesPerSecond = 120.0f;

        [Tooltip("Fallback only when Movement Settings is unassigned. Airborne vertical acceleration. More negative falls faster; closer to 0 falls slower.")]
        [SerializeField] private float gravity = -25.0f;

        [Tooltip("Fallback only when Movement Settings is unassigned. Ground snap velocity. More negative sticks harder to slopes/floor; closer to 0 is lighter.")]
        [SerializeField] private float groundedStickVelocity = -2.0f;

        [Tooltip("Fallback only when Movement Settings is unassigned. Jump apex height in meters. Lower hops less; higher jumps higher.")]
        [SerializeField] private float jumpHeight = 1.35f;

        [Tooltip("Fallback only when Movement Settings is unassigned. Base-layer fade for jump start/fall/landing clips. Lower snaps faster; higher blends more softly.")]
        [SerializeField] private float jumpAnimationFadeDuration = 0.08f;

        [Tooltip("Fallback only when Movement Settings is unassigned. Desired landing animation duration before returning to locomotion. Lower plays/returns faster; higher plays the landing more slowly.")]
        [SerializeField] private float jumpLandingDuration = 1.0f;

        [Tooltip("Fallback only when Movement Settings is unassigned. Landing duration while movement input is held. Lower returns legs to run/strafe sooner and reduces foot sliding; higher gives moving landings more visible weight.")]
        [SerializeField] private float movingJumpLandingDuration = 0.12f;

        [Tooltip("Fallback only when Movement Settings is unassigned. Normalized landing clip point used as the exit pose. Lower exits earlier; higher plays more of the clip but values near 1 can expose loop-boundary pops.")]
        [SerializeField, Range(0.5f, 0.98f)] private float jumpLandingExitNormalizedTime = 0.92f;

        [Tooltip("Fallback only when Movement Settings is unassigned. Normalized landing clip point used while movement continues through landing. Lower returns to run/strafe before the deep crouch; higher plays more landing pose.")]
        [SerializeField, Range(0.1f, 0.98f)] private float movingJumpLandingExitNormalizedTime = 0.38f;

        [Tooltip("Fallback only when Movement Settings is unassigned. Upper-body landing duration while standing. Lower returns torso/arms sooner; higher keeps landing recovery visible longer.")]
        [SerializeField] private float jumpLandingUpperBodyDuration = 1.0f;

        [Tooltip("Fallback only when Movement Settings is unassigned. Upper-body landing duration while movement continues. Lower removes torso recovery sooner; higher smooths the upper body while legs are already running.")]
        [SerializeField] private float movingJumpLandingUpperBodyDuration = 0.25f;

        [Tooltip("Fallback only when Movement Settings is unassigned. Normalized landing clip point used by the standing upper-body overlay.")]
        [SerializeField, Range(0.1f, 0.98f)] private float jumpLandingUpperBodyExitNormalizedTime = 0.92f;

        [Tooltip("Fallback only when Movement Settings is unassigned. Normalized landing clip point used by the moving upper-body overlay.")]
        [SerializeField, Range(0.1f, 0.98f)] private float movingJumpLandingUpperBodyExitNormalizedTime = 0.92f;

        [Tooltip("Fallback only when Movement Settings is unassigned. Fade-out time for the standing upper-body landing overlay. Lower snaps back faster; higher blends more softly.")]
        [SerializeField] private float jumpLandingUpperBodyFadeOutDuration = 0.15f;

        [Tooltip("Fallback only when Movement Settings is unassigned. Fade-out time for the moving upper-body landing overlay. Lower returns torso faster; higher reduces jerk after legs resume running.")]
        [SerializeField] private float movingJumpLandingUpperBodyFadeOutDuration = 0.12f;

        [Tooltip("Fallback only when Movement Settings is unassigned. Enable to let the upper-body landing clip keep advancing while its layer fades out. Disable to freeze the exit pose during fade.")]
        [SerializeField] private bool landingUpperBodyContinuesDuringFade = true;

        [Tooltip("Fallback only when Movement Settings is unassigned. Normalized landing clip time the upper-body overlay tries to reach by the end of fade-out. Lower finishes earlier; higher plays more recovery.")]
        [SerializeField, Range(0.1f, 0.98f)] private float landingUpperBodyFadeEndNormalizedTime = 0.98f;

        [Tooltip("Fallback only when Movement Settings is unassigned. Extra visual yaw applied to Model Root after animation evaluation. 0 keeps the authored prefab rotation; positive turns the visible model right; negative turns it left.")]
        [SerializeField] private float modelYawOffsetDegrees;

        [Tooltip("Fallback only when Movement Settings is unassigned. Assigned Strafe Clips uses the strafe clip slots; Forward Run With Visual Yaw avoids shuffle clips by turning the visual model toward movement.")]
        [SerializeField] private WowStrafeAnimationMode strafeAnimationMode = WowStrafeAnimationMode.UseForwardRunWithVisualYaw;

        [Tooltip("Fallback only when Movement Settings is unassigned. Visual yaw smoothing for Forward Run With Visual Yaw mode. Lower snaps faster; higher turns more softly.")]
        [SerializeField] private float visualMovementYawSmoothTime = 0.06f;

        [Tooltip("Fallback only when Movement Settings is unassigned. Enable to use strafe clips when rotating in place.")]
        [SerializeField] private bool useStrafeClipsForInPlaceTurn = true;

        [Tooltip("Fallback only when Movement Settings is unassigned. Root yaw speed in degrees per second needed for full left/right turn animation weight.")]
        [SerializeField] private float inPlaceTurnFullSpeedDegreesPerSecond = 120f;

        [Tooltip("Fallback only when Movement Settings is unassigned. Root yaw speed below which in-place turn animation is ignored.")]
        [SerializeField] private float inPlaceTurnMinSpeedDegreesPerSecond = 5f;

        [Header("Animation")]
        [Tooltip("Fallback only when Movement Settings is unassigned. Locomotion mixer startup fade. Lower snaps faster; higher blends in more slowly.")]
        [SerializeField] private float locomotionFadeDuration = 0.10f;

        [Tooltip("Fallback only when Movement Settings is unassigned. Upper-body action fade-in. Lower appears faster; higher blends in more slowly.")]
        [SerializeField] private float upperBodyFadeInDuration = 0.10f;

        [Tooltip("Fallback only when Movement Settings is unassigned. Upper-body action fade-out. Lower disappears faster; higher blends out longer.")]
        [SerializeField] private float upperBodyFadeOutDuration = 0.15f;

        [Tooltip("Fallback only when Movement Settings is unassigned. Visual mixer smoothing. Lower responds faster; higher softens animation direction changes. Movement remains constant velocity.")]
        [SerializeField] private float mixerParameterSmoothTime = 0.08f;

        [Tooltip("Fallback only when Movement Settings is unassigned. Synchronize locomotion mixer children for consistent cycle timing.")]
        [SerializeField] private bool synchronizeLocomotionChildren = true;

        [Tooltip("Fallback only when Movement Settings is unassigned. Synchronize idle with locomotion clips. Usually off.")]
        [SerializeField] private bool synchronizeIdle;

        [Tooltip("Fallback only when Movement Settings is unassigned. Enable for full-body actions while stationary; disable to keep actions upper-body only.")]
        [SerializeField] private bool fullBodyActionsWhenStationary = true;

        [Tooltip("Fallback only when Movement Settings is unassigned. Input magnitude below which the character is stationary. Lower requires less input; higher tolerates small movement.")]
        [SerializeField] private float stationaryActionThreshold = 0.05f;

        [Header("Debug")]
        [Tooltip("Show the runtime debug overlay. Disable to hide on-screen text.")]
        [SerializeField] private bool drawDebug;

        [Tooltip("Draw selected-object movement gizmos in the Scene view. Disable to reduce editor visual noise.")]
        [SerializeField] private bool drawGizmos = true;

        [Tooltip("Fallback only when Movement Settings is unassigned. Enable per-frame RMB camera/root rotation diagnostics.")]
        [SerializeField] private bool rotationDiagnosticLogs;

        private AnimancerLayer baseLayer;
        private AnimancerLayer upperLayer;
        private AnimancerLayer landingUpperLayer;
        private Vector2 rawMoveInput;
        private Vector2 normalizedMoveInput;
        private Vector2 visualMixerParameter;
        private Vector2 effectiveLocomotionParameter;
        private Vector3 worldMove;
        private Vector3 airborneHorizontalVelocity;
        private float verticalVelocity;
        private bool rmbHeld;
        private bool lmbHeld;
        private bool rmbPressedThisFrame;
        private bool rmbReleasedThisFrame;
        private bool rmbYawMovedOnRelease;
        private bool rmbCameraYawMovedSincePress;
        private bool rmbRootCatchUpActive;
        private bool standingJumpLandingRootCatchUpActive;
        private bool jumpPressedThisFrame;
        private bool keyboardTurnActive;
        private bool adKeysAreStrafing;
        private bool autoRunActive;
        private bool isMoving;
        private bool grounded;
        private bool canCaptureStandingJumpAirInput;
        private bool standingJumpTookOffFromStandstill;
        private bool standingJumpAirFacingActive;
        private bool hasStandingJumpAirCommittedInput;
        private bool standingJumpDiagnosticActive;
        private bool standingJumpLandingVisualCatchUpActive;
        private int startupGroundingGraceFramesRemaining;
        private float currentCameraYaw;
        private float cameraYawOnRmbPress;
        private float rmbRootCatchUpTargetYaw;
        private float standingJumpTakeoffRootYaw;
        private float standingJumpAirTargetRootYaw;
        private float standingJumpLookYaw;
        private float nextStandingJumpDiagnosticTime;
        private Vector2 standingJumpAirCommittedInput;
        private float currentRootYaw;
        private float rootYawDegreesPerSecond;
        private float currentHeadLookYaw;
        private float currentUpperBodyLookYaw;
        private float headLookYawVelocity;
        private float upperBodyLookYawVelocity;
        private float currentSpeedMultiplier = 1f;
        private bool lastLoggedFacingLockedStrafe;
        private bool facingLockedStrafeDiagnosticActive;
        private float nextFacingLockedStrafeDiagnosticTime;
        private Vector2 targetMixerParameter;
        private Vector2 lastVisualMixerParameter;
        private Vector2 visualMixerParameterVelocity;
        private Vector3 lastWorldMove;
        private Quaternion authoredModelRootLocalRotation = Quaternion.identity;
        private bool hasAuthoredModelRootLocalRotation;
        private float visualMovementYawOffset;
        private float targetVisualMovementYawOffset;
        private Transform lookSpineLower;
        private Transform lookSpineMiddle;
        private Transform lookSpineUpper;
        private Transform lookChest;
        private Transform lookNeck;
        private Transform lookHead;
        private Transform debugLeftClavicle;
        private Transform debugLeftShoulder;
        private Transform debugLeftUpperArm;
        private Transform debugLeftForearm;
        private Transform debugLeftHand;
        private Transform debugRightClavicle;
        private Transform debugRightShoulder;
        private Transform debugRightUpperArm;
        private Transform debugRightForearm;
        private Transform debugRightHand;
        private int landingArmDiagnosticFramesRemaining;
        private WowWeightedMaskGroup activeWeightedMaskGroup = WowWeightedMaskGroup.NoUpperBodyOverride;
        private readonly List<int> forwardRunAnimationChildIndices = new List<int>(3);
        private readonly List<int> backwardAnimationChildIndices = new List<int>(3);
        private Vector2MixerState locomotionMixerState;
        private AnimancerState locomotionState;
        private AnimancerState currentUpperBodyState;
        private AnimancerState currentLandingUpperBodyState;
        private AnimancerState currentJumpState;
        private JumpAnimationPhase jumpAnimationPhase;
        private bool landingStartedMoving;
        private float landingTimeRemaining;
        private float landingUpperBodyTimeRemaining;
        private float maskResetDelayRemaining;
        private readonly StringBuilder debugBuilder = new StringBuilder(1024);

        private bool UseCharacterController
        {
            get { return movementSettings != null ? movementSettings.UseCharacterController : useCharacterController; }
        }

        private float CharacterControllerSkinWidth
        {
            get { return movementSettings != null ? movementSettings.CharacterControllerSkinWidth : characterControllerSkinWidth; }
        }

        private float CharacterControllerMinMoveDistance
        {
            get { return movementSettings != null ? movementSettings.CharacterControllerMinMoveDistance : characterControllerMinMoveDistance; }
        }

        private bool UseScriptDrivenMotion
        {
            get { return movementSettings != null ? movementSettings.UseScriptDrivenMotion : useScriptDrivenMotion; }
        }

        private bool RightMouseHeldControlsFacing
        {
            get { return movementSettings != null ? movementSettings.RightMouseHeldControlsFacing : rightMouseHeldControlsFacing; }
        }

        private bool AlwaysStrafeAD
        {
            get { return movementSettings != null ? movementSettings.AlwaysStrafeAD : alwaysStrafeAD; }
        }

        private bool NormalizeDiagonalInput
        {
            get { return movementSettings != null ? movementSettings.NormalizeDiagonalInput : normalizeDiagonalInput; }
        }

        private bool LeftAndRightMouseMoveForward
        {
            get { return movementSettings != null ? movementSettings.LeftAndRightMouseMoveForward : leftAndRightMouseMoveForward; }
        }

        private bool RmbDelayedRootFacing
        {
            get { return movementSettings != null ? movementSettings.RmbDelayedRootFacing : rmbDelayedRootFacing; }
        }

        private float RmbLookHeadMaxYawDegrees
        {
            get { return movementSettings != null ? movementSettings.RmbLookHeadMaxYawDegrees : Mathf.Max(0f, rmbLookHeadMaxYawDegrees); }
        }

        private float RmbLookUpperBodyMaxYawDegrees
        {
            get { return movementSettings != null ? movementSettings.RmbLookUpperBodyMaxYawDegrees : Mathf.Max(0f, rmbLookUpperBodyMaxYawDegrees); }
        }

        private float RmbLookRootTurnStartYawDegrees
        {
            get { return movementSettings != null ? movementSettings.RmbLookRootTurnStartYawDegrees : Mathf.Max(0f, rmbLookRootTurnStartYawDegrees); }
        }

        private float RmbHeldRootTurnDegreesPerSecond
        {
            get { return movementSettings != null ? movementSettings.RmbHeldRootTurnDegreesPerSecond : Mathf.Max(0f, rmbHeldRootTurnDegreesPerSecond); }
        }

        private float RmbReleaseRootTurnDegreesPerSecond
        {
            get { return movementSettings != null ? movementSettings.RmbReleaseRootTurnDegreesPerSecond : Mathf.Max(0f, rmbReleaseRootTurnDegreesPerSecond); }
        }

        private float RmbLookTwistSmoothTime
        {
            get { return movementSettings != null ? movementSettings.RmbLookTwistSmoothTime : Mathf.Max(0f, rmbLookTwistSmoothTime); }
        }

        private float RunSpeed
        {
            get { return movementSettings != null ? movementSettings.RunSpeed : runSpeed; }
        }

        private float WalkSpeed
        {
            get { return movementSettings != null ? movementSettings.WalkSpeed : walkSpeed; }
        }

        private float BackwardSpeedMultiplier
        {
            get { return movementSettings != null ? movementSettings.BackwardSpeedMultiplier : backwardSpeedMultiplier; }
        }

        private float ForwardRunAnimationSpeedMultiplier
        {
            get
            {
                if (movementSettings != null)
                    return movementSettings.ForwardRunAnimationSpeedMultiplier;

                return forwardRunAnimationSpeedMultiplier > 0f ? forwardRunAnimationSpeedMultiplier : 1.0f;
            }
        }

        private float BackwardAnimationSpeedMultiplier
        {
            get { return movementSettings != null ? movementSettings.BackwardAnimationSpeedMultiplier : backwardAnimationSpeedMultiplier; }
        }

        private float RmbStrafeVisualYawDegrees
        {
            get
            {
                if (movementSettings != null)
                    return movementSettings.RmbStrafeVisualYawDegrees;

                return rmbStrafeVisualYawDegrees > 0f ? rmbStrafeVisualYawDegrees : 90f;
            }
        }

        private bool LocomotionDecisionLogs
        {
            get { return movementSettings != null && movementSettings.LocomotionDecisionLogs; }
        }

        private bool LandingDiagnosticLogs
        {
            get { return movementSettings != null && movementSettings.LandingDiagnosticLogs; }
        }

        private bool RotationDiagnosticLogs
        {
            get { return movementSettings != null ? movementSettings.RotationDiagnosticLogs : rotationDiagnosticLogs; }
        }

        private float StrafeSpeedMultiplier
        {
            get { return movementSettings != null ? movementSettings.StrafeSpeedMultiplier : strafeSpeedMultiplier; }
        }

        private float StandingJumpAirInputSpeedMultiplier
        {
            get { return movementSettings != null ? movementSettings.StandingJumpAirInputSpeedMultiplier : standingJumpAirInputSpeedMultiplier; }
        }

        private bool StandingJumpAirFacing
        {
            get { return movementSettings != null ? movementSettings.StandingJumpAirFacing : standingJumpAirFacing; }
        }

        private float StandingJumpAirRootTurnDegreesPerSecond
        {
            get { return movementSettings != null ? movementSettings.StandingJumpAirRootTurnDegreesPerSecond : Mathf.Max(0f, standingJumpAirRootTurnDegreesPerSecond); }
        }

        private float StandingJumpLandingRootCatchUpDegreesPerSecond
        {
            get { return movementSettings != null ? movementSettings.StandingJumpLandingRootCatchUpDegreesPerSecond : Mathf.Max(0f, standingJumpLandingRootCatchUpDegreesPerSecond); }
        }

        private float KeyboardTurnDegreesPerSecond
        {
            get { return movementSettings != null ? movementSettings.KeyboardTurnDegreesPerSecond : keyboardTurnDegreesPerSecond; }
        }

        private float Gravity
        {
            get { return movementSettings != null ? movementSettings.Gravity : gravity; }
        }

        private float GroundedStickVelocity
        {
            get { return movementSettings != null ? movementSettings.GroundedStickVelocity : groundedStickVelocity; }
        }

        private float JumpHeight
        {
            get { return movementSettings != null ? movementSettings.JumpHeight : jumpHeight; }
        }

        private float JumpAnimationFadeDuration
        {
            get { return movementSettings != null ? movementSettings.JumpAnimationFadeDuration : jumpAnimationFadeDuration; }
        }

        private float JumpLandingDuration
        {
            get { return movementSettings != null ? movementSettings.JumpLandingDuration : jumpLandingDuration; }
        }

        private float MovingJumpLandingDuration
        {
            get { return movementSettings != null ? movementSettings.MovingJumpLandingDuration : movingJumpLandingDuration; }
        }

        private float JumpLandingExitNormalizedTime
        {
            get { return movementSettings != null ? movementSettings.JumpLandingExitNormalizedTime : jumpLandingExitNormalizedTime; }
        }

        private float MovingJumpLandingExitNormalizedTime
        {
            get { return movementSettings != null ? movementSettings.MovingJumpLandingExitNormalizedTime : movingJumpLandingExitNormalizedTime; }
        }

        private float JumpLandingUpperBodyDuration
        {
            get { return movementSettings != null ? movementSettings.JumpLandingUpperBodyDuration : jumpLandingUpperBodyDuration; }
        }

        private float MovingJumpLandingUpperBodyDuration
        {
            get { return movementSettings != null ? movementSettings.MovingJumpLandingUpperBodyDuration : movingJumpLandingUpperBodyDuration; }
        }

        private float JumpLandingUpperBodyExitNormalizedTime
        {
            get { return movementSettings != null ? movementSettings.JumpLandingUpperBodyExitNormalizedTime : jumpLandingUpperBodyExitNormalizedTime; }
        }

        private float MovingJumpLandingUpperBodyExitNormalizedTime
        {
            get { return movementSettings != null ? movementSettings.MovingJumpLandingUpperBodyExitNormalizedTime : movingJumpLandingUpperBodyExitNormalizedTime; }
        }

        private float JumpLandingUpperBodyFadeOutDuration
        {
            get { return movementSettings != null ? movementSettings.JumpLandingUpperBodyFadeOutDuration : jumpLandingUpperBodyFadeOutDuration; }
        }

        private float MovingJumpLandingUpperBodyFadeOutDuration
        {
            get { return movementSettings != null ? movementSettings.MovingJumpLandingUpperBodyFadeOutDuration : movingJumpLandingUpperBodyFadeOutDuration; }
        }

        private bool LandingUpperBodyContinuesDuringFade
        {
            get { return movementSettings != null ? movementSettings.LandingUpperBodyContinuesDuringFade : landingUpperBodyContinuesDuringFade; }
        }

        private float LandingUpperBodyFadeEndNormalizedTime
        {
            get { return movementSettings != null ? movementSettings.LandingUpperBodyFadeEndNormalizedTime : landingUpperBodyFadeEndNormalizedTime; }
        }

        private float ModelYawOffsetDegrees
        {
            get { return movementSettings != null ? movementSettings.ModelYawOffsetDegrees : modelYawOffsetDegrees; }
        }

        private WowStrafeAnimationMode StrafeAnimationMode
        {
            get { return movementSettings != null ? movementSettings.StrafeAnimationMode : strafeAnimationMode; }
        }

        private float VisualMovementYawSmoothTime
        {
            get { return movementSettings != null ? movementSettings.VisualMovementYawSmoothTime : visualMovementYawSmoothTime; }
        }

        private bool UseStrafeClipsForInPlaceTurn
        {
            get { return movementSettings != null ? movementSettings.UseStrafeClipsForInPlaceTurn : useStrafeClipsForInPlaceTurn; }
        }

        private float InPlaceTurnFullSpeedDegreesPerSecond
        {
            get { return movementSettings != null ? movementSettings.InPlaceTurnFullSpeedDegreesPerSecond : inPlaceTurnFullSpeedDegreesPerSecond; }
        }

        private float InPlaceTurnMinSpeedDegreesPerSecond
        {
            get { return movementSettings != null ? movementSettings.InPlaceTurnMinSpeedDegreesPerSecond : inPlaceTurnMinSpeedDegreesPerSecond; }
        }

        private float LocomotionFadeDuration
        {
            get { return movementSettings != null ? movementSettings.LocomotionFadeDuration : locomotionFadeDuration; }
        }

        private float UpperBodyFadeInDuration
        {
            get { return movementSettings != null ? movementSettings.UpperBodyFadeInDuration : upperBodyFadeInDuration; }
        }

        private float UpperBodyFadeOutDuration
        {
            get { return movementSettings != null ? movementSettings.UpperBodyFadeOutDuration : upperBodyFadeOutDuration; }
        }

        private float MixerParameterSmoothTime
        {
            get { return movementSettings != null ? movementSettings.MixerParameterSmoothTime : mixerParameterSmoothTime; }
        }

        private bool SynchronizeLocomotionChildren
        {
            get { return movementSettings != null ? movementSettings.SynchronizeLocomotionChildren : synchronizeLocomotionChildren; }
        }

        private bool SynchronizeIdle
        {
            get { return movementSettings != null ? movementSettings.SynchronizeIdle : synchronizeIdle; }
        }

        private bool FullBodyActionsWhenStationary
        {
            get { return movementSettings != null ? movementSettings.FullBodyActionsWhenStationary : fullBodyActionsWhenStationary; }
        }

        private float StationaryActionThreshold
        {
            get { return movementSettings != null ? movementSettings.StationaryActionThreshold : stationaryActionThreshold; }
        }

        /// <summary>Raw input before normalization, where x is strafe and y is forward.</summary>
        public Vector2 RawMoveInput
        {
            get { return rawMoveInput; }
        }

        /// <summary>Canonical movement settings read by this prototype at runtime.</summary>
        public WowMovementSettings MovementSettings
        {
            get { return movementSettings; }
            set { movementSettings = value; }
        }

        /// <summary>Normalized movement input used for physical movement.</summary>
        public Vector2 NormalizedMoveInput
        {
            get { return normalizedMoveInput; }
        }

        /// <summary>Smoothed mixer parameter used only for visual animation blending.</summary>
        public Vector2 VisualMixerParameter
        {
            get { return visualMixerParameter; }
        }

        /// <summary>Effective locomotion mixer parameter after applying the configured strafe animation fallback.</summary>
        public Vector2 EffectiveLocomotionParameter
        {
            get { return effectiveLocomotionParameter; }
        }

        /// <summary>Approximate mixer parameter velocity derived from the visual smoothing step.</summary>
        public Vector2 VisualMixerParameterVelocity
        {
            get { return visualMixerParameterVelocity; }
        }

        /// <summary>World-space horizontal movement direction generated from root yaw and normalized input.</summary>
        public Vector3 WorldMove
        {
            get { return worldMove; }
        }

        /// <summary>True while the right mouse button is held.</summary>
        public bool RightMouseHeld
        {
            get { return rmbHeld; }
        }

        /// <summary>True when period-key autorun is currently feeding forward movement input.</summary>
        public bool AutoRunActive
        {
            get { return autoRunActive; }
        }

        /// <summary>True while the left mouse button is held.</summary>
        public bool LeftMouseHeld
        {
            get { return lmbHeld; }
        }

        /// <summary>True when A or D is currently rotating the character rather than strafing.</summary>
        public bool KeyboardTurnActive
        {
            get { return keyboardTurnActive; }
        }

        /// <summary>True when A and D are currently interpreted as strafe keys.</summary>
        public bool ADKeysAreStrafing
        {
            get { return adKeysAreStrafing; }
        }

        /// <summary>True when normalized input magnitude is above the stationary action threshold.</summary>
        public bool IsMoving
        {
            get { return isMoving; }
        }

        /// <summary>Current yaw copied from the camera transform.</summary>
        public float CurrentCameraYaw
        {
            get { return currentCameraYaw; }
        }

        /// <summary>Camera transform used for camera-relative movement and RMB facing.</summary>
        public Transform CameraTransform
        {
            get { return cameraTransform; }
        }

        /// <summary>Camera rig used for calibrated WoW-style yaw queries and keyboard turn camera coupling.</summary>
        public WowThirdPersonCameraRig CameraRig
        {
            get { return cameraRig; }
        }

        /// <summary>
        /// Assigns the camera rig and child camera transform used by camera-relative movement.
        /// </summary>
        /// <param name="rig">Camera rig that follows this character.</param>
        public void SetCamera(WowThirdPersonCameraRig rig)
        {
            cameraRig = rig;
            cameraTransform = rig != null && rig.ControlledCamera != null
                ? rig.ControlledCamera.transform
                : null;
            currentCameraYaw = GetCameraYaw();
        }

        /// <summary>Current yaw of the character root.</summary>
        public float CurrentRootYaw
        {
            get { return currentRootYaw; }
        }

        /// <summary>Speed multiplier applied to the configured run speed.</summary>
        public float CurrentSpeedMultiplier
        {
            get { return currentSpeedMultiplier; }
        }

        /// <summary>True when the CharacterController reports grounded or no CharacterController is in use.</summary>
        public bool Grounded
        {
            get { return grounded; }
        }

        /// <summary>Current vertical velocity used for script-driven gravity.</summary>
        public float VerticalVelocity
        {
            get { return verticalVelocity; }
        }

        /// <summary>Current weighted mask group applied to the upper layer.</summary>
        public WowWeightedMaskGroup ActiveWeightedMaskGroup
        {
            get { return activeWeightedMaskGroup; }
        }

        /// <summary>Current base Animancer layer state.</summary>
        public AnimancerState CurrentBaseState
        {
            get { return baseLayer != null ? baseLayer.CurrentState : null; }
        }

        /// <summary>Current upper-body Animancer layer state.</summary>
        public AnimancerState CurrentUpperState
        {
            get { return upperLayer != null ? upperLayer.CurrentState : null; }
        }

        /// <summary>Current weight of the upper Animancer layer.</summary>
        public float UpperLayerWeight
        {
            get { return upperLayer != null ? upperLayer.Weight : 0f; }
        }

        private void Reset()
        {
            characterRoot = transform;
            characterController = GetComponent<CharacterController>();
            animancer = GetComponentInChildren<AnimancerComponent>();
            weightedMaskLayers = GetComponentInChildren<WeightedMaskLayers>();
            if (animancer != null)
                modelRoot = animancer.transform;
            if (Camera.main != null)
                cameraTransform = Camera.main.transform;
            ResolveCameraRig();
        }

        private void Awake()
        {
            if (characterRoot == null)
                characterRoot = transform;
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;
            ResolveCameraRig();

            ValidateReferences();
            InitializeLookTwistBones();
            CaptureAuthoredModelRootRotation();
            InitializeAnimator();
            InitializeLayers();
            InitializeWeightedMasks();
            InitializeLocomotion();
            InitializeGroundingState();
            ForceInitialAnimationPose();
        }

        private void Update()
        {
            ReadInput();
            ApplyFacing(Time.deltaTime);
            ApplyMovement(Time.deltaTime);
            UpdateLocomotionParameter(Time.deltaTime);
            UpdateJumpAnimation(Time.deltaTime);
            UpdateLandingUpperBody(Time.deltaTime);
            LogLandingArmDiagnostics("UpdateAfterLanding", false);
            UpdateUpperBodyCompletion(Time.deltaTime);
        }

        private void LateUpdate()
        {
            ApplyVisualModelYaw();
            ApplyRmbLookTwist();
            LogLandingArmDiagnostics("LateAfterLookTwist", true);
            LogFacingLockedStrafeDiagnosticsIfNeeded();
        }

        /// <summary>
        /// Plays the configured cast clip on the upper-body layer.
        /// </summary>
        public void PlayCast()
        {
            PlayUpperBody(animSet != null ? animSet.cast : null);
        }

        /// <summary>
        /// Plays the configured attack clip on the upper-body layer.
        /// </summary>
        public void PlayAttack()
        {
            PlayUpperBody(animSet != null ? animSet.attack : null);
        }

        /// <summary>
        /// Plays the configured aim pose on the upper-body layer using the aim-only mask group.
        /// </summary>
        public void PlayAimPose()
        {
            var clip = animSet != null ? animSet.aimPose : null;
            if (clip == null)
            {
                Debug.LogWarning("Aim pose clip is not assigned.", this);
                return;
            }

            PlayUpperBodyWithGroup(clip, WowWeightedMaskGroup.UpperBodyAimOnly, false, UpperBodyFadeInDuration);
        }

        /// <summary>
        /// Plays an arbitrary upper-body clip while preserving base locomotion.
        /// </summary>
        /// <param name="clip">Clip to play on Animancer layer 1.</param>
        /// <param name="forceFullBody">If true, uses the stationary full-body mask even while moving.</param>
        /// <param name="fadeIn">Optional layer fade-in duration; negative uses the serialized default.</param>
        public void PlayUpperBody(AnimationClip clip, bool forceFullBody = false, float fadeIn = -1f)
        {
            if (clip == null)
            {
                Debug.LogWarning("Upper-body clip is null.", this);
                return;
            }

            var group = WowWeightedMaskGroup.UpperBodyActionWhileMoving;
            if (forceFullBody || (!isMoving && FullBodyActionsWhenStationary))
                group = WowWeightedMaskGroup.FullBodyActionWhileStationary;

            PlayUpperBodyWithGroup(clip, group, false, fadeIn);
        }

        /// <summary>
        /// Fades the upper-body layer to zero and returns the weighted mask to no override after the fade.
        /// </summary>
        /// <param name="fadeOut">Optional layer fade-out duration; negative uses the serialized default.</param>
        public void StopUpperBody(float fadeOut = -1f)
        {
            if (upperLayer == null)
                return;

            var duration = fadeOut >= 0f ? fadeOut : UpperBodyFadeOutDuration;
            upperLayer.StartFade(0f, duration);
            currentUpperBodyState = null;
            maskResetDelayRemaining = Mathf.Max(0f, duration);
        }

        /// <summary>
        /// Builds a reader-facing debug string for the current locomotion and masking state.
        /// </summary>
        /// <returns>Formatted debug overlay text.</returns>
        public string BuildDebugString()
        {
            debugBuilder.Length = 0;
            debugBuilder.AppendLine("WoW Locomotion Research");
            debugBuilder.Append("RMB held: ").Append(rmbHeld).AppendLine();
            debugBuilder.Append("RMB yaw active: ").Append(rmbCameraYawMovedSincePress).AppendLine();
            debugBuilder.Append("LMB held: ").Append(lmbHeld).AppendLine();
            debugBuilder.Append("autorun: ").Append(autoRunActive).AppendLine();
            debugBuilder.Append("raw input: ").Append(Format(rawMoveInput)).AppendLine();
            debugBuilder.Append("normalized input: ").Append(Format(normalizedMoveInput)).AppendLine();
            debugBuilder.Append("effective locomotion: ").Append(Format(effectiveLocomotionParameter)).AppendLine();
            debugBuilder.Append("visual mixer parameter: ").Append(Format(visualMixerParameter)).AppendLine();
            debugBuilder.Append("visual movement yaw: ").Append(visualMovementYawOffset.ToString("0.###")).AppendLine();
            debugBuilder.Append("camera yaw: ").Append(currentCameraYaw.ToString("0.###")).AppendLine();
            debugBuilder.Append("character root yaw: ").Append(currentRootYaw.ToString("0.###")).AppendLine();
            debugBuilder.Append("root yaw speed: ").Append(rootYawDegreesPerSecond.ToString("0.###")).AppendLine();
            debugBuilder.Append("RMB head look yaw: ").Append(currentHeadLookYaw.ToString("0.###")).AppendLine();
            debugBuilder.Append("RMB upper-body look yaw: ").Append(currentUpperBodyLookYaw.ToString("0.###")).AppendLine();
            debugBuilder.Append("model yaw offset: ").Append(ModelYawOffsetDegrees.ToString("0.###")).AppendLine();
            debugBuilder.Append("keyboard turn active: ").Append(keyboardTurnActive).AppendLine();
            debugBuilder.Append("AD mode: ").Append(adKeysAreStrafing ? "strafing" : "turning").AppendLine();
            debugBuilder.Append("world move vector: ").Append(Format(worldMove)).AppendLine();
            debugBuilder.Append("speed multiplier: ").Append(currentSpeedMultiplier.ToString("0.###")).AppendLine();
            debugBuilder.Append("walk speed setting: ").Append(WalkSpeed.ToString("0.###")).AppendLine();
            debugBuilder.Append("base layer state: ").Append(GetStateName(CurrentBaseState)).AppendLine();
            debugBuilder.Append("upper layer state: ").Append(GetStateName(CurrentUpperState)).AppendLine();
            debugBuilder.Append("upper layer weight: ").Append(UpperLayerWeight.ToString("0.###")).AppendLine();
            debugBuilder.Append("active WeightedMask group: ").Append(activeWeightedMaskGroup).AppendLine();
            debugBuilder.Append("jump phase: ").Append(jumpAnimationPhase).AppendLine();
            debugBuilder.Append("grounded: ").Append(grounded).AppendLine();
            debugBuilder.Append("vertical velocity: ").Append(verticalVelocity.ToString("0.###")).AppendLine();
            debugBuilder.Append("standing jump air facing: ").Append(standingJumpAirFacingActive).AppendLine();
            debugBuilder.Append("standing jump target yaw: ").Append(standingJumpAirTargetRootYaw.ToString("0.###")).AppendLine();
            debugBuilder.AppendLine();
            debugBuilder.AppendLine("EXPECTED W+D:");
            debugBuilder.AppendLine("input ~= (0.707, 0.707)");
            debugBuilder.AppendLine("root/camera yaw stays stable unless RMB look or A/D-alone turn is active");
            debugBuilder.AppendLine("mixer ~= (0.707, 0.707)");
            return debugBuilder.ToString();
        }

        /// <summary>
        /// Builds a read-only snapshot of landing, locomotion, and layer state for debug visualizers.
        /// </summary>
        /// <returns>Current landing debug data sampled from the active Animancer layers.</returns>
        public WowLandingDebugSnapshot BuildLandingDebugSnapshot()
        {
            var landingUpperState = currentLandingUpperBodyState != null
                ? currentLandingUpperBodyState
                : GetLayerCurrentState(landingUpperLayer);

            return new WowLandingDebugSnapshot(
                jumpAnimationPhase.ToString(),
                grounded,
                isMoving,
                landingStartedMoving,
                autoRunActive,
                rawMoveInput,
                normalizedMoveInput,
                visualMixerParameter,
                GetStateName(currentJumpState),
                GetStateNormalizedTime(currentJumpState),
                GetLayerWeight(baseLayer),
                landingTimeRemaining,
                ResolveLandingDuration(),
                GetLandingExitNormalizedTime(),
                GetStateName(GetLayerCurrentState(baseLayer)),
                GetStateName(landingUpperState),
                GetStateNormalizedTime(landingUpperState),
                GetLayerWeight(landingUpperLayer),
                landingUpperBodyTimeRemaining,
                ResolveLandingUpperBodyDuration(),
                GetLandingUpperBodyExitNormalizedTime(),
                GetStateName(GetLayerCurrentState(landingUpperLayer)),
                GetStateName(GetLayerCurrentState(upperLayer)),
                GetLayerWeight(upperLayer),
                GetStateName(locomotionState),
                visualMovementYawOffset,
                targetVisualMovementYawOffset);
        }

        private void ValidateReferences()
        {
            if (animancer == null)
                Debug.LogError("AnimancerComponent is required.", this);
            if (weightedMaskLayers == null)
                Debug.LogError("WeightedMaskLayers is required for Generic rig upper-body masking.", this);
            if (animSet == null)
                Debug.LogError("WowLocomotionAnimSet is required.", this);
            if (boneProfile == null)
                Debug.LogError("WowGenericBoneProfile is required.", this);
            if (weightedMaskProfile == null)
                Debug.LogError("WowWeightedMaskProfile is required.", this);
            if (movementSettings == null)
                Debug.LogWarning("WowMovementSettings is not assigned. Serialized fallback movement values will be used.", this);
            if (cameraTransform == null)
                Debug.LogError("cameraTransform is required for RMB-facing movement.", this);

            if (animSet != null)
            {
                var report = animSet.Validate();
                LogWarnings(report.Warnings);
            }

            if (boneProfile != null)
            {
                var report = boneProfile.Validate();
                LogErrors(report.Errors);
                LogWarnings(report.Warnings);
            }
        }

        private void InitializeAnimator()
        {
            if (animancer == null || animancer.Animator == null)
                return;

            var animator = animancer.Animator;
            if (animator.isHuman)
                Debug.LogError("Animator must use a Generic rig. Humanoid retargeting is not supported by this prototype.", this);

            animator.applyRootMotion = true;
            WarnForRootMotionTranslations();
        }

        private void InitializeLayers()
        {
            if (animancer == null)
                return;

            baseLayer = animancer.Layers[BaseLayerIndex];
            upperLayer = animancer.Layers[UpperLayerIndex];
            landingUpperLayer = animancer.Layers[LandingUpperLayerIndex];
            upperLayer.IsAdditive = false;
            upperLayer.SetLayerWeightOnPlay = false;
            upperLayer.Weight = 0f;
            landingUpperLayer.IsAdditive = false;
            landingUpperLayer.SetLayerWeightOnPlay = false;
            landingUpperLayer.Weight = 0f;
        }

        private void InitializeWeightedMasks()
        {
            if (weightedMaskLayers == null)
                return;

            if (weightedMaskLayers.LayerCount < 3)
                weightedMaskLayers.LayerCount = 3;

            if (weightedMaskLayers.Layers == null)
            {
                Debug.LogWarning(
                    "WeightedMaskLayers runtime list is not initialized. Configure its Definition before Play Mode or use the setup report Apply button in Edit Mode.",
                    this);
                return;
            }

            SetWeightedMaskGroup(WowWeightedMaskGroup.NoUpperBodyOverride, 0f);
            SetLandingUpperBodyMaskGroup(0f);
        }

        private void InitializeLocomotion()
        {
            if (baseLayer == null || animSet == null)
                return;

            BuildLocomotionMixerTransitionIfNeeded();
            locomotionState = baseLayer.Play(locomotionMixerTransition);
            locomotionMixerState = locomotionState as Vector2MixerState;
            if (locomotionMixerState == null)
                Debug.LogError("Locomotion transition did not create a Vector2 mixer state.", this);
            else
                locomotionMixerState.Parameter = Vector2.zero;
        }

        private void ForceInitialAnimationPose()
        {
            if (animancer == null)
                return;

            animancer.Evaluate(0f);
            ApplyVisualModelYaw();
        }

        private void InitializeGroundingState()
        {
            startupGroundingGraceFramesRemaining = StartupGroundingGraceFrameCount;
            jumpAnimationPhase = JumpAnimationPhase.Grounded;
            currentJumpState = null;

            if (characterController != null && UseCharacterController)
            {
                ApplyCharacterControllerRuntimeSettings();
                characterController.Move(Vector3.down * Mathf.Max(0.001f, CharacterControllerSkinWidth));
                grounded = characterController.isGrounded;
                verticalVelocity = grounded ? GroundedStickVelocity : 0f;
                return;
            }

            grounded = true;
            verticalVelocity = 0f;
        }

        private void BuildLocomotionMixerTransitionIfNeeded()
        {
            if (locomotionMixerTransition != null && locomotionMixerTransition.IsValid)
                return;

            locomotionMixerTransition = new MixerTransition2D();
            locomotionMixerTransition.Type = MixerTransition2D.MixerType.Directional;
            locomotionMixerTransition.DefaultParameter = Vector2.zero;
            forwardRunAnimationChildIndices.Clear();
            backwardAnimationChildIndices.Clear();

            var clips = new List<Object>();
            var thresholds = new List<Vector2>();
            var speeds = new List<float>();
            var synchronize = new List<bool>();

            AddMixerChild(clips, thresholds, speeds, synchronize, animSet.idle, Vector2.zero, SynchronizeIdle, 1f);
            RegisterForwardRunAnimationChild(AddMixerChild(clips, thresholds, speeds, synchronize, animSet.runForward, new Vector2(0f, 1f), SynchronizeLocomotionChildren, ForwardRunAnimationSpeedMultiplier));
            RegisterBackwardAnimationChild(AddMixerChild(clips, thresholds, speeds, synchronize, ResolveBackpedalClip(), new Vector2(0f, -1f), SynchronizeLocomotionChildren, BackwardAnimationSpeedMultiplier));
            AddMixerChild(clips, thresholds, speeds, synchronize, animSet.strafeLeft, new Vector2(-1f, 0f), SynchronizeLocomotionChildren, 1f);
            AddMixerChild(clips, thresholds, speeds, synchronize, animSet.strafeRight, new Vector2(1f, 0f), SynchronizeLocomotionChildren, 1f);
            RegisterForwardRunAnimationChild(AddMixerChild(clips, thresholds, speeds, synchronize, animSet.runForwardLeft, new Vector2(-1f, 1f), SynchronizeLocomotionChildren, ForwardRunAnimationSpeedMultiplier));
            RegisterForwardRunAnimationChild(AddMixerChild(clips, thresholds, speeds, synchronize, animSet.runForwardRight, new Vector2(1f, 1f), SynchronizeLocomotionChildren, ForwardRunAnimationSpeedMultiplier));
            RegisterBackwardAnimationChild(AddMixerChild(clips, thresholds, speeds, synchronize, animSet.runBackwardLeft, new Vector2(-1f, -1f), SynchronizeLocomotionChildren, BackwardAnimationSpeedMultiplier));
            RegisterBackwardAnimationChild(AddMixerChild(clips, thresholds, speeds, synchronize, animSet.runBackwardRight, new Vector2(1f, -1f), SynchronizeLocomotionChildren, BackwardAnimationSpeedMultiplier));

            locomotionMixerTransition.Animations = clips.ToArray();
            locomotionMixerTransition.Thresholds = thresholds.ToArray();
            locomotionMixerTransition.Speeds = speeds.ToArray();
            locomotionMixerTransition.SynchronizeChildren = synchronize.ToArray();
        }

        private AnimationClip ResolveBackpedalClip()
        {
            return animSet.walkBackward != null ? animSet.walkBackward : animSet.runBackward;
        }

        private void RegisterForwardRunAnimationChild(int childIndex)
        {
            if (childIndex >= 0)
                forwardRunAnimationChildIndices.Add(childIndex);
        }

        private void RegisterBackwardAnimationChild(int childIndex)
        {
            if (childIndex >= 0)
                backwardAnimationChildIndices.Add(childIndex);
        }

        private static int AddMixerChild(
            List<Object> clips,
            List<Vector2> thresholds,
            List<float> speeds,
            List<bool> synchronize,
            AnimationClip clip,
            Vector2 threshold,
            bool shouldSynchronize,
            float speed)
        {
            if (clip == null)
                return -1;

            var index = clips.Count;
            clips.Add(clip);
            thresholds.Add(threshold);
            speeds.Add(Mathf.Max(0.01f, speed));
            synchronize.Add(shouldSynchronize);
            return index;
        }

        private void ReadInput()
        {
            var forward = 0f;
            var strafe = 0f;
            keyboardTurnActive = false;

            if (IsPressedThisFrame(Key.Period))
                autoRunActive = !autoRunActive;

            if (IsPressed(Key.W))
                forward += 1f;
            if (autoRunActive)
                forward += 1f;
            if (IsPressed(Key.S))
                forward -= 1f;
            if (IsPressed(Key.Q))
                strafe -= 1f;
            if (IsPressed(Key.E))
                strafe += 1f;

            var wasRmbHeld = rmbHeld;
            rmbHeld = IsRightMousePressed();
            lmbHeld = IsLeftMousePressed();
            jumpPressedThisFrame = IsJumpPressedThisFrame();
            rmbPressedThisFrame = rmbHeld && !wasRmbHeld;
            rmbReleasedThisFrame = !rmbHeld && wasRmbHeld;
            rmbYawMovedOnRelease = rmbReleasedThisFrame && rmbCameraYawMovedSincePress;

            if (rmbPressedThisFrame)
            {
                cameraYawOnRmbPress = GetCameraYaw();
                rmbCameraYawMovedSincePress = false;
            }
            else if (!rmbHeld && !rmbReleasedThisFrame)
            {
                rmbCameraYawMovedSincePress = false;
            }

            if (LeftAndRightMouseMoveForward && lmbHeld && rmbHeld)
                forward += 1f;

            var hasForwardOrBackwardInput = Mathf.Abs(forward) > 0.01f;
            adKeysAreStrafing = AlwaysStrafeAD || rmbHeld || hasForwardOrBackwardInput || ShouldTreatADAsStandingJumpAirInput();
            if (adKeysAreStrafing)
            {
                if (IsPressed(Key.A))
                    strafe -= 1f;
                if (IsPressed(Key.D))
                    strafe += 1f;
            }
            else
            {
                keyboardTurnActive = IsPressed(Key.A) || IsPressed(Key.D);
            }

            rawMoveInput = new Vector2(strafe, forward);
            normalizedMoveInput = NormalizeDiagonalInput
                ? Vector2.ClampMagnitude(rawMoveInput, 1f)
                : new Vector2(Mathf.Clamp(rawMoveInput.x, -1f, 1f), Mathf.Clamp(rawMoveInput.y, -1f, 1f));

            isMoving = normalizedMoveInput.sqrMagnitude > StationaryActionThreshold * StationaryActionThreshold;
        }

        private bool ShouldTreatADAsStandingJumpAirInput()
        {
            return IsStandingJumpAirborne();
        }

        private void ApplyFacing(float deltaTime)
        {
            if (characterRoot == null)
                return;

            var previousRootYaw = characterRoot.eulerAngles.y;
            var cameraYawBeforeInput = GetCameraYaw();
            var processedCameraRotationInput = cameraRig != null && cameraRig.ProcessRotationInputForFrame();
            currentCameraYaw = GetCameraYaw();

            if (rmbYawMovedOnRelease && RightMouseHeldControlsFacing)
            {
                rmbRootCatchUpActive = true;
                rmbRootCatchUpTargetYaw = currentCameraYaw;
            }

            if (rmbHeld && !rmbPressedThisFrame)
            {
                var yawDeltaSincePress = Mathf.Abs(Mathf.DeltaAngle(cameraYawOnRmbPress, currentCameraYaw));
                if (yawDeltaSincePress > 0.01f)
                    rmbCameraYawMovedSincePress = true;
            }

            if (ShouldApplyStandingJumpAirFacing())
            {
                ApplyStandingJumpAirFacing(deltaTime);
            }
            else if (ShouldRmbControlFacing())
            {
                rmbRootCatchUpActive = false;
                if (ShouldUseDelayedRmbFacing())
                    ApplyHeldRmbDelayedRootFacing(deltaTime);
                else
                    SetCharacterRootYaw(currentCameraYaw);
            }
            else if (rmbRootCatchUpActive)
            {
                var rootYaw = characterRoot.eulerAngles.y;
                var catchUpSpeed = standingJumpLandingRootCatchUpActive
                    ? StandingJumpLandingRootCatchUpDegreesPerSecond
                    : RmbReleaseRootTurnDegreesPerSecond;
                var newRootYaw = MoveYawTowards(rootYaw, rmbRootCatchUpTargetYaw, catchUpSpeed, deltaTime);
                SetCharacterRootYaw(newRootYaw);
                if (Mathf.Abs(Mathf.DeltaAngle(newRootYaw, rmbRootCatchUpTargetYaw)) <= 0.1f)
                {
                    rmbRootCatchUpActive = false;
                    if (!standingJumpLandingVisualCatchUpActive)
                        standingJumpLandingRootCatchUpActive = false;
                }
            }
            else if (!adKeysAreStrafing)
            {
                var turn = 0f;
                if (IsPressed(Key.A))
                    turn -= 1f;
                if (IsPressed(Key.D))
                    turn += 1f;

                if (turn != 0f)
                {
                    var yawDelta = turn * KeyboardTurnDegreesPerSecond * deltaTime;
                    characterRoot.Rotate(0f, yawDelta, 0f, Space.World);
                    RotateCameraWithKeyboardTurn(yawDelta);
                }
            }

            currentRootYaw = characterRoot.eulerAngles.y;
            rootYawDegreesPerSecond = deltaTime > 0f
                ? Mathf.DeltaAngle(previousRootYaw, currentRootYaw) / deltaTime
                : 0f;

            UpdateRmbLookTwistTargets(deltaTime);
            LogRotationDiagnostics(
                cameraYawBeforeInput,
                currentCameraYaw,
                previousRootYaw,
                currentRootYaw,
                processedCameraRotationInput,
                deltaTime);

            if (rmbReleasedThisFrame)
                rmbCameraYawMovedSincePress = false;
        }

        private bool ShouldUseDelayedRmbFacing()
        {
            return RmbDelayedRootFacing && !isMoving;
        }

        private bool ShouldRmbControlFacing()
        {
            return rmbHeld && RightMouseHeldControlsFacing && !IsStandingJumpAirborne() && (rmbCameraYawMovedSincePress || isMoving);
        }

        private bool ShouldApplyStandingJumpAirFacing()
        {
            return StandingJumpAirFacing && standingJumpAirFacingActive && !grounded;
        }

        private bool IsStandingJumpAirborne()
        {
            return standingJumpTookOffFromStandstill && !grounded && jumpAnimationPhase != JumpAnimationPhase.Grounded;
        }

        private void ApplyStandingJumpAirFacing(float deltaTime)
        {
            var rootYaw = characterRoot.eulerAngles.y;
            LogStandingJumpAirFacing(rootYaw, rootYaw);

            if (rmbHeld && RightMouseHeldControlsFacing)
                standingJumpLookYaw = currentCameraYaw;
        }

        private void ApplyHeldRmbDelayedRootFacing(float deltaTime)
        {
            var rootYaw = characterRoot.eulerAngles.y;
            var yawDelta = Mathf.DeltaAngle(rootYaw, currentCameraYaw);
            var rootStartDegrees = Mathf.Max(0f, RmbLookRootTurnStartYawDegrees);
            if (Mathf.Abs(yawDelta) <= rootStartDegrees)
                return;

            var sign = Mathf.Sign(yawDelta);
            var targetRootYaw = currentCameraYaw - sign * rootStartDegrees;
            var newRootYaw = MoveYawTowards(rootYaw, targetRootYaw, RmbHeldRootTurnDegreesPerSecond, deltaTime);
            SetCharacterRootYaw(newRootYaw);
        }

        private void UpdateRmbLookTwistTargets(float deltaTime)
        {
            var targetLookYaw = ResolveRmbLookYaw();
            var targetUpperBodyYaw = ResolveUpperBodyLookYaw(targetLookYaw);
            var targetHeadYaw = Mathf.Clamp(
                targetLookYaw - targetUpperBodyYaw,
                -RmbLookHeadMaxYawDegrees,
                RmbLookHeadMaxYawDegrees);

            var smoothTime = RmbLookTwistSmoothTime;
            if (smoothTime <= 0f || deltaTime <= 0f)
            {
                currentUpperBodyLookYaw = targetUpperBodyYaw;
                currentHeadLookYaw = targetHeadYaw;
                upperBodyLookYawVelocity = 0f;
                headLookYawVelocity = 0f;
                return;
            }

            currentUpperBodyLookYaw = Mathf.SmoothDampAngle(
                currentUpperBodyLookYaw,
                targetUpperBodyYaw,
                ref upperBodyLookYawVelocity,
                smoothTime,
                Mathf.Infinity,
                deltaTime);

            currentHeadLookYaw = Mathf.SmoothDampAngle(
                currentHeadLookYaw,
                targetHeadYaw,
                ref headLookYawVelocity,
                smoothTime,
                Mathf.Infinity,
                deltaTime);
        }

        private float ResolveRmbLookYaw()
        {
            if (characterRoot == null)
                return 0f;

            if (rmbHeld && RightMouseHeldControlsFacing && rmbCameraYawMovedSincePress && ShouldUseDelayedRmbFacing())
                return ClampRmbLookYaw(Mathf.DeltaAngle(characterRoot.eulerAngles.y, currentCameraYaw));

            if (ShouldUseStandingJumpRmbCounterLook())
                return ClampRmbLookYaw(-visualMovementYawOffset);

            if (standingJumpAirFacingActive)
                return ClampRmbLookYaw(Mathf.DeltaAngle(characterRoot.eulerAngles.y, ResolveStandingJumpLookYaw()));

            if (ShouldUseFacingLockedStrafeAnimation(normalizedMoveInput))
                return ClampRmbLookYaw(-visualMovementYawOffset);

            if (rmbRootCatchUpActive)
                return ClampRmbLookYaw(Mathf.DeltaAngle(characterRoot.eulerAngles.y, rmbRootCatchUpTargetYaw));

            return 0f;
        }

        private float ResolveStandingJumpLookYaw()
        {
            if (rmbHeld && RightMouseHeldControlsFacing)
                return currentCameraYaw;

            return standingJumpLookYaw;
        }

        private float ClampRmbLookYaw(float yaw)
        {
            var maxYaw = RmbLookHeadMaxYawDegrees + RmbLookUpperBodyMaxYawDegrees;
            return Mathf.Clamp(yaw, -maxYaw, maxYaw);
        }

        private float ResolveUpperBodyLookYaw(float lookYaw)
        {
            var sign = Mathf.Sign(lookYaw);
            var beyondHead = Mathf.Max(0f, Mathf.Abs(lookYaw) - RmbLookHeadMaxYawDegrees);
            return sign * Mathf.Min(beyondHead, RmbLookUpperBodyMaxYawDegrees);
        }

        private void SetCharacterRootYaw(float yaw)
        {
            var euler = characterRoot.eulerAngles;
            euler.y = yaw;
            characterRoot.eulerAngles = euler;
        }

        private static float MoveYawTowards(float currentYaw, float targetYaw, float degreesPerSecond, float deltaTime)
        {
            if (degreesPerSecond <= 0f || deltaTime <= 0f)
                return currentYaw;

            return Mathf.MoveTowardsAngle(currentYaw, targetYaw, degreesPerSecond * deltaTime);
        }

        private float GetCameraYaw()
        {
            if (cameraRig != null)
                return cameraRig.CurrentYaw;

            return cameraTransform != null ? cameraTransform.eulerAngles.y : currentCameraYaw;
        }

        private void ResolveCameraRig()
        {
            if (cameraRig != null)
                return;

            if (cameraTransform != null)
                cameraRig = cameraTransform.GetComponentInParent<WowThirdPersonCameraRig>();
        }

        private void RotateCameraWithKeyboardTurn(float yawDelta)
        {
            ResolveCameraRig();
            if (cameraRig == null)
                return;

            cameraRig.AddYawDelta(yawDelta);
            currentCameraYaw = cameraRig.CurrentYaw;
        }

        private void ApplyMovement(float deltaTime)
        {
            if (characterRoot == null)
                return;

            var localMove = new Vector3(normalizedMoveInput.x, 0f, normalizedMoveInput.y);
            worldMove = characterRoot.rotation * localMove;
            lastWorldMove = worldMove;
            currentSpeedMultiplier = CalculateSpeedMultiplier();

            if (!UseScriptDrivenMotion)
                return;

            var desiredHorizontalVelocity = worldMove * (RunSpeed * currentSpeedMultiplier);

            if (characterController != null && UseCharacterController)
            {
                ApplyCharacterControllerRuntimeSettings();

                var wasGrounded = grounded;
                grounded = characterController.isGrounded;
                var isStartupGrounding = startupGroundingGraceFramesRemaining > 0;
                var shouldJump = jumpPressedThisFrame && grounded && JumpHeight > 0f;
                var horizontalVelocity = desiredHorizontalVelocity;
                if (shouldJump)
                {
                    startupGroundingGraceFramesRemaining = 0;
                    CaptureJumpTakeoffVelocity(desiredHorizontalVelocity);
                    horizontalVelocity = airborneHorizontalVelocity;
                    verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
                    grounded = false;
                    StartJumpAnimation();
                }
                else if (grounded)
                {
                    ResetAirborneHorizontalVelocity();
                    verticalVelocity = GroundedStickVelocity;
                }
                else
                {
                    CaptureStandingJumpAirInputIfNeeded();
                    horizontalVelocity = airborneHorizontalVelocity;
                    verticalVelocity += Gravity * deltaTime;
                }

                var motion = horizontalVelocity * deltaTime + Vector3.up * (verticalVelocity * deltaTime);
                characterController.Move(motion);
                grounded = characterController.isGrounded;

                if (isStartupGrounding && !shouldJump)
                {
                    startupGroundingGraceFramesRemaining--;
                    if (grounded)
                    {
                        ResetAirborneHorizontalVelocity();
                        verticalVelocity = GroundedStickVelocity;
                        if (jumpAnimationPhase != JumpAnimationPhase.Grounded)
                            ResumeLocomotion(0f);
                    }
                    else if (startupGroundingGraceFramesRemaining == 0 && jumpAnimationPhase == JumpAnimationPhase.Grounded)
                    {
                        CaptureJumpTakeoffVelocity(desiredHorizontalVelocity);
                        StartAirborneAnimation();
                    }

                    return;
                }

                if (!grounded && !shouldJump && wasGrounded && jumpAnimationPhase == JumpAnimationPhase.Grounded)
                {
                    CaptureJumpTakeoffVelocity(desiredHorizontalVelocity);
                    StartAirborneAnimation();
                }
                else if (!wasGrounded && grounded && !shouldJump)
                {
                    StartLandingAnimation();
                }
            }
            else
            {
                grounded = true;
                verticalVelocity = 0f;
                ResetAirborneHorizontalVelocity();
                var horizontal = desiredHorizontalVelocity * deltaTime;
                characterRoot.position += horizontal;
            }
        }

        private void ApplyCharacterControllerRuntimeSettings()
        {
            var skinWidth = Mathf.Max(0.001f, CharacterControllerSkinWidth);
            if (!Mathf.Approximately(characterController.skinWidth, skinWidth))
                characterController.skinWidth = skinWidth;

            var minMoveDistance = Mathf.Max(0f, CharacterControllerMinMoveDistance);
            if (!Mathf.Approximately(characterController.minMoveDistance, minMoveDistance))
                characterController.minMoveDistance = minMoveDistance;
        }

        private void CaptureJumpTakeoffVelocity(Vector3 desiredHorizontalVelocity)
        {
            airborneHorizontalVelocity = desiredHorizontalVelocity;
            standingJumpTookOffFromStandstill = desiredHorizontalVelocity.sqrMagnitude <= 0.0001f;
            canCaptureStandingJumpAirInput = standingJumpTookOffFromStandstill;
            standingJumpAirFacingActive = false;
            hasStandingJumpAirCommittedInput = false;
            standingJumpAirCommittedInput = Vector2.zero;
            standingJumpDiagnosticActive = false;

            standingJumpTakeoffRootYaw = GetCameraYaw();

            standingJumpLookYaw = standingJumpTakeoffRootYaw;
        }

        private void CaptureStandingJumpAirInputIfNeeded()
        {
            if (!canCaptureStandingJumpAirInput || normalizedMoveInput.sqrMagnitude <= StationaryActionThreshold * StationaryActionThreshold)
                return;

            var localMove = new Vector3(normalizedMoveInput.x, 0f, normalizedMoveInput.y);
            var inputDirection = Quaternion.Euler(0f, standingJumpTakeoffRootYaw, 0f) * localMove;
            inputDirection.y = 0f;
            if (inputDirection.sqrMagnitude <= 0.0001f)
                return;

            inputDirection.Normalize();
            airborneHorizontalVelocity =
                inputDirection *
                (RunSpeed * currentSpeedMultiplier * Mathf.Max(0f, StandingJumpAirInputSpeedMultiplier));
            standingJumpAirTargetRootYaw = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg;
            standingJumpAirFacingActive = StandingJumpAirFacing;
            standingJumpAirCommittedInput = normalizedMoveInput;
            hasStandingJumpAirCommittedInput = true;
            canCaptureStandingJumpAirInput = false;
            standingJumpDiagnosticActive = true;
            nextStandingJumpDiagnosticTime = 0f;
            LogStandingJumpAirCapture(inputDirection);
        }

        private void ResetAirborneHorizontalVelocity()
        {
            airborneHorizontalVelocity = Vector3.zero;
            canCaptureStandingJumpAirInput = false;
            standingJumpTookOffFromStandstill = false;
            standingJumpAirFacingActive = false;
            hasStandingJumpAirCommittedInput = false;
            standingJumpAirCommittedInput = Vector2.zero;
            standingJumpDiagnosticActive = false;
        }

        private float CalculateSpeedMultiplier()
        {
            if (normalizedMoveInput.y < -0.01f)
                return BackwardSpeedMultiplier;
            if (Mathf.Abs(normalizedMoveInput.x) > 0.01f && Mathf.Abs(normalizedMoveInput.y) <= 0.01f)
                return StrafeSpeedMultiplier;
            return 1f;
        }

        private void UpdateLocomotionParameter(float deltaTime)
        {
            var animationInput = ResolveAnimationMoveInput();
            effectiveLocomotionParameter = ResolveEffectiveLocomotionParameter(animationInput);
            targetMixerParameter = effectiveLocomotionParameter;
            lastVisualMixerParameter = visualMixerParameter;

            if (MixerParameterSmoothTime <= 0f)
            {
                visualMixerParameter = targetMixerParameter;
            }
            else
            {
                var alpha = 1f - Mathf.Exp(-deltaTime / Mathf.Max(0.0001f, MixerParameterSmoothTime));
                visualMixerParameter = Vector2.LerpUnclamped(visualMixerParameter, targetMixerParameter, alpha);
            }

            visualMixerParameterVelocity = deltaTime > 0f
                ? (visualMixerParameter - lastVisualMixerParameter) / deltaTime
                : Vector2.zero;

            if (locomotionMixerState != null)
            {
                ApplyLocomotionChildSpeeds();
                locomotionMixerState.Parameter = visualMixerParameter;
            }

            UpdateVisualMovementYaw(deltaTime, animationInput);
            ClearFacingLockedStrafeLogStateIfNeeded();
        }

        private void ApplyLocomotionChildSpeeds()
        {
            if (locomotionMixerState == null)
                return;

            var forwardSpeed = Mathf.Max(0.01f, ForwardRunAnimationSpeedMultiplier);
            for (int i = 0; i < forwardRunAnimationChildIndices.Count; i++)
            {
                var childIndex = forwardRunAnimationChildIndices[i];
                if (childIndex >= 0 && childIndex < locomotionMixerState.ChildCount)
                    locomotionMixerState.GetChild(childIndex).Speed = forwardSpeed;
            }

            var speed = Mathf.Max(0.01f, BackwardAnimationSpeedMultiplier);
            for (int i = 0; i < backwardAnimationChildIndices.Count; i++)
            {
                var childIndex = backwardAnimationChildIndices[i];
                if (childIndex >= 0 && childIndex < locomotionMixerState.ChildCount)
                    locomotionMixerState.GetChild(childIndex).Speed = speed;
            }
        }

        private void StartJumpAnimation()
        {
            if (jumpAnimationPhase == JumpAnimationPhase.Starting || jumpAnimationPhase == JumpAnimationPhase.Airborne)
                return;

            if (GetLayerWeight(landingUpperLayer) > 0.001f)
                StartLandingArmDiagnostics("new-jump-while-landing-layer-active");
            StopLandingUpperBody(JumpAnimationFadeDuration);
            landingStartedMoving = false;
            landingTimeRemaining = 0f;
            jumpAnimationPhase = JumpAnimationPhase.Starting;
            currentJumpState = PlayBaseJumpClip(animSet != null ? animSet.jumpStart : null);
            if (currentJumpState == null)
                StartAirborneAnimation();
        }

        private void StartAirborneAnimation()
        {
            landingTimeRemaining = 0f;
            jumpAnimationPhase = JumpAnimationPhase.Airborne;
            currentJumpState = PlayBaseJumpClip(animSet != null ? animSet.jumpLoop : null);
        }

        private void StartLandingAnimation()
        {
            BeginStandingJumpLandingCatchUpIfNeeded();
            verticalVelocity = GroundedStickVelocity;
            jumpAnimationPhase = JumpAnimationPhase.Landing;
            landingStartedMoving = IsMovingForLanding();
            landingTimeRemaining = ResolveLandingDuration();
            currentJumpState = PlayBaseJumpClip(animSet != null ? animSet.jumpLand : null);
            StartLandingArmDiagnostics("landing-start");
            LogJumpLandingLifecycle("START_BASE", "touchdown");
            if (currentJumpState == null)
            {
                LogJumpLandingLifecycle("BASE_EXIT", "missing-base-clip");
                ResumeLocomotion();
                return;
            }

            ConfigureLandingState(currentJumpState);
        }

        private void BeginStandingJumpLandingCatchUpIfNeeded()
        {
            if (!standingJumpAirFacingActive || characterRoot == null)
                return;

            standingJumpLandingRootCatchUpActive = true;
            rmbRootCatchUpActive = true;
            rmbRootCatchUpTargetYaw = currentCameraYaw;
            standingJumpLandingVisualCatchUpActive = true;
            LogStandingJumpLandingCatchUp();
            standingJumpAirFacingActive = false;
            hasStandingJumpAirCommittedInput = false;
            standingJumpAirCommittedInput = Vector2.zero;
            standingJumpDiagnosticActive = false;
        }

        private AnimancerState PlayBaseJumpClip(AnimationClip clip)
        {
            if (baseLayer == null || clip == null)
                return null;

            var state = baseLayer.Play(clip, JumpAnimationFadeDuration);
            state.TimeD = 0;
            return state;
        }

        private void UpdateJumpAnimation(float deltaTime)
        {
            if (jumpAnimationPhase == JumpAnimationPhase.Grounded)
                return;

            if (jumpAnimationPhase == JumpAnimationPhase.Starting)
            {
                if (currentJumpState == null || currentJumpState.NormalizedTime >= 0.95)
                    StartAirborneAnimation();
                return;
            }

            if (jumpAnimationPhase == JumpAnimationPhase.Landing)
            {
                PromoteLandingToMovingIfNeeded();
                landingTimeRemaining -= deltaTime;
                if (currentJumpState == null || currentJumpState.Clip == null)
                {
                    ResumeLocomotion();
                    return;
                }

                var exitNormalizedTime = GetLandingExitNormalizedTime();
                if (currentJumpState.NormalizedTime >= exitNormalizedTime)
                {
                    LogJumpLandingLifecycle("BASE_EXIT", "normalized-exit");
                    StartLandingUpperBodyFromBaseExit();
                    ResumeLocomotion();
                    return;
                }

                if (landingTimeRemaining <= 0f)
                {
                    LogJumpLandingLifecycle("BASE_EXIT", "duration-expired");
                    StartLandingUpperBodyFromBaseExit();
                    ResumeLocomotion();
                }
            }
        }

        private void PromoteLandingToMovingIfNeeded()
        {
            if (landingStartedMoving || !IsMovingForLanding())
                return;

            landingStartedMoving = true;
            landingTimeRemaining = Mathf.Min(landingTimeRemaining, ResolveLandingDuration());
            if (currentJumpState != null && currentJumpState.Clip != null)
                ConfigureLandingState(currentJumpState);

            if (currentLandingUpperBodyState != null && currentLandingUpperBodyState.Clip != null)
            {
                landingUpperBodyTimeRemaining = Mathf.Min(landingUpperBodyTimeRemaining, ResolveLandingUpperBodyDuration());
                ConfigureLandingUpperBodyState(currentLandingUpperBodyState);
                if (currentLandingUpperBodyState.NormalizedTime >= GetLandingUpperBodyExitNormalizedTime())
                {
                    LogJumpLandingLifecycle("UPPER_EXIT", "promoted-past-moving-exit");
                    StopLandingUpperBody();
                }
            }

            LogLandingPromotedToMoving();
            StartLandingArmDiagnostics("landing-promoted-to-moving");
        }

        private void ConfigureLandingState(AnimancerState state)
        {
            var exitNormalizedTime = GetLandingExitNormalizedTime();
            var duration = Mathf.Max(0.01f, landingTimeRemaining);
            state.Duration = duration / exitNormalizedTime;
        }

        private bool ShouldUseLandingUpperBodyOverlay()
        {
            return ResolveLandingUpperBodyDuration() > ResolveLandingDuration() + 0.001f;
        }

        private void StartLandingUpperBodyFromBaseExit()
        {
            var initialNormalizedTime = GetStateNormalizedTime(currentJumpState);
            var remainingDuration = ResolveLandingUpperBodyDuration() - ResolveLandingDuration();
            StartLandingUpperBody(initialNormalizedTime, remainingDuration, "base-exit");
        }

        private void StartLandingUpperBody(
            float initialNormalizedTime = 0f,
            float remainingDurationOverride = -1f,
            string reason = "touchdown")
        {
            if (landingUpperLayer == null || animSet == null || animSet.jumpLand == null)
                return;

            if (currentUpperBodyState != null && upperLayer != null && upperLayer.Weight > 0.01f)
                return;

            if (!ShouldUseLandingUpperBodyOverlay())
            {
                StartLandingArmDiagnostics("landing-upper-skipped");
                LogJumpLandingLifecycle("SKIP_UPPER", "upper-does-not-outlive-base");
                return;
            }

            var exitNormalizedTime = GetLandingUpperBodyExitNormalizedTime();
            initialNormalizedTime = Mathf.Clamp(initialNormalizedTime, 0f, exitNormalizedTime);
            if (initialNormalizedTime >= exitNormalizedTime)
            {
                StartLandingArmDiagnostics("landing-upper-initial-past-exit");
                LogJumpLandingLifecycle("SKIP_UPPER", "initial-time-past-exit");
                return;
            }

            landingUpperBodyTimeRemaining = remainingDurationOverride >= 0f
                ? Mathf.Max(0.01f, remainingDurationOverride)
                : ResolveLandingUpperBodyDuration();
            SetLandingUpperBodyMaskGroup(0f);
            currentLandingUpperBodyState = landingUpperLayer.Play(animSet.jumpLand);
            currentLandingUpperBodyState.NormalizedTime = initialNormalizedTime;
            ConfigureLandingUpperBodyState(currentLandingUpperBodyState);
            landingUpperLayer.Weight = 1f;
            StartLandingArmDiagnostics("landing-upper-start");
            LogJumpLandingLifecycle("START_UPPER", reason);
        }

        private void ConfigureLandingUpperBodyState(AnimancerState state)
        {
            if (state == null)
                return;

            var exitNormalizedTime = GetLandingUpperBodyExitNormalizedTime();
            var currentNormalizedTime = Mathf.Clamp(GetStateNormalizedTime(state), 0f, exitNormalizedTime);
            var remainingNormalizedTime = Mathf.Max(0.01f, exitNormalizedTime - currentNormalizedTime);
            state.Duration = Mathf.Max(0.01f, landingUpperBodyTimeRemaining) / remainingNormalizedTime;
        }

        private void UpdateLandingUpperBody(float deltaTime)
        {
            if (currentLandingUpperBodyState == null || currentLandingUpperBodyState.Clip == null)
                return;

            landingUpperBodyTimeRemaining -= deltaTime;
            var exitNormalizedTime = GetLandingUpperBodyExitNormalizedTime();
            if (currentLandingUpperBodyState.NormalizedTime >= exitNormalizedTime)
            {
                LogJumpLandingLifecycle("UPPER_EXIT", "normalized-exit");
                StopLandingUpperBody();
                return;
            }

            if (landingUpperBodyTimeRemaining <= 0f)
            {
                LogJumpLandingLifecycle("UPPER_EXIT", "duration-expired");
                StopLandingUpperBody();
            }
        }

        private void StopLandingUpperBody(float fadeOut = -1f)
        {
            if (landingUpperLayer == null)
                return;

            var duration = fadeOut >= 0f ? fadeOut : ResolveLandingUpperBodyFadeOutDuration();
            LogJumpLandingLifecycle("STOP_UPPER", $"fade={duration:0.###}");
            StartLandingArmDiagnostics($"landing-upper-stop-fade-{duration:0.###}");
            PrepareLandingUpperBodyFadePose(duration);
            landingUpperLayer.StartFade(0f, duration);
            currentLandingUpperBodyState = null;
            landingUpperBodyTimeRemaining = 0f;
            if (jumpAnimationPhase != JumpAnimationPhase.Landing)
                landingStartedMoving = false;
        }

        private void PrepareLandingUpperBodyFadePose(float fadeOutDuration)
        {
            if (!LandingUpperBodyContinuesDuringFade || fadeOutDuration <= 0f)
            {
                FreezeLandingUpperBodyFadePose();
                return;
            }

            var state = currentLandingUpperBodyState != null
                ? currentLandingUpperBodyState
                : GetLayerCurrentState(landingUpperLayer);
            if (state == null)
                return;

            var currentNormalizedTime = GetStateNormalizedTime(state);
            var targetNormalizedTime = Mathf.Clamp(LandingUpperBodyFadeEndNormalizedTime, 0.1f, 0.98f);
            var remainingNormalizedTime = targetNormalizedTime - currentNormalizedTime;
            if (currentNormalizedTime < 0f || remainingNormalizedTime <= 0.001f)
            {
                FreezeLandingUpperBodyFadePose();
                return;
            }

            var fadeDuration = Mathf.Max(0.01f, fadeOutDuration);
            var retimedDuration = fadeDuration / remainingNormalizedTime;
            state.Duration = retimedDuration;
            WowLocomotionDebugLog.Log(
                LandingDiagnosticLogs,
                this,
                () =>
                    "Jump landing ARM_FADE_CONTINUES. " +
                    $"frame={Time.frameCount}, t={Time.unscaledTime:0.###}, state={DescribeState(state)}, current={currentNormalizedTime:0.###}, " +
                    $"target={targetNormalizedTime:0.###}, remaining={remainingNormalizedTime:0.###}, fade={fadeDuration:0.###}, " +
                    $"retimedDuration={retimedDuration:0.###}, actualDuration={state.Duration:0.###}, actualSpeed={state.Speed:0.###}, " +
                    $"landingLayerWeight={GetLayerWeight(landingUpperLayer):0.###}");
        }

        private void FreezeLandingUpperBodyFadePose()
        {
            var state = currentLandingUpperBodyState != null
                ? currentLandingUpperBodyState
                : GetLayerCurrentState(landingUpperLayer);
            if (state == null)
                return;

            state.Speed = 0f;
            WowLocomotionDebugLog.Log(
                LandingDiagnosticLogs,
                this,
                () =>
                    "Jump landing ARM_FADE_POSE_FROZEN. " +
                    $"frame={Time.frameCount}, t={Time.unscaledTime:0.###}, state={DescribeState(state)}, landingLayerWeight={GetLayerWeight(landingUpperLayer):0.###}");
        }

        private float ResolveLandingDuration()
        {
            var duration = landingStartedMoving
                ? MovingJumpLandingDuration
                : JumpLandingDuration;
            return Mathf.Max(0.01f, duration);
        }

        private float ResolveLandingUpperBodyDuration()
        {
            var duration = landingStartedMoving
                ? MovingJumpLandingUpperBodyDuration
                : JumpLandingUpperBodyDuration;
            return Mathf.Max(0.01f, duration);
        }

        private float ResolveLandingUpperBodyFadeOutDuration()
        {
            var duration = landingStartedMoving
                ? MovingJumpLandingUpperBodyFadeOutDuration
                : JumpLandingUpperBodyFadeOutDuration;
            return Mathf.Max(0f, duration);
        }

        private bool IsMovingForLanding()
        {
            return normalizedMoveInput.sqrMagnitude > StationaryActionThreshold * StationaryActionThreshold;
        }

        private float GetLandingExitNormalizedTime()
        {
            return landingStartedMoving
                ? Mathf.Clamp(MovingJumpLandingExitNormalizedTime, 0.1f, 0.98f)
                : Mathf.Clamp(JumpLandingExitNormalizedTime, 0.5f, 0.98f);
        }

        private float GetLandingUpperBodyExitNormalizedTime()
        {
            return landingStartedMoving
                ? Mathf.Clamp(MovingJumpLandingUpperBodyExitNormalizedTime, 0.1f, 0.98f)
                : Mathf.Clamp(JumpLandingUpperBodyExitNormalizedTime, 0.1f, 0.98f);
        }

        private void ResumeLocomotion(float fadeDuration = -1f)
        {
            jumpAnimationPhase = JumpAnimationPhase.Grounded;
            currentJumpState = null;
            landingTimeRemaining = 0f;
            if (currentLandingUpperBodyState == null)
                landingStartedMoving = false;

            StartLandingArmDiagnostics("resume-locomotion");
            if (baseLayer == null || locomotionMixerTransition == null || !locomotionMixerTransition.IsValid)
                return;

            locomotionState = baseLayer.Play(locomotionMixerTransition, fadeDuration >= 0f ? fadeDuration : LocomotionFadeDuration);
            locomotionMixerState = locomotionState as Vector2MixerState;
            if (locomotionMixerState != null)
                locomotionMixerState.Parameter = visualMixerParameter;
        }

        private Vector2 ResolveEffectiveLocomotionParameter(Vector2 input)
        {
            if (UseStrafeClipsForInPlaceTurn && input.sqrMagnitude <= 0.0001f)
            {
                var turnSpeed = Mathf.Abs(rootYawDegreesPerSecond);
                if (turnSpeed >= InPlaceTurnMinSpeedDegreesPerSecond)
                {
                    var fullSpeed = Mathf.Max(InPlaceTurnMinSpeedDegreesPerSecond, InPlaceTurnFullSpeedDegreesPerSecond);
                    var turnParameter = Mathf.Clamp(turnSpeed / fullSpeed, 0f, 1f);
                    return new Vector2(Mathf.Sign(rootYawDegreesPerSecond) * turnParameter, 0f);
                }
            }

            if (StrafeAnimationMode != WowStrafeAnimationMode.UseForwardRunWithVisualYaw)
                return input;

            if (input.sqrMagnitude <= 0.0001f)
                return Vector2.zero;

            if (ShouldUseFacingLockedStrafeAnimation(input))
            {
                var effective = ResolveForwardRunLocomotionParameter(input);
                LogFacingLockedStrafeDecision(input, effective);
                return effective;
            }

            return ResolveForwardRunLocomotionParameter(input);
        }

        private void UpdateVisualMovementYaw(float deltaTime, Vector2 animationInput)
        {
            targetVisualMovementYawOffset = ResolveVisualMovementYawOffset(animationInput);
            if (standingJumpLandingVisualCatchUpActive)
            {
                targetVisualMovementYawOffset = 0f;
                visualMovementYawOffset = MoveYawTowards(
                    visualMovementYawOffset,
                    targetVisualMovementYawOffset,
                    StandingJumpLandingRootCatchUpDegreesPerSecond,
                    deltaTime);

                if (Mathf.Abs(Mathf.DeltaAngle(visualMovementYawOffset, targetVisualMovementYawOffset)) <= 0.1f)
                {
                    visualMovementYawOffset = 0f;
                    standingJumpLandingVisualCatchUpActive = false;
                    standingJumpLandingRootCatchUpActive = false;
                }

                LogStandingJumpLandingVisualCatchUp();
                return;
            }

            if (VisualMovementYawSmoothTime <= 0f)
            {
                visualMovementYawOffset = targetVisualMovementYawOffset;
                return;
            }

            var alpha = 1f - Mathf.Exp(-deltaTime / Mathf.Max(0.0001f, VisualMovementYawSmoothTime));
            visualMovementYawOffset = Mathf.LerpAngle(visualMovementYawOffset, targetVisualMovementYawOffset, alpha);
        }

        private float ResolveVisualMovementYawOffset(Vector2 input)
        {
            if (StrafeAnimationMode != WowStrafeAnimationMode.UseForwardRunWithVisualYaw)
                return 0f;

            if (ShouldUseStandingJumpCommittedAnimationInput())
                return ResolveStandingJumpAirVisualYawOffset();

            if (input.sqrMagnitude <= 0.0001f)
                return 0f;

            if (ShouldUseFacingLockedStrafeAnimation(input))
                return ResolveFacingLockedStrafeVisualYaw(input);

            return ResolveMovementVisualYaw(input);
        }

        private bool ShouldUseFacingLockedStrafeAnimation(Vector2 input)
        {
            return rmbHeld && RightMouseHeldControlsFacing && Mathf.Abs(input.x) > 0.01f;
        }

        private Vector2 ResolveAnimationMoveInput()
        {
            if (ShouldUseStandingJumpCommittedAnimationInput())
                return standingJumpAirCommittedInput;

            return normalizedMoveInput;
        }

        private bool ShouldUseStandingJumpCommittedAnimationInput()
        {
            return hasStandingJumpAirCommittedInput && standingJumpTookOffFromStandstill && !grounded;
        }

        private bool ShouldUseStandingJumpRmbCounterLook()
        {
            return standingJumpAirFacingActive && rmbHeld && RightMouseHeldControlsFacing;
        }

        private float ResolveStandingJumpAirVisualYawOffset()
        {
            if (characterRoot == null)
                return 0f;

            var yaw = Mathf.DeltaAngle(characterRoot.eulerAngles.y, standingJumpAirTargetRootYaw);
            var maxYaw = Mathf.Max(0f, RmbStrafeVisualYawDegrees);
            return Mathf.Clamp(yaw, -maxYaw, maxYaw);
        }

        private Vector2 ResolveForwardRunLocomotionParameter(Vector2 input)
        {
            var magnitude = Mathf.Clamp01(input.magnitude);
            var signedForward = input.y < -0.01f ? -magnitude : magnitude;
            return new Vector2(0f, signedForward);
        }

        private float ResolveFacingLockedStrafeVisualYaw(Vector2 input)
        {
            var movementYaw = ResolveMovementVisualYaw(input);
            var maxYaw = Mathf.Max(0f, RmbStrafeVisualYawDegrees);
            return Mathf.Clamp(movementYaw, -maxYaw, maxYaw);
        }

        private static float ResolveMovementVisualYaw(Vector2 input)
        {
            if (input.y < -0.01f)
                return Mathf.Atan2(-input.x, -input.y) * Mathf.Rad2Deg;

            return Mathf.Atan2(input.x, Mathf.Max(0f, input.y)) * Mathf.Rad2Deg;
        }

        private void LogFacingLockedStrafeDecision(Vector2 input, Vector2 effective)
        {
            if (lastLoggedFacingLockedStrafe)
                return;

            lastLoggedFacingLockedStrafe = true;
            WowLocomotionDebugLog.Log(
                LocomotionDecisionLogs,
                this,
                () =>
                    "RMB facing-locked strafe using forward-run animation. " +
                    $"raw={Format(rawMoveInput)}, normalized={Format(input)}, effective={Format(effective)}, " +
                    $"visualYaw={visualMovementYawOffset:0.###}, headYaw={currentHeadLookYaw:0.###}, upperYaw={currentUpperBodyLookYaw:0.###}");
        }

        private void ClearFacingLockedStrafeLogStateIfNeeded()
        {
            if (!ShouldUseFacingLockedStrafeAnimation(normalizedMoveInput))
                lastLoggedFacingLockedStrafe = false;
        }

        private void LogStandingJumpAirCapture(Vector3 inputDirection)
        {
            WowLocomotionDebugLog.Log(
                LandingDiagnosticLogs,
                this,
                () =>
                    "Standing jump air CAPTURE. " +
                    $"frame={Time.frameCount}, t={Time.unscaledTime:0.###}, " +
                    $"raw={Format(rawMoveInput)}, normalized={Format(normalizedMoveInput)}, committed={Format(standingJumpAirCommittedInput)}, " +
                    $"inputDirection={Format(inputDirection)}, airborneVelocity={Format(airborneHorizontalVelocity)}, " +
                    $"takeoffYawReference={standingJumpTakeoffRootYaw:0.###}, targetRootYaw={standingJumpAirTargetRootYaw:0.###}, " +
                    $"rootYaw={currentRootYaw:0.###}, cameraYaw={currentCameraYaw:0.###}, cameraRootDelta={Mathf.DeltaAngle(currentRootYaw, currentCameraYaw):0.###}, " +
                    $"rmbHeld={rmbHeld}, lmbHeld={lmbHeld}, rightMouseControlsFacing={RightMouseHeldControlsFacing}, canCapture={canCaptureStandingJumpAirInput}, " +
                    $"phase={jumpAnimationPhase}, grounded={grounded}");
        }

        private void LogStandingJumpAirFacing(float previousRootYaw, float newRootYaw)
        {
            if (!standingJumpDiagnosticActive)
                standingJumpDiagnosticActive = true;

            if (Time.unscaledTime < nextStandingJumpDiagnosticTime)
                return;

            nextStandingJumpDiagnosticTime = Time.unscaledTime + StandingJumpDiagnosticInterval;
            WowLocomotionDebugLog.Log(
                LandingDiagnosticLogs,
                this,
                () =>
                    "Standing jump air FACING. " +
                    $"frame={Time.frameCount}, t={Time.unscaledTime:0.###}, " +
                    $"raw={Format(rawMoveInput)}, normalized={Format(normalizedMoveInput)}, committed={Format(standingJumpAirCommittedInput)}, " +
                    $"previousRootYaw={previousRootYaw:0.###}, newRootYaw={newRootYaw:0.###}, targetRootYaw={standingJumpAirTargetRootYaw:0.###}, " +
                    $"targetDelta={Mathf.DeltaAngle(newRootYaw, standingJumpAirTargetRootYaw):0.###}, turnSpeed={StandingJumpAirRootTurnDegreesPerSecond:0.###}, " +
                    "rootFrozenInAir=True, " +
                    $"cameraYaw={currentCameraYaw:0.###}, takeoffYawReference={standingJumpTakeoffRootYaw:0.###}, " +
                    $"rmbHeld={rmbHeld}, lmbHeld={lmbHeld}, rightMouseControlsFacing={RightMouseHeldControlsFacing}, rmbCounterLook={ShouldUseStandingJumpRmbCounterLook()}, " +
                    $"airborneVelocity={Format(airborneHorizontalVelocity)}, visualYaw={visualMovementYawOffset:0.###}, targetVisualYaw={targetVisualMovementYawOffset:0.###}, " +
                    $"phase={jumpAnimationPhase}, grounded={grounded}");
        }

        private void LogStandingJumpLandingCatchUp()
        {
            WowLocomotionDebugLog.Log(
                LandingDiagnosticLogs,
                this,
                () =>
                    "Standing jump air LANDING_CATCHUP. " +
                    $"frame={Time.frameCount}, t={Time.unscaledTime:0.###}, " +
                    $"rootYaw={currentRootYaw:0.###}, cameraYaw={currentCameraYaw:0.###}, catchUpTargetYaw={rmbRootCatchUpTargetYaw:0.###}, " +
                    $"committed={Format(standingJumpAirCommittedInput)}, targetRootYaw={standingJumpAirTargetRootYaw:0.###}, visualYaw={visualMovementYawOffset:0.###}, " +
                    $"rmbHeld={rmbHeld}, lmbHeld={lmbHeld}, phase={jumpAnimationPhase}, grounded={grounded}");
        }

        private void LogStandingJumpLandingVisualCatchUp()
        {
            if (Time.unscaledTime < nextStandingJumpDiagnosticTime)
                return;

            nextStandingJumpDiagnosticTime = Time.unscaledTime + StandingJumpDiagnosticInterval;
            WowLocomotionDebugLog.Log(
                LandingDiagnosticLogs,
                this,
                () =>
                    "Standing jump landing VISUAL_CATCHUP. " +
                    $"frame={Time.frameCount}, t={Time.unscaledTime:0.###}, visualYaw={visualMovementYawOffset:0.###}, targetVisualYaw={targetVisualMovementYawOffset:0.###}, " +
                    $"turnSpeed={StandingJumpLandingRootCatchUpDegreesPerSecond:0.###}, rootYaw={currentRootYaw:0.###}, cameraYaw={currentCameraYaw:0.###}, " +
                    $"rmbRootCatchUpActive={rmbRootCatchUpActive}, standingJumpLandingRootCatchUpActive={standingJumpLandingRootCatchUpActive}, phase={jumpAnimationPhase}, grounded={grounded}");
        }

        private void LogLandingPromotedToMoving()
        {
            WowLocomotionDebugLog.Log(
                LandingDiagnosticLogs,
                this,
                () =>
                    "Jump landing PROMOTED_TO_MOVING. " +
                    $"frame={Time.frameCount}, t={Time.unscaledTime:0.###}, raw={Format(rawMoveInput)}, normalized={Format(normalizedMoveInput)}, " +
                    $"baseRemaining={landingTimeRemaining:0.###}, baseExit={GetLandingExitNormalizedTime():0.###}, baseState={GetStateName(currentJumpState)}, " +
                    $"upperRemaining={landingUpperBodyTimeRemaining:0.###}, upperExit={GetLandingUpperBodyExitNormalizedTime():0.###}, upperState={GetStateName(currentLandingUpperBodyState)}, " +
                    $"rootYaw={currentRootYaw:0.###}, cameraYaw={currentCameraYaw:0.###}, phase={jumpAnimationPhase}, grounded={grounded}");
        }

        private void LogRotationDiagnostics(
            float cameraYawBeforeInput,
            float cameraYawAfterInput,
            float rootYawBefore,
            float rootYawAfter,
            bool processedCameraRotationInput,
            float deltaTime)
        {
            if (!rmbHeld && !rmbReleasedThisFrame && !rmbRootCatchUpActive)
                return;

            var cameraYawDelta = Mathf.DeltaAngle(cameraYawBeforeInput, cameraYawAfterInput);
            var rootYawDelta = Mathf.DeltaAngle(rootYawBefore, rootYawAfter);
            var cameraRootDelta = Mathf.DeltaAngle(rootYawAfter, cameraYawAfterInput);
            var controllerVelocity = characterController != null ? characterController.velocity : Vector3.zero;
            WowLocomotionDebugLog.Log(
                RotationDiagnosticLogs,
                this,
                () =>
                    "RMB rotation DIAGNOSTIC. " +
                    $"frame={Time.frameCount}, t={Time.unscaledTime:0.###}, dt={deltaTime:0.####}, unscaledDt={Time.unscaledDeltaTime:0.####}, fixedDt={Time.fixedDeltaTime:0.####}, " +
                    $"processedCameraInput={processedCameraRotationInput}, cameraReady={(cameraRig != null && cameraRig.IsRotationInputReady)}, cameraApplied={(cameraRig != null && cameraRig.HasAppliedRotationSinceBegin)}, " +
                    $"rmbHeld={rmbHeld}, rmbPressed={rmbPressedThisFrame}, rmbReleased={rmbReleasedThisFrame}, yawMovedSincePress={rmbCameraYawMovedSincePress}, " +
                    $"shouldRmbControl={ShouldRmbControlFacing()}, delayedRmb={ShouldUseDelayedRmbFacing()}, catchUp={rmbRootCatchUpActive}, " +
                    $"cameraBefore={cameraYawBeforeInput:0.###}, cameraAfter={cameraYawAfterInput:0.###}, cameraDelta={cameraYawDelta:0.###}, " +
                    $"rootBefore={rootYawBefore:0.###}, rootAfter={rootYawAfter:0.###}, rootDelta={rootYawDelta:0.###}, rootSpeed={rootYawDegreesPerSecond:0.###}, cameraRootDelta={cameraRootDelta:0.###}, " +
                    $"isMoving={isMoving}, raw={Format(rawMoveInput)}, normalized={Format(normalizedMoveInput)}, controllerVelocity={Format(controllerVelocity)}, phase={jumpAnimationPhase}, grounded={grounded}");
        }

        private void LogJumpLandingLifecycle(string stage, string reason)
        {
            WowLocomotionDebugLog.Log(
                LandingDiagnosticLogs,
                this,
                () =>
                    "Jump landing LIFECYCLE. " +
                    $"stage={stage}, reason={reason}, frame={Time.frameCount}, t={Time.unscaledTime:0.###}, " +
                    $"landingStartedMoving={landingStartedMoving}, raw={Format(rawMoveInput)}, normalized={Format(normalizedMoveInput)}, " +
                    $"baseRemaining={landingTimeRemaining:0.###}, baseDuration={ResolveLandingDuration():0.###}, baseExit={GetLandingExitNormalizedTime():0.###}, " +
                    $"baseState={GetStateName(currentJumpState)}, baseTime={GetStateNormalizedTime(currentJumpState):0.###}, baseLayerState={GetStateName(GetLayerCurrentState(baseLayer))}, baseLayerTime={GetStateNormalizedTime(GetLayerCurrentState(baseLayer)):0.###}, " +
                    $"upperRemaining={landingUpperBodyTimeRemaining:0.###}, upperDuration={ResolveLandingUpperBodyDuration():0.###}, upperExit={GetLandingUpperBodyExitNormalizedTime():0.###}, " +
                    $"upperFade={ResolveLandingUpperBodyFadeOutDuration():0.###}, upperFadeContinues={LandingUpperBodyContinuesDuringFade}, upperFadeEnd={LandingUpperBodyFadeEndNormalizedTime:0.###}, " +
                    $"upperState={GetStateName(currentLandingUpperBodyState)}, upperTime={GetStateNormalizedTime(currentLandingUpperBodyState):0.###}, landingLayerState={GetStateName(GetLayerCurrentState(landingUpperLayer))}, landingLayerTime={GetStateNormalizedTime(GetLayerCurrentState(landingUpperLayer)):0.###}, " +
                    $"landingLayerWeight={GetLayerWeight(landingUpperLayer):0.###}, upperLayerWeight={GetLayerWeight(upperLayer):0.###}, " +
                    $"upperLayerState={GetStateName(GetLayerCurrentState(upperLayer))}, upperLayerTime={GetStateNormalizedTime(GetLayerCurrentState(upperLayer)):0.###}, " +
                    $"locomotionState={GetStateName(locomotionState)}, visualMixer={Format(visualMixerParameter)}, phase={jumpAnimationPhase}, grounded={grounded}");
        }

        private void StartLandingArmDiagnostics(string reason)
        {
            landingArmDiagnosticFramesRemaining = Mathf.Max(landingArmDiagnosticFramesRemaining, LandingArmDiagnosticFrameBudget);
            WowLocomotionDebugLog.Log(
                LandingDiagnosticLogs,
                this,
                () =>
                    "Jump landing ARM_DIAGNOSTIC_START. " +
                    $"reason={reason}, frame={Time.frameCount}, t={Time.unscaledTime:0.###}, " +
                    $"landingStartedMoving={landingStartedMoving}, phase={jumpAnimationPhase}, grounded={grounded}, raw={Format(rawMoveInput)}, normalized={Format(normalizedMoveInput)}, " +
                    $"baseLayerState={GetStateName(GetLayerCurrentState(baseLayer))}, landingLayerState={GetStateName(GetLayerCurrentState(landingUpperLayer))}, " +
                    $"landingLayerWeight={GetLayerWeight(landingUpperLayer):0.###}, upperLayerState={GetStateName(GetLayerCurrentState(upperLayer))}, upperLayerWeight={GetLayerWeight(upperLayer):0.###}");
        }

        private void LogLandingArmDiagnostics(string stage, bool consumeFrame)
        {
            if (landingArmDiagnosticFramesRemaining <= 0)
                return;

            WowLocomotionDebugLog.Log(
                LandingDiagnosticLogs,
                this,
                () =>
                    "Jump landing ARM_SAMPLE. " +
                    $"stage={stage}, frame={Time.frameCount}, t={Time.unscaledTime:0.###}, dt={Time.deltaTime:0.####}, remainingFrames={landingArmDiagnosticFramesRemaining}, " +
                    $"landingStartedMoving={landingStartedMoving}, phase={jumpAnimationPhase}, grounded={grounded}, verticalVelocity={verticalVelocity:0.###}, " +
                    $"raw={Format(rawMoveInput)}, normalized={Format(normalizedMoveInput)}, isMoving={isMoving}, " +
                    $"rootYaw={currentRootYaw:0.###}, cameraYaw={currentCameraYaw:0.###}, cameraRootDelta={Mathf.DeltaAngle(currentRootYaw, currentCameraYaw):0.###}, " +
                    $"visualYaw={visualMovementYawOffset:0.###}, targetVisualYaw={targetVisualMovementYawOffset:0.###}, headYaw={currentHeadLookYaw:0.###}, upperYaw={currentUpperBodyLookYaw:0.###}, " +
                    $"baseTracked={DescribeState(currentJumpState)}, baseLayer={DescribeState(GetLayerCurrentState(baseLayer))}, baseLayerWeight={GetLayerWeight(baseLayer):0.###}, " +
                    $"landingTracked={DescribeState(currentLandingUpperBodyState)}, landingLayer={DescribeState(GetLayerCurrentState(landingUpperLayer))}, landingLayerWeight={GetLayerWeight(landingUpperLayer):0.###}, " +
                    $"upperLayer={DescribeState(GetLayerCurrentState(upperLayer))}, upperLayerWeight={GetLayerWeight(upperLayer):0.###}, locomotion={DescribeState(locomotionState)}, visualMixer={Format(visualMixerParameter)}, " +
                    $"spineLower={FormatBone(lookSpineLower)}, spineMiddle={FormatBone(lookSpineMiddle)}, spineUpper={FormatBone(lookSpineUpper)}, chest={FormatBone(lookChest)}, neck={FormatBone(lookNeck)}, head={FormatBone(lookHead)}, " +
                    $"lClavicle={FormatBone(debugLeftClavicle)}, lShoulder={FormatBone(debugLeftShoulder)}, lUpperArm={FormatBone(debugLeftUpperArm)}, lForearm={FormatBone(debugLeftForearm)}, lHand={FormatBone(debugLeftHand)}, " +
                    $"rClavicle={FormatBone(debugRightClavicle)}, rShoulder={FormatBone(debugRightShoulder)}, rUpperArm={FormatBone(debugRightUpperArm)}, rForearm={FormatBone(debugRightForearm)}, rHand={FormatBone(debugRightHand)}");

            if (consumeFrame)
                landingArmDiagnosticFramesRemaining--;
        }

        private void LogFacingLockedStrafeDiagnosticsIfNeeded()
        {
            var active = ShouldUseFacingLockedStrafeAnimation(normalizedMoveInput);
            if (!active)
            {
                if (facingLockedStrafeDiagnosticActive)
                {
                    facingLockedStrafeDiagnosticActive = false;
                    WowLocomotionDebugLog.Log(
                        LocomotionDecisionLogs,
                        this,
                        () =>
                            "RMB facing-locked strafe EXIT. " +
                            $"frame={Time.frameCount}, raw={Format(rawMoveInput)}, normalized={Format(normalizedMoveInput)}, " +
                            $"visualYaw={visualMovementYawOffset:0.###}, targetVisualYaw={targetVisualMovementYawOffset:0.###}, " +
                            $"modelLocalYaw={GetModelRootLocalYaw():0.###}, rootYaw={currentRootYaw:0.###}, cameraYaw={currentCameraYaw:0.###}");
                }

                return;
            }

            if (!facingLockedStrafeDiagnosticActive)
            {
                facingLockedStrafeDiagnosticActive = true;
                nextFacingLockedStrafeDiagnosticTime = 0f;
            }

            if (Time.unscaledTime < nextFacingLockedStrafeDiagnosticTime)
                return;

            nextFacingLockedStrafeDiagnosticTime = Time.unscaledTime + FacingLockedStrafeDiagnosticInterval;
            var movementYaw = ResolveMovementVisualYaw(normalizedMoveInput);
            var clampedMovementYaw = ResolveFacingLockedStrafeVisualYaw(normalizedMoveInput);
            var appliedYaw = ModelYawOffsetDegrees + visualMovementYawOffset;
            var mixerParameter = locomotionMixerState != null ? locomotionMixerState.Parameter : Vector2.zero;
            WowLocomotionDebugLog.Log(
                LocomotionDecisionLogs,
                this,
                () =>
                    "RMB facing-locked strafe DIAGNOSTIC. " +
                    $"frame={Time.frameCount}, t={Time.unscaledTime:0.###}, " +
                    $"raw={Format(rawMoveInput)}, normalized={Format(normalizedMoveInput)}, " +
                    $"effective={Format(effectiveLocomotionParameter)}, targetMixer={Format(targetMixerParameter)}, " +
                    $"visualMixer={Format(visualMixerParameter)}, stateMixer={Format(mixerParameter)}, mixerVelocity={Format(visualMixerParameterVelocity)}, " +
                    $"movementYaw={movementYaw:0.###}, clampedMovementYaw={clampedMovementYaw:0.###}, maxStrafeYaw={RmbStrafeVisualYawDegrees:0.###}, " +
                    $"targetVisualYaw={targetVisualMovementYawOffset:0.###}, visualYaw={visualMovementYawOffset:0.###}, visualYawSmooth={VisualMovementYawSmoothTime:0.###}, " +
                    $"appliedModelYaw={appliedYaw:0.###}, modelLocalYaw={GetModelRootLocalYaw():0.###}, modelYawOffset={ModelYawOffsetDegrees:0.###}, " +
                    $"rootYaw={currentRootYaw:0.###}, cameraYaw={currentCameraYaw:0.###}, cameraRootDelta={Mathf.DeltaAngle(currentRootYaw, currentCameraYaw):0.###}, " +
                    $"headYaw={currentHeadLookYaw:0.###}, upperYaw={currentUpperBodyLookYaw:0.###}, " +
                    $"rmbHeld={rmbHeld}, rightMouseControlsFacing={RightMouseHeldControlsFacing}, strafeMode={StrafeAnimationMode}, " +
                    $"baseState={GetStateName(baseLayer != null ? baseLayer.CurrentState : null)}, jumpPhase={jumpAnimationPhase}, grounded={grounded}");
        }

        private void PlayUpperBodyWithGroup(
            AnimationClip clip,
            WowWeightedMaskGroup group,
            bool restart,
            float fadeIn)
        {
            if (upperLayer == null)
                return;

            var duration = fadeIn >= 0f ? fadeIn : UpperBodyFadeInDuration;
            StopLandingUpperBody(duration);
            SetWeightedMaskGroup(group, duration);
            currentUpperBodyState = upperLayer.Play(clip);
            if (restart)
                currentUpperBodyState.TimeD = 0;
            upperLayer.StartFade(1f, duration);
            maskResetDelayRemaining = 0f;
        }

        private void UpdateUpperBodyCompletion(float deltaTime)
        {
            if (maskResetDelayRemaining > 0f)
            {
                maskResetDelayRemaining -= deltaTime;
                if (maskResetDelayRemaining <= 0f)
                    SetWeightedMaskGroup(WowWeightedMaskGroup.NoUpperBodyOverride, 0f);
            }

            if (currentUpperBodyState == null || currentUpperBodyState.Clip == null || currentUpperBodyState.Clip.isLooping)
                return;

            if (currentUpperBodyState.NormalizedTime >= 1.0)
                StopUpperBody();
        }

        private void SetWeightedMaskGroup(WowWeightedMaskGroup group, float fadeDuration)
        {
            activeWeightedMaskGroup = group;
            if (weightedMaskLayers == null || weightedMaskLayers.Layers == null)
                return;

            if (fadeDuration > 0f)
                weightedMaskLayers.FadeWeights(UpperLayerIndex, (int)group, fadeDuration);
            else
                weightedMaskLayers.SetWeights(UpperLayerIndex, (int)group);
        }

        private void SetLandingUpperBodyMaskGroup(float fadeDuration)
        {
            if (weightedMaskLayers == null || weightedMaskLayers.Layers == null)
                return;

            if (fadeDuration > 0f)
                weightedMaskLayers.FadeWeights(LandingUpperLayerIndex, (int)WowWeightedMaskGroup.UpperBodyActionWhileMoving, fadeDuration);
            else
                weightedMaskLayers.SetWeights(LandingUpperLayerIndex, (int)WowWeightedMaskGroup.UpperBodyActionWhileMoving);
        }

        private void WarnForRootMotionTranslations()
        {
            if (animSet == null)
                return;

            var clips = new List<AnimationClip>();
            AddClip(clips, animSet.idle);
            AddClip(clips, animSet.runForward);
            AddClip(clips, animSet.runBackward);
            AddClip(clips, animSet.strafeLeft);
            AddClip(clips, animSet.strafeRight);
            AddClip(clips, animSet.runForwardLeft);
            AddClip(clips, animSet.runForwardRight);
            AddClip(clips, animSet.runBackwardLeft);
            AddClip(clips, animSet.runBackwardRight);
            AddClip(clips, animSet.cast);
            AddClip(clips, animSet.attack);
            AddClip(clips, animSet.aimPose);
            AddClip(clips, animSet.readyPose);

            for (int i = 0; i < clips.Count; i++)
            {
                var clip = clips[i];
                if (clip != null && clip.averageSpeed.sqrMagnitude > 0.0001f)
                {
                    Debug.LogWarning(
                        $"Clip '{clip.name}' reports average root-motion translation. Animator root-motion streams are enabled for WeightedMaskLayers, but OnAnimatorMove discards root motion and script-driven movement remains authoritative.",
                        clip);
                }
            }
        }

        private void OnAnimatorMove()
        {
            // WeightedMaskLayers needs root-motion streams enabled, but movement remains script-driven.
        }

        private void CaptureAuthoredModelRootRotation()
        {
            if (modelRoot == null)
                return;

            authoredModelRootLocalRotation = modelRoot.localRotation;
            hasAuthoredModelRootLocalRotation = true;
        }

        private void InitializeLookTwistBones()
        {
            if (boneProfile == null || characterRoot == null)
                return;

            lookSpineLower = ResolveRuntimeBone(boneProfile.spineLower);
            lookSpineMiddle = ResolveRuntimeBone(boneProfile.spineMiddle);
            lookSpineUpper = ResolveRuntimeBone(boneProfile.spineUpper);
            lookChest = ResolveRuntimeBone(boneProfile.chest);
            lookNeck = ResolveRuntimeBone(boneProfile.neck);
            lookHead = ResolveRuntimeBone(boneProfile.head);
            debugLeftClavicle = ResolveRuntimeBone(boneProfile.leftClavicle);
            debugLeftShoulder = ResolveRuntimeBone(boneProfile.leftShoulder);
            debugLeftUpperArm = ResolveRuntimeBone(boneProfile.leftUpperArm);
            debugLeftForearm = ResolveRuntimeBone(boneProfile.leftForearm);
            debugLeftHand = ResolveRuntimeBone(boneProfile.leftHand);
            debugRightClavicle = ResolveRuntimeBone(boneProfile.rightClavicle);
            debugRightShoulder = ResolveRuntimeBone(boneProfile.rightShoulder);
            debugRightUpperArm = ResolveRuntimeBone(boneProfile.rightUpperArm);
            debugRightForearm = ResolveRuntimeBone(boneProfile.rightForearm);
            debugRightHand = ResolveRuntimeBone(boneProfile.rightHand);
        }

        private Transform ResolveRuntimeBone(Transform profileBone)
        {
            if (profileBone == null)
                return null;

            if (profileBone.IsChildOf(characterRoot))
                return profileBone;

            return FindChildByName(characterRoot, profileBone.name);
        }

        private void ApplyVisualModelYaw()
        {
            if (modelRoot == null || !hasAuthoredModelRootLocalRotation)
                return;

            modelRoot.localRotation = authoredModelRootLocalRotation * Quaternion.Euler(0f, ModelYawOffsetDegrees + visualMovementYawOffset, 0f);
        }

        private void ApplyRmbLookTwist()
        {
            if (characterRoot == null)
                return;

            if (ShouldUseFacingLockedStrafeAnimation(normalizedMoveInput) || ShouldUseStandingJumpRmbCounterLook())
            {
                ApplyFacingLockedStrafeLookTwist();
                return;
            }

            ApplyWeightedYaw(
                currentUpperBodyLookYaw,
                lookSpineLower,
                0.10f,
                lookSpineMiddle,
                0.20f,
                lookSpineUpper,
                0.30f,
                lookChest,
                0.40f);

            ApplyWeightedYaw(
                currentHeadLookYaw,
                lookNeck,
                0.35f,
                lookHead,
                0.65f);
        }

        private void ApplyFacingLockedStrafeLookTwist()
        {
            ApplyWeightedYaw(
                currentUpperBodyLookYaw,
                lookSpineUpper,
                0.35f,
                lookChest,
                0.65f);

            ApplyWeightedYaw(
                currentHeadLookYaw,
                lookNeck,
                0.35f,
                lookHead,
                0.65f);
        }

        private void ApplyWeightedYaw(
            float yaw,
            Transform first,
            float firstWeight,
            Transform second,
            float secondWeight)
        {
            var totalWeight = GetWeight(first, firstWeight) + GetWeight(second, secondWeight);
            if (totalWeight <= 0f)
                return;

            ApplyWeightedYaw(first, yaw, firstWeight, totalWeight);
            ApplyWeightedYaw(second, yaw, secondWeight, totalWeight);
        }

        private void ApplyWeightedYaw(
            float yaw,
            Transform first,
            float firstWeight,
            Transform second,
            float secondWeight,
            Transform third,
            float thirdWeight,
            Transform fourth,
            float fourthWeight)
        {
            var totalWeight =
                GetWeight(first, firstWeight) +
                GetWeight(second, secondWeight) +
                GetWeight(third, thirdWeight) +
                GetWeight(fourth, fourthWeight);
            if (totalWeight <= 0f)
                return;

            ApplyWeightedYaw(first, yaw, firstWeight, totalWeight);
            ApplyWeightedYaw(second, yaw, secondWeight, totalWeight);
            ApplyWeightedYaw(third, yaw, thirdWeight, totalWeight);
            ApplyWeightedYaw(fourth, yaw, fourthWeight, totalWeight);
        }

        private void ApplyWeightedYaw(Transform bone, float yaw, float weight, float totalWeight)
        {
            if (bone == null || weight <= 0f)
                return;

            var weightedYaw = yaw * (weight / totalWeight);
            if (Mathf.Abs(weightedYaw) <= 0.001f)
                return;

            bone.rotation = Quaternion.AngleAxis(weightedYaw, characterRoot.up) * bone.rotation;
        }

        private static float GetWeight(Transform bone, float weight)
        {
            return bone != null ? Mathf.Max(0f, weight) : 0f;
        }

        private static Transform FindChildByName(Transform root, string childName)
        {
            if (root == null || string.IsNullOrEmpty(childName))
                return null;

            if (root.name == childName)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                var child = FindChildByName(root.GetChild(i), childName);
                if (child != null)
                    return child;
            }

            return null;
        }

        private static bool IsPressed(Key key)
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard[key].isPressed;
        }

        private static bool IsPressedThisFrame(Key key)
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard[key].wasPressedThisFrame;
        }

        private static bool IsLeftMousePressed()
        {
            var mouse = Mouse.current;
            return mouse != null && mouse.leftButton.isPressed;
        }

        private static bool IsRightMousePressed()
        {
            var mouse = Mouse.current;
            return mouse != null && mouse.rightButton.isPressed;
        }

        private static bool IsJumpPressedThisFrame()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
        }

        private static void AddClip(List<AnimationClip> clips, AnimationClip clip)
        {
            if (clip != null && !clips.Contains(clip))
                clips.Add(clip);
        }

        private void LogWarnings(List<string> warnings)
        {
            for (int i = 0; i < warnings.Count; i++)
                Debug.LogWarning(warnings[i], this);
        }

        private void LogErrors(List<string> errors)
        {
            for (int i = 0; i < errors.Count; i++)
                Debug.LogError(errors[i], this);
        }

        private void OnGUI()
        {
            if (!drawDebug)
                return;

            GUI.Box(new Rect(12f, 12f, 430f, 390f), BuildDebugString());
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos)
                return;

            var origin = characterRoot != null ? characterRoot.position : transform.position;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(origin, origin + lastWorldMove.normalized * 2f);
        }

        private float GetModelRootLocalYaw()
        {
            return modelRoot != null ? Mathf.DeltaAngle(0f, modelRoot.localEulerAngles.y) : 0f;
        }

        private static string Format(Vector2 value)
        {
            return $"({value.x:0.###}, {value.y:0.###})";
        }

        private static string Format(Vector3 value)
        {
            return $"({value.x:0.###}, {value.y:0.###}, {value.z:0.###})";
        }

        private static string FormatBone(Transform bone)
        {
            if (bone == null)
                return "None";

            var localEuler = bone.localEulerAngles;
            return $"{bone.name}({Mathf.DeltaAngle(0f, localEuler.x):0.#},{Mathf.DeltaAngle(0f, localEuler.y):0.#},{Mathf.DeltaAngle(0f, localEuler.z):0.#})";
        }

        private static string DescribeState(AnimancerState state)
        {
            if (state == null)
                return "None";

            return $"{GetStateName(state)}@n={GetStateNormalizedTime(state):0.###},w={state.Weight:0.###},spd={state.Speed:0.###}";
        }

        private static AnimancerState GetLayerCurrentState(AnimancerLayer layer)
        {
            return layer != null ? layer.CurrentState : null;
        }

        private static string GetStateName(AnimancerState state)
        {
            if (state == null)
                return "None";
            return state.Clip != null ? state.Clip.name : state.ToString();
        }

        private static float GetStateNormalizedTime(AnimancerState state)
        {
            return state != null ? (float)state.NormalizedTime : -1f;
        }

        private static float GetLayerWeight(AnimancerLayer layer)
        {
            return layer != null ? layer.Weight : -1f;
        }
    }
}
