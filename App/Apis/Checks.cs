using Microsoft.AspNetCore.Http;
using System.ComponentModel;
using System;
using SkiaSharp;
using System.Collections.Generic;
using System.IO;
using App.Components;
using App.HttpApi;
using App.DAL;
using App.Utils;
using App.Web;
using System.Linq;
using App.Entities;

namespace App.API
{
    [Scope("Base")]
    [Description("隐患排查")]
    public class Checks
    {
        //--------------------------------------------
        // 枚举信息
        //--------------------------------------------
        [HttpApi("ObjectType", CacheSeconds = 60 * 60, AuthLogin=true)]
        public static APIResult GetObjectType() => typeof(CheckObjectType).GetEnumInfos().ToResult();

        [HttpApi("RiskLevel", CacheSeconds = 60 * 60, AuthLogin=true)]
        public static APIResult GetRiskLevel() => typeof(CheckRiskLevel).GetEnumInfos().ToResult();

        [HttpApi("RiskColor", CacheSeconds = 60 * 60, AuthLogin=true)]
        public static APIResult GetRiskColor() => typeof(CheckRiskColor).GetEnumInfos().ToResult();

        [HttpApi("BuildingType", CacheSeconds = 60 * 60, AuthLogin=true)]
        public static APIResult GetBuildingType() => typeof(CheckBuildingType).GetEnumInfos().ToResult();

        [HttpApi("Scope", CacheSeconds = 60 * 60, AuthLogin=true)]
        public static APIResult GetScope() => typeof(CheckScope).GetEnumInfos().ToResult();

        [HttpApi("ObjectScale", CacheSeconds = 60 * 60, AuthLogin=true)]
        public static APIResult GetObjectScale() => typeof(CheckObjectScale).GetEnumInfos().ToResult();


        [HttpApi("HazardStatus", CacheSeconds = 60 * 60, AuthLogin=true)]
        public static APIResult GetHazardStatus() => typeof(CheckHazardStatus).GetEnumInfos().ToResult();


        //--------------------------------------------
        // 检查对象管理
        //--------------------------------------------
        [HttpApi("检查对象列表", AuthLogin = true)]
        public static APIResult GetCheckObjects(Paging pi, string name, string socialCreditCode, long? orgId, long? checkerId, CheckObjectType? objectType, CheckObjectScale? scale)
        {
            var userId = Auth.GetUserId();
            var user = User.Get(userId);
            orgId = orgId ?? user?.OrgId;
            return CheckObject.Search(name, socialCreditCode, orgId, checkerId, objectType, scale).SortPageExport(pi).ToResult();
        }

        [HttpApi("检查对象详情", AuthLogin = true)]
        public static APIResult GetCheckObject(int id)
        {
            return CheckObject.Get(id).ToResult();
        }

        [HttpApi("添加检查对象", AuthLogin = true)]
        public static APIResult AddCheckObject(CheckObject obj)
        {
            obj.CreateDt = DateTime.Now;
            obj.UpdateDt = DateTime.Now;
            obj.Save(EntityOp.New);
            return obj.ToResult();
        }

        [HttpApi("修改检查对象", AuthLogin = true)]
        public static APIResult UpdateCheckObject(CheckObject obj)
        {
            obj.UpdateDt = DateTime.Now;
            obj.Save(EntityOp.Edit);
            return obj.ToResult();
        }

        [HttpApi("删除检查对象", AuthLogin = true)]
        public static APIResult DeleteCheckObject(int id)
        {
            CheckObject.Delete(id);
            return new APIResult(0, "删除成功");
        }

        //--------------------------------------------
        // 检查标签和检查表
        //--------------------------------------------
        // 获取检查标签树
        [HttpApi("获取检查标签树", AuthLogin=true)]
        public static APIResult GetCheckTagTree()
        {
            var user = Auth.GetUser();
            if (user.Name == "admin")
                return CheckTag.GetTree().ToResult();
                
            var authOrgId = user.AuthOrgId ?? user.OrgId;
            return CheckTag.IncludeSet.Where(t => t.OrgId == authOrgId).ToList().ToTree().ToResult();
        }

