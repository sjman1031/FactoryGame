# 최신화 점검 — 리뷰 문서 대조와 추가 발견 사항

- 날짜: 2026-11-21
- 대상: 최신화한 `Assets/Scripts/` .cs 51개, `Assets/Data/`, `Assets/Scenes/`, `ProjectSettings/ProjectVersion.txt`
- 기준 문서: `docs/code-review-2026-11-21.md` (이하 "리뷰")
- 방법: 정적 분석만 했습니다. 이번 점검에서 스크립트는 수정하지 않았습니다. Unity도 아직 열지 않았습니다(새 파일에 `.meta`가 없음).

> 결론: 리뷰의 B1~B13, W1~W17은 **행 번호까지 지금 코드와 일치**하고, 아직 하나도 고쳐지지 않았습니다.
> 하지만 리뷰보다 먼저 처리해야 할 **새 P0 4건(S1~S4)** 이 있습니다. 지금 상태로는 컴파일이 되지 않습니다.

---

## 0. 요약

| 구분 | 개수 | 내용 |
|---|---|---|
| 새 확정 문제 (S) | 7 | 최신화하면서 생긴 문제, 또는 리뷰가 놓친 문제 |
| 새 잠재 경고 (V) | 5 | 지금은 드러나지 않지만 조건이 맞으면 터지는 문제 |
| 리뷰 정정 (R) | 4 | 리뷰 내용 가운데 이 프로젝트에 맞지 않는 부분 |
| 리뷰 항목 대조 | 37 | B 13 · W 17 · K 7 — 모두 그대로 남아 있음 (3절) |

**가장 먼저 할 일 (리뷰 P0보다 앞)**
1. **S1** 구버전 파일 7개 삭제 — 클래스가 중복으로 정의돼서 컴파일 실패
2. **S2** `Conveyorbelt.cs`를 `ConveyorBelt.cs`로 이름 변경 — 파일명과 클래스명이 다르면 Unity가 컴포넌트로 붙이지 못함
3. **S3** 컴파일 후 Data Importer에서 "전체 임포트" — 필드 이름이 바뀌어 기존 SO 데이터가 비고, `GameDatabase.asset`도 없음
4. **S4** 씬 구성과 건물 프리팹 확인 — 두 씬에 스크립트 컴포넌트가 하나도 없고 `Prefabs/`가 비어 있음

---

## 1. 새 확정 문제

### S1 · 구버전 파일 7개가 남아 타입이 중복 정의됨 — 컴파일 실패 — **P0**
최신 파일을 덮어쓸 때 **이름이 바뀌었거나 다른 파일로 옮겨진 구버전 파일**이 지워지지 않았습니다. 구버전 파일은 CP949로 인코딩돼 있어서(주석이 깨져 보임) 새 파일(UTF-8)과 구분됩니다.

| 남은 구버전 파일 | 문제 | 대체한 새 파일 |
|---|---|---|
| `Player/PlayerState.cs` | `MovementConfig`, `PlayerState`, `PlayerStateManager` 중복 (CS0101) | `PlayerMovementManager.cs`, `PlayerStateManager.cs` |
| `World/ChunkCore.cs` | `ChunkUtils`, `ChunkData` 중복. 지워진 `NodeEntry`를 직접 정의함 | `ChunkUtils.cs`, `ChunkData.cs` |
| `World/ChunkObjects.cs` | `ChunkInstance` 중복 | `ChunkInstance.cs` |
| `World/OreSystem.cs` | `OreDefinition`, `IOreTable`, `HardcodedOreTable` 중복. 없는 enum 값 `NodePurity.NORMAL` 등 사용 | `OreTable.cs` |
| `World/ResourceNodeBehaviour.cs` | `ResourceNodeBehaviour` 중복. `NodePurity.PURE` 등 사용 | `Buildings/ResourceNodeBehaviour.cs` |
| `Buildings/ManufactureBuilding.cs` | 중복은 아니지만 없는 필드 `craftingTime` 사용 (CS1061) | `ManufacturerBuilding.cs` |
| `Buildings/BuildingCategory.cs` | 컴파일은 되지만 쓰는 곳이 없는 죽은 코드 | `BuildingCatalog.cs` |

- **확인한 것:** 두 씬과 모든 에셋에서 위 7개 파일의 GUID를 참조하는 곳이 없습니다. 지워도 끊어지는 참조가 없습니다.
- **수정안:** Unity Project 창에서 7개 파일을 지웁니다(`.meta`도 같이 지워짐). git으로 지운다면 `.cs`와 `.cs.meta`를 함께 `git rm` 합니다.

