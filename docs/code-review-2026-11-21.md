# 공장 게임 프로토타입 — 심층 코드 리뷰

- 날짜: 2026-11-21
- 대상: `Scripts/` 폴더의 .cs 파일 47개(빈 파일 8개 포함)와 README.md(11-04, 11-07 작업 기록)
- 방법: 정적 분석만 했습니다. Unity에서 실행하지 않았으므로, 성능 수치나 실행 타이밍에 관한 항목은 프로파일러로 확인해야 합니다.

> Claude Code에서 쓰기: 이 파일을 프로젝트의 `docs/`에 두면 CLAUDE.md의 "로드맵을 따른다" 규칙에 따라 아래 **7절 로드맵** 순서로 작업합니다.
> 각 항목의 ID(B1, W3 …)로 지시하면 됩니다. 예: "B2 고쳐줘".

---

## 0. 요약

| 구분 | 개수 | 내용 |
|---|---|---|
| 확정 버그 (B) | 13 | 코드만 읽어도 재현 경로가 나오는 문제 |
| 잠재 경고 (W) | 17 | 지금은 드러나지 않지만 조건이 맞거나 기능이 추가되면 터지는 문제 |
| 이미 알려진 것 (K) | 7 | README "아직 남은 것"에 적힌 항목. 새로 세지 않고 로드맵에만 넣었습니다 |

**가장 먼저 고칠 세 가지 (P0)**
1. **B1** 창고에 아이템을 종류마다 50개까지만 넣을 수 있습니다. README가 목표로 한 "계속 쌓인다" 장면이 약 17분 뒤에 멈춥니다.
2. **B2** 불러오기가 실패해서 빈 데이터로 시작해도, 게임을 끄면 자동 저장이 그 빈 데이터로 원래 세이브를 덮어씁니다.
3. **B3** 새 게임 생성 도중 앱을 끄면, 반쯤 쓴 world.bin이 "저장 있음"으로 인식돼 그 슬롯이 계속 깨진 상태로 열립니다.

세 가지 모두 **실패를 "기본값으로 대체"해서 조용히 넘기는 패턴**에서 나옵니다. 지난번 DataImporter에서 얻은 교훈("성공 로그 전에 예외가 나면 조용히 실패한다")이 저장 시스템에도 그대로 남아 있습니다. 9절에 이 패턴을 한 번에 다루는 방법을 정리했습니다.

---

## 1. 잘 된 점

리뷰의 균형을 위해, 그리고 면접에서 설명할 거리로 먼저 적습니다.

- **세이브 분리가 명확합니다.** 변하지 않는 지형(`world.bin`, 인덱스 테이블로 Seek)과 변하는 상태(`live.json`)를 나눴고, 매직 넘버와 버전 헤더로 형식이 바뀌어도 옛 파일을 구분합니다.
- **광맥을 저장하지 않고 시드로 다시 계산합니다.** 노드 28만 개를 없애고 월드 파일을 수 MB 줄인 결정이 좋습니다. 확률을 연속값으로 바꾼 설명(README 3단계)도 면접에서 그대로 쓸 만합니다.
- **건물 상시 유지(B안)** 덕분에 고스트 건물, 중복 생성, 재방문 시 소실 같은 문제가 구조적으로 사라졌습니다.
- **TickManager의 swap-back 역순 순회**가 정확합니다. 끝 원소가 이미 Tick을 받았다는 점까지 고려했습니다.
- **벨트 막힘이 소스까지 전파됩니다.** 용량 4가 차면 더 꺼내지 않아서, 아이템이 사라지지 않는 배압(backpressure)이 생깁니다.
- **GameDatabase의 `ResetStatics`** 로 Domain Reload를 꺼도 캐시가 남지 않게 했습니다. 다만 같은 처리를 다른 static에는 하지 않았습니다(W3).

---

## 2. 확정 버그

### B1 · 창고와 인벤토리에 아이템을 종류마다 50개까지만 넣을 수 있음 — **P0**
- **위치:** `Core/Inventory.cs` 27행, `Buildings/StorageBuilding.cs` 24행
- **문제:** `TryAdd`가 "슬롯 하나의 최대치"인 `stackSize`를 **그 아이템의 총 보유 한도**로 씁니다. 그래서 슬롯이 20개인 창고도 철광석은 50개까지만 받습니다. 남은 19칸은 다른 종류의 아이템에만 쓸 수 있습니다.
- **재현:** 채굴기 → 벨트 → 창고. 중앙값 산출(분당 약 3개, README 4절) 기준으로 약 17분 뒤 창고가 50개에서 멈추고, 막힘이 채굴기까지 전해집니다. 플레이어 인벤토리도 똑같습니다.
- **수정안:** 슬롯을 `(ItemData item, int count)` 배열로 바꾸고, 한 칸이 `stackSize`를 넘으면 다음 칸으로 넘깁니다. 지금처럼 Dictionary를 유지하고 싶으면 한도를 `stackSize × 남은 슬롯 수`로 계산해도 됩니다. 다만 UI에서 슬롯을 그리려면 결국 배열이 필요합니다.
  ```csharp
  // 최소 수정: 종류별 총량 한도 = 그 아이템이 차지할 수 있는 슬롯 수 × stackSize
  int slotsUsedByOthers = /* 다른 아이템들이 차지한 슬롯 수 합 */;
  int maxForItem = (_maxSlots - slotsUsedByOthers) * item.stackSize;
  ```
