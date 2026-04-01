using App.Components;
using App.Utils;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;


namespace App.DAL
{
    /// <summary>
    /// 数据库初始化
    /// https://docs.microsoft.com/zh-cn/aspnet/core/data/ef-rp/intro
    /// </summary>
    public static class AppPlatContextInitializer
    {
        public static void Initialize(AppPlatContext context)
        {
            //context.Database.EnsureCreated();
            context.Database.Migrate();

            // 已经初始化
            if (context.Users.Any())
                return;
            else
            {
                GetSiteConfigs().ForEach(c => c.Save());
                GetOrgs().ForEach(d => d.Save());
                GetUsers().ForEach(u => u.Save());
                GetRoles().ForEach(r => r.Save());
                GetMenus().ForEach(m => m.Save());
            }
        }

        private static List<SiteConfig> GetSiteConfigs()
        {
            return new List<SiteConfig>() {
                new SiteConfig
                {
                    Title = "AppPlat8",
                    BeiAnNo = "浙ICP备XXX号",
                    Icon = "/Files/SiteConfig/2139794460303691776.png",
                    LoginBg = "/Files/SiteConfig/2139794464653185024.png",
                    PageSize = 50,
                    DefaultPassword = "abc@123",
                    UpFileTypes = ".gif, .png, .jpg, .jpeg, .bmp, .mp3, .mp4, .doc, .docx, .xls, .xlsx, .ppt, .pptx, .pdf, .cdr",
                    UpFileSize = 50,
                    MapKey = "pk.xxx"
                }
            };

        }
        private static List<Menu> GetMenus()
        {
            var menus = new List<Menu> {
                new Menu
                {
                    Name = "仪表盘",
                    SortId = 0,
                    NavigateUrl = "Me/Dashboard",
                    ImageUrl = "fas fa-chart-line",
                    Power = (Power?)0,
                },
                new Menu
                {
                    Name = "排查",
                    SortId = 10,
                    Remark = "顶级菜单",
                    ImageUrl = "fas fa-folder",
                    Power = (Power?)0,
                    Expanded = true,
                    Children = new List<Menu> {
                        new Menu
                        {
                            Name = "对象",
                            SortId = 10,
                            NavigateUrl = "Checks/CheckObjects",
                            ImageUrl = "fas fa-university",
                            Power = (Power?)0,
                        },
                        new Menu
                        {
                            Name = "排查",
                            SortId = 20,
                            NavigateUrl = "Checks/CheckLogs",
                            ImageUrl = "fas fa-search",
                            Power = (Power?)321,
                        },
                        new Menu
                        {
                            Name = "标签",
                            SortId = 25,
                            NavigateUrl = "Checks/CheckTags",
                            ImageUrl = "fas fa-flag",
                            Power = (Power?)341,
                            Fixed = false,
                        },
                        new Menu
                        {
                            Name = "检查表",
                            SortId = 30,
                            NavigateUrl = "Checks/CheckSheets",
                            ImageUrl = "fas fa-tachometer-alt",
                            Power = (Power?)341,
                        },
                        new Menu
                        {
                            Name = "隐患",
                            SortId = 40,
                            NavigateUrl = "Checks/CheckHazards",
                            ImageUrl = "fas fa-file-excel",
                            Power = (Power?)331,
                        },
                        new Menu
                        {
                            Name = "任务",
                            SortId = 50,
                            NavigateUrl = "Checks/CheckTasks",
                            ImageUrl = "fas fa-list",
                            Power = (Power?)311,
                        },
                        new Menu
                        {
                            Name = "报表",
                            SortId = 60,
                            NavigateUrl = "Checks/CheckReports",
                            ImageUrl = "fas fa-chart-pie",
                            Power = (Power?)0,
                        },
                    }
                },
                new Menu
                {
                    Name = "OA",
                    SortId = 20,
                    ImageUrl = "fas fa-folder",
                    Power = (Power?)0,
                    Children = new List<Menu> {
                        new Menu
                        {
                            Name = "公告",
                            SortId = 0,
                            NavigateUrl = "OA/Announces",
                            ImageUrl = "fas fa-bullhorn",
                            Power = (Power?)71,
                        },
                        new Menu
                        {
                            Name = "公司",
                            SortId = 0,
                            NavigateUrl = "OA/Companies",
                            ImageUrl = "fas fa-university",
                            Power = (Power?)81,
                        },
                        new Menu
                        {
                            Name = "资产",
                            SortId = 10,
                            NavigateUrl = "OA/Assets",
                            ImageUrl = "fas fa-print",
                            Power = (Power?)201,
                        },
                        new Menu
                        {
                            Name = "预算",
                            SortId = 20,
                            NavigateUrl = "OA/Budgets",
                            ImageUrl = "fas fa-chart-line",
                            Power = (Power?)241,
                        },
                        new Menu
                        {
                            Name = "交办",
                            SortId = 30,
                            NavigateUrl = "Tasks/Tasks",
                            ImageUrl = "fas fa-list-ol",
                            Power = (Power?)261,
                        },
                        new Menu
                        {
                            Name = "项目",
                            SortId = 40,
                            NavigateUrl = "Tasks/Projects",
                            ImageUrl = "fas fa-cubes",
                            Power = (Power?)231,
                        },
                        new Menu
                        {
                            Name = "事件",
                            SortId = 50,
                            NavigateUrl = "Tasks/Events",
                            ImageUrl = "fas fa-edit",
                            Power = (Power?)221,
                        },
                    }
                },
                new Menu
                {
                    Name = "知识库",
                    SortId = 25,
                    ImageUrl = "fas fa-folder",
                    Power = (Power?)0,
                    Fixed = false,
                    Children = new List<Menu> {
                        new Menu
                        {
                            Name = "文档",
                            SortId = 20,
                            NavigateUrl = "Articles/Articles",
                            ImageUrl = "fas fa-file-alt",
                            Power = (Power?)251,
                        },
                        new Menu
                        {
                            Name = "目录",
                            SortId = 30,
                            NavigateUrl = "Articles/ArticleDirs",
                            ImageUrl = "fas fa-boxes",
                            Fixed = false,
                        },
                    }
                },
                new Menu
                {
                    Name = "账户",
                    SortId = 30,
                    Remark = "顶级菜单",
                    ImageUrl = "fas fa-folder",
                    Power = (Power?)0,
                    Children = new List<Menu> {
                        new Menu
                        {
                            Name = "组织",
                            SortId = 0,
                            Remark = "二级菜单",
                            NavigateUrl = "Admins/Orgs",
                            ImageUrl = "fas fa-sitemap",
                            Power = (Power?)61,
                        },
                        new Menu
                        {
                            Name = "用户",
                            SortId = 10,
                            Remark = "二级菜单",
                            NavigateUrl = "Admins/Users",
                            ImageUrl = "fas fa-users",
                            Power = (Power?)51,
                        },
                        new Menu
                        {
                            Name = "权限",
                            SortId = 90,
                            Remark = "二级菜单",
                            NavigateUrl = "Admins/RolePower",
                            ImageUrl = "fas fa-cubes",
                            Power = (Power?)57,
                        },
                    }
                },
                new Menu
                {
                    Name = "运维",
                    SortId = 40,
                    ImageUrl = "fas fa-folder",
                    Power = (Power?)0,
                    Children = new List<Menu> {
                        new Menu
                        {
                            Name = "菜单",
                            SortId = 10,
                            Remark = "二级菜单",
                            NavigateUrl = "Maintains/Menus",
                            ImageUrl = "fas fa-list",
                            Power = (Power?)22,
                        },
                        new Menu
                        {
                            Name = "日志",
                            SortId = 20,
                            Remark = "二级菜单",
                            NavigateUrl = "Maintains/Logs",
                            ImageUrl = "fas fa-check-double",
                            Power = (Power?)31,
                        },
                        new Menu
                        {
                            Name = "站点配置",
                            SortId = 30,
                            Remark = "二级菜单",
                            NavigateUrl = "Maintains/SiteConfigForm",
                            ImageUrl = "fas fa-cog",
                            Power = (Power?)21,
                        },
                        new Menu
                        {
                            Name = "AI配置",
                            SortId = 40,
                            NavigateUrl = "AI/AiConfigs",
                            ImageUrl = "fas fa-rocket",
                            Power = (Power?)24,
                            Fixed = false,
                        },
                    }
                },
                new Menu
                {
                    Name = "开发",
                    SortId = 50,
                    ImageUrl = "fas fa-folder",
                    Power = (Power?)6,
                    Children = new List<Menu> {
                        new Menu
                        {
                            Name = "API",
                            SortId = 0,
                            NavigateUrl = "Dev/API",
                            ImageUrl = "fas fa-link",
                            Power = (Power?)6,
                        },
                        new Menu
                        {
                            Name = "Chat",
                            SortId = 30,
                            NavigateUrl = "Chats/Chat",
                            ImageUrl = "fas fa-cogs",
                        },
                        new Menu
                        {
                            Name = "测试",
                            SortId = 60,
                            ImageUrl = "fas fa-folder",
                            Power = (Power?)6,
                            Children = new List<Menu> {
                                new Menu
                                {
                                    Name = "图标",
                                    SortId = 0,
                                    NavigateUrl = "EleUISamples/Controls/Icons",
                                    ImageUrl = "fas fa-lightbulb",
                                    Power = (Power?)6,
                                    Fixed = false,
                                },
                                new Menu
                                {
                                    Name = "图标2",
                                    SortId = 20,
                                    NavigateUrl = "Dev/IconFas",
                                    ImageUrl = "fas fa-sliders-h",
                                    Power = (Power?)6,
                                },
                                new Menu
                                {
                                    Name = "控件",
                                    SortId = 30,
                                    NavigateUrl = "EleUISamples/index",
                                    ImageUrl = "fas fa-file",
                                    Power = (Power?)6,
                                    Target = "_blank",
                                    Fixed = false,
                                },
                                new Menu
                                {
                                    Name = "Blazor",
                                    SortId = 60,
                                    NavigateUrl = "Blazors/Index",
                                    ImageUrl = "fas fa-file",
                                },
                            }
                        },
                    }
                },
                new Menu
                {
                    Name = "驾驶舱",
                    SortId = 70,
                    ImageUrl = "fas fa-folder",
                    Power = (Power?)0,
                    Children = new List<Menu> {
                        new Menu
                        {
                            Name = "驾驶舱",
                            SortId = 0,
                            NavigateUrl = "GIS/GisIndex",
                            ImageUrl = "fas fa-tachometer-alt",
                            Power = (Power?)0,
                            Target = "_blank",
                        },
                        new Menu
                        {
                            Name = "区域",
                            SortId = 20,
                            NavigateUrl = "GIS/Regions",
                            ImageUrl = "fas fa-project-diagram",
                            Power = (Power?)0,
                        },
                    }
                },
                new Menu
                {
                    Name = "账户",
                    SortId = 130,
                    NavigateUrl = "me/Profile",
                    ImageUrl = "fas fa-user",
                    Power = (Power?)0,
                    Fixed = true,
                },
                new Menu
                {
                    Name = "退出",
                    SortId = 140,
                    NavigateUrl = "Logout",
                    ImageUrl = "fas fa-external-link-alt",
                    Power = (Power?)0,
                    Remark = "二级菜单",
                    Target = "_top",
                    Fixed = true,
                },
            };

            return menus;
        }

