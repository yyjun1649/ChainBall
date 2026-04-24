# SpecData Pipeline

Excel 스펙 시트를 Unity용 **auto-generated C# 클래스 + JSON**으로 변환하고,
런타임에 `Table<TKey, TValue>`로 O(1) 조회 가능한 형태로 노출하는 파이프라인.

```
_UDT-Galaxy__Dev_Spec_Table.xlsx
      │
      ▼ [Editor]  Tools > SpecData > Rebuild All
      │
      ├─► Assets/Scripts/SpecData/Generated/*.g.cs      (컴파일)
      └─► Assets/SpecData/Resources/SpecData/*.json     (런타임 로드)
                            │
                            ▼ [Runtime]
                  SpecDataManager.Tower.Get(20002)
```

---

## 1. Unity 프로젝트에 배치

아래 트리대로 복사한다. Editor 폴더는 반드시 `Editor/` 하위여야 런타임 빌드에서 제외된다.

```
Assets/
├─ Editor/SpecData/                           ← 에디터 전용 (빌드에 포함 안 됨)
│  ├─ ExcelBook.cs
│  ├─ TypeMapper.cs
│  ├─ SchemaParser.cs
│  ├─ EnumParser.cs
│  ├─ RowParser.cs
│  ├─ CodeGenerator.cs
│  ├─ JsonExporter.cs
│  ├─ SpecDataValidator.cs
│  ├─ SpecTableImporter.cs
│  └─ SpecTablePostprocessor.cs               ← (선택) xlsx 저장 시 자동 재생성
├─ Scripts/SpecData/
│  ├─ Table.cs
│  ├─ SpecDataManager.cs
│  ├─ Bootstrap.cs                            ← (선택) BeforeSceneLoad 자동 로딩
│  ├─ Partial/
│  │  └─ SpecDataManager.Tables.cs            ← 직접 수정 (테이블 와이어링)
│  └─ Generated/                              ← 자동 생성, 손대지 말 것
│     ├─ Enums.g.cs
│     └─ T*.g.cs
└─ SpecData/
   ├─ Raw/
   │  └─ _UDT-Galaxy__Dev_Spec_Table.xlsx     ← 원본 (버전관리 대상)
   └─ Resources/SpecData/                     ← 자동 생성 JSON
      └─ T*.json
```

경로는 `SpecTableImporter.cs` 상단 상수에서 조정 가능하다.

---

## 2. 의존성 설치

### 2-1. Newtonsoft.Json (런타임 + 에디터 둘 다 필요)

Package Manager → Add package by name → `com.unity.nuget.newtonsoft-json`

`manifest.json` 에 직접 추가해도 된다:

```json
"com.unity.nuget.newtonsoft-json": "3.2.1"
```

### 2-2. ClosedXML (에디터 전용)

Unity는 NuGet을 직접 지원하지 않으므로 다음 중 하나를 사용:

**Option A — NuGetForUnity (권장)**
1. https://github.com/GlitchEnzo/NuGetForUnity 설치
2. `NuGet > Manage NuGet Packages` → `ClosedXML` 검색 → Install

**Option B — DLL 수동 배치**
1. .NET 프로젝트에서 `dotnet add package ClosedXML` 후 `bin/` 의 DLL 수집
2. 다음 DLL들을 `Assets/Plugins/Editor/` 에 넣는다 (버전은 최신 기준, 조금씩 다를 수 있음):
   - `ClosedXML.dll`
   - `DocumentFormat.OpenXml.dll`
   - `ExcelNumberFormat.dll`
   - `RBush.dll`
   - `SixLabors.Fonts.dll`
   - `System.IO.Packaging.dll` (필요 시)
3. 각 DLL 선택 → Inspector → **Editor 플랫폼만 체크** (런타임 포함 방지)

> 주의: **EPPlus** 는 5.x부터 상업용 유료 라이선스라 권장하지 않음.

---

## 3. 엑셀 컨벤션 (파서가 따르는 규약)

### 시트명

| 접두 | 역할 | 처리 |
|------|------|------|
| `#Menu`   | 기획자용 목차 | 무시 |
| `#enum`   | enum 원본 정의 | `Enums.g.cs` 생성 |
| `#` 기타  | 메타 시트 | 무시 |
| `T*`      | 데이터 테이블 | `T*.g.cs` + `T*.json` 생성 |

### 데이터 테이블 행 구조 (1-based, 엑셀 기준)

