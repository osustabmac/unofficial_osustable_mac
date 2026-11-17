using System;
using System.ComponentModel;
using System.Collections;
using System.Drawing;
using System.Threading;

namespace System.Windows.Forms;

public interface IWin32Window { IntPtr Handle { get; } }
public interface IButtonControl { DialogResult DialogResult { get; set; } void NotifyDefault(bool value); void PerformClick(); }
public interface IDataObject { }

public delegate void MethodInvoker();
public delegate void ThreadExceptionEventHandler(object sender, ThreadExceptionEventArgs e);
public delegate void FormClosingEventHandler(object sender, FormClosingEventArgs e);
public delegate void PaintEventHandler(object sender, PaintEventArgs e);
public delegate void KeyEventHandler(object sender, KeyEventArgs e);
public delegate void KeyPressEventHandler(object sender, KeyPressEventArgs e);
public delegate void MouseEventHandler(object sender, MouseEventArgs e);
public delegate void DragEventHandler(object sender, DragEventArgs e);
public delegate void ControlEventHandler(object sender, ControlEventArgs e);
public delegate void LayoutEventHandler(object sender, LayoutEventArgs e);
public delegate void PreviewKeyDownEventHandler(object sender, PreviewKeyDownEventArgs e);
public delegate void LinkLabelLinkClickedEventHandler(object sender, LinkLabelLinkClickedEventArgs e);
public delegate void WebBrowserProgressChangedEventHandler(object sender, WebBrowserProgressChangedEventArgs e);

public class ThreadExceptionEventArgs : EventArgs { public ThreadExceptionEventArgs(Exception exception) { Exception = exception; } public Exception Exception { get; } }
public class FormClosingEventArgs : CancelEventArgs { public FormClosingEventArgs(CloseReason reason, bool cancel) : base(cancel) { CloseReason = reason; } public CloseReason CloseReason { get; } }
public class PaintEventArgs : EventArgs { public PaintEventArgs(Graphics graphics, Rectangle clipRect) { Graphics = graphics; ClipRectangle = clipRect; } public Graphics Graphics { get; } public Rectangle ClipRectangle { get; } }
public class KeyEventArgs : EventArgs { public KeyEventArgs(Keys keyData) { KeyData = keyData; } public Keys KeyData { get; } public Keys KeyCode => KeyData & Keys.KeyCode; public bool Alt => (KeyData & Keys.Alt) != 0; public bool Control => (KeyData & Keys.Control) != 0; public bool Shift => (KeyData & Keys.Shift) != 0; public bool SuppressKeyPress { get; set; } public bool Handled { get; set; } }
public class KeyPressEventArgs : EventArgs { public KeyPressEventArgs(char keyChar) { KeyChar = keyChar; } public char KeyChar { get; set; } public bool Handled { get; set; } }
public class MouseEventArgs : EventArgs { public MouseEventArgs(MouseButtons button, int clicks, int x, int y, int delta) { Button = button; Clicks = clicks; X = x; Y = y; Delta = delta; } public MouseButtons Button { get; } public int Clicks { get; } public int X { get; } public int Y { get; } public int Delta { get; } public Point Location => new(X, Y); }
public class DragEventArgs : EventArgs { public IDataObject Data { get; set; } public DragDropEffects Effect { get; set; } }
public class ControlEventArgs : EventArgs { public ControlEventArgs(Control control) { Control = control; } public Control Control { get; } }
public class LayoutEventArgs : EventArgs { }
public class PreviewKeyDownEventArgs : KeyEventArgs { public PreviewKeyDownEventArgs(Keys keyData) : base(keyData) { } public bool IsInputKey { get; set; } }
public class LinkLabelLinkClickedEventArgs : EventArgs { }
public class WebBrowserProgressChangedEventArgs : EventArgs { }
public class ColumnWidthChangedEventArgs : EventArgs { }

public class CreateParams { public int ClassStyle { get; set; } }
public struct Padding { public Padding(int all) { Left = Top = Right = Bottom = all; } public int Left, Top, Right, Bottom; }
public struct Message { public int Msg; public IntPtr WParam; public IntPtr LParam; public IntPtr Result; }

