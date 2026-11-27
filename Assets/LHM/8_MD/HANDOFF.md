# docs/HANDOFF.md — 인수인계 문서

> 작업이 끝날 때마다 **덮어쓴다.** 이전 기록은 git 히스토리에 남는다.
> ⏰ 마감 모드 (AI_RULES 0장): 짧은 형식, Gemini 리뷰 생략. **커밋은 현민이 직접 한다.**

---

## 🛠 보스 씬 연결 툴: `UnMachine/Boss Setup` (현민 요청 2026-11-27, 기준 커밋 `bc3c69d`)
- **쓰는 법:** 실제 보스 씬을 열고 보스 선택 → `열린 씬 보스에 v2 연결` → Console 보고 확인 → Ctrl+S. `연결 상태 검사` = 읽기 전용 점검.
- **하는 일:** 보스에 `BossFlowDriver`·`BossTelegraphView`·`ComboSignalView`·`BossGroggyDriver` 추가(없을 때만), Config 4종(BossFlow·PatternTable·Groggy·JumpSlam — 없으면 `3_ScriptableObject` 에 생성) 연결, `BossController` Flow Driver·Use Flow V2 On. **이미 값이 있는 칸은 덮어쓰지 않음.** Undo 한 묶음, 씬 Dirty 표시만.
- **`그로기 바 UI 만들기` (T67):**
  - **1순위 (Chapter_1_Boss 등 SYS 체력바 씬, 현민 지정):** 보스 `DamageHandler` 에 연결된 `BossHealthBarBridge` 의 비주얼(`Boss HP`, `UnifiedHealthBarVisual`)을 복제 → 체력바 바로 아래 `Groggy Bar`(+BossGroggyBar) → `Fill`(복제본, 브릿지 제거). 같은 폭, 높이 = 체력바 × 0.35(최소 8), 간격 8. 머티리얼은 `Boss Health.mat` → **`Assets/LHM/1_Material/Boss Groggy.mat`** 복사(채움 색 `_ColorFill` 주황, 원본 그대로).
  - 2순위: 레거시 `BossController.healthBar` 아래 Image 바 → 3순위: Canvas 위쪽 가운데 (Canvas 없으면 `BossUICanvas` 생성). 이미 BossGroggyBar 가 있으면 만들지 않음.
  - `BossGroggyBar.cs` 수정: `_visual`(UnifiedHealthBarVisual) 칸 추가 — 지정 시 Image 대신 `SetFill(게이지)`, 표시될 때 고스트 HP 끔.
- **안 하는 일:** 씬 열기·저장, 프리팹 원본 수정, 증기·경고음(T59)·JumpSlamConfig 방 값(T54).
- **변경 파일:** `Editor/BossSceneSetupTool.cs` (새), `Editor/BossTestSceneBuilder.cs` (`ResolveOrCreateConfig` private → internal 1줄)
- **규칙 변경 제안 (현민 판단):** AI_RULES 0장 2번 예외에 "현민이 요청한 **열린 씬 연결 툴**(Undo 가능, 저장은 현민, 기존 값 덮어쓰기 없음)" 추가. 이번 툴은 채팅에서 현민이 허락한 1회 예외로 작성.
- **커밋 제안:** `[chore] 보스 씬 v2 연결 툴 (UnMachine/Boss Setup)` — 위 2파일 + `BossSceneSetupTool.cs.meta`

---

## 🛠 테스트 도구: `UnMachine/Boss Test Panel` (현민 요청, 기준 커밋 `70953f2`)
- **쓰는 법:** 메뉴 `UnMachine/Boss Test Panel` 로 창 열기 → (테스트 씬에서) Play → 보스 자동 탐색.
  - **조작:** 강제 단위 P1~C4·해제 / 2P 전환(HP 49%) / HP 50% 경계 / 사망 / 그로기 +25 / 그로기 진입 / 기절
  - **상태:** 흐름·남은 시간·페이즈·배율 / 현재 패턴 / P1 점프 단계·피해 거부 / HP·그로기 / 씬에 남은 탄(포탄·총알/파편·에너지탄·마커·충격파·드론)
  - **체크리스트 (자동 판정):** P1 착지·충격파 적중·화면 밖 피해 0 / 그로기 / 2P 전환+틴트 / 전환 직후 남은 탄 0 / C1~C4 끝(이유 표시) / 콤보 신호 먼저 / 사망 후 남은 것 0 / Console 에러 0
  - **자동 순회:** P1 → 그로기 → 2P 전환 → C1 → C2 → C3 → C4 → 사망 (각 단계 완료 로그를 기다림, 최대 30초). 플레이어는 피하기만.
  - **보고서 복사:** 결과를 클립보드로 → 채팅에 붙여 주면 판단·수정.
- **변경 파일:** `Editor/BossTestPanel.cs` (새), 디버그 진입점(`#if UNITY_EDITOR`, 빌드 제외): `BossController.cs`(HP 비율·즉시 사망·마커 수·피해 거부 여부), `Gimmick/BossFlowDriver.cs`(강제 단위·기절·페이즈·패턴 이름·점프 단계·충격파 수), `Gimmick/BossGroggyDriver.cs`(+N), `Gimmick/Pattern/JumpSlamPattern.cs`(충격파 수, 런타임 읽기 전용)
- **한계:** 판정은 GameLog 문구 기준 (문구를 바꾸면 패널도 수정). 회피·점프 같은 손맛과 색·이펙트는 눈으로. C1 예외(명중·벽)는 자동 순회에서 일부러 맞거나 벽에 붙어야 나옴.
- **커밋 제안:** `[chore] 보스 테스트 패널 (자동 체크리스트·자동 순회·보고서)` — 위 파일 + `BossTestPanel.cs.meta`, `8_MD/HANDOFF.md`

