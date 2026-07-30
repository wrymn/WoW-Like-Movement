using System.Collections.Generic;
using UnityEngine;

namespace WowLocomotionResearch
{
    /// <summary>
    /// Exposes a <see cref="WowLocomotionAnimSet"/> to Unity editor systems that query <see cref="IAnimationClipSource"/>.
    /// </summary>
    public sealed class WowAnimSetClipSource : MonoBehaviour, IAnimationClipSource
    {
        [Tooltip("Animation set exposed to Unity editor clip discovery. Assign the canonical HumanMaleHdAnimSet so the Animation window and Animancer can find these clips.")]
        [SerializeField] private WowLocomotionAnimSet animSet;

        /// <summary>Animation set whose clips should be visible to Unity clip-source queries.</summary>
        public WowLocomotionAnimSet AnimSet
        {
            get { return animSet; }
            set { animSet = value; }
        }

        /// <summary>
        /// Adds every assigned clip in the configured animation set to Unity's clip-source list.
        /// </summary>
        /// <param name="results">Destination list supplied by Unity.</param>
        public void GetAnimationClips(List<AnimationClip> results)
        {
            if (animSet == null)
                return;

            Add(results, animSet.idle);
            Add(results, animSet.runForward);
            Add(results, animSet.runBackward);
            Add(results, animSet.strafeLeft);
            Add(results, animSet.strafeRight);
            Add(results, animSet.walkForward);
            Add(results, animSet.walkBackward);
            Add(results, animSet.walkStrafeLeft);
            Add(results, animSet.walkStrafeRight);
            Add(results, animSet.jumpStart);
            Add(results, animSet.jumpLoop);
            Add(results, animSet.jumpLand);
            Add(results, animSet.turnLeft);
            Add(results, animSet.turnRight);
            Add(results, animSet.runForwardLeft);
            Add(results, animSet.runForwardRight);
            Add(results, animSet.runBackwardLeft);
            Add(results, animSet.runBackwardRight);
            Add(results, animSet.upperBodyIdle);
            Add(results, animSet.cast);
            Add(results, animSet.attack);
            Add(results, animSet.aimPose);
            Add(results, animSet.readyPose);
        }

        private static void Add(List<AnimationClip> results, AnimationClip clip)
        {
            if (clip != null && !results.Contains(clip))
                results.Add(clip);
        }
    }
}
