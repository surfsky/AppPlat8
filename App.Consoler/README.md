# 控制台项目


这是一个用于执行后台任务的控制台项目，目前包含以下功能：

- 定时任务
    - 每日X点，执行一次检查对象任务（更新对象的检查状态 IsChecked，调用 CheckObject.FixAll() 方法），类名 CheckObjectStatusFixJob
    - 每天X点，找一个GPS坐标为空的检查对象（CheckObject），根据地址查询高德地图接口，更新对象的位置信息, 类名 CheckObjectGpsFixJob
    - 每天X点，遍历一次接口清单，更新接口状态、数据条数等信息，并更新GisMenu统计值, 类名 ApiCheckJob
    - 每日X点，执行报表统计任务（尚未实现）, 类名 StatJob
    - ...

- 运行参数由 ConsolerConfig 统一承载；其中数据库连接串 `conn` 仅从 `App.Consoler.Config.json` 读取，其它参数可由命令行覆盖
- 任务调度定义在 Program.cs 中；API 地址、数据库连接、高德 Key 等可写入 App.Consoler.Config.json

- 用户可临时执行某个任务，如
    dotnet App.Consoler.dll --run=CheckObjectStatusFixJob


- 在源码目录下运行：
```
dotnet run
```

- 运行已发布的程序集（与主站点同目录发布）：
```
dotnet App.Consoler.dll
```
