using System.Collections.Generic;
using System.IO;
using System.Linq;
using App.Components;
using App.DAL;
using App.DAL.GIS;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.GIS
{
    [CheckPower(Power.GisGeometryEdit)]
    public class TyphoonImporterModel : AdminModel
    {
        public string TypeTitle { get; set; } = "台风数据文件";
        public string MetaFileName { get; set; } = "typhoons.json";

        /// <summary>初始化</summary>
        public void OnGet()
        {
        }

        /// <summary>执行导入</summary>
        public IActionResult OnPostImport()
        {
            var logs = new List<string>();
            var files = Request?.Form?.Files?.ToList() ?? new();
            if (files.Count == 0)
                return BuildFailResult("请先选择要导入的文件", logs);

            try
            {
                var list = new List<GisTyphoonImportFile>();
                foreach (var file in files)
                {
                    if (file == null || file.Length <= 0)
                        continue;
                    using var reader = new StreamReader(file.OpenReadStream());
                    list.Add(new GisTyphoonImportFile(file.FileName, reader.ReadToEnd()));
                }
                if (list.Count == 0)
                    return BuildFailResult("未读取到有效文件", logs);

                var result = GisTyphoonImporter.ImportFiles(Common.GetDbConnection(), list, 2016);
                return BuildResult(0, "导入成功", new
                {
                    success = result.TyphoonAddCnt + result.TyphoonEditCnt,
                    typhoonAddCnt = result.TyphoonAddCnt,
                    typhoonEditCnt = result.TyphoonEditCnt,
                    logAddCnt = result.LogAddCnt,
                    logDeleteCnt = result.LogDeleteCnt,
                    fileCnt = result.FileCnt,
                    logs = result.Logs
                });
            }
            catch (System.Exception ex)
            {
                logs.Add($"导入失败: {ex.Message}");
                return BuildFailResult("导入失败", logs);
            }
        }

        /// <summary>构造失败结果</summary>
        IActionResult BuildFailResult(string message, List<string> logs)
        {
            if (logs == null) logs = new List<string>();
            if (message?.Length > 0 && (logs.Count == 0 || logs[^1] != message))
                logs.Add(message);
            return BuildResult(400, message, new { logs });
        }
    }
}
