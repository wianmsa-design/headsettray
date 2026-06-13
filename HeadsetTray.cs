using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace HeadsetTray
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TrayApp());
        }
    }

    class TrayApp : ApplicationContext
    {
        private NotifyIcon _trayIcon;
        private Thread _pollThread;
        private volatile bool _running = true;

        private enum HeadsetState { Unknown, Off, On }
        private HeadsetState _lastState = HeadsetState.Unknown;

        private Icon _iconOn;
        private Icon _iconOff;

        private const string HeadsetControlPath = @"C:\Program Files\headsetcontrol 0.0.0~continuous-49-g4d57d17\bin\headsetcontrol.exe";
        private const int POLL_OFF_MS = 10000;
        private const int POLL_ON_MS = 5000;

        public TrayApp()
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (var stream = assembly.GetManifestResourceStream("HeadsetTray.connected.ico"))
                _iconOn = new Icon(stream);
            using (var stream = assembly.GetManifestResourceStream("HeadsetTray.disconnected.ico"))
                _iconOff = new Icon(stream);

            _trayIcon = new NotifyIcon
            {
                Icon = _iconOff,
                Text = "Headset: Checking...",
                Visible = true,
                ContextMenuStrip = BuildMenu()
            };

            _pollThread = new Thread(PollLoop) { IsBackground = true };
            _pollThread.Start();
        }

        private ContextMenuStrip BuildMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("Exit", null, (s, e) =>
            {
                _running = false;
                _trayIcon.Visible = false;
                Application.Exit();
            });
            return menu;
        }

        private void PollLoop()
        {
            while (_running)
            {
                QueryHeadset();
                int interval = _lastState == HeadsetState.On ? POLL_ON_MS : POLL_OFF_MS;
                for (int i = 0; i < interval / 100 && _running; i++)
                    Thread.Sleep(100);
            }
        }

        private void QueryHeadset()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = HeadsetControlPath,
                    Arguments = "-o json -b",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                string output;
                using (var proc = Process.Start(psi))
                {
                    output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit(5000);
                }

                HeadsetState newState = (output.Contains("BATTERY_CHARGING") || output.Contains("BATTERY_AVAILABLE"))
                    ? HeadsetState.On
                    : HeadsetState.Off;

                if (newState != _lastState && _lastState != HeadsetState.Unknown)
                {
                    if (newState == HeadsetState.On)
                        _trayIcon.ShowBalloonTip(3000, "Headset", "Connected", ToolTipIcon.None);
                    else
                        _trayIcon.ShowBalloonTip(3000, "Headset", "Disconnected", ToolTipIcon.None);
                }

                _lastState = newState;
                _trayIcon.Icon = newState == HeadsetState.On ? _iconOn : _iconOff;
                _trayIcon.Text = newState == HeadsetState.On ? "Headset: Connected" : "Headset: Off";
            }
            catch
            {
                _trayIcon.Text = "Headset: Error";
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _running = false;
                _trayIcon?.Dispose();
                _iconOn?.Dispose();
                _iconOff?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