### 현민 씬 테스트 결과 — 2차 (2026-11-27 12:25, 테스트 씬) ✅ 13/13 통과
- 착지 몸체 피해 수정 확인 (`몸체 적중 1 (피해 8)`), 충격파 적중 1회, 그로기 진입 1회 (자동 순회 그로기 단계), 나머지 1차와 동일하게 통과. Console 에러 0.
- 상태: **T46·T55·T56·T57·T58·T34·T23 씬 통과 → 현민 커밋 대기.** (T38·T39·T60·T61·T63 은 이전 커밋분)
- 남은 수동 확인(선택): C1 정상 경로 `Completed`(돌진 회피 → 0.3초 뒤 Brake → 짓밟기), C1 `WallStun`(벽에 바짝 붙어 점프), 눈으로 볼 것(아래).

### 현민 씬 테스트 결과 — 1차 (2026-11-27 12:14, 테스트 씬)
- ✓ 11 / ✗ 0 / 미확인 2. 컴파일 OK, Console 에러 0.
- ✓ T46 P1 착지 6회, 화면 밖 피해 0 / ✓ T33·T34 전환 + 붉은 틴트 / ✓ T23 전환 직후·사망 후 남은 탄·드론·충격파 0
- ✓ T56 C2·C3 Completed / ✓ T57 C1 (ChargeHitPlayer 경로), C4 Completed / ✓ T58 콤보 신호 4/4 먼저
- 미확인: T46 충격파 적중(서서 맞아보기), T61 그로기(→ 자동 순회에 그로기 단계 추가함). C1 정상 경로(돌진 회피 → Brake → 짓밟기 = `Completed`)와 WallStun 경로도 아직 기록 없음.
- 🐞 수정 (같은 날): **착지 몸체 피해가 항상 0** — 보스가 중력 0 이라 배치 높이(테스트 씬 y 3)에 떠 있어 몸체 판정 아래 끝이 바닥보다 3 높았음. `JumpSlamPattern.Land` 에서 판정 박스를 `Ground Y` 까지 늘림 (보스 위치는 그대로). Driver Gizmo(노랑)로 마지막 착지 박스 표시. → 그림자 위에 서 있으면 8 다시 확인.
- 충격파 맞는 법: 그림자가 **빨갛게 고정되면** 옆으로 3~4 Unit 벗어나 **바닥에 선 채로** 기다림 (몸 옆에서 출발해 바깥으로 이동). 그림자를 안 벗어나면 착지 판정(8)만 받고 충격파는 바깥으로 지나감.
- 눈으로 볼 것(패널이 판정 못 함): 예고 색·깜빡임, 그림자 색 변화, C1 Brake 정지 위치, C4 곡사 자세가 남는지.

---

## ★ 남은 에디터 작업 체크리스트 (현민) — 축소 계획 코드 작업 완료 후
> 순서대로 하면 됨. 테스트 씬(`UnMachine/Test Scene/Boss Arena`)은 1~3번 대부분이 자동이고, **실제 보스 씬**은 아래를 손으로.

### T36 ✂️ v2 흐름 켜기 (실제 보스 씬) — 🛠 툴로 자동
- [ ] 실제 보스 씬 열기 → Hierarchy 에서 보스 선택 → 메뉴 **`UnMachine/Boss Setup/열린 씬 보스에 v2 연결`** → Console 보고 확인 → **Ctrl+S**
  (Driver·TelegraphView·ComboSignalView·GroggyDriver 추가, Config 4종·참조 연결, `Use Flow V2` On. 이미 채워진 칸은 그대로. Ctrl+Z 한 번으로 되돌림)
- [ ] `UnMachine/Boss Setup/연결 상태 검사` 로 ✓/✗ 확인
- [ ] (선택) 보스 Animator 에 Int 파라미터 `State` 추가 → Console 의 `Parameter 'State' does not exist` 경고 사라짐 (상태 8~12 애니 전이는 생략 가능)

### T67 ✂️ 그로기 바 UI — 🛠 툴로 자동
- [ ] (위 v2 연결 후) `Scenes_Main/Chapter_1_Boss` 에서 메뉴 **`UnMachine/Boss Setup/그로기 바 UI 만들기`** → `Boss HP` 체력바를 복제한 주황 바가 바로 아래에 생김 → 위치·높이(`Groggy Bar`)·색(`Boss Groggy.mat` `_ColorFill`) 조정 → Ctrl+S
- [ ] 이미 BossGroggyBar 가 있으면 만들지 않고 선택만 함

### T54 ✂️ P1 점프 내려찍기 에셋
- [ ] `JumpSlamConfig` 에셋 값: 실제 보스 룸의 `Room Min X`·`Room Max X`(벽 x), `Ground Y`(바닥), `Ceiling Y`(천장) 로 맞추기
- [ ] (선택) 보스 Animator 에 Trigger `JumpSlamLaunch`(도약 jumping 3~5)·`JumpSlamLand`(착지 lending) + 상태·전이 → 없으면 애니 없이 이동만
- [ ] (선택) 그림자 프리팹: SpriteRenderer + `BossShadowMarker`, **피벗 아래 끝** → Config `Shadow Prefab`
- [ ] (선택) 충격파 프리팹: SpriteRenderer + `BossShockwave`, 피벗 가운데, 오른쪽을 보는 그림 → Config `Shockwave Prefab` (크기는 프리팹 그대로, 판정은 Config 폭·높이)
- [ ] `Player Layer` 가 플레이어 레이어인지 확인 (비우면 "player")

