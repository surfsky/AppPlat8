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
                    Icon = "",
                    LoginBg = "/res/themes/image_blue_moon/moon.jpg",
                    BeiAnNo = "浙ICP备案XXXXX",
                    Title = "AppPlat",
                    PageSize = 20,
                    Theme = "Cupertino",
                }
            };

        }
        private static List<Menu> GetMenus()
        {
            var menus = new List<Menu> {
                new Menu
                {
                    Name = "系统管理",
                    SortId = 20,
                    Remark = "顶级菜单",
                    ImageUrl = "~/res/icon/folder.png",
                    Children = new List<Menu> {
                        new Menu
                        {
                            Name = "用户",
                            SortId = 10,
                            Remark = "二级菜单",
                            NavigateUrl = "~/Admins/Users",
                            ImageUrl = "~/res/icon/page.png",
                            Power = Power.UserView
                        },
                        new Menu
                        {
                            Name = "组织",
                            SortId = 50,
                            Remark = "二级菜单",
                            NavigateUrl = "~/Admins/OrgUser",
                            ImageUrl = "~/res/icon/page.png",
                            Power = Power.OrgView
                        },
                        new Menu
                        {
                            Name = "角色",
                            SortId = 70,
                            Remark = "二级菜单",
                            NavigateUrl = "~/Admins/RoleUser",
                            ImageUrl = "~/res/icon/page.png",
                            Power = Power.RoleUserEdit
                        },
                        new Menu
                        {
                            Name = "权限",
                            SortId = 90,
                            Remark = "二级菜单",
                            NavigateUrl = "~/Admins/RolePower",
                            ImageUrl = "~/res/icon/page.png",
                            Power = Power.RolePowerEdit
                        },
                        new Menu
                        {
                            Name = "菜单",
                            SortId = 100,
                            Remark = "二级菜单",
                            NavigateUrl = "~/Admins/Menus",
                            ImageUrl = "~/res/icon/page.png",
                            Power = Power.ConfigMenu
                        },
                        new Menu
                        {
                            Name = "在线",
                            SortId = 110,
                            Remark = "二级菜单",
                            NavigateUrl = "~/Admins/Onlines",
                            ImageUrl = "~/res/icon/page.png",
                            Power = Power.MonitorOnline
                        },
                        new Menu
                        {
                            Name = "公告",
                            SortId = 200,
                            Remark = "二级菜单",
                            NavigateUrl = "~/Admins/Announcements",
                            ImageUrl = "~/res/icon/page.png",
                            Power = Power.Web
                        },
                        new Menu
                        {
                            Name = "配置",
                            SortId = 900,
                            Remark = "二级菜单",
                            NavigateUrl = "~/Admins/Config",
                            ImageUrl = "~/res/icon/cog.png",
                            Power = Power.ConfigSite
                        },
                        new Menu
                        {
                            Name = "AI配置",
                            SortId = 910,
                            Remark = "二级菜单",
                            NavigateUrl = "~/AI/AIConfigs",
                            ImageUrl = "~/res/icon/page.png",
                            Power = Power.ConfigAI
                        },
                        new Menu
                        {
                            Name = "AI聊天",
                            SortId = 920,
                            Remark = "二级菜单",
                            NavigateUrl = "~/AI/Chat",
                            ImageUrl = "~/res/icon/page.png",
                            Power = Power.AIChat
                        },
                        new Menu
                        {
                            Name = "日志",
                            SortId = 200,
                            Remark = "二级菜单",
                            NavigateUrl = "~/Admins/Logs",
                            ImageUrl = "~/res/icon/page.png",
                            Power = Power.MonitorLog
                        },
                    }
                },
                new Menu
                {
                    Name = "测试",
                    SortId = 30,
                    ImageUrl = "~/res/icon/folder.png",
                    Visible = false,
                    Children = new List<Menu> {
                        new Menu
                        {
                            Name = "Controls",
                            SortId = 20,
                            ImageUrl = "~/res/icon/folder.png",
                            Children = new List<Menu> {
                                new Menu
                                {
                                    Name = "Button",
                                    NavigateUrl = "~/Tests/Buttons",
                                    ImageUrl = "~/res/icon/page.png",
                                    SortId = 20,
                                },
                                new Menu
                                {
                                    Name = "Grid1",
                                    NavigateUrl = "~/Tests/Grid",
                                    ImageUrl = "~/res/icon/page.png",
                                    SortId = 50,
                                },
                                new Menu
                                {
                                    Name = "Grid2",
                                    NavigateUrl = "~/Tests/DynamicGrid",
                                    ImageUrl = "~/res/icon/page.png",
                                    SortId = 50,
                                },
                                new Menu
                                {
                                    Name = "Responsive",
                                    NavigateUrl = "~/Tests/Responsive",
                                    ImageUrl = "~/res/icon/page.png",
                                    SortId = 40,
                                },
                            }
                        },
                        new Menu
                        {
                            Name = "Blazors",
                            SortId = 60,
                            ImageUrl = "~/res/icon/folder.png",
                            Children = new List<Menu> {
                               new Menu
                                {
                                    Name = "Blazor",
                                    NavigateUrl = "~/Blazors/Index",
                                    ImageUrl = "~/res/icon/page.png",
                                    SortId = 60,
                                }
                            }
                       },
                        new Menu
                        {
                            Name = "Chat",
                            NavigateUrl = "~/Chats/Chat",
                            ImageUrl = "~/res/icon/page.png",
                            SortId = 80,
                        },
                    }
                },
                new Menu
                {
                    Name = "修改密码",
                    SortId = 130,
                    Remark = "二级菜单",
                    NavigateUrl = "~/Admin/ChangePassword",
                    ImageUrl = "~/res/icon/tag_blue.png"
                },
                new Menu
                {
                    Name = "安全退出",
                    SortId = 140,
                    Remark = "二级菜单",
                    NavigateUrl = "~/Logout",
                    ImageUrl = "~/res/icon/door_out.png",
                    Target="_top"
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