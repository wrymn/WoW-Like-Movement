using Sirenix.OdinInspector;
using UnityEngine;

namespace WowLocomotionResearch
{
    /// <summary>
    /// Defines how side movement is animated when the imported Generic rig does not provide true strafe clips.
    /// </summary>
    public enum WowStrafeAnimationMode
    {
        /// <summary>Use the clips assigned to strafeLeft and strafeRight in the locomotion animation set.</summary>
        UseAssignedStrafeClips = 0,

        /// <summary>Use forward/backward run clips and yaw the visual model toward the local movement direction.</summary>
        UseForwardRunWithVisualYaw = 1
    }

    /// <summary>
    /// Canonical runtime settings for WoW-like character movement and locomotion blending.
    /// </summary>
    [CreateAssetMenu(
        fileName = "WowMovementSettings",
        menuName = "Research/WoW Locomotion/Movement Settings")]
    [Searchable]
    public sealed class WowMovementSettings : ScriptableObject
    {
        [BoxGroup("Character Controller")]
        [Tooltip(
            "Use CharacterController.Move when a CharacterController is assigned. Disable to move the transform directly, which ignores controller collision handling.")]
        [SerializeField]
        private bool useCharacterController = true;

        [BoxGroup("Character Controller")]
        [Tooltip(
            "CharacterController skin width in meters. Lower reduces the visible hover above terrain; higher is more collision-stable but leaves a larger ground gap.")]
        [SerializeField]
        private float characterControllerSkinWidth = 0.025f;

        [BoxGroup("Character Controller")]
        [Tooltip(
            "Smallest CharacterController.Move distance Unity will process. 0 applies tiny gravity/grounding moves; higher values can ignore subtle floor-snapping motion.")]
        [SerializeField]
        private float characterControllerMinMoveDistance;

        [BoxGroup("Input And Facing")]
        [Tooltip(
            "Move the character from script at constant velocity. Disable only when testing animation/root-motion driven movement.")]
        [SerializeField]
        private bool useScriptDrivenMotion = true;

        [BoxGroup("Input And Facing")]
        [Tooltip(
            "When enabled, holding RMB makes the character face the camera yaw. Disable to let RMB rotate only the camera.")]
        [SerializeField]
        private bool rightMouseHeldControlsFacing = true;

        [BoxGroup("Input And Facing")]
        [Tooltip(
            "When enabled, A/D always strafe. When disabled, A/D turn only when pressed alone; W+A, W+D, S+A, and S+D still strafe diagonally without rotating the camera/root.")]
        [SerializeField]
        private bool alwaysStrafeAD;

        [BoxGroup("Input And Facing")]
        [Tooltip(
            "Clamp diagonal movement to the same top speed as straight movement. Disable to let diagonal input become faster.")]
        [SerializeField]
        private bool normalizeDiagonalInput = true;

        [BoxGroup("Input And Facing")]
        [Tooltip(
            "When enabled, holding both mouse buttons moves forward like WoW. Disable if mouse buttons should not drive movement.")]
        [SerializeField]
        private bool leftAndRightMouseMoveForward = true;

        [BoxGroup("RMB Look Twist")]
        [Tooltip(
            "When enabled and the character is standing still, RMB camera rotation is absorbed by head/upper-body twist before the root turns. Movement still faces the camera directly so controls stay responsive.")]
        [SerializeField]
        private bool rmbDelayedRootFacing = true;

        [BoxGroup("RMB Look Twist")]
        [LabelText("Head Max Yaw")]
        [Tooltip(
            "Maximum yaw in degrees added across neck/head while RMB look is active. Lower makes the root start helping sooner; higher lets the head look farther before the body/root follows.")]
        [SerializeField, Range(0f, 90f)]
        private float rmbLookHeadMaxYawDegrees = 60f;

        [BoxGroup("RMB Look Twist")]
        [LabelText("Upper Body Max Yaw")]
        [Tooltip(
            "Maximum yaw in degrees added across spine/chest after the head yaw is saturated. Lower keeps the torso straighter; higher lets the upper body twist farther before root catch-up is needed.")]
        [SerializeField, Range(0f, 90f)]
        private float rmbLookUpperBodyMaxYawDegrees = 40f;

