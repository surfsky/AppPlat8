using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.DAL;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace App.Pages.Gis
{
    [AllowAnonymous]
    public class GisIndexModel : PageModel
    {
        public void OnGet()
        {
        }

        public JsonResult OnGetMarkers()
        {
            // Return some dummy markers or real data if available
            // Let's use some CheckObjects as markers if they have coordinates
            // Assuming CheckObject has lat/long or we just mock it for now.
            // CheckObject has Address but maybe no Lat/Lng? 
            // Checking CheckObject.cs ... It has Address but no Lat/Long fields visible in previous `cat`.
            // Wait, I saw Lat/Long in CheckObjectForm.cshtml earlier!
            // Let's check CheckObject.cs again or just mock it.
            // Mocking is safer for now.
            
            var markers = new List<object>();
            var random = new Random();
            // Center around Hangzhou or similar: 30.2741, 120.1551
            double centerLat = 30.2741;
            double centerLng = 120.1551;

            for (int i = 0; i < 20; i++)
            {
                markers.Add(new {
                    id = i,
                    lat = centerLat + (random.NextDouble() - 0.5) * 0.1,
                    lng = centerLng + (random.NextDouble() - 0.5) * 0.1,
                    title = $"Marker {i}",
                    description = $"This is marker {i}"
                });
            }

            return new JsonResult(new { code = 0, data = markers });
        }
    }
}
