using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using App.Components;
using App.DAL;
using App.HttpApi;
using App.Utils;
using App.Entities;
using App.EleUI;
using System.Linq.Expressions;
using App.Web;

namespace App.Pages.Admins
{
    [Auth(Permission.OrgView)]
    public class OrgsModel : AuthModel
    {
        public App.DAL.Org Item { get; set; }

        public void OnGet(){}

        //--------------------------------------------------------------------------------
        // 查询和导出
        //--------------------------------------------------------------------------------
        /// <summary>获取清单</summary>
        public async Task<IActionResult> OnGetData(string name)
        {
            List<DAL.Org> tree = Search(name);
            return BuildResult(0, "success", tree, null);
        }

        private static List<DAL.Org> Search(string name)
        {
            var allOrgs = App.DAL.Org.All; //await.Set.OrderBy(o => o.SortId).ToListAsync();
            if (!string.IsNullOrEmpty(name))
            {
                //allOrgs = allOrgs.Where(o => o.Name.Contains(name)).ToList();
                //allOrgs = allOrgs.FirstOrDefault(t => t.Name.Contains(name)).GetDescendants();
                var ids = allOrgs.Where(o => o.Name.Contains(name)).Select(t => t.Id).ToList();
                allOrgs = DAL.Org.GetDescendants(ids).ToList();
            }


            // Convert to nested structure
            var tree = allOrgs.ToTree();
            return tree;
        }


        // 导出列表到 Excel
        public IActionResult OnPostExport(Paging pi, string name)
        {
            var exportPi = new Paging { PageIndex = 1, PageSize = int.MaxValue, SortField = pi.SortField, SortDirection = pi.SortDirection }; // 导出所有匹配的数据（不分页）,保持与页面上相同的排序
            var list = Search(name);
            ExcelExporter.Export(list, $"组织_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
            return new EmptyResult();
        }

        //--------------------------------------------------------------------------------
        // 删除
        //--------------------------------------------------------------------------------
        /// <summary>删除</summary>
        public async Task<IActionResult> OnPostDelete([FromBody]long[] ids)
        {
            Logger.Info($"尝试删除组织: {string.Join(",", ids)}");
            if (ids == null || ids.Length == 0)
                return BuildResult(400, "参数错误");
            if (!HasPermission(Permission.OrgDelete))
                return BuildResult(403, "无权操作");

            foreach (var id in ids)
            {
                // Check users
                int userCount = await App.DAL.User.Set.Where(u => u.OrgId == id).CountAsync();
                if (userCount > 0) 
                    return BuildResult(400, $"删除失败！组织[{id}]下还有用户");
                    //return EleHandler.ShowToast($"删除失败！组织[{id}]下还有用户");

                // Check children
                int childCount = await App.DAL.Org.Set.Where(d => d.ParentId == id).CountAsync();
                if (childCount > 0) 
                    return BuildResult(400, $"删除失败！组织[{id}]下还有子组织");
                    //return EleHandler.ShowToast($"删除失败！组织[{id}]下还有子组织");

                var item = App.DAL.Org.Get(id);
                if (item != null)
                    item.Delete();
            }
            App.DAL.Org.ClearCache();
            return BuildResult(0, "删除成功");
        }

        //--------------------------------------------------------------------------------
        // 修正
        //--------------------------------------------------------------------------------
        /// <summary>补全所有组织全称</summary>
        public IActionResult OnPostFixAll()
        {
            Assert(HasPermission(Permission.OrgEdit), "无权操作");
            var cnt = App.DAL.Org.FixAll();
            return BuildResult(0, $"已补全 {cnt} 条组织全称");
        }

        //--------------------------------------------------------------------------------
        // 移动
        //--------------------------------------------------------------------------------
        public IActionResult OnPostMoveUp([FromBody] ElePayload payload)
        {
            Assert(payload?.SelectedCount == 1, "请先选择一行");
            var item = Exist(App.DAL.Org.Get(payload.FirstSelectedId.Value));
            Assert(item.MoveUp(), "已经是最顶层");
            return EleHandler.Toast("上移成功", NotifyType.Success).ReloadData().ToResult();
        }

        public IActionResult OnPostMoveDown([FromBody] ElePayload payload)
        {
            Assert(payload?.SelectedCount == 1, "请先选择一行");
            var item = Exist(App.DAL.Org.Get(payload.FirstSelectedId.Value));
            Assert(item.MoveDown(), "已经是最底层");
            return EleHandler.Toast("下移成功", NotifyType.Success).ReloadData().ToResult();
        }

        public IActionResult OnPostMoveFirst([FromBody] ElePayload payload)
        {
            Assert(payload?.SelectedCount == 1, "请先选择一行");
            var item = Exist(App.DAL.Org.Get(payload.FirstSelectedId.Value));
            Assert(item.MoveFirst(), "已经是最顶层");
            return EleHandler.Toast("到顶成功", NotifyType.Success).ReloadData().ToResult();
        }

        public IActionResult OnPostMoveLast([FromBody] ElePayload payload)
        {
            Assert(payload?.SelectedCount == 1, "请先选择一行");
            var item = Exist(App.DAL.Org.Get(payload.FirstSelectedId.Value));
            Assert(item.MoveLast(), "已经是最底层");
            return EleHandler.Toast("到底成功", NotifyType.Success).ReloadData().ToResult();
        }
    }
}