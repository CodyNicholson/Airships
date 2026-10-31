# Platform-Specific Engineering & Production Blueprints

When executing Phase 4 of `design-game`, select the blueprint matching `target_platform` and include its architectural specifications in the final development plan.

---

## Blueprint A: `web` (Web Browser Game App)

### 1. Recommended Tech Stack & Engine Matrix
- **2D Arcade / Puzzle / Roguelite / Strategy**: **Phaser 3** or **PixiJS v8** + TypeScript + Vite (fastest load time, tiny bundle size, seamless DOM/Canvas UI hybrid).
- **3D Action / Spatial Exploration**: **Three.js** (with `@react-three/fiber` + `rapier3d` physics) or **PlayCanvas** (WebGL2/WebGPU optimized).
- **Full-Engine Cross-Compilation**: **Godot 4 (Web Export)** (Note: requires SharedArrayBuffer COOP/COEP headers or single-threaded export mode for broad itch.io/portal compatibility).

### 2. Web-Specific Three Cs & UX Constraints
- **Controls**: Support seamless hot-switching between **Keyboard + Mouse (WASD/Arrows + Pointer Lock)**, **Gamepad API (`navigator.getGamepads()`)**, and **Mobile Web Touch fallback**. Prevent default browser scrolling on Spacebar/Arrow keys (`e.preventDefault()`).
- **Audio Unlock Ritual**: Browser autoplay policies block audio until the first user gesture. Build the **Title Screen / "Click to Enter the Magic Circle"** button to simultaneously resume the `AudioContext` and preload initial audio sprites.
- **Asset Delivery & Cold-Start Budget**:
  - Target **< 3.5 MB initial gzipped payload** so the player enters the **Core Toy** within 3 seconds of clicking the URL.
  - Stream Level 2+ assets asynchronously in the background while the player is inside the Level 1 **Training Cage**.
- **Persistence & Telemetry**:
  - Save game state and settings to `IndexedDB` (with `localStorage` fallback) after every Beat Chart milestone.
  - Provide JSON export/import save strings for zero-backend portability.

---

## Blueprint B: `mobile` (Mobile Game App — iOS & Android)

### 1. Recommended Tech Stack & Engine Matrix
- **2D / 3D Commercial Cross-Platform**: **Unity (URP)** or **Godot 4 (Mobile Renderer)** — industry standard for C#/GDScript rapid iteration, touch input, and native store pipelines.
- **Lightweight 2D / UI-Heavy Strategy / Card Games**: **Defold** or **Flutter + Flame Engine** (minimal binary size, ultra-low battery drain).

### 2. Mobile-Specific Three Cs & Ergonomic Constraints
- **Orientation Decision**:
  - *Portrait (One-Handed Thumb Play)*: Ideal for card battlers (*Slay the Spire* style), puzzle, idle, or swipe-runner games. Keep 90% of interactive targets in the **bottom two-thirds of the screen**.
  - *Landscape (Two-Thumb Action)*: Required for twin-stick action, platformers, or tactics. Use floating/dynamic virtual joysticks on the left 40% of the screen and 2–3 radial action buttons on the right 40%.
- **Zero-Friction Touch Controls**:
  - Never port a 10-button PC HUD onto glass. Consolidate actions using **context-sensitive tap/swipe/hold gestures** or auto-fire upon target lock so the player focuses on positioning and timing.
  - Respect hardware **Safe Areas (`Screen.safeArea`)** to avoid dynamic islands, camera notches, and home-bar swipe zones.
- **Session Pacing & Interruptibility**:
  - Structure the **Master Beat Chart** into **90-second to 3-minute micro-beats**.
  - Implement instant background state serialization (`OnApplicationPause`) so a phone call or train stop never costs the player progress.
- **Discernibility via Haptics & Battery Budgets**:
  - Pair visual hit-flashes with crisp hardware haptics (`HapticFeedback.LightImpact` for resource pickup, `HeavyImpact` for boss telegraph/damage).
  - Target 60 FPS with adaptive thermal scaling (drop shadow resolution before dropping frame rate, as frame drops ruin the **Three Cs**).

---

## Blueprint C: `desktop` (Desktop Game App — Windows, macOS, Linux)

### 1. Recommended Tech Stack & Engine Matrix
- **2D / Stylized 3D / Indie-to-AA**: **Godot 4 (Forward+ Renderer)** or **Unity**.
- **High-Fidelity 3D / Action / Cinematic**: **Unreal Engine 5** (Blueprints for rapid grey-box prototyping + C++ for core systems).
- **Systems-Heavy Simulation / ECS**: **Rust + Bevy** or **C# + MonoGame/FNA**.

### 2. Desktop-Specific Three Cs & Architecture Constraints
- **Dual Input Parity (M&K + Controller)**:
  - Implement instantaneous UI glyph swapping the millisecond a player touches a mouse vs. an Xbox/DualSense controller.
  - Include full action rebinding (`InputAction` mapping tables), mouse sensitivity sliders, raw mouse input toggle, and accessibility remapping (hold-vs-toggle options).
- **Camera & Display Pipeline**:
  - Decouple all physics and movement math from frame rate (`delta_time` fixed-timestep physics at 60/120Hz with interpolated rendering up to 144Hz/240Hz+).
  - Support 16:9, 16:10 (Steam Deck 1280x800), and 21:9 Ultrawide aspect ratios without clipping critical HUD elements.
- **Data-Driven Systems & Moddability**:
  - Store **Constitutive Rules** (enemy HP, weapon frame data, economy sources/drains, loot tables) in external JSON/CSV/ScriptableObject catalogs so designers can iterate during **Playtesting** without recompiling code.
- **Platform SDK Integration**:
  - Architect an abstract `PlatformServices` interface early for Steamworks / Epic / GOG achievements, Rich Presence (showing current Beat Chart stage), and Steam Cloud Save synchronization.