public class Control : Component, IWin32Window
{
    public class ControlCollection : BaseCollection
    {
        private readonly ArrayList items = new();
        protected override ArrayList List => items;
        public virtual void Add(Control value) => items.Add(value);
        public virtual void AddRange(Control[] controls) => items.AddRange(controls);
        public virtual void Remove(Control value) => items.Remove(value);
        public virtual void Clear() => items.Clear();
        public Control this[int index] => (Control)items[index];
    }
    public virtual IntPtr Handle => IntPtr.Zero;
    public virtual bool IsHandleCreated => true;
    public virtual bool IsDisposed { get; protected set; }
    public virtual string Text { get; set; } = string.Empty;
    public virtual string Name { get; set; } = string.Empty;
    public virtual bool Enabled { get; set; } = true;
    public virtual bool Visible { get; set; } = true;
    public virtual bool Focused { get; set; } = true;
    public virtual bool AllowDrop { get; set; }
    public virtual bool CausesValidation { get; set; }
    public virtual bool TabStop { get; set; }
    public virtual int TabIndex { get; set; }
    public virtual DockStyle Dock { get; set; }
    public virtual bool DoubleBuffered { get; set; }
    public virtual Color BackColor { get; set; }
    public virtual Color ForeColor { get; set; }
    public virtual Font Font { get; set; }
    public virtual Size ClientSize { get; set; }
    public virtual Rectangle ClientRectangle => new(Point.Empty, ClientSize);
    public virtual Point Location { get; set; }
    public virtual Size Size { get; set; }
    public virtual int Width { get => Size.Width; set => Size = new Size(value, Size.Height); }
    public virtual int Height { get => Size.Height; set => Size = new Size(Size.Width, value); }
    public virtual object Tag { get; set; }
    public virtual Control Parent { get; set; }
    public virtual Cursor Cursor { get; set; } = Cursors.Arrow;
    public virtual ControlCollection Controls { get; } = new();
    protected virtual CreateParams CreateParams => new();
    protected bool DesignMode => false;
    public event EventHandler Click, DoubleClick, Enter, Leave, GotFocus, LostFocus, Resize, SizeChanged, LocationChanged, ClientSizeChanged, TextChanged, VisibleChanged;
    public event KeyEventHandler KeyDown, KeyUp;
    public event KeyPressEventHandler KeyPress;
    public event PreviewKeyDownEventHandler PreviewKeyDown;
    public event MouseEventHandler MouseDown, MouseUp, MouseMove, MouseWheel;
    public event PaintEventHandler Paint;
    public event DragEventHandler DragEnter, DragDrop;
    public event ControlEventHandler ControlAdded, ControlRemoved;
    public virtual void CreateControl() { }
    public virtual void RecreateHandle() { }
    public virtual void SuspendLayout() { }
    public virtual void ResumeLayout() { }
    public virtual void ResumeLayout(bool performLayout) { }
    public virtual void PerformLayout() { }
    public virtual void Refresh() { }
    public virtual void Invalidate() { }
    public virtual void Update() { }
    public virtual void SendToBack() { }
    public virtual void BringToFront() { }
    public virtual bool Focus() => true;
    public virtual Point PointToScreen(Point point) => point;
    public virtual Point PointToClient(Point point) => point;
    public virtual Rectangle RectangleToScreen(Rectangle rectangle) => rectangle;
    public virtual Rectangle RectangleToClient(Rectangle rectangle) => rectangle;
    public virtual object BeginInvoke(Delegate method) { method.DynamicInvoke(); return null; }
    public virtual object Invoke(Delegate method) { return method.DynamicInvoke(); }
    protected void SetStyle(ControlStyles flag, bool value) { }
    protected virtual void OnHandleCreated(EventArgs e) { }
    protected virtual void OnHandleDestroyed(EventArgs e) { }
    protected virtual void OnPaint(PaintEventArgs e) => Paint?.Invoke(this, e);
    protected virtual void OnResize(EventArgs e) => Resize?.Invoke(this, e);
    protected virtual void OnParentChanged(EventArgs e) { }
    protected virtual void OnGotFocus(EventArgs e) => GotFocus?.Invoke(this, e);
    public void RaiseMouseWheel(int delta, int x, int y) =>
        MouseWheel?.Invoke(this, new MouseEventArgs(MouseButtons.None, 0, x, y, delta));
    protected virtual void WndProc(ref Message m) { }
    protected override void Dispose(bool disposing) { IsDisposed = true; base.Dispose(disposing); }
}

