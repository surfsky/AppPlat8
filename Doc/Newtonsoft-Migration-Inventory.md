# Newtonsoft.Json 使用台账与迁移进度

更新时间：自动扫描后（当前工作区）

## 扫描口径
- 目录：仓库根目录（排除 `.git/bin/obj`）
- 模式：`Newtonsoft.Json|JsonConvert|JObject|JArray|JToken|JsonSerializerSettings|StringEnumConverter|CamelCasePropertyNamesContractResolver|ReferenceLoopHandling`
- 说明：该口径会包含少量“字符串命中”噪声（例如 `JsonStringEnumConverter` 中的 `StringEnumConverter` 子串）。

## 当前统计（迁移第一批后）
- 命中行数：161
- 涉及文件数：55
- 显式 NuGet 依赖（Newtonsoft.Json）：
  - `App.API/HttpApi/HttpApi.csproj`
  - `App.Utils/App.Utils/App.Utils.csproj`

## 已完成第一批迁移（低风险）
- `App.EleUI/EleUI/EleManager.cs`
- `App.EleUI/EleUI/Forms/EleSelectTagHelper.cs`
- `App.EleUI/EleUI/Forms/EleRadioTagHelper.cs`
- `App.EleUI/EleUI/Tables/EleColumnTagHelper.cs`
- `App.EleUI/EleUISamples/Controls/CitySelect.cshtml.cs`

结果：`App.EleUI/**/*.cs` 范围内已无 `Newtonsoft.Json|JsonConvert|JObject|JArray|JToken` 命中。

## 命中 Top（按文件）
1. `App.Utils/App.Utils/Serialization/JsonHelper.cs` (30)
2. `App/Pages/AI/Chat.cshtml.cs` (20)
3. `App.API/HttpApi/HttpApiConfig.cs` (9)
4. `App/Pages/GIS/Index.cshtml.cs` (7)
5. `App/Pages/GIS/GeometryInfo.cshtml.cs` (7)
6. `App.Utils/App.Utils/Base/ConfigurationManager.cs` (7)
7. `App/Pages/Articles/ArticleForm.cshtml.cs` (6)
8. `App/Components/PageModels/BaseModel.cs` (5)
9. `App.Utils/App.Utils/Serialization/JsonConverters.cs` (5)
10. `App.API/HttpApi/Components/SerializeHelper.cs` (5)

## 全量文件清单（按命中数）
- `App.Utils/App.Utils/Serialization/JsonHelper.cs` (30)
- `App/Pages/AI/Chat.cshtml.cs` (20)
- `App.API/HttpApi/HttpApiConfig.cs` (9)
- `App/Pages/GIS/Index.cshtml.cs` (7)
- `App/Pages/GIS/GeometryInfo.cshtml.cs` (7)
- `App.Utils/App.Utils/Base/ConfigurationManager.cs` (7)
- `App/Pages/Articles/ArticleForm.cshtml.cs` (6)
- `App/Components/PageModels/BaseModel.cs` (5)
- `App.Utils/App.Utils/Serialization/JsonConverters.cs` (5)
- `App.API/HttpApi/Components/SerializeHelper.cs` (5)
- `App/Components/DemoData.cs` (4)
- `App/Apis/ApiExtension.cs` (3)
- `App.Utils/App.Utils/Serialization/XmlHelper.cs` (3)
- `App.API/HttpApi/RequestDecoder.cs` (3)
- `App/Pages/OA/CompanyForm.cshtml.cs` (2)
- `App/Pages/OA/Assets.cshtml.cs` (2)
- `App/Pages/OA/AssetForm.cshtml.cs` (2)
- `App.Utils/App.UtilsTests/ConvertorTests.cs` (2)
- `App.Utils/App.Utils/Base/Convertor.Parse.cs` (2)
- `App.API/HttpApi/Components/ReflectHelper.cs` (2)
- `task.txt` (1)
- `App/Startup.cs` (1)
- `App/Properties/PublishProfiles/FTPProfile.pubxml.user` (1)
- `App/Pages/Maintains/MenuForm.cshtml` (1)
- `App/Pages/Checks/CheckTagForm.cshtml` (1)
- `App/Pages/Admins/Users.cshtml` (1)
- `App/Pages/Admins/RolePower.cshtml.cs` (1)
- `App/Pages/Admins/OrgForm.cshtml` (1)
- `App/Apis/Tests/Demo.cs` (1)
- `App/Apis/Tests/Demo.Class.cs` (1)
- `App/Apis/Tests/Demo.Auth.cs` (1)
- `App.Utils/README.md` (1)
- `App.Utils/App.Utils/Serialization/Xmlizer.cs` (1)
- `App.Utils/App.Utils/Base/ListHelper.cs` (1)
- `App.Utils/App.Utils/Base/Convertor.Encoder.cs` (1)
- `App.Utils/App.Utils/Base/Convertor.cs` (1)
- `App.Utils/App.Utils/Attributes/UISetting.cs` (1)
- `App.Utils/App.Utils/Attributes/UIAttribute.cs` (1)
- `App.Utils/App.Utils/App.Utils.csproj` (1)
- `App.EleUI/EleUI/EleManager.cs` (1)
- `App.BLL/App.BLL/Models/Maintains/IPFilter.cs` (1)
- `App.BLL/App.BLL/Models/Articles/ArticleDir.cs` (1)
- `App.BLL/App.BLL/Entities/XUI.cs` (1)
- `App.BLL/App.BLL/Entities/Res.cs` (1)
- `App.BLL/App.BLL/Entities/EntityBaseT.cs` (1)
- `App.BLL/App.BLL/Entities/EntityBase.cs` (1)
- `App.API/HttpApi/HttpApi.csproj` (1)
- `App.API/HttpApi/Components/XmlSerializer.cs` (1)
- `App.API/HttpApi/Components/CacheHelper.cs` (1)
- `App.API/HttpApi.Test/Startup.cs` (1)
- `App.API/HttpApi.Test/API/Person.cs` (1)
- `App.API/HttpApi.Test/API/Demo.Type.cs` (1)
- `App.API/HttpApi.Test/API/Demo.Security.cs` (1)
- `App.API/HttpApi.Test/API/Demo.cs` (1)
- `App.API/HttpApi.Test/API/Demo.Class.cs` (1)

## 后续建议分批
- 批次 B（页面层）：`App/Pages/AI/*`, `App/Pages/GIS/*`, `App/Pages/Articles/*`
- 批次 C（核心库）：`App.Utils` 与 `App.API/HttpApi`
- 批次 D（实体与测试）：`App.BLL`, `App.API/HttpApi.Test`, Razor 页面 `@using Newtonsoft.Json`
- 批次 E（移除包）：先确保 `App.Utils` 与 `HttpApi` 完成替换，再删除两个 csproj 的 PackageReference
