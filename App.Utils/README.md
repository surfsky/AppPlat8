# DotNet 基础类库（net8）


## 功能

- Base
- ComponentModel
- Drawing
- Interop
- IO
- Math
- Net
- Reflections
- Serialization
- Threads


## 使用

```
public void ConfigureServices(IServiceCollection services)
{
    services.AddNewtonsoftJson();       // 支持 NewtonsoftJson
}
```




## History

8.0.0.*

- 迁移到 net8
- 修正了一些跨平台文件路径问题

6.0.0.*

* 重命名版本，前三位与支持的类库同名，最后一位为修正版本号
* 修订 RegexHelper，增加若干 FindXXX(), MoveXXX() 方法，并将网页解析部分拆分为 partial 类
* 修订 ListHelper，增加 ToJoinString() 方法
* 升级 Newtonsoft.Json 版本到
+ 增加 MathHelper.AESEncrypt(), AESEncrypt(), SHA256()
+ 增加 SNGenerator 30字符序列号生成类


3.0.2
    *IO.WriteFile
    +IO.ReadFileText
    +IO.ReadFileBytes

3.0.1
    Add Reflector.ExpressOf()

3.0.0
    迁移到 netstandard, 并拆分为 App.Utils 和 App.Web 两个项目
    去除功能
        /NetHelper.GetServerOrNetworkImage
        /TypeBuilder
        /ExcelHelper 拆分为 ExcelHelper（项目App.Utils） 和 ExcelExporter（项目 App.Web）
    重构Cache
        重构 IO.Cache，支持 null 值
        新增 Cacher 类
    修正 JsEvaluator, CsEvaluate, PinYin

