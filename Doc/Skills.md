# AppPlat 项目编程参考

请阅读根目录 README.md 文件，了解目录结构。以下是要求：

- 数据库
  - 实体类可参考 /app.bll/dal/org.cs 文件
  - 若变更了数据库结构，请运行数据库迁移命令。千万千万不要自动删除重建数据库，如果真需要删除数据库请先询问。

- 界面方案
  - 控件库用ElementPlus，用 RazorPage TagHeper 进行强类型精简封装。
    - 控件库：位于 /AppPlat/Components/EleTagHelpers/ 目录，包括表格和表单控件
    - 表格窗口：请参考 /appplat/Pages/Maintains/Orgs.cshtml， /appplat/pages/dev/eletables
    - 表单窗口：请参考 /appplat/Pages/Maintains/OrgForm.cshtml /appplat/pages/dev/eleforms
  - 普通界面用 tailswind css 编写样式
  - 响应式布局，支持PC端到手机端端适配

- 前后端数据交互
  - 统一用 APIResult 封装。
  - 包括 RazorPage 对应的 Model，用 BuildResult()方法封装。参考 /appplat/pages/maintains/Orgs, OrgForm页面
  - 纯 API 数据接口，位于 /AppPlat/Apis/ 目录，返回统一的  APIResult 对象，会序列化为 json 传递给客户端。

- 编译和测试
  - 编辑代码后，若需要重新编译（变更了后端逻辑），请自动执行相关的编译和运行命令。端口不变更
  - 若有需要，请自动化测试，不限于以下方法：
    - curl页面，保存临时文件运行调试
    - 创建或修改自动测试脚本（在目录 /AppPlat/WebTest/ 下）进行测试并截图验证
    - 自动化网页测试工具不限于：Puppeteer、Playwright、Chrome DevTools MCP
    

记住，该系统设计的初衷是符合dotnet程序员的习惯，尽量不要写客户端js代码和css，尽量写语义式代码，复杂的事情交给控件去干，把精力集中用来写业务代码。


## AI Code 八荣八耻

- 以瞎猜接口为耻，以认真查询为荣。
- 以模糊执行为耻，以寻求确认为荣。
- 以臆想业务为耻，以人类确认为荣。
- 以创造接口为耻，以复用现有为荣。
- 以跳过验证为耻，以主动测试为荣。
- 以破坏架构为耻，以遵循规范为荣。
- 以假装理解为耻，以诚实无知为荣。
- 以盲目修改为耻，以谨慎重构为荣。


## SKILLS

已成功从 skills.sh 安装了排名前 20 的 Skills（按安装量排序）：

1. vercel-react-best-practices (Vercel 官方 React 最佳实践)
2. web-design-guidelines (Web 设计指南)
3. remotion-best-practices (Remotion 视频生成最佳实践)
4. frontend-design (前端设计)
5. find-skills (Skill 查找工具)
6. agent-browser (Agent 浏览器能力)
7. skill-creator (Skill 创建工具)
8. seo-audit (SEO 审计)
9. audit-website (网站审计)
10. supabase-postgres-best-practices (Supabase Postgres 最佳实践)
11. building-native-ui (构建原生 UI)
12. copywriting (文案写作)
13. ui-ux-pro-max (UI/UX 增强)
14. better-auth-best-practices (Better Auth 最佳实践)
15. pdf (PDF 处理)
16. marketing-psychology (营销心理学)
17. upgrading-expo (Expo 升级指南)
18. native-data-fetching (原生数据获取)
19. brainstorming (头脑风暴)
20. vercel-composition-patterns (Vercel 组合模式)
