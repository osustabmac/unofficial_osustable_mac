using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Xna.Framework;

// Native stable keeps these methods in one Win32 helper. The macOS port keeps
// the contract so the original callers remain untouched, and implements the
// platform services without loading user32/kernel32.
public static class Type_0200069E
{
    public enum Type_0200069F : long { }
    public enum Type_020006A2 { }
    public enum Type_020006A3 : long { }
    public enum Type_020006A4 : uint { }

    public static class Type_020006A0
    {
        public class Type_020006A1 : NativeWindow
        {
            private Type_020002FA callback;

            internal Type_020006A1(IntPtr arg0) { }
            internal void Method_671DF7A1D75CFA60(Type_020002FA arg0) =>
                callback = (Type_020002FA)Delegate.Combine(callback, arg0);
            internal void Method_54B749099EA92608(Type_020002FA arg0) =>
                callback = (Type_020002FA)Delegate.Remove(callback, arg0);
            internal void Method_E5F830236AB5FFB1() { }

            protected override void WndProc(ref Message arg0)
            {
                callback?.Invoke(ref arg0);
            }
        }

        internal static Rectangle Method_32FC8AB1F8187125()
        {
            var size = MacStableWindow.ClientSize;
            return new Rectangle(0, 0, size.Width, size.Height);
        }
    }

    internal static IntPtr Method_ADE1763C39CB1CFE(IntPtr arg0, string arg1) => IntPtr.Zero;
    public static IntPtr Method_C41924733836C5D2(string arg0) => IntPtr.Zero;
    public static bool Method_C3E3FAE68E8197F6() => false;
    public static bool Method_A12813901FDF2271(IntPtr arg0, int arg1) => false;
    internal static ushort Method_27C3553D0DDC0FD5(string arg0) => 0;
    internal static ushort Method_314E17A0ACE048DB(ushort arg0) => 0;
    internal static int Method_86923EDF3A9F39DA(IntPtr arg0, string arg1, int arg2) => 0;
    internal static int Method_BBCA51A3307CA8A1(IntPtr arg0, string arg1) => 0;
    public static int Method_44B649866C6EE812(int arg0) => 0;
    public static bool Method_521CDB1DD74F31CF(Type_020007DE[] arg0, int arg1, int arg2) => false;
    public static bool Method_25E47BBC8ED96581(IntPtr arg0, int arg1, Type_02000312[] arg2, int arg3) => false;
    public static bool Method_53B2807A9E0BAC13(IntPtr arg0) => true;
    public static int Method_B29722BDA80DA528(IntPtr arg0, Type_020001B2 arg1, out Type_02000976 arg2, ref int arg3, int arg4)
    {
        arg2 = default;
        return 0;
    }
    public static bool Method_60A71E6B1259E4BC(int arg0, out Type_020003E2 arg1)
    {
        arg1 = default;
        return false;
    }
    internal static IntPtr Method_8924E81F0D79DC12(IntPtr arg0, Type_020006A2 arg1, IntPtr arg2) => IntPtr.Zero;
    public static long Method_543B729DCA28B8F6(IntPtr arg0, Type_020006A2 arg1) => 0;
    public static int Method_297E1288BCDEC51D(IntPtr arg0, IntPtr arg1, int arg2, int arg3, int arg4, int arg5, int arg6) => 1;
    public static uint Method_A2DDE61EA29C2E6F(Type_020006A4 arg0) => (uint)arg0;
    public static bool Method_2658228A24530128(string arg0, out ulong arg1, out ulong arg2, out ulong arg3)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(arg0));
        var drive = new DriveInfo(root!);
        arg1 = (ulong)drive.AvailableFreeSpace;
        arg2 = (ulong)drive.TotalSize;
        arg3 = (ulong)drive.TotalFreeSpace;
        return true;
    }
}
