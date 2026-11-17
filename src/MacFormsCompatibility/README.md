# MacFormsCompatibility

A clean-room, lightweight implementation of the essential `System.Windows.Forms` API surface designed specifically for modern **.NET 10 on macOS (Apple Silicon ARM64)**.

## Why this exists
Standard `System.Windows.Forms` is historically tied to Windows (`user32.dll`, `gdi32.dll`, Win32 message pump) and has never been officially supported on modern cross-platform .NET (Core / 5 / 6 / 7 / 8 / 9 / 10) on macOS or Linux.

Legacy rhythm game engines and tools heavily relied on Windows Forms for:
- Message loop and lifecycle management (`Application.Run`, `Application.DoEvents`)
- Native dialogs, clipboard, and screen queries (`Screen.PrimaryScreen`, `Clipboard`, `Cursor`)
- Input events (`Keys`, `KeyEventArgs`, `MouseEventArgs`, `MouseButtons`)
- Component hierarchy and control styling (`Control`, `Form`, `Timer`)

`MacFormsCompatibility` provides a drop-in contract replacement that compiles cleanly under modern .NET 10 ARM64 without pulling in legacy Win32 dependencies or requiring Wine/Rosetta emulation layers.
