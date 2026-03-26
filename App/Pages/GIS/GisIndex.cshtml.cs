using System;
using System.Collections.Generic;
using System.Linq;
using App.DAL;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace App.Pages.Gis
{
    [AllowAnonymous]
    public class GisIndexModel : PageModel
    {
        public void OnGet()
        {
        }

        public JsonResult OnGetLayerData()
        {
            var tagLookup = CheckObjectTag.IncludeSet
                .Select(t => new
                {
                    t.CheckObjectId,
                    TagName = t.Tag != null ? t.Tag.Name : null
                })
                .ToList()
                .Where(t => !string.IsNullOrWhiteSpace(t.TagName))
                .GroupBy(t => t.CheckObjectId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.TagName).Distinct().ToList());

            var objects = CheckObject.Set
                .Where(o => !string.IsNullOrWhiteSpace(o.Gps))
                .Select(o => new
                {
                    o.Id,
                    o.Name,
                    o.Gps,
                    o.Address,
                    o.SocialCreditCode
                })
                .ToList();

            var list = new List<object>();
            foreach (var item in objects)
            {
                if (!TryParseGps(item.Gps, out var lng, out var lat))
                    continue;

                tagLookup.TryGetValue(item.Id, out var tags);
                list.Add(new
                {
                    id = item.Id,
                    name = item.Name,
                    lat,
                    lng,
                    address = item.Address,
                    socialCreditCode = item.SocialCreditCode,
                    tags = tags ?? new List<string>()
                });
            }

            return new JsonResult(new { code = 0, data = list });
        }

        private static bool TryParseGps(string gps, out double lng, out double lat)
        {
            lng = 0;
            lat = 0;
            if (string.IsNullOrWhiteSpace(gps))
                return false;

            var text = gps.Replace("，", ",").Trim();
            var parts = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length < 2)
                return false;

            if (!double.TryParse(parts[0], out lng))
                return false;
            if (!double.TryParse(parts[1], out lat))
                return false;

            return true;
        }
    }
}
