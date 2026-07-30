# WoW-Like Movement

> **Educational research prototype.** AI wrote implementation code under human direction; the behavior and values were personally measured, tested, debugged, and fine-tuned.

A Unity proof of concept for WoW-like character locomotion, animation, and camera behavior. It is not affiliated with Blizzard Entertainment and does not claim to reproduce the retail client's internal implementation.

## Included

- Movement, camera, setup, and diagnostic scripts.
- Measured tuning values stored in ScriptableObjects.
- Generic-rig and Animancer weighted-mask tooling.

## Requirements

- Unity `6000.3.10f1`.
- Animancer Pro 8.x or newer.
- Odin Inspector.
- Your own legally obtained Generic character and animation clips.

Paid plugins, proprietary game assets, extracted data, and local demo content are intentionally excluded. Install licensed dependencies before expecting the project to compile.

## Controls

`W/S` move · `A/D` turn or RMB-strafe · `Q/E` strafe · RMB rotates and aligns facing · LMB+RMB moves forward · `F1` diagnostics · `F2` VSync

## Code

- [`Scripts`](Assets/Research/WowLocomotion/Scripts)
- [`Measured settings`](Assets/Research/WowLocomotion/ScriptableObjects)
- [`Setup guide`](Assets/Research/WowLocomotion/README.md)

The measured behavior is the useful result. The implementation is prototype-quality and should be rewritten before production use.

World of Warcraft and Blizzard Entertainment are trademarks or registered trademarks of Blizzard Entertainment, Inc.
