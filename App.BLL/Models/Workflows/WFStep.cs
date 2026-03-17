using App.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using App.Entities;

namespace App.DAL
{
    /// <summary>
    /// 工作流步骤类别
    /// </summary>
    public enum WFStepType : int
    {
        [UI("开始")] Start = 0,   // 一个流程只有一个开始节点
        [UI("结束")] End = 1,     // 可以有多个结束节点
        [UI("普通")] Normal = 2,
    }

    /// <summary>
    /// 工作流步骤
    /// </summary>
    public class WFStep : EntityBase<WFStep>
    {
        // 配置信息
        [UI("节点类别")]           public WFStepType? Type { get; set; }
        [UI("状态ID")]             public int? Status { get; set; }
        [UI("状态名称")]           public string StatusName { get; set; }
        [UI("路由", Column=ColumnType.None)]  public virtual List<WFRoute> Routes { get; set; }

        // 权限
        [UI("权限")]               public Power? Power { get; set; }
        [UI("权限名")]             public string PowerName { get { return this.Power.GetTitle(); } }

        // 便利属性，可作为导出用
        [UI("动作"), NotMapped]    public string Action { get; set; }
        [UI("路由"), NotMapped]    public string RoutesText { get { return this.Routes.Cast(t => t.ToString()).ToSeparatedString(); } }
        [UI("类型"), NotMapped]    public string TypeName { get { return this.Type.GetTitle(); } }

        /// <summary>请使用 Create 泛型方法</summary>
        public WFStep() { }
        /// <summary>创建步骤</summary>
        public static WFStep Create<T>(T status, Power? power = null, WFStepType type = WFStepType.Normal)
            where T : struct
        {
            var step = new WFStep();
            step.Type = type;
            step.Status = Convert.ToInt32(status);
            step.StatusName = status.GetTitle();
            step.Action = step.StatusName.Replace("已", "").Replace("中", "");
            step.Power = power;
            step.Routes = new List<WFRoute>();
            return step;
        }

        /// <summary>获取导出对象</summary>
        public override object Export(ExportMode type = ExportMode.Normal)
        {
            return new
            {
                this.Type,
                this.Status,
                this.StatusName,
                this.Action,
                Power = (int?)this.Power,
                this.PowerName
            };
        }

        /// <summary>设置动作</summary>
        public WFStep SetAction(string action)
        {
            this.Action = action;
            return this;
        }

        /// <summary>检测人员</summary>
        public bool CheckUser(User user)
        {
            if (user == null)
                return true;

            // 检测角色
            if (this.Power != null)
                if (!user.HasPower(this.Power.Value))
                    return false;

            return true;
        }

        //----------------------------------------------------
        // 辅助方法
        //----------------------------------------------------
        /// <summary>设置后续步骤</summary>
        public WFStep AddNexts(params WFStep[] steps)
        {
            var arr = steps.ToList();
            foreach (var step in steps)
                this.AddNext(step);
            return this;
        }

        /// <summary>设置后续步骤</summary>
        public WFStep AddNext(WFStep nextStep, string action=null, string expression = null)
        {
            var route = new WFRoute();
            route.Step = this;
            route.NextStep = nextStep;
            route.Status = this.Status;
            route.NextStatus = nextStep.Status;

            route.Condition = expression;
            route.Action = action ?? nextStep.StatusName.Replace("已", "").Replace("中", "");
            //route.Save();
            this.Routes.Add(route);
            return this;
        }

        /// <summary>获取文本</summary>
        public override string ToString()
        {
            return string.Format("{0} {1}", this.StatusName, this.Type.GetTitle());
        }

        //----------------------------------------------------
        // 数据库
        //----------------------------------------------------
        /// <summary>获取详细对象</summary>
        public new static WFStep GetDetail(long id)
        {
            return Set.FirstOrDefault(t => t.Id == id);
        }

        // 
        public WFStep Save()
        {
            // 先不保存到数据库
            return null;
        }

    }
}
