# MacPlatformBridge

Native macOS platform integration layer bridging classic desktop game lifecycles with modern Apple Silicon (ARM64) macOS technologies.

## Key Subsystems

- **`NativeWindowHost.cs`**:
  High-performance windowing host built on top of SDL3 / Cocoa. Handles native macOS window creation, ProMotion refresh rates (up to 120Hz/240Hz), Retina display pixel backing scales, and low-latency mouse/keyboard event polling.
- **`MacApplicationActivation.cs`**:
  Configures macOS `NSApplicationActivationPolicyRegular`, sets up the dock icon, menu bar, and brings the native window to the foreground when launched by Finder or LaunchServices.
- **`MacPangoBootstrap.cs` & `MacFontRegistry.cs`**:
  Integrates Pango, Cairo, and Fontconfig with an embedded `fonts.conf`. Ensures all dynamic fonts, track labels, and text widgets rasterize crisply on Retina displays without requiring system-level X11 or external font installations.
- **`WindowsPlatformServices.cs`**:
  Implements safe, sandboxed emulations for Windows Registry queries, platform paths (mapping `%APPDATA%` and `%LOCALAPPDATA%` to `~/Library/Application Support`), and shell open handlers.
