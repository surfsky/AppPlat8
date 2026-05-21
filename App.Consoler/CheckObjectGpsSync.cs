using App.DAL;
using System.Text.Json;

public static class CheckObjectGpsSync
{
	private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

	public sealed class SyncResult
	{
		public int Total { get; set; }
		public int Success { get; set; }
		public int Failed { get; set; }
	}

	public static SyncResult Sync(int limit, int intervalMs, string amapKey)
	{
		var result = new SyncResult();
		if (string.IsNullOrWhiteSpace(amapKey))
		{
			Console.WriteLine("CheckObject.Gps 同步跳过：amapKey为空");
			return result;
		}

		var items = CheckObject.Set
			.Where(t => t.IsDel != true && string.IsNullOrWhiteSpace(t.Gps))
			.Where(t => !string.IsNullOrWhiteSpace(t.Address) || !string.IsNullOrWhiteSpace(t.Name))
			.OrderBy(t => t.Id)
			.Take(limit)
			.ToList();

		result.Total = items.Count;
		if (items.Count == 0)
			return result;

		for (var i = 0; i < items.Count; i++)
		{
			var item = items[i];
			var q = !string.IsNullOrWhiteSpace(item.Address) ? item.Address.Trim() : item.Name?.Trim() ?? string.Empty;
			if (string.IsNullOrWhiteSpace(q))
			{
				result.Failed++;
				continue;
			}

			try
			{
				if (TryGeocodeWgs84(q, amapKey, out var gps))
				{
					item.Gps = gps;
					item.UpdateDt = DateTime.Now;
					result.Success++;
				}
				else
				{
					result.Failed++;
				}
			}
			catch (Exception ex)
			{
				result.Failed++;
				Console.WriteLine($"CheckObject[{item.Id}] 坐标转换失败: {ex.Message}");
			}

			if (i < items.Count - 1)
				Thread.Sleep(intervalMs);
		}

		CheckObject.Db.SaveChanges();
		return result;
	}

	// 使用高德地图 API，获取地址坐标（GCJ02），并转换为 WGS84 坐标
	private static bool TryGeocodeWgs84(string queryText, string amapKey, out string gps)
	{
		gps = string.Empty;
		var address = Uri.EscapeDataString(queryText);
		var key = Uri.EscapeDataString(amapKey);
		var url = $"https://restapi.amap.com/v3/geocode/geo?key={key}&address={address}";

		using var resp = Http.GetAsync(url).GetAwaiter().GetResult();
		if (!resp.IsSuccessStatusCode)
			return false;

		var text = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
		if (string.IsNullOrWhiteSpace(text))
			return false;

		using var doc = JsonDocument.Parse(text);
		var root = doc.RootElement;
		var status = root.TryGetProperty("status", out var statusNode) ? statusNode.GetString() : "0";
		if (status != "1")
			return false;

		if (!root.TryGetProperty("geocodes", out var geocodes) || geocodes.ValueKind != JsonValueKind.Array || geocodes.GetArrayLength() == 0)
			return false;

		var geo = geocodes[0];
		if (!geo.TryGetProperty("location", out var locNode))
			return false;

		var location = locNode.GetString() ?? string.Empty;
		var parts = location.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		if (parts.Length < 2)
			return false;

		if (!double.TryParse(parts[0], out var gcjLng) || !double.TryParse(parts[1], out var gcjLat))
			return false;

		var wgs = Gcj02ToWgs84(gcjLng, gcjLat);
		gps = $"{wgs.lng:0.######},{wgs.lat:0.######}";
		return true;
	}

	// 将 GCJ02 坐标转换为 WGS84 坐标
	private static (double lng, double lat) Gcj02ToWgs84(double lng, double lat)
	{
		if (OutOfChina(lng, lat))
			return (lng, lat);

		const double a = 6378245.0;
		const double ee = 0.00669342162296594323;
		var dLat = TransformLat(lng - 105.0, lat - 35.0);
		var dLng = TransformLng(lng - 105.0, lat - 35.0);
		var radLat = lat / 180.0 * Math.PI;
		var magic = Math.Sin(radLat);
		magic = 1 - ee * magic * magic;
		var sqrtMagic = Math.Sqrt(magic);
		dLat = (dLat * 180.0) / ((a * (1 - ee)) / (magic * sqrtMagic) * Math.PI);
		dLng = (dLng * 180.0) / (a / sqrtMagic * Math.Cos(radLat) * Math.PI);
		var mgLat = lat + dLat;
		var mgLng = lng + dLng;
		return (lng * 2 - mgLng, lat * 2 - mgLat);
	}

	private static bool OutOfChina(double lng, double lat)
	{
		return lng < 72.004 || lng > 137.8347 || lat < 0.8293 || lat > 55.8271;
	}

	private static double TransformLat(double lng, double lat)
	{
		var ret = -100.0 + 2.0 * lng + 3.0 * lat + 0.2 * lat * lat + 0.1 * lng * lat + 0.2 * Math.Sqrt(Math.Abs(lng));
		ret += (20.0 * Math.Sin(6.0 * lng * Math.PI) + 20.0 * Math.Sin(2.0 * lng * Math.PI)) * 2.0 / 3.0;
		ret += (20.0 * Math.Sin(lat * Math.PI) + 40.0 * Math.Sin(lat / 3.0 * Math.PI)) * 2.0 / 3.0;
		ret += (160.0 * Math.Sin(lat / 12.0 * Math.PI) + 320 * Math.Sin(lat * Math.PI / 30.0)) * 2.0 / 3.0;
		return ret;
	}

	private static double TransformLng(double lng, double lat)
	{
		var ret = 300.0 + lng + 2.0 * lat + 0.1 * lng * lng + 0.1 * lng * lat + 0.1 * Math.Sqrt(Math.Abs(lng));
		ret += (20.0 * Math.Sin(6.0 * lng * Math.PI) + 20.0 * Math.Sin(2.0 * lng * Math.PI)) * 2.0 / 3.0;
		ret += (20.0 * Math.Sin(lng * Math.PI) + 40.0 * Math.Sin(lng / 3.0 * Math.PI)) * 2.0 / 3.0;
		ret += (150.0 * Math.Sin(lng / 12.0 * Math.PI) + 300.0 * Math.Sin(lng / 30.0 * Math.PI)) * 2.0 / 3.0;
		return ret;
	}
}