| 행 | 내용 |
|----|------|
| 1 | `#Menu` / 한글 설명 (무시) |
| 2 | **필드명** (`#` 접두는 dev 컬럼 → 스킵) |
| 3 | **타입** (`#` 접두는 dev 컬럼 → 스킵) |
| 4+ | 데이터 행. 1열 값이 `IGNORE_ROW` 면 스킵 |

### 지원 타입

```
int, long, float, double, bool, string
int[], string[], ...          (배열: 셀 안에서 '/' 구분)
enum:eXxx                     (#enum 시트에서 생성된 타입)
enum[]:eXxx                   (enum 배열)
```

배열 구분자는 `RowParser.ARRAY_DELIM` 에 정의되어 있음 (현재 `/`).

### `#enum` 시트 구조

row 2에 `[eXxx, value:eXxx, (#desc), eYyy, value:eYyy, ...]` 식으로 열 페어가 나열되어 있으면 자동 인식.
row 3 이상이 실제 `key, value` 데이터.

---

## 4. 사용 흐름

### 첫 회 셋업
```
1. xlsx 를 Assets/SpecData/Raw/ 에 복사
2. 유니티 에디터 메뉴 → Tools > SpecData > Rebuild All
3. Generated 폴더에 Enums.g.cs + T*.g.cs 생성 확인
4. Partial/SpecDataManager.Tables.cs 를 열어 필요한 테이블 와이어링
5. Play 하면 Bootstrap.cs 가 BeforeSceneLoad 에 LoadAll() 호출
```

### 반복 작업
- xlsx 저장 → `SpecTablePostprocessor` 가 변경을 감지해 자동 재생성 (또는 수동 메뉴).

### 런타임 사용 예

```csharp
using SpecData;

var tower = SpecDataManager.Tower.Get(20002);
Debug.Log($"{tower.name} atk={tower.attack} type={tower.tower_type}");

if (SpecDataManager.Option.TryGet("ENERGY_MAX", out var opt))
    Debug.Log($"energy cap: {opt.value}");

foreach (var item in SpecDataManager.Item.All)
    if (item.main_type == eItemMainType.CURRENCY) { /* ... */ }
```

### 비즈니스 로직 추가 (partial class)

생성된 `TTower.g.cs` 를 건드리지 않고, 별도 파일에 partial class 로 확장:

```csharp
// Assets/Scripts/SpecData/Partial/TTower.cs
namespace SpecData
{
    public partial class TTower
    {
        public float AttacksPerSecond => attack_delay > 0 ? (float)(1.0 / attack_delay) : 0f;
        public bool  IsUnlocked(int userLevel) => userLevel >= unlock_level;
    }
}
```

---

## 5. 새 테이블 추가 체크리스트

```
[ ] xlsx 에 T{Name} 시트 추가 (행 컨벤션 준수)
[ ] Tools > SpecData > Rebuild All 실행
[ ] Generated/T{Name}.g.cs 가 기대한 필드로 생성됐는지 확인
[ ] Partial/SpecDataManager.Tables.cs 에 프로퍼티 + LoadResource 한 줄 추가
[ ] (선택) Partial/T{Name}.cs 에 헬퍼 메서드 작성
```

---

## 6. 검증 (Validator)

`SpecDataValidator` 는 테이블의 `enum:` 필드 값이 실제 `#enum` 에 정의되어 있는지 교차 검증한다.
`SpecTableImporter.RebuildAll` 마지막 단계에서 자동 호출되며, 오류는 Console 에 빨간 로그로 표시된다.

예: `TItem[3].main_type: 'CURENCY' is not a member of eItemMainType`
→ 엑셀 오타를 설계자에게 리포트.

---

## 7. 자주 마주칠 이슈

| 증상 | 원인 / 대응 |
|------|-------------|
| `Resource not found: SpecData/TXxx` | JSON이 `Assets/SpecData/Resources/SpecData/` 아래에 있는지 확인. 폴더 이름이 정확히 `Resources` 여야 함. |
| `duplicate key '...' in TXxx` | 키 컬럼에 중복. 엑셀 데이터 검토. 이후 행은 무시되고 첫 번째만 사용됨. |
| 빌드 시 `ClosedXML` 관련 컴파일 에러 | DLL이 Editor 플랫폼이 아닌 곳에 포함됨. Plugin Inspector 에서 플랫폼 체크 조정. |
| `could not convert string to float: '1,000'` 류 | 이미 Parser가 콤마 제거 처리. 만약 또 보이면 `RowParser.ConvertScalar` 확인. |
| 한글 깨짐 | 모든 파일 UTF-8 (BOM 없음)로 저장되는지 확인. `CodeGenerator` / `JsonExporter` 는 UTF-8 고정. |

---
