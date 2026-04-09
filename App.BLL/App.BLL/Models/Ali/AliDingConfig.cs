using App.Utils;
using App.Entities;

namespace App.DAL
{
    /// <summary>钉钉配置</summary>
    [UI("系统", "阿里钉钉配置")]
    public class AliDingConfig : EntityBase<AliDingConfig>
    {
        [UI("钉钉",       "CorpId")]        public string CorpId          { get; set; }
        [UI("钉钉小程序", "AgentId")]       public long?  MPAgentID       { get; set; }
        [UI("钉钉小程序", "AppKey")]        public string MPAppKey        { get; set; }
        [UI("钉钉小程序", "AppSecret")]     public string MPAppSecret     { get; set; }
        [UI("钉钉小程序", "PushToken")]     public string MPPushToken     { get; set; }
        [UI("钉钉小程序", "PushKey")]       public string MPPushKey       { get; set; }
        [UI("钉钉小程序", "TokenServer")]   public string MPTokenServer   { get; set; }
    }

}