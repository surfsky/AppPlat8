using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using App.Entities;
using App.Utils;

namespace App.DAL.OA
{
    /// <summary>固定资产类别</summary>
    public enum AssetCategory
    {
        [UI("电脑设备")] Computer = 0,
        [UI("流量卡")] SimCard = 1,
        [UI("办公设备")] Office = 2,
        [UI("电子设备")] Electronic = 3,
        [UI("其他")] Other = 99
    }

    /// <summary>固定资产</summary>
    [UI("OA", "固定资产")]
    public class Asset : EntityBase<Asset>
    {
        [UI("名称")]        public string Name { get; set; }
        [UI("类别")]        public AssetCategory? Category { get; set; }
        [UI("组织")]        public long? OrgId { get; set; }
        [UI("责任人")]      public long? ChargeUserId { get; set; }
        [NotMapped]         public string ChargeUserName { get; set; }
        [UI("位置")]        public string Location { get; set; }
        [UI("厂商")]        public string Manufacturer { get; set; }
        [UI("图片")]        public string Image { get; set; }
        [UI("参数")]        public string Parameters { get; set; }
        [UI("启用时间")]     public DateTime? EnableDt { get; set; }
        [UI("过期时间")]     public DateTime? ExpireDt { get; set; }
        [UI("是否到期提醒")]  public bool IsExpireAlert { get; set; }

        public virtual Org Org { get; set; }
        public virtual User ChargeUser { get; set; }


        /// <summary>导出数据供客户端使用（根据导出模式返回不同的字段）</summary>
        override public object Export(ExportMode mode = ExportMode.Normal)
        {
            return new
            {
                id = Id,
                name = Name,
                category = Category,
                categoryName = Category.GetTitle(),
                orgId = OrgId,
                orgName = Org?.Name,
                chargeUserId = ChargeUserId,
                chargeUserName = ChargeUser?.Name,
                location = Location,
                manufacturer = Manufacturer,
                image = Image,
                parameters = Parameters,
                enableDt = EnableDt,
                expireDt = ExpireDt,
                isExpireAlert = IsExpireAlert,
                createDt = CreateDt,
                updateDt = UpdateDt
            };
        }

        public static IQueryable<Asset> Search(string name, AssetCategory? category, long? orgId)
        {
            var q = IncludeSet.AsQueryable();
            if (name.IsNotEmpty())       q = q.Where(o => o.Name.Contains(name.Trim()));
            if (category.IsNotEmpty())   q = q.Where(o => o.Category == category.Value);
            if (orgId.IsNotEmpty())      q = q.Where(o => o.OrgId == orgId.Value);
            return q;
        }
    }
}
