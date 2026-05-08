using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
//using System.Drawing;
//using System.Drawing.Imaging;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml;

namespace App.Utils
{
    /// <summary>
    /// XML 相关的辅助类
    /// </summary>
    public static class XmlHelper
    {
        //------------------------------------------
        // xml
        //------------------------------------------
        /// <summary>将XML字符串转为Json字符串（慎用，层次和属性都可能有差异）</summary>
        /// <example>
        /// "<Person><Name>Kevin</Name><Age>21</Age></Person>"  -> {"Person":{"Name":"Kevin","Age":"21"}}
        /// </example>
        public static string ParseXmlToJson(this string xml)
        {
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            var root = ToJsonNode(doc.DocumentElement);
            return root?.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) ?? "{}";
        }

        /// <summary>将 Json 字符串解析为动态对象</summary>
        public static dynamic ParseDynamic(this string json)
        {
            var normalized = NormalizeJsonLikeInput(json);
            var node = JsonNode.Parse(normalized);
            return node == null ? new ExpandoObject() : ToDynamic(node);
        }

        private static string NormalizeJsonLikeInput(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return "{}";

            var normalized = json.Replace('\'', '"');
            normalized = Regex.Replace(
                normalized,
                @"(?<=\{|,)\s*(?<key>[A-Za-z_][A-Za-z0-9_]*)\s*:",
                m => $"\"{m.Groups["key"].Value}\":"
            );
            return normalized;
        }

        private static JsonNode ToJsonNode(XmlNode node)
        {
            if (node == null)
            return new JsonObject();

            if (node.NodeType == XmlNodeType.Text || node.NodeType == XmlNodeType.CDATA)
                return JsonValue.Create(node.Value ?? string.Empty);

            var element = node as XmlElement;
            if (element == null)
                return JsonValue.Create(node.InnerText);

            var result = new JsonObject();

            if (element.Attributes != null)
            {
                foreach (XmlAttribute attr in element.Attributes)
                    result["@" + attr.Name] = attr.Value;
            }

            var childElements = element.ChildNodes.Cast<XmlNode>()
                .Where(n => n.NodeType == XmlNodeType.Element)
                .ToList();

            if (childElements.Count == 0)
            {
                if (!string.IsNullOrEmpty(element.InnerText))
                    result["#text"] = element.InnerText;
            }
            else
            {
                foreach (var group in childElements.GroupBy(n => n.Name))
                {
                    if (group.Count() == 1)
                    {
                        result[group.Key] = ToJsonNode(group.First());
                    }
                    else
                    {
                        var arr = new JsonArray();
                        foreach (var item in group)
                            arr.Add(ToJsonNode(item));
                        result[group.Key] = arr;
                    }
                }
            }

            var wrapped = new JsonObject { [element.Name] = result };
            return wrapped;
        }

        private static object ToDynamic(JsonNode node)
        {
            if (node is JsonObject obj)
            {
                IDictionary<string, object> expando = new ExpandoObject();
                foreach (var kv in obj)
                    expando[kv.Key] = kv.Value == null ? null : ToDynamic(kv.Value);
                return (ExpandoObject)expando;
            }

            if (node is JsonArray arr)
            {
                var list = new List<object>();
                foreach (var item in arr)
                    list.Add(item == null ? null : ToDynamic(item));
                return list;
            }

            var val = node as JsonValue;
            return val == null ? null : GetJsonPrimitive(val);
        }

        private static object GetJsonPrimitive(JsonValue value)
        {
            if (value.TryGetValue<string>(out var s)) return s;
            if (value.TryGetValue<int>(out var i)) return i;
            if (value.TryGetValue<long>(out var l)) return l;
            if (value.TryGetValue<double>(out var d)) return d;
            if (value.TryGetValue<decimal>(out var m)) return m;
            if (value.TryGetValue<bool>(out var b)) return b;
            if (value.TryGetValue<DateTime>(out var dt)) return dt;
            return value.ToString();
        }

        /// <summary>解析XML字符串为对象</summary>
        public static T ParseXml<T>(this string xml) where T : class
        {
            return new Xmlizer().Parse<T>(xml);
        }

        /// <summary>解析XML字符串为对象</summary>
        public static object ParseXml(this string xml, Type type)
        {
            return new Xmlizer().Parse(xml, type);
        }

        /// <summary>解析XML字符串为 Xml 文档对象</summary>
        public static XmlDocument ParseXml(this string xml)
        {
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            return doc;
        }

        /// <summary>将对象转化为 XML 字符串</summary>
        public static string ToXml(this object o, string rootName = "xml")
        {
            return new Xmlizer().ToXml(o, rootName);
        }



        //-------------------------------------
        // XML 文件读写
        //-------------------------------------
        /// <summary>保存对象为 Xml 文件</summary>
        public static void SaveXmlFile(this object obj, string filePath)
        {
            using (StreamWriter writer = new StreamWriter(filePath))
            {
                writer.Write(obj.ToXml());
                writer.Close();
            }
        }

        /// <summary>加载 XML 文件并解析为对象</summary>
        public static object LoadXmlFile(string filePath, Type type)
        {
            if (!File.Exists(filePath))
                return null;
            using (StreamReader reader = new StreamReader(filePath))
            {
                var obj = reader.ReadToEnd().ParseXml(type);
                reader.Close();
                return obj;
            }
        }

        /// <summary>加载 XML 文件并解析为对象</summary>
        public static T LoadXmlFile<T>(string filePath) where T : class
        {
            if (!File.Exists(filePath))
                return null;
            var txt = File.ReadAllText(filePath);
            return txt.ParseXml<T>();
        }
    }
}