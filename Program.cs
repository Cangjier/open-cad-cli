using OpenCad.Cli;
using System.Reflection;
using System.Text.RegularExpressions;
using TidyHPC.Extensions;
using TidyHPC.Routers.Args;

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
        var httpProxy = await getHttpProxy();
        Console.WriteLine($"httpProxy: {httpProxy}");
        if (httpProxy != "")
        {
            axios.setProxy(httpProxy);
        }
        var binDirectory = "C:\\bin";
        if(Directory.Exists(binDirectory) == false)
        {
            Directory.CreateDirectory(binDirectory);
        }
        var path = Environment.GetEnvironmentVariable("Path");
        if (path.Contains(binDirectory) == false)
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
    }
    return true;
}

ArgsRouter argsRouter = new();
argsRouter.Register(async ([Args] string[] fullArgs) =>
{
    if(await installEnvironment()==false)
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