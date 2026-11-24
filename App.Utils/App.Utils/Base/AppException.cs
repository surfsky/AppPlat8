using System;

namespace App.Utils
{
    /// <summary>
    /// 通用业务异常。配合 BaseModel.Check 使用，失败时抛出，由 BaseModel 统一拦截转换为 APIResult。
    /// </summary>
    public class AppException : Exception
    {
        /// <summary>错误码</summary>
        public int Code { get; }

        public AppException(int code, string message) : base(message) => Code = code;
        public AppException(string message) : this(400, message) { }
    }
}