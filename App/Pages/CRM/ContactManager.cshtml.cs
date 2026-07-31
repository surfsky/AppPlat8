using App.Components;
using App.DAL;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace App.Pages.CRM
{
    [Auth(Power.ContactView)]
    public class ContactManagerModel : AdminModel
    {
        public void OnGet()
        {
        }
    }
}