        [BoxGroup("RMB Look Twist")]
        [LabelText("Root Starts Turning After")]
        [Tooltip(
            "Camera/root yaw difference in degrees before the character root starts slowly following during RMB hold. Lower makes the feet/root react earlier; higher keeps the response mostly in the head and torso.")]
        [SerializeField, Range(0f, 180f)]
        private float rmbLookRootTurnStartYawDegrees = 90f;

        [BoxGroup("RMB Look Twist")]
        [LabelText("Held Root Catch-Up Speed")]
        [Tooltip(
            "Maximum root turn speed in degrees per second while RMB is still held and the look angle exceeds Root Starts Turning After. Lower keeps the feet planted longer; higher rotates the whole character sooner.")]
        [SerializeField]
        private float rmbHeldRootTurnDegreesPerSecond = 45f;

        [BoxGroup("RMB Look Twist")]
        [LabelText("Release Root Turn Speed")]
        [Tooltip(
            "Maximum root turn speed in degrees per second after RMB is released. Lower makes the character settle to the final camera direction slower; higher snaps closer to the final direction.")]
        [SerializeField]
        private float rmbReleaseRootTurnDegreesPerSecond = 540f;

        [BoxGroup("RMB Look Twist")]
        [LabelText("Look Twist Smooth Time")]
        [Tooltip(
            "Smoothing time for head and upper-body look twist. Lower reacts faster; higher makes the head/torso ease more softly into and out of the look offset.")]
        [SerializeField]
        private float rmbLookTwistSmoothTime = 0.06f;

        [BoxGroup("Time Control")]
        [LabelText("Slow Time Scale")]
        [Tooltip(
            "Time.timeScale applied while slow time is toggled on. Lower means stronger slow motion; 1 means normal speed. Starts at 0.2 for 20% speed.")]
        [SerializeField, Range(0.01f, 1.0f)]
        private float slowTimeScale = 0.2f;

        [BoxGroup("Ground Movement")]
        [Tooltip("Forward movement speed in meters per second. Lower is slower; higher is faster.")]
        [SerializeField]
        private float runSpeed = 7.0f;

        [BoxGroup("Ground Movement")]
        [Tooltip(
            "Reserved walk speed in meters per second. Lower is slower walking; higher is faster walking. Current base movement still uses Run Speed.")]
        [SerializeField]
        private float walkSpeed = 2.5f;

        [BoxGroup("Ground Movement")]
        [Tooltip(
            "Backward speed as a multiplier of Run Speed. Lower makes backpedal slower; 1 matches forward speed; higher than 1 makes backward faster than forward.")]
        [SerializeField]
        private float backwardSpeedMultiplier = 0.65f;

        [BoxGroup("Locomotion Animation")]
        [Tooltip(
            "Playback speed multiplier for forward run animations only. Lower makes the run cycle slower; higher makes it cycle faster. Physical movement speed is unchanged.")]
        [SerializeField]
        private float forwardRunAnimationSpeedMultiplier = 1.0f;

        [BoxGroup("Locomotion Animation")]
        [Tooltip(
            "Playback speed multiplier for the backpedal animation only. Lower makes the backward clip cycle slower; higher makes it cycle faster. Physical movement speed is unchanged.")]
        [SerializeField]
        private float backwardAnimationSpeedMultiplier = 1.0f;

        [BoxGroup("Locomotion Animation")]
        [Tooltip(
            "Maximum visual model yaw used while RMB is held and side movement is pressed. 90 lets pure A/D face fully sideways like WoW; lower values keep the body more camera-facing. Head/torso counter-yaw keeps aim toward camera.")]
        [SerializeField]
        private float rmbStrafeVisualYawDegrees = 90f;

        [BoxGroup("Debug Logging")]
        [Tooltip(
            "Enable sparse locomotion decision logs. Disabled means message lambdas are never evaluated, preventing string allocation and concatenation.")]
        [SerializeField]
        private bool locomotionDecisionLogs;

        [BoxGroup("Debug Logging")]
        [Tooltip(
            "Enable verbose jump/landing diagnostic logs, including per-frame Animancer layer states and arm bone rotations. Disable during normal play because this intentionally emits many log lines.")]
        [SerializeField]
        private bool landingDiagnosticLogs;

