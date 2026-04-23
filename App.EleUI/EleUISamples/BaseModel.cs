using System.Collections.Generic;
using System.Dynamic;
using System.Reflection;
using App.HttpApi;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace App.Pages.EleUISamples
{
    /// <summary>
    /// Lightweight sample page model for App.EleUI demo pages.
    /// It is intentionally independent from App's runtime BaseModel.
    /// </summary>
    public class BaseModel : PageModel
    {
        /// <summary>
        /// Export [BindProperty] values for client initialization.
        /// </summary>
        public object Export()
        {
            IDictionary<string, object> data = new ExpandoObject();
            var properties = GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);

            foreach (var property in properties)
            {
                if (!property.CanRead || property.GetIndexParameters().Length > 0)
                    continue;
                if (property.GetCustomAttribute<BindPropertyAttribute>() == null)
                    continue;

                data[property.Name] = property.GetValue(this);
            }

            return data;
        }

        /// <summary>
        /// Unified API response builder for sample handlers.
        /// </summary>
        public static JsonResult BuildResult(int code, string msg, object data = null, App.Components.Paging pager = null)
        {
            // Use framework default System.Text.Json pipeline to avoid serializer type mismatch.
            return new JsonResult(new APIResult(code, msg, data, pager));
        }
    }
}
