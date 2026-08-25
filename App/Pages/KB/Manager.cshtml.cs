using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using App.Components;
using App.DAL;
using App.Entities;
using App.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;

namespace App.Pages.KB
{
    //[Auth(Power.KbMenuView)]
    [RequestSizeLimit(2147483648)]        // 2GB
    [RequestFormLimits(MultipartBodyLengthLimit = 2147483648, ValueCountLimit = 4096)]
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
        /// <summary>删除目录（递归删除所有子目录和关联附件）</summary>
        public IActionResult OnPostDeleteMenu([FromBody] JsonElement payload)
        {
            if (!CheckPower(Power.KbMenuDelete))
                return OkBuildResult(403, "无权操作");

            long id = ExtractId(payload, 0);
            if (id <= 0)
                return OkBuildResult(400, "请先选择要删除的目录");

            var menu = KbMenu.Get(id);
            if (menu == null)
                return OkBuildResult(404, "目录不存在");

            var descendantIds = KbMenu.GetChildIds(id);
            if (descendantIds == null || descendantIds.Count == 0)
                descendantIds = new List<long> { id };
            else if (!descendantIds.Contains(id))
                descendantIds.Insert(0, id);

            var deleteCount = descendantIds.Count;
            var attKeys = descendantIds.Select(i => $"KbMenu-{i}").ToList();
            var atts = Att.Set.Where(a => attKeys.Contains(a.Key)).ToList();
            var fileCount = 0;
            foreach (var att in atts)
            {
                try
                {
                    var content = att.Content ?? string.Empty;
                    if (content.StartsWith("/") || content.StartsWith("~/") || content.StartsWith("."))
                    {
                        var phyPath = App.Web.Asp.MapPath(content);
                        if (System.IO.File.Exists(phyPath))
                        {
                            System.IO.File.Delete(phyPath);
                        }
                    }
                }
                catch { }
                try { Att.Delete(att.Id); } catch { }
                fileCount++;
            }

            descendantIds.Reverse();
            foreach (var menuId in descendantIds)
            {
                try { KbMenu.Delete(menuId); } catch { }
            }

            KbMenu.ClearCache();
            var tree = KbMenu.GetTree();
            long nextCurrent = 0;
            return OkBuildResult(0, $"删除成功（共{deleteCount}个目录，{fileCount}个文件）", new
            {
                reload = false,
                refreshTree = true,
                tree,
                managerMenuTree = tree,
                managerCurrentMenuId = (long?)nextCurrent,
                nextMenuId = (long?)nextCurrent,
                deletedId = id,
                deletedIds = descendantIds,
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

        /// <summary>按本地目录结构批量导入知识库目录和附件。</summary>
        public IActionResult OnPostUploadDirectory(long? menuId)
        {
            if (!CheckPower(Power.KbMenuEdit) && !CheckPower(Power.KbMenuNew))
                return OkBuildResult(403, "无权操作");

            var files = Request?.Form?.Files;
            if (files == null || files.Count == 0)
                return OkBuildResult(400, "请选择要上传的目录");

            var targetParent = menuId > 0 ? KbMenu.Get(menuId.Value) : null;
            if (menuId > 0 && targetParent == null)
                return OkBuildResult(404, "目标目录不存在");

            var relativePathsRaw = Request.Form["relativePaths"].ToList();
            if (relativePathsRaw.Count != files.Count)
                return OkBuildResult(400, "目录结构信息不完整，请重新选择目录");

            var filePairs = new List<(IFormFile file, string path)>();
            for (var i = 0; i < files.Count; i++)
            {
                var f = files[i];
                if (f == null) continue;
                var p = NormalizeRelativePath(relativePathsRaw[i]);
                if (p.IsEmpty()) p = NormalizeRelativePath(f.FileName);
                filePairs.Add((f, p));
            }
            filePairs = filePairs
                .OrderBy(t => t.path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var allMenus = KbMenu.Set.OrderBy(t => t.SortId).ThenBy(t => t.Id).ToList();
            var menuCache = allMenus.ToDictionary(BuildMenuKey, t => t);
            var siblingSortSeed = new Dictionary<long, int>();
            var fileGroupKeys = new Dictionary<long, List<string>>();

            var importedFileCount = 0;
            var importedMenuIds = new List<long>();
            long? importedRootMenuId = null;
            var pendingFileAtts = new List<(long parentId, IFormFile file, string fileName)>();

            foreach (var (file, relativePath) in filePairs)
            {
                if (file == null || file.Length <= 0 || relativePath.IsEmpty())
                    continue;

                var segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
                if (segments.Count == 0)
                    continue;

                var fileName = segments.Last();
                segments.RemoveAt(segments.Count - 1);

                long? parentId = targetParent?.Id;
                foreach (var segment in segments)
                {
                    var parentMenu = parentId > 0 ? KbMenu.Get(parentId.Value) : null;
                    var child = GetOrCreateChildMenu(parentId, segment, parentMenu, menuCache, siblingSortSeed);
                    importedMenuIds.Add(child.Id);
                    if (importedRootMenuId == null)
                        importedRootMenuId = child.Id;
                    parentId = child.Id;
                }

                if (parentId == null && importedRootMenuId == null && targetParent != null)
                    importedRootMenuId = targetParent.Id;

                var groupParent = parentId ?? 0;
                if (!fileGroupKeys.ContainsKey(groupParent))
                    fileGroupKeys[groupParent] = new List<string>();
                fileGroupKeys[groupParent].Add(fileName);
                pendingFileAtts.Add((groupParent, file, fileName));
            }

            var fileSortStart = new Dictionary<long, int>();
            foreach (var gk in fileGroupKeys.Keys.ToList())
            {
                var startId = (Att.Set
                    .Where(t => t.Key == $"KbMenu-{gk}")
                    .Select(t => (int?)t.SortId).Max() ?? 0);
                fileSortStart[gk] = startId;
            }
            var fileGroupCursor = fileGroupKeys.ToDictionary(k => k.Key, _ => 0);

            var processedGroupIndex = new Dictionary<long, int>();
            foreach (var (parentId, file, fileName) in pendingFileAtts)
            {
                var pid = parentId;
                if (!processedGroupIndex.ContainsKey(pid)) processedGroupIndex[pid] = 0;
                processedGroupIndex[pid]++;
                var nextSortId = fileSortStart.ContainsKey(pid)
                    ? fileSortStart[pid] + processedGroupIndex[pid]
                    : processedGroupIndex[pid];

                var folder = nameof(KbMenu);
                var url = Uploader.SaveFile(folder, file);
                new Att
                {
                    Key = $"KbMenu-{pid}",
                    Content = url,
                    FileName = fileName.IsNotEmpty() ? fileName : file.FileName,
                    SortId = nextSortId,
                    Protect = true,
                    FileSize = file.Length
                }.Save();
                importedFileCount++;
            }

            KbMenu.ClearCache();
            var tree = KbMenu.GetTree();
            var nextMenuId = importedRootMenuId ?? targetParent?.Id ?? 0;
            return OkBuildResult(0, $"导入成功，共{importedFileCount}个文件", new
            {
                refreshTree = true,
                tree,
                managerMenuTree = tree,
                managerCurrentMenuId = (long?)nextMenuId,
                nextMenuId = (long?)nextMenuId,
                importedFileCount,
                importedMenuCount = importedMenuIds.Distinct().Count()
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

        /// <summary>按父级和名称生成目录缓存键。</summary>
        private static string BuildMenuKey(KbMenu menu)
        {
            return BuildMenuKey(menu?.ParentId, menu?.Name);
        }

        /// <summary>按父级和名称生成目录缓存键。</summary>
        private static string BuildMenuKey(long? parentId, string name)
        {
            var normalizedName = (name ?? string.Empty).Trim().ToLowerInvariant();
            return $"{parentId ?? 0}|{normalizedName}";
        }

        /// <summary>获取或创建子目录。</summary>
        private static KbMenu GetOrCreateChildMenu(
            long? parentId,
            string name,
            KbMenu parentMenu,
            Dictionary<string, KbMenu> menuCache,
            Dictionary<long, int> siblingSortSeed)
        {
            var normalizedName = (name ?? string.Empty).Trim();
            var key = BuildMenuKey(parentId, normalizedName);
            if (menuCache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var groupKey = parentId ?? 0;
            if (!siblingSortSeed.ContainsKey(groupKey))
            {
                var existingSiblings = KbMenu.Set
                    .Where(m => m.ParentId == parentId)
                    .OrderBy(m => m.SortId).ThenBy(m => m.Id)
                    .ToList();
                var existingMax = existingSiblings.Count == 0
                    ? 0
                    : existingSiblings.Max(m => m.SortId);
                siblingSortSeed[groupKey] = Math.Max(existingMax, existingSiblings.Count);
            }
            siblingSortSeed[groupKey]++;

            var menu = new KbMenu
            {
                ParentId = parentId,
                Name = normalizedName,
                OrgId = parentMenu?.OrgId,
                SortId = siblingSortSeed[groupKey]
            };
            menu.Save();
            menuCache[key] = menu;
            return menu;
        }

        /// <summary>规范化浏览器上传的相对路径。</summary>
        private static string NormalizeRelativePath(string relativePath)
        {
            var path = (relativePath ?? string.Empty).Trim();
            if (path.IsEmpty())
                return string.Empty;
            path = path.Replace('\\', '/');
            while (path.Contains("//"))
                path = path.Replace("//", "/");
            return path.Trim('/');
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