        [BoxGroup("Debug Logging")]
        [Tooltip(
            "Enable per-frame RMB rotation diagnostics. Higher log volume, but useful for detecting camera/root one-frame lag, yaw snaps, and transform rotation jitter.")]
        [SerializeField]
        private bool rotationDiagnosticLogs;

        [BoxGroup("Ground Movement")]
        [Tooltip(
            "Pure strafe speed as a multiplier of Run Speed. Lower makes side movement slower; 1 matches forward speed; higher than 1 makes strafing faster than forward.")]
        [SerializeField]
        private float strafeSpeedMultiplier = 0.90f;

        [BoxGroup("Air And Jump Physics")]
        [Tooltip(
            "Airborne horizontal speed multiplier for the first movement input after jumping from standstill. Lower gives less mid-air nudge; higher lets a standing jump commit to more horizontal speed. Running jumps ignore this because their takeoff velocity is locked.")]
        [SerializeField]
        private float standingJumpAirInputSpeedMultiplier = 0.35f;

        [BoxGroup("Standing Jump Air Facing")]
        [Tooltip(
            "When enabled, standing jumps that receive their first movement input in mid-air rotate the lower body/root toward that committed air velocity while head/torso counter-aim toward the look direction.")]
        [SerializeField]
        private bool standingJumpAirFacing = true;

        [BoxGroup("Standing Jump Air Facing")]
        [LabelText("Air Root Turn Speed")]
        [Tooltip(
            "Root/lower-body turn speed in degrees per second after the first movement key is pressed during a standing jump. Lower turns legs slower; higher points the lower body toward jump velocity faster.")]
        [SerializeField]
        private float standingJumpAirRootTurnDegreesPerSecond = 360f;

        [BoxGroup("Standing Jump Air Facing")]
        [LabelText("Landing Character Turn Speed")]
        [Tooltip(
            "Character root turn speed in degrees per second after landing from a standing air-controlled jump. Lower makes the character rotate back toward camera-facing slower; higher snaps the character back sooner.")]
        [SerializeField]
        private float standingJumpLandingRootCatchUpDegreesPerSecond = 720f;

        [BoxGroup("Input And Facing")]
        [Tooltip(
            "A/D keyboard-turn speed in degrees per second when not strafing. Lower turns slower; higher turns faster.")]
        [SerializeField]
        private float keyboardTurnDegreesPerSecond = 120.0f;

        [BoxGroup("Air And Jump Physics")]
        [Tooltip("Vertical acceleration while airborne. More negative falls faster; closer to 0 falls slower.")]
        [SerializeField]
        private float gravity = -25.0f;

        [BoxGroup("Air And Jump Physics")]
        [Tooltip(
            "Small downward velocity applied while grounded to keep the CharacterController snapped to the floor. More negative sticks harder; closer to 0 is lighter.")]
        [SerializeField]
        private float groundedStickVelocity = -2.0f;

        [BoxGroup("Air And Jump Physics")]
        [Tooltip(
            "Jump apex height in meters. Lower produces a smaller hop; higher launches the character farther upward.")]
        [SerializeField]
        private float jumpHeight = 1.35f;

        [BoxGroup("Jump Animation - Shared")]
        [LabelText("Jump Clip Blend Fade")]
        [Tooltip(
            "Base-layer fade used when entering jump start, fall loop, and landing clips. Lower snaps faster; higher blends jump animation changes more softly.")]
        [SerializeField]
        private float jumpAnimationFadeDuration = 0.08f;

        [BoxGroup("Standing Landing - Lower Body Exit Sequence")]
        [LabelText("Lower Body Duration")]
        [Tooltip(
            "How long the lower body stays in the landing clip before standing locomotion resumes. Lower exits faster; higher keeps the legs in the landing longer.")]
        [SerializeField]
        private float jumpLandingDuration = 1.0f;

        [BoxGroup("Moving Landing - Lower Body Exit Sequence")]
        [LabelText("Lower Body Duration")]
        [Tooltip(
            "How long the lower body stays in the landing clip while movement continues. Lower gets legs back to run/strafe sooner and reduces sliding; higher gives moving landings more visible weight.")]
        [SerializeField]
        private float movingJumpLandingDuration = 0.12f;

