using System.Collections.Generic;
using UnityEngine;

namespace WowLocomotionResearch
{
    /// <summary>
    /// Stores the Generic-rig animation clips used by the WoW-like locomotion research prototype.
    /// </summary>
    [CreateAssetMenu(
        fileName = "WowLocomotionAnimSet",
        menuName = "Research/WoW Locomotion/Locomotion Anim Set")]
    public sealed class WowLocomotionAnimSet : ScriptableObject
    {
        /// <summary>Stationary idle clip used at mixer parameter (0, 0).</summary>
        [Tooltip("Stationary base clip used when movement input is zero.")]
        public AnimationClip idle;

        /// <summary>Required forward run clip used at mixer parameter (0, 1).</summary>
        [Tooltip("Forward run clip used for W/RMB forward movement.")]
        public AnimationClip runForward;

        /// <summary>Required backward run clip used at mixer parameter (0, -1).</summary>
        [Tooltip("Backward movement clip used for S/backpedal.")]
        public AnimationClip runBackward;

        /// <summary>Required left strafe clip used at mixer parameter (-1, 0).</summary>
        [Tooltip("Left strafe clip used for Q or A while strafing.")]
        public AnimationClip strafeLeft;

        /// <summary>Required right strafe clip used at mixer parameter (1, 0).</summary>
        [Tooltip("Right strafe clip used for E or D while strafing.")]
        public AnimationClip strafeRight;

        /// <summary>Optional forward walk clip reserved for slower locomotion experiments.</summary>
        [Tooltip("Optional forward walk clip. Reserved for slower movement experiments; current base movement uses run speed.")]
        public AnimationClip walkForward;

        /// <summary>Optional backward walk clip reserved for slower locomotion experiments.</summary>
        [Tooltip("Optional backward walk clip. Reserved for slower movement experiments.")]
        public AnimationClip walkBackward;

        /// <summary>Optional left walk-strafe clip reserved for slower locomotion experiments.</summary>
        [Tooltip("Optional left walk-strafe clip. Reserved for slower movement experiments.")]
        public AnimationClip walkStrafeLeft;

        /// <summary>Optional right walk-strafe clip reserved for slower locomotion experiments.</summary>
        [Tooltip("Optional right walk-strafe clip. Reserved for slower movement experiments.")]
        public AnimationClip walkStrafeRight;

        /// <summary>Optional jump start clip.</summary>
        [Tooltip("Optional jump-start clip for future jump transitions.")]
        public AnimationClip jumpStart;

        /// <summary>Optional jump loop clip.</summary>
        [Tooltip("Optional airborne/fall loop clip for future jump transitions.")]
        public AnimationClip jumpLoop;

        /// <summary>Optional jump land clip.</summary>
        [Tooltip("Optional landing clip for future jump transitions.")]
        public AnimationClip jumpLand;

        /// <summary>Optional keyboard-turn-left clip.</summary>
        [Tooltip("Optional turn-left-in-place clip for keyboard turning experiments.")]
        public AnimationClip turnLeft;

        /// <summary>Optional keyboard-turn-right clip.</summary>
        [Tooltip("Optional turn-right-in-place clip for keyboard turning experiments.")]
        public AnimationClip turnRight;

        /// <summary>Optional forward-left run clip used at mixer parameter (-1, 1).</summary>
        [Tooltip("Optional diagonal forward-left run clip. If empty, the 2D mixer blends forward and left strafe clips.")]
        public AnimationClip runForwardLeft;

        /// <summary>Optional forward-right run clip used at mixer parameter (1, 1).</summary>
        [Tooltip("Optional diagonal forward-right run clip. If empty, the 2D mixer blends forward and right strafe clips.")]
        public AnimationClip runForwardRight;

        /// <summary>Optional backward-left run clip used at mixer parameter (-1, -1).</summary>
        [Tooltip("Optional diagonal backward-left run clip. If empty, the 2D mixer blends backward and left strafe clips.")]
        public AnimationClip runBackwardLeft;

        /// <summary>Optional backward-right run clip used at mixer parameter (1, -1).</summary>
        [Tooltip("Optional diagonal backward-right run clip. If empty, the 2D mixer blends backward and right strafe clips.")]
        public AnimationClip runBackwardRight;

        /// <summary>Optional upper-body idle clip.</summary>
        [Tooltip("Optional upper-body idle/ready clip used for weapon or spell-ready overlay experiments.")]
        public AnimationClip upperBodyIdle;

        /// <summary>Optional upper-body cast action clip.</summary>
        [Tooltip("Upper-body spell cast clip played by the cast action.")]
        public AnimationClip cast;

        /// <summary>Optional upper-body attack action clip.</summary>
        [Tooltip("Upper-body attack clip played by the attack action.")]
        public AnimationClip attack;

