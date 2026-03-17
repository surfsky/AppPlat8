using App.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
//using EntityFramework.Extensions;
using App.Entities;

namespace App.DAL
{
    /// <summary>
    /// 工作流类别
    /// </summary>
    public enum WFType : int
    {
        UnKnown = -1,
        [UI("Maintain",  "反馈", typeof(FeedbackStatus))]   Feedback  = 5
    }



    /// <summary>
    /// 工作流
    /// </summary>
    public class Workflow : EntityBase<Workflow>
    {
        // 配置信息
        //[UI("类别")]     public WFType? Type { get; set; }
        [UI("类别名")]   public string Name { get; set; }
        [UI("状态类型")] public Type StatusType { get; set; }
        [UI("实体类型")] public Type EntityType { get; set; }
        [UI("所有步骤")] public virtual List<WFStep> Steps { get; set; }

        // 构造
        public Workflow() { }
        public Workflow(string name, Type entityType, Type statusType, List<WFStep> steps=null)
        {
            //this.Type = type;
            this.Name = name;
            this.EntityType = entityType;
            this.StatusType = statusType;
            this.Steps = steps;
        }

        /// <summary>获取导出对象</summary>
        public override object Export(ExportMode type = ExportMode.Normal)
        {
            var steps = this.Steps.Where(t => t.Type == WFStepType.Start);
            return new
            {
                Name,
                Steps = steps
            };
        }

        //---------------------------------------
        // 获取操作步骤
        //---------------------------------------
        /// <summary>获取开始步骤</summary>
        public List<WFStep> StartSteps => this.Steps.Where(t => t.Type == WFStepType.Start).ToList();

        /// <summary>查找指定步骤</summary>
        public WFStep GetStep(int status)
        {
            return this.Steps.Find(t => t.Status == (int)status);
        }

        /// <summary>获取后继步骤</summary>
        /// <param name="status">当前状态</param>
        /// <returns>若当前状态为空，获取流程开始步骤；否则获取后继操作步骤</returns>
        public List<WFStep> GetNextSteps(int? status, object data=null, User user=null)
        {
            if (status == null)
                return StartSteps;
            else
            {
                var step = GetStep(status.Value);
                if (step == null)
                    return new List<WFStep>();
                return step.Routes
                    .Where(t => t.CheckCondition(data))
                    .Select(t => t.NextStep)
                    .Where(t =>t.CheckUser(user))
                    .ToList()
                    .Cast(t => t.SetAction(t.Action))
                    ;
            }
        }

        /// <summary>更改状态</summary>
        /// <param name="status">当前步骤</param>
        /// <param name="nextStatus">后继步骤</param>
        /// <returns>若成功则返回新步骤，否则返回空</returns>
        public WFStep ChangeStep(int? status, int nextStatus, object data = null, User user = null)
        {
            var steps = GetNextSteps(status, data, user);
            var step = steps.Find(t => t.Status == nextStatus);
            return step;
        }

        /// <summary>获取工作流</summary>
        public static Workflow GetFlow(WFType? type)
        {
            if (type == WFType.Feedback)  return Feedback.Flow;
            return Feedback.Flow;
        }
    }
}
