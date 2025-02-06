using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WinFormsApp1
{
    public partial class MainForm : Form
    {
        private bool isTrackingActive = false;
        private CancellationTokenSource cancellationTokenSource;

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        public static extern int ToUnicode(uint wVirtKey, uint wScanCode, byte[] lpKeyState, StringBuilder pwszBuff, int cchBuff, uint wFlags);

        [DllImport("user32.dll")]
        public static extern bool GetKeyboardState(byte[] lpKeyState);

        [DllImport("user32.dll")]
        public static extern short GetKeyState(int nVirtKey);

        public MainForm()
        {
            SetupUIComponents();
            this.Controls.AddRange(new Control[]
            {
                logKeystrokesCheck,
                monitorProcessesCheck,
                logFilePathInput,
                restrictedWordsInput,
                restrictedAppsInput,
                startButton,
                stopButton,
                reportButton
            });
        }

        private async void StartMonitoring(object sender, EventArgs e)
        {
            isTrackingActive = true;
            cancellationTokenSource = new CancellationTokenSource();
            try
            {
                await TrackUserActivity(cancellationTokenSource.Token);
            }
            catch (TaskCanceledException)
            {
                // Завдання було скасовано, нічого робити не потрібно
            }
        }

        private void StopMonitoring(object sender, EventArgs e)
        {
            isTrackingActive = false;
            cancellationTokenSource?.Cancel();
        }

        private void ShowReport(object sender, EventArgs e)
        {
            if (File.Exists(logFilePathInput.Text))
                Process.Start("notepad.exe", logFilePathInput.Text);
        }

        private async Task TrackUserActivity(CancellationToken cancellationToken)
        {
            string logFile = logFilePathInput.Text;
            string[] restrictedWords = restrictedWordsInput.Text.Split(',');
            string[] restrictedApps = restrictedAppsInput.Text.Split(',');

            while (isTrackingActive)
            {
                if (monitorProcessesCheck.Checked)
                {
                    var processes = Process.GetProcesses().Select(p => p.ProcessName);
                    foreach (var process in processes)
                    {
                        if (restrictedApps.Contains(process))
                        {
                            File.AppendAllText(logFile, $"Припинено роботу обмеженого додатка: {process} {DateTime.Now}\n");
                            foreach (var proc in Process.GetProcessesByName(process)) proc.Kill();
                        }
                    }
                }

                if (logKeystrokesCheck.Checked)
                {
                    LogKeystrokes(logFile);
                }

                await Task.Delay(50, cancellationToken);
            }
        }

        private void LogKeystrokes(string logFile)
        {
            StringBuilder output = new StringBuilder();
            byte[] keyboardState = new byte[256];

            GetKeyboardState(keyboardState);

            if (IsShiftPressed())
                keyboardState[0x10] = 0x80;

            if (IsCapsLockActive())
                keyboardState[0x14] = 0x01;

            for (int key = 8; key <= 255; key++)
            {
                short keyState = GetAsyncKeyState(key);
                if ((keyState & 0x8000) != 0)
                {
                    StringBuilder keyName = new StringBuilder(10);
                    if (ToUnicode((uint)key, 0, keyboardState, keyName, keyName.Capacity, 0) > 0)
                    {
                        output.Append(keyName.ToString());
                    }
                }
            }

            if (output.Length > 0)
            {
                File.AppendAllText(logFile, output.ToString(), Encoding.UTF8);
            }
        }

        private bool IsShiftPressed()
        {
            return (GetAsyncKeyState(0x10) & 0x8000) != 0;
        }

        private bool IsCapsLockActive()
        {
            return (GetKeyState(0x14) & 0x0001) != 0;
        }
    }
}