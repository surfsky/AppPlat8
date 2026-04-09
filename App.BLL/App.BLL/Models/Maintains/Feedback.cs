using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
//using EntityFramework.Extensions;
using App.Utils;
using App.Entities;

namespace App.DAL
{
    /// <summary>反馈类型</summary>
    public enum FeedType
    {
        [UI("BUG")]         Bug = 0,
        [UI("错误")]        Error = 1,
        [UI("新需求")]      Request = 2,
        [UI("调整")]        Adjust = 3,
        [UI("建议")]        Suggest = 4,
        [UI("其它")]        Other = 9,
    }

    /// <summary>反馈状态</summary>
    public enum FeedbackStatus
    {
        [UI("已创建")]  Create = 0,
        [UI("已完成")]  Finish = 1,
        [UI("已取消")]  Cancel = 2,
        [UI("已派发")]  Dispatched = 3,
        [UI("处理中")]  Handling = 4,
        [UI("已搁置")]  Suspend = 5,
    }

    /// <summary>反馈归属的系统</summary>
    public enum FeedApp
    {
        [UI("网站")]       Web = 0,
        [UI("钉钉小程序")] DingMP = 1,
        [UI("微信小程序")] WechatMP = 5,
        [UI("综合")]       Mix = 9,
    }

    /// <summary>
    /// 反馈
    /// </summary>
    [UI("基础", "反馈建议")]
    public class Feedback : EntityBase<Feedback>, IDeleteLogic
    {
        // 分类
        [UI("类型")]        public FeedType? Type { get; set; }
        [UI("状态")]        public FeedbackStatus? Status { get; set; }
        [UI("系统")]        public FeedApp? App { get; set; }
        [UI("模块")]        public string AppVersion { get; set; }
        [UI("模块")]        public string AppModule { get; set; }
        [UI("在用")]        public bool? InUsed { get; set; } = true;

        // 提交人
        [UI("提交人")]      public long? UserID { get; set; }
        [UI("提交人")]      public string User { get; set; }
        [UI("联系方式")]    public string Contacts { get; set; }

        // 内容
        [UI("概述")]        public string Title { get; set; }
        [UI("信息")]        public string Content { get; set; }
        [UI("回应")]        public string Reply { get; set; }
        [UI("图片1")]       public string Image1 { get; set; }
        [UI("图片2")]       public string Image2 { get; set; }
        [UI("图片3")]       public string Image3 { get; set; }
        [UI("反馈图片")]    public string Image4 { get; set; }

        // 扩展
        [UI("类型")]        public string TypeName { get { return this.Type.GetTitle(); } }
        [UI("状态")]        public string StatusName { get { return this.Status.GetTitle(); } }
        [UI("系统")]        public string AppName { get { return this.App.GetTitle(); } }


        //-----------------------------------------------
        // 公共方法
        //-----------------------------------------------
        // 查找
        public static IQueryable<Feedback> Search(
            long? userId = null, string user = null, 
            string keyword = null,
            FeedType? type = null,  FeedbackStatus? status = null,
            FeedApp? app = null,  string appVersion = null,
            DateTime? fromDt = null
            )
        {
            IQueryable<Feedback> q = Set.Where(t => t.InUsed != false);
            if (userId != null)            q = q.Where(t => t.UserID == userId);
            if (user.IsNotEmpty())         q = q.Where(t => t.User.Contains(user));
            if (keyword.IsNotEmpty())      q = q.Where(t => t.Content.Contains(keyword) || t.Title.Contains(keyword));
            if (appVersion.IsNotEmpty())   q = q.Where(t => t.AppVersion.Contains(appVersion));
            if (type != null)              q = q.Where(t => t.Type == type);
            if (status != null)            q = q.Where(t => t.Status == status);
            if (app != null)               q = q.Where(t => t.App == app);
            if (fromDt != null)            q = q.Where(t => t.CreateDt >= fromDt);
            return q;
        }



        //-----------------------------------------------
        // 公共方法
        //-----------------------------------------------
        // 工作流
        public static Workflow Flow = GetFlow();

        /// <summary>创建反馈处理流</summary>
        static Workflow GetFlow()
        {
            var stepCreate = WFStep.Create(FeedbackStatus.Create, null, WFStepType.Start);
            var stepCancel = WFStep.Create(FeedbackStatus.Cancel, null, WFStepType.End);
            var stepFinish = WFStep.Create(FeedbackStatus.Finish, null, WFStepType.End);
            var stepDispatch = WFStep.Create(FeedbackStatus.Dispatched, Power.FeedBackDispatch);
            var stepHandle = WFStep.Create(FeedbackStatus.Handling, Power.FeedBackHandle);
            var stepSuspend = WFStep.Create(FeedbackStatus.Suspend, null, WFStepType.End);

            // 关系
            stepCreate.AddNext(stepDispatch).AddNext(stepCancel);
            stepDispatch.AddNext(stepHandle).AddNext(stepCancel);
            stepHandle.AddNext(stepFinish).AddNext(stepCancel).AddNext(stepSuspend).AddNext(stepDispatch, "退回");

            // 导出
            var steps = Convertor.ToList(stepCreate, stepCancel, stepFinish, stepSuspend, stepHandle, stepDispatch);
            var flow = new Workflow("反馈处理流程", typeof(Feedback), typeof(FeedbackStatus), steps);
            return flow;
        }

        /// <summary>获取当前用户可操作的动作</summary>
        public List<WFStep> GetNextSteps(User user)
        {
            return Flow.GetNextSteps((int?)this.Status, null, user);
        }

        /// <summary>更改状态（若失败会抛出异常）</summary>
        public Feedback ChangeStatus(FeedbackStatus status, User user, string remark = "", List<string> fileUrls = null)
        {
            // 状态相同就不用更改了
            if (this.Status == status)
                return this;

            // 找到合适的下一步状态
            var statusName = status.GetTitle();
            var step = Flow.ChangeStep((int?)this.Status, (int)status, null, user);
            if (step == null)
                throw new Exception("不允许更改状态：" + statusName);

            // 更新信息，增加操作历史
            this.Status = status;
            this.Save();
            this.AddHistory(user.Id, user.RealName, user.Mobile, statusName, (int)status, remark, fileUrls);
            return this;
        }

        /// <summary>设置后继处理人</summary>
        public Feedback SetNextProcessor(User user, DateTime? dt)
        {
            var hist = this.LastHistory;
            if (hist != null)
            {
                hist.AssignNextUser(user?.Id, user?.RealName, user?.Mobile, dt);
            }
            return this;
        }
    }
}