### T59 콤보 신호
- [ ] 보스 자식에 증기 ParticleSystem (Play On Awake 끔) → `ComboSignalView` `Steam`
- [ ] 경고음 클립 → `Warning Clip` (소리가 BGM 에 묻히면 별도 AudioSource 를 만들어 `Audio Source` 에)

### T24 ✂️ 최종 플레이 (실제 보스 씬)
- [ ] 1P: P1~P5 가 섞여 나오고, 예고 색이 패턴마다 맞음 (P1 하늘·P2/P3 빨강 깜빡·P4/P5 노랑)
- [ ] 그로기: 때리거나 돌진 벽 충돌로 바가 차고 → 100 에서 Groggy 4초 (피해 ×1.5) → 0
- [ ] HP 50% → 전환(무적·탄 회수·붉은 틴트) → 2P: 콤보마다 증기·경고음 → 색, P6 드론
- [ ] 사망 → 씬에 충격파·포탄·파편·에너지탄·드론·경고 마커 0개, Console 에러 0
- [ ] 문제가 있으면 Console 로그와 함께 알려주면 수정 (또는 "로그 체크")

---

## T23 회수 누락 보강 (파편·에너지탄) — 🎮 현민 확인 대기
- 근거: boss.md 8장 전환 1 "진행 중 패턴·발사체 정리", BACKLOG T23 원안 / 기준 커밋 `70953f2` (착수 시 기록, T55~T34 미커밋 상태)

### 원인
- 곡사 파편: `HowitzerCannonball` 이 풀 대신 매번 `Instantiate`, 아무도 추적 안 함 → 전환·사망 때 안 사라짐. AirBurst 사거리 코루틴은 포탄 위에서 돌아 포탄이 풀로 돌아가는 순간 멈춤 (파편이 수명 5초까지 날아감).
- 드론 에너지탄: `ConductorDrone` 이 매번 `Instantiate`, 추적 없음 → 전환·사망 후에도 남음.

### 변경 파일
| 파일 | 내용 |
|---|---|
| `HowitzerCannonball.cs` | 파편을 `SpawnPellet` → 보스 파편 풀에서 꺼냄 (보스 없으면 예전처럼 Instantiate). AirBurst 사거리 = 파편 `SetLifetime` |
| `BossProjectile.cs` | `SetLifetime(초)` 추가 |
| `BossController.cs` | 파편 Get 에 대체 프리팹 인자(보스 프리팹 없을 때 포탄 것), 활성 파편 목록 추적·반환 시 제거. `SpawnEnergyBullet` (꺼진 것 재사용·추적). `ClearAllActiveProjectiles` 에서 파편 반환 + 에너지탄 끄기 |
| `ConductorDrone.cs` | 에너지탄 생성을 `SpawnEnergyBullet` 로 |

- 사망(`DeadState`)·v2 전환(`EnterFlowPhaseTransition`)·레거시 전환 모두 `ClearAllActiveProjectiles` 를 거치므로 한 곳 수정으로 모두 적용.
- 리스크: 보스(`BossController`)와 `BossAttackPatterns` 의 파편 프리팹이 서로 다르면 이제 **보스 쪽 프리팹**이 쓰임 (지금까지는 BossAttackPatterns 쪽). 같은 프리팹인지 확인.

### 확인할 것
1. 곡사(P4)에서 AirBurst·BounceBlast 파편이 날아가는 중에 F1 → 파편 전부 즉시 사라짐, 에러 없음. AirBurst 파편이 사거리만큼만 날고 사라짐.
2. 2P 드론 에너지탄 비행 중 보스 사망(HP 0) → 에너지탄 사라짐. 에너지탄이 계속 정상 발사(재사용)되고 Hierarchy 에 꺼진 에너지탄이 끝없이 늘지 않음.
3. 곡사 여러 번 후 Hierarchy 의 파편 개수가 계속 늘지 않음 (풀 재사용).

### 커밋 제안
- `[fix] T23 회수 누락: 곡사 파편 풀 사용·추적, 드론 에너지탄 추적 → 전환·사망 시 회수` —
  `HowitzerCannonball.cs`, `BossProjectile.cs`, `BossController.cs`, `ConductorDrone.cs`, `8_MD/HANDOFF.md`, `8_MD/BACKLOG.md`.
  BossController 는 T57 과 같은 파일 → 함께 또는 뒤에.

---

## T34 ✂️ 2P 붉은 틴트 유지 — 🎮 현민 확인 대기
- 근거: boss.md 8장 전환 3·9장 "눈은 예고 전용" / 기준 커밋 `70953f2` (착수 시 기록, T55~T58 미커밋 상태)

### 변경 파일
| 파일 | 내용 |
|---|---|
| `Gimmick/BossTelegraphView.cs` | 페이즈 틴트 추가 (새 파일 대신 — 스프라이트 색을 바꾸는 곳을 하나로 유지). PhaseTransition 진입 시 On, `BossController.PhaseChanged` 로 2P 이상 On / 1P Off. 예고 중엔 예고 색만, 그 외엔 붉은 틴트(1, 0.55, 0.55) 〔임시값〕 |

