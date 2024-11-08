﻿using Microsoft.Win32;
using OpenCad.Cli;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using TidyHPC.Extensions;
using TidyHPC.Loggers;
using TidyHPC.Routers.Args;


string GetDownloadFolderPath()
{
    if(Environment.OSVersion.Platform != PlatformID.Win32NT)
    {
        string downloadFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

        // 打开注册表项路径
        using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders"))
        {
            if (key != null&& key.GetValue("{374DE290-123F-4565-9164-39C4925E467B}") is string valueString)
            {
                // 获取 "Downloads" 文件夹的路径
                downloadFolderPath = valueString;

                // 如果路径包含环境变量, 需要解析为完整路径
                downloadFolderPath = Environment.ExpandEnvironmentVariables(downloadFolderPath);
            }
        }

        return downloadFolderPath;
    }
    else
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    }

}

bool checkContainsTscl()
{
    var paths = Environment.GetEnvironmentVariable("Path")?.Split(';');
    if (paths == null)
    {
        Console.WriteLine($"Environment Variable Path is null");
        return false;
    }
    foreach (var path in paths)
    {
        if (File.Exists(Path.Combine(path, "tscl.exe")))
        {
            return true;
        }
    }
    return false;
}

async Task<bool> checkContainsGit()
{
    var output = await Util.cmdAsync2(Environment.CurrentDirectory, "git --version");
    return output.Trim() != "";
}

async Task<string> getGitProxy()
{
    return await Util.cmdAsync2(Environment.CurrentDirectory, "git config --global http.proxy");
}

async Task installGit()
{
    // 获取最新版本的Git
    var gitUrl = "https://github.com/git-for-windows/git/releases/download/v2.47.0.windows.2/Git-2.47.0.2-64-bit.exe";
    Console.WriteLine($"Downloading {gitUrl}");
    var downloadPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.exe");
    await axios.download(gitUrl, downloadPath);
    // git 静默安装
    Console.WriteLine("Installing Git");
    await Util.execAsync(downloadPath, "/VERYSILENT","/NORESTART","/NOCANCEL","/SP-","CLOSEAPPLICATIONS","/RESTARTAPPLICATIONS","/COMPONENTS=\"icons,ext\\reg\\shellhere,assoc,assoc_sh\"");
}
async Task<bool> installEnvironment(bool forceUpdate)
{
    if (await checkContainsGit()==false)
    {
        try
        {
            await installGit();
        }
        catch(Exception e)
        {
            Logger.Error(e);
            Console.WriteLine(e);
            Console.WriteLine("-".PadRight(32, '-'));
            Console.WriteLine($"Git Download: https://git-scm.com/downloads/win");
            Console.WriteLine("Please install git first");
            return false;
        }
        
    }
    // 检查所有Path下是否存在tscl.exe
    if (File.Exists("C:\\OPEN_CAD\\bin\\tscl.exe") == false||forceUpdate==true)
    {
        var binDirectory = "C:\\OPEN_CAD\\bin";
        var downloadDirectory = GetDownloadFolderPath();
        if (Directory.Exists(binDirectory) == false)
        {
            Directory.CreateDirectory(binDirectory);
        }
        var path = Environment.GetEnvironmentVariable("Path");
        if (path?.Contains(binDirectory) == false)
        {
            Environment.SetEnvironmentVariable("Path", $"{Environment.GetEnvironmentVariable("Path")};{binDirectory}", EnvironmentVariableTarget.User);
        }
        Console.WriteLine("Downloading tscl");
        await axios.download("https://github.com/Cangjier/type-sharp/releases/download/latest/tscl.exe", $"{binDirectory}\\tscl.exe");
        Console.WriteLine("Installing tscl");
        var binSelfPath = $"{binDirectory}\\{Path.GetFileName(Environment.ProcessPath)}";
        if (Path.GetDirectoryName(Environment.ProcessPath).Replace("\\", "/").ToLower() != "c:/bin")
        {
            File.Copy(Environment.ProcessPath, $"{binDirectory}\\{Path.GetFileName(Environment.ProcessPath)}", true);
        }
        if((await Util.cmdAsync2(Environment.CurrentDirectory, "dotnet --list-runtimes")).Contains("Microsoft.NETCore.App 8.0.10")==false)
        {
            var dotNetPath = $"{downloadDirectory}/dotnet-hosting-8.0.10-win.exe";
            Console.WriteLine("Downloading dotnet-hosting-8.0.10-win.exe");
            await axios.download("https://download.visualstudio.microsoft.com/download/pr/dfbcd81d-e383-4c92-a174-5079bde0a180/b05bcf7656d1ea900bd23c4f1939a642/dotnet-hosting-8.0.10-win.exe",
                dotNetPath);
            Console.WriteLine("Installing dotnet-hosting-8.0.10-win.exe");
            await Util.execAsync(dotNetPath, "/install", "/quiet", "/norestart");
        }
    }
    return true;
}


ArgsRouter argsRouter = new();
argsRouter.Register(async ([Args] string[] fullArgs) =>
{
    var gitProxy = await getGitProxy();
    var systemProxy = WebRequest.DefaultWebProxy;
    if (systemProxy?.GetProxy(new Uri("http://www.example.com")) is Uri webProxy)
    {
        Console.WriteLine($"System Proxy: {webProxy}");
        axios.setProxy(webProxy.ToString());
    }
    else if (gitProxy != "")
    {
        Console.WriteLine($"Git Proxy: {gitProxy}");
        axios.setProxy(gitProxy);
    }
    if (await installEnvironment(fullArgs.Length == 0) == false)
    {
        return;
    }
    if (fullArgs.Length == 0)
    {
        var name = Assembly.GetExecutingAssembly().GetName();
        Console.WriteLine($"{name.Name} {name.Version}");
        return;
    }
    var cmdTail = "--application-name open-cad --repository https://github.com/Cangjier/open-cad.git";
    var cmd = $"tscl run {fullArgs.Join(" ")} {cmdTail}";
    if (fullArgs.Length == 1 && fullArgs[0] == "list")
    {
        cmd = $"tscl list {cmdTail}";
    }
    var code = await Util.cmdAsync(Environment.CurrentDirectory, cmd);
    if (code != 0)
    {
        //Console.WriteLine($"cmd failed, {code}");
    }
});

await argsRouter.Route(args);