- **주의:** 세이브의 `InventoryEntry{itemId, amount}`는 총량 기준이라 형식을 바꾸지 않아도 됩니다.

### B2 · 불러오기 실패가 다음 저장에서 영구 손실로 굳음 — **P0**
- **위치:** `Save/SaveSystem.cs` 145·151–155행, `Save/SaveController.cs` 32·37행, `Buildings/StorageBuilding.cs` 51–52행, `Save/SaveController.cs` 52–53행
- **문제:**
  1. live.json 파싱이 실패하면 `new LiveData()`로 대체합니다. 그러면 건물이 하나도 없고 플레이어는 (0,0,0)에 놓입니다.
  2. 이 상태에서 게임을 끄면 `OnApplicationQuit → SaveNow`가 빈 데이터를 저장해서 **원래 파일을 덮어씁니다.** 손으로 고칠 수도 있었던 파일이 사라집니다.
  3. 같은 일이 아이템 하나 단위로도 일어납니다. `GameDatabase.GetItem`이 null을 돌려주면 그 항목을 조용히 건너뛰는데, 원인은 GameDatabase.asset이 빌드에 없거나 CSV에서 id를 바꾼 경우입니다. 그 상태로 다음에 저장하면 그 아이템은 영구히 사라집니다.
- **수정안:**
  - 파싱이 실패하면 원본을 `live.json.corrupt-<시각>`으로 복사해 두고, `SaveSystem`에 `LoadFailed` 플래그를 세웁니다.
  - 플래그가 서 있으면 자동 저장(종료 시)을 막습니다. F5 같은 수동 저장만 경고 후 허용합니다.
  - id를 찾지 못한 항목은 버리지 말고 `_unresolved` 목록에 원본 그대로 보관했다가, 저장할 때 다시 써 줍니다. 그러면 데이터를 고친 뒤 되살릴 수 있습니다.

### B3 · 생성 도중 실패하거나 앱을 끄면 반쯤 쓴 world.bin이 "저장 있음"으로 인식됨 — **P0**
- **위치:** `World/WorldPreGenerator.cs` 57–62행, `Save/SaveSystem.cs` 37행(`Exists`)·236–237행(인덱스 0 채움)·83–90행(읽기)·307–313행
- **문제:**
  - 헤더와 0으로 채운 인덱스 테이블을 **맨 처음에** 씁니다. 실제 인덱스는 `Finalize`에서 채웁니다. 그래서 그 사이에 실패하거나 앱이 종료되면 형식상 유효한 world.bin이 남습니다.
  - `Exists()`는 world.bin이 있는지만 봅니다. 그래서 다음 실행 때 로딩 씬이 이 파일을 "이어하기"로 엽니다.
  - 인덱스는 전부 `(0,0, offset 0)`입니다. 그래서 청크 (0,0)를 읽으면 파일 헤더 바이트를 높이값으로 읽습니다. 나머지 청크는 "인덱스 없음"이 됩니다.
  - live.json이 없으니 플레이어는 (0,0,0)에 스폰합니다(B4 참고).
  - 예외 경로에서 `_currentWriter`를 Dispose하지 않아서 **파일 핸들도 열린 채 남습니다.**
  - 같은 원인으로, `WriteChunk`나 `Finalize`가 false를 돌려줘도 무시하고 "완료!"를 띄웁니다. 청크 하나만 실패해도 남은 0 엔트리가 진짜 (0,0) 엔트리를 덮어씁니다. 읽기 루프에서 나중에 나온 엔트리가 이기기 때문입니다.
- **수정안:**
  - `world.bin.tmp`에 쓰고, Finalize가 성공한 뒤에만 `world.bin`으로 rename합니다. 이것이 커밋 마커 역할을 합니다.
  - `WriteChunk`나 `Finalize`가 실패하면 예외를 던지게 바꿉니다. `GenerateAll`의 catch에서 `SaveSystem.AbortWorldWrite()`(Dispose + tmp 삭제)를 호출합니다.
  - 버전을 v3로 올릴 때(K1 청크 틈 수정과 같이) 헤더에 `chunkCount`와 함께 "완료 플래그"를 넣으면 이중으로 안전합니다.

### B4 · live.json 교체가 원자적이지 않고, 파일이 없으면 맵 구석에 스폰함 — **P1**
- **위치:** `Save/SaveSystem.cs` 194–195행, 145행
- **문제:** `File.Delete(final)`와 `File.Move(tmp, final)` 사이에 앱이 죽으면 live.json이 없어지고 `.tmp`만 남습니다. 다음 실행에서는 `new LiveData()`가 되어 플레이어가 (0,0,0)에 놓입니다. 그곳은 평지 존(544, 544)에서 먼 구석 청크이고, 높이가 최대 26까지 올라가는 지형 아래라서 플레이어가 땅속에 갇히거나 계속 떨어집니다.
- **수정안:**
  ```csharp
  if (File.Exists(finalPath)) File.Replace(tempPath, finalPath, finalPath + ".bak");
  else                        File.Move(tempPath, finalPath);
  ```
  불러올 때 live.json이 없으면 `.tmp`, 그다음 `.bak` 순서로 찾습니다. 그래도 없으면 world.bin 헤더에 저장해 둔 스폰 위치를 씁니다(v3 헤더에 추가).

