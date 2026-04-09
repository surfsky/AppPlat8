# 实体类和菜单预设


以最新代码为准 2026-02-01


============================================================
## 数据模型/基础
============================================================
1.用户管理（User）
    - 字段：ID.Name.Sex.Birthday.任职OrgID
    - 网页：/admins/Users，UserForm

2.组织管理（Org）
    - 字段：ID.父ID.Name
    - 页面：/admins/Orgs, OrgForm

3.用户兼职组织管理（UserOrg）
    - 字段：UserID.OrgID.Title
    - 网页：/admins/UserOrgs，UserOrgForm

4.角色管理（Role）
    - 字段：ID.Name
    - 网页：/admins/Roles，RoleForm， RolePowers
    - 其中权限（Power｜Permission）是枚举
    
5.用户角色管理（UserRole）
    - 字段：UserID.RoleID
    - 网页：/admins/UserRoles，UserRoleForm

6.日志管理（Log）
    - 字段：ID.时间.用户ID.操作类型.操作内容.IP地址.浏览器信息
    - 页面：/maintains/Logs, LogForm

7.在线用户管理（Online）
    - 字段：ID.用户ID.登录时间.IP地址.浏览器信息.登录状态
    - 页面：/maintains/Onlines, OnlineForm


============================================================
数据模型/OA
============================================================
1.公告管理（Announce）
    - 字段：ID.时间.作者.标题.内容.发布时间.状态。用新架构实现论证技术可行性。
    - 页面：/oa/Announces, AnnounceForm

2.厂商管理（Company）
    - 字段：ID.名称.统一社会信用代码.地址.法人.法人联系方式.联系人.联系方式
    - 页面：/oa/Companies, CompanyForm

3.固定资产（Asset）
    - 字段：ID.名称.组织.责任人.位置.厂商.参数.启用时间.过期时间.是否到期提醒
    - 页面：/pages/oa/Assets, AssetForm

4.评论（Comment）
    - 字段：ID.评论内容ID.类别（事件.文档等）.评论人.评论内容.评分
    - 页面：/pages/oa/Comments, CommentForm

5.事件管理（Event）
    - 字段：事件管理：事件ID.时间.类别(EventType).级别.标题.内容.主图.图片列表.组织ID.发布人ID.是否允许评论
    - 页面：/pages/oa/Events, EventForm, EventTypes, EventTypeForm
    - 含评论清单，关联 Comments

6.项目管理（Project）
    - 字段：ID.名称.简称别称.组织.责任人.开发厂商.维护厂商.合同时间.附件.进度百分比.进度历史
    - 页面：/pages/oa/Projects, ProjectForm
    - 项目进度历史子表ProjectLog：项目ID.时间.人员.状态.说明.进度百分比
    - 页面：ProjectLogs, ProjectLogForm

7.预算管理（Budget）
    - 字段：ID.预算年份.组织.预算分类(BudgeType).名称.厂商.关联项目.支付日期.支付状态
    - 页面：/pages/OA/Budges, BudgeForm
    - 含预算类别管理 BudgeType
    - 页面：BudgeTypes, BudgetTypeForm

8.文档库管理（Article）
    - 字段：ID.文档目录ID.名称.内容.附件列表.评论数.是否允许评论
    - 页面：/pages/OA/Articles, ArticleForm, ArticleDirs, ArticleDirForm
    - 含评论清单，关联 Comments
    - 可以容纳词条excel

9.交办任务管理（AssignTask）
    - 字段：ID.父ID.名称.发起人.责任人.责任组织.提交周期.开始日期.最后填报日期.下次填报日期.进度（百分比）.状态（发布.取消.完成）.填报列表
    - 页面：/pages/oa/tasks, taskForm
    - 含任务处理历史（TaskLog）：TaskID, 处理人.处理进度.状态
    - 页面：TaskLogs, TaskLogForm

============================================================
数据模型/驾驶舱
============================================================
1.GIS区域管理（GisRegion）
    - 字段：ID.父ID.区域类别（三角形.椭圆.多边形）.名称.简称.责任组织.编辑者.Json数据
    - 页面：/pages/GIS/Regions, RegionForm

============================================================
数据模型/隐患排查
============================================================
1.检查对象（CheckObject）
    - 模型（App.BLL/DAL/Check/CheckObject)：
    - 字段：
        ID.名称.编码.是否有效.社会信用代码.地址Address.地块号AreaCode.建档日期CreateDt.领域.
        责任组织OrgID.电表号（EleMeeterNum）.员工数.规模（小微.规上.个体）.生产内容.
        类型ObjectType（独立企业.厂中厂.园中园）.外观图片（OutlookImage）.工商执照（LicenseImage）.
        是否录入工业企业在线平台.是否为重点监管企业.是否为示范企业.是否夜间生产.三方安全机构名称.
        技术检查员CheckerID.社区网格员SocialCheckerID, 负责人信息.安全管理员信息.
        内部奖励机制.是否有安全管家.企业标准化创建情况.
        占地面积.建筑面积.厂房使用权类型.房屋结构.是否设置喷淋系统.行业类型.行业风险.是否三场所三企业.是否园中园厂中厂叠加风险.是否涉及电气焊.是否涉及环保设备
        四色等级(绿.黄.橙.红）
    - 页面：/Pages/Check/CheckObjects, CheckObjectForm
    - 子表-联系人表（Contacts）：ID.姓名.照片.证件号.证件照片.联系方式.执证日期.证件过期日期。一个对象包含多个人员。
    - 子表-对象标签表（CheckTag）：ID.父ID.Name。一个对象包含多个标签；
    - 子表-检查项表（CheckItem）：ID.TagID.Name.Order，Level。一个标签对应多个检查项；

2.检查（Check）
    - 字段：检查ID.任务ID.时间.科室.人员.对象ID.检查表ID.检查结果.是否有隐患.隐患是否闭环.过期日期.隐患数.隐患清单
    - 子表：隐患表
    - 网页：/Checks/Checks, CheckForm

3.隐患（CheckHazard）
    - 字段：隐患ID.检查记录ID.检查表项ID.检查表项文本.隐患描述.隐患图片列表.整改状态.整改期限.整改日期.是否录入141系统.检查复查记录
	- 子表：隐患复查记录：隐患ID.记录人ID.时间.说明.图片列表.整改状态
    - 网页：/Check/CheckHarzards, CheckHarzardForm

4.检查任务（CheckTask）
    - 字段：ID.父ID.发布人.名称.过滤条件.检查企业清单.截止时间.进度.接受组织列表.检查表列表.检查项列表
    - 网页：/Check/CheckTasks, CheckTaskForm



