using Jint;
using System;
using System.CodeDom.Compiler;
using System.Reflection;

namespace App.Utils
{
    /// <summary>
    /// 使用Jint javascript 引擎来解析字符串的值
    /// 因为 Microsoft.JScript 在 netcore 中木有，故考虑用 Jint 实现
    /// 更多功能参考 https://github.com/sebastienros/jint
    /// Jint 非常好用！
    /// </summary>
    public class JsEvaluator : Evaluator
    {
        static Engine _engine;
        static JsEvaluator()
        {
            _engine = new Engine().SetValue("log", new Action<object>(Console.WriteLine));
        }

        /// <summary>解析表达式值</summary>
        public override object Eval(string expression)
        {
            var cmd = $"eval({expression.Quote()})";
            return _engine.Evaluate(cmd).ToObject();
        }

        /// <summary>
        /// 转化为日期时间必须用这个函数。格式如：new Date('2018/01/01 12:00:00')
        /// </summary>
        public override DateTime EvalDateTime(string expression)
        {
            var cmd = $"eval({expression.Quote()}).toLocaleString()";
            var o = _engine.Evaluate(cmd).ToObject();
            return Convert.ToDateTime(o);
        }
    }
}
