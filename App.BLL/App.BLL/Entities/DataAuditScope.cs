namespace App.Entities
{
    /// <summary>
    /// 当前请求的数据录入审计上下文。
    /// </summary>
    public class DataAuditScope
    {
        public bool Enabled { get; set; } = true;
        public long? UserId { get; set; }
        public long? OrgId { get; set; }
    }
}