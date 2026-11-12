using System.Collections.Generic;
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
    /// EleButton Payload 请求容器，仅使用结构化的 Filter、Page、Select、Context 字段。
    /// </summary>
    public class ElePayload
    {
        //========================================
        // 结构化 Payload（Payload="All/Move/..." 时使用）
        //========================================
        public FilterInfo   Filter  { get; set; }
        public PageInfo     Page    { get; set; }
        public SelectInfo   Select  { get; set; }
        public ContextInfo  Context { get; set; }

        //========================================
        // 便利属性，避免业务代码到处判空。
        //========================================
        [JsonIgnore] public string UniId => Context?.UniId ?? string.Empty;
        [JsonIgnore] public List<long> SelectedIds => Select?.Ids ?? new List<long>();
        [JsonIgnore] public int   SelectedCount   => SelectedIds?.Count ?? 0;
        [JsonIgnore] public long? FirstSelectedId => SelectedCount > 0 ? SelectedIds[0] : null;
        [JsonIgnore] public Dictionary<string, object> FilterItems => Filter?.Items ?? new Dictionary<string, object>();
        [JsonIgnore] public string FilterKeys => FilterItems != null ? string.Join(",", FilterItems.Keys) : string.Empty;
        [JsonIgnore] public (int PageIndex, int PageSize, string SortField, string SortDirection) Pager => (Page?.PageIndex ?? 0, Page?.PageSize ?? 0, Page?.SortField ?? string.Empty, Page?.SortDirection ?? string.Empty);

    }
}
