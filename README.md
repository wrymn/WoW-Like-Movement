# WoW-Like Movement — Human-Directed, AI-Assisted Research

> # EDUCATION-ONLY RESEARCH PROTOTYPE
>
> **This is a human-led research project built with AI assistance. AI was used to write implementation code, but the project was not autonomously or randomly generated. A human spent substantial time directing the work, measuring behavior, validating and fine-tuning values, testing results, finding bugs, correcting the implementation, and iterating until the behavior matched the observations.**

This is a proof of concept for observable, WoW-like character animation, locomotion, and third-person camera behavior in Unity. The implementation is prototype-grade, but the behavior and tuning values were produced through precise measurement, hands-on testing, and repeated human-guided refinement against observations from a retail game client.

This repository is not affiliated with, endorsed by, or derived from source code belonging to Blizzard Entertainment. It does not claim to reproduce the retail client's internal implementation.

## What Is Included

- Human-directed, AI-assisted character locomotion and camera scripts.
- ScriptableObject definitions and measured tuning values.
- Editor helpers for configuring a compatible local test character.
- Runtime diagnostics used while comparing observed behavior.

## What Is Deliberately Excluded

No proprietary game models, textures, animations, extracted data, paid Unity assets, or local demo scenes are distributed here. An education-only label would not make redistribution of those files legal.

The local prototype used:

- Unity `6000.3.10f1`.
- Animancer Pro 8.x or newer.
- Odin Inspector.
- A Generic-rigged character and animation clips supplied separately by the user.

Install licensed dependencies from their official sources and provide your own legal test assets. The repository may not compile until those dependencies are present.

## Controls

- `W` / `S`: forward and backward.
- `A` / `D`: turn, or strafe while holding RMB.
- `Q` / `E`: strafe.
- RMB: rotate camera and align character facing.
- LMB + RMB: move forward when enabled.
- `F1`: toggle the runtime diagnostic overlay.
- `F2`: toggle VSync.

## Project Layout

- [`Assets/Research/WowLocomotion/Scripts`](Assets/Research/WowLocomotion/Scripts): runtime and editor code.
- [`Assets/Research/WowLocomotion/ScriptableObjects`](Assets/Research/WowLocomotion/ScriptableObjects): publishable tuning assets.
- [`Assets/Research/WowLocomotion/README.md`](Assets/Research/WowLocomotion/README.md): setup details and acceptance checks.

## Quality Warning

Treat this as carefully tested research data wrapped in prototype code. The values were measured, checked, and adjusted through repeated hands-on testing rather than guessed by AI. The implementation can still contain duplication, awkward naming, poor boundaries, and AI-assisted design mistakes, so production use should begin with a deliberate rewrite of the parts you actually need.

World of Warcraft and Blizzard Entertainment are trademarks or registered trademarks of Blizzard Entertainment, Inc. All other trademarks belong to their respective owners.
