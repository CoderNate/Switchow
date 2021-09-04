using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace HotkeyHelper
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            // Based on https://social.msdn.microsoft.com/Forums/en-US/c061954b-19bf-463b-a57d-b09c98a3fe7d/assign-global-hotkey-to-a-system-tray-application-in-c
            var HotKeyManager = new HotkeyManager();
            RegisterHotKey(HotKeyManager.Handle, Constants.HOTKEY_ID, Constants.ALT, (int)Keys.Space);
            Application.Run();
        }

        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vlc);
        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }

    internal static class Constants
    {
        public const int HOTKEY_ID = 9002;

        public const int NOMOD = 0x0000;
        public const int ALT = 0x0001;
        public const int CTRL = 0x0002;
        public const int SHIFT = 0x0004;
        public const int WIN = 0x0008;

        public const int WM_HOTKEY_MSG_ID = 0x0312;
    }

    public sealed class HotkeyManager : NativeWindow, IDisposable
    {
        public HotkeyManager()
        {
            CreateHandle(new CreateParams());
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Constants.WM_HOTKEY_MSG_ID)
            {
                if (m.WParam.ToInt32() == Constants.HOTKEY_ID)
                {
                    System.Diagnostics.Process.Start(@"Switchow.exe");
                }
            }
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            DestroyHandle();
        }

    }
}

