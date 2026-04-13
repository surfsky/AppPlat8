using App.Components;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.EleUISamples
{
    public class UserListModel : BaseModel
    {
        public User Item { get; set; }

        public void OnGet()
        {
        }

        public IActionResult OnGetData(Paging pi, string name)
        {
            var result = Data.QueryUsers(pi.PageIndex, pi.PageSize, name, "", "");
            return BuildResult(0, "success", new
            {
                items = result.Items,
                total = result.Total
            });
        }
    }
}
