using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Extensions.FileProviders.Physical;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Caching.Distributed;

namespace App.Utils
{
    /// <summary>
    /// 缓存处理（支持内存、Redis、文件）
    /// </summary>
    public class Cacher
    {
        static MemoryCache _memory = new MemoryCache(new MemoryCacheOptions() { });
        static RedisCache _redis;

        /// <summary>使用Redis作为缓存</summary>
        public static void UseRedis(string ip, string password, string instanceName)
        {
            _redis = new RedisCache(new RedisCacheOptions()
            {
                Configuration = $"{ip},password={password}",
                InstanceName = $"{instanceName}"
            });
        }

        /// <summary>移除缓存</summary>
        public static void Remove(string key)
        {
            if (_redis != null)
                _redis.Remove(key);
            else
                _memory.Remove(key);
        }


        /// <summary>是否存在缓存</summary>
        public static bool Contains(string key)
        {
            if (_redis != null)
                return _redis.Get(key) == null;
            else
                return _memory.TryGetValue(key,  out object o);
        }


        /// <summary>是否存在缓存</summary>
        public static void Set(string key, object value)
        {
            if (_redis != null)
                _redis.Set(key, value.ToObjectBytes());
            else
                _memory.Set(key, value);
        }


        /// <summary>从缓存中获取数据。若该缓存失效，则自动从创建对象并塞入缓存</summary>
        public static T Get<T>(string key, Func<T> func=null, DateTime? expiredTime=null) where T : class
        {
            if (_redis != null)
                return GetRedisCache(key, func, expiredTime);
            return GetMemoryCache(key, func, expiredTime);
        }

        //---------------------------------------------------
        // MemoryCache & RedisCache
        //---------------------------------------------------
        /// <summary>从本机内存缓存中获取数据。若该缓存失效，则自动从创建对象并塞入缓存</summary>
        public static T GetMemoryCache<T>(string key, Func<T> func, DateTime? expiredTime = null) where T : class
        {
            if (!_memory.TryGetValue(key, out object o))
            {
                if (func == null)
                    return null;

                o = func();
                if (expiredTime == null)
                    _memory.Set(key, o);
                else
                    _memory.Set(key, o, expiredTime.Value - DateTime.Now);
            }
            return o as T;
        }

        /// <summary>从Redis获取缓存数据（未测试）</summary>
        /// <remarks>
        /// Redis存储的是byte[]
        /// 现阶段用以下方式转化为对象：bytes -> json -> object
        /// 以后尝试直接用 jsonb 方式： bytes -> object
        /// </remarks>
        public static T GetRedisCache<T>(string key, Func<T> func, DateTime? expiredTime = null) where T: class
        {
            var bytes = _redis.Get(key);
            if (bytes == null || bytes.Length == 0)
            {
                if (func == null)
                    return null;

                bytes = func().ToObjectBytes();
                _redis.Set(key, bytes, new DistributedCacheEntryOptions()
                {
                     AbsoluteExpiration = expiredTime
                });
            }
            var o = bytes.ToObject() as T;
            return o;
        }

        /// <summary>从文件中获取缓存，若文件变更，自动刷新缓存（未测试）</summary>
        public static string GetFileCache(string fileName)
        {
            if (_memory.TryGetValue(fileName, out string txt))
            {
                var fileInfo = new FileInfo(fileName);
                txt = File.ReadAllText(fileName);
                var cacheEntityOps = new MemoryCacheEntryOptions();
                cacheEntityOps.AddExpirationToken(new PollingFileChangeToken(fileInfo)); // 监控文件变化
                cacheEntityOps.RegisterPostEvictionCallback((key, value, reason, state) => { Console.WriteLine($"文件 {key} 改动了"); }); // 缓存失效时处理
                _memory.Set(fileInfo.Name, txt, cacheEntityOps);
            }
            return txt;
        }



    }
}
