using App.DAL;
using App.DAL.GIS;
using App.Entities;
using Microsoft.EntityFrameworkCore;
using System.IO;

var connArg = args.FirstOrDefault(t => t.StartsWith("--conn=", StringComparison.OrdinalIgnoreCase));
var conn = connArg?.Substring("--conn=".Length);
if (string.IsNullOrWhiteSpace(conn))
{
	var dbPath = ResolveDefaultDbPath();
	conn = $"Data Source={dbPath};";
}

var options = new DbContextOptionsBuilder<AppPlatContext>()
	.UseSqlite(conn)
	.Options;

using var db = new AppPlatContext(options);
if (!db.Database.CanConnect())
{
	Console.WriteLine($"数据库连接失败: {conn}");
	return;
}

EntityConfig.Instance.OnGetDb += () => db;
EntityConfig.Instance.OnGetDataAccessScope += () => new DataAccessScope
{
	Enabled = false,
	AllowAll = true,
};

var count = GisMenu.FixAll();
Console.WriteLine($"GisMenu.FixAll 执行完成，更新菜单数: {count}");

static string ResolveDefaultDbPath()
{
	var candidates = new List<string>();

	void AddCandidate(string path)
	{
		if (string.IsNullOrWhiteSpace(path))
			return;
		candidates.Add(Path.GetFullPath(path));
	}

	AddCandidate(Path.Combine(Directory.GetCurrentDirectory(), "App", "Db", "sqlite.db"));
	AddCandidate(Path.Combine(AppContext.BaseDirectory, "App", "Db", "sqlite.db"));

	var current = new DirectoryInfo(AppContext.BaseDirectory);
	for (var i = 0; i < 8 && current != null; i++)
	{
		AddCandidate(Path.Combine(current.FullName, "App", "Db", "sqlite.db"));
		current = current.Parent;
	}

	var firstExists = candidates.FirstOrDefault(File.Exists);
	if (!string.IsNullOrWhiteSpace(firstExists))
		return firstExists;

	return candidates.First();
}
