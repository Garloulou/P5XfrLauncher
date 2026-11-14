using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using LauncherP5XFr.Helpers;
using WinRT;

namespace LauncherP5XFr;

public static class Program
{
    private const string MutexName = @"Local\LauncherP5XFr_SingleInstance_Mutex";
    private static Mutex? _instanceMutex;

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_RESTORE = 9;

    [STAThread]
    public static void Main(string[] args)
    {
        // 1. Set AppContext & WindowsAppRuntime base directory for single-file unpackaged deployment
        Environment.SetEnvironmentVariable("MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY", AppContext.BaseDirectory);

        // 2. Cleanup any leftover .old binary from a previous self-update
        CleanupPreviousExecutable();

        // 3. Single Instance Enforcement (Process check first, then Mutex)
        try
        {
            var currentPid = Process.GetCurrentProcess().Id;
            var currentProcessName = Process.GetCurrentProcess().ProcessName;
            var existingProcesses = Process.GetProcessesByName(currentProcessName)
                .Where(p => p.Id != currentPid)
                .ToList();

            if (existingProcesses.Count > 0)
            {
                // Another instance is genuinely running -> bring it to front
                BringExistingInstanceToFront();
                return;
            }

            _instanceMutex = new Mutex(true, MutexName, out _);
        }
        catch
        {
            // Ignore mutex creation failure and continue
        }

        // 4. Global unhandled exception logging
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            LogFatalCrash(e.ExceptionObject as Exception);
        };

        // 5. Initialize WinRT COM wrappers and start WinUI
        ComWrappersSupport.InitializeComWrappers();
        Application.Start((p) =>
        {
            var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            var context = new DispatcherQueueSynchronizationContext(dispatcherQueue);
            SynchronizationContext.SetSynchronizationContext(context);
            new App();
        });

        // Release mutex on clean shutdown
        try
        {
            _instanceMutex?.ReleaseMutex();
            _instanceMutex?.Dispose();
        }
        catch
        {
            // Ignore
        }
    }

    private static void CleanupPreviousExecutable()
    {
        try
        {
            var currentExe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(currentExe))
            {
                var oldExe = currentExe + ".old";
                if (File.Exists(oldExe))
                {
                    File.Delete(oldExe);
                }
            }
        }
        catch
        {
            // Ignore - may be cleaned up on next launch
        }
    }

    private static void BringExistingInstanceToFront()
    {
        try
        {
            var hWnd = FindWindow(null, "Persona 5: The Phantom X - Launcher FR");
            if (hWnd != IntPtr.Zero)
            {
                ShowWindow(hWnd, SW_RESTORE);
                SetForegroundWindow(hWnd);
            }
        }
        catch
        {
            // Ignore
        }
    }

    private static void LogFatalCrash(Exception? ex)
    {
        try
        {
            var logDir = Path.Combine(AppEnvironment.AppDirectory, "logs");
            Directory.CreateDirectory(logDir);
            var logFile = Path.Combine(logDir, "launcher.log");
            var message = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [FATAL] Crash non géré : {ex?.Message}\n{ex?.StackTrace}\n";
            File.AppendAllText(logFile, message);
        }
        catch
        {
            // Best effort
        }
    }
}
