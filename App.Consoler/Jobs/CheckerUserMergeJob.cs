using Quartz;
using App.DAL;
using App.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// 分析 & 合并带数字后缀的重复网格员账户
/// 使用方式：
///   1. 先做只读分析，打印+写出计划CSV
///      cd App.Consoler && dotnet run -- --run=CheckerUserMergeJob --mode=analyze
///   2. 确认无误后应用（会先做整库 SQLite 备份再单事务更新+删除）
///      cd App.Consoler && dotnet run -- --run=CheckerUserMergeJob --mode=apply
/// </summary>
public class CheckerUserMergeJob : IJob
{
    static readonly Regex _nameRegex = new Regex(@"^(?<base>.+?)(?<num>\d{1,3})$", RegexOptions.Compiled);

    /// <summary>
    /// 是否是「真实网格员姓名末尾 1~3 位数字」的合并规则（排除掉 login 名、合成名、数字超过 3 位、纯英文 userXXX 等噪音）
    /// </summary>
    static bool IsRealCheckerGrouping(string baseName, List<UserLite> list)
    {
        // 基础名不能含特殊分隔符（如 / 赵师泉3/钟飞全 这种是合成名）
        if (baseName.Any(c => "/\\|,，;；:：".Contains(c))) return false;
        // 基础名必须含中文字符（真实网格员姓名都是中文）
        if (!baseName.Any(ch => ch >= 0x4E00 && ch <= 0x9FFF)) return false;
        // 组内至少有 1 条「有数字后缀且 RealName 含中文字符」的账户
        return list.Any(x => x.Base != null && x.Num > 0 && x.Num < 1000 && (x.RealName ?? "").Any(ch => ch >= 0x4E00 && ch <= 0x9FFF));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var extras = JobExtras.Current;
        string Get(string k)
        {
            if (extras != null && extras.TryGetValue(k, out var v1) && v1 != null) return v1;
            try { return context.MergedJobDataMap.GetString(k) ?? ""; }
            catch { return ""; }
        }
        var mode = Get("mode");
        if (string.IsNullOrEmpty(mode)) mode = "analyze";

        var allUsers = await App.DAL.User.Set
            .AsNoTracking()
            .Select(u => new { u.Id, u.Name, u.RealName, u.IsDel, u.DeleteDt, u.OrgId })
            .ToListAsync();

        var groups = new Dictionary<string, List<UserLite>>(StringComparer.Ordinal);
        foreach (var u in allUsers)
        {
            var text = (u.RealName ?? u.Name ?? "").Trim();
            if (text.Length == 0) continue;
            var m = _nameRegex.Match(text);
            string? baseName = null;
            int num = 0;
            if (m.Success)
            {
                baseName = m.Groups["base"].Value;
                _ = int.TryParse(m.Groups["num"].Value, out num);
            }
            var key = baseName ?? text;
            if (!groups.ContainsKey(key)) groups[key] = new List<UserLite>();
            groups[key].Add(new UserLite(u.Id, u.Name ?? "", text, baseName, num, u.IsDel.GetValueOrDefault()));
        }

        var planRows = new List<MergePlanRow>();
        foreach (var kv in groups)
        {
            var list = kv.Value;
            var hasSuffixed = list.Any(x => x.Base != null);
            if (!hasSuffixed) continue;
            if (!IsRealCheckerGrouping(kv.Key, list)) continue;

            var baseName = kv.Key;
            UserLite target = PickTarget(list);
            var srcs = list.Where(x => x.Id != target.Id).OrderBy(x => x.Num).ThenBy(x => x.Id).ToList();
            if (srcs.Count == 0) continue;

            var plan = new MergePlanRow
            {
                BaseName = baseName,
                TargetId = target.Id,
                TargetName = target.RealName,
                TargetLoginName = target.Name,
            };
            foreach (var s in srcs)
            {
                long sid = s.Id;
                var checkObjects = await App.DAL.CheckObject.Set.AsNoTracking()
                    .Where(o => o.CheckerId == sid)
                    .Select(o => new { o.Id })
                    .ToListAsync();

                var userOrgs = await App.DAL.UserOrg.Set.AsNoTracking()
                    .Where(uo => uo.UserId == sid).CountAsync();
                var roleCount = 0;
                var rolesQuery = App.DAL.User.Set.AsNoTracking()
                    .Where(u => u.Id == sid)
                    .SelectMany(u => u.Roles);
                roleCount = await rolesQuery.CountAsync();

                plan.Sources.Add(new MergeSrcRow
                {
                    SrcId = s.Id,
                    SrcName = s.RealName,
                    SrcLoginName = s.Name,
                    Num = s.Num,
                    IsDel = s.IsDel,
                    CheckObjectCount = checkObjects.Count,
                    CheckObjectSampleIds = checkObjects.Take(10).Select(o => o.Id).ToList(),
                    UserOrgCount = userOrgs,
                    UserRoleCount = roleCount,
                });
            }
            planRows.Add(plan);
        }

        // —— 输出计划到 Console + CSV ——
        var outDir = AppDomain.CurrentDomain.BaseDirectory;
        var csvPath = Path.Combine(outDir, $"CheckerMergePlan_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        var sb = new StringBuilder();
        sb.AppendLine("BaseName,TargetId,TargetName,TargetLogin,SrcId,SrcName,SrcLogin,Num,IsDel,CheckObjectCount,UserOrgCount,UserRoleCount");
        Console.WriteLine("==========================================================");
        Console.WriteLine($"= 合并计划（共 {planRows.Count} 组重复账户） mode={mode}");
        Console.WriteLine("==========================================================");
        foreach (var p in planRows.OrderByDescending(x => x.TotalCheckObjects))
        {
            Console.WriteLine($"[{p.BaseName}] → 目标 Id={p.TargetId} Name={p.TargetName} Login={p.TargetLoginName}");
            foreach (var s in p.Sources)
            {
                Console.WriteLine($"   · 源 Id={s.SrcId,-6} Name={s.SrcName,-12} Login={s.SrcLoginName,-15} Num={s.Num} IsDel={s.IsDel} " +
                                  $"CheckObject={s.CheckObjectCount,4} UserOrg={s.UserOrgCount} Roles={s.UserRoleCount}");
                sb.AppendLine($"{Csv(p.BaseName)},{p.TargetId},{Csv(p.TargetName)},{Csv(p.TargetLoginName)}," +
                              $"{s.SrcId},{Csv(s.SrcName)},{Csv(s.SrcLoginName)},{s.Num},{s.IsDel}," +
                              $"{s.CheckObjectCount},{s.UserOrgCount},{s.UserRoleCount}");
            }
        }
        var totalCheckObjAffected = planRows.Sum(p => p.TotalCheckObjects);
        var totalSrcUsers = planRows.Sum(p => p.Sources.Count);
        Console.WriteLine($"合计：需合并 {totalSrcUsers} 个重复账户，影响 {totalCheckObjAffected} 条 CheckObject.CheckerId 引用，共 {planRows.Count} 组姓名。");
        Console.WriteLine($"计划 CSV 已写入：{csvPath}");
        await File.WriteAllTextAsync(csvPath, sb.ToString(), Encoding.UTF8);

        // —— 应用模式：单事务更新 + 删除（先备份整库）——
        if (mode.Equals("apply", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine();
            Console.WriteLine("==========================================================");
            Console.WriteLine("= APPLY 模式开始执行（先整库备份，再单事务）");
            Console.WriteLine("==========================================================");
            var db = (AppPlatContext)EntityBase.Db;
            var dbFile = db.Database.GetDbConnection().DataSource;
            var backupPath = dbFile + ".bak_merge_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            if (File.Exists(dbFile))
            {
                File.Copy(dbFile, backupPath, true);
                Console.WriteLine($"[备份] {dbFile} → {backupPath}");
            }

            using var tx = await db.Database.BeginTransactionAsync();
            try
            {
                int updatedObjs = 0, deletedUsers = 0;
                foreach (var p in planRows)
                {
                    foreach (var s in p.Sources)
                    {
                        var n = await db.CheckObjects
                            .Where(o => o.CheckerId == s.SrcId)
                            .ExecuteUpdateAsync(set => set.SetProperty(o => o.CheckerId, p.TargetId));
                        if (n > 0)
                        {
                            updatedObjs += n;
                            Console.WriteLine($"  · [{p.BaseName}] CheckObject: {s.SrcName}(Id={s.SrcId}) → {p.TargetName}(Id={p.TargetId}) 更新 {n} 行");
                        }

                        // 合并 UserOrg：避免目标重复
                        if (s.UserOrgCount > 0)
                        {
                            var uos = await db.Set<UserOrg>().Where(uo => uo.UserId == s.SrcId).ToListAsync();
                            foreach (var uo in uos)
                            {
                                var exist = await db.Set<UserOrg>()
                                    .AnyAsync(x => x.UserId == p.TargetId && x.OrgId == uo.OrgId);
                                if (!exist) db.Set<UserOrg>().Add(new UserOrg { UserId = p.TargetId, OrgId = uo.OrgId });
                            }
                            db.Set<UserOrg>().RemoveRange(uos);
                        }

                        // 合并 角色（User 多对多导航）
                        var srcUser = await db.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Id == s.SrcId);
                        var tgtUser = await db.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Id == p.TargetId);
                        if (srcUser != null && tgtUser != null)
                        {
                            foreach (var r in srcUser.Roles.ToList())
                                if (!tgtUser.Roles.Any(x => x.Id == r.Id))
                                    tgtUser.Roles.Add(r);
                        }
                        await db.SaveChangesAsync();
                    }
                }

                // 先软删再硬删，兼容 IDeleteLogic 约束
                foreach (var p in planRows)
                {
                    foreach (var s in p.Sources)
                    {
                        var u = await db.Users.FirstOrDefaultAsync(x => x.Id == s.SrcId);
                        if (u != null) { u.IsDel = true; u.DeleteDt = DateTime.Now; }
                    }
                }
                await db.SaveChangesAsync();
                foreach (var p in planRows)
                {
                    foreach (var s in p.Sources)
                    {
                        var u = await db.Users.FirstOrDefaultAsync(x => x.Id == s.SrcId);
                        if (u != null) { db.Users.Remove(u); deletedUsers++; }
                    }
                }
                await db.SaveChangesAsync();

                await tx.CommitAsync();
                Console.WriteLine();
                Console.WriteLine("==========================================================");
                Console.WriteLine($"= APPLY 完成：更新 {updatedObjs} 条 CheckObject；删除 {deletedUsers} 条 Users 记录；");
                Console.WriteLine($"= 整库备份文件：{backupPath}");
                Console.WriteLine($"= 合并计划 CSV   ：{csvPath}");
                Console.WriteLine("==========================================================");
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                Console.WriteLine($"[APPLY FAILED] 已回滚：{ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }
        else
        {
            Console.WriteLine();
            Console.WriteLine("= 当前为 ANALYZE 模式（只读，未改任何数据）。若确认以上计划无误，执行：");
            Console.WriteLine("    cd App.Consoler && dotnet run -- --run=CheckerUserMergeJob --mode=apply");
            Console.WriteLine("==========================================================");
        }

        static string Csv(string? v) => $"\"{(v ?? "").Replace("\"", "\"\"")}\"";
    }

    static UserLite PickTarget(List<UserLite> list)
    {
        // 1) 优先：无后缀(Base=null) 且 未删除
        var candidates1 = list.Where(v => !v.IsDel && v.Base == null).OrderBy(v => v.Id).ToList();
        if (candidates1.Count > 0) return candidates1[0];

        // 2) 其次：所有 Num=0（无数字后缀或匹配异常但 Num=0）且未删除
        var candidates2 = list.Where(v => !v.IsDel && v.Num == 0).OrderBy(v => v.Id).ToList();
        if (candidates2.Count > 0) return candidates2[0];

        // 3) 兜底：所有未删除里最小 Num 最小 Id
        var candidates3 = list.Where(v => !v.IsDel).OrderBy(v => v.Num).ThenBy(v => v.Id).ToList();
        if (candidates3.Count > 0) return candidates3[0];

        // 4) 最后只能取最小 Id（被删了也没办法）
        return list.OrderBy(v => v.Num).ThenBy(v => v.Id).First();
    }
}

public record UserLite(long Id, string Name, string RealName, string? Base, int Num, bool IsDel);

public class MergePlanRow
{
    public string BaseName { get; set; } = "";
    public long TargetId { get; set; }
    public string TargetName { get; set; } = "";
    public string TargetLoginName { get; set; } = "";
    public List<MergeSrcRow> Sources { get; } = new();
    public int TotalCheckObjects => Sources.Sum(s => s.CheckObjectCount);
}

public class MergeSrcRow
{
    public long SrcId { get; set; }
    public string SrcName { get; set; } = "";
    public string SrcLoginName { get; set; } = "";
    public int Num { get; set; }
    public bool IsDel { get; set; }
    public int CheckObjectCount { get; set; }
    public List<long> CheckObjectSampleIds { get; set; } = new();
    public int UserOrgCount { get; set; }
    public int UserRoleCount { get; set; }
}
