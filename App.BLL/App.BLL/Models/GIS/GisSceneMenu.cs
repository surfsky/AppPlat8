using System;
using System.Linq;
using App.Entities;

namespace App.DAL.GIS
{
    /// <summary>场景图层关联</summary>
    public class GisSceneMenu : EntityBase<GisSceneMenu>
    {
        public long SceneId { get; set; }
        public long MenuId { get; set; }

        public virtual GisScene Scene { get; set; }
        public virtual GisMenu Menu { get; set; }
    }
}
