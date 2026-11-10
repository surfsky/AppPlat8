using System.Linq;
using App.Components;
using App.DAL;
using App.Entities;
using App.HttpApi;
using App.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace App.Pages.Checks
{
    [Auth(Power.CheckTaskView)]
    public class CheckTaskObjectsModel : AuthModel
    {
        public CheckTaskObject Item { get; set; } = new CheckTaskObject();
        public string CheckerName { get; set; }
        public string ScopeStatus { get; set; }

        public void OnGet(long? taskId, long? orgId, long? checkerId, string status)
        {
            Item.TaskId = taskId;
            Item.OrgId = orgId;
            Item.CheckerId = checkerId;
            ScopeStatus = status;
            Item.Status = ParseStatus(status);
            if (checkerId.GetValueOrDefault() > 0)
            {
                var checker = App.DAL.User.Get(checkerId.Value);
                CheckerName = checker?.RealName.IsNotEmpty() == true ? checker.RealName : checker?.Name;
            }
        }

        public IActionResult OnGetData(Paging pi, long? taskId, long? orgId, long? checkerId, string status)
        {
            pi ??= new Paging();
            if (pi.SortField.IsEmpty())
            {
                pi.SortField = "SortId";
                pi.SortDirection = "ASC";
            }

            IQueryable<CheckTaskObject> q = CheckTaskObject.Search(taskId: taskId, orgId: orgId, checkerId: checkerId)
                .Include(t => t.Task)
                .Include(t => t.CheckObject).ThenInclude(o => o.Org)
                .Include(t => t.CheckObject).ThenInclude(o => o.Checker)
                .Include(t => t.Org)
                .Include(t => t.Checker);
            var statusValue = ParseStatus(status);
            if (statusValue.HasValue)
                q = q.Where(t => t.Status == statusValue.Value);
            q = q.FilterByOrg(Auth.GetUser());

            var list = q.SortPageExport(pi);
            return BuildResult(0, "success", list, pi);
        }

        static CheckTaskObjectStatus? ParseStatus(string status)
        {
            if (string.Equals(status, "checked", System.StringComparison.OrdinalIgnoreCase))
                return CheckTaskObjectStatus.Checked;
            if (string.Equals(status, "fixed", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "finished", System.StringComparison.OrdinalIgnoreCase))
                return CheckTaskObjectStatus.Finished;
            return System.Enum.TryParse<CheckTaskObjectStatus>(status, true, out var value) ? value : null;
        }
    }
}
