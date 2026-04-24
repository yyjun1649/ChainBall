// Assets/Scripts/SpecData/SpecDataManager.cs

using Library;
using UnityEngine;

namespace SpecData
{
    /// <summary>
    /// 모든 스펙 테이블 접근의 중심점.
    /// 싱글톤은 이 클래스에 상속/Instance 패턴을 추가해 적용.
    /// 실제 테이블 필드와 로딩 로직은 SpecDataManager.Tables.g.cs (자동 생성) 에 작성.
    /// </summary>
    public partial class SpecDataManager
    {
        public bool IsLoaded { get; private set; }

        public void LoadAll()
        {
            if (IsLoaded) return;
            LoadTables();  // 구현은 partial 파일에서
            IsLoaded = true;
            DebugUtil.Log("[SpecData] all tables loaded.");
        }

        public void Unload()
        {
            UnloadTables();  // partial
            IsLoaded = false;
        }

        partial void LoadTables();
        partial void UnloadTables();
    }
}
