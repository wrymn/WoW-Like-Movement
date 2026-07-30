using System.Collections.Generic;
using UnityEngine;

namespace WowLocomotionResearch
{
    /// <summary>
    /// Explicit Generic-rig bone mapping used by the research prototype without Humanoid bone assumptions.
    /// </summary>
    [CreateAssetMenu(
        fileName = "WowGenericBoneProfile",
        menuName = "Research/WoW Locomotion/Generic Bone Profile")]
    public sealed class WowGenericBoneProfile : ScriptableObject
    {
        /// <summary>Top transform of the imported skeleton hierarchy.</summary>
        [Tooltip("Top transform of the imported skeleton hierarchy. The setup tool assigns humanmale_hd_skeleton.")]
        public Transform skeletonRoot;

        /// <summary>Optional armature transform above the animated bones.</summary>
        [Tooltip("Optional armature transform above animated bones. Usually the same as Skeleton Root for this GLB.")]
        public Transform armatureRoot;

        /// <summary>Optional motion/root transform that may carry imported root motion curves.</summary>
        [Tooltip("Root/motion bone that may receive imported root-motion curves. Weighted masks keep this protected from upper-body overrides.")]
        public Transform motionRoot;

        /// <summary>Pelvis transform when the rig separates pelvis and hips.</summary>
        [Tooltip("Pelvis bone used as lower-body center when the rig separates pelvis from hips.")]
        public Transform pelvis;

        /// <summary>Hips transform used as the lower body center when present.</summary>
        [Tooltip("Hips/root lower-body center. Protected from upper-body-only masks.")]
        public Transform hips;

        /// <summary>Lower spine transform.</summary>
        [Tooltip("Lower spine bone. Influences how much upper-body actions blend into the torso.")]
        public Transform spineLower;

        /// <summary>Middle spine transform.</summary>
        [Tooltip("Middle spine bone. Influences torso blending for upper-body actions.")]
        public Transform spineMiddle;

        /// <summary>Upper spine transform.</summary>
        [Tooltip("Upper spine bone. Usually gets stronger upper-body action influence than lower spine.")]
        public Transform spineUpper;

        /// <summary>Chest transform.</summary>
        [Tooltip("Chest bone. Upper-body actions generally fully control this bone.")]
        public Transform chest;

        /// <summary>Neck transform.</summary>
        [Tooltip("Neck bone. Upper-body actions generally control this along with head/chest.")]
        public Transform neck;

        /// <summary>Head transform.</summary>
        [Tooltip("Head bone. Used by masks and as the camera pivot fallback target name bone_Head.")]
        public Transform head;

        /// <summary>Left clavicle transform.</summary>
        [Tooltip("Left clavicle/shoulder-root bone for upper-body masks.")]
        public Transform leftClavicle;

        /// <summary>Left shoulder transform.</summary>
        [Tooltip("Left shoulder bone for upper-body masks.")]
        public Transform leftShoulder;

        /// <summary>Left upper arm transform.</summary>
        [Tooltip("Left upper arm bone for upper-body action masks.")]
        public Transform leftUpperArm;

        /// <summary>Left forearm transform.</summary>
        [Tooltip("Left forearm bone for upper-body action masks.")]
        public Transform leftForearm;

        /// <summary>Left hand transform.</summary>
        [Tooltip("Left hand bone for upper-body action masks.")]
        public Transform leftHand;

        /// <summary>Left finger transforms.</summary>
        [Tooltip("Left finger bones included in upper-body action masks.")]
        public List<Transform> leftFingers = new List<Transform>();

        /// <summary>Right clavicle transform.</summary>
        [Tooltip("Right clavicle/shoulder-root bone for upper-body masks.")]
        public Transform rightClavicle;

        /// <summary>Right shoulder transform.</summary>
        [Tooltip("Right shoulder bone for upper-body masks.")]
        public Transform rightShoulder;

        /// <summary>Right upper arm transform.</summary>
        [Tooltip("Right upper arm bone for upper-body action masks.")]
        public Transform rightUpperArm;

        /// <summary>Right forearm transform.</summary>
        [Tooltip("Right forearm bone for upper-body action masks.")]
        public Transform rightForearm;