- BACKLOG 원안 파일(`BossPhaseTransitionView.cs`)은 만들지 않음: 두 컴포넌트가 같은 스프라이트 색을 덮어쓰게 되기 때문.
- 역재생·증기·흔들림은 축소안대로 없음, 기존 `SpawnPhaseTransitionEffect` 화면 이펙트는 그대로.
- 2P 예고 깜빡임의 "꺼진" 순간은 붉은색 (원래 색 아님).

### 확인할 것
1. 1P: 평소 원래 색, 예고 때만 예고 색 (예전과 같음).
2. F1 → 전환 시작 순간 붉어지고 Console `2P 붉은 틴트 On`, 전환이 끝난 뒤 2P 내내 유지.
3. 2P 예고: 예고 색(빨강·노랑·하늘)이 붉은색과 섞이지 않고 보이고, 예고가 끝나면 다시 붉은 틴트. 사망까지 유지.

### 커밋 제안
- `[feat] T34 ✂️ 2P 붉은 틴트 유지 (BossTelegraphView 페이즈 틴트)` —
  `Gimmick/BossTelegraphView.cs`, `8_MD/HANDOFF.md`, `8_MD/BACKLOG.md`. TelegraphView 는 T58 과 같은 파일 → 함께 또는 뒤에.

---

## T58 콤보 시작 신호 (증기 + 경고음) — 🎮 현민 확인 대기
- 근거: boss.md 9장 표 "증기 분출 + 경고음 = 2페이즈 콤보 시작 (첫 패턴의 눈 색 예고보다 먼저)" / 기준 커밋 `70953f2` (착수 시 기록, T55~T57 미커밋 상태)

### 변경 파일
| 파일 | 내용 |
|---|---|
| `Gimmick/ComboSignalView.cs` (새) | 콤보 Telegraph 진입 순간 증기 파티클 Play + 경고음 PlayOneShot 1회. 단독 패턴은 무시. 비어 있으면 로그만 |
| `Gimmick/BossFlowConfig.cs` | `콤보 신호 선행 시간` 0.3초 〔임시값〕 |
| `Gimmick/BossFlowDriver.cs` | 콤보 예고 = 콤보 예고 값 + 0.3초. `IsCurrentCombo`·`CurrentSignalLead` 공개 |
| `Gimmick/BossTelegraphView.cs` | 콤보면 예고 앞 0.3초는 색 없음 → 그 뒤 첫 패턴 색 (깜빡임도 남은 구간 기준) |
| `Editor/BossTestSceneBuilder.cs` | `ComboSignalView` 자동 추가·Driver 연결 |

- 콤보 예고가 0.3초 길어짐 (색 구간은 그대로). 기존 씬 값 수정 불필요 (코드에서 더함).
- 경고음은 보스 오브젝트의 AudioSource(BGM 과 같은 소스)에서 PlayOneShot — 소리가 묻히면 T59 에서 별도 AudioSource 지정.

### 에디터 작업 (현민) — T59 와 묶어서
1. 보스 자식에 증기 파티클(Play On Awake 끔) 배치 → `ComboSignalView` 의 `Steam` 에 지정, `Warning Clip` 에 경고음 지정. 기존 씬이면 보스에 `ComboSignalView` 추가하고 `Driver` 지정 (테스트 씬은 재생성 시 자동).

### 확인할 것
1. `Force Unit` = C1~C4: 예고 시작 순간 Console `[ComboSignal] C… 시작 신호` + 증기·경고음 → 0.3초 동안 보스 원래 색 → 그다음 눈 색(C1 빨강 깜빡, C2·C3 하늘색, C4 노랑).
2. `Force Unit` = P1~P6: 신호 로그·증기·소리 없음, 예고 시작부터 바로 색 (예전과 같음).
3. 2P 자연 선택에서 콤보마다 신호 1회씩 (중복 재생 없음).

### 커밋 제안
- `[feat] T58 콤보 시작 신호: 증기·경고음을 눈 색 예고보다 먼저 (ComboSignalView)` —
  `Gimmick/ComboSignalView.cs`(+ `.meta`), `Gimmick/BossFlowConfig.cs`, `Gimmick/BossFlowDriver.cs`, `Gimmick/BossTelegraphView.cs`,
  `Editor/BossTestSceneBuilder.cs`, `8_MD/HANDOFF.md`, `8_MD/BACKLOG.md`. Driver 는 T55~T57 과 같은 파일 → 함께 또는 뒤에.

---

## T57 C1·C4 (돌진 계열 콤보) — 🎮 현민 확인 대기
- 근거: boss.md 8장 C1·C4·C1 예외 / 기준 커밋 `70953f2` (T55·T56 미커밋 상태에서 이어서 착수)

