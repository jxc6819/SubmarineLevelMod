# IEYTD2 Submarine Level Mod

A custom **Submarine level mod** for *I Expect You To Die 2*, built using **MelonLoader**, **Unity 2019.4**, and **IL2CPP-safe scripting**.

This project merges assets from existing game scenes, adds new interactive systems, and layers in custom puzzle logic, effects, and restart handling.  
The focus is on hands-on VR interaction, mechanical puzzles, and environmental storytelling.

---

## Features

- Merges assets from multiple base-game scenes into a custom submarine environment
- Adds new interactive puzzles (wires, levers, gauges, vents, reactor systems)
- Implements custom visual effects without relying on Unity’s built-in ParticleSystem where possible
- Supports full level reset without reloading the game
- Makes normally non-grabbable objects fully interactable in VR by hooking into Schell's custom interaction system
- Centralized logic for puzzle state, failures, and progression

---

## Core Architecture

- **MyMod.cs** is the main mod entry point and bootstrapper
- **SubmarineLevelLogic.cs** acts as the brain of the level
- **ObjectBank.cs** stores references to important scene objects
- Systems are split into small, focused scripts instead of monoliths
- Effects are handled by lightweight custom drivers
- Everything is designed to be **IL2CPP-safe** and MelonLoader-friendly

---

## Script Overview

### Core / Bootstrap

- **MyMod.cs**  
  Main mod entry point that loads the asset bundle, merges scenes, initializes systems, and handles restarts.

- **SubmarineLevelLogic.cs**  
  Central game logic controller that tracks puzzle state, sabotage events, thresholds, and failure conditions.

- **SubmarineRunManager.cs**  
  Manages the lifecycle of a single submarine run, including resets.

- **ObjectBank.cs**  
  Central reference hub for important scene objects.

- **AttachUnityScript.cs**  
  Dynamically attaches scripts to GameObjects by name at runtime.

---

### Asset & Scene Management

- **GatherGameAssets.cs**  
  Loads and clones required objects from other game scenes into the submarine level.

- **SubBundle2Manager.cs**  
  Manages loading of custom asset bundles.

- **PhoenixProbe.cs**  
  Debug and inspection tool for Phoenix prefabs and components.

- **PhoenixButtonHook.cs**  
  Hooks Phoenix button interactions into custom logic.

---

### Interaction & Grabbables

- **MakeGrabbable.cs**  
  Converts custom GameObjects into objects that can be used with Schell's interaction system.

- **WheelScript.cs**  
  Handles rotation-based interaction for the main wheel.

- **LeverScript.cs**  
  Controls lever interaction and signals level logic.

- **HatchScript.cs**  
  Manages the escape pod logic at the end of the level.

- **NeedleScript.cs**  
  Controls gauge needle movement and fires threshold events.

---

### Wire Puzzle System

- **WireManager.cs**  
  Central controller for all wire puzzle logic.

- **WireTubeRenderer.cs**  
  Procedurally renders smooth tube meshes to visually represent wires.

- **LooseWire.cs**  
  Represents a cut or disconnected wire.

- **WireCutterListener.cs**  
  Detects wire cutter usage.

- **SolderListener.cs**  
  Handles soldering interactions.

---

### Reactor, Vent, and Fan Systems

- **ReactorDriver.cs**  
  Controls reactor flame visuals and behavior.

- **ReactorVentTriggerDetector.cs**  
  Detects sponge insertion and triggers reactor sabotage logic.

- **fanTriggerDetector.cs**  
  Detects when an object enters the fan trigger zone.

- **SpinFan.cs**  
  Spins the fan and allows it to slow or stop.

- **SteamDriver.cs**  
  Creates steam effects from vents or overheating systems.

---

### Water & Coolant Systems

- **WaterDriver.cs**  
  Controls animated water planes or water behavior.

- **WaterSprayDriver.cs**  
  Creates directional water spray effects.

- **CoolantSprayDriver.cs**  
  Handles coolant spray visuals.

- **WaterDiag.cs**  
  Displays water-related diagnostics.

---

### Visual Effects

- **ExplosionDriver.cs**  
  One-shot explosion effect without Unity ParticleSystem.

- **SmokeDriver.cs**  
  Smoke effects tied to damage or failures.

- **SparkDriver.cs**  
  Electrical spark effects.

- **BloodMistDriver.cs**  
  Blood mist effect on character death.

- **GlassDriver.cs**  
  Handles glass breaking when hit.

- **DamageOverlayDriver.cs**  
  Screen damage overlay when the player is hurt.

---

### Items & Misc

- **GunScript.cs**  
  Shooting logic and hit detection.

- **FlashlightScript.cs**  
  Flashlight toggle and VR input handling.

- **CigarScript.cs**  
  Cigar interaction behavior.

- **ChipOrientationHelper.cs**  
  Debug script - helped to align puzzle chips correctly.

- **StopperScript.cs**  
  Controls stopper mechanics in gauge puzzle.

- **SubLoopAmbience.cs**  
  Plays looping submarine ambient audio.

- **AudioUtil.cs**  
  Utility for consistent spatial audio playback.

---

## Notes

There was minimal AI used in the creation of this project's code. AI was mostly used to assist with the Driver's (as using Unity's particle system and other fx methods are extremely tedious to use without the Unity engine) and other IL2CPP-modding specific challenges
