using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.Components;
using App.DAL;
using App.EleUI;
using App.HttpApi;
using App.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace App.Pages.Checks
{
    [CheckPower(Power.CheckObjectEdit)]
    public class CheckObjectFormModel : AdminModel
    {
        public CheckObject Item { get; set; }

        public void OnGet(long id = 0)
        {
            Item = CheckObject.GetDetail(id) ?? new CheckObject();
            //JsonData = Item.Export(ExportMode.Normal).ToJson();
        }

        public IActionResult OnGetData(long id)
        {
            var item = CheckObject.GetDetail(id) ?? new CheckObject();
            return BuildResult(0, "success", item.Export(ExportMode.Normal));
        }

        public IActionResult OnPostSave([FromBody] CheckObject req)
        {
            if (req == null)
                return BuildResult(400, "参数错误");

            var item = CheckObject.Get(req.Id); 
            if (item == null)
            {
                item = new CheckObject();
                item.CreateDt = DateTime.Now;
            }

            item.Name = req.Name;
            item.Code = req.Code;
            item.DutyOrgId = req.DutyOrgId;
            item.Address = req.Address;
            item.Gps = req.Gps;
            item.SocialCreditCode = req.SocialCreditCode;
            item.ObjectType = req.ObjectType;
            item.Field = req.Field;
            item.DutyUserName = req.DutyUserName;
            item.SafetyAdminName = req.SafetyAdminName;
            item.EleMeeterNum = req.EleMeeterNum;
            item.EmployeeCount = req.EmployeeCount;
            item.Scale = req.Scale;
            item.ProductContent = req.ProductContent;
            item.IsUsing = req.IsUsing;
            item.LandArea = req.LandArea;
            item.BuildingArea = req.BuildingArea;
            item.IsInOnlinePlatform = req.IsInOnlinePlatform;
            item.IsKeySupervision = req.IsKeySupervision;
            item.IsProductInNight = req.IsProductInNight;
            item.IsProductInSpringFestival = req.IsProductInSpringFestival;
            item.ThirdPartySafetyAgency = req.ThirdPartySafetyAgency;
            item.IsYiEnterprise = req.IsYiEnterprise;

            item.RiskLevel = req.RiskLevel;
            item.RiskColor = req.RiskColor;
            item.Scope = req.Scope;
            item.CheckerId = req.CheckerId;
            item.SocialCheckerId = req.SocialCheckerId;
            item.OutlookImage = req.OutlookImage;
            item.LicenseImage = req.LicenseImage;
            item.IsIn141Platform = req.IsIn141Platform;
            item.IsDemonstration = req.IsDemonstration;
            item.IndustryType = req.IndustryType;
            item.IndustryRisk = req.IndustryRisk;
            item.IsThreePlacesThreeEnterprises = req.IsThreePlacesThreeEnterprises;
            item.IsParkFactoryOverlayRisk = req.IsParkFactoryOverlayRisk;
            item.HasWelding = req.HasWelding;
            item.HasEnvironmentalEquipment = req.HasEnvironmentalEquipment;
            item.InternalRewardMechanism = req.InternalRewardMechanism;
            item.SafetySteward = req.SafetySteward;
            item.StandardizationStatus = req.StandardizationStatus;
            item.BuildingType = req.BuildingType;
            item.FactoryUsageType = req.FactoryUsageType;
            item.BuildingStructure = req.BuildingStructure;
            item.HasSprinklerSystem = req.HasSprinklerSystem;
            item.OutlookImage = Uploader.SaveFile(nameof(CheckObject), req.OutlookImage);
            item.LicenseImage = Uploader.SaveFile(nameof(CheckObject), req.LicenseImage);

            item.Save();
            return BuildResult(0, "保存成功");
        }

        /// <summary>显示联系人</summary>
        public IActionResult OnPostShowContacts([FromBody] CheckObject req)
        {
            var objectId = req?.Id ?? 0;
            if (objectId <= 0)
            {
                return EleManager.ShowClientNotify("请先保存检查对象，再维护联系人", NotifyType.Warning, "提示");
            }

            var objectName = Uri.EscapeDataString(req?.Name ?? string.Empty);
            var url = $"/Checks/ObjectContacts?objectId={objectId}&objectName={objectName}&md={this.Mode}";
            return EleManager.OpenClientDrawer(
                title: "对象联系人",
                url: url,
                size: "50%"
                );
        }

        /// <summary>显示对象事件</summary>
        public IActionResult OnPostShowObjectEvents([FromBody] CheckObject req)
        {
            var objectId = req?.Id ?? 0;
            if (objectId <= 0)
            {
                return EleManager.ShowClientNotify("请先保存检查对象，再维护对象事件", NotifyType.Warning, "提示");
            }

            var objectName = Uri.EscapeDataString(req?.Name ?? string.Empty);
            var url = $"/Checks/CheckObjectEvents?objectId={objectId}&objectName={objectName}&md={this.Mode}";
            return EleManager.OpenClientDrawer(
                title: "对象事件",
                url: url,
                size: "70%"
                );
        }

        /// <summary>显示文件</summary>
        public IActionResult OnPostShowFiles([FromBody] CheckObject req)
        {
            var uniId = req.UniId;
            var objectName = Uri.EscapeDataString(req?.Name ?? string.Empty);
            var url = $"/Shared/Atts?uniId={uniId}&name={objectName}&md={this.Mode}";
            return EleManager.OpenClientDrawer(
                title: "文件",
                url: url,
                size: "50%"
                );
        }

        /// <summary>显示检查历史</summary>
        public IActionResult OnPostShowCheckLogs([FromBody] CheckObject req)
        {
            var objectId = req?.Id ?? 0;
            if (objectId <= 0)
            {
                return EleManager.ShowClientNotify("请先保存检查对象，再查看检查历史", NotifyType.Warning, "提示");
            }

            var objectName = Uri.EscapeDataString(req?.Name ?? string.Empty);
            var url = $"/Checks/CheckLogs?objectId={objectId}&objectName={objectName}&md={this.Mode}";
            return EleManager.OpenClientDrawer(
                title: "检查历史",
                url: url,
                size: "70%"
                );
        }

        /// <summary>显示隐患清单</summary>
        public IActionResult OnPostShowHazards([FromBody] CheckObject req)
        {
            var objectId = req?.Id ?? 0;
            if (objectId <= 0)
            {
                return EleManager.ShowClientNotify("请先保存检查对象，再查看隐患清单", NotifyType.Warning, "提示");
            }

            var objectName = Uri.EscapeDataString(req?.Name ?? string.Empty);
            var url = $"/Checks/CheckHazards?objectId={objectId}&objectName={objectName}&md={this.Mode}";
            return EleManager.OpenClientDrawer(
                title: "隐患清单",
                url: url,
                size: "70%"
                );
        }
    }
}
