using App.Entities;
using App.Utils;

namespace App.DAL
{
    /// <summary>用户授权组织</summary>
    [UI("系统", "用户授权组织")]
    public class UserOrg : EntityBase<UserOrg>
    {
        [UI("用户")] public long? UserId { get; set; }
        [UI("组织")] public long? OrgId { get; set; }
        [UI("职务")] public string Title { get; set; }

        public virtual User User { get; set; }
        public virtual Org Org { get; set; }

        public string UserName => User?.Name;
        public string UserRealName => User?.RealName;
        public string OrgName => Org?.Name;
        public string OrgFullName => Org?.FullName ?? Org?.Name;

        public override object Export(ExportMode type = ExportMode.Normal)
        {
            return new
            {
                Id,
                UserId,
                UserName,
                UserRealName,
                OrgId,
                OrgName,
                OrgFullName,
                Title
            };
        }
    }
}