        /// <summary>Optional upper-body aim pose clip.</summary>
        [Tooltip("Upper-body aim pose clip played by the aim action.")]
        public AnimationClip aimPose;

        /// <summary>Optional upper-body ready pose clip.</summary>
        [Tooltip("Upper-body ready pose clip used as the neutral ready/combat pose.")]
        public AnimationClip readyPose;

        /// <summary>
        /// Validates assigned clips without searching the project or blocking play mode for optional clips.
        /// </summary>
        /// <returns>A report containing warnings for missing or suspicious assignments.</returns>
        public WowAnimSetValidationReport Validate()
        {
            var report = new WowAnimSetValidationReport();

            ValidateRequiredClip(report, idle, nameof(idle));
            ValidateRequiredClip(report, runForward, nameof(runForward));
            ValidateRequiredClip(report, runBackward, nameof(runBackward));
            ValidateRequiredClip(report, strafeLeft, nameof(strafeLeft));
            ValidateRequiredClip(report, strafeRight, nameof(strafeRight));

            ValidateLocomotionClip(report, walkForward, nameof(walkForward), false);
            ValidateLocomotionClip(report, walkBackward, nameof(walkBackward), false);
            ValidateLocomotionClip(report, walkStrafeLeft, nameof(walkStrafeLeft), false);
            ValidateLocomotionClip(report, walkStrafeRight, nameof(walkStrafeRight), false);
            ValidateLocomotionClip(report, jumpStart, nameof(jumpStart), false);
            ValidateLocomotionClip(report, jumpLoop, nameof(jumpLoop), false);
            ValidateLocomotionClip(report, jumpLand, nameof(jumpLand), false);
            ValidateLocomotionClip(report, turnLeft, nameof(turnLeft), false);
            ValidateLocomotionClip(report, turnRight, nameof(turnRight), false);
            ValidateLocomotionClip(report, runForwardLeft, nameof(runForwardLeft), false);
            ValidateLocomotionClip(report, runForwardRight, nameof(runForwardRight), false);
            ValidateLocomotionClip(report, runBackwardLeft, nameof(runBackwardLeft), false);
            ValidateLocomotionClip(report, runBackwardRight, nameof(runBackwardRight), false);

            ValidateUpperBodyClip(report, upperBodyIdle, nameof(upperBodyIdle), false);
            ValidateUpperBodyClip(report, cast, nameof(cast), true);
            ValidateUpperBodyClip(report, attack, nameof(attack), true);
            ValidateUpperBodyClip(report, aimPose, nameof(aimPose), false);
            ValidateUpperBodyClip(report, readyPose, nameof(readyPose), false);

            return report;
        }

        private static void ValidateRequiredClip(WowAnimSetValidationReport report, AnimationClip clip, string fieldName)
        {
            if (clip == null)
            {
                report.Warnings.Add($"Required clip '{fieldName}' is missing.");
                return;
            }

            ValidateLocomotionClip(report, clip, fieldName, true);
        }

        private static void ValidateLocomotionClip(
            WowAnimSetValidationReport report,
            AnimationClip clip,
            string fieldName,
            bool requireLooping)
        {
            if (clip == null)
                return;

            WarnIfHumanoid(report, clip, fieldName);

            if (requireLooping && !clip.isLooping)
                report.Warnings.Add($"Locomotion clip '{fieldName}' should be imported as looping.");
        }

        private static void ValidateUpperBodyClip(
            WowAnimSetValidationReport report,
            AnimationClip clip,
            string fieldName,
            bool looksLikeOneShot)
        {
            if (clip == null)
                return;

            WarnIfHumanoid(report, clip, fieldName);

            if (looksLikeOneShot && clip.isLooping)
                report.Warnings.Add($"Upper-body action clip '{fieldName}' looks one-shot but is looping.");
        }

        private static void WarnIfHumanoid(WowAnimSetValidationReport report, AnimationClip clip, string fieldName)
        {
            if (clip.humanMotion)
                report.Warnings.Add($"Clip '{fieldName}' appears to contain Humanoid motion. Import it as Generic.");
        }

        private void OnValidate()
        {
            var report = Validate();
            for (int i = 0; i < report.Warnings.Count; i++)
                Debug.LogWarning(report.Warnings[i], this);
        }
    }

    /// <summary>
    /// Contains validation warnings generated by <see cref="WowLocomotionAnimSet"/>.
    /// </summary>
    public sealed class WowAnimSetValidationReport
    {
        /// <summary>Warnings found while validating the animation set.</summary>
        public readonly List<string> Warnings = new List<string>();

        /// <summary>True when no warnings were produced.</summary>
        public bool IsValid
        {
            get { return Warnings.Count == 0; }
        }
    }
}
