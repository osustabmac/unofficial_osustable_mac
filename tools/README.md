# Build & Packaging Tools

Automation scripts to bundle native arm64 dynamic libraries and assemble the final macOS `.app` and `.dmg`.

## Scripts

- **`fetch-macos-native-deps.sh`**:
  Downloads and unpacks verified Apple Silicon (arm64) dylibs:
  - `libSDL3.dylib`
  - `libbass.dylib`
  - `libgdiplus.dylib`
- **`fetch-macos-drawing-runtime.sh`**:
  Fetches Pango, Cairo, HarfBuzz, and Fontconfig dependencies for typography rendering.
- **`macos-app-launcher.sh`**:
  Native shell trampoline placed inside `Contents/MacOS/` that sets up dynamic library paths (`DYLD_FALLBACK_LIBRARY_PATH`), font configuration, and launches the .NET 10 ARM64 host.