### S2 · `Conveyorbelt.cs`의 파일명과 클래스명(`ConveyorBelt`)이 다름 — **P0**
- **위치:** `Buildings/Conveyorbelt.cs`
- **문제:** Unity는 MonoBehaviour의 파일명과 클래스명이 같아야 스크립트를 컴포넌트로 인식합니다. 지금은 벨트 프리팹에 `ConveyorBelt`를 붙일 수 없습니다. CLAUDE.md 규칙("MonoBehaviour는 파일명 = 클래스명")에도 어긋나고, 리뷰도 `Buildings/ConveyorBelt.cs`라고 적고 있습니다.
- **수정안:** Unity Project 창에서 `ConveyorBelt.cs`로 이름을 바꿉니다. 그러면 `.meta`의 GUID가 유지됩니다. Windows는 대소문자만 다른 이름 변경을 git이 놓칠 수 있으므로, 커밋할 때 `git mv Conveyorbelt.cs ConveyorBelt.cs`(+ `.meta`)로 기록합니다.

### S3 · 직렬화 필드 이름이 바뀌어 기존 SO 데이터가 비고, GameDatabase.asset도 없음 — **P0**
- **위치:** `Data/ResourceNodeData.cs` 10행, `Data/RecipeData.cs` 14행, `Assets/Resources/`
- **문제:**
  1. 기존 노드 에셋 5개는 `nodeID`로 저장돼 있는데, 코드의 필드는 `nodeId`입니다. Unity가 읽으면 id가 빈 값이 되고, `GameDatabase.BuildMaps`는 빈 id를 건너뜁니다. 그 결과 `MinerBuilding`이 모든 광물에서 "ResourceNodeData가 없습니다"를 띄우고 **아무것도 캐지 않습니다.**
  2. 레시피 에셋 12개는 `craftingTime`으로 저장돼 있습니다. 코드의 필드 `craftTime`은 기본값 4초로 돌아갑니다.
  3. `Assets/Resources/GameDatabase.asset`이 없습니다. 그래서 `GameDatabase.GetItem`/`GetRecipe`/`GetNode`가 전부 null을 돌려줍니다. 리뷰 B2-3("id를 찾지 못한 항목은 조용히 버림")이 **모든 아이템에서** 일어나는 상태입니다. 이대로 불러오고 저장하면 창고와 인벤토리 내용물이 영구히 사라집니다.
- **수정안:** S1·S2로 컴파일이 되면, **아무 세이브도 열기 전에** Tools › Data Importer › "전체 임포트"를 한 번 실행합니다. CSV가 원본이라서 `nodeId`와 `craftTime`이 다시 채워지고 DB도 만들어집니다. 앞으로 필드 이름을 바꿀 때는 `[FormerlySerializedAs("이전이름")]`을 붙이는 것을 규칙으로 둡니다.
- **참고:** enum은 이름만 바뀌고 순서는 같아서(`IMPURE,NORMAL,PURE` → `Impure,Normal,Pure` 등) 저장된 정수값이 그대로 맞습니다. `ItemCategory`의 0번은 `NONE`에서 `Raw`로 바뀌었는데, 이는 의도한 변경으로 보입니다.

### S4 · 씬에 스크립트 컴포넌트가 하나도 없고 건물 프리팹이 없음 — **P0 (확인 필요)**
- **위치:** `Assets/Scenes/LoadingScene.unity`, `MainScene.unity`, `Assets/Prefabs/`
- **문제:** 두 씬 파일 모두 `m_Script` 참조가 0개입니다. `MainScene.unity`는 2022-05-27 템플릿 그대로입니다. `Prefabs/` 폴더도 비어 있습니다. 그래서 이 저장소만으로는 GameBootstrapper·매니저·SaveController·BuildingCatalog·BuildingPlacer가 놓인 씬도, 채굴기·벨트·창고·제작기 프리팹도 없습니다. 리뷰의 재현 경로("채굴기 → 벨트 → 창고")를 지금은 실행해 볼 수 없습니다. 또 `LoadingSceneController`는 `"GameScene"`이라는 씬을 찾는데 그런 씬이 없습니다.
- **수정안:** 다른 PC나 저장하지 않은 씬에 구성이 있다면 그 씬과 프리팹을 이 저장소로 가져옵니다. 없다면 에디터에서 씬을 구성해야 합니다(CLAUDE.md 규칙상 `.unity`/`.prefab`은 직접 수정하지 않음). 필요하면 배치할 오브젝트와 연결할 필드를 체크리스트로 정리해 드리겠습니다.