        [HttpApi("获取检查表", AuthLogin=true)]
        public static APIResult GetCheckSheets(string name, CheckScope? scope=null, long? tagId=null)
        {
            return CheckSheet.Search(name, scope, tagId).ToList().ToResult();
        }

        [HttpApi("获取某个对象的检查表", AuthLogin=true)]
        public static APIResult GetObjectCheckSheets(long objectId)
        {
            var tagIds = CheckObject.GetDetail(objectId).Tags.Select(t => t.Id).ToList();
            return CheckSheet.Search(tagIds:tagIds).ToList().ToResult();
        }

        [HttpApi("获取某个检查表的检查项", AuthLogin=true)]
        public static APIResult GetCheckSheetItems(long sheetId)
        {
            return CheckSheetItem.Search(sheetId:sheetId).ToList().ToResult();
        }


        //--------------------------------------------
        // 检查
        //--------------------------------------------
        [HttpApi("检查列表", AuthLogin = true)]
        public static APIResult GetChecks(Paging pi, string objectName, string socialCreditCode, long? objectId, CheckObjectType? objectType, DateTime? checkStartDt, DateTime? checkEndDt)
        {
            return Check.Search(objectName, socialCreditCode, objectId, objectType, checkStartDt, checkEndDt).SortPageExport(pi).ToResult();
        }

        [HttpApi("检查详情", AuthLogin = true)]
        public static APIResult GetCheck(int id)
        {
            return Check.GetDetail(id).ToResult();
        }

        [HttpApi("添加检查记录", AuthLogin = true)]
        public static APIResult AddCheckLog(DAL.Check log)
        {
            log.CreateDt = DateTime.Now;
            log.UpdateDt = DateTime.Now;
            log.Save(EntityOp.New);
            return log.ToResult();
        }

        [HttpApi("修改检查记录", AuthLogin = true)]
        public static APIResult UpdateCheckLog(DAL.Check log)
        {
            log.UpdateDt = DateTime.Now;
            log.Save(EntityOp.Edit);
            return log.ToResult();
        }

        [HttpApi("删除检查记录", AuthLogin = true)]
        public static APIResult DeleteCheckLog(int id)
        {
            DAL.Check.Delete(id);
            return new APIResult(0, "删除成功");
        }

        //--------------------------------------------
        // 隐患
        //--------------------------------------------
        [HttpApi("隐患列表", AuthLogin = true)]
        public static APIResult GetCheckHazards(Paging pi, string objectName, long? objectId, string checkerName, long? checkerId, CheckHazardStatus? status, DateTime? createStartDt)
        {
            return CheckHazard.Search(objectName, objectId, checkerName, checkerId, status, createStartDt).SortPageExport(pi).ToResult();
        }

        [HttpApi("隐患详情", AuthLogin = true)]
        public static APIResult GetCheckHazard(int id)
        {
            return CheckHazard.GetDetail(id).ToResult();
        }

        [HttpApi("添加隐患", AuthLogin = true)]
        public static APIResult AddCheckHazard(CheckHazard hazard)
        {
            hazard.CreateDt = DateTime.Now;
            hazard.UpdateDt = DateTime.Now;
            hazard.Save(EntityOp.New);
            return hazard.ToResult();
        }

        [HttpApi("修改隐患", AuthLogin = true)]
        public static APIResult UpdateCheckHazard(CheckHazard hazard)
        {
            hazard.UpdateDt = DateTime.Now;
            hazard.Save(EntityOp.Edit);
            return hazard.ToResult();
        }

        [HttpApi("删除隐患", AuthLogin = true)]
        public static APIResult DeleteCheckHazard(int id)
        {
            var hazard = CheckHazard.Get(id);
            if (hazard == null)
                return new APIResult(400, "隐患不存在");
            if (hazard.Status == CheckHazardStatus.Closed)
                return new APIResult(400, "隐患已关闭，无法删除");
            if (!Auth.CheckRole("Admins") && hazard.CheckerId != Auth.GetUserId())
                return new APIResult(403, "无权操作");

            CheckHazard.Delete(id);
            return new APIResult(0, "删除成功");
        }
    }
}