### 변경 파일
| 파일 | 내용 |
|---|---|
| `BossAttackPatterns.cs` (레거시) | `ExecutePattern(..., chargeBrakeAfterPass, howitzerDetachFall)` 옵션 2개 (기본 false → 기존 BT·단독 패턴 동작 그대로). `Charge()`: 지나친 시점 기록, Brake 옵션이면 `chargeBrakeDelay`(0.3, 인스펙터) 뒤 정지·스턴 없음, 결과 `LastChargeHitPlayer`·`LastChargeHitWallBeforePass`. `Howitzer()`: 경고·낙하를 `HowitzerFallSequence` 로 분리 — 분리 옵션이면 발사 직후 자세 종료 트리거 → 낙하는 BossController 에서 계속, 패턴은 바로 끝 |
| `BossController.cs` | `StartDetachedHowitzerFall(IEnumerator)`, `ClearAllActiveProjectiles` 첫 줄에서 멈춤 (전환·사망) |
| `Gimmick/IComboStepOutcomeSource.cs` (새) | 단계 결과 보고 인터페이스 |
| `Gimmick/LegacyPatternBridge.cs` | 옵션 전달 + `WithComboOptions`, 돌진 결과 → `HitPlayer`(우선)/`HitWall`/`Completed` |
| `Gimmick/Pattern/ComboPattern.cs` | 단계 목록을 직접 받음, 단계 결과로 C1 분기. 명중·벽 → P2 틈(0.6)으로 끝 |
| `Gimmick/BossFlowDriver.cs` | C1 첫 단계 = 돌진+Brake, C4 첫 단계 = 곡사 발사만 |

- C1 "벽 → Stunned": 축소안은 v2 Stunned 를 쓰지 않으므로 **레거시 돌진 자체 스턴(1초) + P2 틈 0.6** 으로 끝 (단독 P2 벽 충돌과 같음).
- 리스크: C4 에서 곡사 낙하가 돌진과 동시에 진행 → 그로기 등으로 돌진이 끊겨도 낙하는 계속(의도). 곡사 자세 종료 트리거 직후 돌진이 Animator 트리거를 리셋하므로, 곡사 자세가 남으면 알려줄 것. Brake 전용 애니는 없음(그냥 정지).

### 확인할 것
1. `Force Unit` = C1: 돌진이 플레이어를 지나쳐 0.3초 뒤 멈추고(벽까지 안 감, 스턴 없음) 바로 짓밟기 → 틈 0.9. 돌진에 맞으면 짓밟기 없이 `끝 (ChargeHitPlayer)` → 틈 0.6. 벽에 바짝 붙어서(약 2 Unit 이내) 점프로 피하면 보스가 플레이어 x 를 지나기 전에 벽 → 스턴 후 `끝 (WallStun)`.
2. `Force Unit` = C4: 포탄 3발 발사 직후 경고 마커가 뜨는 동안 이미 돌진, 포탄은 계속 낙하·폭발. 경고 마커가 남지 않음.
3. C4 낙하 중 F1(전환) → 포탄·마커 회수, 에러 없음. 단독 P2·P4 는 예전과 같음.

### 커밋 제안
- `[feat] T57 C1 돌진 Brake·예외 + C4 발사 직후 돌진 (레거시 Charge·Howitzer 옵션)` —
  `BossAttackPatterns.cs`, `BossController.cs`, `Gimmick/IComboStepOutcomeSource.cs`(+ `.meta`), `Gimmick/LegacyPatternBridge.cs`,
  `Gimmick/Pattern/ComboPattern.cs`, `Gimmick/BossFlowDriver.cs`, `8_MD/HANDOFF.md`, `8_MD/BACKLOG.md`.
  T55·T56 과 같은 파일(ComboPattern·Driver)이라 그 커밋과 함께 또는 뒤에.

---

## T55 콤보 계획·실행기 + T56 콤보 실행 — 🎮 현민 확인 대기
- 근거: boss.md 8장 콤보 표·C1 예외, 17장 #9·#10 / 기준 커밋 `70953f2` (T55 착수 시 기록)

### 변경 파일
| 파일 | 내용 |
|---|---|
| `Core/ComboPlan.cs`, `ComboRunner.cs`, `ComboStepOutcome.cs`, `ComboEndReason.cs` (새) | 구성 C1 P2→P3 / C2 P1→P5 / C3 P1×3 / C4 P4→P2. 단계 진행, C1 예외(첫 돌진 명중 → 종료·P2 틈 / 벽 → Stunned 요청) |
| `Gimmick/Pattern/ComboPattern.cs` (새) | 콤보 = 패턴 하나. 예고 1회 → 단계 패턴을 틈 없이 연속 실행 → 마지막에만 콤보 틈 |
| `Gimmick/BossFlowDriver.cs` | C1~C4 를 ComboPattern 으로 등록 (단계 누락 시 그 콤보만 제외 + 경고). "콤보 미구현" 경고 삭제. 콤보 틈 기본값: C1 0.9 · C2 0.6 · C3 1.8 · C4 0.6 |

- 상태 머신은 그대로 (콤보 동안 Attacking 이 길어짐). 단계 사이 Telegraph·Punishable·Resting 없음.
- (C1 Brake·예외, C4 발사 직후 돌진은 위 T57 에서 처리)
- 리스크: 기존 씬 Driver 의 콤보 틈 값은 예전 값(C2 1.2, C4 0)이 남아 있음 → 씬 재생성 또는 인스펙터에서 위 값으로.

### 에디터 작업 (현민)
1. 테스트 씬 다시 생성 (또는 Driver `Unit Patterns` 의 C1~C4 반격 틈을 0.9 / 0.6 / 1.8 / 0.6 으로).

