using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Text;
using System.Reflection;
using System.Collections.Generic;
using Microsoft.CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.IO;
using System.Collections;

namespace App.Utils
{
    /// <summary>
    /// C# 表达式计算器
    /// From: http://www.codeproject.com/csharp/runtime_eval.asp
    /// netcore 使用 Roslyn 进行编译（替代 CodeDom）
    /// </summary>
    public class CsEvaluator : Evaluator
    {
        /// <summary>CSharp 表达式求值</summary>
        /// <param name="expression">CSharp 表达式。如：2.5, DateTime.Now</param>
        public override object Eval(string expression)
        {
            // 代码
            var text = string.Format(@"
                using System;
                public class Calculator
                {{
                    public static object Evaluate() {{ return {0}; }}
                }}", expression);

            // 编译生成程序集
            var tree = SyntaxFactory.ParseSyntaxTree(text);
            var compilation = CSharpCompilation.Create(
                "calc.dll",
                new[] { tree },
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
                references: new[] { 
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
                });
            Assembly compiledAssembly;
            using (var stream = new MemoryStream())
            {
                var compileResult = compilation.Emit(stream);
                compiledAssembly = Assembly.Load(stream.GetBuffer());
            }

            // 用反射执行方法
            var calculatorClass = compiledAssembly.GetType("Calculator");
            var evaluateMethod = calculatorClass.GetMethod("Evaluate");
            return evaluateMethod.Invoke(null, null);
        }
    }
}

