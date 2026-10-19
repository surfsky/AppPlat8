
/// <summary>
/// 简化版 IJobExecutionContext：所有 Quartz 接口字段以最小可用方式实现，
/// 真正的命令行参数通过 JobExtras.Current 获取，MergedJobDataMap 仅作 fallback 兜底
/// </summary>
public class FakeJobExecutionContext : Quartz.IJobExecutionContext
{
	readonly Dictionary<string, string> _extras;
	readonly Quartz.JobDataMap _jdm;
	public FakeJobExecutionContext(Dictionary<string, string> extras)
	{
		_extras = extras ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		_jdm = new Quartz.JobDataMap();
		foreach (var kv in _extras) _jdm[kv.Key] = kv.Value;
	}
	public Quartz.IScheduler Scheduler => null!;
	public Quartz.ITrigger Trigger => null!;
	public Quartz.ICalendar Calendar => null!;
	public bool Recovering => false;
	public Quartz.TriggerKey RecoveringTriggerKey => null!;
	public int RefireCount => 0;
	public Quartz.JobDataMap MergedJobDataMap => _jdm;
	public Quartz.IJobDetail JobDetail => null!;
	public Quartz.IJob JobInstance => null!;
	public DateTimeOffset FireTimeUtc => DateTimeOffset.UtcNow;
	public DateTimeOffset? ScheduledFireTimeUtc => null;
	public DateTimeOffset? PreviousFireTimeUtc => null;
	public DateTimeOffset? NextFireTimeUtc => null;
	public TimeSpan JobRunTime => TimeSpan.Zero;
	public object? Result { get; set; }
	public System.Threading.CancellationToken CancellationToken => System.Threading.CancellationToken.None;
	public void Put(object key, object objectValue) => _jdm[key.ToString() ?? ""] = objectValue;
	public object? Get(object key) => _extras.TryGetValue(key?.ToString() ?? "", out var v) ? v : _jdm[key?.ToString() ?? ""];
	public bool ContainsKey(object key) => _extras.ContainsKey(key?.ToString() ?? "") || _jdm.ContainsKey(key?.ToString() ?? "");
	public System.Collections.IDictionary ContextMap => _extras;
	public string FireInstanceId => Guid.NewGuid().ToString();
	public long? FireInstanceIdLong { get; set; }
}