### 확인할 것
1. `Force Unit` = C3: 예고 1회 → 점프 3번이 사이 틈 없이 이어지고, 마지막 착지 후에만 Punishable 1.8초. Console `[Combo C3] 단계 1/3 … 2/3 … 3/3 … 끝 (Completed)`.
2. `Force Unit` = C2: 착지하자마자 기관총 3연발(틈 없음) → Punishable 0.6. C1 = 돌진 → 바로 짓밟기, C4 = 곡사 → 돌진.
3. F1 → 2P 자연 선택에서 콤보가 섞여 나오고, 콤보 도중 F1·사망·그로기 시 다음 단계로 안 넘어감 (로그 끊김).

### 질문
- C1 예외 2가지·C3 긴 틈 1.8초는 〔임시값〕 그대로.

### 커밋 제안
- `[feat] T55·T56 콤보: ComboPlan·ComboRunner + ComboPattern 연속 실행 (C1~C4)` —
  `Core/ComboPlan.cs`, `Core/ComboRunner.cs`, `Core/ComboStepOutcome.cs`, `Core/ComboEndReason.cs`,
  `Gimmick/Pattern/ComboPattern.cs`, `Gimmick/BossFlowDriver.cs`, `8_MD/HANDOFF.md`, `8_MD/BACKLOG.md` (+ 새 `.cs` 의 `.meta`)

---

## T46 ✂️ P1 점프 내려찍기 (T45 통합) — 🎮 현민 확인 대기
- 근거: boss.md 6장 P1·4장·17장 #11, BACKLOG 축소 계획 6번 / 기준 커밋 `35b2fb2` (착수 시 기록)

### 변경 파일
| 파일 | 내용 |
|---|---|
| `Core/JumpSlamPhase.cs`, `Core/JumpSlamModel.cs` (새) | 도약 0.3 → 추적 1.2(그림자 x = 플레이어 x, 방 클램프) → 고정 0.4 → 낙하 0.2 → 착지. 남은 시간 이월 |
| `Core/ShockwaveModel.cs` (새) | 한 방향 이동, 앞 끝이 벽에 닿으면 소멸 |
| `Gimmick/Pattern/JumpSlamConfig.cs` (새) | 메뉴 `UnMachine/Config/Jump Slam (P1)`. 예고 0.5·틈 1.2·단계 시간·방 x(-20~20)·바닥 y 0·천장 30 +5·피해 8/4·속도 12·높이 1·폭 1·프리팹·Animator 트리거 이름 |
| `Gimmick/Pattern/JumpSlamPattern.cs` (새) | `IBossPattern`. 화면 밖(추적·고정) = 몸체 콜라이더 Off + `SetUntargetable`, 낙하 시작에 On. 착지: 몸체 bounds 범위 피해 8 + 좌우 충격파. 취소 시 바닥 복귀 |
| `Gimmick/Pattern/BossShockwave.cs`, `BossShadowMarker.cs`, `BossHitBox.cs` (새) | 충격파(ObjectPool, 1회 피해, Gizmo) / 그림자(추적 검정 → 고정 빨강) / 플레이어 박스 판정 |
| `Gimmick/BossFlowDriver.cs` | `_jumpSlam` 지정 시 P1 = JumpSlamPattern (목록 P1 무시). 전환·사망 때 충격파 회수. 인스펙터 `Debug Jump Slam Phase` |
| `BossController.cs` | `SetUntargetable(bool)` + TakeDamage 거부 1줄 (`IsInvulnerable`·자물쇠 변화 없음) |
| `Editor/BossTestSceneBuilder.cs` | `JumpSlamConfig` 자동 생성·Driver 연결 |

- 프리팹이 없으면 **임시 사각형**(그림자 검정 띠, 충격파 주황 1×1)으로 동작 → T54 전에도 테스트 가능.
- Animator 트리거(`JumpSlamLaunch`·`JumpSlamLand`)는 컨트롤러에 없으면 건너뜀 (로그 1줄). 웅크림 = Telegraph 0.5초(T39 하늘색 틴트), 별도 애니 없음.
- C2·C3 는 아직 임시 레거시(Stomp) — T56 에서 교체.

### 에디터 작업 (현민)
1. 테스트 씬 다시 생성(`UnMachine/Test Scene/Boss Arena`) → `JumpSlamConfig.asset` 자동 생성·연결. 기존 씬이면 Driver `Jump Slam` 칸에 `Create > UnMachine > Config > Jump Slam (P1)` 에셋 지정.
2. 실제 보스 룸에서 쓰면 Config 의 방 x·바닥 y·천장 y 를 그 룸에 맞춘다.

### 확인할 것
1. Driver `Force Unit` = P1: 하늘색 예고 → 보스가 위로 사라짐 → 바닥 그림자가 플레이어를 1.2초 따라오다 빨갛게 멈춤(0.4초) → 그 자리 착지. 벽 쪽으로 가도 그림자가 방 밖으로 안 나감.
2. 그림자 밖이면 착지 피해 0, 위면 8. 좌우 주황 충격파: 서 있으면 4, 점프로 넘으면 0. 벽에서 사라짐.
3. 화면 밖 동안 공격해도 피해·그로기 0, 체력바 자물쇠 안 뜸. 착지 후 Punishable 1.2초(라벨) 동안 ×1.2. F1(전환) 을 공중에서 누르면 보스가 바닥으로 내려오고 충격파 사라짐.

### 질문
- 도약 0.3초·낙하 0.2초·충격파 폭 1 은 〔임시값〕(기획서 없음). 웅크림 애니를 Telegraph 에 붙일지는 T54 때 결정.

