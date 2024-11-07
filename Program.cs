using Microsoft.Win32;
using OpenCad.Cli;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using TidyHPC.Extensions;
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

async Task<string> getHttpProxy()
{
    return await Util.cmdAsync2(Environment.CurrentDirectory, "git config --global http.proxy");
}

bool IsHostingBundleInstalled(string version)
{
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        // 注册表路径
        string uninstallKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

        using (RegistryKey? uninstallKey = Registry.LocalMachine.OpenSubKey(uninstallKeyPath))
        {
            if (uninstallKey == null)
                return false;

            // 遍历所有子项
            foreach (string subkeyName in uninstallKey.GetSubKeyNames())
            {
                using (RegistryKey? subkey = uninstallKey.OpenSubKey(subkeyName))
                {
                    if (subkey == null)
                        continue;

                    // 获取 DisplayName 和 DisplayVersion
                    string? displayName = subkey.GetValue("DisplayName") as string;
                    string? displayVersion = subkey.GetValue("DisplayVersion") as string;

                    // 检查名称和版本是否匹配
                    if (!string.IsNullOrEmpty(displayName) &&
                        displayName.Contains("Microsoft ASP.NET Core") &&
                        displayVersion == version)
                    {
                        return true; // 找到匹配项，已安装
                    }
                }
            }
        }

        return false; // 未找到匹配项
    }
    else
    {
        return false;
    }
}



async Task installGit()
{
    var response = await axios.get("https://git-scm.com/download/win", new axiosConfig()
    {
        responseType = "text"
    });
    if (response.data is string dataHtml)
    {
        // 通过正则表达式获取所有a.href
        var hrefRegex = new Regex("<a[^>]*href=\"([^\"]*)\"[^>]*>(.*?)</a>", RegexOptions.IgnoreCase);
        var hrefMatches = hrefRegex.Matches(dataHtml);
        var hrefs = hrefMatches.Select(m => m.Groups[1].Value).ToArray();
        // 获取最新版本的Git
        var gitUrl = hrefs.Where(href => href.Contains("Git-") && href.Contains("-64-bit.exe")).FirstOrDefault();
        var downloadPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.exe");
        await axios.download(gitUrl, downloadPath);
        // git 静默安装
        await Util.execAsync(downloadPath, "/VERYSILENT /NORESTART /NOCANCEL /SP- /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS /COMPONENTS=\"icons,ext\\reg\\shellhere,assoc,assoc_sh\"");
    }
}
async Task<bool> installEnvironment()
{
    if (await checkContainsGit()==false)
    {
        try
        {
            await installGit();
        }
        catch
        {
            Console.WriteLine($"Git Download: https://git-scm.com/downloads/win");
            Console.WriteLine("Please install git first");
            return false;
        }
        
    }
    // 检查所有Path下是否存在tscl.exe
    if (checkContainsTscl() == false)
    {
        
        var binDirectory = "C:\\bin";
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
        Console.WriteLine("installing tscl.exe");
        await axios.download("https://github.com/Cangjier/type-sharp/releases/download/latest/tscl.exe", $"{binDirectory}\\tscl.exe");
        var binSelfPath = $"{binDirectory}\\{Path.GetFileName(Environment.ProcessPath)}";
        if (File.Exists(binSelfPath) == false)
        {
            File.Copy(Environment.ProcessPath, $"{binDirectory}\\{Path.GetFileName(Environment.ProcessPath)}");
        }
        if(IsHostingBundleInstalled("8.0.10") == false)
        {
            var dotNetPath = $"{downloadDirectory}/dotnet-hosting-8.0.10-win.exe";
            await axios.download("https://download.visualstudio.microsoft.com/download/pr/dfbcd81d-e383-4c92-a174-5079bde0a180/b05bcf7656d1ea900bd23c4f1939a642/dotnet-hosting-8.0.10-win.exe",
                dotNetPath);
            await Util.execAsync(dotNetPath, "/install", "/quiet", "/norestart");
        }
    }
    return true;
}


ArgsRouter argsRouter = new();
argsRouter.Register(async ([Args] string[] fullArgs) =>
{
    var httpProxy = await getHttpProxy();
    var iwebProxy = WebRequest.DefaultWebProxy;
    Console.WriteLine($"httpProxy: {httpProxy}");
    if (iwebProxy is WebProxy webProxy && webProxy.Address != null)
    {
        Console.WriteLine($"webProxy: {webProxy.Address}");
        axios.setProxy(webProxy.Address.ToString());
    }
    else if (httpProxy != "")
    {
        axios.setProxy(httpProxy);
    }
    if (await installEnvironment()==false)
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
    //Console.WriteLine(cmd);
    var code = await Util.cmdAsync(Environment.CurrentDirectory, cmd);
    if (code != 0)
    {
        //Console.WriteLine($"cmd failed, {code}");
    }
});

await argsRouter.Route(args);