### B5 · 풀에서 꺼낸 청크가 한 프레임 동안 이전 지형 메시와 콜라이더로 활성화됨 — **P1**
- **위치:** `World/WorldStreamingManager.cs` 71–76행
- **문제:** `SetActive(true)`가 `await Task.Yield()` **앞에** 있고, `BuildMesh`는 그 **뒤에** 있습니다. 재사용된 청크는 다른 좌표의 메시와 MeshCollider를 그대로 가진 채 새 위치에 한 프레임 동안 나타납니다. 그 프레임의 FixedUpdate에서 플레이어나 레이캐스트가 엉뚱한 높이의 지형과 부딪힐 수 있습니다.
- **수정안:** 위치 이동, `BuildMesh`, `SetActive(true)` 순서로 바꿉니다. 메시 굽기를 다음 프레임으로 미루고 싶다면, 활성화도 그 뒤로 같이 미룹니다.

### B6 · 건물 하나의 예외가 그 프레임의 나머지 건물 Tick을 전부 멈춤 — **P1**
- **위치:** `Core/TickManager.cs` 43행
- **문제:** 루프에 예외 격리가 없습니다. 예를 들어 레시피의 `output`이 null이면(B8) `ManufacturerBuilding.TryStartCraft`의 `GetAmount(null)`이 Dictionary에서 `ArgumentNullException`을 던집니다. 그러면 그보다 인덱스가 작은 건물은 **매 틱 한 번도 Tick을 받지 못합니다.** 콘솔에는 같은 에러가 초당 10번씩 쌓이고, 공장 절반이 멈춘 이유를 찾기 어렵습니다.
- **수정안:**
  ```csharp
  try { building.Tick(delta); }
  catch (Exception e)
  {
      Debug.LogException(e, building);  // 오브젝트를 넘겨서 콘솔에서 클릭하면 해당 건물로 이동
      building.enabled = false;         // 또는 오류 상태 플래그 + 시각 표시
  }
  ```

### B7 · DataImporter 방어 패치 2건이 아직 적용되지 않음 — **P1**
- **위치:** `Editor/DataImporter.cs` 50·66·93행(`row["id"]`), 136행(`File.ReadAllLines`)
- **문제:** 지난번에 제안했지만 지금 코드에도 그대로 있습니다.
  1. 헤더에 `id`가 없거나 오타가 있으면 `KeyNotFoundException`이 납니다. OnGUI 안에서 예외가 나므로 `RebuildDatabase`와 로그 패널 출력이 모두 실행되지 않습니다.
  2. Excel이 파일을 열고 있으면 `IOException`이 납니다. 결과는 위와 같습니다.
- **수정안:**
  ```csharp
  if (!row.TryGetValue("id", out var id) || string.IsNullOrWhiteSpace(id))
  { Log($"⚠️ {Path.GetFileName(csv)} {lineNo}행: id 없음 — 건너뜀"); continue; }
  ```
  ```csharp
  string[] lines;
  try { lines = File.ReadAllLines(path); }
  catch (IOException e) { Log($"❌ 읽기 실패 (Excel에서 열려 있나요?): {e.Message}"); return result; }
  ```

### B8 · CSV의 id 오타와 빈 행이 조용히 잘못된 데이터를 만듦 — **P1**
- **위치:** `Editor/DataImporter.cs` 75–77·80·96행
- **문제:**
  - 레시피 재료 id가 틀리면 `itemSO == null`이 되고, 그 재료를 **경고 없이 뺍니다.** 그래서 재료가 덜 드는(심하면 공짜인) 레시피가 생깁니다.
  - 결과물 id가 틀리면 `output = null`이 되고, 런타임에 B6처럼 예외가 납니다.
  - Excel은 끝에 `,,,,` 같은 빈 행을 자주 남깁니다. 이 행은 공백만 있는 줄 검사를 통과하고 `id = ""`가 되어, `Items/.asset` 경로로 에셋을 만들려고 합니다. 그러면 생성 오류가 나거나 이름 없는 에셋이 생깁니다. B7의 수정이 이 경우도 같이 막습니다.
  - `input1`이 비어 있으면 `break`하므로 `input2` 이후는 읽지 않습니다. 의도한 동작이라면 괜찮지만, 의도가 아니라면 빈 칸은 `continue`로 넘겨야 합니다.
- **수정안:** id를 찾지 못하면 그 **행 전체를 실패로 처리**하고, 파일명·행 번호·없는 id를 로그 패널에 남깁니다. 실패한 레시피는 SO를 갱신하지 않습니다. 임포트가 끝날 때 "경고 N건"을 요약해서 보여 줍니다.

### B9 · `SetRecipe`가 소모한 재료를 날리고, 이전 레시피의 재료가 입력 버퍼 슬롯을 영구히 차지함 — **P2**
- **위치:** `Buildings/ManufacturerBuilding.cs` 80–83행
- **문제:** 제작 중에 레시피를 바꾸면 이미 소모한 재료가 사라집니다. 또 이전 레시피의 재료는 입력 버퍼에 남는데, 새 레시피는 그 재료를 쓰지도 내보내지도 않습니다. 그래서 8칸 중 일부가 영구히 막힙니다.
- **수정안:** 제작 중이면 소모한 재료를 되돌립니다. 새 레시피에 없는 재료는 출력 버퍼로 옮기거나, 레시피를 바꾸는 UI에서 플레이어 인벤토리로 돌려줍니다.

