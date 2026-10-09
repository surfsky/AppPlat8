using App.Components;
using App.DAL;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.Shared
{
    [Auth(AuthLogin = true)]
    public class PropsEditorModel : AuthModel
    {
        [BindProperty(SupportsGet = true)]
        public string Md { get; set; }

        [BindProperty(SupportsGet = true)]
        public string Props { get; set; }

        [BindProperty(SupportsGet = true)]
        public string DataJson { get; set; }

        [BindProperty(SupportsGet = true)]
        public string Json { get; set; }

        [BindProperty(SupportsGet = true)]
        public string Ret { get; set; }

        public void OnGet()
        {
        }
    }
}