        /// <summary>Right hand transform.</summary>
        [Tooltip("Right hand bone for upper-body action masks.")]
        public Transform rightHand;

        /// <summary>Right finger transforms.</summary>
        [Tooltip("Right finger bones included in upper-body action masks.")]
        public List<Transform> rightFingers = new List<Transform>();

        /// <summary>Left thigh transform.</summary>
        [Tooltip("Left thigh bone protected for locomotion/lower-body movement.")]
        public Transform leftThigh;

        /// <summary>Left calf or shin transform.</summary>
        [Tooltip("Left calf/shin bone protected for locomotion/lower-body movement.")]
        public Transform leftCalf;

        /// <summary>Left foot transform.</summary>
        [Tooltip("Left foot bone protected for locomotion/lower-body movement.")]
        public Transform leftFoot;

        /// <summary>Left toe transform.</summary>
        [Tooltip("Left toe bone protected for locomotion/lower-body movement.")]
        public Transform leftToe;

        /// <summary>Extra left-side lower body bones that should remain locomotion-driven.</summary>
        [Tooltip("Extra left leg/twist bones protected from upper-body overrides.")]
        public List<Transform> leftExtraLegBones = new List<Transform>();

        /// <summary>Right thigh transform.</summary>
        [Tooltip("Right thigh bone protected for locomotion/lower-body movement.")]
        public Transform rightThigh;

        /// <summary>Right calf or shin transform.</summary>
        [Tooltip("Right calf/shin bone protected for locomotion/lower-body movement.")]
        public Transform rightCalf;

        /// <summary>Right foot transform.</summary>
        [Tooltip("Right foot bone protected for locomotion/lower-body movement.")]
        public Transform rightFoot;

        /// <summary>Right toe transform.</summary>
        [Tooltip("Right toe bone protected for locomotion/lower-body movement.")]
        public Transform rightToe;

        /// <summary>Extra right-side lower body bones that should remain locomotion-driven.</summary>
        [Tooltip("Extra right leg/twist bones protected from upper-body overrides.")]
        public List<Transform> rightExtraLegBones = new List<Transform>();

        /// <summary>Additional upper-body bones affected by upper-body overrides unless ignored.</summary>
        [Tooltip("Additional bones treated as upper body. Add twist/accessory bones here when upper-body actions should affect them.")]
        public List<Transform> extraUpperBodyBones = new List<Transform>();

        /// <summary>Additional lower-body bones protected from upper-body overrides.</summary>
        [Tooltip("Additional bones treated as lower body. Add bones here when upper-body actions should not affect them.")]
        public List<Transform> extraLowerBodyBones = new List<Transform>();

        /// <summary>Bones intentionally excluded from weighted mask influence.</summary>
        [Tooltip("Bones excluded from generated weighted masks. Use for helper/attachment bones that should not be layer-blended.")]
        public List<Transform> ignoredBones = new List<Transform>();

        /// <summary>
        /// Validates required assignments, hierarchy ownership, duplicate ownership, and simple side-swap heuristics.
        /// </summary>
        /// <returns>A structured validation report.</returns>
        public WowBoneProfileValidationReport Validate()
        {
            var report = new WowBoneProfileValidationReport();

            Require(report, skeletonRoot, nameof(skeletonRoot));
            if (pelvis == null && hips == null)
                report.Errors.Add("Either pelvis or hips must be assigned.");

            if (spineLower == null && spineMiddle == null && spineUpper == null && chest == null)
                report.Errors.Add("At least one spine or chest bone must be assigned.");

            Require(report, leftUpperArm, nameof(leftUpperArm));
            Require(report, rightUpperArm, nameof(rightUpperArm));
            Require(report, leftForearm, nameof(leftForearm));
            Require(report, rightForearm, nameof(rightForearm));
            Require(report, leftHand, nameof(leftHand));
            Require(report, rightHand, nameof(rightHand));
            Require(report, leftThigh, nameof(leftThigh));
            Require(report, rightThigh, nameof(rightThigh));
            Require(report, leftCalf, nameof(leftCalf));
            Require(report, rightCalf, nameof(rightCalf));
            Require(report, leftFoot, nameof(leftFoot));
            Require(report, rightFoot, nameof(rightFoot));

            WarnForNonChildren(report);
            WarnForUpperLowerOverlap(report);
            WarnForLikelySwappedSides(report);

            return report;
        }

