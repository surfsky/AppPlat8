using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using App.Entities;
using App.Utils;

namespace App.DAL
{
    /// <summary>厂商</summary>
    [UI("基础", "厂商")]
    public class Company : EntityBase<Company>
    {
        [UI("名称")]             public string Name { get; set; }
        [UI("组织")]             public long? OrgId { get; set; }
        [UI("简称")]             public string AbbrName { get; set; }
        [UI("统一社会信用代码")]   public string UnifiedSocialCreditCode { get; set; }
        [UI("地址")]             public string Address { get; set; }
        [UI("法人")]             public string LegalPerson { get; set; }
        [UI("法人联系方式")]      public string LegalPersonPhone { get; set; }
        [UI("联系人")]           public string Contact { get; set; }
        [UI("联系方式")]         public string ContactPhone { get; set; }

        /// <summary>导出数据供客户端使用（根据导出模式返回不同的字段）</summary>
        override public object Export(ExportMode mode=ExportMode.Normal)
        {
            return new
            {
                Id,
                Name,
                OrgId,
                AbbrName,
                UnifiedSocialCreditCode,
                Address,
                LegalPerson,
                LegalPersonPhone,
                Contact,
                ContactPhone,
                CreateDt,
                UpdateDt
            };
        }

        public static IQueryable<Company> Search(string name, string abbrName, string legalPerson, long? orgId = null)
        {
            var q = IncludeSet.AsQueryable();
            if (name.IsNotEmpty())          q = q.Where(o => o.Name.Contains(name.Trim()));
            if (abbrName.IsNotEmpty())     q = q.Where(o => o.AbbrName.Contains(abbrName.Trim()));
            if (legalPerson.IsNotEmpty())  q = q.Where(o => o.LegalPerson.Contains(legalPerson.Trim()));
            if (orgId.IsNotEmpty())        q = q.Where(o => o.OrgId == orgId.Value);
            return q;
         }
    }
}
