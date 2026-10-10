using System;
using System.Collections.Generic;
using System.Linq;
using App.Components;
using App.DAL;
using App.Entities;
using App.Utils;
using Microsoft.AspNetCore.Mvc;
using App.EleUI;

namespace App.Pages.KB
{
    [Auth(Power.KbMenuView, Power.KbMenuEdit)]
    public class MenuFormModel : AuthModel
    {
        public KbMenu Item { get; set; }
        public List<KbMenu> MenuTree { get; set; }

        public void OnGet(long? parentId)
        {
            this.MenuTree = BuildAuthorizedMenuTree();
            Item = new KbMenu { ParentId = parentId };
        }

        public IActionResult OnGetData(long id, long? selectId)
        {
            var item = KbMenu.GetDetail(id);
            if (item == null)
            {
                item = new KbMenu();
                item.ParentId = selectId;
            }
            return BuildResult(0, "success", item);
        }

        public IActionResult OnPostSave([FromBody] KbMenu req)
        {
            if (req == null)
                return BuildResult(400, "参数错误");
            if (req.Name.IsEmpty())
                return BuildResult(400, "名称不能为空");

            var cu = Auth.GetUser();
            var isAdmin = Auth.IsAdmin(cu);

            // 越权校验：非 admin 时，新目录的 OrgId 必须在自己授权子树中或为 null（公共目录）
            if (!isAdmin)
            {
                if (req.OrgId.HasValue && req.OrgId.Value > 0)
                {
                    if (!OrgFilter.IsAuth(cu?.EffectiveAuthOrgId, req.OrgId.Value))
                        return BuildResult(403, $"无权将目录归属于该组织（OrgId={req.OrgId}）");
                }
                // 非 admin 禁止把目录设置为公共（OrgId=null），避免把内部内容暴露给全系统
                if (!req.OrgId.HasValue)
                    return BuildResult(403, "普通用户只能将目录归属到自己的授权组织");
            }

            var item = req.Id > 0 ? KbMenu.Get(req.Id) : new KbMenu();
            if (req.Id > 0 && item == null)
                return BuildResult(404, "目录不存在");
            if (req.ParentId == req.Id)
                return BuildResult(400, "上级目录不能是自己");

            // 非 admin：编辑已有目录时，必须原本就在自己 scope 中
            if (!isAdmin && req.Id > 0)
            {
                if (item.OrgId.HasValue)
                {
                    if (!OrgFilter.IsAuth(cu?.EffectiveAuthOrgId, item.OrgId.Value))
                        return BuildResult(403, "无权编辑该目录");
                }
            }

            item.Name = req.Name;
            item.SortId = req.SortId;
            item.ParentId = req.ParentId;
            item.OrgId = req.OrgId;
            item.Save();
            KbMenu.ClearCache();
            return BuildResult(0, "保存成功");
        }

        // 工具：构建当前用户可见的目录树（同 Manager 一致）
        private List<KbMenu> BuildAuthorizedMenuTree()
        {
            var cu = Auth.GetUser();
            var all = KbMenu.Set.OrderBy(m => m.SortId).ThenBy(m => m.Id).ToList();
            if (Auth.IsAdmin(cu))
                return all.ToTree();

            var authId = cu?.EffectiveAuthOrgId;
            List<KbMenu> visible;
            if (!authId.HasValue || authId.Value <= 0)
                visible = all.Where(m => m.OrgId == null).ToList();
            else
            {
                var orgIds = OrgFilter.GetAuthOrgIds(authId.Value);
                visible = all.Where(m =>
                    m.OrgId == null ||
                    (m.OrgId.HasValue && orgIds.Contains(m.OrgId.Value))
                ).ToList();
            }
            var visibleMap = visible.ToDictionary(m => m.Id, m => m);
            var allMap = all.ToDictionary(m => m.Id, m => m);
            foreach (var node in visible.ToList())
            {
                var cur = node;
                while (cur != null)
                {
                    visibleMap[cur.Id] = cur;
                    if (cur.ParentId == null) break;
                    if (!allMap.TryGetValue(cur.ParentId.Value, out cur)) break;
                }
            }
            return visibleMap.Values.ToList().ToTree();
        }
    }
}

