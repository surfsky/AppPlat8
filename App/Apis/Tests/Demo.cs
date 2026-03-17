using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Xml.Serialization;
using App.Components;
using App.HttpApi;
using App.Utils;
using App.Web;
using Newtonsoft.Json;

namespace App.Api
{
    //--------------------------------------------------
    // 类型定义
    //--------------------------------------------------
    /// <summary>Person sex</summary>
    public enum Sex
    {
        [UI("男")] Male = 0,
        [UI("女")] Female = 1,
        [Description("不详")] Unknown = 2,
    }

    /// <summary>Person</summary>
    public class Person
    {
        public string Name { get; set; }
        public DateTime? Birth { get; set; }
        public Sex? Sex { get; set; }
        public Person Father { get; set; }
        public List<Person> Children { get; set; } = new List<Person>();

        [JsonIgnore]
        [XmlIgnore]
        //[ScriptIgnore]
        //[NonSerialized]
        public Person Mather { get; set; }
    }



    //--------------------------------------------------
    // 演示
    //--------------------------------------------------
    [Description("HttpApi Demo")]
    [HttpApi.Script(CacheDuration =0, ClassName ="Demo", NameSpace ="App")]
    [HttpApi.History("2016-11-01", "SURFSKY", "History log1")]
    [HttpApi.History("2019-08-15", "SURFSKY", "Fix token")]
    public partial class Demo
    {
        //---------------------------------------------
        // 静态方法
        //---------------------------------------------
        [HttpApi("HelloWorld")]
        public static string HelloWorld(string info)
        {
            System.Threading.Thread.Sleep(200);
            return string.Format("Hello world! {0} {1}", info, DateTime.Now);
        }

        [HttpApi("TestSession")]
        public static string TestSession(string info)
        {
            Asp.SetSession("info", info);
            return Asp.GetSession<string>("info");
        }

        [HttpApi("静态方法示例", Type = ResponseType.JSON)]
        public static object GetStaticObject()
        {
            return new { h = "3", a = "1", b = "2", c = "3" };
        }

        [HttpApi("Json结果包裹器示例", Wrap = true, WrapCondition ="获取数据成功")]
        public static object TestWrap()
        {
            return new { h = "3", a = "1", b = "2", c = "3" };
        }

        [HttpApi("默认方法参数示例", Remark = "p2的默认值为a", Status = ApiStatus.Delete, AuthVerbs ="GET")]
        public static object TestDefaultParameter(string p1, string p2="a")
        {
            return new { p1 = p1, p2 = p2};
        }

        [HttpApi("测试错误")]
        public static object TestError()
        {
            int n = 0;
            int m = 1 / n;
            return true;
        }

        [HttpApi("限制访问方式", AuthVerbs ="Post")]
        public static string TestVerbs()
        {
            return Asp.Current.Request.Method;
        }

        [HttpApi("测试可空枚举")]
        public static Sex? GetNullalbeEnum(Sex? sex=Sex.Male)
        {
            return sex;
        }




    }
}
