using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace App.WebCore
{
    /// <summary>Appliction 数据存取接口</summary>
    public interface IApplicationService
    {
        object Get(string key);
        void Set(string key, object value);
    }

    /// <summary>模拟实现 Application["key"]</summary>
    /// <example>
    /// # StartUp
    /// services.TryAddSingleton<IApplicationService, ApplicationService>();
    /// 
    /// # cshtml
    /// @inject IApplicationService Application
    /// Application.Get("SystemKeywords")
    /// </example>
    public class ApplicationService : IApplicationService
    {
        private readonly ConcurrentDictionary<string, object> _dict = new ConcurrentDictionary<string, object>();

        public object Get(string key)
        {
            _dict.TryGetValue(key, out var val);
            return val;
        }

        public void Set(string key, object value)
        {
            _dict[key] = value;
        }
    }
}