### S5 · CSV에 주석 줄이 남아 있음 — CLAUDE.md의 교훈과 어긋남 — **P1**
- **위치:** `Assets/Data/CSV/items.csv`, `recipes.csv`, `resource_nodes.csv` 첫 몇 줄
- **문제:** 세 파일 모두 `#` 주석 줄이 있습니다. 게다가 `recipes.csv`의 주석 줄 끝에는 `,,,,,`가 붙어 있어서 이미 Excel로 한 번 저장된 흔적이 있습니다. 지금은 주석이 따옴표로 감싸져 있지 않아서 동작합니다. 하지만 `items.csv` 2행처럼 쉼표가 들어간 주석(`id(필수, 에셋 파일명이 됨)`)은 Excel로 다시 저장하면 `"# ..."`로 감싸집니다. 그러면 주석 건너뛰기가 깨지고 그 줄이 헤더로 읽힙니다. 지난번 임포트 실패와 같은 원인입니다.
- **수정안:** 주석은 `Assets/Data/CSV/README.md`나 Excel의 별도 시트로 옮깁니다. CSV에는 헤더와 데이터만 둡니다. 코드 쪽에서는 `ReadCSV`가 줄 맨 앞의 따옴표를 벗긴 뒤 `#`를 검사하도록 바꾸면 이중으로 안전합니다.

### S6 · 리뷰 문서 사본과 README가 저장소에 정리되지 않음 — **P2**
- 프로젝트 루트의 `새 폴더/code-review-2026-11-21.md`는 `docs/`의 리뷰와 같은 파일(35,663바이트)입니다. 지워도 됩니다.
- 리뷰가 근거로 삼은 `README.md`(11-04·11-07 작업 기록, "4절 아직 남은 것")가 저장소에 없습니다. K1~K7의 출처이고, CLAUDE.md도 "수정 내용은 README에 남긴다"고 정하고 있으므로 README를 가져와야 합니다.
- 새 파일 18개가 아직 git에 추가되지 않았습니다(untracked). S1~S3를 마친 뒤 한 번에 커밋하는 것을 권합니다.

### S7 · 마주 보는 벨트 두 개가 서로를 출력 대상으로 잡아 아이템이 왕복함 — **P2**
- **위치:** `Buildings/Conveyorbelt.cs` 42행
- **문제:** `outputTarget`은 앞 칸의 `IItemInput`을 방향과 상관없이 잡습니다. 앞 칸 벨트가 나를 향하고 있으면 둘이 서로에게 밀어 넣으면서 아이템이 영원히 오가고, 어디에도 배달되지 않습니다.
- **수정안:** 앞 칸이 벨트라면 `Vector3.Dot(neighbor.transform.forward, transform.forward) > -0.5f`일 때만 연결합니다. 리뷰 W5의 격자 딕셔너리로 바꿀 때 방향 검사를 같이 넣으면 됩니다.

---

## 2. 새 잠재 경고

### V1 · `ChunkData.buildings` 필드가 쓰이지 않고, 청크를 읽을 때마다 배열을 두 번 할당함 — **P2**
- **위치:** `World/ChunkData.cs` 10–11행, `Save/SaveSystem.cs` 114·116행
- 건물 B안 이후 청크는 건물을 들고 있지 않습니다. 그런데 `buildings` 리스트가 청크마다 생깁니다. 또 필드 초기화에서 `float[4096]`을 만든 뒤 `LoadChunk`가 새 배열로 바로 덮어씁니다.
- **수정안:** `buildings`를 지우고, 생성자에서 `heights`를 받게 합니다. 리뷰 W9(`ReadBytes` 한 번 + `Buffer.BlockCopy`)와 같이 처리합니다.

### V2 · `Buildings/ResourceNodeBehaviour`가 "노드마다 GameObject를 만들지 않는다"는 결정과 맞지 않음 — **P2 (아키텍처 확인 필요)**
- 쓰는 곳도, 씬에서 참조하는 곳도 없습니다. 광맥은 이제 `ChunkGenerator.GetOreDistribution`으로 위치마다 계산합니다. 면접관이 보면 "확정된 설계와 남은 코드가 다르다"는 지적을 받을 수 있습니다.
- **수정안:** 지우거나, 에디터에서 광맥 분포를 보여 주는 디버그 도구(K3 광맥 시각화)로 용도를 바꿉니다.

