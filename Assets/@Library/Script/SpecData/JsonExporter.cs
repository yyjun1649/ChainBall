// Assets/Editor/SpecData/JsonExporter.cs
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace SpecData.EditorTools
{
    public static class JsonExporter
    {
        static readonly JsonSerializerSettings Settings = new()
        {
            Formatting = Formatting.Indented,
            // 빈 배열이 object[] 로 직렬화돼도 Newtonsoft가 알아서 처리.
        };

        public static void Write(List<Dictionary<string, object>> rows, string path)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var json = JsonConvert.SerializeObject(rows, Settings);
            File.WriteAllText(path, json, Encoding.UTF8);
        }
    }
}