### 커밋 제안
- `[feat] T46 P1 점프 내려찍기: JumpSlamModel·Pattern·충격파·그림자 + 화면 밖 피해 거부` —
  `Core/JumpSlamPhase.cs`, `Core/JumpSlamModel.cs`, `Core/ShockwaveModel.cs`, `Gimmick/Pattern/` 폴더 전체(5개 .cs),
  `Gimmick/BossFlowDriver.cs`, `BossController.cs`, `Editor/BossTestSceneBuilder.cs`, `8_MD/HANDOFF.md`, `BACKLOG.md`
  (+ 각 `.meta`, `Pattern` 폴더 `.meta`, 새로 생기는 `JumpSlamConfig.asset`). 아래 T38~T63 묶음과 같은 파일(Driver·Controller·Builder)을 건드리므로 그 묶음 먼저 또는 함께 커밋.

---

## 기본 정보 (이전 묶음)
- 작업: T38 ✂️ 선택기 연결 + T39 ✂️ 색 예고 + T60·T61·T63 ✂️ 그로기 (전부 미커밋, 한 묶음)
- 근거: boss.md 11장·9장·7장·15장, BACKLOG "★ 1주 축소 계획" 1~5번
- 기준 커밋: `35b2fb2` (T38 착수 시 기록, 이후 작업도 같은 기준)
- 상태: 🎮 현민 확인 대기 (T29~T33·T37 도 이 씬에서 함께 확인)

## 변경 파일
| 파일 | 내용 |
|---|---|
| `Assets/LHM/0_Script/Boss/New/Gimmick/PatternRegistry.cs` (새) | 단위 → `IBossPattern` 등록·조회 |
| `Assets/LHM/0_Script/Boss/New/Gimmick/BossFlowDriver.cs` | 고정 순환(`_legacyCycle`) 삭제 → 선택기. `_patternTable`(필수)·단위→레거시 목록 `_unitPatterns`·드론 상한 1·고정 시드·강제 선택 단위·디버그(구간·체류·후보·최근 2개, 머리 위 라벨 4번째 줄). 콤보 = 첫 패턴만 + 경고 로그. 선택 실패 시 P5 로 대체 |
| `Assets/LHM/0_Script/Boss/New/Gimmick/LegacyPatternBridge.cs` | 레거시 자체 반복 방지 끔 (`useRepeatGuard: false`) |
| `Assets/LHM/0_Script/Boss/New/BossAttackPatterns.cs` | `ExecutePattern(..., bool useRepeatGuard = true)` 매개변수 1개 (기존 BT 호출은 그대로) |
| `Assets/LHM/0_Script/Boss/New/Editor/BossTestSceneBuilder.cs` | Driver 에 `PatternTableConfig` 연결 (없으면 `Assets/LHM/3_ScriptableObject/PatternTableConfig.asset` 새로 생성), `BossTelegraphView` 추가·연결 |
| (T39) `Core/TelegraphSignal.cs`, `Core/TelegraphColor.cs` (새) | 단위 → 색(빨강 깜빡 2회 / 노랑 / 하양) + `IsLit(경과, 길이)`. 콤보 = 첫 패턴 색 (C1 빨강, C2·C3 하양, C4 노랑) |
| (T39) `Gimmick/BossTelegraphView.cs` (새) | Telegraph 동안 보스 SpriteRenderer 전체 곱셈 틴트, 끝나면 원래 색. World 채널 따름 |
| (T39) `Gimmick/BossFlowDriver.cs` | `FlowStateChanged` 이벤트, `CurrentUnit`, `StateRemaining` 공개 |

| (T60) `Core/GroggyGauge.cs`, `Gimmick/GroggyConfig.cs` (새) | 최대 100, 마지막 증가 3초 뒤 초당 5 감소, 가득 차면 유지. Config: 벽 +25, **피해 1당 +0.5 〔임시값〕** (메뉴 `UnMachine/Config/Groggy`) |
| (T61) `Gimmick/BossGroggyDriver.cs` (새) | 피해·벽 충돌 → 게이지, 가득 → `EnterGroggy`(4초 ×1.5), Groggy 끝나면 0. Groggy·전환·사망 중엔 안 오름. 에디터 F5 = +25, 인스펙터 값·마지막 출처 |
| (T61) `BossController.cs` | `Damaged(float 배율 전 피해)` 이벤트 +3줄 |
| (T61) `BossAttackPatterns.cs` | `ChargeHitWall` 이벤트 (레거시 돌진 벽 충돌 지점 1줄) |
| (T61) `Gimmick/BossFlowDriver.cs` | `State`·`IsRunning`·`EnterGroggy()` 공개 |
| (T63) `Gimmick/BossGroggyBar.cs` (새) | Filled 이미지 fillAmount = 게이지, 전투 시작 때 표시·사망 때 숨김 |

- (T61) 벽 충돌 시 v2 Stunned(×1.2)는 들어가지 않음 — 레거시 돌진 자체 스턴을 그대로 씀 (축소안). 벽 충돌로 게이지가 가득 차면 Groggy 가 레거시 스턴을 끊고 들어감.
- (T39) "하양"은 곱셈 틴트로 안 보여서 **옅은 하늘색(0.6, 0.85, 1)** 기본값. View 인스펙터에서 3색 조정 가능.
- (T39) 보스 자식의 이펙트 스프라이트도 함께 물듦 (Awake 시점에 있던 것만). 거슬리면 View `Renderers` 에 본체만 지정.

