using System;
using System.Collections.Generic;
using System.Linq;
using App.Components;
using App.DAL;
using App.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;

namespace App.Pages.KB
{
    //[Auth(Power.KbMenuView)]
    public class ManagerModel : AdminModel
    {
        public List<KbMenu> MenuTree { get; set; }

        [BindProperty]
        public long? CurrentMenuId { get; set; }

        public string DefaultAttsUrl { get; set; }

        public void OnGet(long? menuId)
        {
            MenuTree = KbMenu.GetTree();
            CurrentMenuId = menuId;
            DefaultAttsUrl = BuildAttsUrl(menuId);
        }

        private static string BuildAttsUrl(long? menuId)
        {
            if (!menuId.HasValue || menuId.Value <= 0)
                return string.Empty;

            var menu = KbMenu.Get(menuId.Value);
            if (menu == null)
                return string.Empty;

            var uniId = $"KbMenu-{menuId.Value}";
            var nameEncoded = System.Net.WebUtility.UrlEncode(menu.Name ?? string.Empty);
            return $"/Shared/Atts?uniId={System.Net.WebUtility.UrlEncode(uniId)}&name={nameEncoded}";
        }



        //-------------------------------------------------------------------
        // 工具栏操作
        //-------------------------------------------------------------------
        /// <summary>删除目录</summary>
        public IActionResult OnPostDeleteMenu([FromBody] JsonElement payload)
        {
            if (!CheckPower(Power.KbMenuDelete))
                return OkBuildResult(403, "无权操作");

            long id = ExtractId(payload, 0);
            if (id <= 0)
                return OkBuildResult(400, "请先选择要删除的目录");

            if (KbMenu.Set.Any(x => x.ParentId == id))
                return OkBuildResult(400, "存在下级目录，无法删除");

            KbMenu.Delete(id);
            KbMenu.ClearCache();
            var tree = KbMenu.GetTree();
            long nextCurrent = 0;
            return OkBuildResult(0, "删除成功", new
            {
                reload = false,
                refreshTree = true,
                tree,
                managerMenuTree = tree,
                managerCurrentMenuId = (long?)nextCurrent,
                nextMenuId = (long?)nextCurrent,
                deletedId = id,
                forceReloadIframe = true
            });
        }

        /// <summary>移动目录上</summary>
        public IActionResult OnPostMenuMoveUp([FromBody] JsonElement payload)
        {
            if (!CheckPower(Power.KbMenuEdit))
                return OkBuildResult(403, "无权操作");

            long id = ExtractId(payload, 0);
            if (id <= 0)
                return OkBuildResult(400, "请先选择目录");

            var item = KbMenu.Get(id);
            if (item == null)
                return OkBuildResult(404, "目录不存在");

            var siblings = KbMenu.Set.Where(x => x.ParentId == item.ParentId)
                .OrderBy(x => x.SortId).ThenBy(x => x.Id).ToList();
            var idx = siblings.FindIndex(x => x.Id == id);
            var moved = false;
            if (idx > 0)
            {
                var prev = siblings[idx - 1];
                // 若 SortId 相同（典型都为 0）：先归一化整组兄弟节点 SortId 为唯一值后再交换
                if (NormalizeSiblingSortIdsIfNeeded(siblings, item, prev))
                {
                    KbMenu.ClearCache();
                    siblings = KbMenu.Set.Where(x => x.ParentId == item.ParentId)
                        .OrderBy(x => x.SortId).ThenBy(x => x.Id).ToList();
                    idx = siblings.FindIndex(x => x.Id == id);
                    if (idx <= 0)
                    {
                        var treeEmpty = KbMenu.GetTree();
                        return OkBuildResult(0, "已经是第一个", new
                        {
                            reload = false,
                            refreshTree = true,
                            tree = treeEmpty,
                            managerMenuTree = treeEmpty,
                            managerCurrentMenuId = (long?)id,
                            nextMenuId = (long?)id,
                            moved = false,
                            boundary = true,
                            keepSelection = true,
                            forceReloadIframe = false
                        });
                    }
                    prev = siblings[idx - 1];
                }
                (item.SortId, prev.SortId) = (prev.SortId, item.SortId);
                item.Save();
                prev.Save();
                moved = true;
            }
            KbMenu.ClearCache();
            var tree = KbMenu.GetTree();
            return OkBuildResult(0, moved ? "上移成功" : "已经是第一个", new
            {
                reload = false,
                refreshTree = true,
                tree,
                managerMenuTree = tree,
                managerCurrentMenuId = (long?)id,
                nextMenuId = (long?)id,
                moved,
                boundary = idx <= 0,
                keepSelection = true,
                forceReloadIframe = false
            });
        }

