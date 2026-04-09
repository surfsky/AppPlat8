# AppPlat 数据/业务层开发流程


## 实体类设计约定

- 所有实体类均继承自 EntityBase<T>，内置了Id生成、创建时间、更新时间、保存、删除、软删除、查询、缓存等功能；
- 树结构实体类可继承自 TreeEntityBase<T>，内置了树结构的构造、缓存等功能；
- 实体类的属性采用驼峰命名法，例如：User、OrderItem, UserOrders、OrderItems 等。
- 实现 IExport 相关接口，用于导出实体类数据，用于给前端导出数据。
- 实现 Search 相关方法，用于查询实体类数据。


## 数据库更新流程

- 创建实体类（位于 `/Models` 目录）。
- 修改 AppPlatContext.cs 中的 DbSet<T> 属性，添加或删除对应实体类的 DbSet。
- 打开终端，切换到解决方案根目录（包含 `.sln` 的目录），执行以下命令：
  ```bash
  dotnet ef migrations add <MigrationName> --project App/App.csproj --startup-project App/App.csproj
  ```
- 如果在开发初期遇到复杂的迁移冲突，可以考虑删除数据库文件（如 `sqlite.db`）和 `Migrations` 文件夹，然后重新生成初始迁移：
    ```bash
    rm App/sqlite.db
    rm -rf App/Migrations
    dotnet ef migrations add InitialCreate --project App/App.csproj --startup-project App/App.csproj
    ```
    重启应用后，系统会自动创建包含所有表的新数据库。
- SQLite 对某些架构更改（如重命名列、修改列类型）的支持有限。EF Core 可能会通过“重建表”的方式来处理这些更改，这可能会导致数据丢失或性能问题。在生产环境中请务必先备份数据。


## 审计字段自动填充与数据过滤

- 在 `AppPlatContext.SaveChanges` 中，新增实体会自动填充审计字段（仅在字段为空时填充，不会覆盖业务显式赋值）：
    - `CreatorId <- scope.UserId`
    - `OwnerId <- scope.UserId`
    - `OrgId <- scope.OrgId`
    - `AuthorId <- scope.UserId`
- 统一数据访问过滤由 `DataAccessFilter` 注入，按 `OrgId` 与 `OwnerId` 进行匹配：
    - 组织范围命中：实体 `OrgId` 在当前授权组织集合内
    - 个人范围命中：实体 `OwnerId == 当前用户Id`
- 说明：`CreatorId` 主要用于审计追踪；后续“我的数据/数据权限”过滤默认基于 `OwnerId`（以及可选的 `OrgId`），而不是 `CreatorId`。



## 表关联的方法

- 1:n 关联

```c#
public class Order : EntityBase<Order>
{
    public string OrderNumber { get; set; }
    public virtual List<OrderItem> Items { get; set; } = new List<OrderItem>();
}
```

- n:m 关联

方案一：

```c#
public class User : EntityBase<User>
{
    public string Name { get; set; }
    public virtual List<Role> Roles { get; set; } = new List<Role>();
}
public class Role : EntityBase<Role>
{
    public string Name { get; set; }
    public virtual List<User> Users { get; set; } = new List<User>();
}
public class AppPlatContext : DbContext
{
    // 若以下代码不写，EF Core 会自动创建关联表 RoleUser（因为r的顺序在u之前）
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<User>()
            .HasMany(u => u.Roles)    // User 有多个 Role
            .WithMany(r => r.Users)   // Role 可以被多个 User 拥有
            .UsingEntity(j => j.ToTable("UserRole")) // 指定连接表的名称为 "UserRole"
            ;
    }
}
```

方案二：

```c#
// 用户实体
public class User
{
    public int Id { get; set; }
    public string Username { get; set; }
    public string Email { get; set; }

    // 导航属性：关联 UserRole（而非直接关联 Role）
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}

// 角色实体（仍仅含 Id/Name）
public class Role
{
    public int Id { get; set; }
    public string Name { get; set; }

    // 导航属性：关联 UserRole
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}

// 核心：手动定义连接表（关联实体）
public class UserRole
{
    public int UserId { get; set; } // 外键：关联 User
    public int RoleId { get; set; } // 外键：关联 Role
    public DateTime CreateTime { get; set; } = DateTime.Now; // 扩展字段：关联时间

    // 导航属性
    public User User { get; set; }
    public Role Role { get; set; }
}

```