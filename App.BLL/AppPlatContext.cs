using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using App.DAL;
using App.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;
using App.DAL.OA;
using System.Reflection;
using System.ComponentModel.DataAnnotations.Schema;

namespace App.DAL
{
    /// <summary>
    /// 数据库上下文
    /// </summary>
    public class AppPlatContext : DbContext
    {
        //---------------------------------------------------
        // 数据表
        //---------------------------------------------------
        // base
        public DbSet<Org> Orgs { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        //public DbSet<RoleUser> RoleUsers { get; set; }
        public DbSet<RolePower> RolePowers { get; set; }
        public DbSet<History> Histories { get; set; }
        public DbSet<Att> Atts { get; set; }

        // configs
        public DbSet<Menu> Menus { get; set; }
        public DbSet<Sequence> Sequences { get; set; }
        public DbSet<SiteConfig> SiteConfigs { get; set; }
        public DbSet<AIConfig> AIConfigs { get; set; }
        public DbSet<AliSmsConfig> AliSmsConfigs { get; set; }

        // open
        public DbSet<Application> Applications {get; set; }

        // maintains
        public DbSet<Log> Logs { get; set; }
        public DbSet<VerifyCode> VerifyCodes { get; set; }
        public DbSet<Feedback> Feedbacks { get; set; }
        public DbSet<Online> Onlines { get; set; }
        public DbSet<IPFilter> IPFilters { get; set; }
        public DbSet<Message> Messages { get; set; }


        // GIS 和 驾驶舱
        public DbSet<App.DAL.GIS.GisRegion> GisRegions { get; set; }


        // Check 隐患排查
        public DbSet<CheckObject> CheckObjects { get; set; }
        public DbSet<CheckObjectContact> CheckObjectContacts { get; set; }
        public DbSet<CheckObjectTag> ObjectTags { get; set; }
        public DbSet<Check> Checks { get; set; }
        public DbSet<CheckHazard> CheckHazards { get; set; }
        public DbSet<CheckHazardLog> CheckHazardLogs { get; set; }
        public DbSet<CheckTask> CheckTasks { get; set; }
        public DbSet<CheckTaskObject> CheckTaskObjects {get;set;}
        public DbSet<CheckTaskOrg> CheckTaskOrgs {get;set;}
        public DbSet<CheckTaskSheet> CheckTaskSheets {get;set;}

        // CheckSheet 检查表
        public DbSet<CheckSheet> CheckSheets { get; set; }
        public DbSet<CheckSheetItem> CheckSheetItems { get; set; }
        public DbSet<CheckTag> CheckTags { get; set; }


        // Article 知识库
        public DbSet<Article> Articles { get; set; }
        public DbSet<ArticleDir> ArticleDirs { get; set; }

        // 任务、项目、事件记录管理
        public DbSet<Project> Projects { get; set; }
        public DbSet<ProjectLog> ProjectLogs { get; set; }
        public DbSet<AssignTask> AssignTasks { get; set; }
        public DbSet<AssignTaskLog> AssignTaskLogs { get; set; }
        public DbSet<Event> Events { get; set; }
        public DbSet<EventType> EventTypes { get; set; }

        // OA
        public DbSet<Announce> Announces { get; set; }
        public DbSet<Company> Companies { get; set; }
        public DbSet<Asset> Assets { get; set; }
        public DbSet<Comment> Comments { get; set; }
        public DbSet<Budget> Budgets { get; set; }
        public DbSet<BudgetType> BudgetTypes { get; set; }


        //---------------------------------------------------
        // 构造函数和配置
        //---------------------------------------------------
        public AppPlatContext(DbContextOptions<AppPlatContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User/Role 多对多关系
            modelBuilder.Entity<User>()
                .HasMany(u => u.Roles)                   // User 有多个 Role
                .WithMany(r => r.Users)                  // Role 可以被多个 User 拥有
                .UsingEntity(j => j.ToTable("UserRole")) // 指定连接表的名称为 "UserRole"
                ;

            // CheckTag/CheckSheet 多对多关系
            modelBuilder.Entity<CheckTag>()
                .HasMany(t => t.Sheets)                        // CheckTag 对应到多个 CheckSheet
                .WithMany(s => s.Tags)                         // CheckSheet 对应到多个 CheckTag
                .UsingEntity(j => j.ToTable("CheckTagSheet"))  // 指定关联表名称为 CheckTagSheet
                ;

            MapListIds(modelBuilder);
        }

        /// <summary>将 List<long> 或 List<int> 类型的属性映射为字符串存储</summary>
        private static void MapListIds(ModelBuilder modelBuilder)
        {
            // List<long> -> string 转换
            var converter = new ValueConverter<List<long>, string>(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                v => string.IsNullOrWhiteSpace(v)
                    ? new List<long>()
                    : JsonSerializer.Deserialize<List<long>>(v, (JsonSerializerOptions)null) ?? new List<long>());

            // List<long> 比较器
            var comparer = new ValueComparer<List<long>>(
                (a, b) => (a ?? new()).SequenceEqual(b ?? new()),
                v => v.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
                v => v.ToList());

            // 将数据库中的 List<long> 和 List<int> 转换为字符串存储
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                var clrType = entityType.ClrType;
                if (clrType == null) continue;
                var listLongProps = entityType
                    .GetProperties()
                    .Where(p => p.ClrType == typeof(List<long>) || p.ClrType == typeof(List<int>))
                    .Where(p =>
                    {
                        var pi = p.PropertyInfo;
                        if (pi == null) return false;

                        // 过滤 [NotMapped]
                        if (pi.GetCustomAttribute<NotMappedAttribute>() != null)
                            return false;

                        // 过滤 virtual 属性
                        var getter = pi.GetMethod;
                        if (getter != null && getter.IsVirtual && !getter.IsFinal)
                            return false;

                        return true;
                    })
                    .ToList();

                foreach (var prop in listLongProps)
                {
                    modelBuilder.Entity(clrType)
                        .Property(prop.Name)
                        .HasConversion(converter)
                        .Metadata.SetValueComparer(comparer);
                }
            }
        }
    }
}