        /// <summary>移动目录下</summary>
        public IActionResult OnPostMenuMoveDown([FromBody] JsonElement payload)
        {
            if (!CheckPower(Power.KbMenuEdit))
                return OkBuildResult(403, "无权操作");

            long id = ExtractId(payload, 0);
            if (id <= 0)
                return OkBuildResult(400, "请先选择目录");

            var item = KbMenu.Get(id);
            if (item == null)
                return OkBuildResult(404, "目录不存在");

            var siblings = KbMenu.Set.Where(x => x.ParentId == item.ParentId)
                .OrderBy(x => x.SortId).ThenBy(x => x.Id).ToList();
            var idx = siblings.FindIndex(x => x.Id == id);
            var moved = false;
            if (idx >= 0 && idx < siblings.Count - 1)
            {
                var next = siblings[idx + 1];
                // 若 SortId 相同（典型都为 0）：先归一化整组兄弟节点 SortId 为唯一值后再交换
                if (NormalizeSiblingSortIdsIfNeeded(siblings, item, next))
                {
                    KbMenu.ClearCache();
                    siblings = KbMenu.Set.Where(x => x.ParentId == item.ParentId)
                        .OrderBy(x => x.SortId).ThenBy(x => x.Id).ToList();
                    idx = siblings.FindIndex(x => x.Id == id);
                    if (idx >= siblings.Count - 1)
                    {
                        var treeEmpty = KbMenu.GetTree();
                        return OkBuildResult(0, "已经是最后一个", new
                        {
                            reload = false,
                            refreshTree = true,
                            tree = treeEmpty,
                            managerMenuTree = treeEmpty,
                            managerCurrentMenuId = (long?)id,
                            nextMenuId = (long?)id,
                            moved = false,
                            boundary = true,
                            keepSelection = true,
                            forceReloadIframe = false
                        });
                    }
                    next = siblings[idx + 1];
                }
                (item.SortId, next.SortId) = (next.SortId, item.SortId);
                item.Save();
                next.Save();
                moved = true;
            }
            KbMenu.ClearCache();
            var tree2 = KbMenu.GetTree();
            var isLast = idx >= siblings.Count - 1;
            return OkBuildResult(0, moved ? "下移成功" : "已经是最后一个", new
            {
                reload = false,
                refreshTree = true,
                tree = tree2,
                managerMenuTree = tree2,
                managerCurrentMenuId = (long?)id,
                nextMenuId = (long?)id,
                moved,
                boundary = isLast,
                keepSelection = true,
                forceReloadIframe = false
            });
        }

        // 排序前归一化：如果当前节点与目标邻居的 SortId 相同（典型是都为 0），
        // 则按当前顺序把整组兄弟节点的 SortId 先设为各自 Id（保证唯一且排序稳定），再做交换
        private static bool NormalizeSiblingSortIdsIfNeeded(
            List<KbMenu> orderedSiblings,
            KbMenu current,
            KbMenu targetNeighbor)
        {
            if (current == null || targetNeighbor == null) return false;
            if (current.SortId != targetNeighbor.SortId) return false;
            if (orderedSiblings == null || orderedSiblings.Count == 0) return false;

            var changed = false;
            foreach (var s in orderedSiblings)
            {
                if (s == null) continue;
                int wantId;
                if (s.Id > 0 && s.Id <= int.MaxValue) wantId = (int)s.Id;
                else wantId = (Math.Abs(s.GetHashCode()) & int.MaxValue) | 1;
                if (s.SortId != wantId)
                {
                    s.SortId = wantId;
                    s.Save();
                    changed = true;
                }
            }
            return changed;
        }

        //----------------------------------------------------------------------
        // Utils
        //----------------------------------------------------------------------
        // 封装 BuildResult：无论成功失败，都强制 HTTP 200，
        // 让 axios 走 .then 分支，确保前端能直接读到 body.code/msg/data，
        // 避免 BaseModel.BuildResult 把非 0 code 转成 4xx/5xx 导致 axios catch，丢失具体原因。
        private static JsonResult OkBuildResult(int code, string message, object data = null)
        {
            var json = BuildResult(code, message, data);
            json.StatusCode = 200;
            return json;
        }

        /// <summary>从 JSON 中提取 Id</summary>
        private static long ExtractId(JsonElement payload, long fallback = 0)
        {
            if (payload.TryGetProperty("id", out var idEl) && long.TryParse(idEl.ToString(), out var id1))
                return id1;
            if (payload.TryGetProperty("value", out var valEl))
            {
                if (valEl.ValueKind == JsonValueKind.Object)
                {
                    if (valEl.TryGetProperty("id", out var idEl2) && long.TryParse(idEl2.ToString(), out var id2))
                        return id2;
                }
                else if (long.TryParse(valEl.ToString(), out var id0))
                    return id0;
            }
            return fallback;
        }


    }
}
