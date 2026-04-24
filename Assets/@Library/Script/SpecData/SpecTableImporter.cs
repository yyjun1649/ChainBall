// Assets/Editor/SpecData/SpecTableImporter.cs
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace SpecData.EditorTools
{
    /// <summary>
    /// SpecData 자동 생성 오케스트레이터.
    /// Tools > SpecData > Rebuild All 로 실행.
    /// 경로/옵션은 SpecDataSettings (Tools > SpecData > Settings) 에서 지정.
    /// </summary>
    public static class SpecTableImporter
    {
        // 외부(Postprocessor 등)에서 참조하는 경로 접근자 — 실제 값은 SpecDataSettings 에서 로드.
        public static string XLSX_PATH    => SpecDataSettings.GetOrCreate().xlsxPath;
        public static string CODE_GEN_DIR => SpecDataSettings.GetOrCreate().codeGenDir;
        public static string JSON_OUT_DIR => SpecDataSettings.GetOrCreate().jsonOutDir;
        public static string ENUM_SHEET   => SpecDataSettings.GetOrCreate().enumSheet;
        public static string NAMESPACE    => SpecDataSettings.GetOrCreate().namespaceName;

        [MenuItem("Tools/SpecData/Rebuild All")]
        public static void RebuildAll()
        {
            var settings = SpecDataSettings.GetOrCreate();
            var xlsxPath    = settings.xlsxPath;
            var codeGenDir  = settings.codeGenDir;
            var jsonOutDir  = settings.jsonOutDir;
            var enumSheet   = settings.enumSheet;
            var nameSpace   = settings.namespaceName;

            if (!File.Exists(xlsxPath))
            {
                Debug.LogError($"[SpecData] xlsx not found: {xlsxPath}\nCheck SpecDataSettings (Tools > SpecData > Settings).");
                return;
            }

            var sw = Stopwatch.StartNew();
            Debug.Log($"[SpecData] Reading {xlsxPath}");
            var book = ExcelBook.Open(xlsxPath);

            // 1) Enum 먼저 생성. 테이블이 enum 타입을 참조하므로.
            if (book.Sheets.TryGetValue(enumSheet, out var enumSheetObj))
            {
                var defs = EnumParser.Parse(enumSheetObj);
                CodeGenerator.WriteEnums(defs, Path.Combine(codeGenDir, "Enums.g.cs"), nameSpace);
                Debug.Log($"[SpecData] Enums: {defs.Count} generated.");
            }
            else
            {
                Debug.LogWarning($"[SpecData] '{enumSheet}' sheet not found. Enum generation skipped.");
            }

            // 2) 데이터 테이블.
            var parsedTables = new System.Collections.Generic.Dictionary<
                string, (TableSchema schema, System.Collections.Generic.List<
                    System.Collections.Generic.Dictionary<string, object>> rows)>();
            int okCount = 0, skipCount = 0;
            foreach (var kv in book.Sheets)
            {
                var name = kv.Key;
                if (name.StartsWith("#")) continue; // #Menu, #enum 등 메타 시트

                var schema = SchemaParser.Parse(kv.Value);
                if (schema.Fields.Count == 0)
                {
                    Debug.LogWarning($"[SpecData]   SKIP {name}: no valid fields.");
                    skipCount++; continue;
                }

                var rows = RowParser.Parse(kv.Value, schema);
                CodeGenerator.WriteClass(schema, Path.Combine(codeGenDir, $"{name}.g.cs"), nameSpace);
                JsonExporter.Write(rows, Path.Combine(jsonOutDir, $"{name}.json"));
                Debug.Log($"[SpecData]   {name}: {schema.Fields.Count} fields, {rows.Count} rows");
                parsedTables[name] = (schema, rows);
                okCount++;
            }

            // 3) 검증 — enum 참조 무결성.
            if (book.Sheets.TryGetValue(enumSheet, out var enumSheet2))
            {
                var defs = EnumParser.Parse(enumSheet2);
                var rpt = SpecDataValidator.Validate(parsedTables, defs);
                SpecDataValidator.Log(rpt);
            }

            // 4) SpecDataManager 자동 와이어링 (파셜 생성).
            var schemas = new System.Collections.Generic.List<TableSchema>(parsedTables.Count);
            foreach (var kv in parsedTables) schemas.Add(kv.Value.schema);
            var addressPrefix = settings.addressablePrefix ?? string.Empty;
            CodeGenerator.WriteManagerTables(
                schemas,
                Path.Combine(codeGenDir, "SpecDataManager.Tables.g.cs"),
                nameSpace,
                addressPrefix);
            Debug.Log($"[SpecData] SpecDataManager.Tables.g.cs wired for {schemas.Count} tables (addressablePrefix=\"{addressPrefix}\").");

            sw.Stop();
            Debug.Log($"[SpecData] Done. {okCount} tables generated, {skipCount} skipped. ({sw.ElapsedMilliseconds} ms)");
            AssetDatabase.Refresh();
        }

        [MenuItem("Tools/SpecData/Open xlsx")]
        public static void OpenXlsx()
        {
            var path = SpecDataSettings.GetOrCreate().xlsxPath;
            if (File.Exists(path)) EditorUtility.RevealInFinder(path);
            else Debug.LogError($"[SpecData] xlsx not found: {path}");
        }
    }
}
