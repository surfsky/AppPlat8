using System.IO;
using App.DAL;
using App.DAL.GIS;
using App.Entities;
using Quartz;

/// <summary>台风数据导入任务</summary>
public class TyphoonImportJob : IJob
{
    /// <summary>执行导入</summary>
    public Task Execute(IJobExecutionContext context)
    {
        var root = ResolveRoot();
        var metaPath = Path.Combine(root, "Doc", "Map", "js", "layers", "typhoons.json");
        var dataDir = Path.Combine(root, "Doc", "Map", "typhoon", "typhoon-org-cn");
        var db = EntityConfig.Db as AppPlatContext;
        if (db == null)
            throw new InvalidOperationException("数据库上下文未初始化");
        var result = GisTyphoonImporter.ImportFolder(db, metaPath, dataDir, 2016);
        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 台风导入完成，新增台风 {result.TyphoonAddCnt}，更新台风 {result.TyphoonEditCnt}，新增轨迹 {result.LogAddCnt}，替换旧轨迹 {result.LogDeleteCnt}");
        foreach (var log in result.Logs)
            Console.WriteLine(log);
        return Task.CompletedTask;
    }

    /// <summary>定位项目根目录</summary>
    static string ResolveRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (int i = 0; i < 8 && dir != null; i++, dir = dir.Parent)
        {
            var metaPath = Path.Combine(dir.FullName, "Doc", "Map", "js", "layers", "typhoons.json");
            if (File.Exists(metaPath))
                return dir.FullName;
        }
        return Directory.GetCurrentDirectory();
    }
}
