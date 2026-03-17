# App.Entities EF 基础类库

这是一个围绕 Entity Framework Core 封装的基础类库，提供常用实体操作、帮助方法以及若干通用实体，旨在加速业务层开发。

## 核心功能

- 快速生成和操作实体类，内置 `EntityBase<T>` 通用基类
- 支持逻辑删除与物理删除，并在物理删除时进行级联资源/历史清理
- 内建树形结构处理（`ITree` 接口及辅助方法）
- 提供 SnowflakeId 生成和可选的自定义主键策略
- 自动维护 `CreateDt`/`UpdateDt` 时间戳
- 可导出实体数据为不同级别视图 Export()
- 常用 IQueryable 扩展（分页、排序、动态 Where 等）
- 数据库上下文配置抽象（`EntityConfig`）
- 多种辅助接口与类：日志、缓存、并发检测、序号等

## 使用说明

1. 在应用启动时设置数据库上下文：
   ```csharp
   EntityConfig.Instance.OnGetDb = () => new AppPlatContext();
   ```
2. 业务实体继承 `EntityBase<T>` 并在 `AppPlatContext` 中注册。
3. 常用操作示例：
   ```csharp    
   // New and save
   var u = new User();
   u.Name = "Kevin";
   u.Save();
   
   // Get and modify
   var user = User.Get(5);
   var user = User.Get(t => t.UserId == 5);
   user.Age = 20;
   user.Save();
   
   // Search and Bind
   var users = User.Search(t => t.Name.Contains("Kevin")).ToList();
   var data = users.SortAndPage(t => t.Name, true, 0, 50);
   DataGrid1.DataSource = data;
   DataGrid1.DataBind();

   // export json
   var data = users.SortPageExport(pi);
   return data;
   
   // transaction
   using (var transaction = AppContext.Current.Database.BeginTransaction())
   {
      try
      {
         var orderItem = new OrderItem(....);
         var order = new Order(....);
         orderItem.Save();
         order.Save();
         transaction.Commit();
      }
      catch
      {
         transaction.Rollback();
      }
   }

   // Tree Entity
   public class Org : TreeEntity<Org>
   {
      ...
   }
   var orgTree = Org.Tree;  // use tree cache
   var org = Org.Get(1);
   org.Name = 'NewOrg';
   org.Save();
   Org.ClearCache();  // clear tree cache
   ```


## 配置与通用类型

| 文件 | 说明 |
|------|------|
| `EntityConfig.cs` | 单例配置类，持有获取当前 `DbContext` 的委托。 |
| `Interfaces.cs` | 定义若干实体相关接口（`ILogChange`, `IExport`, `IDeleteLogic`, `ITree` 等）。 |
| `DbHelper.cs` | `DbCommand` 扩展，用于生成带参数的 SQL 字符串，方便调试。 |
| `PagingInfo.cs` | 分页参数和返回结构，用于列表查询。 |
| `EntityHelper.cs` | 一些静态工具方法，例如树形节点递归。 |
| `EntityBase.cs`、`EntityBaseT.cs` | 泛型基类，含主键、时间戳、CRUD、导出、日志、UI 配置钩子等。 |
| `History.cs` | 处理历史记录表，带搜索、添加和批量删除方法。 |
| `Res.cs` | 附属资源（文件/图片）实体，包含文件元数据和批量删除逻辑。 |
| `XState.cs` | 可用于存储系统状态或枚举配置的键值表。 |
| `XUI.cs` | UI 配置实体，支持缓存并解析 JSON 配置。 |
| `StatItem.cs` | 简单的统计数据项类，用于报表或图表。 |
| `EFHelper.cs` | 为 `IQueryable<T>` 提供分页、排序、动态过滤、布尔组合表达式等扩展方法。 |
| `TreeHelper.cs` | 树结构操作，例如设置层级、叶子标志等。 |


## 常见接口说明

| 接口 | 用途 |
|------|------|
| `ILogChange` | 包含 `CreateDt`/`UpdateDt`，用于自动记录操作时间。 |
| `IExport` | 支持数据导出方法，实体实现可返回自定义对象。 |
| `IDeleteLogic` | 标识启用逻辑删除的实体，包含 `InUsed` 字段。 |
| `ICollsionDetect` | 并发冲突检测，通过 `CollisionId` 实现。 |
| `ISort` | 包含排序索引 `SortId`。 |
| `ITree` / `ITree<T>` | 支持树结构实体，含父子关系与层级属性。 |
| `IFix` / `IInit` | 数据修正和初始化接口，实体可在系统启动时调用。 |
| `ICacheAll` | 标记接口，被标记类使用缓存获取所有数据。 |

## 扩展与注意事项

- 若需在删除时做级联操作，可重写 `EntityBase.OnDeleteReference` 或 `AfterChange`。
- 导出逻辑可通过覆盖 `Export` 方法自定义字段集合。
- 使用 `XUI` 时，配置 JSON 保存在 `SettingText`，`Parse()` 后生成 `UISetting` 对象。
- `EFHelper` 中的动态排序/过滤依赖字符串名称，请谨慎使用以避免运行时错误。

此 README 可作为开发人员阅读实体库时的快速参考，后续功能扩展请同步更新。


