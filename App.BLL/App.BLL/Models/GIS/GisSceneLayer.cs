using App.Entities;

namespace App.DAL.GIS
{
    /// <summary>场景展示图层关联</summary>
    public class GisSceneLayer : Entity<GisSceneLayer>
    {
        public long SceneId { get; set; }
        public string LayerName { get; set; }

        public virtual GisScene Scene { get; set; }
    }
}
