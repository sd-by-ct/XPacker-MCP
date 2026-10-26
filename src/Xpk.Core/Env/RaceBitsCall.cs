using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Xpk.Core.Env
{
    /// <summary>
    /// Runs a RaceBits call on its own STA thread with a time-out. Some RaceBits errors open a modal MessageBox
    /// (e.g. ImportFBX with an invalid root path), which would block `xpk serve` forever. A dialog that appears on
    /// the worker thread is read, closed and reported as RACEBITS_DIALOG; a call that does not finish in time is
    /// abandoned (background thread) and reported as RACEBITS_TIMEOUT.
    /// </summary>
    public static class RaceBitsCall
    {
        public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);

        private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

        public static void Run(Action action, TimeSpan timeout, string what) =>
            Run(() => { action(); return true; }, timeout, what);

        public static T Run<T>(Func<T> action, TimeSpan timeout, string what)
        {
            T result = default(T);
            ExceptionDispatchInfo error = null;
            int nativeThreadId = 0;
            using (var done = new ManualResetEventSlim())
            {
                var thread = new Thread(() =>
                {
                    Volatile.Write(ref nativeThreadId, GetCurrentThreadId());
                    try { result = action(); }
                    catch (Exception ex) { error = ExceptionDispatchInfo.Capture(ex); }
                    finally { done.Set(); }
                })
                {
                    IsBackground = true,
                    Name = "RaceBits: " + what,
                };
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();

                var dialogs = new List<string>();
                var closing = new HashSet<IntPtr>();
                DateTime deadline = DateTime.UtcNow + timeout;
                while (!done.Wait(PollInterval))
                {
                    CloseDialogs(Volatile.Read(ref nativeThreadId), dialogs, closing);
                    if (DateTime.UtcNow > deadline)
                        throw new XpkException(ErrorCodes.RaceBitsTimeout,
                            "RaceBits reageert niet binnen " + timeout.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                            " s (" + what + ")." + DialogSuffix(dialogs));
                }

                if (dialogs.Count > 0)
                    throw new XpkException(ErrorCodes.RaceBitsDialog,
                        "RaceBits toonde een melding tijdens " + what + ": " + string.Join(" | ", dialogs), error?.SourceException);
                error?.Throw();
                return result;
            }
        }

        private static string DialogSuffix(List<string> dialogs) =>
            dialogs.Count == 0 ? "" : " Melding: " + string.Join(" | ", dialogs);

        // Standard dialogs (MessageBox) have window class #32770. First WM_CLOSE (works for OK and OK/Cancel boxes),
        // on the next poll IDCANCEL and IDNO for boxes that ignore WM_CLOSE.
        private static void CloseDialogs(int threadId, List<string> dialogs, HashSet<IntPtr> closing)
        {
            if (threadId == 0) return;
            EnumThreadWindows(threadId, (hwnd, _) =>
            {
                if (!IsWindowVisible(hwnd) || ClassName(hwnd) != "#32770") return true;
                if (closing.Add(hwnd))
                {
                    dialogs.Add(DialogText(hwnd));
                    PostMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                }
                else
                {
                    PostMessage(hwnd, WM_COMMAND, (IntPtr)IDCANCEL, IntPtr.Zero);
                    PostMessage(hwnd, WM_COMMAND, (IntPtr)IDNO, IntPtr.Zero);
                }
                return true;
            }, IntPtr.Zero);
        }

        private static string DialogText(IntPtr dialog)
        {
            var parts = new List<string>();
            string title = WindowText(dialog);
            if (title.Length > 0) parts.Add(title);
            EnumChildWindows(dialog, (child, _) =>
            {
                if (ClassName(child) == "Static")
                {
                    string text = WindowText(child);
                    if (text.Trim().Length > 0) parts.Add(text.Trim());
                }
                return true;
            }, IntPtr.Zero);
            return string.Join(": ", parts);
        }

        private static string ClassName(IntPtr hwnd)
        {
            var sb = new StringBuilder(256);
            GetClassName(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }

        private static string WindowText(IntPtr hwnd)
        {
            var sb = new StringBuilder(Math.Max(GetWindowTextLength(hwnd) + 1, 1));
            GetWindowText(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }

        private const uint WM_CLOSE = 0x0010;
        private const uint WM_COMMAND = 0x0111;
        private const int IDCANCEL = 2;
        private const int IDNO = 7;

        private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern int GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern bool EnumThreadWindows(int threadId, EnumWindowsProc callback, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc callback, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hwnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hwnd, StringBuilder name, int maxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int maxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextLength(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
    }
}
