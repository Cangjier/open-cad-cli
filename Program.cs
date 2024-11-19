using Microsoft.Win32;
using OpenCad.Cli;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using TidyHPC.Extensions;
using TidyHPC.LiteJson;
using TidyHPC.Loggers;
using TidyHPC.Routers.Args;

var binDirectory = "C:\\OPEN_CAD\\bin";

bool IsFileLocked(string filePath)
{
    if(File.Exists(filePath)==false)
    {
        return false;
    }
    FileStream? stream = null;

    try
    {
        // 尝试以只写模式打开文件，不共享
        stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }
    catch (IOException)
    {
        // 如果抛出 IOException，表示文件被占用
        return true;
    }
    finally
    {
        stream?.Close(); // 关闭流释放文件
    }

    return false;
}

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

async Task setGitProxy(string proxy)
{
    await Util.cmdAsync2(Environment.CurrentDirectory, $"git config --global http.proxy {proxy}");
}

async Task unsetGitProxy()
{
    await Util.cmdAsync2(Environment.CurrentDirectory, "git config --global --unset http.proxy");
}

async Task<bool> installGit()
{
    if (await checkContainsGit() == false)
    {
        try
        {
            // 获取最新版本的Git
            var gitUrl = "https://github.com/git-for-windows/git/releases/download/v2.47.0.windows.2/Git-2.47.0.2-64-bit.exe";
            Console.WriteLine($"Downloading {gitUrl}");
            var downloadPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.exe");
            await axios.download(gitUrl, downloadPath);
            // git 静默安装
            Console.WriteLine("Installing Git");
            await Util.execAsync(downloadPath, "/VERYSILENT", "/NORESTART", "/NOCANCEL", "/SP-", "CLOSEAPPLICATIONS", "/RESTARTAPPLICATIONS", "/COMPONENTS=\"icons,ext\\reg\\shellhere,assoc,assoc_sh\"");
        }
        catch (Exception e)
        {
            Logger.Error(e);
            Console.WriteLine(e);
            Console.WriteLine("-".PadRight(32, '-'));
            Console.WriteLine($"Git Download: https://git-scm.com/downloads/win");
            Console.WriteLine("Please install git first");
            return false;
        }
    }
    return true;
}

