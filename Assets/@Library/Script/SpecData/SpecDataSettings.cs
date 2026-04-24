// Assets/Editor/SpecData/SpecDataSettings.cs
using UnityEngine;
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
#endif

namespace SpecData.EditorTools
{
    /// <summary>
    /// SpecData 파이프라인 경로 및 옵션 설정.
    /// Inspector 에서 xlsx 위치, 생성 코드 출력 폴더, JSON 출력 폴더 등을 지정.
    /// </summary>
    [CreateAssetMenu(fileName = "SpecDataSettings", menuName = "SpecData/Settings", order = 0)]
    public class SpecDataSettings : ScriptableObject
    {
        [Header("Input")]
        [Tooltip("소스 xlsx 경로. 프로젝트 루트 기준 'Assets/...' 형태.")]
        public string xlsxPath = "Assets/SpecData/Raw/SpecTable.xlsx";

        [Header("Output")]
        [Tooltip("자동 생성 C# 스크립트 출력 폴더.")]
        public string codeGenDir = "Assets/Scripts/SpecData/Generated";

        [Tooltip("JSON 데이터 출력 폴더. Addressable 로 등록할 폴더 경로.")]
        public string jsonOutDir = "Assets/SpecData/Json";

        [Header("Codegen")]
        [Tooltip("Enum 정의 시트 이름.")]
        public string enumSheet = "#enum";

        [Tooltip("생성 코드 네임스페이스.")]
        public string namespaceName = "SpecData";

        [Tooltip("런타임 Addressable 키 prefix. 최종 키 = prefix + 테이블명. 예) \"SpecData/\" + \"TTower\" → \"SpecData/TTower\".")]
        public string addressablePrefix = "SpecData/";

#if UNITY_EDITOR
        const string DEFAULT_ASSET_PATH = "Assets/@Project/Scripts/Data/SpecData/SpecDataSettings.asset";

        static SpecDataSettings _cached;

        /// <summary>
        /// 프로젝트에 존재하는 설정 에셋을 로드. 없으면 기본 경로에 자동 생성.
        /// </summary>
        public static SpecDataSettings GetOrCreate()
        {
            if (_cached != null) return _cached;

            var guids = AssetDatabase.FindAssets("t:SpecDataSettings");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                _cached = AssetDatabase.LoadAssetAtPath<SpecDataSettings>(path);
                if (guids.Length > 1)
                    Debug.LogWarning($"[SpecData] Multiple SpecDataSettings found. Using: {path}");
                return _cached;
            }

            var settings = CreateInstance<SpecDataSettings>();
            var dir = Path.GetDirectoryName(DEFAULT_ASSET_PATH);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            AssetDatabase.CreateAsset(settings, DEFAULT_ASSET_PATH);
            AssetDatabase.SaveAssets();
            Debug.Log($"[SpecData] Settings asset created: {DEFAULT_ASSET_PATH}");
            _cached = settings;
            return settings;
        }

        public static void InvalidateCache() => _cached = null;

        [MenuItem("Tools/SpecData/Settings")]
        static void SelectSettings()
        {
            var s = GetOrCreate();
            Selection.activeObject = s;
            EditorGUIUtility.PingObject(s);
        }
#endif
    }
}
