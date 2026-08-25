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
using System.Threading;
using System.Threading.Tasks;

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
        public DbSet<UserOrg> UserOrgs { get; set; }
        public DbSet<Role> Roles { get; set; }
        //public DbSet<RoleUser> RoleUsers { get; set; }
        public DbSet<RolePower> RolePowers { get; set; }
        public DbSet<RoleMenu> RoleMenus { get; set; }
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
        public DbSet<Site> Sites { get; set; }

        // maintains
        public DbSet<Log> Logs { get; set; }
        public DbSet<VerifyCode> VerifyCodes { get; set; }
        public DbSet<Feedback> Feedbacks { get; set; }
        public DbSet<Online> Onlines { get; set; }
        public DbSet<IPFilter> IPFilters { get; set; }
        public DbSet<Message> Messages { get; set; }


        // GIS 和 驾驶舱
        public DbSet<App.DAL.GIS.GisMenu> GisMenus { get; set; }
        public DbSet<App.DAL.GIS.RoleGisMenu> RoleGisMenus { get; set; }
        public DbSet<App.DAL.GIS.GisGeometry> GisGeometries { get; set; }
        public DbSet<App.DAL.GIS.GisApi> GisApis { get; set; }
        public DbSet<App.DAL.GIS.GisPanel> GisPanels { get; set; }
        public DbSet<App.DAL.GIS.GisScene> GisScenes { get; set; }
        public DbSet<App.DAL.GIS.GisSceneMenu> GisSceneMenus { get; set; }
        public DbSet<App.DAL.GIS.GisScenePanel> GisScenePanels { get; set; }
        public DbSet<App.DAL.GIS.GisSceneLayer> GisSceneLayers { get; set; }
        public DbSet<App.DAL.GIS.GisTyphoon> GisTyphoons { get; set; }
        public DbSet<App.DAL.GIS.GisTyphoonLog> GisTyphoonLogs { get; set; }


        // Check 隐患排查
        public DbSet<CheckObject> CheckObjects { get; set; }
        public DbSet<CheckSheet> CheckSheets { get; set; }
        public DbSet<CheckSheetItem> CheckSheetItems { get; set; }
        public DbSet<CheckTag> CheckTags { get; set; }
        public DbSet<CheckObjectContact> CheckObjectContacts { get; set; }
        public DbSet<CheckObjectEvent> CheckObjectEvents { get; set; }
        public DbSet<CheckObjectTag> CheckObjectTags { get; set; }
        public DbSet<Check> Checks { get; set; }
        public DbSet<CheckHazard> CheckHazards { get; set; }
        public DbSet<CheckHazardLog> CheckHazardLogs { get; set; }
        public DbSet<CheckTask> CheckTasks { get; set; }
        public DbSet<CheckTaskObject> CheckTaskObjects {get;set;}
        public DbSet<CheckTaskOrg> CheckTaskOrgs {get;set;}
        public DbSet<CheckTaskSheet> CheckTaskSheets {get;set;}
        public DbSet<CheckPoint> CheckPoints { get; set; }



        // CMS 内容管理
        public DbSet<Article> Articles { get; set; }
        public DbSet<ArticleMenu> ArticleDirs { get; set; }
        public DbSet<Comment> Comments { get; set; }

        // KB 知识库
        public DbSet<KbMenu> KbMenus { get; set; }



        // 任务、项目、事件记录管理
        public DbSet<Project> Projects { get; set; }
        public DbSet<ProjectLog> ProjectLogs { get; set; }
        public DbSet<AssignTask> AssignTasks { get; set; }
        public DbSet<AssignTaskLog> AssignTaskLogs { get; set; }
        public DbSet<Event> Events { get; set; }
        public DbSet<EventType> EventTypes { get; set; }

        // OA
        public DbSet<Announce> Announces { get; set; }

        // 财务
        public DbSet<Asset> Assets { get; set; }
        public DbSet<Budget> Budgets { get; set; }
        public DbSet<BudgetType> BudgetTypes { get; set; }

        // CRM
        public DbSet<ContactMenu> ContactMenus { get; set; }
        public DbSet<Contact> Contacts { get; set; }
        public DbSet<RoleContactMenu> RoleContactMenus { get; set; }


        //---------------------------------------------------
        // 构造函数和配置
        //---------------------------------------------------
        public AppPlatContext(DbContextOptions<AppPlatContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 统一配置 Creator 导航属性与 CreatorId 的关系：
            // 之前 Gis* 实体和 Att 实体都有 public virtual User Creator { get; set; }，
            // 但没显式配 HasForeignKey，导致 migrations add 时报：
            // "Unable to determine the relationship represented by navigation 'Att.Creator' of type 'User'"
            // 下面通过反射找出所有继承 EntityBase、且同时存在 Creator（User 类型）+ CreatorId（long? 类型）属性的实体，
            // 统一配成：HasOne(e => e.Creator).WithMany().HasForeignKey(e => e.CreatorId).OnDelete(DeleteBehavior.Restrict)
            ConfigureCreatorNavigation(modelBuilder);

            // User/Role 多对多关系
            modelBuilder.Entity<User>()
                .HasMany(u => u.Roles)                   // User 有多个 Role
                .WithMany(r => r.Users)                  // Role 可以被多个 User 拥有
                .UsingEntity(j => j.ToTable("UserRole")) // 指定连接表的名称为 "UserRole"
                ;

            // User/UserOrg 多对多（兼职/授权组织）
            modelBuilder.Entity<User>()
                .HasMany(u => u.UserOrgs)
                .WithOne(o => o.User)
                .HasForeignKey(o => o.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserOrg>()
                .HasOne(o => o.Org)
                .WithMany()
                .HasForeignKey(o => o.OrgId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<KbMenu>()
                .HasOne(o => o.Org)
                .WithMany()
                .HasForeignKey(o => o.OrgId)
                .OnDelete(DeleteBehavior.Restrict);

            // CheckTag/CheckSheet 多对多关系
            modelBuilder.Entity<CheckTag>()
                .HasMany(t => t.Sheets)                        // CheckTag 对应到多个 CheckSheet
                .WithMany(s => s.Tags)                         // CheckSheet 对应到多个 CheckTag
                .UsingEntity(j => j.ToTable("CheckTagSheet"))  // 指定关联表名称为 CheckTagSheet
                ;

            // Check/CheckTask 多对多关系
            modelBuilder.Entity<Check>()
                .HasMany(t => t.Tasks)
                .WithMany(s => s.Checks)
                .UsingEntity<Dictionary<string, object>>(
                    "CheckTaskCheck",
                    j => j.HasOne<CheckTask>().WithMany().HasForeignKey("TaskId").OnDelete(DeleteBehavior.Cascade),
                    j => j.HasOne<Check>().WithMany().HasForeignKey("CheckId").OnDelete(DeleteBehavior.Cascade),
                    j =>
                    {
                        j.ToTable("CheckTaskCheck");
                        j.HasKey("CheckId", "TaskId");
                    })
                ;

            MapListIds(modelBuilder);
        }

        private static void ConfigureCreatorNavigation(ModelBuilder modelBuilder)
        {
            var entityBaseType = typeof(EntityBase);
            var userType = typeof(User);
            var nullableLongType = typeof(long?);

            // 从当前已加载到 modelBuilder 的实体里挑（AppPlatContext 会自动注册所有有 DbSet 及被关系链条带到的实体）
            foreach (var et in modelBuilder.Model.GetEntityTypes())
            {
                var clrType = et.ClrType;
                if (clrType == null) continue;
                if (!entityBaseType.IsAssignableFrom(clrType)) continue;

                // 有没有 public User Creator 属性（含父类继承的 virtual）
                var creatorProp = clrType.GetProperty("Creator", BindingFlags.Instance | BindingFlags.Public);
                if (creatorProp == null || creatorProp.PropertyType != userType) continue;

                // 有没有 CreatorId 属性（long?）
                var creatorIdProp = clrType.GetProperty("CreatorId", BindingFlags.Instance | BindingFlags.Public);
                if (creatorIdProp == null || (creatorIdProp.PropertyType != nullableLongType && creatorIdProp.PropertyType != typeof(long))) continue;

                try
                {
                    // 用字符串 API 弱类型配置 HasOne(Creator, User) → WithMany() → HasForeignKey(CreatorId) → OnDelete Restrict
                    var entBuilder = modelBuilder.Entity(clrType);

                    // 调用：EntityTypeBuilder.HasOne(string navigationName, Type relatedEntityType)
                    var refBuilder = entBuilder.HasOne(userType, "Creator");

                    // 调用：ReferenceCollectionBuilder.WithMany()
                    var withMany = refBuilder.WithMany();

                    // 调用：ReferenceCollectionBuilder.HasForeignKey(string foreignKeyPropertyName)
                    var fk = withMany.HasForeignKey("CreatorId");

                    // 调用：ReferenceCollectionBuilder.OnDelete(DeleteBehavior)
                    fk.OnDelete(DeleteBehavior.Restrict);
                }
                catch
                {
                    // 如果实体已经显式用 [ForeignKey] 或自定义覆盖过就跳过
                }
            }
        }

        public override int SaveChanges()
        {
            ApplyAuditValues();
            return base.SaveChanges();
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            ApplyAuditValues();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            ApplyAuditValues();
            return base.SaveChangesAsync(cancellationToken);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            ApplyAuditValues();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        private void ApplyAuditValues()
        {
            var now = DateTime.Now;
            var scope = EntityConfig.DataAuditScope;
            var hasScope = scope != null && scope.Enabled;

            foreach (var entry in ChangeTracker.Entries<EntityBase>())
            {
                if (entry.State == EntityState.Added)
                {
                    if (!entry.Entity.CreateDt.HasValue)
                        entry.Entity.CreateDt = now;

                    if (hasScope)
                    {
                        // 新增数据默认记录创建人/责任人/组织/作者，若业务已显式赋值则不覆盖。
                        SetNullableLongIfEmpty(entry, nameof(EntityBase.CreatorId), scope.UserId);
                        SetNullableLongIfEmpty(entry, nameof(EntityBase.OwnerId), scope.UserId);
                        SetNullableLongIfEmpty(entry, "OrgId", scope.OrgId);
                        SetNullableLongIfEmpty(entry, "AuthorId", scope.UserId);
                    }
                }

                if (entry.State == EntityState.Modified)
                {
                    entry.Entity.UpdateDt = now;
                }
            }
        }

        private static void SetNullableLongIfEmpty(EntityEntry entry, string propertyName, long? value)
        {
            if (!value.HasValue)
                return;

            var prop = entry.Properties.FirstOrDefault(t => t.Metadata.Name == propertyName);
            if (prop == null)
                return;

            var clrType = prop.Metadata.ClrType;
            if (clrType != typeof(long) && clrType != typeof(long?))
                return;

            if (prop.CurrentValue == null)
            {
                prop.CurrentValue = value.Value;
                return;
            }

            if (prop.CurrentValue is long longValue && longValue == 0)
                prop.CurrentValue = value.Value;
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
