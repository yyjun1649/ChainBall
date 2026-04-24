// Assets/Editor/SpecData/SpecTablePostprocessor.cs
using UnityEditor;
using UnityEngine;

namespace SpecData.EditorTools
{
    /// <summary>
    /// xlsx 파일이 Import / 수정될 때 자동으로 RebuildAll 을 호출.
    /// 자동 재생성을 원하지 않으면 이 파일을 삭제하거나 OnPostprocessAllAssets 내부를 주석 처리.
    /// </summary>
    public class SpecTablePostprocessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(
            string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            var target = SpecTableImporter.XLSX_PATH?.Replace('\\', '/');
            if (string.IsNullOrEmpty(target)) return;

            bool touched = false;
            foreach (var p in imported)
                if (p.Replace('\\', '/') == target) { touched = true; break; }
            if (!touched) return;

            Debug.Log($"[SpecData] detected change on {target} → RebuildAll");
            EditorApplication.delayCall += SpecTableImporter.RebuildAll;
        }
    }
}
