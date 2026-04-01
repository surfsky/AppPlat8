using System;
using System.Linq;
using App.Entities;
using App.Utils;

namespace App.DAL
{
    /// <summary>
    /// AI 接口配置（OpenAI 兼容）
    /// </summary>
    [UI("配置", "AI配置")]
    public class AIConfig : EntityBase<AIConfig>
    {
        [UI("名称")] public string Name { get; set; }
        [UI("接口地址")] public string BaseUrl { get; set; }
        [UI("API Key")] public string ApiKey { get; set; }
        [UI("模型")] public string Model { get; set; }
        [UI("支持服务")] public string Services { get; set; } = "chat";
        [UI("超时(秒)")] public int TimeoutSeconds { get; set; } = 60;
        [UI("是否默认")] public bool IsDefault { get; set; } = false;
        [UI("启用")] public bool InUsed { get; set; } = true;
        [UI("排序")] public int SortId { get; set; } = 0;
        [UI("备注")] public string Remark { get; set; }

        [UI("API Key(脱敏)")] public string ApiKeyMask => MaskKey(ApiKey);

        public static IQueryable<AIConfig> Search(string name, bool? inUsed)
        {
            var q = Set.AsQueryable();
            if (!string.IsNullOrWhiteSpace(name))
                q = q.Where(t => t.Name.Contains(name));
            if (inUsed != null)
                q = q.Where(t => t.InUsed == inUsed);
            return q.OrderByDescending(t => t.IsDefault).ThenBy(t => t.SortId).ThenBy(t => t.Id);
        }

        public static AIConfig GetDefault()
        {
            return Set
                .Where(t => t.InUsed)
                .OrderByDescending(t => t.IsDefault)
                .ThenBy(t => t.SortId)
                .ThenBy(t => t.Id)
                .FirstOrDefault();
        }

        public override object Export(ExportMode type = ExportMode.Normal)
        {
            return new
            {
                Id,
                Name,
                BaseUrl,
                ApiKey,
                ApiKeyMask,
                Model,
                Services,
                TimeoutSeconds,
                IsDefault,
                InUsed,
                SortId,
                Remark,
                CreateDt,
                UpdateDt
            };
        }

        private static string MaskKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return string.Empty;
            if (key.Length <= 8)
                return "****";
            return $"{key.Substring(0, 4)}****{key.Substring(key.Length - 4)}";
        }
    }
}