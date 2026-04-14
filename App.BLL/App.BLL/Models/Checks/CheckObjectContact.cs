using System;
using System.Linq;
using App.Entities;
using App.Utils;
using Z.EntityFramework.Plus;


/*
检查对象CheckObject --(1:n)-- 检查对象联系人CheckObjectContact
                  --(1:n)-- 检查对象标签CheckTag
*/
namespace App.DAL
{
    /// <summary>对象拥有的人员</summary>
    [UI("检查", "对象拥有的人员清单")]
    public class CheckObjectContact : EntityBase<CheckObjectContact>
    {
        [UI("姓名")] public string Name { get; set; }
        [UI("照片")] public string Photo { get; set; }
        [UI("证件号")] public string IdCard { get; set; }
        [UI("证件照片")] public string IdCardImage { get; set; }
        [UI("联系方式")] public string Phone { get; set; }
        [UI("执证日期")] public DateTime? CertDt { get; set; }
        [UI("过期日期")] public DateTime? CertExpireDt { get; set; }

        public virtual CheckObject CheckObject { get; set; }

        public override object Export(ExportMode mode)
        {
            return new
            {
                id = Id,
                name = Name,
                photo = Photo,
                idCard = IdCard,
                idCardImage = IdCardImage,
                phone = Phone,
                certDt = CertDt,
                certExpireDt = CertExpireDt,
            };
        }

        public static IQueryable<CheckObjectContact> Search(string name="", string phone="", string socialCreditCode="", long? objectId=null, long? orgId=null, long? checkerId=null, CheckObjectType? objectType=null, CheckObjectScale? scale=null)
        {
            IQueryable<CheckObjectContact>     q = CheckObjectContact.IncludeSet;
            if (objectId.IsNotEmpty())         q = q.Where(o => o.CheckObject.Id == objectId.Value);
            if (orgId.IsNotEmpty())            q = q.Where(o => o.CheckObject.DutyOrgId == orgId.Value);
            if (checkerId.IsNotEmpty())        q = q.Where(o => o.CheckObject.CheckerId == checkerId.Value);
            if (name.IsNotEmpty())             q = q.Where(o => o.Name.Contains(name.Trim()));
            if (phone.IsNotEmpty())            q = q.Where(o => o.Phone.Contains(phone.Trim()));
            if (socialCreditCode.IsNotEmpty()) q = q.Where(o => o.CheckObject.SocialCreditCode.Contains(socialCreditCode.Trim()));
            if (objectType.IsNotEmpty())       q = q.Where(o => o.CheckObject.ObjectType == objectType.Value);
            if (scale.IsNotEmpty())            q = q.Where(o => o.CheckObject.Scale == scale.Value);
            return q;
        }
    }

}