        [BoxGroup("Standing Landing - Lower Body Exit Sequence")]
        [LabelText("Lower Body Exit Clip Time")]
        [Tooltip(
            "Normalized point in the landing clip used as the standing lower-body exit pose. Lower exits before the deep crouch; higher plays more of the landing. Values near 1 can expose bad looping data.")]
        [SerializeField, Range(0.5f, 0.98f)]
        private float jumpLandingExitNormalizedTime = 0.92f;

        [BoxGroup("Moving Landing - Lower Body Exit Sequence")]
        [LabelText("Lower Body Exit Clip Time")]
        [Tooltip(
            "Normalized point in the landing clip used when movement continues through landing. Lower returns to run/strafe before the deep crouch; higher plays more landing pose and can look like foot sliding.")]
        [SerializeField, Range(0.1f, 0.98f)]
        private float movingJumpLandingExitNormalizedTime = 0.38f;

        [BoxGroup("Standing Landing - Upper Body Follow-Through")]
        [LabelText("Upper Body Duration")]
        [Tooltip(
            "How long the masked upper-body landing recovery stays active while standing. Lower returns torso/arms to locomotion sooner; higher keeps the recovery visible longer.")]
        [SerializeField]
        private float jumpLandingUpperBodyDuration = 1.0f;

        [BoxGroup("Moving Landing - Upper Body Follow-Through")]
        [LabelText("Upper Body Duration")]
        [Tooltip(
            "How long the masked upper-body landing recovery stays active while movement continues. Lower removes torso recovery sooner; higher smooths the upper body while legs are already running.")]
        [SerializeField]
        private float movingJumpLandingUpperBodyDuration = 0.25f;

        [BoxGroup("Standing Landing - Upper Body Follow-Through")]
        [LabelText("Upper Body Exit Clip Time")]
        [Tooltip(
            "Normalized landing clip point used by the standing upper-body overlay. Lower exits earlier; higher lets torso/arms play deeper into the landing recovery.")]
        [SerializeField, Range(0.1f, 0.98f)]
        private float jumpLandingUpperBodyExitNormalizedTime = 0.92f;

        [BoxGroup("Moving Landing - Upper Body Follow-Through")]
        [LabelText("Upper Body Exit Clip Time")]
        [Tooltip(
            "Normalized landing clip point used by the moving upper-body overlay. Lower exits earlier; higher lets torso/arms continue the fall/landing recovery longer after legs resume.")]
        [SerializeField, Range(0.1f, 0.98f)]
        private float movingJumpLandingUpperBodyExitNormalizedTime = 0.92f;

        [BoxGroup("Standing Landing - Upper Body Follow-Through")]
        [LabelText("Upper Body Fade Out")]
        [Tooltip(
            "Fade-out time after the standing upper-body landing overlay reaches its exit clip time. Lower snaps torso/arms back faster; higher blends them out more softly.")]
        [SerializeField]
        private float jumpLandingUpperBodyFadeOutDuration = 0.15f;

        [BoxGroup("Moving Landing - Upper Body Follow-Through")]
        [LabelText("Upper Body Fade Out")]
        [Tooltip(
            "Fade-out time after the moving upper-body landing overlay reaches its exit clip time. Lower returns torso/arms faster; higher reduces jerk after legs have resumed running.")]
        [SerializeField]
        private float movingJumpLandingUpperBodyFadeOutDuration = 0.12f;

        [BoxGroup("Jump Animation - Shared")]
        [LabelText("Upper Body Continues During Fade")]
        [Tooltip(
            "When enabled, the upper-body landing clip keeps advancing while its layer fades out. This makes arms/torso finish the landing more naturally. When disabled, the upper-body pose is frozen during fade, which is safer but can look like a quick synthetic snap.")]
        [SerializeField]
        private bool landingUpperBodyContinuesDuringFade = true;

        [BoxGroup("Jump Animation - Shared")]
        [LabelText("Upper Body Fade End Clip Time")]
        [Tooltip(
            "Normalized landing clip point the upper body tries to reach by the end of fade-out. Lower stops the follow-through earlier; higher lets arms/torso finish more of the clip. Keep below 1 to avoid loop-boundary artifacts.")]
        [SerializeField, Range(0.1f, 0.98f)]
        private float landingUpperBodyFadeEndNormalizedTime = 0.98f;

