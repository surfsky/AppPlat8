using App.Components;
using App.DAL;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace App.Pages.CRM
{
    [Auth(Permission.ContactView)]
    public class ContactManagerModel : AuthModel
    {
        public void OnGet()
        {
        }
    }
}
