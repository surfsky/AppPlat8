using System;
using System.Collections.Generic;
using System.Linq;
using App.Components;
using App.DAL;
using App.DAL.GIS;
using App.Entities;
using App.HttpApi;
using App.Utils;
using App.Utils.Gis;
using Microsoft.EntityFrameworkCore;

namespace App.API
{
    /// <summary>
    /// Gis 数据接口
    /// </summary>
    public class Gis
    {

        //---------------------------------------------------------
        // GIS菜单树相关接口
        //---------------------------------------------------------
        [HttpApi("获取GIS菜单树", AuthLogin = true)]
        public static APIResult GetMenuTree(long? excludeId = null, long? selectedId = null)
        {
            // 这里使用实时查询，避免前端在菜单调整后读取到旧缓存。
            var all = GisMenu.Set.AsNoTracking().ToList();
            var allMap = all.ToDictionary(t => t.Id, t => t);
            var visibleMap = all.ToDictionary(t => t.Id, t => t);

            if (excludeId.HasValue)
            {
                var blockedIds = all.GetDescendants(excludeId).Select(t => t.Id).ToHashSet();
                foreach (var id in blockedIds)
                {
                    visibleMap.Remove(id);
                }
            }

            if (selectedId.HasValue && allMap.TryGetValue(selectedId.Value, out var selected))
            {
                var current = selected;
                while (current != null)
                {
                    visibleMap[current.Id] = current;
                    if (current.ParentId == null) break;
                    if (!allMap.TryGetValue(current.ParentId.Value, out current)) break;
                }
            }

            var tree = visibleMap.Values.OrderBy(t => t.SortId).ThenBy(t => t.Id).ToList().ToTree();
            return tree.Select(t => t.Export()).ToList().ToResult();
        }


        //---------------------------------------------------------
        // GIS场景相关接口
        //---------------------------------------------------------
        [HttpApi("获取GIS地图样式", AuthLogin = true)]
        public static APIResult GetMapStyles()
        {
            return GisScene.Styles.ToResult();
        }

        [HttpApi("获取GIS场景列表", AuthLogin = true)]
        public static APIResult GetScenes()
        {
            var list = GisScene.Set.AsNoTracking().OrderBy(t => t.SortId).ToList();
            return list.ToResult();
        }

        [HttpApi("获取GIS场景详情", AuthLogin = true)]
        public static APIResult GetSceneDetail(long id)
        {
            var scene = GisScene.Set.AsNoTracking()
                .Include(t => t.SceneMenus)
                .Include(t => t.ScenePanels)
                .FirstOrDefault(t => t.Id == id);
            
            if (scene == null)
                return new APIResult(404, "场景不存在");

            return new
            {
                scene.Id,
                scene.Name,
                scene.MapZoom,
                scene.MapCenter,
                scene.MapPitch,
                Enable3D = scene.Map3D,
                AutoRotate = scene.AutoRotate,
                scene.MapStyle,
                scene.MapProjection,
                menuIds = scene.SceneMenus.Select(m => m.MenuId).ToList(),
                panelIds = scene.ScenePanels.Select(p => p.PanelId).ToList()
            }.ToResult();
        }

        //---------------------------------------------------------
        // 地址查询相关接口（使用高德地图API）
        //---------------------------------------------------------
        [HttpApi("获取检查对象点位数据", AuthLogin = false)]
        public static APIResult GetCheckObjectPoints(string name = null, long? orgId = null, bool? isDel = false, string region = null, int maxCount = 500)
        {
            if (maxCount <= 0)     maxCount = 100;
            if (maxCount > 5000)   maxCount = 5000;

            // 获取点位数据
            var q = CheckObject.Set.AsNoTracking()  .Where(t => !string.IsNullOrWhiteSpace(t.Gps));
            if (isDel != null)           q = q.Where(t => t.IsDel == isDel.Value);
            if (orgId.IsNotEmpty())      q = q.Where(t => t.DutyOrgId == orgId.Value);
            if (name.IsNotEmpty())       q = q.Where(t => (t.Name ?? string.Empty).Contains(name.Trim()));
            var items = q
                .OrderBy(t => t.Id)
                .Take(maxCount * 4)
                .Select(t => new
                {
                    t.Id,
                    t.Name,
                    t.Address,
                    t.Gps,
                    t.SocialCreditCode,
                    t.RiskLevel,
                    t.ObjectType,
                    t.Scale,
                    DutyOrgId = t.DutyOrgId,
                    t.DutyUserName,
                })
                .ToList();

            // 过滤掉不合法的坐标和不在指定区域内的点位。
            var regioner = GisRegion.Parse(region);
            var list = new List<object>();
            foreach (var item in items)
            {
                if (!GisHelper.TryParseLngLat(item.Gps, out var lng, out var lat))
                    continue;
                if (regioner != null && !regioner.Contains(lng, lat))
                    continue;

                list.Add(new
                {
                    id = item.Id,
                    name = item.Name,
                    gps = item.Gps,
                    addr = item.Address,
                    lng,
                    lat,
                    dataJson = new
                    {
                        item.SocialCreditCode,
                        item.RiskLevel,
                        item.ObjectType,
                        item.Scale,
                        item.DutyOrgId,
                        item.DutyUserName,
                    }
                });

                if (list.Count >= maxCount)
                    break;
            }

            return list.ToResult();
        }

        [HttpApi("获取地址列表", AuthLogin = true)]
        public static APIResult GetAddrs(string name)
        {
            if (name.IsEmpty())
                return new APIResult(-1, "请输入地址关键字");
            return AmapHelper.GetAddrs(name).ToResult();
        }



        [HttpApi("获取单个地址", AuthLogin = true)]
        public static APIResult GetAddr(string name)
        {
            var addr = AmapHelper.GetAddr(name);
            if (addr == null)
                return new APIResult(-1, "未找到该地址");
            return addr.ToResult();
        }

   
    }
}
