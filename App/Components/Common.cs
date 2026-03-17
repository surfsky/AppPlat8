using System.Reflection;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using App.Utils;
using System.Threading.Tasks;
using App.Web;
using Microsoft.AspNetCore.Authentication;

namespace App.Components
{
    /// <summary>
    /// 公共方法类
    /// </summary>
    public class Common
    {

        /// <summary>获取产品版本</summary>
        public static string GetVersion()
        {
            Version v = Assembly.GetExecutingAssembly().GetName().Version;
            return String.Format("{0}.{1}.{2}", v.Major, v.Minor, v.Build);
        }

        /// <summary>获取数据库连接实例（静态方法）</summary>
        public static DAL.AppPlatContext GetDbConnection()
        {
            //return FineUICore.PageContext.GetRequestService<DAL.AppPlatContext>();
            return Asp.Current.RequestServices.GetService(typeof(DAL.AppPlatContext)) as DAL.AppPlatContext;
        }
    }
}