### V3 · 같은 `instanceId`를 가진 건물이 파괴되면 다른 건물의 등록까지 지워짐 — **P2 (리뷰 W2에 추가)**
- **위치:** `Buildings/BuildingBase.cs` 43행, `Buildings/BuildingRegistry.cs` 14행
- W2의 상황(id 겹침)에서 한쪽이 `OnDestroy`로 `Unregister(id)`를 부르면, 살아 있는 다른 건물도 목록에서 빠져 저장되지 않습니다.
- **수정안:** `Unregister(string id, BuildingBase b)`로 바꾸고, `_live[id] == b`일 때만 지웁니다.

### V4 · 로드 도중 범위 밖이 된 청크가 다음 경계 이동 때까지 남음 — **P2 (리뷰 B12와 같이)**
- **위치:** `World/WorldStreamingManager.cs` 75–78행
- `await` 사이에 플레이어가 다른 청크로 넘어가면, 이미 필요 없어진 좌표도 `_loaded`에 등록됩니다.
- **수정안:** B12의 프레임 예산 큐로 바꿀 때, 큐에서 꺼낸 시점에 "아직 필요한 좌표인지"를 다시 확인합니다.

### V5 · CSV 파서가 따옴표 이스케이프(`""`)를 처리하지 않음 — **P2 (리뷰 B8·W16과 같이)**
- **위치:** `Editor/DataImporter.cs` 156행
- Excel은 셀 안의 `"`를 `""`로 저장합니다. 지금 파서는 따옴표를 만날 때마다 토글만 하므로 `"5"" 파이프"`가 `5 파이프`로 읽힙니다.
- **수정안:** 따옴표 안에서 `""`를 만나면 `"` 하나를 넣고 한 칸 건너뜁니다.

---

## 3. 리뷰 항목 대조

리뷰의 위치(파일·행)를 지금 코드에서 하나씩 확인했습니다.

| ID | 위치 일치 | 상태 | 비고 |
|---|---|---|---|
| B1 | ✅ Inventory 27행, Storage 24행 | 미수정 | 한도는 아이템별 `stackSize` — R2 참고 |
| B2 | ✅ SaveSystem 145·151–155, SaveController 32·37·52–53, Storage 51–52 | 미수정 | S3 때문에 지금은 모든 아이템에서 발생 |
| B3 | ✅ WorldPreGenerator 57–62, SaveSystem 37·236–237·83–90·307–316 | 미수정 | |
| B4 | ✅ SaveSystem 194–195·145 | 미수정 | |
| B5 | ✅ WorldStreamingManager 71–76 | 미수정 | |
| B6 | ✅ TickManager 43 | 미수정 | |
| B7 | ✅ DataImporter 50·66·93·136 | 미수정 | CLAUDE.md "미완료" 항목 — **적용 안 됨으로 확정** |
| B8 | ✅ DataImporter 75–77·80·96 | 미수정 | |
| B9 | ✅ ManufacturerBuilding 80–83 | 미수정 | |
| B10 | ✅ WorldStreamingManager 53, LoadingSceneController 27·29 | 미수정 | **수정안 정정 필요 — R1** |
| B11 | ✅ LoadingSceneController 57–58, SaveSystem 58–63 | 미수정 | |
| B12 | ✅ WorldStreamingManager 63–85, SaveSystem 115, ChunkInstance 28–59 | 미수정 | V4 추가 |
| B13 | ✅ InputManager 18, PlayerMovementManager 48 | 미수정 | |
| W1~W17 | ✅ 전부 | 미수정 | W4·W5의 파일명은 S2 이후 `ConveyorBelt.cs` |
| K1~K7 | — | 미수정 | 출처 README가 저장소에 없음 (S6) |

**CLAUDE.md "미완료 / 확인 필요" 항목 결과**
- DataImporter 방어 패치 2건 → **적용되지 않았습니다** (B7).
- 광맥 확률 분포 방식 전환 → **적용됐습니다.** `ChunkGenerator.GetOreDistribution`(띠 노이즈 × 군집 노이즈 × 희귀도, 합이 1을 넘으면 정규화)과 `MinerBuilding`의 확률 가중 추첨으로 구현돼 있습니다.

---

## 4. 리뷰 정정

