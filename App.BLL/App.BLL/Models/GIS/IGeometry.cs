using App.Utils;

namespace App.DAL.GIS
{
    /// <summary>统一 GIS 点位接口</summary>
    public interface IGeometry
    {
        long Id { get; }
        long RawId { get; }
        GeometryType? Type { get; }
        long? MenuId { get; }
        int SortId { get; }
        string Name { get; }
        string Alias { get; }
        string Addr { get; }
        string Gps { get; }
        string Region { get; }
        string Url { get; }
        string File { get; }
        string GeoJson { get; }
        string DataJson { get; }
        string Remark { get; }
        bool? IsVisible { get; }
        double? Scale { get; }
        string LabelColor { get; }
        string Icon { get; }
        string MenuName { get; }
        GisDataFrom DataFrom { get; }
    }

    /// <summary>统一 GIS 点位数据</summary>
    public class GeometryItem : IGeometry
    {
        const long ApiIdSeed = 1_000_000_000L;

        public long Id { get; set; }
        public long RawId { get; set; }
        public GeometryType? Type { get; set; } = GeometryType.Point;
        public long? MenuId { get; set; }
        public int SortId { get; set; }
        public string Name { get; set; }
        public string Alias { get; set; }
        public string Addr { get; set; }
        public string Gps { get; set; }
        public string Region { get; set; }
        public string Url { get; set; }
        public string File { get; set; }
        public string GeoJson { get; set; }
        public string DataJson { get; set; }
        public string Remark { get; set; }
        public bool? IsVisible { get; set; }
        public double? Scale { get; set; }
        public string LabelColor { get; set; }
        public string Icon { get; set; }
        public string MenuName { get; set; }
        public GisDataFrom DataFrom { get; set; } = GisDataFrom.Geometry;

        /// <summary>生成 API 点位唯一编号</summary>
        public static long BuildApiId(long? menuId, long rawId)
        {
            var menu = menuId.GetValueOrDefault();
            if (menu <= 0)
                menu = 1;
            return -((menu * ApiIdSeed) + rawId);
        }

        /// <summary>复制数据到指定菜单</summary>
        public GeometryItem CloneForMenu(long? menuId, string menuName, string icon)
        {
            return new GeometryItem
            {
                Id = DataFrom == GisDataFrom.API ? BuildApiId(menuId, RawId > 0 ? RawId : Id) : Id,
                RawId = RawId > 0 ? RawId : Id,
                Type = Type,
                MenuId = menuId,
                SortId = SortId,
                Name = Name,
                Alias = Alias,
                Addr = Addr,
                Gps = Gps,
                Region = Region,
                Url = Url,
                File = File,
                GeoJson = GeoJson,
                DataJson = DataJson,
                Remark = Remark,
                IsVisible = IsVisible,
                Scale = Scale,
                LabelColor = LabelColor,
                Icon = icon.IsNotEmpty() ? icon : Icon,
                MenuName = menuName.IsNotEmpty() ? menuName : MenuName,
                DataFrom = DataFrom
            };
        }
    }
}
