using System;
using Microsoft.AspNetCore.Builder;
using App.Components;

namespace App.Middlewares
{
    /// <summary>全局异常捕获中间件</summary>
    public static class ExceptionCatchMiddleware
    {
        /// <summary>使用全局异常捕获中间件</summary>
        public static IApplicationBuilder UseExceptionCatch(this IApplicationBuilder app, Action<Exception> callback = null)
        {
            return app.Use(async (context, next) =>
            {
                try
                {
                    await next();
                }
                catch (Exception ex)
                {
                    callback?.Invoke(ex);
                    throw;
                }
            });
        }
    }    
}
