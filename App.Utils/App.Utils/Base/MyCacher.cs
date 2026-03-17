using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace App.Utils
{
    /// <summary>
    /// 缓存元素（含过期逻辑）
    /// </summary>
    internal class CacheItem
    {
        public object Value { get; set; }
        public DateTime? ExpireTime { get; set; }
    }


    /// <summary>
    /// 缓存类（替代 Runtime.Cache)。支持值为null。未测试
    /// </summary>
    public class MyCacher
    {
        Dictionary<string, CacheItem> _dict = new Dictionary<string, CacheItem>();
        object _lock = new object();

        // 构造函数
        public MyCacher()
        {
            // 开个线程自动释放缓存
            var t = new Thread(new ThreadStart(Run));
            t.Start();
        }

        //
        void Run()
        {
            var interval = 1000;
            while (true)
            {
                lock (_lock)
                {
                    foreach (var key in _dict.Keys)
                    {
                        var item = _dict[key];
                        item = ExpireItemIfNeed(key, item);
                    }
                }
                Thread.Sleep(interval);
            }
        }

        /// <summary>缓存总数</summary>
        public int Count 
        {
            get
            {
                lock (_lock)
                {
                    return _dict.Count;
                }
            }
        }

        /// <summary>如有必要过期缓存</summary>
        private CacheItem ExpireItemIfNeed(string key, CacheItem item)
        {
            if (item.ExpireTime != null && item.ExpireTime > DateTime.Now)
            {
                _dict.Remove(key);
                return null;
            }
            return item;
        }

        /// <summary>删除</summary>
        public void Remove(string key)
        {
            lock (_lock)
            {
                _dict.Remove(key);
            }
        }

        /// <summary>索引器，相当于Get(key)</summary>
        public object this[string key] => Get(key);

        /// <summary>获得一个Cache对象</summary>
        public object Get(string key)
        {
            lock (_lock)
            {
                if (key.IsEmpty())
                    return null;
                if (!_dict.ContainsKey(key))
                    return null;
            
                var item = _dict[key];
                item = ExpireItemIfNeed(key, item);
                if (item != null)
                {
                    if (item.ExpireTime == null)
                        return item.Value;
                    if (item.ExpireTime < DateTime.Now)
                        return item.Value;
                }
                return null;
            }
        }

        /// <summary>是否存在缓存</summary>
        public bool Contains(string key)
        {
            lock (_lock)
            {
                if (key.IsEmpty()) 
                    return false;
                if (!_dict.ContainsKey(key))
                    return false;

                var item = _dict[key];
                item = ExpireItemIfNeed(key, item);
                if (item != null)
                {
                    if (item.ExpireTime == null)
                        return true;
                }
                return false;
            }
        }

        /// <summary>设置缓存</summary>
        public void Set(string key, object value, DateTime? expired)
        {
            lock (_lock)
            {
                if (key.IsEmpty())
                    return;
                if (!_dict.ContainsKey(key))
                {
                    Add(key, value, expired);
                    return;
                }

                // 修改
                var item = _dict[key];
                item = ExpireItemIfNeed(key, item);
                if (item != null)
                {
                    item.Value = value;
                    item.ExpireTime = expired;
                }
                else
                {
                    Add(key, value, expired);
                }
            }
        }

        /// <summary>新增缓存</summary>
        private void Add(string key, object value, DateTime? expired)
        {
            var item = new CacheItem() { Value = value, ExpireTime = expired };
            if (item.ExpireTime != null && item.ExpireTime < DateTime.Now)
                _dict.Add(key, item);
        }
    }
}
