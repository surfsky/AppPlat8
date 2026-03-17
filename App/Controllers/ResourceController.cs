using Microsoft.AspNetCore.Mvc;
using System.Reflection;
using System.IO;

namespace App.Controllers
{
    [Route("res")]
    public class ResourceController : Controller
    {
        [HttpGet("{name}")]
        public IActionResult Get(string name)
        {
            var assembly = typeof(App.Startup).Assembly;
            
            // Try full name first
            var resourceName = name;
            var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null) 
            {
                // Try to find resource case-insensitive
                var allNames = assembly.GetManifestResourceNames();
                foreach (var n in allNames)
                {
                    if (n.Equals(resourceName, System.StringComparison.OrdinalIgnoreCase) || 
                        n.EndsWith($".{name}", System.StringComparison.OrdinalIgnoreCase))
                    {
                        stream = assembly.GetManifestResourceStream(n);
                        break;
                    }
                }
            }
            if (stream == null)
                return NotFound($"Resource not found: {name}");

            // Determine content type based on file extension
            var ext = Path.GetExtension(name).ToLower();
            var contentType = ext switch
            {
                ".js" => "application/javascript",
                ".css" => "text/css",
                _ => "application/octet-stream"
            };

            return File(stream, contentType);
        }
    }
}
