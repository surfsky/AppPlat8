using App.Components;
using App.DAL;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Text.RegularExpressions;

namespace App.Pages.Shared
{
    [CheckPower(Power.CheckObjectView)]
    public class LocatorModel : AdminModel
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
                var normalized = gps.Replace('，', ',').Replace('；', ',').Replace(';', ',').Trim();
                normalized = Regex.Replace(normalized, "\\s+", ",");
                var parts = normalized.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
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
