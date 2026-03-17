# AI 编程参考

请阅读各目录下的 README.md 文件，了解项目及功能。

以下是编程要求：

- 数据库
  - 实体类可参考 /app.bll/dal/org.cs 文件
  - 若变更了数据库结构，请运行数据库迁移命令。千万千万不要自动删除重建数据库。

- 界面方案
  - 控件库用ElementPlus，用 RazorPage TagHeper 进行强类型精简封装。
    - 控件库：位于 /App/Components/EleTagHelpers/ 目录，包括表格和表单控件
    - 控件测试窗口：/app/pages/eleui/ 目录类内
    - 表格窗口：请参考 /App/Pages/Maintains/Orgs.cshtml， /App/pages/dev/eletables
    - 表单窗口：请参考 /App/Pages/Maintains/OrgForm.cshtml /App/pages/dev/eleforms
  - 普通界面用 tailswind css 编写样式
  - 响应式布局，支持PC端到手机端端适配

- 前后端数据交互
  - 统一用 APIResult 封装。
  - RazorPageModel 用 BuildResult() 方法封装。参考 /App/pages/maintains/Orgs, OrgForm页面
  - 纯 API 数据接口，位于 /App/Apis/ 目录，返回统一的  APIResult 对象，会序列化为 json 传递给客户端。

- 编译和测试
  - 编辑代码后，若需要重新编译（变更了后端逻辑），请自动执行相关的编译和运行命令。端口不变更
  - 若有需要，请自动化测试，不限于以下方法：
    - curl页面，保存临时文件运行调试
    - 创建或修改自动测试脚本（在目录 /App/WebTest/ 下）进行测试并截图验证
    - 自动化网页测试工具不限于：Puppeteer、Playwright、Chrome DevTools MCP
    