public class ScrollableControl : Control { public bool AutoScroll { get; set; } }
public class ContainerControl : ScrollableControl { public SizeF AutoScaleDimensions { get; set; } public AutoScaleMode AutoScaleMode { get; set; } public Control ActiveControl { get; set; } }
public class Form : ContainerControl
{
    private Size formClientSize;
    private FormWindowState windowState;
    private FormBorderStyle formBorderStyle;
    private bool topMost;
    public event CancelEventHandler Closing;
    public event FormClosingEventHandler FormClosing;
    public event EventHandler ResizeEnd;
    public event EventHandler Activated;
    public event EventHandler Deactivate;
    public Icon Icon { get; set; }
    public double Opacity { get; set; } = 1;
    public new Size ClientSize { get => formClientSize; set { formClientSize = value; MacFormsBridge.SetClientSize?.Invoke(value); } }
    public FormWindowState WindowState { get => windowState; set { windowState = value; MacFormsBridge.SetWindowState?.Invoke(value); } }
    public FormBorderStyle FormBorderStyle { get => formBorderStyle; set { formBorderStyle = value; MacFormsBridge.SetFormBorderStyle?.Invoke(value); } }
    public FormStartPosition StartPosition { get; set; }
    public DialogResult DialogResult { get; set; }
    public bool TopMost { get => topMost; set { topMost = value; MacFormsBridge.SetTopMost?.Invoke(value); } }
    public bool MaximizeBox { get; set; } = true;
    public bool MinimizeBox { get; set; } = true;
    public bool ShowInTaskbar { get; set; } = true;
    public IButtonControl AcceptButton { get; set; }
    public IButtonControl CancelButton { get; set; }
    public virtual DialogResult ShowDialog() => DialogResult;
    public virtual DialogResult ShowDialog(IWin32Window owner) => DialogResult;
    public virtual void Show() { Visible = true; }
    public virtual void Close() { var closing = new CancelEventArgs(); Closing?.Invoke(this, closing); var formClosing = new FormClosingEventArgs(CloseReason.UserClosing, closing.Cancel); FormClosing?.Invoke(this, formClosing); Visible = false; }
    protected void CenterToScreen() { }
    protected virtual void OnShown(EventArgs e) { }
    protected virtual void OnFormClosing(FormClosingEventArgs e) => FormClosing?.Invoke(this, e);
}

public static class MacFormsBridge
{
    public static Action<Size> SetClientSize;
    public static Action<FormWindowState> SetWindowState;
    public static Action<FormBorderStyle> SetFormBorderStyle;
    public static Action<bool> SetTopMost;
    public static Action<Rectangle> SetCursorClip;
    public static Action<bool> SetCursorVisible;
    public static Func<Rectangle> GetScreenBounds;
}

public class Screen
{
    private static readonly Screen primary = new();
    public static Screen PrimaryScreen => primary;
    public static Screen FromHandle(IntPtr handle) => primary;
    public Rectangle Bounds => MacFormsBridge.GetScreenBounds?.Invoke() ?? new Rectangle(0, 0, 1280, 720);
    public Rectangle WorkingArea => Bounds;
    public string DeviceName => "macOS Display";
}

public class Cursor : IDisposable
{
    private static Rectangle clip = Rectangle.Empty;
    public Cursor(IntPtr handle) { Handle = handle; }
    public IntPtr Handle { get; }
    public static Rectangle Clip { get => clip; set { clip = value; MacFormsBridge.SetCursorClip?.Invoke(value); } }
    public static Point Position { get; set; } = Point.Empty;
    public static void Show() => MacFormsBridge.SetCursorVisible?.Invoke(true);
    public static void Hide() => MacFormsBridge.SetCursorVisible?.Invoke(false);
    public void Dispose() { }
}

public class NativeWindow
{
    public IntPtr Handle { get; private set; }
    public void AssignHandle(IntPtr handle) => Handle = handle;
    public void ReleaseHandle() => Handle = IntPtr.Zero;
    protected virtual void WndProc(ref Message m) { }
}

public static class Cursors
{
    public static Cursor Arrow { get; } = new(IntPtr.Zero);
    public static Cursor Hand { get; } = new(IntPtr.Zero);
    public static Cursor SizeWE { get; } = new(IntPtr.Zero);
    public static Cursor Cross { get; } = new(IntPtr.Zero);
}

public static class Application
{
    public static event EventHandler Idle;
    public static event ThreadExceptionEventHandler ThreadException;
    public static string ExecutablePath => Environment.ProcessPath ?? string.Empty;
    public static void EnableVisualStyles() { }
    public static void SetCompatibleTextRenderingDefault(bool defaultValue) { }
    public static void Run(Form form) { while (form.Visible) { Idle?.Invoke(null, EventArgs.Empty); Thread.Yield(); } }
    public static void Exit() { }
    public static void DoEvents() => Idle?.Invoke(null, EventArgs.Empty);
}

