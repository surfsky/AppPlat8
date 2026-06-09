using System;
using System.Linq;
using App.Entities;

namespace App.DAL.GIS
{
    /// <summary>场景面板关联</summary>
    public class GisScenePanel : EntityBase<GisScenePanel>
    {
        public long SceneId { get; set; }
        public long PanelId { get; set; }

        public virtual GisScene Scene { get; set; }
        public virtual GisPanel Panel { get; set; }
    }
}
