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
    /// 工作流实例（启动、变状态、处理、事件等）（尚未启用）
    /// </summary>
    public class WFInstance : EntityBase<WFInstance>
    {
        [UI("工作流")]   public long WorkflowId { get; set; }
        [UI("当前步骤")] public List<int> CurrentStatus { get; set; }
        //[UI("数据")]     public string Data { get; set; }

        // 导航信息
        public virtual Workflow Workflow { get; set; }

        public WFInstance() { }

        // 构造函数
        public WFInstance(Workflow flow)
        {
            this.Workflow = flow;
            this.WorkflowId = flow.Id;
            this.CreateDt = DateTime.Now;
        }

        /// <summary>获取后继步骤</summary>
        public List<WFStep> GetNextSteps(int? status, string data)
        {
            return this.Workflow?.GetNextSteps(status);
        }
    }
}