        [BoxGroup("Locomotion Animation")]
        [Tooltip(
            "Extra visual yaw applied to the model child after animation evaluation. 0 keeps the authored prefab rotation; higher positive values turn the visible model to the right; lower negative values turn it to the left. Use +/-90 when an imported model faces sideways relative to the root.")]
        [SerializeField]
        private float modelYawOffsetDegrees;

        [BoxGroup("Locomotion Animation")]
        [Tooltip(
            "How side movement is animated. Assigned Strafe Clips uses strafeLeft/Right clips; Forward Run With Visual Yaw avoids shuffle clips by turning only the visual model toward movement while the logical root still faces the camera.")]
        [SerializeField]
        private WowStrafeAnimationMode strafeAnimationMode = WowStrafeAnimationMode.UseForwardRunWithVisualYaw;

        [BoxGroup("Locomotion Animation")]
        [Tooltip(
            "How fast the visual model yaws toward movement direction in Forward Run With Visual Yaw mode. Lower snaps faster; higher turns more softly.")]
        [SerializeField]
        private float visualMovementYawSmoothTime = 0.06f;

        [BoxGroup("Locomotion Animation")]
        [Tooltip(
            "When enabled, standing root rotation uses strafeLeft/Right clips instead of visually rotating an idle pose. Disable to keep idle while turning in place.")]
        [SerializeField]
        private bool useStrafeClipsForInPlaceTurn = true;

        [BoxGroup("Locomotion Animation")]
        [Tooltip(
            "Root yaw speed in degrees per second needed for full left/right turn animation weight. Lower reaches full shuffle animation sooner; higher keeps slower turns closer to idle.")]
        [SerializeField]
        private float inPlaceTurnFullSpeedDegreesPerSecond = 120f;

        [BoxGroup("Locomotion Animation")]
        [Tooltip(
            "Root yaw speed below which in-place turn animation is ignored. Lower reacts to tiny turns; higher prevents small camera/root jitter from shuffling.")]
        [SerializeField]
        private float inPlaceTurnMinSpeedDegreesPerSecond = 5f;

        [BoxGroup("Locomotion Animation")]
        [Tooltip(
            "Fade duration when the locomotion mixer starts. Lower snaps into locomotion faster; higher blends in more slowly.")]
        [SerializeField]
        private float locomotionFadeDuration = 0.10f;

        [BoxGroup("Upper Body Actions")]
        [Tooltip(
            "Upper-body layer fade-in duration. Lower makes cast/attack overlays appear faster; higher makes them blend in more slowly.")]
        [SerializeField]
        private float upperBodyFadeInDuration = 0.10f;

        [BoxGroup("Upper Body Actions")]
        [Tooltip(
            "Upper-body layer fade-out duration. Lower removes overlays faster; higher leaves them blending out longer.")]
        [SerializeField]
        private float upperBodyFadeOutDuration = 0.15f;

        [BoxGroup("Locomotion Animation")]
        [Tooltip(
            "Visual animation mixer smoothing time. Lower makes animation direction respond faster; higher makes blend changes softer. Physical movement remains constant velocity.")]
        [SerializeField]
        private float mixerParameterSmoothTime = 0.08f;

        [BoxGroup("Locomotion Animation")]
        [Tooltip(
            "Synchronize non-idle locomotion clips in the Animancer mixer. Disable if synchronization causes bad foot timing for these clips.")]
        [SerializeField]
        private bool synchronizeLocomotionChildren = true;

        [BoxGroup("Locomotion Animation")]
        [Tooltip(
            "Synchronize idle with locomotion clips. Usually off: enabling can make idle timing follow locomotion cycle behavior.")]
        [SerializeField]
        private bool synchronizeIdle;

        [BoxGroup("Upper Body Actions")]
        [Tooltip(
            "When enabled, actions started while stationary use the full-body mask. Disable to always keep actions upper-body only.")]
        [SerializeField]
        private bool fullBodyActionsWhenStationary = true;

        [BoxGroup("Upper Body Actions")]
        [Tooltip(
            "Input magnitude below which the character counts as stationary for action masking. Lower requires being almost perfectly still; higher treats small movement as stationary.")]
        [SerializeField]
        private float stationaryActionThreshold = 0.05f;

