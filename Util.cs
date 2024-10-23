using System.Diagnostics;

namespace OpenCad.Cli;
internal class Util
{
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
            // 读取标准输出并打印  
            process.OutputDataReceived += (sender, e) => Console.WriteLine(e.Data);
            process.BeginOutputReadLine();

            // 读取标准错误并打印  
            process.ErrorDataReceived += (sender, e) => Console.WriteLine(e.Data);
            process.BeginErrorReadLine();
            // 等待进程退出
            await process.WaitForExitAsync();

            // 返回进程的退出代码
            return process.ExitCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine("An error occurred: " + ex.Message);
            return -1; // 错误时返回 -1
        }
    }
}
