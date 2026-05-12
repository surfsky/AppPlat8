using App.Components;
using App.DAL;
using App.Entities;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace App.Pages.AI
{
    [IgnoreAntiforgeryToken]
    [CheckPower(Power.AIChat)]
    public class VoiceWhisperModel : AdminModel
    {
        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostTranscribe([FromForm] VoiceTranscribeRequest req)
        {
            var result = await VoiceTranscribeHelper.TranscribeAsync(req);
            if (!result.Success)
                return BuildResult(result.Code, result.Message, result.Data);

            return BuildResult(0, "success", result.Data);
        }
    }
}
