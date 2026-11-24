using Microsoft.AspNetCore.Hosting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace App.Web
{
    public interface IServerService
    {
        string MapPath(string path);
    }

    /// <summary>
    /// services.AddSingleton<IServerProvider, ServerProvider>();
    /// </summary>
    public class ServerService : IServerService
    {
        private IWebHostEnvironment _host;
        public ServerService(IWebHostEnvironment host)
        {
            _host = host;
        }

        public string MapPath(string path)
        {
            return Path.Combine(_host.ContentRootPath, path);
        }
    }
}
