using System;

namespace App.HttpApi
{
    /// <summary>
    /// API 返回值
    /// </summary>
    public class APIResult
    {
        /// <summary>错误编码</summary>
        public int Code { get; set; }

        /// <summary>详细信息（文本类型，一些说明性的文字）</summary>
        public string Message { get; set; }

        /// <summary>数据创建时间</summary>
        public DateTime? CreateDt { get; set; }

        /// <summary>详细数据（自定义类型，可为数组、对象）</summary>
        public object Data { get; set; }

        /// <summary>附加数据（分页信息Pager）</summary>
        public object Pager { get; set; }

        public APIResult(int code=0, string message="", object data=null, object pager=null)
        {
            Code = code;
            Message = message;
            Data = data;
            Pager = pager;
            CreateDt = DateTime.Now;
        }
    }
}
