# WoW-Like Locomotion Research Prototype

> **EDUCATION ONLY:** This is human-led research implemented with AI assistance. AI wrote implementation code under human direction, while the behavior and values were personally measured, tested, debugged, corrected, and fine-tuned through repeated iterations. It is not autonomous or one-shot AI output, though the implementation remains prototype-quality.

This folder contains a research-only prototype for observable WoW-like third-person locomotion using Generic-rigged imported character assets, Animancer Pro v8.x or newer, and Animancer `WeightedMaskLayers`.

Do not ship, redistribute, commit, package, addressable-build, or otherwise publish World of Warcraft assets. Keep any imported research assets outside production folders and do not claim this is Blizzard's internal implementation.

## Requirements

- Animancer Pro v8.x or newer.
- Generic rig import.
- No Unity Humanoid retargeting.
- No Animator Controllers.
- No Humanoid AvatarMask body-part toggles.
- Script-driven movement. `Animator.applyRootMotion` is enabled only so Animancer `WeightedMaskLayers` can evaluate root-motion streams; `WowLikeAnimancerLocomotionPrototype.OnAnimatorMove` discards the delta root motion.

## Recommended Hierarchy

```text
CharacterRoot
  CharacterController optional
  Animator
  AnimancerComponent
  WeightedMaskLayers
  WowLikeAnimancerLocomotionPrototype
  WowUpperBodyActionController optional
  WowLocomotionDebugOverlay optional
  ModelRoot
    SkinnedMeshRenderer
    Armature/Skeleton
```

## Required Components

- `CharacterRoot` GameObject.
- `Animator` on the character root.
- `AnimancerComponent` on the character root.
- `WeightedMaskLayers` on the character root.
- `WowLikeAnimancerLocomotionPrototype` on the character root or controller object.
- Optional `CharacterController`.

## Import Settings

- Set Animation Type to `Generic`.
- Loop locomotion clips.
- Keep gameplay movement script-driven; root motion is swallowed by `OnAnimatorMove`.
- Ensure all clips target the same Generic skeleton.
- Keep scale consistent across model and clips.
- Keep assets outside production folders.

## Setup

1. Assign the `AnimancerComponent`.
2. Assign the `WeightedMaskLayers`.
3. Create a `WowLocomotionAnimSet` and assign idle, run forward, run backward, strafe left, and strafe right clips.
4. Select the skeleton root and run `Tools/Research/WoW Locomotion/Create Generic Bone Profile From Selected Skeleton`.
5. Review and fix every suggested bone assignment manually.
6. Create a `WowWeightedMaskProfile`.
7. Run `Tools/Research/WoW Locomotion/Weighted Mask Setup Report`.
8. Assign the bone profile, weighted mask profile, and `WeightedMaskLayers`.
9. Press `Apply Weights To WeightedMaskLayers`.
10. Assign `cameraTransform`.
11. Enter Play Mode.
12. Test W, S, A, D, Q, E, RMB, and LMB+RMB.

## Input Contract

- W moves forward.
- S moves backward.
- A/D turn left/right when RMB is not held and `alwaysStrafeAD` is false.
- Q/E strafe left/right.
- RMB makes A/D strafe and makes the logical root face camera yaw.
- LMB+RMB behaves as forward movement when enabled.

## Acceptance Tests

1. W moves forward and mixer target is `(0, 1)`.
2. S moves backward, mixer target is `(0, -1)`, and the backward speed multiplier applies.
3. D without RMB and `alwaysStrafeAD` false turns right without strafe movement from D alone.
4. A without RMB and `alwaysStrafeAD` false turns left without strafe movement from A alone.
5. D with RMB strafes right and mixer target is `(1, 0)`.
6. A with RMB strafes left and mixer target is `(-1, 0)`.
7. W + D + RMB normalizes to approximately `(0.707, 0.707)`, root yaw equals camera yaw, movement is forward-right relative to root/camera, and mixer parameter approaches `(0.707, 0.707)`.
8. Q + W moves forward-left and mixer target is approximately `(-0.707, 0.707)`.
9. LMB + RMB moves forward and mixer target is `(0, 1)`.
10. Moving + cast keeps legs in locomotion, casts on the upper body, keeps hips/legs/feet/toes driven by locomotion, fades in the upper layer, and activates `UpperBodyActionWhileMoving`.
11. Stationary + attack uses `FullBodyActionWhileStationary` when enabled, while root and motion root stay at weight `0`.
12. Stop action fades the upper layer to `0`, returns the mask to `NoUpperBodyOverride`, and leaves locomotion uninterrupted.
13. Debug overlay shows W+D+RMB values, active mask group, and current layer weights.

## Notes

The locomotion mixer uses an Animancer 2D Directional mixer. Required children are idle plus four cardinal locomotion clips. Optional diagonal clips are included when assigned.

Mixer parameters are smoothed visually with deterministic exponential smoothing. Physical movement uses unsmoothed normalized input by default.

Upper-body actions fade the Animancer layer weight and the weighted mask group. The prototype does not configure additive upper-body blending.
