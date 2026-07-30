using UnityEngine;
using UnityEngine.InputSystem;

namespace WowLocomotionResearch
{
    /// <summary>
    /// Prototype-only keyboard harness for testing upper-body action masks.
    /// </summary>
    public sealed class WowUpperBodyActionController : MonoBehaviour
    {
        private const float NormalTimeScale = 1.0f;

        [Tooltip("Locomotion prototype that receives cast, attack, and aim commands.")]
        [SerializeField] private WowLikeAnimancerLocomotionPrototype locomotionPrototype;

        [Tooltip("Optional settings override for the slow-time multiplier. If empty, the controller reads the locomotion prototype's Movement Settings asset.")]
        [SerializeField] private WowMovementSettings movementSettings;

        [Tooltip("When enabled, holding/releases R toggles aim pose. When disabled, R acts as press-and-hold aim.")]
        [SerializeField] private bool aimIsToggle = true;

        [Tooltip("Keyboard key that toggles slow time on/off. Default F matches the requested WoW prototype debug binding.")]
        [SerializeField] private Key slowTimeToggleKey = Key.F;

        [Tooltip("Keyboard key that clears active upper-body action overlays. F is reserved for slow time, so this debug cancel uses X.")]
        [SerializeField] private Key stopUpperBodyKey = Key.X;

        private bool aimActive;
        private bool slowTimeActive;
        private bool hasDefaultFixedDeltaTime;
        private float defaultFixedDeltaTime;

        private WowMovementSettings ActiveMovementSettings
        {
            get { return movementSettings != null ? movementSettings : locomotionPrototype != null ? locomotionPrototype.MovementSettings : null; }
        }

        private void Reset()
        {
            locomotionPrototype = GetComponent<WowLikeAnimancerLocomotionPrototype>();
        }

        private void OnEnable()
        {
            CaptureDefaultFixedDeltaTime();
        }

        private void OnDisable()
        {
            if (!slowTimeActive)
                return;

            slowTimeActive = false;
            ApplyTimeScale(NormalTimeScale);
        }

        private void Update()
        {
            UpdateSlowTimeToggle();

            if (locomotionPrototype == null)
                return;

            if (WasPressed(Key.C))
                locomotionPrototype.PlayCast();

            if (WasPressed(Key.V))
                locomotionPrototype.PlayAttack();

            if (aimIsToggle)
            {
                if (WasPressed(Key.R))
                {
                    aimActive = !aimActive;
                    if (aimActive)
                        locomotionPrototype.PlayAimPose();
                    else
                        locomotionPrototype.StopUpperBody();
                }
            }
            else
            {
                if (WasPressed(Key.R))
                    locomotionPrototype.PlayAimPose();
                if (WasReleased(Key.R))
                    locomotionPrototype.StopUpperBody();
            }

            if (WasPressed(stopUpperBodyKey))
            {
                aimActive = false;
                locomotionPrototype.StopUpperBody();
            }
        }

        private void UpdateSlowTimeToggle()
        {
            if (WasPressed(slowTimeToggleKey))
            {
                slowTimeActive = !slowTimeActive;
                ApplyTimeScale(slowTimeActive ? GetSlowTimeScale() : NormalTimeScale);
                return;
            }

            if (slowTimeActive)
                ApplyTimeScale(GetSlowTimeScale());
        }

        private float GetSlowTimeScale()
        {
            var settings = ActiveMovementSettings;
            return settings != null ? settings.SlowTimeScale : 0.2f;
        }

        private void CaptureDefaultFixedDeltaTime()
        {
            if (hasDefaultFixedDeltaTime)
                return;

            defaultFixedDeltaTime = Time.fixedDeltaTime;
            hasDefaultFixedDeltaTime = true;
        }

        private void ApplyTimeScale(float timeScale)
        {
            CaptureDefaultFixedDeltaTime();

            var clampedTimeScale = Mathf.Clamp(timeScale, 0.01f, NormalTimeScale);
            Time.timeScale = clampedTimeScale;
            Time.fixedDeltaTime = defaultFixedDeltaTime * clampedTimeScale;
        }

        private static bool WasPressed(Key key)
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard[key].wasPressedThisFrame;
        }

        private static bool WasReleased(Key key)
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard[key].wasReleasedThisFrame;
        }
    }
}