        /// <summary>True to move through the assigned <see cref="CharacterController"/> when one exists.</summary>
        public bool UseCharacterController
        {
            get { return useCharacterController; }
        }

        /// <summary>Skin width applied to the active <see cref="CharacterController"/> at runtime.</summary>
        public float CharacterControllerSkinWidth
        {
            get { return characterControllerSkinWidth; }
        }

        /// <summary>Minimum movement distance applied to the active <see cref="CharacterController"/> at runtime.</summary>
        public float CharacterControllerMinMoveDistance
        {
            get { return characterControllerMinMoveDistance; }
        }

        /// <summary>True to move the character from script instead of relying on root motion.</summary>
        public bool UseScriptDrivenMotion
        {
            get { return useScriptDrivenMotion; }
        }

        /// <summary>True to copy camera yaw to the character root while the right mouse button is held.</summary>
        public bool RightMouseHeldControlsFacing
        {
            get { return rightMouseHeldControlsFacing; }
        }

        /// <summary>True to make A and D always strafe instead of keyboard-turning when pressed alone.</summary>
        public bool AlwaysStrafeAD
        {
            get { return alwaysStrafeAD; }
        }

        /// <summary>True to clamp diagonal movement magnitude to one.</summary>
        public bool NormalizeDiagonalInput
        {
            get { return normalizeDiagonalInput; }
        }

        /// <summary>True to move forward when both left and right mouse buttons are held.</summary>
        public bool LeftAndRightMouseMoveForward
        {
            get { return leftAndRightMouseMoveForward; }
        }

        /// <summary>True to let head and upper-body twist absorb stationary RMB camera yaw before turning the root.</summary>
        public bool RmbDelayedRootFacing
        {
            get { return rmbDelayedRootFacing; }
        }

        /// <summary>Maximum yaw in degrees applied across neck and head while RMB look is active.</summary>
        public float RmbLookHeadMaxYawDegrees
        {
            get { return Mathf.Max(0f, rmbLookHeadMaxYawDegrees); }
        }

        /// <summary>Maximum yaw in degrees applied across upper spine and chest while RMB look is active.</summary>
        public float RmbLookUpperBodyMaxYawDegrees
        {
            get { return Mathf.Max(0f, rmbLookUpperBodyMaxYawDegrees); }
        }

        /// <summary>Camera/root yaw difference in degrees before the root slowly follows during stationary RMB look.</summary>
        public float RmbLookRootTurnStartYawDegrees
        {
            get { return Mathf.Max(0f, rmbLookRootTurnStartYawDegrees); }
        }

        /// <summary>Maximum root turn speed in degrees per second while RMB remains held past the twist threshold.</summary>
        public float RmbHeldRootTurnDegreesPerSecond
        {
            get { return Mathf.Max(0f, rmbHeldRootTurnDegreesPerSecond); }
        }

        /// <summary>Maximum root turn speed in degrees per second after RMB is released.</summary>
        public float RmbReleaseRootTurnDegreesPerSecond
        {
            get { return Mathf.Max(0f, rmbReleaseRootTurnDegreesPerSecond); }
        }

        /// <summary>Smoothing time for manual head and upper-body look twist.</summary>
        public float RmbLookTwistSmoothTime
        {
            get { return Mathf.Max(0f, rmbLookTwistSmoothTime); }
        }

        /// <summary>Time scale applied while the prototype slow-time toggle is active.</summary>
        public float SlowTimeScale
        {
            get { return Mathf.Clamp(slowTimeScale, 0.01f, 1.0f); }
        }

        /// <summary>Forward run speed in meters per second.</summary>
        public float RunSpeed
        {
            get { return runSpeed; }
        }

        /// <summary>Walk speed reserved for slower movement experiments.</summary>
        public float WalkSpeed
        {
            get { return walkSpeed; }
        }

        /// <summary>Multiplier applied when moving backward.</summary>
        public float BackwardSpeedMultiplier
        {
            get { return backwardSpeedMultiplier; }
        }

        /// <summary>Playback speed multiplier applied only to forward run animation clips.</summary>
        public float ForwardRunAnimationSpeedMultiplier
        {
            get { return forwardRunAnimationSpeedMultiplier > 0f ? forwardRunAnimationSpeedMultiplier : 1.0f; }
        }

