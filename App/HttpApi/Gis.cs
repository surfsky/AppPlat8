using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using App.Components;
using App.DAL;
using App.DAL.GIS;
using App.Entities;
using App.HttpApi;
using App.Utils;
using App.Utils.Gis;
using App.Web;
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

        /// <summary>获取检查对象统一 GIS 数据</summary>
        [HttpApi("获取检查对象GIS数据", AuthLogin = false)]
        public static APIResult GetCheckObjects(string tagNames = null, Paging pi = null)
        {
            pi ??= new Paging();
            if (pi.PageSize <= 0) pi.PageSize = 20;
            if (pi.PageSize > 200) pi.PageSize = 200;
            if (pi.PageIndex < 0) pi.PageIndex = 0;

            var tags = ParseTagNames(tagNames);
            var tagIds = tags.Count == 0
                ? new List<long>()
                : CheckTag.Set.AsNoTracking()
                    .Where(t => tags.Contains(t.Name))
                    .Select(t => t.Id)
                    .ToList();

            var query = CheckObject.Search(tagIds: tagIds, includeTags: true)
                .OrderBy(t => t.IsDel ?? false)
                .ThenBy(t => t.Name)
                .ThenBy(t => t.Id);

            var total = query.Count();
            pi.SetTotal(total);
            var items = query
                .Skip(pi.PageIndex * pi.PageSize)
                .Take(pi.PageSize)
                .ToList()
                .Select((CheckObject t) => (IGeometry)t.ToGeometryItem())
                .ToList();

            return new
            {
                items,
                pageInfo = pi
            }.ToResult();
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

        /// <summary>从菜单获取统一 GIS 数据</summary>
        public static List<GeometryItem> GetMenuGeometryItems(long menuId, Paging pi = null, string keyword = null, bool? isVisible = null)
        {
            var menu = GisMenu.Get(menuId);
            if (menu == null)
                return new List<GeometryItem>();

            return GetMenuGeometryItems(menu.Id, menu.DataFrom, menu.DataUrl, pi, keyword, menu.Name, menu.Icon, isVisible);
        }

        /// <summary>从参数获取统一 GIS 数据</summary>
        public static List<GeometryItem> GetMenuGeometryItems(long? menuId, GisDataFrom? dataFrom, string dataUrl, Paging pi = null, string keyword = null, string menuName = null, string icon = null, bool? isVisible = null)
        {
            var from = dataFrom ?? GisDataFrom.Geometry;
            if (from == GisDataFrom.API)
            {
                var menu = new GisMenu
                {
                    Id = menuId ?? 0,
                    Name = menuName,
                    Icon = icon,
                    DataFrom = GisDataFrom.API,
                    DataUrl = dataUrl
                };
                return GetApiMenuGeometryItems(menu, pi, keyword);
            }

            if (!menuId.IsNotEmpty())
                return new List<GeometryItem>();

            return GetGeometryMenuItems(menuId.Value, keyword, isVisible);
        }

        /// <summary>获取菜单点位数量</summary>
        public static int GetMenuGeometryCount(long? menuId, GisDataFrom? dataFrom, string dataUrl, string menuName = null, string icon = null)
        {
            var from = dataFrom ?? GisDataFrom.Geometry;
            if (from == GisDataFrom.API)
            {
                var menu = new GisMenu
                {
                    Id = menuId ?? 0,
                    Name = menuName,
                    Icon = icon,
                    DataFrom = GisDataFrom.API,
                    DataUrl = dataUrl
                };
                return GetApiMenuGeometryCount(menu);
            }

            if (!menuId.IsNotEmpty())
                throw new Exception("请先保存菜单后再测试 Geometry 数据");

            return GisGeometry.Search(menuId: menuId.Value, recursive: true).Count();
        }

        /// <summary>获取 Geometry 菜单数据</summary>
        static List<GeometryItem> GetGeometryMenuItems(long menuId, string keyword, bool? isVisible)
        {
            var query = GisGeometry.Search(menuId: menuId, recursive: true, isVisible: isVisible);
            if (keyword.IsNotEmpty())
                query = query.Where(t => (t.Name ?? "").Contains(keyword) || (t.Alias ?? "").Contains(keyword) || (t.Addr ?? "").Contains(keyword));

            return query
                .OrderBy(t => t.SortId)
                .ThenBy(t => t.Id)
                .ToList()
                .Select(t => new GeometryItem
                {
                    Id = t.Id,
                    RawId = t.Id,
                    Type = t.Type,
                    MenuId = t.MenuId,
                    SortId = t.SortId,
                    Name = t.Name,
                    Alias = t.Alias,
                    Addr = t.Addr,
                    Gps = t.Gps,
                    Region = t.Region,
                    Url = t.Url,
                    File = t.File,
                    GeoJson = t.GeoJson,
                    DataJson = t.DataJson,
                    Remark = t.Remark,
                    IsVisible = t.IsVisible,
                    Scale = t.Scale,
                    LabelColor = t.LabelColor,
                    Icon = t.MenuIcon,
                    MenuName = t.MenuName,
                    DataFrom = GisDataFrom.Geometry
                })
                .ToList();
        }

        /// <summary>获取 API 菜单数据</summary>
        static List<GeometryItem> GetApiMenuGeometryItems(GisMenu menu, Paging pi, string keyword)
        {
            if (TryGetLocalCheckObjects(menu, pi, keyword, out var localItems))
                return localItems;

            var pageInfo = pi ?? BuildApiPaging(menu);
            var url = BuildMenuDataUrl(menu.DataUrl, pageInfo, keyword);
            if (url.IsEmpty())
                return new List<GeometryItem>();

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            using var resp = client.GetAsync(url).GetAwaiter().GetResult();
            if (!resp.IsSuccessStatusCode)
                throw new Exception($"获取 API 菜单数据失败: HTTP {(int)resp.StatusCode}");

            var text = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (text.IsEmpty())
                return new List<GeometryItem>();

            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            var code = root.TryGetProperty("code", out var codeNode)
                ? GetIntValue(codeNode, 0)
                : 0;
            if (code != 0)
            {
                var msg = root.TryGetProperty("msg", out var msgNode)
                    ? msgNode.GetString()
                    : root.TryGetProperty("message", out var msgNode2) ? msgNode2.GetString() : "接口返回失败";
                throw new Exception(msg.IsEmpty() ? "接口返回失败" : msg);
            }

            if (!root.TryGetProperty("data", out var dataNode) || dataNode.ValueKind != JsonValueKind.Object)
                return new List<GeometryItem>();
            if (!dataNode.TryGetProperty("items", out var itemsNode) || itemsNode.ValueKind != JsonValueKind.Array)
                return new List<GeometryItem>();

            var list = JsonSerializer.Deserialize<List<GeometryItem>>(itemsNode.GetRawText(), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<GeometryItem>();

            return list
                .Select(t =>
                {
                    t.DataFrom = GisDataFrom.API;
                    t.RawId = t.RawId > 0 ? t.RawId : (t.Id > 0 ? t.Id : 0);
                    return t.CloneForMenu(menu.Id, menu.Name, menu.Icon);
                })
                .ToList();
        }

        /// <summary>获取 API 菜单点位数量</summary>
        static int GetApiMenuGeometryCount(GisMenu menu)
        {
            if (TryGetLocalCheckObjectsCount(menu, out var cnt))
                return cnt;

            var url = BuildMenuDataUrl(menu.DataUrl, new Paging { PageIndex = 0, PageSize = 1 }, null);
            if (url.IsEmpty())
                return 0;

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            using var resp = client.GetAsync(url).GetAwaiter().GetResult();
            if (!resp.IsSuccessStatusCode)
                throw new Exception($"获取 API 菜单数据失败: HTTP {(int)resp.StatusCode}");

            var text = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (text.IsEmpty())
                return 0;
            return ParseDataCount(text);
        }

        /// <summary>尝试读取本地检查对象接口</summary>
        static bool TryGetLocalCheckObjects(GisMenu menu, Paging pi, string keyword, out List<GeometryItem> list)
        {
            list = null;
            var raw = (menu?.DataUrl ?? string.Empty).Trim().TrimStart('/');
            if (raw.IsEmpty())
                return false;

            if (!raw.StartsWith("GetCheckObjects", StringComparison.OrdinalIgnoreCase)
                && !raw.StartsWith("api/gis/GetCheckObjects", StringComparison.OrdinalIgnoreCase)
                && !raw.StartsWith("httpapi/gis/GetCheckObjects", StringComparison.OrdinalIgnoreCase)
                && !raw.StartsWith("gis/GetCheckObjects", StringComparison.OrdinalIgnoreCase))
                return false;

            var idx = raw.IndexOf('?');
            var qs = idx >= 0 ? raw[(idx + 1)..] : string.Empty;
            var query = ParseQueryString(qs);
            var tagNames = query.TryGetValue("tagNames", out var tagVal) ? tagVal : null;
            var pageInfo = pi ?? BuildApiPaging(menu);
            var res = GetCheckObjects(tagNames, pageInfo);
            if (res?.Code != 0)
                throw new Exception(res?.Message ?? "获取检查对象失败");

            var json = JsonSerializer.Serialize(res.Data);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("items", out var itemsNode))
            {
                list = new List<GeometryItem>();
                return true;
            }

            list = JsonSerializer.Deserialize<List<GeometryItem>>(itemsNode.GetRawText(), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<GeometryItem>();

            list = list.Select(t =>
            {
                t.DataFrom = GisDataFrom.API;
                t.RawId = t.RawId > 0 ? t.RawId : (t.Id > 0 ? t.Id : 0);
                return t.CloneForMenu(menu.Id, menu.Name, menu.Icon);
            }).ToList();
            return true;
        }

        /// <summary>尝试读取本地检查对象数量</summary>
        static bool TryGetLocalCheckObjectsCount(GisMenu menu, out int cnt)
        {
            cnt = 0;
            var raw = (menu?.DataUrl ?? string.Empty).Trim().TrimStart('/');
            if (raw.IsEmpty())
                return false;

            if (!raw.StartsWith("GetCheckObjects", StringComparison.OrdinalIgnoreCase)
                && !raw.StartsWith("api/gis/GetCheckObjects", StringComparison.OrdinalIgnoreCase)
                && !raw.StartsWith("httpapi/gis/GetCheckObjects", StringComparison.OrdinalIgnoreCase)
                && !raw.StartsWith("gis/GetCheckObjects", StringComparison.OrdinalIgnoreCase))
                return false;

            var idx = raw.IndexOf('?');
            var qs = idx >= 0 ? raw[(idx + 1)..] : string.Empty;
            var query = ParseQueryString(qs);
            var tagNames = query.TryGetValue("tagNames", out var tagVal) ? tagVal : null;
            var res = GetCheckObjects(tagNames, new Paging { PageIndex = 0, PageSize = 1 });
            if (res?.Code != 0)
                throw new Exception(res?.Message ?? "获取检查对象失败");

            var json = JsonSerializer.Serialize(new
            {
                code = res.Code,
                msg = res.Message,
                data = res.Data
            });
            cnt = ParseDataCount(json);
            return true;
        }

        /// <summary>构建 API 默认分页</summary>
        static Paging BuildApiPaging(GisMenu menu)
        {
            var size = menu?.DataCnt ?? 200;
            if (size <= 0) size = 200;
            if (size > 5000) size = 5000;
            return new Paging { PageIndex = 0, PageSize = size };
        }

        /// <summary>构建菜单数据地址</summary>
        static string BuildMenuDataUrl(string dataUrl, Paging pi, string keyword)
        {
            var raw = (dataUrl ?? string.Empty).Trim();
            if (raw.IsEmpty())
                return string.Empty;

            raw = NormalizeDataUrl(raw);

            var pageInfo = pi ?? new Paging();
            var sep = raw.Contains('?') ? "&" : "?";
            var url = $"{raw}{sep}pi.PageIndex={pageInfo.PageIndex}&pi.PageSize={pageInfo.PageSize}";
            if (keyword.IsNotEmpty())
                url += $"&keyword={Uri.EscapeDataString(keyword.Trim())}";
            return url;
        }

        /// <summary>解析查询字符串</summary>
        static Dictionary<string, string> ParseQueryString(string qs)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (qs.IsEmpty())
                return dict;

            foreach (var seg in qs.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var idx = seg.IndexOf('=');
                if (idx < 0)
                {
                    dict[Uri.UnescapeDataString(seg)] = string.Empty;
                    continue;
                }
                var key = Uri.UnescapeDataString(seg[..idx]);
                var val = Uri.UnescapeDataString(seg[(idx + 1)..]);
                dict[key] = val;
            }
            return dict;
        }

        /// <summary>规范化数据地址</summary>
        static string NormalizeDataUrl(string raw)
        {
            if (raw.IsEmpty())
                return string.Empty;
            if (raw.Contains("://"))
                return raw;

            var path = raw.StartsWith("/") ? raw : "/" + raw;
            if (!Asp.IsWeb)
                return path;
            return Asp.Host.TrimEnd('/') + path;
        }

        /// <summary>解析标签名</summary>
        static List<string> ParseTagNames(string tagNames)
        {
            if (tagNames.IsEmpty())
                return new List<string>();
            return tagNames
                .Split(new[] { ',', '，', ';', '；', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct()
                .ToList();
        }

        /// <summary>读取整数值</summary>
        static int GetIntValue(JsonElement node, int defaultValue)
        {
            try
            {
                if (node.ValueKind == JsonValueKind.Number)
                    return node.GetInt32();
                if (node.ValueKind == JsonValueKind.String && int.TryParse(node.GetString(), out var val))
                    return val;
            }
            catch
            {
            }
            return defaultValue;
        }

        /// <summary>解析数据数量</summary>
        static int ParseDataCount(string text)
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;

            if (TryReadDataCount(root, out var directCnt))
                return directCnt;

            var code = root.TryGetProperty("code", out var codeNode)
                ? GetIntValue(codeNode, 0)
                : 0;
            if (code != 0)
            {
                var msg = root.TryGetProperty("msg", out var msgNode)
                    ? msgNode.GetString()
                    : root.TryGetProperty("message", out var msgNode2) ? msgNode2.GetString() : "接口返回失败";
                throw new Exception(msg.IsEmpty() ? "接口返回失败" : msg);
            }

            if (!root.TryGetProperty("data", out var dataNode))
                return 0;

            return TryReadDataCount(dataNode, out var cnt) ? cnt : 0;
        }

        /// <summary>尝试读取返回数据总数</summary>
        static bool TryReadDataCount(JsonElement node, out int cnt)
        {
            cnt = 0;
            if (node.ValueKind == JsonValueKind.Array)
            {
                cnt = node.GetArrayLength();
                return true;
            }
            if (node.ValueKind != JsonValueKind.Object)
                return false;

            if (node.TryGetProperty("pageInfo", out var pageInfoNode) && pageInfoNode.ValueKind == JsonValueKind.Object)
            {
                if (pageInfoNode.TryGetProperty("total", out var totalNode))
                {
                    cnt = GetIntValue(totalNode, 0);
                    return true;
                }
            }

            if (node.TryGetProperty("pager", out var pagerNode) && pagerNode.ValueKind == JsonValueKind.Object)
            {
                if (pagerNode.TryGetProperty("total", out var totalNode))
                {
                    cnt = GetIntValue(totalNode, 0);
                    return true;
                }
            }

            if (node.TryGetProperty("total", out var dataTotalNode))
            {
                cnt = GetIntValue(dataTotalNode, 0);
                return true;
            }
            if (node.TryGetProperty("items", out var itemsNode) && itemsNode.ValueKind == JsonValueKind.Array)
            {
                cnt = itemsNode.GetArrayLength();
                return true;
            }
            if (node.TryGetProperty("list", out var listNode) && listNode.ValueKind == JsonValueKind.Array)
            {
                cnt = listNode.GetArrayLength();
                return true;
            }
            return false;
        }

   
    }
}
