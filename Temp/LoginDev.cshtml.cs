using App.Components;
using App.DAL;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;

namespace App.Pages
{
    public class LoginDevModel : BaseModel
    {
        public void OnGet()
        {
        }

        public IActionResult OnPostLogin(string userName)
        {
            var user = App.DAL.User.GetDetail(u => u.Name == userName);
            if (user != null)
            {
                Auth.LoginSuccess(user);
                return RedirectToPage("/Index");
            }
            return Page();
        }
    }
}
