using App.EleUI;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.EleUISamples
{
    public class DialogModel : BaseModel
    {
        public void OnGet()
        {
        }

        public IActionResult OnPostDialogClosed([FromBody] DialogClosedCallbackRequest req)
        {
            var action = (req?.Action ?? string.Empty).Trim().ToLowerInvariant();
            return EleManager.ShowClientNotify($"服务端回调：Dialog 已关闭（action={action}）", NotifyType.Info, "Dialog Closed");
        }

        public class DialogClosedCallbackRequest
        {
            public string Action { get; set; }
        }
    }
}
