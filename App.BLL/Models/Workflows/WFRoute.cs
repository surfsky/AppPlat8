using App.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using App.Entities;

/*
工作流设计思路
    步骤：WFStep
        步骤名、步骤值
        后继路由集合
        前驱路由集合
        执行：Undo, Success, Fail, Back
    路由：WFRoute
        From/To/
        路由类型: Or/And/Vote
        时延：时延结束自动跳到下一步骤
        角色、部门、人员：作为路由的参数，但工作流并不处理，由事件自行处理
    工作流实例：WFInstance
        由WFService.CreateInstance(wfId)启动
        保存实例数据：如报销金额
        实例事件：启动、结束、进入、跳出
        实例日志：选择路由后，会将选择步骤记录在 WFLog 表中
    后台进程：WFService, WFConsoler
        负责定时调度工作流

*/
namespace App.DAL
{
    /// <summary>
    /// 工作流路由类型
    /// </summary>
    public enum WFRouteType
    {
        [UI("普通节点")]   Normal,    // 多并发后继
        [UI("逻辑与节点")] And,       // 前驱节点结果都为Succeed时才能进入后继节点。
        [UI("时延节点")]   Delay,     // 延长指定指定时间后才进入后继节点。多并发后继。
        [UI("手工节点")]   Manual,    // 请在 OnStepLeave 事件中设置后继节点。
    }

    /// <summary>
    /// 工作流路由
    /// </summary>
    public class WFRoute : EntityBase<WFRoute>
    {
        [UI("路由类型")] public WFRouteType? Type { get; set; }
        [UI("当前步骤")] public int? Status { get; set; }
        [UI("后继步骤")] public int? NextStatus{ get; set; }
        [UI("操作名称")] public string Action { get; set; }

        /// <summary>逻辑表达式，格式如：data >= 3000</summary>
        [UI("表达式")]   public string Condition { get; set; } = "";  // 格式如： data>=3000

        // 扩展属性
        [UI("当前步骤")] public virtual WFStep Step { get; set; }
        [UI("后继步骤")] public virtual WFStep NextStep { get; set; }

        /// <summary>获取导出对象</summary>
        public override object Export(ExportMode type = ExportMode.Normal)
        {
            return new
            {
                Type,
                NextStep.Status,
                NextStep.StatusName,
                Action
            };
        }

        public override string ToString()
        {
            return $"{Action}({NextStatus})";
        }

        /// <summary>检测路由条件（表达式）</summary>
        public bool CheckCondition(object data)
        {
            if (data.IsEmpty() || this.Condition.IsEmpty())
                return true;

            // 用Js引擎来解析表达式值
            var eval = new JsEvaluator();
            var code = this.Condition.Replace("data", data.ToString());
            return eval.EvalBool(code);
        }


    }
}