        private static List<Role> GetRoles()
        {
            var roles = new List<Role>()
            {
                new Role()
                {
                    Name = "系统管理员",
                    Remark = ""
                },
                new Role()
                {
                    Name = "部门管理员",
                    Remark = ""
                },
                new Role()
                {
                    Name = "开发人员",
                    Remark = ""
                },
            };

            return roles;
        }

        private static List<User> GetUsers()
        {
            string[] USER_NAMES = { "男", "童光喜", "男", "方原柏", "女", "祝春亚", "男", "涂辉", "男", "舒兆国", "男", "熊忠文", "男", "徐吉琳", "男", "方金海", "男", "包卫峰", "女", "靖小燕", "男", "杨习斌", "男", "徐长旺", "男", "聂建雄", "男", "周敦友", "男", "陈友庭", "女", "陆静芳", "男", "袁国柱", "女", "骆新桂", "男", "许治国", "男", "马先加", "男", "赵恢川", "男", "柯常胜", "男", "黄国鹏", "男", "柯尊北", "男", "刘海云", "男", "罗清波", "男", "张业权", "女", "丁溯鋆", "男", "吴俊", "男", "郑江", "男", "李亚华", "男", "石光富", "男", "谭志洪", "男", "胡中生", "男", "董龙剑", "男", "陈红", "男", "汪海平", "男", "彭道洲", "女", "尹莉君", "男", "占耀玲", "男", "付杰", "男", "王红艳", "男", "邝兴", "男", "饶玮", "男", "王方胜", "男", "陈劲松", "男", "邓庆华", "男", "王石林", "男", "胡俊明", "男", "索相龙", "男", "陈海军", "男", "吴文涛", "女", "熊望梅", "女", "段丽华", "女", "胡莎莎", "男", "徐友安", "男", "肖诗涛", "男", "王闯", "男", "余兴龙", "男", "芦荫杰", "男", "丁金富", "男", "谭军令", "女", "鄢旭燕", "男", "田坤", "男", "夏德胜", "男", "喻显发", "男", "马兴宝", "男", "孙学涛", "男", "陶云成", "男", "马远健", "男", "田华", "男", "聂子森", "男", "郑永军", "男", "余昌平", "男", "陶俊华", "男", "李小林", "男", "李荣宝", "男", "梅盈凯", "男", "张元群", "男", "郝新华", "男", "刘红涛", "男", "向志强", "男", "伍小峰", "男", "胡勇民", "男", "黄定祥", "女", "高红香", "男", "刘军", "男", "叶松", "男", "易俊林", "男", "张威", "男", "刘卫华", "男", "李浩", "男", "李寿庚", "男", "涂洋", "男", "曹晶", "男", "陈辉", "女", "彭博", "男", "严雪冰", "男", "刘青", "女", "印媛", "男", "吴道雄", "男", "邓旻", "男", "陈骏", "男", "崔波", "男", "韩静颐", "男", "严安勇", "男", "刘攀", "女", "刘艳", "女", "孙昕", "女", "郑新", "女", "徐睿", "女", "李月杰", "男", "吕焱鑫", "女", "刘沈", "男", "朱绍军", "女", "马茜", "女", "唐蕾", "女", "刘姣", "女", "于芳", "男", "吴健", "女", "张丹梅", "女", "王燕", "女", "贾兆梅", "男", "程柏漠", "男", "程辉", "女", "任明慧", "女", "焦莹", "女", "马淑娟", "男", "徐涛", "男", "孙庆国", "男", "刘胜", "女", "傅广凤", "男", "袁弘", "男", "高令旭", "男", "栾树权", "女", "申霞", "女", "韩文萍", "女", "隋艳", "男", "邢海洲", "女", "王宁", "女", "陈晶", "女", "吕翠", "女", "刘少敏", "女", "刘少君", "男", "孔鹏", "女", "张冰", "女", "王芳", "男", "万世忠", "女", "徐凡", "女", "张玉梅", "女", "何莉", "女", "时会云", "女", "王玉杰", "女", "谭素英", "女", "李艳红", "女", "刘素莉", "男", "王旭海", "女", "安丽梅", "女", "姚露", "女", "贾颖", "女", "曹微", "男", "黄经华", "女", "陈玉华", "女", "姜媛", "女", "魏立平", "女", "张萍", "男", "来辉", "女", "陈秀玫", "男", "石岩", "男", "王洪捍", "男", "张树军", "女", "李亚琴", "女", "王凤", "女", "王珊华", "女", "杨丹丹", "女", "教黎明", "女", "修晶", "女", "丁晓霞", "女", "张丽", "女", "郭素兰", "女", "徐艳丽", "女", "任子英", "女", "胡雁", "女", "彭洪亮", "女", "高玉珍", "女", "王玉姝", "男", "郑伟", "女", "姜春玲", "女", "张伟", "女", "王颖", "女", "金萍", "男", "孙望", "男", "闫宝东", "男", "周相永", "女", "杨美娜", "女", "欧立新", "女", "刘宝霞", "女", "刘艳杰", "女", "宋艳平", "男", "李克", "女", "梁翠", "女", "宗宏伟", "女", "刘国伟", "女", "敖志敏", "女", "尹玲" };
            string[] EMAIL_NAMES = { "qq.com", "gmail.com", "163.com", "126.com", "outlook.com", "foxmail.com" };

            var users = new List<User>();
            var rdm = new Random();

            for (int i = 0, count = USER_NAMES.Length; i < count; i += 2)
            {
                string gender = USER_NAMES[i];
                string chineseName = USER_NAMES[i + 1];
                string userName = "user" + i.ToString();

                users.Add(new User
                {
                    Name = userName,
                    Gender = gender,
                    Password = PasswordUtil.CreateDbPassword(userName),
                    RealName = chineseName,
                    Email = userName + "@" + EMAIL_NAMES[rdm.Next(0, EMAIL_NAMES.Length)],
                    InUsed = true,
                    CreateDt = DateTime.Now
                });
            }

            // 添加超级管理员
            users.Add(new User
            {
                Name = "admin",
                Gender = "男",
                Password = PasswordUtil.CreateDbPassword("admin"),
                RealName = "超级管理员",
                Email = "admin@189.com",
                InUsed = true,
                CreateDt = DateTime.Now
            });

            return users;
        }

        private static List<Org> GetOrgs()
        {
            var items = new List<Org> {
                new Org
                {
                    Name = "研发部",
                    SortId = 1,
                    Remark = "顶级部门",
                    Children = new List<Org> {
                        new Org
                        {
                            Name = "开发部",
                            SortId = 1,
                            Remark = "二级部门"
                        },
                        new Org
                        {
                            Name = "测试部",
                            SortId = 2,
                            Remark = "二级部门"
                        }
                    }
                },
                new Org
                {
                    Name = "销售部",
                    SortId = 2,
                    Remark = "顶级部门",
                    Children = new List<Org> {
                        new Org
                        {
                            Name = "直销部",
                            SortId = 1,
                            Remark = "二级部门"
                        },
                        new Org
                        {
                            Name = "渠道部",
                            SortId = 2,
                            Remark = "二级部门"
                        }
                    }
                },
                new Org
                {
                    Name = "财务部",
                    SortId = 4,
                    Remark = "顶级部门"
                },
            };

            return items;
        }

    }
}