public static class MessageBox
{
    public static DialogResult Show(string text) => DialogResult.OK;
    public static DialogResult Show(string text, string caption) => DialogResult.OK;
    public static DialogResult Show(string text, string caption, MessageBoxButtons buttons) => DefaultResult(buttons);
    public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon) => DefaultResult(buttons);
    public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton) => DefaultResult(buttons, defaultButton);
    public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton) => DefaultResult(buttons, defaultButton);

    private static DialogResult DefaultResult(MessageBoxButtons buttons, MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button1)
    {
        return buttons switch
        {
            MessageBoxButtons.OKCancel => defaultButton == MessageBoxDefaultButton.Button2 ? DialogResult.Cancel : DialogResult.OK,
            MessageBoxButtons.AbortRetryIgnore => defaultButton switch { MessageBoxDefaultButton.Button2 => DialogResult.Retry, MessageBoxDefaultButton.Button3 => DialogResult.Ignore, _ => DialogResult.Abort },
            MessageBoxButtons.YesNoCancel => defaultButton switch { MessageBoxDefaultButton.Button2 => DialogResult.No, MessageBoxDefaultButton.Button3 => DialogResult.Cancel, _ => DialogResult.Yes },
            MessageBoxButtons.YesNo => defaultButton == MessageBoxDefaultButton.Button2 ? DialogResult.No : DialogResult.Yes,
            MessageBoxButtons.RetryCancel => defaultButton == MessageBoxDefaultButton.Button2 ? DialogResult.Cancel : DialogResult.Retry,
            _ => DialogResult.OK
        };
    }
}

public abstract class BaseCollection { protected abstract ArrayList List { get; } public int Count => List.Count; }

[Flags] public enum Keys { None = 0, KeyCode = 0xFFFF, Shift = 0x10000, Control = 0x20000, Alt = 0x40000, LeftShift = 160, RightShift = 161 }
[Flags] public enum MouseButtons { None = 0, Left = 0x100000, Right = 0x200000, Middle = 0x400000 }
[Flags] public enum ControlStyles { UserPaint = 2, AllPaintingInWmPaint = 8192, Opaque = 4 }
public enum DialogResult { None, OK, Cancel, Abort, Retry, Ignore, Yes, No }
public enum MessageBoxButtons { OK = 0, OKCancel = 1, AbortRetryIgnore = 2, YesNoCancel = 3, YesNo = 4, RetryCancel = 5 }
public enum MessageBoxIcon { None = 0, Hand = 16, Stop = 16, Error = 16, Question = 32, Exclamation = 48, Warning = 48, Asterisk = 64, Information = 64 }
public enum MessageBoxDefaultButton { Button1 = 0, Button2 = 256, Button3 = 512 }
public enum CloseReason { None, UserClosing }
public enum FormWindowState { Normal, Minimized, Maximized }
public enum FormBorderStyle { None, FixedSingle, Fixed3D, FixedDialog, Sizable, FixedToolWindow, SizableToolWindow }
public enum FormStartPosition { Manual, CenterScreen, WindowsDefaultLocation, WindowsDefaultBounds, CenterParent }
public enum AutoScaleMode { None, Font, Dpi, Inherit }
public enum DragDropEffects { None = 0, Copy = 1, Move = 2, Link = 4, All = 7 }

// Remaining legacy dialog/control types are only instantiated by updater and
// editor utility paths. They intentionally share the inert Control contract.
public class ButtonBase : Control { }
public class Button : ButtonBase, IButtonControl { public DialogResult DialogResult { get; set; } public void NotifyDefault(bool value) { } public void PerformClick() { } }
public class Label : Control { }
public class Panel : ScrollableControl { }
public class GroupBox : Control { }
public class CheckBox : ButtonBase { public bool Checked { get; set; } public CheckState CheckState { get; set; } public bool ThreeState { get; set; } }
public class RadioButton : ButtonBase { public bool Checked { get; set; } }
public class TextBoxBase : Control { public string SelectedText { get; set; } public int SelectionStart { get; set; } public int SelectionLength { get; set; } public bool ReadOnly { get; set; } public bool HideSelection { get; set; } public bool WordWrap { get; set; } public BorderStyle BorderStyle { get; set; } public void Clear() { Text = string.Empty; SelectionStart = 0; SelectionLength = 0; } public void Select(int start, int length) { SelectionStart = start; SelectionLength = length; } public void SelectAll() { SelectionStart = 0; SelectionLength = Text.Length; } }
public class TextBox : TextBoxBase { public bool Multiline { get; set; } public ScrollBars ScrollBars { get; set; } }
public class UserControl : ContainerControl { }
public class PictureBox : Control { public Image Image { get; set; } }
public class ProgressBar : Control { public int Minimum { get; set; } public int Maximum { get; set; } public int Value { get; set; } }
public class Timer : Component { public int Interval { get; set; } public bool Enabled { get; set; } public event EventHandler Tick; public void Start() => Enabled = true; public void Stop() => Enabled = false; }

public enum CheckState { Unchecked, Checked, Indeterminate }
public enum ScrollBars { None, Horizontal, Vertical, Both }
public enum BorderStyle { None, FixedSingle, Fixed3D }
public enum DockStyle { None, Top, Bottom, Left, Right, Fill }
