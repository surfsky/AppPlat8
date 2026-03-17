- 修改接口：
    - Api -> Api$
    - Apis -> Apis$

- 美化列表和测试页面
    - 改为左右结构（左侧根据 Apis接口，对接口进行分类（根据Scope）并展示接口列表）
    - 右侧展示接口的详细信息（包括参数、返回值、示例代码等）、测试页面
    - 找个Swagger 的参考一下，页面要美观才能吸引人。

- 安全
    - 是否需要登陆后才能查看api清单和页面
    - 集中的事件处理
    HttpApiConfig.Instance.
        .OnVisit()
        .OnEnd()
        .OnAuth()
        .OnBan()
        .OnException()