### B10 · fire-and-forget async가 예외를 삼키고, 플레이 모드가 끝난 뒤에도 이어서 실행됨 — **P1**
- **위치:** `World/WorldStreamingManager.cs` 53행, `UI/LoadingSceneController.cs` 27·29행
- **문제:**
  - `_ = LoadAsync(...)`처럼 `async Task`를 버리면, 안에서 난 예외가 Task에 저장될 뿐 바로 콘솔에 뜨지 않습니다. 예를 들어 `_ui`나 `_generator`가 연결되지 않아 `StartNewGame`의 첫 줄에서 NRE가 나면, 로딩 화면이 아무 로그 없이 멈춥니다.
  - `Task.Yield()`의 continuation은 플레이 모드를 끈 뒤에도 에디터에서 실행될 수 있습니다. 그러면 파괴된 청크에 `BuildMesh`를 호출하게 됩니다(MissingReferenceException).
- **수정안:** 프로젝트가 Unity 6이므로 `Awaitable`과 `destroyCancellationToken`을 쓰는 것이 정석입니다.
  ```csharp
  private async Awaitable LoadAsync(Vector2Int coord)
  {
      ...
      await Awaitable.NextFrameAsync(destroyCancellationToken);
      ...
  }
  ```
  호출부에서 반환값을 버려야 한다면, 작은 확장 메서드 `Forget()`을 만들어 예외를 `Debug.LogException`으로 반드시 남기게 합니다.

### B11 · 월드를 열지 못해도 게임 씬으로 넘어가 빈 공간에 떨어짐 — **P1**
- **위치:** `UI/LoadingSceneController.cs` 57–58행, `Save/SaveSystem.cs` 58–63행
- **문제:** 옛 형식(v1) world.bin을 열거나 파일이 손상되면 `Initialize`가 실패하고 `Instance == null`이 됩니다. 그런데 바로 다음 줄에서 `LoadScene`을 호출합니다. 게임 씬에서는 지형이 생기지 않고 플레이어가 계속 떨어집니다. SaveController는 "로딩 씬에서 시작해야 합니다"라는 **원인과 다른 경고**를 띄웁니다. 지금은 README가 세이브 폴더를 손으로 지우라고 안내하는 것으로 이 문제를 피하고 있습니다.
- **수정안:** `Initialize`가 bool을 돌려주게 합니다. 실패하면 로딩 UI에 이유와 "새 게임으로 덮어쓰기" 버튼을 보여 줍니다. WorldPreGenerator가 실패했을 때도 같은 UI를 씁니다. 지금은 "다시 시도해주세요" 문구만 있고 시도할 버튼이 없습니다.

### B12 · 첫 로드와 청크 경계를 넘을 때 프레임 스파이크 (동기 IO + 한 프레임에 메시 일괄 굽기) — **P1 (성능, 프로파일 확인 필요)**
- **위치:** `World/WorldStreamingManager.cs` 63–85행, `Save/SaveSystem.cs` 115행, `World/ChunkInstance.cs` 28–59행
- **문제:** 이름은 `LoadAsync`지만 IO는 동기입니다. `_viewDistance = 3`이면 첫 프레임에 49청크의 `LoadChunk`가 메인 스레드에서 연달아 돌고, **다음 프레임에 49개의 `BuildMesh`와 MeshCollider 굽기가 한꺼번에** 실행됩니다. 청크 경계를 넘을 때마다 7청크씩 같은 일이 반복됩니다.
- **수정안 (효과가 큰 순서):**
  1. 프레임당 처리할 청크 수에 예산을 둡니다. 가까운 청크부터 큐에 넣고 한 프레임에 1~2개씩 처리합니다.
  2. `Physics.BakeMesh`를 `IJobParallelFor`로 미리 굽습니다(Unity 권장 패턴).
  3. W8과 W9의 할당과 IO를 개선합니다.

### B13 · 입력과 이동 매니저의 Update 순서가 정해지지 않아 1프레임 입력 지연이 생길 수 있음 — **P2**
- **위치:** `Player/InputManager.cs` 18행, `Player/PlayerMovementManager.cs` 48행, `Core/GameBootstrapper.cs`
- **문제:** `PlayerMovementManager.Update`(카메라 회전)가 `InputManager.Update`보다 먼저 실행되면, 카메라가 이전 프레임의 마우스 입력을 씁니다. 실행 순서를 정하는 코드가 어디에도 없습니다(W1).
- **수정안:** `[DefaultExecutionOrder(-100)]`을 InputManager에 붙입니다. 또는 W1처럼 부트스트래퍼가 매니저 Update를 순서대로 호출하게 합니다.

---

## 3. 잠재 경고

### W1 · GameBootstrapper가 주석과 달리 초기화 순서를 제어하지 않음 — **P1 (포트폴리오 관점)**
- **위치:** `Core/GameBootstrapper.cs`
- 주석에는 "초기화 순서를 명시적으로 제어한다 — 순서가 곧 의존성 선언"이라고 되어 있습니다. 하지만 실제로는 null 검사만 합니다. 각 매니저는 자기 `Awake`에서 `OnInitialize`를 부르므로 순서는 Unity 마음입니다. 면접관이 코드를 열어 보면 **설명과 코드가 다르다는 점**이 가장 먼저 눈에 띕니다.
- **수정안:** `SingletonManager.Awake`에서는 `Instance` 등록만 합니다. `OnInitialize`는 부트스트래퍼가 배열 순서대로 호출합니다. 아니면 주석을 "필수 참조 검증"으로 바꿔서 실제 동작과 맞춥니다. 어느 쪽이든 괜찮지만 주석과 코드가 일치해야 합니다.