        /// <summary>
        /// Gets bones that may receive upper-body action weights.
        /// </summary>
        /// <returns>A unique list of assigned upper-body bones.</returns>
        public List<Transform> GetUpperBodyBones()
        {
            var bones = new List<Transform>();
            AddUnique(bones, spineLower);
            AddUnique(bones, spineMiddle);
            AddUnique(bones, spineUpper);
            AddUnique(bones, chest);
            AddUnique(bones, neck);
            AddUnique(bones, head);
            AddUnique(bones, leftClavicle);
            AddUnique(bones, leftShoulder);
            AddUnique(bones, leftUpperArm);
            AddUnique(bones, leftForearm);
            AddUnique(bones, leftHand);
            AddUnique(bones, leftFingers);
            AddUnique(bones, rightClavicle);
            AddUnique(bones, rightShoulder);
            AddUnique(bones, rightUpperArm);
            AddUnique(bones, rightForearm);
            AddUnique(bones, rightHand);
            AddUnique(bones, rightFingers);
            AddUnique(bones, extraUpperBodyBones);
            RemoveAll(bones, ignoredBones);
            return bones;
        }

        /// <summary>
        /// Gets bones that must remain protected from moving upper-body action overrides.
        /// </summary>
        /// <returns>A unique list of assigned lower-body and root-motion bones.</returns>
        public List<Transform> GetLowerBodyBones()
        {
            var bones = new List<Transform>();
            AddUnique(bones, skeletonRoot);
            AddUnique(bones, armatureRoot);
            AddUnique(bones, motionRoot);
            AddUnique(bones, pelvis);
            AddUnique(bones, hips);
            AddUnique(bones, leftThigh);
            AddUnique(bones, leftCalf);
            AddUnique(bones, leftFoot);
            AddUnique(bones, leftToe);
            AddUnique(bones, leftExtraLegBones);
            AddUnique(bones, rightThigh);
            AddUnique(bones, rightCalf);
            AddUnique(bones, rightFoot);
            AddUnique(bones, rightToe);
            AddUnique(bones, rightExtraLegBones);
            AddUnique(bones, extraLowerBodyBones);
            RemoveAll(bones, ignoredBones);
            return bones;
        }

        /// <summary>
        /// Gets root-like bones that must never be driven by upper-body root motion.
        /// </summary>
        /// <returns>A unique list of root and motion bones.</returns>
        public List<Transform> GetRootMotionBones()
        {
            var bones = new List<Transform>();
            AddUnique(bones, skeletonRoot);
            AddUnique(bones, armatureRoot);
            AddUnique(bones, motionRoot);
            return bones;
        }

        /// <summary>
        /// Gets every assigned transform referenced by this profile.
        /// </summary>
        /// <returns>A unique list of all assigned bones.</returns>
        public List<Transform> GetAllAssignedBones()
        {
            var bones = new List<Transform>();
            AddUnique(bones, skeletonRoot);
            AddUnique(bones, armatureRoot);
            AddUnique(bones, motionRoot);
            AddUnique(bones, pelvis);
            AddUnique(bones, hips);
            AddUnique(bones, spineLower);
            AddUnique(bones, spineMiddle);
            AddUnique(bones, spineUpper);
            AddUnique(bones, chest);
            AddUnique(bones, neck);
            AddUnique(bones, head);
            AddUnique(bones, leftClavicle);
            AddUnique(bones, leftShoulder);
            AddUnique(bones, leftUpperArm);
            AddUnique(bones, leftForearm);
            AddUnique(bones, leftHand);
            AddUnique(bones, leftFingers);
            AddUnique(bones, rightClavicle);
            AddUnique(bones, rightShoulder);
            AddUnique(bones, rightUpperArm);
            AddUnique(bones, rightForearm);
            AddUnique(bones, rightHand);
            AddUnique(bones, rightFingers);
            AddUnique(bones, leftThigh);
            AddUnique(bones, leftCalf);
            AddUnique(bones, leftFoot);
            AddUnique(bones, leftToe);
            AddUnique(bones, leftExtraLegBones);
            AddUnique(bones, rightThigh);
            AddUnique(bones, rightCalf);
            AddUnique(bones, rightFoot);
            AddUnique(bones, rightToe);
            AddUnique(bones, rightExtraLegBones);
            AddUnique(bones, extraUpperBodyBones);
            AddUnique(bones, extraLowerBodyBones);
            AddUnique(bones, ignoredBones);
            return bones;
        }

