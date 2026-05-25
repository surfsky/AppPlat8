# 控制台项目


这是一个用于执行后台任务的控制台项目，目前包含以下功能：

- 定时任务
    - 每日X点，执行一次检查对象任务（更新对象的检查状态 IsChecked，调用 CheckObject.FixAll() 方法），类名 CheckObjectStatusFixJob
    - 每天X点，找一个GPS坐标为空的检查对象（CheckObject），根据地址查询高德地图接口，更新对象的位置信息, 类名 CheckObjectGpsFixJob
    - 每天X点，遍历一次接口清单，更新接口状态、数据条数等信息，并更新GisMenu统计值, 类名 ApiCheckJob
    - 每日X点，执行报表统计任务（尚未实现）, 类名 StatJob
    - ...

- 任务、调度、数据库连接、目录等信息都在 Program.cs 中定义和配置好

- 用户可临时执行某个任务，如
    App.Consoler --run=CheckObjectStatusFixJob