- 리스크: **기존 씬의 Driver 는 `_patternTable` 이 비어 있어 비활성 → 보스가 기존 흐름(BT)으로 동작**하고 Console 에 Error 1줄. 아래 에디터 작업 1번으로 해결.
- 기존 씬 Driver 의 레거시 순환 값(`_legacyCycle`)은 사라지고 새 목록 기본값이 들어간다.

## 에디터 작업 (현민)
1. 컴파일 확인 → `UnMachine/Test Scene/Boss Arena` 로 Test_Boss 씬 **다시 생성** (PatternTableConfig 자동 연결).
   - 씬을 다시 만들기 싫으면: 보스의 `BossFlowDriver` → `Pattern Table` 칸에 `PatternTableConfig` 에셋 지정 (없으면 `Create > UnMachine > Config > Pattern Table`).

2. (T63) 그로기 바 UI: 보스 체력바 Canvas 아래에 빈 오브젝트 `GroggyBar`(+ `BossGroggyBar`) → 자식 `Root`(배경 Image, 얇게) → 그 자식 `Fill`(Image, Type = Filled / Horizontal / Origin Left). `BossGroggyBar` 에 Groggy(보스의 `BossGroggyDriver`)·Boss·Root·Fill 지정. **Root 는 GroggyBar 자신이 아닌 자식**으로.
   - 테스트 씬 툴은 `BossGroggyDriver`·`GroggyConfig` 까지만 자동 연결 (UI 는 수동).

## 확인할 것
1. 가까이 서 있으면 라벨 `Near` + Stomp(P3) 위주, 3초 넘게 서 있으면 후보 줄에 `P1:6 [체류×]`. 같은 단위 3연속 없음.
2. F1 → 2P: 후보에 C1~C4·P6 등장, 콤보 선택 시 경고 로그 `콤보 C… 미구현`. 드론이 살아 있으면 `[대체 P6]` (P6 → P5).
3. Driver `Force Unit` 켜고 단위 지정 → 그 패턴만 반복. `Use Fixed Seed` 켜고 같은 위치에서 재시작 → 같은 순서.
4. (T39) 라벨이 Telegraph 일 때만 색: P2·P3 빨강 2번 깜빡, P4·P5·P6 노랑, P1(임시 Stomp) 하늘색. Attacking 되면 원래 색. Force Unit 으로 단위별 확인.
5. (T61) 보스를 때리면 `BossGroggyDriver` 값 상승(피해 × 0.5), 3초 가만히 → 초당 5 감소. F5 4번 → 100 → 라벨 Groggy 4초·피해 로그 ×1.5 → 값 0. P2 벽 충돌 → +25(마지막 출처 "벽 충돌"). Groggy 중 때려도 값 안 오름. (T63) 바가 값대로 움직임.

## 질문
- (T37 이어짐) 반복 방지로 후보가 없으면 반복 허용 / P6→P5 대체 시 확률 합산 / 페이즈 전환 때 기록 유지 — 기본값대로 진행 중.

## 커밋 제안
- `[feat] T38 선택기 연결: PatternRegistry + 레거시 영구 매핑, 레거시 반복 방지 끔` —
  `Assets/LHM/0_Script/Boss/New/Gimmick/PatternRegistry.cs`(+ `.meta`), `Gimmick/BossFlowDriver.cs`,
  `Gimmick/LegacyPatternBridge.cs`, `BossAttackPatterns.cs`, `Editor/BossTestSceneBuilder.cs`,
  `Assets/LHM/8_MD/HANDOFF.md`, `BACKLOG.md` (+ 새로 생긴 `PatternTableConfig.asset`·`Test_Boss.unity` 와 `.meta`)
- `[feat] T39 예고 색: TelegraphSignal + BossTelegraphView (스프라이트 틴트)` —
  `Core/TelegraphSignal.cs`, `Core/TelegraphColor.cs`, `Gimmick/BossTelegraphView.cs` (+ 각 `.meta`),
  `Gimmick/BossFlowDriver.cs`, `Editor/BossTestSceneBuilder.cs` (T38 과 한 커밋으로 묶어도 됨)
- `[feat] T60·T61·T63 그로기 게이지 (피해 비례·벽 충돌) + Groggy 진입 + 게이지 바` —
  `Core/GroggyGauge.cs`, `Gimmick/GroggyConfig.cs`, `Gimmick/BossGroggyDriver.cs`, `Gimmick/BossGroggyBar.cs` (+ 각 `.meta`),
  `BossController.cs`, `BossAttackPatterns.cs`, `Gimmick/BossFlowDriver.cs`, `Editor/BossTestSceneBuilder.cs`
  (+ 새로 생기는 `GroggyConfig.asset`·`.meta`)
- 문서 변경(AI_RULES·CLAUDE·BACKLOG 축소 계획)이 아직 미커밋이면 같이 또는 먼저 커밋.

## 질문 (그로기)
- 피해 1당 +0.5 면 게이지 100 = 피해 200 (HP 500 의 40%). 너무 빠르거나 느리면 `GroggyConfig` 에서 조정.

## 다음 할 일
- 축소 계획 코드 작업 끝. 남은 것은 현민 에디터 묶음: T36 ✂️ → T67 ✂️ → T54 ✂️ → T59 → T24 ✂️ (최종 플레이). 그 사이 🎮 씬 확인 결과를 알려주면 수정.
