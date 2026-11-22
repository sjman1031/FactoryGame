# CLAUDE.md — 공장 게임 프로토타입

이 파일은 세션 간 인수인계 문서다. 세션을 시작할 때마다 이 내용을 전제로 작업한다.
처음에는 claude.ai 프로젝트 "유니티에서 공장형 게임 제작"의 대화를 정리했고, 이후 Claude Code 세션에서 한 작업을 계속 덧붙이고 있다.
**다른 PC(노트북 등)에서도 맥락을 이어 가려면 이 파일과 `docs/`만 git으로 받으면 된다.**
(Claude Code의 로컬 메모리는 PC마다 따로 저장돼 동기화되지 않는다. 오래 남겨야 할 내용은 반드시 이 파일이나 `docs/`에 적는다.)

---

## 0. 가장 중요한 작업 규칙 — 수정 기록 (2026-11-22 사용자 지시)
**모든 수정사항은 다음 두 단계를 반드시 거친다. 예외 없음.**
1. **`docs/`에 문서로 정리한다.**
   - 파일명: `docs/<종류>-YYYY-MM-DD.md` (예: `bugfix-`, `feature-`, `refactor-`, `review-`, `docs-`). 같은 날 같은 종류가 또 있으면 `-2`를 붙인다.
   - 내용: 무엇을 / 왜 / 어떻게 확인했는지 / 바뀐 공개 API·저장 형식 / 에디터에서 사용자가 할 일 / 고치지 않은 것과 이유.
2. **이 CLAUDE.md에 반영한다.**
   - 10절 "변경 이력"에 한 줄 추가 (날짜 · 문서 링크 · 요약).
   - 바뀐 사실(파일 구성, 아키텍처, 미완료 항목, 사용자 결정 대기 목록 등)을 해당 절에서 고친다. 낡은 내용을 남겨 두지 않는다.
- 커밋·푸시는 사용자가 요청할 때만 한다. 노트북에서 이어 가려면 사용자가 커밋·푸시해야 하므로, 작업이 끝나면 "커밋·푸시가 필요하다"고 알린다.

---

