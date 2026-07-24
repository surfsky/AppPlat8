using System.Linq;
using App.Components;
using App.DAL;

namespace App.Pages.Admins
{
    [Auth(Power.RolePowerEdit)]
    public class RoleManagerModel : AdminModel
    {
        public long? RoleId { get; set; }

        public void OnGet(long? roleId)
        {
            RoleId = roleId;
            if (!RoleId.HasValue)
                RoleId = Role.Set.OrderBy(t => t.Id).Select(t => (long?)t.Id).FirstOrDefault();
        }
    }
}
