// Assets/Editor/SpecData/SpecDataValidator.cs
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpecData.EditorTools
{
    /// <summary>
    /// 테이블에 쓰인 enum 값이 실제 #enum 시트에 정의되어 있는지 교차 검증.
    /// 예: TItem.main_type 가 "CURRENCY" 인데 eItemMainType 에 CURRENCY 가 없으면 에러 리포트.
    /// SpecTableImporter 안에서 JSON/CS 생성 후 호출하면 좋다.
    /// </summary>
    public static class SpecDataValidator
    {
        public sealed class Report
        {
            public int ErrorCount;
            public List<string> Messages = new();
            public bool HasErrors => ErrorCount > 0;
        }

        public static Report Validate(
            Dictionary<string, (TableSchema schema, List<Dictionary<string, object>> rows)> tables,
            IList<EnumDef> enums)
        {
            var enumIndex = enums.ToDictionary(
                e => e.Name,
                e => new HashSet<string>(e.Entries.Select(kv => kv.key)));

            var rpt = new Report();
            foreach (var kv in tables)
            {
                var tableName = kv.Key;
                var (schema, rows) = kv.Value;
                foreach (var f in schema.Fields)
                {
                    if (f.Type.Prim != Primitive.Enum) continue;
                    if (!enumIndex.TryGetValue(f.Type.EnumName, out var set))
                    {
                        rpt.ErrorCount++;
                        rpt.Messages.Add($"{tableName}.{f.Name}: unknown enum '{f.Type.EnumName}'");
                        continue;
                    }

                    for (int r = 0; r < rows.Count; r++)
                    {
                        var v = rows[r][f.Name];
                        if (f.Type.IsArray)
                        {
                            foreach (var item in (object[])v)
                                CheckOne(rpt, tableName, f, r, item as string, set);
                        }
                        else
                        {
                            CheckOne(rpt, tableName, f, r, v as string, set);
                        }
                    }
                }
            }
            return rpt;
        }

        static void CheckOne(Report rpt, string table, Field f, int rowIdx, string val, HashSet<string> set)
        {
            if (string.IsNullOrEmpty(val)) return;
            if (set.Contains(val)) return;
            rpt.ErrorCount++;
            rpt.Messages.Add($"{table}[{rowIdx}].{f.Name}: '{val}' is not a member of {f.Type.EnumName}");
        }

        public static void Log(Report rpt)
        {
            if (!rpt.HasErrors) { Debug.Log("[SpecData] validation passed."); return; }
            foreach (var m in rpt.Messages) Debug.LogError("[SpecData] " + m);
            Debug.LogError($"[SpecData] validation FAILED: {rpt.ErrorCount} error(s).");
        }
    }
}
