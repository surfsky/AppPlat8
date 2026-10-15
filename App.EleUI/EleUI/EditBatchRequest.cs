using System.Collections.Generic;

namespace App.EleUI
{
    /// <summary>
    /// 通用批量修改请求体。EleButton Command=EditBatch 触发的后端 OnPost{Handler} 统一接收此类型。
    /// - Ids：要更新的实体主键列表
    /// - Fields：字段名 → 目标值（空值由前端跳过不发送；后端 BatchUpdater 再做一次空值过滤）
    /// </summary>
    public class EditBatchRequest
    {
        public long[] Ids { get; set; }
        public Dictionary<string, object> Fields { get; set; }
    }
}