        private void WarnForNonChildren(WowBoneProfileValidationReport report)
        {
            if (skeletonRoot == null)
                return;

            var bones = GetAllAssignedBones();
            for (int i = 0; i < bones.Count; i++)
            {
                var bone = bones[i];
                if (bone != null && bone != skeletonRoot && !bone.IsChildOf(skeletonRoot))
                    report.Warnings.Add($"{bone.name} is assigned but is not a child of skeletonRoot.");
            }
        }

        private void WarnForUpperLowerOverlap(WowBoneProfileValidationReport report)
        {
            var lower = new HashSet<Transform>(GetLowerBodyBones());
            var upper = GetUpperBodyBones();
            for (int i = 0; i < upper.Count; i++)
            {
                if (upper[i] != null && lower.Contains(upper[i]))
                    report.Warnings.Add($"{upper[i].name} appears in both upper-body and lower-body bone sets.");
            }
        }

        private void WarnForLikelySwappedSides(WowBoneProfileValidationReport report)
        {
            WarnIfNamesSuggestSwapped(report, leftUpperArm, rightUpperArm, "upper arms");
            WarnIfNamesSuggestSwapped(report, leftForearm, rightForearm, "forearms");
            WarnIfNamesSuggestSwapped(report, leftHand, rightHand, "hands");
            WarnIfNamesSuggestSwapped(report, leftThigh, rightThigh, "thighs");
            WarnIfNamesSuggestSwapped(report, leftFoot, rightFoot, "feet");
        }

        private static void WarnIfNamesSuggestSwapped(
            WowBoneProfileValidationReport report,
            Transform left,
            Transform right,
            string label)
        {
            if (left == null || right == null)
                return;

            if (NameLooksRight(left.name) || NameLooksLeft(right.name))
                report.Warnings.Add($"Left/right {label} may be swapped based on bone names.");
        }

        private static bool NameLooksLeft(string name)
        {
            return name.EndsWith("L") || name.Contains("_L") || name.Contains("Left");
        }

        private static bool NameLooksRight(string name)
        {
            return name.EndsWith("R") || name.Contains("_R") || name.Contains("Right");
        }

        private static void Require(WowBoneProfileValidationReport report, Transform transform, string fieldName)
        {
            if (transform == null)
                report.Errors.Add($"Required bone '{fieldName}' is missing.");
        }

        private static void AddUnique(List<Transform> bones, Transform bone)
        {
            if (bone != null && !bones.Contains(bone))
                bones.Add(bone);
        }

        private static void AddUnique(List<Transform> bones, List<Transform> extraBones)
        {
            if (extraBones == null)
                return;

            for (int i = 0; i < extraBones.Count; i++)
                AddUnique(bones, extraBones[i]);
        }

        private static void RemoveAll(List<Transform> bones, List<Transform> removals)
        {
            if (removals == null)
                return;

            for (int i = 0; i < removals.Count; i++)
                bones.Remove(removals[i]);
        }
    }

    /// <summary>
    /// Contains errors and warnings generated by <see cref="WowGenericBoneProfile"/>.
    /// </summary>
    public sealed class WowBoneProfileValidationReport
    {
        /// <summary>Errors that make the bone profile incomplete for the prototype.</summary>
        public readonly List<string> Errors = new List<string>();

        /// <summary>Warnings that indicate suspicious but not necessarily invalid bone assignments.</summary>
        public readonly List<string> Warnings = new List<string>();

        /// <summary>True when no validation errors were produced.</summary>
        public bool IsValid
        {
            get { return Errors.Count == 0; }
        }
    }
}
