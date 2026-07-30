# WoW-Like Locomotion Setup

Research-only Generic-rig locomotion using Animancer Pro `WeightedMaskLayers`. Do not publish imported proprietary assets.

## Requirements

- Animancer Pro 8.x or newer.
- A Generic rig with looping locomotion clips on one skeleton and consistent scale.
- No Humanoid retargeting, Animator Controller, or Humanoid AvatarMask.

Movement is script-driven. Root-motion streams are enabled for weighted masks, but `OnAnimatorMove` discards their movement.

## Character

```text
CharacterRoot
  Animator
  AnimancerComponent
  WeightedMaskLayers
  WowLikeAnimancerLocomotionPrototype
  CharacterController optional
  WowUpperBodyActionController optional
  WowLocomotionDebugOverlay optional
  ModelRoot
    SkinnedMeshRenderer
    Armature/Skeleton
```

## Setup

1. Assign `AnimancerComponent` and `WeightedMaskLayers`.
2. Create a `WowLocomotionAnimSet` with idle and four cardinal locomotion clips.
3. Select the skeleton root and run `Tools/Research/WoW Locomotion/Create Generic Bone Profile From Selected Skeleton`.
4. Review the generated bone assignments.
5. Create a `WowWeightedMaskProfile`.
6. Run `Tools/Research/WoW Locomotion/Weighted Mask Setup Report`, assign the profiles, and apply the weights.
7. Assign `cameraTransform`, enter Play Mode, and test the controls.

## Controls

`W/S` move · `A/D` turn or RMB-strafe · `Q/E` strafe · RMB aligns facing to camera yaw · LMB+RMB moves forward

## Verify

- Cardinal inputs reach mixer targets `(0, ±1)` and `(±1, 0)`.
- Diagonal input normalizes to approximately `0.707` per axis.
- Moving actions affect the upper body without interrupting locomotion.
- Stationary actions can use the full-body mask.
- Stopping an action restores the normal mask and layer weight.
- The debug overlay reports input, active mask, and layer weights.

The mixer uses idle plus four cardinal clips; diagonal clips are optional. Mixer smoothing is visual, while physical movement uses normalized unsmoothed input.
