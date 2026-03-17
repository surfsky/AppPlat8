using App.Entities;
using App.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Z.EntityFramework.Plus;

namespace App.DAL
{
    /// <summary>短信验证码</summary>
    [UI("基础", "短信验证码")]
    public class VerifyCode : EntityBase<VerifyCode>
    {
        [UI("验证码")]   public string Code { get; set; }
        [UI("手机号")]   public string Mobile { get; set; }
        [UI("过期时间")] public DateTime? ExpireDt { get; set; }
        [UI("请求来源")] public string Source { get; set; }

        //-----------------------------------------------
        // 公共方法
        //-----------------------------------------------
        /// <summary>查询</summary>
        public static IQueryable<VerifyCode> Search(string mobile, DateTime? startDt = null, DateTime? endDt = null)
        {
            IQueryable<VerifyCode> q = Set;
            if (mobile.IsNotEmpty()) q = q.Where(t => t.Mobile.Contains(mobile));
            if (startDt != null)     q = q.Where(t => t.CreateDt >= startDt);
            if (endDt != null)       q = q.Where(t => t.CreateDt <= endDt);
            return q;
        }

        /// <summary>批量删除</summary>
        public static void DeleteBatch(int months)
        {
            var date = DateTime.Now.AddMonths(-months);
            Set.Where(t => t.CreateDt < date).Delete();
        }

        /// <summary>获取验证码</summary> 
        public static VerifyCode GetCode(string mobile)
        {
            return Search(mobile, DateTime.Now.AddMinutes(-60)).OrderByDescending(s => s.ExpireDt).FirstOrDefault();
        }
    }

    /// <summary>
    /// 验证码状态
    /// </summary>
    public enum VerifyCodeStatus : int
    {
        [UI("正确")] Ok = 0,
        [UI("错误")] Wrong = 1,
        [UI("已过期")] Expired = 2
    }
}