### R1 · 이 프로젝트는 Unity 6이 아니라 **2022.3.62f3** — B10의 `Awaitable` 수정안을 쓸 수 없음
- `ProjectSettings/ProjectVersion.txt` 기준입니다. `Awaitable`은 Unity 2023.1부터 있습니다. `MonoBehaviour.destroyCancellationToken`은 2022.2부터 있으므로 쓸 수 있습니다.
- **바꾼 수정안:**
  ```csharp
  private async Task LoadAsync(Vector2Int coord)
  {
      var token = destroyCancellationToken;   // 이 오브젝트가 파괴되면 취소됨
      ...
      await Task.Yield();
      if (token.IsCancellationRequested) return;   // 플레이 종료·씬 전환 뒤에는 이어서 실행하지 않음
      chunk.BuildMesh(data);
      ...
  }
  ```
  ```csharp
  // 반환값을 버리는 호출부용 — 예외를 반드시 콘솔에 남긴다
  public static class TaskExt
  {
      public static async void Forget(this Task task, Object context = null)
      {
          try { await task; }
          catch (OperationCanceledException) { }
          catch (Exception e) { Debug.LogException(e, context); }
      }
  }
  // 사용: LoadAsync(coord).Forget(this);
  ```
- 같은 이유로 리뷰 8절 Q&A에서 Unity 버전을 언급한다면 2022.3 LTS로 고쳐서 답해야 합니다.

### R2 · B1의 "50개, 약 17분"은 고정값이 아님
- 한도는 아이템마다 `stackSize`이고, 현재 철광석 에셋은 `stackSize: 100`입니다. 같은 조건(분당 약 3개)이라면 창고는 약 33분 뒤에 멈춥니다. 버그 자체와 수정안은 그대로 유효합니다.

### R3 · 파일 개수
- 리뷰는 "47개(빈 파일 8개)"라고 했지만, 최신 버전 파일은 44개이고 그중 안내 문구만 있는 빈 파일이 9개입니다(`OreDefinition`, `RecipeIngredient`, `MovementConfig`, `BuildingSaveData`, `InventoryEntry`, `PlayerLiveData`, `HardcodedOreTable`, `IOreTable`, `NodeEntry`). 지금 51개는 여기에 S1의 구버전 7개를 더한 숫자입니다. CLAUDE.md의 "약 41개"도 고쳐야 합니다. S1과 빈 파일 정리를 마치면 35개입니다.
- 빈 파일 9개는 컴파일에 문제가 없지만, 파일 안의 안내대로 S1과 같이 지우는 것을 권합니다.

### R4 · 리뷰가 전제한 실행 환경(씬·프리팹·GameDatabase)이 저장소에 없음
- S3·S4 참고. 리뷰의 재현 경로와 성능 추정은 씬을 구성한 뒤에야 확인할 수 있습니다.

---

## 5. 갱신한 로드맵

리뷰 7절 앞에 **P0-pre** 단계를 추가합니다. 나머지 순서는 리뷰를 그대로 따릅니다.

### P0-pre — 컴파일과 실행 환경 복구 (에디터 작업 위주)
0-1. **S1** 구버전 파일 7개 + 빈 파일 9개 삭제
0-2. **S2** `ConveyorBelt.cs`로 이름 변경
0-3. **S3** 전체 임포트로 SO와 `GameDatabase.asset` 재생성 (세이브를 열기 전에)
0-4. **S4** 씬과 프리팹 확보 또는 구성, Build Settings에 LoadingScene·GameScene 등록
0-5. **S6** 리뷰 사본 삭제, README 반영, 커밋

### P0 — 리뷰 그대로
1. B1 → 2. B3 → 3. B2 → 4. B4

### P1 — 리뷰 그대로, 아래만 바뀜
- 6번 DataImporter 묶음: **B7 · B8 · W15** + **S5**(CSV 주석 정리) + **V5**(따옴표 이스케이프)
- 8번 B10: **R1의 수정안**(`Task` + `destroyCancellationToken` + `Forget`)으로 진행
- 9번 B5 · B12: **V4** 포함

### P2 — 리뷰 그대로, 아래만 추가
- 13번 격자 점유 딕셔너리: **S7**(마주 보는 벨트) 포함
- 14번 할당 최적화: **V1** 포함
- 15번 수명과 소유권: **V3** 포함
- 16번 정리: **V2** 포함

---

## 6. CLAUDE.md에 반영하면 좋을 내용 (제안)

CLAUDE.md는 수정하지 않았습니다. 아래 내용을 반영할지 정해 주세요.
- Unity 버전: **2022.3.62f3 LTS** (Awaitable 없음)
- 파일 개수: 정리 후 35개
- 규칙 추가: 직렬화 필드 이름을 바꿀 때는 `[FormerlySerializedAs]`를 붙인다
- 규칙 추가: 파일을 옮기거나 이름을 바꿀 때는 이전 파일 삭제까지 한 작업 단위로 한다
- 리뷰 9절의 "알리고 보존하기" 3원칙을 코드 스타일 규칙에 추가
- "미완료" 항목 갱신: 광맥 확률 분포 → 완료 / DataImporter 방어 패치 → 미적용(B7)
