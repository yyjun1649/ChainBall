// Assets/Editor/SpecData/RowParser.cs
using System.Collections.Generic;
using System.Globalization;

namespace SpecData.EditorTools
{
    /// <summary>
    /// 데이터 행 파싱.
    /// - 1열 값이 "IGNORE_ROW" 면 스킵
    /// - 모든 필드가 비어있으면 스킵
    /// - 숫자는 "1,000" 같은 천 단위 콤마 허용
    /// - 배열은 '/' 구분자 (TypeMapper 규약)
    /// </summary>
    public static class RowParser
    {
        public const string IGNORE_MARKER = "IGNORE_ROW";
        public const char ARRAY_DELIM = '/';

        public static List<Dictionary<string, object>> Parse(ExcelSheet sheet, TableSchema schema)
        {
            var result = new List<Dictionary<string, object>>(sheet.RowCount);
            for (int r = SchemaParser.ROW_DATA_START; r <= sheet.RowCount; r++)
            {
                if (sheet.Cell(r, 1).Trim() == IGNORE_MARKER) continue;
                if (IsEmptyRow(sheet, r, schema)) continue;

                var dict = new Dictionary<string, object>(schema.Fields.Count);
                foreach (var f in schema.Fields)
                {
                    var raw = sheet.Cell(r, f.ColumnIndex);
                    dict[f.Name] = Convert(raw, f.Type);
                }
                result.Add(dict);
            }
            return result;
        }

        static bool IsEmptyRow(ExcelSheet sheet, int r, TableSchema s)
        {
            foreach (var f in s.Fields)
                if (!string.IsNullOrWhiteSpace(sheet.Cell(r, f.ColumnIndex))) return false;
            return true;
        }

        static object Convert(string raw, FieldType ft)
        {
            raw = (raw ?? string.Empty).Trim();
            if (!ft.IsArray) return ConvertScalar(raw, ft);

            if (raw.Length == 0) return System.Array.Empty<object>();
            var parts = raw.Split(ARRAY_DELIM);
            var list = new object[parts.Length];
            for (int i = 0; i < parts.Length; i++) list[i] = ConvertScalar(parts[i].Trim(), ft);
            return list;
        }

        static object ConvertScalar(string raw, FieldType ft)
        {
            if (raw.Length == 0)
            {
                return ft.Prim switch
                {
                    Primitive.Int or Primitive.Long              => 0L,
                    Primitive.Float or Primitive.Double          => 0.0,
                    Primitive.Bool                               => false,
                    _                                            => string.Empty,
                };
            }

            switch (ft.Prim)
            {
                case Primitive.Int:
                case Primitive.Long:
                {
                    var s = raw.Replace(",", "");
                    return (long)double.Parse(s, CultureInfo.InvariantCulture);
                }
                case Primitive.Float:
                case Primitive.Double:
                {
                    var s = raw.Replace(",", "");
                    return double.Parse(s, CultureInfo.InvariantCulture);
                }
                case Primitive.Bool:
                    return raw == "1" || raw.Equals("true", System.StringComparison.OrdinalIgnoreCase);

                case Primitive.Enum:
                case Primitive.String:
                default:
                    return raw;   // enum은 문자열(이름)로 직렬화 → 런타임 StringEnumConverter가 역직렬화
            }
        }
    }
}
