using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using TidyHPC.Extensions;

namespace OpenCad.Cli;
internal class Util
{
    static Util()
    {
        AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
        {
            foreach (var process in BackgroundProcesses.Values)
            {
                try
                {
                    process.Kill();
                }
                catch
                {

                }
            }
        };

        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            foreach (var process in BackgroundProcesses.Values)
            {
                try
                {
                    process.Kill();
                }
                catch
                {

                }
            }
        };
    }

    public static ConcurrentDictionary<int,Process> BackgroundProcesses { get; } = new();

    public static UTF8Encoding UTF8 { get; } = new(false);

    public static string GetShell()
    {
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            return "cmd.exe"; // Windows 下使用 cmd
        }
        else
        {
            return "/bin/bash"; // Linux/MacOS 下使用 bash
        }
    }

    public static string GetShellArguments(string command)
    {
        // 处理命令行中的双引号情况
        string escapedCommand = command.Replace("\"", "\\\"");

        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            return $"/c {escapedCommand}"; // Windows 下 cmd 需要使用 /c 来执行命令
        }
        else
        {
            return $"-c \"{escapedCommand}\""; // Linux/MacOS 下 bash 使用 -c 参数
        }
    }

    public static async Task<int> cmdAsync(string workingDirectory, string commandLine)
    {
        try
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = Util.GetShell(),
                Arguments = Util.GetShellArguments(commandLine),
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            using Process process = new() { StartInfo = startInfo };
            // 启动进程
            process.Start();
            BackgroundProcesses.TryAdd(process.Id, process);
            // 读取标准输出并打印  
            process.OutputDataReceived += (sender, e) => Console.WriteLine(e.Data);
            process.BeginOutputReadLine();

            // 读取标准错误并打印  
            process.ErrorDataReceived += (sender, e) => Console.WriteLine(e.Data);
            process.BeginErrorReadLine();
            // 等待进程退出
            await process.WaitForExitAsync();
            BackgroundProcesses.TryRemove(process.Id, out _);

            // 返回进程的退出代码
            return process.ExitCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine("An error occurred: " + ex.Message);
            return -1; // 错误时返回 -1
        }
    }

    public static async Task<string> cmdAsync2(string workingDirectory, string commandLine)
    {
        try
        {
            // 创建一个新的进程启动信息
            ProcessStartInfo startInfo = new()
            {
                FileName = Util.GetShell(), // 根据系统获取合适的 shell
                Arguments = Util.GetShellArguments(commandLine), // shell 的参数，包括命令行
                UseShellExecute = false,        // 启用 shell 执行，避免重定向
                CreateNoWindow = true,        // 允许创建窗口
                WorkingDirectory = workingDirectory, // 设置工作目录
                RedirectStandardOutput = true, // 重定向标准输出
            };

            using Process process = new() { StartInfo = startInfo };
            
            List<string> lines = new();
            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    lines.Add(e.Data);
                }
            };
            // 启动进程
            process.Start();
            BackgroundProcesses.TryAdd(process.Id, process);
            // 开始异步读取输出
            process.BeginOutputReadLine();
            // 等待进程退出
            await process.WaitForExitAsync();
            BackgroundProcesses.TryRemove(process.Id, out _);

            // 返回进程的退出代码
            return lines.Join("\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine("An error occurred: " + ex.Message);
            return "";
        }
    }

    public static async Task<int> execAsync(string path, params string[] args)
    {
        using var process = new Process();
        process.StartInfo.FileName = path;
        args.Foreach(process.StartInfo.ArgumentList.Add);
        process.Start();
        BackgroundProcesses.TryAdd(process.Id, process);
        await process.WaitForExitAsync();
        BackgroundProcesses.TryRemove(process.Id, out _);
        return process.ExitCode;
    }
}
