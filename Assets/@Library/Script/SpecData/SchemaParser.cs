// Assets/Editor/SpecData/SchemaParser.cs
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SpecData.EditorTools
{
    public sealed class TableSchema
    {
        public string TableName;
        public List<Field> Fields = new();
    }

    public sealed class Field
    {
        public int ColumnIndex;   // 1-based (엑셀 기준)
        public string Name;
        public string RawType;
        public FieldType Type;
    }

    /// <summary>
    /// Row convention (1-based):
    ///   row 1: #Menu / 한글 설명 (무시)
    ///   row 2: 필드명     ('#' 접두는 dev 컬럼, 스킵)
    ///   row 3: 타입       ('#' 접두는 dev 컬럼, 스킵)
    ///   row 4+: 데이터
    /// </summary>
    public static class SchemaParser
    {
        public const int ROW_DESC = 1;
        public const int ROW_NAME = 2;
        public const int ROW_TYPE = 3;
        public const int ROW_DATA_START = 4;

        static readonly Regex IdentRegex = new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

        public static TableSchema Parse(ExcelSheet sheet)
        {
            var schema = new TableSchema { TableName = sheet.Name };
            for (int c = 1; c <= sheet.ColCount; c++)
            {
                var name = sheet.Cell(ROW_NAME, c).Trim();
                var type = sheet.Cell(ROW_TYPE, c).Trim();

                if (name.Length == 0 || type.Length == 0) continue;
                if (name.StartsWith("#") || type.StartsWith("#")) continue; // dev 컬럼
                if (!IdentRegex.IsMatch(name))
                {
                    UnityEngine.Debug.LogWarning(
                        $"[SpecData] {sheet.Name} col{c}: '{name}' is not a valid C# identifier, skipped.");
                    continue;
                }

                schema.Fields.Add(new Field
                {
                    ColumnIndex = c,
                    Name = name,
                    RawType = type,
                    Type = TypeMapper.Parse(type),
                });
            }
            return schema;
        }
    }
}