        /// <summary>Playback speed multiplier applied only to the backpedal animation clip.</summary>
        public float BackwardAnimationSpeedMultiplier
        {
            get { return backwardAnimationSpeedMultiplier; }
        }

        /// <summary>Maximum visual yaw applied to the model while RMB side-strafing.</summary>
        public float RmbStrafeVisualYawDegrees
        {
            get { return rmbStrafeVisualYawDegrees > 0f ? rmbStrafeVisualYawDegrees : 90f; }
        }

        /// <summary>True to emit sparse runtime logs for locomotion decision branches.</summary>
        public bool LocomotionDecisionLogs
        {
            get { return locomotionDecisionLogs; }
        }

        /// <summary>True to emit verbose jump/landing diagnostics.</summary>
        public bool LandingDiagnosticLogs
        {
            get { return landingDiagnosticLogs; }
        }

        /// <summary>True to emit per-frame RMB camera/root rotation diagnostics.</summary>
        public bool RotationDiagnosticLogs
        {
            get { return rotationDiagnosticLogs; }
        }

        /// <summary>Multiplier applied when strafing without forward or backward input.</summary>
        public float StrafeSpeedMultiplier
        {
            get { return strafeSpeedMultiplier; }
        }

        /// <summary>Multiplier applied to the first airborne input after jumping from standstill.</summary>
        public float StandingJumpAirInputSpeedMultiplier
        {
            get { return standingJumpAirInputSpeedMultiplier; }
        }

        /// <summary>True to rotate standing-jump lower body toward the first committed mid-air movement direction.</summary>
        public bool StandingJumpAirFacing
        {
            get { return standingJumpAirFacing; }
        }

        /// <summary>Root turn speed used while airborne after a standing-jump movement direction is committed.</summary>
        public float StandingJumpAirRootTurnDegreesPerSecond
        {
            get { return Mathf.Max(0f, standingJumpAirRootTurnDegreesPerSecond); }
        }

        /// <summary>Root turn speed used to settle back to camera facing after landing from standing-jump air control.</summary>
        public float StandingJumpLandingRootCatchUpDegreesPerSecond
        {
            get { return Mathf.Max(0f, standingJumpLandingRootCatchUpDegreesPerSecond); }
        }

        /// <summary>Keyboard turn speed in degrees per second for A/D turning.</summary>
        public float KeyboardTurnDegreesPerSecond
        {
            get { return keyboardTurnDegreesPerSecond; }
        }

        /// <summary>Gravity applied when using script-driven CharacterController motion.</summary>
        public float Gravity
        {
            get { return gravity; }
        }

        /// <summary>Small downward velocity used to keep the CharacterController grounded.</summary>
        public float GroundedStickVelocity
        {
            get { return groundedStickVelocity; }
        }

        /// <summary>Desired jump apex height in meters.</summary>
        public float JumpHeight
        {
            get { return jumpHeight; }
        }

        /// <summary>Fade duration used when changing base-layer jump animations.</summary>
        public float JumpAnimationFadeDuration
        {
            get { return jumpAnimationFadeDuration; }
        }

        /// <summary>Desired duration used for the landing animation before locomotion resumes.</summary>
        public float JumpLandingDuration
        {
            get { return jumpLandingDuration; }
        }

        /// <summary>Desired duration used for landing animation while movement input is held.</summary>
        public float MovingJumpLandingDuration
        {
            get { return movingJumpLandingDuration; }
        }

        /// <summary>Normalized landing clip time used as the transition-out pose.</summary>
        public float JumpLandingExitNormalizedTime
        {
            get { return jumpLandingExitNormalizedTime; }
        }

        /// <summary>Normalized landing clip time used as the transition-out pose while movement continues through landing.</summary>
        public float MovingJumpLandingExitNormalizedTime
        {
            get { return movingJumpLandingExitNormalizedTime; }
        }

        /// <summary>Desired duration of the upper-body landing overlay while standing.</summary>
        public float JumpLandingUpperBodyDuration
        {
            get { return jumpLandingUpperBodyDuration; }
        }

        /// <summary>Desired duration of the upper-body landing overlay while movement continues.</summary>
        public float MovingJumpLandingUpperBodyDuration
        {
            get { return movingJumpLandingUpperBodyDuration; }
        }

