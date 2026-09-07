using System.Diagnostics;

namespace POS.PrintAgent.Installer;

public class Program
{
    private const string ServiceName = "POSPrintAgent";
    private const string DisplayName = "POS Print Agent Service";
    private const string Description = "Handles local printing for POS system";

    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        var command = args[0].ToLower();
        var exePath = args.Length > 1 ? args[1] : GetDefaultServicePath();

        return command switch
        {
            "install" => await InstallService(exePath),
            "uninstall" => await UninstallService(),
            "start" => await RunSc("start", ServiceName),
            "stop" => await RunSc("stop", ServiceName),
            "status" => await RunSc("query", ServiceName),
            "restart" => await RestartService(),
            _ => PrintUsageAndExit()
        };
    }

    private static async Task<int> InstallService(string exePath)
    {
        if (!File.Exists(exePath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERROR] Service executable not found: {exePath}");
            Console.ResetColor();
            return 1;
        }

        Console.WriteLine($"[INFO] Installing service from: {exePath}");

        var createResult = await RunSc("create", ServiceName,
            $"binPath=\"{exePath}\"",
            $"DisplayName=\"{DisplayName}\"",
            "start=auto");

        if (createResult != 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[ERROR] Failed to create service. Make sure you're running as Administrator.");
            Console.ResetColor();
            return createResult;
        }

        await RunSc("description", ServiceName, $"\"{Description}\"");
        await RunSc("failure", ServiceName, "reset=86400", "actions=restart/5000/restart/10000/restart/30000");

        Console.WriteLine("[INFO] Service installed successfully. Starting...");
        var startResult = await RunSc("start", ServiceName);

        if (startResult == 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[SUCCESS] Service is now running!");
            Console.ResetColor();
        }

        return 0;
    }

    private static async Task<int> UninstallService()
    {
        Console.WriteLine("[INFO] Stopping service...");
        await RunSc("stop", ServiceName);

        // استنى ثانيتين عشان الخدمة توقف
        await Task.Delay(2000);

        Console.WriteLine("[INFO] Deleting service...");
        var result = await RunSc("delete", ServiceName);

        if (result == 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[SUCCESS] Service uninstalled successfully!");
            Console.ResetColor();
        }

        return result;
    }

    private static async Task<int> RestartService()
    {
        Console.WriteLine("[INFO] Restarting service...");
        await RunSc("stop", ServiceName);
        await Task.Delay(2000);
        return await RunSc("start", ServiceName);
    }

    private static async Task<int> RunSc(params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "sc.exe",
            Arguments = string.Join(" ", args),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi);
        if (proc == null) return 1;

        var output = await proc.StandardOutput.ReadToEndAsync();
        var error = await proc.StandardError.ReadToEndAsync();

        await proc.WaitForExitAsync();

        if (!string.IsNullOrEmpty(output)) Console.WriteLine(output);
        if (!string.IsNullOrEmpty(error)) Console.Error.WriteLine(error);

        return proc.ExitCode;
    }

    private static string GetDefaultServicePath()
    {
        var current = AppContext.BaseDirectory;
        return Path.Combine(current, "POS.PrintAgent.Service.exe");
    }

    private static void PrintUsage()
    {
        Console.WriteLine("========================================");
        Console.WriteLine("  POS Print Agent Installer");
        Console.WriteLine("========================================");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  installer install [path]   Install the service");
        Console.WriteLine("  installer uninstall        Uninstall the service");
        Console.WriteLine("  installer start            Start the service");
        Console.WriteLine("  installer stop             Stop the service");
        Console.WriteLine("  installer restart          Restart the service");
        Console.WriteLine("  installer status           Check service status");
        Console.WriteLine();
        Console.WriteLine("Note: Run as Administrator for install/uninstall operations.");
    }

    private static int PrintUsageAndExit()
    {
        PrintUsage();
        return 1;
    }
}