## 1. 프로젝트 개요
- Unity(C#)로 만드는 Satisfactory 스타일 공장 건설 게임 프로토타입.
- 목적: 포트폴리오. 채용 담당자나 협업자에게 **게임 시스템 아키텍처 역량**을 보여주는 것이 목표다.
  따라서 "일단 동작"보다 설계의 명확성, 관심사 분리, 설명 가능한 결정을 우선한다.
- 개발 단계: 싱글톤 기반 구조 → 건물 시뮬레이션 → 월드 스트리밍 → 세이브 시스템 → 데이터 파이프라인
  → (2026-11-21~22) 코드 리뷰 · 최신화 점검 · 버그 수정.

## 2. 환경
- **Unity 2022.3.62f3 LTS** (`ProjectSettings/ProjectVersion.txt`).
  - `Awaitable`은 2023.1부터라서 **쓸 수 없다**. 비동기는 `Task` + `destroyCancellationToken`(2022.2+) + `Task.Forget()`(Core/TaskExtensions.cs).
- 저장소: GitHub `sjman1031/FactoryGame`, 브랜치 `master`.
- UI: UGUI(`Slider`, `Button`) + TextMeshPro(`TMP_Text`).
- 입력: 구 Input Manager(`Input.GetAxis`/`GetButtonDown`).

## 3. 폴더와 파일 (Assets/Scripts, 37개 — 2026-11-22 기준)
| 폴더 | 파일 |
|---|---|
| Core | GameBootstrapper, SingletonManager, TickManager, Inventory, TaskExtensions |
| Data | GameDatabase, ItemData, RecipeData, ResourceNodeData |
| World | ChunkData, ChunkGenerator, ChunkInstance, ChunkUtils, OreTable, WorldPreGenerator, WorldStreamingManager |
| Buildings | BuildingBase, BuildingCatalog, BuildingHotbar, BuildingPlacer, BuildingRegistry, ConveyorBelt, ManufacturerBuilding, MinerBuilding, StorageBuilding, ResourceNodeBehaviour(V2 결정 대기) |
| Player | InputManager, PlayerController, PlayerMovementManager, PlayerStateManager |
| Save | SaveSystem, SaveController, LiveData(+InventorySaveUtil) |
| UI | LoadingSceneController, LoadingUIController |
| Loading | LoadingContext |
| Editor | DataImporter |

그 밖의 자산:
- `Assets/Data/CSV/` — items(17행) · recipes(12행) · resource_nodes(5행). UTF-8 BOM, `#` 주석 줄 있음.
- `Assets/Data/ScriptableObjects/` — Items 17 · Recipes 12 · ResourceNodes 5. **아직 이전 필드명(nodeID, craftingTime)으로 저장돼 있음** → `FormerlySerializedAs`로 읽힘. 전체 임포트하면 새 이름으로 갱신된다.
- `Assets/Scenes/` — LoadingScene, MainScene. **스크립트가 하나도 붙어 있지 않다(S4).** Build Settings 씬 목록도 비어 있다. 구성안은 `docs/bugfix-2026-11-22.md` 4절.
- `Assets/Prefabs/` 없음. `Assets/Resources/GameDatabase.asset` 없음(전체 임포트로 생성).
- `docs/` — 4절 "문서 목록" 참고.

## 4. 문서 목록 (docs/)
| 문서 | 내용 |
|---|---|
| `code-review-2026-11-21.md` | 심층 코드 리뷰. 확정 버그 B1~B13, 경고 W1~W17, 기타 K1~K7, P0/P1/P2 로드맵 |
| `code-sync-check-2026-11-21.md` | 스크립트 최신화 후 문서와의 차이 점검. S1~S7, V1~V5, R1~R4 |
| `bugfix-2026-11-21.md` | 위 두 문서 기준 버그 수정 전체 기록 (수정 내역, 바뀐 API, 에디터 할 일, 고치지 않은 것) |
| `docs-2026-11-22.md` | CLAUDE.md 전면 정리, 리뷰 문서 이동, 수정 기록 규칙 도입 |
| `bugfix-2026-11-22.md` | 씬 구성 점검: 배치 입력(BuildingHotbar), 게임 씬 이름, Ground 레이어 수정 + **씬·프리팹 구성안(S4)** |

우선순위는 리뷰 로드맵을 따르되, 이미 처리된 항목은 `bugfix-2026-11-21.md` 기준으로 판단한다.

---

## 5. 아키텍처 (확정된 결정 — 바꾸기 전에 반드시 사용자에게 확인)
- **매니저 기반 + 명시적 싱글톤**: `SingletonManager<T>`, `GameBootstrapper`.
  - 초기화 순서 제어 방식(W1)은 아직 미정 — 9절.
- **세이브 = 바이너리 + JSON 하이브리드** (의도된 분리):
  - 월드 정적 데이터(청크 지형) → `world.bin`: magic `0x31574746`("FGW1"), `WORLD_VERSION = 2`, 시드, 인덱스 테이블.
  - 변하는 런타임 데이터 → `live.json` (JsonUtility).
- **데이터 파이프라인**: Excel → CSV → ScriptableObject. `EditorWindow` 도구 `DataImporter`(Tools › 📊 Data Importer)가 변환한다. `GameDatabase`는 Resources에서 로드한다.
- **건물 소유권 = B안**: 지형만 청크 스트리밍한다. 건물은 청크 로드/언로드와 무관하게 항상 살아 있고 계속 틱이 돈다.
- **자원 노드**: 노드마다 GameObject를 만들지 않는다. 타일 조회(lookup)로 처리한다.
- **광맥**: 위치별 확률 분포로 **구현 완료** (`ChunkGenerator.GetOreDistribution`). 시드로 언제든 다시 계산하므로 저장하지 않는다.
- **인벤토리 = 슬롯 모델** (2026-11-21): `stackSize`개마다 한 칸. 저장은 종류별 총량이라 형식 변화 없음.
- **청크 로드 = 프레임 예산 큐** (2026-11-21): 가까운 순 정렬, 프레임당 `_chunksPerFrame`(기본 2)개. async 없음. 순서는 위치 → 메시 → 활성화.

### 실패 처리 원칙 "알리고 보존" (리뷰 9절, 2026-11-21 적용)
1. **알린다**: 실패는 bool/예외로 돌려주고 로그·UI로 보여 준다. 조용히 넘어가지 않는다.
2. **실패 상태에서는 쓰지 않는다**: 불러오기에 실패하면(`SaveSystem.LoadFailed`) 자동 저장을 막는다. 수동 저장은 경고 후 허용.
3. **모르는 데이터는 버리지 않는다**: 못 찾은 아이템 id·건물 프리팹·레시피 id는 보관했다가 다음 저장에 그대로 다시 쓴다.
- 파일 쓰기는 tmp → 검증 → 교체(`File.Replace` + `.bak`). world.bin은 `world.bin.tmp`에 쓰고 완성돼야 커밋한다.
- 불러오기는 `live.json → .tmp → .bak` 순서. 손상 파일은 `.corrupt-<시각>`으로 백업.

## 6. 코드 스타일 규칙
- 관심사 분리를 깔끔하게 유지한다.
- enum은 관련 클래스 파일 안에 둔다.
- interface는 주로 구현하는 베이스 클래스와 같은 파일에 둔다.
- 순수 데이터 클래스(MonoBehaviour가 아닌 것):
  - 비슷한 역할의 클래스는 한 파일로 묶는다.
  - 한 곳에서만 쓰이는 클래스는 그 사용처 파일로 옮긴다.
- MonoBehaviour는 Unity 규칙상 파일명 = 클래스명을 유지한다.
- **직렬화 필드 이름을 바꾸면 `[FormerlySerializedAs("이전이름")]`을 붙인다.** 기존 에셋·씬 값이 사라지지 않게.
- **파일을 옮기거나 합칠 때는 원본을 반드시 지운다.** 구버전 파일이 남으면 타입 중복 정의(CS0101·CS0111)로 컴파일이 깨진다(2026-11-21 S1). 이름 변경·이동은 `git mv`로 `.cs`와 `.cs.meta`를 함께 옮겨 GUID를 유지한다.
- static 상태가 있는 클래스는 `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`로 초기화한다(Domain Reload 꺼도 안전).
- `async void`나 `_ = Task` 금지 → `Task.Forget(this)`.
- Unity 오브젝트 null 검사에 `?.`/`??` 쓰지 않는다(파괴 검사를 건너뜀) → `== null`.

## 7. 성능 패턴 (이미 적용됨 — 되돌리지 말 것)
- 거리 비교·정렬에는 `Vector3.Distance` 대신 `sqrMagnitude`.
- `BuildingRegistry`: 호출마다 할당하지 않도록 static 키 버퍼 재사용.
- `WorldStreamingManager`: `HashSet` 재사용, 프레임 예산 큐.
- `TickManager`: 제거 시 swap-back 패턴으로 O(1). Tick마다 try/catch로 예외 격리(실패한 건물만 빼고 비활성화).
- `SaveSystem.LoadChunk`: 재사용 바이트 버퍼에 한 번 읽고 `Buffer.BlockCopy`.

## 8. 해결된 버그와 교훈
- 역직렬화 중 `OnBuildingPlaced()` 이중 호출 → `instanceId` 할당을 `Awake()`로 옮기고 Awake → Start 순서에 의존해 해결. `instanceId`는 `[NonSerialized]`(프리팹 복제 시 id 겹침 방지).
- CSV 임포트 실패 원인:
  1. 쉼표가 들어간 주석 줄을 Excel이 따옴표로 감싸서 "주석 줄 건너뛰기" 로직이 깨짐
     → **Excel로 왕복할 CSV에는 주석 줄을 넣지 않는다.** (2026-11-21부터 파서가 따옴표·BOM을 벗기고 판정하므로 코드에서도 막힘)
  2. Excel이 파일을 열고 있으면 `File.ReadAllLines`에서 `IOException`(파일 잠금) → 이제 try/catch로 로그 패널에 표시.
- Unity 에디터 도구는 성공 로그 전에 예외가 나면 **조용히 실패**한다. IO 경계에는 try-catch, 인덱싱은 방어적으로. (DataImporter는 `Run()` 래퍼로 모든 버튼을 감쌈)
- 2026-11-21 리뷰 확정 버그 B1~B13 **전부 수정**. 자세한 내용은 `docs/bugfix-2026-11-21.md`.
- 리뷰가 제안한 해법이 이 Unity 버전에서 안 될 수 있다(Awaitable). 제안을 적용하기 전에 버전부터 확인한다.

## 9. 현재 상태와 할 일 (2026-11-22)

### 사용자가 Unity 에디터에서 할 일 (아직 안 함)
1. Unity를 열어 컴파일 확인. `.meta` 11개(BuildingCatalog, ManufacturerBuilding, ResourceNodeBehaviour, TaskExtensions, GameDatabase, PlayerStateManager, LiveData, SaveController, ChunkData, ChunkUtils, OreTable)는 **생성·커밋 완료(2026-11-22)**. 새 `BuildingHotbar.cs.meta`는 다음에 Unity를 열 때 생긴다 → 커밋해야 다른 PC에서 GUID가 같다.
2. **세이브를 열기 전에** Data Importer › 🔄 전체 임포트 → `Assets/Resources/GameDatabase.asset` 생성.
3. Tags & Layers에 `Ground`, `Building` 레이어 추가.
4. 씬·프리팹 구성(S4): `docs/bugfix-2026-11-22.md` 4절 구성안대로. Build Settings에 LoadingScene(0) · MainScene(1) 등록. Play는 항상 LoadingScene에서 시작.
5. 구성 후 Play 모드 체크리스트: `docs/bugfix-2026-11-21.md` 5절. 건물 배치는 숫자키 1~4(BuildingHotbar), R 회전, Esc 취소.

### 사용자 결정 대기 (구조 변경 — 먼저 묻고 진행)
| ID | 내용 | 선택지 |
|---|---|---|
| W1 | 부트스트래퍼 초기화 순서 | (a) 부트스트래퍼가 `OnInitialize`를 순서대로 호출 / (b) 주석을 "필수 참조 검증"으로 고침 |
| K1 | 청크 사이 1칸 틈 | world.bin v3(청크당 65×65) — 기존 세이브 비호환 |
| W11 | 시드 x/z 분리 | 광맥 위치가 바뀜 → v3와 묶을지 |
| W5·W14·W4·K4 | 격자 점유 딕셔너리 | 리뷰 로드맵 13번으로 진행할지 |
| W13 | Save 리더 소유권 | `IChunkSource` + asmdef 분리 |
| V2 | `Buildings/ResourceNodeBehaviour.cs` | 삭제 / K3 시각화 도구로 전환 |

### 남은 개선 (결정 불필요, 로드맵 순서대로)
- 기능: K2 진행 상태 저장, K5 #21 이동 보간, K6 씬 재진입(매니저 수명 구조라 W13과 함께), 건설 메뉴 UI(지금은 BuildingHotbar 숫자키뿐).
- 성능·편의(P2): W7 · W8 · W10 · W17. (W15는 해당 없음)
- README 없음 — 포트폴리오용 README 작성 고려.

### 검증 방법 (Unity 없이 컴파일 확인)
- Unity 설치본의 Roslyn으로 직접 컴파일했다:
  `<Unity>/Editor/Data/NetCoreRuntime/dotnet.exe <Unity>/Editor/Data/DotNetSdkRoslyn/csc.dll`
  참조: `NetStandard/ref/2.1.0/netstandard.dll`, `Managed/UnityEngine/*.dll`(UnityEditor.CoreModule이 여기 있으므로 `Managed/UnityEditor.dll`을 함께 넣으면 CS0433. 이 PC의 Unity 경로는 `E:/Unity/2022.3.62f3`), 옵션 `-define:UNITY_EDITOR -nowarn:0169,0649,0414`.
  UGUI·TMP는 DLL이 패키지 캐시(Library)에 있어 읽지 않으므로 `TMP_Text`·`Slider`·`Button` 스텁 파일을 따로 만들어 넣었다.
- 스크립트·스텁·테스트는 세션 임시 폴더에만 있었다(저장소에 없음). 다른 PC에서는 Unity 설치 경로에 맞춰 다시 만든다.
- 순수 로직 테스트: Inventory 19개, CSV 파서 7개 — 모두 통과(2026-11-21).
- 가장 확실한 확인: `%LOCALAPPDATA%\Unity\Editor\Editor.log` 또는 사용자가 붙여 주는 Console 에러.

---

## 10. 변경 이력
| 날짜 | 문서 | 요약 |
|---|---|---|
| 2026-11-21 | `docs/code-review-2026-11-21.md` | 심층 코드 리뷰 (claude.ai에서 작성) |
| 2026-11-21 | `docs/code-sync-check-2026-11-21.md` | 최신화된 스크립트와 문서 비교, 수정 필요 항목 정리 |
| 2026-11-21 | `docs/bugfix-2026-11-21.md` | 컴파일 에러 제거, B1~B13 전부 + 경고 다수 수정, 51 → 36 파일 (커밋 `14c6ace5`) |
| 2026-11-22 | `docs/docs-2026-11-22.md` | CLAUDE.md 전면 정리, 리뷰 문서 `새 폴더/` → `docs/` 이동, 수정 기록 규칙(0절) 도입 |
| 2026-11-22 | `docs/bugfix-2026-11-22.md` | 씬 구성 점검: BuildingHotbar 추가(배치 진입점), 게임 씬 이름 MainScene + 사전 검사, 청크 Ground 레이어 자동 지정, Placer 마스크·카메라 방어, 씬 구성안 |

---

## 11. 작업 방식
- 단계별(phase) 반복 개발. Claude는 함께 코딩하는 파트너 역할.
- 사용자는 아키텍처 결정에 직접 참여한다. 구조를 바꾸는 변경은 먼저 제안하고 확인받는다.
- 코드 리뷰 형식: **확정 버그**와 **잠재적 경고**를 나누고, 심각도 순으로 정렬하고, 각 항목에 바로 적용할 수 있는 수정안을 붙인다.
- 수정 기록은 0절 규칙을 따른다.
- 답변과 주석은 한국어로.

## 12. Unity 작업 규칙
- `Library/` `Temp/` `Logs/` `UserSettings/`는 읽거나 수정하지 않는다 (`.claude/settings.json`에서도 막혀 있음).
- `.meta` 파일은 만들거나 고치지 않는다 (Unity가 생성한다). 단, 파일 삭제·이름 변경 시 짝 `.meta`를 `git rm`/`git mv`로 함께 처리하는 것은 허용.
- `.unity` `.prefab` `.asset` YAML은 직접 수정하지 말고, 필요하면 사용자에게 에디터에서 해 달라고 요청한다.
- 컴파일 에러 확인: `%LOCALAPPDATA%\Unity\Editor\Editor.log`를 읽거나, 사용자에게 Console 에러를 붙여 달라고 한다.
