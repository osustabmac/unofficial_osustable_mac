# OsuStable.Native

Native runtime implementation connecting core game logic to hardware-accelerated Apple Silicon audio and graphics APIs.

## Architecture

- **Audio Subsystem (`/Audio`)**:
  Direct 64-bit BASS audio bridge for ARM64 macOS. Delivers sub-millisecond clock synchronization and hardware sample playback with zero emulation translation latency.
- **Platform & Windowing (`/Platform`)**:
  SDL3-based window management, full-screen exclusive mode, raw mouse cursor lock, and keyboard state capturing.
- **Scenes & Render Loop (`/Scenes`)**:
  Game loop orchestration, menu states, and song select scenes.
