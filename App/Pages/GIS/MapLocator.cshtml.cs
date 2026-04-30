using App.Components;
using App.DAL;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.Shared
{
    [CheckPower(Power.CheckObjectView)]
    public class MapLocatorModel : AdminModel
    {
        [BindProperty(SupportsGet = true)]
        public string Gps { get; set; }

        [BindProperty(SupportsGet = true)]
        public double? Lng { get; set; }

        [BindProperty(SupportsGet = true)]
        public double? Lat { get; set; }

        public void OnGet(string gps, double? lng, double? lat)
        {
            Gps = gps;
            Lng = lng;
            Lat = lat;

            if (!string.IsNullOrWhiteSpace(gps))
            {
                var parts = gps.Split(',');
                if (parts.Length >= 2
                    && double.TryParse(parts[0].Trim(), out var lngVal)
                    && double.TryParse(parts[1].Trim(), out var latVal))
                {
                    Lng = lngVal;
                    Lat = latVal;
                }
            }
        }
    }
}
