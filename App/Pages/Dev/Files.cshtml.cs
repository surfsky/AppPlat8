using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using App.Components;
using App.Utils;

namespace App.Pages.Dev
{
    public class FilesModel : AdminModel
    {
        public List<FileItem> Items { get; set; } = new();

        public void OnGet()
        {
            var root = Path.Combine(Directory.GetCurrentDirectory(), "Files");
            var samplesRoot = Path.Combine(root, "Samples");
            if (!Directory.Exists(samplesRoot))
                Directory.CreateDirectory(samplesRoot);

            Items = Directory
                .GetFiles(samplesRoot, "*", SearchOption.AllDirectories)
                .Select(path => new FileItem
                {
                    Name = Path.GetFileName(path),
                    RelativePath = ("Samples/" + Path.GetRelativePath(samplesRoot, path)).Replace('\\', '/'),
                    Extension = Path.GetExtension(path).TrimStart('.').ToLower(),
                    SizeText = new FileInfo(path).Length.ToSizeText()
                })
                .OrderBy(t => t.RelativePath)
                .ToList();
        }

        public class FileItem
        {
            public string Name { get; set; }
            public string RelativePath { get; set; }
            public string Extension { get; set; }
            public string SizeText { get; set; }
        }
    }
}
