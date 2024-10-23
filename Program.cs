using OpenCad.Cli;
using TidyHPC.Routers.Args;

async Task init(
    [ArgsIndex] string cadName,
    [Args] string[]? fullArgs = null)
{
    var cmd = $"tscl run {cadName} init --application-name open-cad --repository https://github.com/Cangjier/open-cad.git";
    Console.WriteLine(cmd);
    await Util.cmdAsync(Environment.CurrentDirectory, cmd);
}


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
argsRouter.Register(["init"], init);

await argsRouter.Route(args);