using System.Threading;
using System.Windows;

namespace HordesOfAzeroth.Launcher
{
    public partial class App : Application
    {
        const string InstanceName = "HordesOfAzerothLauncher";

        Mutex m_single;
        EventWaitHandle m_showRequest;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            int source = System.Array.IndexOf(e.Args, "--source");
            if (source >= 0 && source + 1 < e.Args.Length)
                UpdateCheck.LocalSource = e.Args[source + 1];
            m_single = new Mutex(true, InstanceName, out bool first);
            m_showRequest = new EventWaitHandle(false, EventResetMode.AutoReset, InstanceName + ".Show");
            if (!first)
            {
                m_showRequest.Set();
                Shutdown();
                return;
            }
            MainWindow window = new MainWindow();
            window.Show();
            Thread listener = new Thread(() =>
            {
                while (m_showRequest.WaitOne())
                    Dispatcher.Invoke(window.ShowFromTray);
            }) { IsBackground = true };
            listener.Start();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            m_single?.Dispose();
            base.OnExit(e);
        }
    }
}