async Task<bool> installTscl()
{
    try
    {
        bool needUpdateTscl = false;
        var tsclFilePath = Path.Combine(binDirectory, "tscl.exe");
        if (File.Exists(tsclFilePath) == false)
        {
            needUpdateTscl = true;
        }
        else
        {
            var latestResponse = await axios.get("https://api.github.com/repos/Cangjier/type-sharp/releases/latest");
            if (latestResponse.data is Json dataJson)
            {
                var assets = dataJson["assets"];
                var asset = assets.Find(item => Path.GetFileName(item.Read("browser_download_url", string.Empty)) == "tscl.exe");
                var updated_at = asset.Read("updated_at", DateTime.MinValue);
                var tsclFileInfo = new FileInfo(tsclFilePath);
                if (tsclFileInfo.LastWriteTime < updated_at)
                {
                    Console.WriteLine($"local tscl is {tsclFileInfo.LastWriteTime}, latest tscl is {updated_at}");
                    needUpdateTscl = true;
                }
            }
        }
        if (needUpdateTscl)
        {
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
            var tsclPath = Path.Combine(downloadDirectory, "tscl.exe");
            if(IsFileLocked(tsclPath))
            {
                await axios.download("https://github.com/Cangjier/type-sharp/releases/download/latest/tscl.exe", $"{binDirectory}\\tscl.exe.update");
            }
            else
            {
                await axios.download("https://github.com/Cangjier/type-sharp/releases/download/latest/tscl.exe", $"{binDirectory}\\tscl.exe");
            }
            if (Path.GetDirectoryName(Environment.ProcessPath).Replace("\\", "/").ToLower() != binDirectory.ToLower().Replace("\\", "/"))
            {
                File.Copy(Environment.ProcessPath, $"{binDirectory}\\{Path.GetFileName(Environment.ProcessPath)}", true);
            }
            if ((await Util.cmdAsync2(Environment.CurrentDirectory, "dotnet --list-runtimes")).Contains("Microsoft.NETCore.App 8.0.10") == false)
            {
                var dotNetPath = $"{downloadDirectory}/dotnet-hosting-8.0.10-win.exe";
                Console.WriteLine("Downloading dotnet-hosting-8.0.10-win.exe");
                await axios.download("https://download.visualstudio.microsoft.com/download/pr/dfbcd81d-e383-4c92-a174-5079bde0a180/b05bcf7656d1ea900bd23c4f1939a642/dotnet-hosting-8.0.10-win.exe",
                    dotNetPath);
                Console.WriteLine("Installing dotnet-hosting-8.0.10-win.exe");
                await Util.execAsync(dotNetPath, "/install", "/quiet", "/norestart");
            }
            if ((await Util.cmdAsync2(Environment.CurrentDirectory, "dotnet --list-runtimes")).Contains("Microsoft.AspNetCore.App 8.0.10") == false)
            {
                var dotNetPath = $"{downloadDirectory}/aspnetcore-runtime-8.0.10-win-x64.exe";
                Console.WriteLine("Downloading aspnetcore-runtime-8.0.10-win-x64.exe");
                await axios.download("https://download.visualstudio.microsoft.com/download/pr/a17b907f-8457-45a8-90db-53f2665ee49e/49bccd33593ebceb2847674fe5fd768e/aspnetcore-runtime-8.0.10-win-x64.exe",
                    dotNetPath);
                Console.WriteLine("Installing aspnetcore-runtime-8.0.10-win-x64.exe");
                await Util.execAsync(dotNetPath, "/install", "/quiet", "/norestart");
            }
        }
    }
    catch(Exception e)
    {
        Logger.Error(e);
        return false;
    }
    return true;
}

async Task<bool> installEnvironment()
{
    if (await installGit() == false)
    {
        return false;
    }
    if (await installTscl() == false)
    {
        return false;
    }
    return true;
}

ArgsRouter argsRouter = new();
argsRouter.Register(async ([Args] string[] fullArgs) =>
{
    var gitProxy = await getGitProxy();
    var systemProxy = WebRequest.DefaultWebProxy;
    string systemProxyString = string.Empty;
    bool isSettedGitProxy = false;
    try
    {
        if (systemProxy?.GetProxy(new Uri("http://www.example.com")) is Uri webProxy)
        {
            systemProxyString = webProxy.ToString();
            Console.WriteLine($"System Proxy: {webProxy}");
            axios.setProxy(webProxy.ToString());
        }
        else if (gitProxy != "")
        {
            Console.WriteLine($"Git Proxy: {gitProxy}");
            axios.setProxy(gitProxy);
        }
        if (string.IsNullOrEmpty(gitProxy) && string.IsNullOrEmpty(systemProxyString) == false)
        {
            Console.WriteLine($"Set Git Proxy with {systemProxyString}");
            await setGitProxy(systemProxyString);
            isSettedGitProxy = true;
        }
        else if(string.IsNullOrEmpty(gitProxy)&&await Util.IsConnect("http://127.0.0.1:7897/"))
        {
            Console.WriteLine($"Use Proxy: http://127.0.0.1:7897/");
            Console.WriteLine($"Set Git Proxy:  http://127.0.0.1:7897/");
            await setGitProxy("http://127.0.0.1:7897/");
            axios.setProxy("http://127.0.0.1:7897/");
            isSettedGitProxy = true;
        }
        if (await installEnvironment() == false)
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
        await Util.cmdAsync(Environment.CurrentDirectory, cmd);
    }
    catch
    {
        throw;
    }
    finally
    {
        if (isSettedGitProxy)
        {
            await unsetGitProxy();
        }
    }
});

await argsRouter.Route(args);