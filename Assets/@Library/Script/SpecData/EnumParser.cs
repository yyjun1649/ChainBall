// Assets/Editor/SpecData/EnumParser.cs
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace SpecData.EditorTools
{
    public sealed class EnumDef
    {
        public string Name;
        public List<(string key, long value)> Entries = new();
    }

    /// <summary>
    /// #enum 시트 파싱.
    /// row 2 에 [eXxx, value:eXxx, (#desc)], [eYyy, value:eYyy], ... 식으로 열 페어가 나열됨.
    /// row 3 이상이 실제 key/value 쌍.
    /// </summary>
    public static class EnumParser
    {
        static readonly Regex IdentRegex = new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

        public static List<EnumDef> Parse(ExcelSheet sheet)
        {
            var defs = new List<EnumDef>();
            if (sheet == null) return defs;

            int c = 1;
            while (c <= sheet.ColCount)
            {
                var head = sheet.Cell(SchemaParser.ROW_NAME, c).Trim();
                var next = (c + 1 <= sheet.ColCount)
                    ? sheet.Cell(SchemaParser.ROW_NAME, c + 1).Trim()
                    : string.Empty;

                bool isPair = head.StartsWith("e") && next == $"value:{head}";
                if (!isPair) { c++; continue; }

                var def = new EnumDef { Name = head };
                var seen = new HashSet<string>();
                for (int r = SchemaParser.ROW_TYPE; r <= sheet.RowCount; r++)
                {
                    var key = sheet.Cell(r, c).Trim();
                    var valRaw = sheet.Cell(r, c + 1).Trim();
                    if (key.Length == 0 || !IdentRegex.IsMatch(key)) continue;
                    if (!double.TryParse(valRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var dv))
                        continue;
                    var lv = (long)dv;
                    if (!seen.Add(key)) continue;
                    def.Entries.Add((key, lv));
                }
                if (def.Entries.Count > 0) defs.Add(def);
                c += 2;
            }
            return defs;
        }
    }
}
