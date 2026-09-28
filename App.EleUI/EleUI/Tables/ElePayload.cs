using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace App.EleUI
{
    /// <summary>
    /// 筛选条件快照（EleTable 的 filters 对象）。
    /// </summary>
    public class FilterInfo
    {
        [JsonExtensionData] public Dictionary<string, object> Items { get; set; } = new();
    }

    /// <summary>
    /// 分页与排序快照。
    /// </summary>
    public class PageInfo
    {
        public string SortField { get; set; }
        public string SortDirection { get; set; }
        public int PageIndex { get; set; }
        public int PageSize { get; set; }
    }

    /// <summary>
    /// 勾选信息快照（EleTable 批量勾选项 Id 数组）。
    /// </summary>
    public class SelectInfo
    {
        public List<long> Ids { get; set; } = new();
        [JsonIgnore] public int Count => Ids?.Count ?? 0;
        [JsonIgnore] public long? FirstId => (Ids != null && Ids.Count > 0) ? Ids[0] : null;
    }

    /// <summary>
    /// 页面上下文信息（比如 URL 中的 uniId 等），便于后端不需要再从 Request.Query 中读。
    /// </summary>
    public class ContextInfo
    {
        public string UniId { get; set; }
        public string Url { get; set; }
        public string Path { get; set; }
    }


    /// <summary>
    /// EleButton 按钮 Payload 可传输的数据类型（Flags 枚举，可按位组合多种）。
    /// </summary>
    [Flags]
    public enum ElePayloadKinds
    {
        None = 0,
        FilterInfo = 1,
        PageInfo = 2,
        SelectInfo = 4,
        ContextInfo = 8,
    }

    /// <summary>ElePayloadKinds 常用组合常量</summary>
    public static class ElePayloadPreset
    {
        public const string All = "FilterInfo, PageInfo, SelectInfo, ContextInfo";
        public const string StandardPaging = "FilterInfo, PageInfo";
        public const string Move = "SelectInfo, ContextInfo";
    }


    /// <summary>
    /// EleButton Payload 请求容器：后端统一模型就绑这一个。
    /// 同时兼容两种 payload 形态：
    ///   (1) 结构化新形态：{ Filter, Page, Select, Context }
    ///   (2) EleTable 默认扁平形态：{ selectedIds, filters, pageIndex, pageSize, sortField, sortDirection, uniId }
    /// 注意：故意不再提供 Rows/rows 结构，避免 STJ CamelCase 策略下"Rows" 与 "rows" 属性名冲突导致的 500。
    ///       若业务确实需要当前页数据，可从后端用相同过滤+分页条件重新查询一次即可。
    /// </summary>
    public class ElePayload
    {
        //========================================
        // 第 1 部分：结构化新 DTO（Payload="All/Move/..." 时使用）
        //========================================
        public FilterInfo   Filter  { get; set; }
        public PageInfo     Page    { get; set; }
        public SelectInfo   Select  { get; set; }
        public ContextInfo  Context { get; set; }

        //========================================
        // 第 2 部分：扁平兼容字段（EleTable 默认 postHandler payload 或旧链路使用）
        //========================================
        [JsonPropertyName("selectedIds")]   public List<long>                 SelectedIds_Flat { get; set; }
        [JsonPropertyName("filters")]       public Dictionary<string, object> Filters_Flat { get; set; }
        [JsonPropertyName("pageIndex")]     public int?                       PageIndex_Flat { get; set; }
        [JsonPropertyName("pageSize")]      public int?                       PageSize_Flat { get; set; }
        [JsonPropertyName("sortField")]     public string                     SortField_Flat { get; set; }
        [JsonPropertyName("sortDirection")] public string                     SortDirection_Flat { get; set; }
        [JsonPropertyName("uniId")]         public string                     UniId_Flat { get; set; }

        //========================================
        // 第 3 部分：便利属性：结构化优先，扁平兜底（避免业务代码到处判空）
        //========================================
        [JsonIgnore]
        public List<long> SafeSelectedIds
        {
            get
            {
                if (Select?.Ids?.Count > 0) return Select.Ids;
                if (SelectedIds_Flat?.Count > 0) return SelectedIds_Flat;
                return new List<long>();
            }
        }

        [JsonIgnore] public int   SafeSelectedCount   => SafeSelectedIds?.Count ?? 0;
        [JsonIgnore] public long? SafeFirstSelectedId => SafeSelectedCount > 0 ? SafeSelectedIds[0] : null;

        [JsonIgnore]
        public Dictionary<string, object> SafeFilterItems
        {
            get
            {
                if (Filter?.Items != null && Filter.Items.Count > 0) return Filter.Items;
                if (Filters_Flat?.Count > 0) return Filters_Flat;
                return new Dictionary<string, object>();
            }
        }

        [JsonIgnore] public string SafeFilterKeys => SafeFilterItems != null ? string.Join(",", SafeFilterItems.Keys) : string.Empty;

        [JsonIgnore]
        public (int PageIndex, int PageSize, string SortField, string SortDirection) SafePage
        {
            get
            {
                if (Page != null)
                    return (Page.PageIndex, Page.PageSize, Page.SortField ?? string.Empty, Page.SortDirection ?? string.Empty);
                return (
                    PageIndex_Flat ?? 0,
                    PageSize_Flat ?? 0,
                    SortField_Flat ?? string.Empty,
                    SortDirection_Flat ?? string.Empty
                );
            }
        }

        [JsonIgnore]
        public string SafeUniId => !string.IsNullOrWhiteSpace(Context?.UniId) ? Context.UniId : (UniId_Flat ?? string.Empty);

        //========================================
        // 第 4 部分：兜底：任何不匹配字段都保留，保证 env 永远不为 null
        //========================================
        [JsonExtensionData]
        public Dictionary<string, JsonElement> ExtensionData { get; set; } = new();
    }
}
