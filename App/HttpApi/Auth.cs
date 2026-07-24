using App.DAL;
using App.HttpApi;
using App.Utils;
using App.Web;

namespace App.API.Extra
{
    [Scope("Base")]
    public class Auth
    {
        [HttpApi("获取角色菜单ID列表", AuthLogin = true)]
        public static APIResult GetRoleMenus(long? roleId = null)
        {
            if (roleId.HasValue && roleId.Value > 0)
                return RoleMenu.GetRoleMenuIds(roleId.Value).ToResult();

            var userId = App.Components.Auth.GetUserId(Asp.Current);
            if (!userId.HasValue)
                return new APIResult(401, "用户未登录");

            return RoleMenu.GetUserMenuIds(userId.Value).ToResult();
        }
    }
}