### W2 · `instanceId`가 public 직렬화 필드라, 값이 겹치면 건물 하나가 저장에서 조용히 빠짐 — **P2**
- **위치:** `Buildings/BuildingBase.cs` 6행, `Buildings/BuildingRegistry.cs` 13행
- 프리팹 인스펙터에 값이 들어가거나 씬에 놓인 건물을 복제하면 id가 같아집니다. `_live[id] = b`가 경고 없이 덮어쓰므로 앞의 건물은 저장되지 않습니다.
- **수정안:** `[field: NonSerialized] public string InstanceId { get; private set; }`처럼 바꿉니다. `Register`에서 같은 키가 이미 있으면 에러를 남깁니다.

### W3 · Domain Reload를 끄면 static 상태가 남음 (GameDatabase만 처리됨) — **P2**
- **위치:** `BuildingRegistry._live`, `SaveSystem.Instance`(리더는 이미 닫힌 상태), `LoadingContext.Current`, `ChunkGenerator._seed`·`_flatCenter`
- Enter Play Mode Options에서 Domain Reload를 끄고 게임 씬을 바로 실행하면, 이전 플레이의 닫힌 리더 때문에 `LoadChunk`에서 ObjectDisposedException이 납니다.
- **수정안:** 각 클래스에 GameDatabase처럼 `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` 초기화를 붙입니다.

### W4 · 벨트 연결이 `??=`라서 파괴된 건물을 계속 붙잡음 — **P2 (철거 기능을 넣을 때)**
- **위치:** `Buildings/ConveyorBelt.cs` 41–42행
- `IItemOutput`이나 `IItemInput` 인터페이스로 들고 있는 파괴된 MonoBehaviour는 C# 기준으로 null이 아닙니다. 그래서 `??=`가 새로 연결하지 않고, 죽은 건물의 메서드를 계속 호출합니다.
- **수정안:** 연결 대상을 `BuildingBase`로 들고 Unity null 비교(`== null`)를 씁니다. 또는 `BuildingBase.OnDestroy`에서 이웃 벨트에 연결 해제를 알립니다.

### W5 · 경사면에서 높이를 스냅하지 않아 벨트 이웃 탐지가 실패할 수 있음 — **P2**
- **위치:** `Buildings/BuildingPlacer.cs` 101–103행, `Buildings/ConveyorBelt.cs` 48행
- y는 레이캐스트 높이를 그대로 씁니다. 그래서 이웃 칸과의 높이 차가 약 0.5를 넘으면 반지름 0.3인 구가 이웃 콜라이더에 닿지 않고, 벨트가 **아무 경고 없이** 연결되지 않습니다.
- **수정안:** 이웃 탐지를 물리 쿼리 대신 **격자 좌표 → 건물** 딕셔너리로 바꿉니다. 자원 노드에서 "타일 조회"를 택한 것과 같은 방향이고, 철거와 겹침 검사(W14)까지 한 번에 해결됩니다.

### W6 · 맵 밖 청크를 이동할 때마다 다시 로드하려 하고, 경고가 두 번씩 쌓임 — **P2**
- **위치:** `World/WorldStreamingManager.cs` 69행, `Save/SaveSystem.cs` 106행
- 맵 가장자리에서는 경계를 넘을 때마다, 맵 밖 좌표 전부(모서리 청크라면 49개 중 33개)에 대해 경고가 2개씩 찍힙니다.
- **수정안:** `GetCoordsInRange`에서 맵 범위(world.bin 헤더의 폭과 높이)로 잘라냅니다.

### W7 · `SetChunkVisible`이 청크마다 모든 건물을 훑음 — **P2**
- **위치:** `Buildings/BuildingRegistry.cs` 52–57행
- 첫 로드 때 49청크 × 건물 N개만큼 `WorldToChunk`를 계산합니다.
- **수정안:** `Dictionary<Vector2Int, List<BuildingBase>>` 청크별 버킷을 둡니다. 건물은 움직이지 않으므로 Register할 때 한 번만 계산하면 됩니다.

### W8 · `BuildMesh`가 매번 배열을 새로 할당하는데, 삼각형과 UV는 모든 청크가 같음 — **P2**
- **위치:** `World/ChunkInstance.cs` 32–34행
- `tris`와 `uvs`는 static으로 한 번만 만들고, `verts`는 인스턴스 버퍼를 재사용합니다. 메시 버퍼까지 재사용한 지난 최적화의 연장입니다. 덤으로 UV는 `x / (size - 1)`이어야 0~1을 정확히 채웁니다.

### W9 · `LoadChunk`가 `ReadSingle`을 4096번 호출함 — **P2**
- **위치:** `Save/SaveSystem.cs` 115행
- **수정안:** `ReadBytes(size * 4)` 한 번과 `Buffer.BlockCopy`로 바꿉니다. 데이터는 이미 연속된 float 배열 형식입니다.

### W10 · 복원 시 1.5만큼 띄우는 고정값이 큰 히치에서 부족할 수 있음 — **P2**
- **위치:** `Save/SaveController.cs` 13행
- 지형이 생기기 전까지 FixedUpdate가 몰아서 돌면, 최대 시간 간격 0.333초 기준으로 약 1.4m 떨어질 수 있습니다. B12의 스파이크와 겹치면 아슬아슬합니다.
- **수정안:** 발밑 청크가 `IsChunkLoaded`가 될 때까지 `CC.enabled = false`로 둡니다(이벤트 `OnChunkLoaded`).

