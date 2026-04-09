# DAL 数据访问层

基于 Entity Framework Core 的数据访问层，定义了应用所有数据实体和配置。

- 数据库字段用 PascalCase 方式命名，如 UserId、CreateDt 等。
- 在不影响理解的前提下，尽可能采用短的、常见的单词，如 Manufactory -> Shop。
- 常用缩写表见下文。


## 模块

| 模块       | 说明                                   |
|-----------|----------------------------------------|
| Base      | 基础模块，包含通用实体（如用户、角色、权限等
| Ali       | 阿里巴巴相关服务
| Checks    | 隐患排查
| GIS       | 地理信息、驾驶舱
| OA        | 办公自动化模块
| Configs   | 配置模块
| Open      | 开放接口模块，包含第三方系统对接的实体，如微信、钉钉等
| Maintains | 维护模块
| Workflows | 工作流模块


## 常用缩写及单词

| 缩写      | 全称            |  说明    |
|----------|----------------|------------
| Id       | identify       | 主键
| Uid      | user id        | 如SaleUid, CreateUid
| Dt       | date           | 日期时间
| InUsed   | in used        | 是否在用
| SortId   | sort id        | 排序Id
| Seq      | sequence       | 序列号
| Cert     | centificate    | 认证
| No       | number         | 编号
| Pic      | picture        | 图片统一用Pic
| Tel      | telphone       | 电话
| Remark   | remark         | 备注
| Info     | information    | 信息
| Sts      | status         | 状态
| Log      | history        | 历史记录
|----------|----------------|------------
| WF       | workflow       | 工作流
| Check    | hazard check   | 隐患排查

