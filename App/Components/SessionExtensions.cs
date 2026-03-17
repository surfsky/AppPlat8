using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace App.Utils
{
    /// <summary>
    /// Helper extensions to serialize/deserialize objects into <see cref="ISession" />.
    /// </summary>
    public static class SessionExtensions
    {
        public static void SetObject<T>(this ISession session, string key, T value)
        {
            if (session == null) return;
            if (value == null)
            {
                session.Remove(key);
                return;
            }

            string json = JsonSerializer.Serialize(value);
            session.SetString(key, json);
        }

        public static T GetObject<T>(this ISession session, string key)
        {
            if (session == null) return default;
            var json = session.GetString(key);
            if (json == null) return default;
            return JsonSerializer.Deserialize<T>(json);
        }
    }
}