        /// <summary>Normalized landing clip time used by the standing upper-body landing overlay.</summary>
        public float JumpLandingUpperBodyExitNormalizedTime
        {
            get { return jumpLandingUpperBodyExitNormalizedTime; }
        }

        /// <summary>Normalized landing clip time used by the moving upper-body landing overlay.</summary>
        public float MovingJumpLandingUpperBodyExitNormalizedTime
        {
            get { return movingJumpLandingUpperBodyExitNormalizedTime; }
        }

        /// <summary>Fade-out duration for the standing upper-body landing overlay.</summary>
        public float JumpLandingUpperBodyFadeOutDuration
        {
            get { return jumpLandingUpperBodyFadeOutDuration; }
        }

        /// <summary>Fade-out duration for the moving upper-body landing overlay.</summary>
        public float MovingJumpLandingUpperBodyFadeOutDuration
        {
            get { return movingJumpLandingUpperBodyFadeOutDuration; }
        }

        /// <summary>True when the upper-body landing clip should keep playing while its layer fades out.</summary>
        public bool LandingUpperBodyContinuesDuringFade
        {
            get { return landingUpperBodyContinuesDuringFade; }
        }

        /// <summary>Normalized landing clip time the upper-body overlay tries to reach by the end of fade-out.</summary>
        public float LandingUpperBodyFadeEndNormalizedTime
        {
            get { return landingUpperBodyFadeEndNormalizedTime; }
        }

        /// <summary>Additional visual yaw applied to the model child after animation evaluation.</summary>
        public float ModelYawOffsetDegrees
        {
            get { return modelYawOffsetDegrees; }
        }

        /// <summary>Strategy used to animate side movement when true strafe clips are unavailable.</summary>
        public WowStrafeAnimationMode StrafeAnimationMode
        {
            get { return strafeAnimationMode; }
        }

        /// <summary>Smoothing time used when yawing the visual model toward movement direction.</summary>
        public float VisualMovementYawSmoothTime
        {
            get { return visualMovementYawSmoothTime; }
        }

        /// <summary>True to use strafe clips when rotating in place.</summary>
        public bool UseStrafeClipsForInPlaceTurn
        {
            get { return useStrafeClipsForInPlaceTurn; }
        }

        /// <summary>Root yaw speed that maps to full in-place turn animation weight.</summary>
        public float InPlaceTurnFullSpeedDegreesPerSecond
        {
            get { return inPlaceTurnFullSpeedDegreesPerSecond; }
        }

        /// <summary>Minimum root yaw speed required before in-place turn animation is used.</summary>
        public float InPlaceTurnMinSpeedDegreesPerSecond
        {
            get { return inPlaceTurnMinSpeedDegreesPerSecond; }
        }

        /// <summary>Fade duration used when starting the locomotion mixer.</summary>
        public float LocomotionFadeDuration
        {
            get { return locomotionFadeDuration; }
        }

        /// <summary>Default fade-in duration for upper-body action layers.</summary>
        public float UpperBodyFadeInDuration
        {
            get { return upperBodyFadeInDuration; }
        }

        /// <summary>Default fade-out duration for upper-body action layers.</summary>
        public float UpperBodyFadeOutDuration
        {
            get { return upperBodyFadeOutDuration; }
        }

        /// <summary>Smoothing time used for visual mixer parameters. Physical movement remains constant velocity.</summary>
        public float MixerParameterSmoothTime
        {
            get { return mixerParameterSmoothTime; }
        }

        /// <summary>True to synchronize locomotion mixer children.</summary>
        public bool SynchronizeLocomotionChildren
        {
            get { return synchronizeLocomotionChildren; }
        }

        /// <summary>True to synchronize the idle mixer child with locomotion children.</summary>
        public bool SynchronizeIdle
        {
            get { return synchronizeIdle; }
        }

        /// <summary>True to use the full-body action mask when an action starts while stationary.</summary>
        public bool FullBodyActionsWhenStationary
        {
            get { return fullBodyActionsWhenStationary; }
        }

        /// <summary>Input magnitude below which the character is considered stationary for action masking.</summary>
        public float StationaryActionThreshold
        {
            get { return stationaryActionThreshold; }
        }
    }
}
