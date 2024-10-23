using OpenCad.Cli;
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

async Task check()
{
    // 检查所有Path下是否存在tscl.exe
    if (checkContainsTscl()==false)
    {
        Console.WriteLine("请先安装tscl");
    }
}

ArgsRouter argsRouter = new();
argsRouter.Register(async ([Args] string[] fullArgs) =>
{
    var cmdTail = "--application-name open-cad --repository https://github.com/Cangjier/open-cad.git";
    var cmd = $"tscl run {fullArgs.Join(" ")} {cmdTail}";
    if (fullArgs.Length == 1 && fullArgs[0] == "list")
    {
        cmd = $"tscl list {cmdTail}";
    }
    Console.WriteLine(cmd);
    var code = await Util.cmdAsync(Environment.CurrentDirectory, cmd);
    if (code != 0)
    {
        Console.WriteLine($"cmd failed, {code}");
    }
});

await argsRouter.Route(args);