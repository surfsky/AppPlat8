using System.Collections.Generic;
using System.Linq;
using App.EleUI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace App.Pages.EleUISamples
{
    public class CitySelectDto
    {
        public string ProvinceId { get; set; }
        public string CityId { get; set; }
        public string CountyId { get; set; }
        // Alias for fluent-expression demo compatibility.
        public string CountId { get => CountyId; set => CountyId = value; }
        public bool CountyEnabled { get; set; } = true;
        public string RegionNodeId { get; set; }
    }

    
    public class CitySelectModel : BaseModel
    {
        [BindProperty]
        public CitySelectDto Item { get; set; }

        public List<SelectListItem> ProvinceOptions { get; set; } = new();
        public List<SelectListItem> CityOptions { get; set; } = new();
        public List<SelectListItem> CountyOptions { get; set; } = new();
        public List<TreeItem> RegionTree { get; set; } = new();


        private static readonly Dictionary<string, Dictionary<string, List<string>>> ProvinceData = new()
        {
            ["浙江省"] = new Dictionary<string, List<string>>
            {
                ["温州市"] = new List<string> { "鹿城区", "龙湾区", "瓯海区" },
                ["杭州市"] = new List<string> { "上城区", "西湖区", "滨江区" }
            },
            ["江苏省"] = new Dictionary<string, List<string>>
            {
                ["南京市"] = new List<string> { "玄武区", "秦淮区", "建邺区" },
                ["苏州市"] = new List<string> { "姑苏区", "吴中区", "相城区" }
            }
        };

        public void OnGet()
        {
            ProvinceOptions = ProvinceData.Keys
                .Select(x => new SelectListItem(x, x))
                .ToList();

            RegionTree = new List<TreeItem>
            {
                new TreeItem("east", "华东", new List<TreeItem>
                {
                    new TreeItem("zj", "浙江省"),
                    new TreeItem("js", "江苏省")
                }),
                new TreeItem("south", "华南", new List<TreeItem>
                {
                    new TreeItem("gd", "广东省")
                })
            };
        }

        public IActionResult OnGetData()
        {
            Item = new CitySelectDto();
            return BuildResult(0, "ok", Item);
        }

        public IActionResult OnPostProvinceChanged([FromBody] ControlChangeRequest req)
        {
            var province = req?.Value?.ToString() ?? string.Empty;
            var cities = ProvinceData.TryGetValue(province, out var cityMap)
                ? EleHelper.ToOptions(cityMap.Keys)
                : new List<ListItem>();

            return EleManager
                .SetControl<CitySelectDto>(t => t.CityId, Enabled: cities.Count > 0, Data: cities, Value: null)
                .SetControl<CitySelectDto>(t => t.CountyId, Enabled: false, Data: new List<ListItem>(), Value: null)
                .ToActionResult();
        }

        public IActionResult OnPostCityChanged([FromBody] ControlChangeRequest req)
        {
            var form = req?.Form ?? new Newtonsoft.Json.Linq.JObject();
            var province = (form["provinceId"]?.ToString()) ?? string.Empty;
            var city = req?.Value?.ToString() ?? string.Empty;

            var counties = ProvinceData.TryGetValue(province, out var cityMap) && cityMap.TryGetValue(city, out var countyList)
                ? EleHelper.ToOptions(countyList)
                : new List<ListItem>();

            return EleManager
                .SetControl<CitySelectDto>(t => t.CountyId, Enabled: counties.Count > 0, Data: counties, Value: null)
                .ToActionResult();
        }

        public IActionResult OnPostCountyEnabledChanged([FromBody] ControlChangeRequest req)
        {
            var enabled = false;
            if (req?.Value is bool boolValue)
                enabled = boolValue;
            else if (bool.TryParse(req?.Value?.ToString(), out var parsed))
                enabled = parsed;

            return EleManager
                .SetControl<CitySelectDto>(t => t.CountyId, Enabled: enabled)
                .ToActionResult();
        }

        public IActionResult OnPostRegionChanged([FromBody] ControlChangeRequest req)
        {
            var regionValue = req?.Value?.ToString() ?? string.Empty;
            var showCityCounty = regionValue == "zj" || regionValue == "js";

            return EleManager
                .SetControl<CitySelectDto>(t => t.ProvinceId, Visible: showCityCounty)
                .SetControl<CitySelectDto>(t => t.CityId, Visible: showCityCounty)
                .SetControl<CitySelectDto>(t => t.CountyId, Visible: showCityCounty)
                .ToActionResult();
        }


    }
}
