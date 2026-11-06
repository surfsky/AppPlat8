using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Hosting;

namespace App.Pages.EleUISamples
{
    public class IndexModel : PageModel
    {
        private readonly IWebHostEnvironment _environment;

        private static readonly IReadOnlyDictionary<string, SampleSource> Sources =
            new Dictionary<string, SampleSource>(System.StringComparer.OrdinalIgnoreCase)
            {
                ["about"] = new("About.cshtml"),
                ["manager-server"] = new("Managers/ManagerServer.cshtml", "Managers/ManagerServer.cshtml.cs"),
                ["manager-client"] = new("Managers/Manager.cshtml", "Managers/Manager.cshtml.cs"),
                ["theme"] = new("Theme.cshtml"),
                ["hstack"] = new("Layouts/HStack.cshtml"),
                ["vstack"] = new("Layouts/VStack.cshtml"),
                ["grid"] = new("Layouts/Grid.cshtml"),
                ["button"] = new("Controls/Button.cshtml", "Controls/Button.cshtml.cs"),
                ["forms"] = new("Controls/Form.cshtml", "Controls/Form.cshtml.cs"),
                ["tree"] = new("Controls/Tree.cshtml", "Controls/Tree.cshtml.cs"),
                ["city-select"] = new("Controls/CitySelect.cshtml", "Controls/CitySelect.cshtml.cs"),
                ["icons"] = new("Controls/Icons.cshtml"),
                ["image-viewer"] = new("Controls/ImageViewer.cshtml", "Controls/ImageViewer.cshtml.cs"),
                ["drawer"] = new("Popups/Drawer.cshtml", "Popups/Drawer.cshtml.cs"),
                ["server-drawer"] = new("Popups/ServerDrawer.cshtml", "Popups/ServerDrawer.cshtml.cs"),
                ["open-drawer"] = new("Popups/OpenDrawer.cshtml"),
                ["dialog"] = new("Popups/Dialog.cshtml", "Popups/Dialog.cshtml.cs"),
                ["card"] = new("Containers/Card.cshtml"),
                ["split-panel"] = new("Containers/SplitPanel.cshtml"),
                ["container"] = new("Containers/Container.cshtml"),
                ["users"] = new("Datas/Users.cshtml", "Datas/Users.cshtml.cs"),
                ["user-list"] = new("Datas/UserList.cshtml", "Datas/UserList.cshtml.cs"),
                ["timer"] = new("Datas/Timer.cshtml", "Datas/Timer.cshtml.cs"),
                ["role-users"] = new("Datas/RoleUsers.cshtml", "Datas/RoleUsers.cshtml.cs"),
                ["dept-users"] = new("Datas/DeptUsers.cshtml", "Datas/DeptUsers.cshtml.cs"),
                ["app-example"] = new("Apps/Index.cshtml")
            };

        public IndexModel(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        public async Task<IActionResult> OnGetSourceAsync(string id, string kind)
        {
            if (string.IsNullOrWhiteSpace(id) || !Sources.TryGetValue(id, out var source))
                return NotFound("未找到该示例的源码。");

            var relativePath = string.Equals(kind, "cshtmlcs", System.StringComparison.OrdinalIgnoreCase)
                ? source.CodeBehind
                : string.Equals(kind, "cshtml", System.StringComparison.OrdinalIgnoreCase)
                    ? source.Razor
                    : null;

            if (string.IsNullOrWhiteSpace(relativePath))
                return Content("该示例没有独立的 .cshtml.cs 文件。", "text/plain; charset=utf-8");

            var fullPath = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, relativePath));
            var rootPath = Path.GetFullPath(_environment.ContentRootPath + Path.DirectorySeparatorChar);
            if (!fullPath.StartsWith(rootPath, System.StringComparison.Ordinal)
                || !System.IO.File.Exists(fullPath))
                return NotFound("源码文件当前不可用。");

            var text = await System.IO.File.ReadAllTextAsync(fullPath);
            return Content(text, "text/plain; charset=utf-8");
        }

        private sealed record SampleSource(string Razor, string CodeBehind = null);
    }
}
