		

## AI开发环境

vscode copilot：
    - C# 补全和重构：内置支持（用microsoft xxx 插件）
    - C# 代码调试：内置支持；
    - 20260601更改付费逻辑后，10美元一天就用完了，提高一档要39美元

trae：
    - C# 补全和重构：编程提示需安装ReSharper 插件（个人可免费），重构经常不能完全生效；或其他插件如OmniSharp。
    - C# 代码调试：调试需要安装 C# with NetCoreDbg，
    - lite $3，pro $10, pro+$30, Ultra $200；

Cursor：
    - pro $20每月（500次请求）

OpenAI Codex:

CodeGraphy 代码图谱以减少token消耗
    - npm install -g @colbymchenry/codegraph

## 测试

- 若有需要，请自动化测试，不限于以下方法
- 对于类库方法，创建对应的测试用例并进行单元测试；
- 对于网页：

  - curl页面，保存临时文件运行调试；
  - 创建或修改自动测试脚本（在目录 /AppPlat/WebTest/ 下）进行测试并截图验证
  - 自动化网页测试工具不限于：Puppeteer、Playwright、Microsoft playright-mcp、Chrome DevTools MCP、Selenium
  - 对于有授权的网页(AllowAttribute, AuthAttribute)，若绕不开登录页面，可先关闭页面授权进行测试，测试成功后再开启页面授权。