### W11 · 시드가 x와 z에 같은 값으로 더해짐 — **P2**
- **위치:** `World/ChunkGenerator.cs` 52·87–88행
- 시드가 달라도 같은 무한 노이즈를 **대각선 방향으로 평행이동**한 월드가 됩니다. 지금은 시드 범위(0~9999)가 넓어서 체감하기 어렵지만, 시드의 의미로는 약합니다.
- **수정안:** `math.hash`로 시드에서 x 오프셋과 z 오프셋을 따로 파생합니다(지형 생성 대화에서 다룬 서브시드 패턴). float 시드 대신 `uint`를 저장하면 비교와 재현도 쉬워집니다.

### W12 · 같은 슬롯의 world.bin을 열어 둔 채 새 게임을 만들면 공유 위반 — **P2 (메뉴 흐름을 만들 때)**
- **위치:** `Save/SaveSystem.cs` 70행(읽기 FileStream, FileShare 기본값) ↔ 227행(`FileMode.Create`)
- **수정안:** `BeginWorldWrite`에서 같은 슬롯을 열고 있으면 먼저 `Close`합니다. B3의 tmp → rename 방식으로 바꾸면 자연스럽게 해결됩니다.

### W13 · SaveSystem의 리더를 WorldStreamingManager가 닫음 (소유권 누수) — **P2**
- **위치:** `World/WorldStreamingManager.cs` 99행
- Save와 World가 서로를 참조하는 순환(README 5절 6번)을 더 굳힙니다.
- **수정안:** SaveSystem의 수명은 SaveSystem 자신이 관리합니다(`Application.quitting`, 씬 전환 시 `Close`). World는 `IChunkSource` 인터페이스만 알게 하면, asmdef를 나눌 때 순환이 끊깁니다.

### W14 · 배치 겹침 검사가 1×1 크기에 회전을 무시하고, UI 클릭도 통과함 — **P2**
- **위치:** `Buildings/BuildingPlacer.cs` 105–107·59행
- 크기가 2×1인 건물부터 겹쳐서 놓입니다. 빌드 메뉴 버튼을 누르면 그 아래 땅에도 건물이 놓입니다.
- **수정안:** 건물마다 `footprint`(Vector2Int)를 두고 W5의 격자 딕셔너리로 검사합니다. 클릭 전에 `EventSystem.current.IsPointerOverGameObject()`를 확인합니다. 매 프레임 호출하는 `GetComponentsInChildren<Renderer>`는 `BeginPlace`에서 한 번만 캐싱합니다.

### W15 · CSV 인코딩 — 한국어 Excel의 기본 "CSV (쉼표로 분리)"는 CP949 — **P1 (확인 필요)**
- **위치:** `Editor/DataImporter.cs` 136행
- `File.ReadAllLines`의 기본 인코딩은 UTF-8입니다. Excel에서 "CSV UTF-8" 형식으로 저장하지 않았다면 `name_kr`이 `��`로 깨집니다. 지금 표시 이름이 정상이라면 이미 UTF-8로 저장하고 계신 것이니 무시하셔도 됩니다.
- **수정안:** 파일에 BOM이 없고 UTF-8 디코딩이 실패하면, 로그 패널에 "CSV UTF-8로 다시 저장하세요"라고 안내합니다.

### W16 · 숫자와 enum 파싱이 실패해도 경고 없이 기본값이 됨 — **P2**
- **위치:** `Editor/DataImporter.cs` 172–174행
- `float.TryParse`가 현재 문화권을 따르므로 독일어 등 쉼표 소수점 로케일에서 `4.5`를 읽지 못합니다. enum 오타(`Constuctor`)는 경고 없이 `Hand`가 됩니다.
- **수정안:** `CultureInfo.InvariantCulture`를 지정하고, 파싱이 실패하면 셀 위치를 로그에 남깁니다.

### W17 · CSV에서 지운 행의 에셋이 GameDatabase에 계속 남음 — **P2**
- **위치:** `Editor/DataImporter.cs` 112–114행
- DB를 CSV가 아니라 폴더 스캔으로 다시 만들기 때문입니다.
- **수정안:** 이번 임포트에서 쓴 id 집합과 비교해서, CSV에 없는 에셋을 목록으로 보여 주고 삭제 버튼을 둡니다. 자동 삭제는 위험하니 하지 않습니다.

> 설계 메모 (번호 없음): `ResourceNodeData.purity`가 이제 **광물 종류 전체**에 하나뿐입니다(철은 전부 Pure이거나 전부 Normal). 위치별 순도가 사라진 셈입니다. 확률 분포에 순도 노이즈를 하나 더 얹으면 새티스팩토리식 "좋은 자리 찾기"가 되살아납니다.

---

## 4. 이미 알려진 것 (README 4절)

