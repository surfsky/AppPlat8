using App.DAL;
using System;

namespace App.Components
{
    /// <summary>访问鉴权</summary>
    /// <example>
    /// [Auth(Power.UserView, Power.UserEdit, Power.UserNew, Power.UserDelete)]
    /// [Auth(AuthLogin=true, AuthSign=true)]
    /// public class UserPage : Page {...}
    /// </example>
    public class AuthAttribute : Attribute
    {
        /// <summary>查看权限</summary>
        public Power? ViewPower { get; set; }
        /// <summary>新建权限</summary>
        public Power? NewPower { get; set; }
        /// <summary>编辑权限</summary>
        public Power? EditPower { get; set; }
        /// <summary>删除权限</summary>
        public Power? DeletePower { get; set; }

        /// <summary>校验登陆</summary>
        public bool AuthLogin { get; set; } = false;
        /// <summary>校验URL签名</summary>
        public bool AuthSign { get; set; } = false;

        /// <summary>是否忽略安全检测</summary>
        public bool Ignore { get; set; } = false;

        /// <summary>是否安全（有查看、登录、签名鉴权；或忽略）</summary>
        public bool IsSafe { get; set; }

        public bool CheckSafe()
        {
            IsSafe = ViewPower != null || AuthLogin == true || AuthSign == true || Ignore == true;
            return IsSafe;
        }

        // 构造方法（注：Attribute 构造函数不支持可空类型，只能多写几个构造方法了）
        public AuthAttribute() { }
        public AuthAttribute(bool isSafe) { IsSafe = isSafe; }
        public AuthAttribute(Power viewPower)
        {
            ViewPower = viewPower;
            NewPower = viewPower;
            EditPower = viewPower;
            DeletePower = viewPower;
        }
        public AuthAttribute(Power viewPower, Power newPower, Power editPower, Power deletePower)
        {
            ViewPower = viewPower;
            NewPower = newPower;
            EditPower = editPower;
            DeletePower = deletePower;
        }
    }
}
