using System;
using System.Collections.Generic;

namespace App.Pages.EleUISamples
{
    /// <summary>
    /// Lightweight uploader used by EleUI samples.
    /// It keeps demo handlers independent from App runtime components.
    /// </summary>
    public static class Uploader
    {
        public static List<string> SaveFiles(string folderName, List<string> urlOrDatas)
        {
            var urls = new List<string>();
            if (urlOrDatas == null)
                return urls;

            foreach (var urlOrData in urlOrDatas)
            {
                urls.Add(SaveFile(folderName, urlOrData));
            }

            return urls;
        }

        public static string SaveFile(string folderName, string urlOrData)
        {
            if (string.IsNullOrWhiteSpace(urlOrData))
                return string.Empty;

            // For demo pages, keep plain URLs unchanged.
            if (!urlOrData.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
                return urlOrData;

            // Return a deterministic virtual path for base64 image payloads in sample flows.
            return $"/Files/{folderName}/{Guid.NewGuid():N}.png";
        }
    }
}
