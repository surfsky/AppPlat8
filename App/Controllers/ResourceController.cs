using Microsoft.AspNetCore.Mvc;
using System.Reflection;
using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using App.EleUI;

namespace App.Controllers
{
    [Route("res")]
    public class ResourceController : Controller
    {
        [HttpGet]
        public IActionResult GetByQuery([FromQuery] string path = null, [FromQuery] string name = null)
        {
            var resource = string.IsNullOrWhiteSpace(path) ? name : path;
            if (string.IsNullOrWhiteSpace(resource))
                return BadRequest("Missing resource path.");

            return GetInternal(resource);
        }

        [HttpGet("{name}")]
        public IActionResult Get(string name)
        {
            return GetInternal(name);
        }

        private IActionResult GetInternal(string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath))
                return BadRequest("Missing resource path.");

            var normalized = NormalizeResourceName(resourcePath);
            var stream = FindResourceStream(normalized, out var matchedName);
            if (stream == null)
            {
                if (TryMapLegacyEleUiResource(resourcePath, out var mappedUrl))
                    return Redirect(mappedUrl);

                return NotFound($"Resource not found: {resourcePath}");
            }

            // Determine content type based on file extension
            var ext = Path.GetExtension(matchedName).ToLowerInvariant();
            var contentType = ext switch
            {
                ".js" => "application/javascript",
                ".css" => "text/css",
                _ => "application/octet-stream"
            };

            return File(stream, contentType);
        }

        private static string NormalizeResourceName(string resourcePath)
        {
            var trimmed = (resourcePath ?? string.Empty).Trim();
            trimmed = trimmed.TrimStart('/');
            return trimmed.Replace('/', '.');
        }

        private static IEnumerable<Assembly> GetCandidateAssemblies()
        {
            // 主项目程序集 + App.EleUI 程序集，覆盖新旧两套路径。
            yield return typeof(App.Startup).Assembly;

            var eleAssembly = typeof(EleAppTagHelper).Assembly;
            if (eleAssembly != null && eleAssembly != typeof(App.Startup).Assembly)
                yield return eleAssembly;
        }

        private static Stream FindResourceStream(string resourceName, out string matchedName)
        {
            matchedName = null;
            var exact = resourceName;
            var bySuffix = "." + resourceName;
            var fileName = Path.GetFileName(resourceName);
            var normalizedTarget = Compact(resourceName);

            foreach (var assembly in GetCandidateAssemblies())
            {
                var names = assembly.GetManifestResourceNames();

                foreach (var name in names)
                {
                    if (name.Equals(exact, StringComparison.OrdinalIgnoreCase)
                        || name.EndsWith(bySuffix, StringComparison.OrdinalIgnoreCase)
                        || name.EndsWith("." + fileName, StringComparison.OrdinalIgnoreCase)
                        || Compact(name).EndsWith(normalizedTarget, StringComparison.OrdinalIgnoreCase))
                    {
                        var stream = assembly.GetManifestResourceStream(name);
                        if (stream != null)
                        {
                            matchedName = name;
                            return stream;
                        }
                    }
                }
            }

            return null;
        }

        private static string Compact(string value)
        {
            return new string((value ?? string.Empty)
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray());
        }

        private static bool TryMapLegacyEleUiResource(string resourcePath, out string mappedUrl)
        {
            mappedUrl = null;
            var compact = Compact(resourcePath);

            // 历史运行时入口：/res/App.EleUI.EleUIJs.EleUI.js（以及变体）
            if (compact.EndsWith("appeleuieleuijseleuijs", StringComparison.Ordinal))
            {
                mappedUrl = "/_content/App.EleUI/eleui/eleui.js";
                return true;
            }

            return false;
        }
    }
}