| ID | 내용 | 로드맵 |
|---|---|---|
| K1 | 청크 사이 1칸 틈 (#3) — world.bin v3 필요 | P1 (B3·B4와 같이 v3로) |
| K2 | 진행 상태 미저장 (제작기 버퍼, 벨트 위 아이템, 채굴기 진행도) | P1 |
| K3 | 광맥 시각화 없음 | P2 |
| K4 | 철거·건설 비용 없음, 벨트 대상 파괴 처리 없음 (→ W4) | P2 |
| K5 | 점프 입력 유실 #14, CharacterController 50Hz 끊김 #21 | P1 |
| K6 | 씬 재진입 시 DontDestroyOnLoad 매니저가 이전 씬 오브젝트를 참조 | P2 (메뉴를 만들 때) |
| K7 | `PlayerController.OnDestroy`의 `Register(null)` → `Unregister` | P2 |

---

## 5. 요약표

| ID | 구분 | 우선순위 | 파일 | 한 줄 요약 |
|---|---|---|---|---|
| B1 | 버그 | P0 | Inventory, StorageBuilding | 종류당 50개 상한 — 창고가 금방 참 |
| B2 | 버그 | P0 | SaveSystem, SaveController | 불러오기 실패 → 종료 자동 저장이 원본 덮어씀 |
| B3 | 버그 | P0 | WorldPreGenerator, SaveSystem | 생성 중단 시 깨진 world.bin이 "저장 있음" |
| B4 | 버그 | P1 | SaveSystem | live.json 교체 비원자적, 없으면 구석 스폰 |
| B5 | 버그 | P1 | WorldStreamingManager | 재사용 청크가 이전 메시로 1프레임 활성 |
| B6 | 버그 | P1 | TickManager | 건물 하나의 예외가 나머지 Tick 차단 |
| B7 | 버그 | P1 | DataImporter | 방어 패치 2건 미적용 |
| B8 | 버그 | P1 | DataImporter | id 오타·빈 행이 조용히 잘못된 데이터 |
| B9 | 버그 | P2 | ManufacturerBuilding | 레시피 변경 시 재료 소실·슬롯 막힘 |
| B10 | 버그 | P1 | WorldStreamingManager, LoadingSceneController | async 예외 삼킴, 플레이 종료 후 실행 |
| B11 | 버그 | P1 | LoadingSceneController, SaveSystem | 월드 열기 실패해도 씬 전환 |
| B12 | 버그 | P1 | WorldStreamingManager, ChunkInstance | 동기 IO + 일괄 메시 굽기 스파이크 |
| B13 | 버그 | P2 | InputManager, PlayerMovementManager | Update 순서 미정 → 입력 1프레임 지연 |
| W1 | 경고 | P1 | GameBootstrapper | 주석과 달리 순서를 제어하지 않음 |
| W2 | 경고 | P2 | BuildingBase, BuildingRegistry | instanceId 겹치면 저장에서 누락 |
| W3 | 경고 | P2 | 여러 static | Domain Reload 꺼짐 시 상태 잔존 |
| W4 | 경고 | P2 | ConveyorBelt | 파괴된 건물을 계속 참조 |
| W5 | 경고 | P2 | BuildingPlacer, ConveyorBelt | 경사면에서 벨트 연결 실패 |
| W6 | 경고 | P2 | WorldStreamingManager | 맵 밖 청크 반복 로드·경고 스팸 |
| W7 | 경고 | P2 | BuildingRegistry | SetChunkVisible O(N) × 청크 수 |
| W8 | 경고 | P2 | ChunkInstance | 메시 배열 매번 할당 |
| W9 | 경고 | P2 | SaveSystem | ReadSingle 4096회 |
| W10 | 경고 | P2 | SaveController | 고정 띄움값이 히치에 취약 |
| W11 | 경고 | P2 | ChunkGenerator | 시드가 대각선 이동일 뿐 |
| W12 | 경고 | P2 | SaveSystem | 같은 슬롯 읽는 중 쓰기 → 공유 위반 |
| W13 | 경고 | P2 | WorldStreamingManager | Save 리더 소유권 누수·순환 |
| W14 | 경고 | P2 | BuildingPlacer | 1×1 전제 겹침 검사, UI 클릭 통과 |
| W15 | 경고 | P1? | DataImporter | CSV 인코딩 (확인 필요) |
| W16 | 경고 | P2 | DataImporter | 파싱 실패가 조용히 기본값 |
| W17 | 경고 | P2 | DataImporter | 지운 행이 DB에 남음 |

---

## 6. 우선순위 기준

- **P0:** 지금 플레이 목표("채굴 → 벨트 → 창고 → 저장 → 재실행해도 계속 쌓임")를 깨거나, 세이브를 되돌릴 수 없게 망가뜨림
- **P1:** 정상 플레이에서 겪을 수 있는 오동작, 원인을 찾기 어려운 조용한 실패, 면접에서 바로 지적될 설계 불일치
- **P2:** 기능을 추가하거나 규모를 키울 때 터지는 것, 성능 개선, 정리

---

## 7. 로드맵

### P0 — 저장의 신뢰성과 목표 장면
1. **B1** 인벤토리 슬롯 모델 수정
2. **B3** world.bin 쓰기를 tmp → rename으로, 실패 시 Abort
3. **B2** 불러오기 실패 감지: 손상 파일 백업, 종료 자동 저장 차단, 못 찾은 id 보존
4. **B4** `File.Replace` + `.bak`/`.tmp` 순서로 복구 (B2·B3와 같은 작업 단위)

### P1 — 조용한 실패 없애기와 체감 품질
5. **B6** TickManager 예외 격리
6. **B7 · B8 · W15** DataImporter 검증과 로그 정리 (한 번에)
7. **B11** 로딩 실패 UI (B3의 Abort와 연결)
8. **B10** `Awaitable` + `destroyCancellationToken`으로 전환
9. **B5 · B12** 청크 로드를 프레임 예산 큐로, 활성화는 메시를 만든 뒤에
10. **world.bin v3:** K1(청크 틈) + 스폰 위치(B4) + 맵 크기(W6) + 완료 플래그(B3)를 한 번에
11. **W1** 부트스트래퍼가 실제로 순서를 제어하게 (B13 포함)
12. **K2 · K5** 진행 상태 저장, 점프와 이동 끊김

### P2 — 확장 준비와 정리
13. **W5 · W14 · W4 · K4** 격자 점유 딕셔너리 도입 → 철거, 다칸 건물, 벨트 연결을 한 번에
14. **W7 · W8 · W9** 할당과 순회 최적화
15. **W2 · W3 · W12 · W13** 수명과 소유권 정리 (K6·K7과 같이, 메뉴 흐름을 만들기 전에)
16. **B9 · W10 · W11 · W16 · W17**, 순도 노이즈(설계 메모), K3

---

## 8. 면접 Q&A (이 코드 기준)

**Q1. 세이브를 바이너리와 JSON으로 나눈 이유는?**
변하는 빈도와 크기가 다르기 때문입니다. 지형은 크고(청크당 16KB) 생성 후 읽기만 하므로, 인덱스 테이블로 필요한 청크만 Seek합니다. 플레이 상태는 작고 자주 바뀌며 사람이 읽고 디버깅할 수 있어야 해서 JSON으로 통째로 씁니다. 저장할 때 world.bin을 건드리지 않으니, 저장 중 사고가 나도 지형은 안전합니다.

**Q2. 그 저장이 정말 안전한가요?** *(B2·B3·B4를 고친 뒤 답할 수 있는 질문)*
쓰기는 임시 파일에 한 뒤 원자적으로 교체하고, 생성은 Finalize가 끝나야 커밋됩니다. 읽다가 실패하면 원본을 백업해 두고 자동 저장을 막아서, 실패가 영구 손실로 굳지 않게 했습니다.

**Q3. 광맥을 저장하지 않는다면, 시드가 같을 때 결과가 같다는 걸 어떻게 보장하나요?**
같은 바이너리와 같은 플랫폼에서는 `Mathf.PerlinNoise`가 결정적입니다. 멀티플레이나 크로스플랫폼에서 비트 단위로 같아야 한다면 float 노이즈를 정수 해시 기반 노이즈로 바꾸거나 결과를 양자화해야 합니다. 지금은 싱글 플레이이고 채굴기가 배치될 때 한 번 계산해서 쓰므로 충분합니다.

**Q4. 건물마다 Update를 쓰지 않고 TickManager를 둔 이유와 그 단점은?**
네이티브와 매니지드 사이의 콜 오버헤드를 줄이고, 틱 주기를 시뮬레이션 단위로 고정하기 위해서입니다. 단점은 예외 하나가 루프 전체를 멈출 수 있다는 점입니다. 그래서 건물 단위로 격리했습니다(B6). 틱 간격을 바꾸면 결과가 달라지는 계산(타이머 리셋 시 남는 시간 손실)이 있다는 점도 알고 있습니다.

**Q5. 청크를 내려도 건물은 계속 돌게 한 결정의 비용은?**
CPU 비용이 건물 수에 비례해서 계속 듭니다. 대신 상태 동기화 문제(고스트, 중복, 소실)가 사라집니다. 규모가 커지면 화면 밖 건물은 "분당 산출량"으로 해석적으로 계산하는 방식(시뮬레이션 LOD)으로 옮길 수 있게, 건물마다 산출량을 이미 분당 단위로 정의해 두었습니다.

**Q6. 물리 쿼리로 이웃을 찾는 방식의 한계는?**
높이 차, 콜라이더 구성, 동기화 타이밍(`SyncTransforms`)에 결과가 좌우되고, 실패해도 조용합니다. 공장 게임은 본질적으로 격자이므로 `Dictionary<Vector2Int, Building>` 점유 맵이 더 맞습니다. 철거, 다칸 건물, 겹침 검사까지 같은 자료구조로 해결됩니다(W5·W14).

---

## 9. 공통 패턴: "기본값으로 대체"를 "알리고 보존하기"로

B2, B3, B4, B7, B8, B10, B11, W16은 모두 같은 형태입니다. 실패했을 때 그럴듯한 기본값을 넣고 계속 진행하는데, 그 기본값이 다음 단계(저장, 씬 전환, 에셋 갱신)에서 **사실처럼 확정**됩니다.

규칙 세 줄로 정리하면:
1. **경계에서 실패하면 결과 타입으로 돌려줍니다.** `bool Try...(out ...)`나 `Result`를 쓰고, 호출자가 무시할 수 없게 합니다.
2. **실패한 상태에서는 쓰지 않습니다.** 읽기에 실패한 세션은 자동 저장을 하지 않고, 검증에 실패한 행은 에셋을 갱신하지 않습니다.
3. **모르는 데이터는 버리지 말고 보존합니다.** 해석하지 못한 id나 항목은 원본 그대로 다시 써 줍니다.

이 세 줄을 CLAUDE.md의 코드 스타일 규칙에 추가해 두면, 앞으로 새 코드에서도 같은 문제가 생기지 않습니다.
