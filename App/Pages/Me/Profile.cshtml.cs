using System;
using System.Linq;
using App.DAL;
using App.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace App.Pages
{
    public class ProfileModel : AdminModel
    {
        public User UserInfo { get; set; }

        public void OnGet()
        {
            var uid = GetUserId();
            this.UserInfo = App.DAL.User.GetDetail(uid.Value);
        }
    }
}
