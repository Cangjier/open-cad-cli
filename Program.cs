using OpenCad.Cli;
using System.Reflection;
using TidyHPC.Extensions;
using TidyHPC.Routers.Args;

bool checkContainsTscl()
{
    var paths = Environment.GetEnvironmentVariable("Path")?.Split(';');
    if (paths == null)
    {
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

async Task<string> getHttpProxy()
{
    return await Util.cmdAsync2(Environment.CurrentDirectory, "git config --global http.proxy");
}

async Task installEnvironment()
{
    // 检查所有Path下是否存在tscl.exe
    if (checkContainsTscl() == false)
    {
        var httpProxy = await getHttpProxy();
        if(httpProxy != "")
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
        await axios.download("https://github.com/Cangjier/type-sharp/releases/download/latest/tscl.exe", $"{binDirectory}\\tscl.exe");
        var binSelfPath = $"{binDirectory}\\{Path.GetFileName(Environment.ProcessPath)}";
        if (File.Exists(binSelfPath) == false)
        {
            File.Copy(Environment.ProcessPath, $"{binDirectory}\\{Path.GetFileName(Environment.ProcessPath)}");
        }
    }
}

ArgsRouter argsRouter = new();
argsRouter.Register(async ([Args] string[] fullArgs) =>
{
    await installEnvironment();
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