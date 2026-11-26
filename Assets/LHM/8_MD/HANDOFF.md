# docs/HANDOFF.md — 인수인계 문서

> 작업이 끝날 때마다 **덮어쓴다.** 이전 기록은 git 히스토리에 남는다.
> 다음 사람(또는 AI)이 이 문서 하나만 읽고 바로 이어서 작업할 수 있어야 한다.
> 작성은 Claude 또는 현민 (사람)만 한다. Gemini는 `docs/REVIEW.md` 에 쓴다.
> **커밋은 현민이 직접 한다.** Claude는 "커밋 제안"만 적는다.

---

## 기본 정보
- 작업 제목: `BossFlowDriver` + BossController 연결 (`useFlowV2`)
- BACKLOG 작업 ID: T31
- 기획서 근거: boss.md 4장(배율)·5장(흐름)·6장(패턴별 반격 틈), BACKLOG Q19 C, DECISIONS 2026-11-25 ⑥
- 작성자: Claude
- 작성 일시: 2026-11-26
- 모드: 일반
- 브랜치: `main`
- 기준 커밋: `82d87de` (T31 착수 시 기록. 이후 `9a71a8c` 는 `.meta` 3개 + HANDOFF 만 바뀐 현민 커밋 → Gemini diff 는 `82d87de..` 기준)
- 커밋 상태: 미커밋 (코드 변경 있음)
- 상태: 🎮 씬 테스트 대부분 통과 (5-2 요약, #3·#12 남음) → 남은 변경 커밋 후 Gemini 리뷰
- 이전 작업: T29·T30 — 커밋됨(`82d87de`, `.meta` 는 `9a71a8c`). 씬 테스트는 이번 T31 절차로 함께 확인

## 0. 이전 작업 리뷰 범위 (Gemini)
- T29·T30 (`BossFlowMachine` 등 Core 6 + `BossFlowConfig`) 은 기준 커밋 `82d87de` 에 포함 → 이번 diff 에 안 보인다. 따로 볼 때: `git diff ae589ab 82d87de`.
- T27·T28·T22 는 `git diff d60f4f9 ae589ab` (팀원 커밋 `b3471dc`·`c2d158d` 섞임). 요청 사항은 직전 HANDOFF(`82d87de`) 9장 참고.

## 1. 목표
- `useFlowV2` 토글 On → 전투 시작(BattleStarted) 이후 Idle/Attacking/Resting 레거시 코루틴 대신 `BossFlowDriver` 가 `BossFlowMachine` 루프를 소유.
- Attacking = `IBossPattern.RunAsync(boss, ct)`. 이번엔 `LegacyPatternBridge`(기존 `ExecutePattern` 래핑) 고정 순환.
- `TakeDamage` 에 상태별 배율, `BossState` 8~12 추가, 머리 위 상태 라벨·F3 Stunned·F4 Groggy (에디터 전용).
- 토글 Off → 기존 동작 그대로.
- 범위 밖: 걷기(T32), 선택기(T38), PhaseTransition 상태·무적(T33), 연출(T34), Animator 8~12 상태(T36).

## 2. 변경 파일
| 파일 | 계층 | 변경 내용 |
|---|---|---|
| `Assets/LHM/0_Script/Boss/New/Gimmick/IBossPattern.cs` | Runtime (신규) | `DebugName`, `Timing`(PatternTiming), `UniTask<PatternResult> RunAsync(BossController, CancellationToken)` |
| `Assets/LHM/0_Script/Boss/New/Gimmick/LegacyPatternBridge.cs` | Runtime (신규, T68 삭제 예정) | `BossAttackPatterns` 위에서 `ExecutePattern` 코루틴 실행(`// guard-allow: 레거시 래핑`), `UniTask.Yield` 로 완료 대기. 취소 시 `ct.Register` 로 **즉시** `ForceStopAllPatterns`. World dt 기준 타임아웃(기본 10초) |
| `Assets/LHM/0_Script/Boss/New/Gimmick/BossFlowDriver.cs` | Runtime (신규) | 상태 머신 생성·`Tick(GameClock.World.DeltaTime)`, `StateChanged` → `ApplyFlowState`·다음 패턴 `Begin`·패턴 실행/CTS 관리, `Died` 구독 → `EnterDead`. `StartLoop(phase)`, `IsReady`, `DamageMultiplier`. 인스펙터: 레거시 순환 목록·타임아웃·라벨 오프셋·읽기 전용 표시. 에디터 전용 `OnGUI` 라벨, F3·F4 (Input System) |
| `Assets/LHM/0_Script/Boss/New/BossController.cs` | Runtime (+79 −10) | enum `Telegraph=8…PhaseTransition=12`, `_useFlowV2`·`_flowDriver` 필드, Start 에서 `_isFlowV2Active` 확정, `StartBattleLoop()`(전투 시작·TakeDamage 전환·F1 3곳), `ChangeState` 에서 v2 면 Dead 외 코루틴 생략, `ApplyFlowState(BossState)`, `TakeDamage` 피격 가능 상태 확장 + 배율 적용·로그 |
| `Assets/LHM/8_MD/HANDOFF.md`, `BACKLOG.md` | docs | 본 문서, T31 → 🎮 |

## 3. 동작 변화와 리스크
- **토글 Off (기본)**: 변화 없음. `StartBattleLoop()` 가 기존 `ChangeState(Idle)` 를 그대로 호출.
- **토글 On**:
  - 착지 → `Driver.StartLoop(1)` → Idle → Telegraph(0.4) → Attacking(레거시 패턴) → Punishable(패턴 값) → Resting(1.5) → Idle … 반복.
  - 순환 (인스펙터 `_legacyCycle`, boss.md 6장 + T38 임시 매핑 기준):
    | 순서 | 레거시 패턴 | 대응 | 예고 | 반격 틈 |
    |---|---|---|---|---|
    | 1 | Stomp | P3 짓밟기 | 0.4 | 0.9 |
    | 2 | MachinegunBurst | P5 직사 연사 | 0.4 | 0.6 |
    | 3 | Howitzer | P4 곡사 | 0.4 | **0** (Punishable 건너뜀, DECISIONS ⑥) |
    | 4 | Charge | P2 돌진 | 0.4 | 0.6 (명중 기준) |
  - 피해: 기존 Idle·Attacking·Resting + Telegraph·Punishable·Stunned·Groggy 에서 받음. `피해 × 배율` (1.0 / 1.2 / 1.5), 매 피격 `[Boss] 피해 a × m = b (상태)` 로그.
  - 사망: `ChangeState(Dead)` → `Died` → Driver `EnterDead` + 패턴 취소 → 기존 `DeadState` 그대로.
  - 2P 전환 (HP 50% 하회·F1): 기존 정리 블록 그대로 → 마지막에 `Driver.StartLoop(2)` (새 상태 머신, 2P 휴식 0.8·틈 ×0.8). PhaseTransition 상태는 거치지 않음 (T33 에서 교체).
- 설계 선택:
  - 계획 때 적은 반격 틈(Stomp 1.2 / 나머지 0.9)은 boss.md 6장 원문과 T38 매핑으로 다시 맞춤 → 위 표 값.
  - Driver 가 Config 등 참조 누락이면 Awake 에서 `GameLog.Error` + 비활성화 → BossController 는 Start 에서 `IsReady` 확인 후 **기존 흐름으로 동작** (에러 1줄).
  - 2P 전환은 `SetPhase` 대신 **상태 머신을 새로 만들어** Idle 부터 재시작 (Core 에 "Idle 로 초기화" API 가 없음, T33 전까지 임시).
  - 인터럽트 시 레거시 정지는 `ct.Register` 로 동기 실행 — 다음 프레임에 하면 그 사이 시작된 다음 패턴까지 멈출 수 있음.
  - 레거시 코루틴은 `BossAttackPatterns` 위에서 돌림 → 기존 `ForceStopAllPatterns()` 한 번에 함께 정지.
- 리스크:
  - **Animator**: State 8~12 전이가 없어(T36) 해당 정수를 넣어도 이전 애니메이션이 남을 수 있음. 상태 확인은 라벨로.
  - **Resting 중 정지**: v2 Resting 은 걷기 없음(T32). 레거시 패턴이 남긴 속도가 있으면 미끄러질 수 있음 → 현민 확인 필요.
  - **레거시 패턴 시간**: 내부가 `Time`·`WaitForSeconds` 라 F2 히트스톱 중에도 패턴은 진행, FSM 타이머(예고·틈·휴식)만 멈춤.
  - **2P 전환 시 진행 중 인터럽트 소실**: Stunned·Groggy 중 50% 하회하면 새 머신으로 재시작되어 Idle 부터 (T33 에서 `RequestPhaseTransition` 보류 규칙으로 해결).
  - 패턴이 취소가 아닌 예외로 끝나면 Attacking 에 머묾 (예외는 UniTask 미관찰 예외로 Console 에 남음).
  - **GameClockDriver 가 씬에 없으면** World dt = 0 → Telegraph 에서 멈춤. Test_Boss 에 있는지 확인 필요.
  - 피해 로그가 매 피격마다 1줄 (에디터·개발 빌드만, `GameLog`).

## 4. 정적 점검 (Claude — 실행 아님)
| 항목 | 결과 |
|---|---|
| 컴파일 가능성 자체 점검 | 문제 없음으로 판단. UniTask 는 기존 `BossContext.cs` 가 이미 사용(같은 어셈블리, asmdef 없음). `destroyCancellationToken`(Unity 2022.2+), `SuppressCancellationThrow`·`UniTask.Yield(PlayerLoopTiming, ct)`·`ct.Register(Action)` 사용. `Old_Stage2/IBossPattern.cs` 는 `BossSystem` 네임스페이스라 `UnMachine.Boss.IBossPattern` 과 충돌 없음 (BossController 는 `using BossSystem` 이 있어 Driver 를 정규화 이름으로 참조, IBossPattern 은 참조 안 함). `Old_Stage2/BossController.cs` 는 전체 주석. 새 enum 값은 끝에 명시값 → 기존 0~7 불변, LJS 2파일은 `Dead` 비교만. `GameLog.Error(context, msg)`·`Warn(msg, context)` 인자 순서 확인. 새 코드 줄 120자 이하 (PowerShell 확인, 899줄 128자는 기존 코드 `70fb6ce7`) |
| unity-guard | 문제 없음. `async void` 없음(`UniTaskVoid`+`Forget`). 새 코루틴은 Bridge 1곳(`guard-allow`). 대기는 모두 ct 전달. `Time.deltaTime` 없음(World 채널). `Find*`·`Update` 내 `GetComponent` 없음. 이벤트: `Died` OnEnable/OnDisable, `StateChanged` 는 `ReleaseMachine` 에서 해제. 새 `Debug.Log` 없음(`GameLog`). 매 프레임 할당: 에디터 전용 `OnGUI` 라벨 문자열만(`guard-allow`). `Camera.main` 은 에디터 전용 `OnGUI` 에서만 |
| spec-check | ✅ 4장 배율 1.0/1.2/1.5 (Config) / ✅ 5장 흐름·Dead 최우선 / ✅ 6장 반격 틈 P3 0.9·P5 0.6·P2 0.6 / ✅ DECISIONS ⑥ P4 틈 없음 / ✅ 9장 최소 예고 0.4 (Config) / ⚠️ 레거시 예고 0.4 는 기획서 패턴별 예고값 아님 (레거시 자체 예비동작 존재, 최소값만) / ➖ P2 벽 충돌 Stunned(T44), 전환(T33) |

## 5. 에디터 작업 (현민)
**빠른 방법 (테스트 씬 툴, 추가 수정)**
1. Unity Console 컴파일 에러 0 확인. 새 `.meta` 3개 생성 확인 (`IBossPattern`·`LegacyPatternBridge`·`BossFlowDriver`).
2. 메뉴 `UnMachine > Test Scene > Boss Arena (기본값으로 바로 생성)` 클릭.
   - `Test_Boss.unity` 가 이미 있으면 덮어쓰지 않고 `Test_Boss 1.unity` 등 새 이름을 묻는다 → "만들기".
   - 자동으로 하는 일: 보스룸·카메라·GameClockDriver 등 배치, `Boss.prefab`·`Player.prefab` 배치, 보스 인스턴스에 `BossFlowDriver` 추가·Config/Boss/AttackPatterns 연결, BossController `Flow Driver` 연결·`Use Flow V2` On.
   - Config: 프로젝트의 `BossFlowConfig` 를 찾아 쓰고, **없으면 `Assets/LHM/3_ScriptableObject/BossFlowConfig.asset` 를 새로 만든다** (Console `[TestScene] BossFlowConfig 새로 생성`).
   - Console 마지막 줄 `[TestScene] 생성 완료: … v2 흐름 On` 확인.
3. 생성된 씬에서 바로 플레이. 토글 Off 테스트는 보스 BossController 의 `Use Flow V2` 체크만 끄면 됨.
4. 옵션을 바꾸려면 `UnMachine > Test Scene > Boss Arena` 창 → "보스 v2 흐름 (T31)" 섹션 (토글·Config 지정).

**수동 방법 (기존 씬에 붙일 때)**: 보스에 `BossFlowDriver` 추가 → Config·Boss·Attack Patterns 연결 → BossController `Flow Driver` 연결 + `Use Flow V2` 체크 → 씬에 `GameClockDriver` 확인 → 저장. **플레이 모드 밖에서** 할 것.

## 5-1. 씬 테스트 절차 (현민)
**준비**: 위 에디터 작업 완료, Game 뷰 + Console 열기, BossFlowDriver 인스펙터 "읽기 전용 표시" 보이게.

| # | 조작 | 기대 결과 | 근거 |
|---|---|---|---|
| 1 | 토글 On → 플레이 → 착지까지 대기 | Console `[BossFlowDriver] 루프 시작 (페이즈 1)`. 머리 위 라벨 `Idle → Telegraph 0.40s → Attacking → Punishable → Resting 1.50s → …` 반복, 셋째 줄에 `Legacy:Stomp` 등 순서대로 | 5장 흐름 |
| 2 | Attacking 동안 관찰 | 기존 패턴(밟기·기관총·곡사·돌진)이 실제로 실행됨 | Q19 C |
| 3 | 예고 길이 | `Legacy Cycle` 예고를 0.2 로 바꿔도 Telegraph 는 0.40s 부터 카운트 (플레이 전에 바꿀 것 — Awake 에서 읽음) | 9장 최소 0.4 |
| 4 | Howitzer 차례 | Punishable 라벨 없이 Attacking → Resting | 틈 0 건너뜀 |
| 5 | Punishable 중 공격 | Console `[Boss] 피해 x × 1.20 = …(Punishable)` | 4장 |
| 6 | Idle·Telegraph·Attacking·Resting 중 공격 | `× 1.00` | 4장 |
| 7 | F3 | 라벨 Stunned 1.5초 → Resting, 그동안 피격 `× 1.20`. Attacking 중이면 패턴 즉시 멈춤 | 5장, T30 |
| 8 | F4 | Groggy 4초 → Resting, 피격 `× 1.50` | 5장, T30 |
| 9 | Groggy 중 F3 / Stunned 중 F3 | 무시 (Console `EnterStunned → False`) | 우선순위 |
| 10 | HP 50% 하회 (F6 후 1타, 또는 F1) | 기존 전환 정리·이펙트·`PhaseChanged → 2` 후 `루프 시작 (페이즈 2)`, 라벨 `P2`, Resting 0.80s, 틈 ×0.8 (Stomp 0.72) | 8장 |
| 11 | 보스 사망까지 공격 | 라벨 Dead, 루프 즉시 중단, 기존 사망 연출(2초 후 비활성화) | 5장 Dead |
| 12 | **토글 Off** 로 다시 플레이 | 라벨 없음, T2 씬 테스트와 같은 기존 동작 (BT 콤보·Resting 걷기) | Q19 C |

**경계 상황**
- Attacking 중 F3 연타 → Console 에 `InvalidOperationException` 없음, 다음 Begin 후 정상 순환.
- 돌진(Charge) 중 F3 → 보스가 미끄러지지 않고 멈춤, 방향 전환 다시 동작 (`ForceStopAllPatterns` → 속도 0·facing 해제).
- F2 히트스톱 중 라벨 남은 시간이 멈춤 (레거시 패턴 자체는 진행 — 3장 리스크).
- `Flow Driver` 비우고 토글 On → Console 에러 1줄 후 기존 흐름으로 동작.

**Console 에 나오면 안 되는 것**: `InvalidOperationException`(Begin/NotifyAttackEnded), `ArgumentOutOfRangeException`, `NullReferenceException`, `[LegacyPatternBridge] … 타임아웃` (정상 패턴은 10초 안에 끝나야 함).

## 5-2. 현민 씬 테스트 결과
- 2026-11-26 1차 (Editor.log 12:31 기준, Claude 로그 확인):
  - ❌ **v2 루프 미동작** — `[BossFlowDriver] Config·BossController·BossAttackPatterns 참조 누락 → 비활성화` →
    `[Boss] useFlowV2 On 이지만 … 기존 흐름으로 동작`. `BossFlowConfig` 에셋이 프로젝트에 없음 (`.asset` 검색 0건) → `_config` 누락으로 판단.
    씬 파일에도 Driver 가 아직 저장돼 있지 않음.
  - 근거: `루프 시작` 로그 없음, `[Boss] 피해 × …` 로그 없음, BT 전용 동작(연속 대체, MachinegunRapid·DroneSpawn) 출력.
  - ✅ (부수 확인) Driver 준비 실패 시 폴백 = 기존 흐름: 등장 → 패턴 순환 → F6 경계 → 1타 전환 `PhaseChanged → 2` → 2P 패턴 → `Died` 까지 예외 없음.
  - 재확인 필요: 5장 2번(Config 에셋 생성)·4번 연결 후 5-1 #1~#12 전체.
- 2026-11-26 2차 (테스트 씬 툴로 만든 씬, Editor.log 12:44 기준, Claude 로그 확인). 상태 전이 로그는 없어서
  `Animator 'State' 파라미터 없음` 경고(Telegraph·Punishable·Resting·Stunned·Groggy 진입마다 1회)로 간접 추적:
  | # | 결과 | 근거 (로그) |
  |---|---|---|
  | 1 | ✅ | `[BossFlowDriver] 루프 시작 (페이즈 1)`, 순서 Stomp → MachinegunBurst → Howitzer → Charge |
  | 2 | ✅ | 각 패턴 `패턴 실행 시작/완료` |
  | 3 | ➖ | 미실시 (예고 0.2 변경) |
  | 4 | ✅ | Howitzer 뒤 경고 2개(Resting·Telegraph) — Punishable 건너뜀. 다른 패턴 뒤는 3개 |
  | 5 | ➖ | Punishable 중 피격 로그 없음 → **미확인** |
  | 6 | ✅ | `× 1.00` (Attacking·Resting·Telegraph) |
  | 7 | ✅ | Charge 중 F3 → `EnterStunned → True`, 같은 프레임 `ForceStopAllPatterns`, 이후 Resting → Telegraph |
  | 8 | ✅ | F4 → `EnterGroggy → True`, `× 1.50 (Groggy)` 6회 |
  | 9 | ✅ | Stunned 중 F3 → `EnterStunned → False` |
  | 10 | ✅ | F6 후 1타 → `PhaseChanged → 2` → `루프 시작 (페이즈 2)` (휴식 0.8 은 로그로 확인 불가) |
  | 11 | ✅ | Attacking(Howitzer) 중 사망 → `Died` → 사망 정리, 이후 패턴 시작 없음 |
  | 12 | ➖ | 이번 회차 미실시 (1차 폴백 실행이 기존 흐름과 같게 동작) |
  - Console 예외(`InvalidOperationException`·`NullReferenceException` 등) 0건.
  - ❌ **Howitzer 타임아웃** 1회: `[LegacyPatternBridge] Howitzer 타임아웃(10.0초) → 강제 종료` (정상 소요 약 6.8초).
    추정 원인: 타임아웃을 World 채널(`unscaledDeltaTime`, 상한 없음)로 쟀고 레거시는 `WaitForSeconds`(`Time.deltaTime`, 상한 있음) →
    에디터 프레임 정지가 타임아웃 쪽에만 쌓임. **수정**: Bridge 타임아웃을 `Time.deltaTime` 으로 (`// guard-allow: 레거시 패턴과 같은 시계`).
    → 재확인: Howitzer 가 `모든 포탄 낙하 완료` → `패턴 실행 완료` 로 끝나는지.
  - ❌ **테스트 씬 툴이 기존 `Test_Boss.unity` 를 덮어씀** (AI_RULES 0장 2번 위반). 로그: 열린 Test_Boss 저장(17800) 직후 같은 경로·같은 GUID 로 새 씬 저장.
    추정 원인: `File.Exists` 상대 경로가 프로세스 현재 폴더 기준. **수정**: `AssetExists`(AssetDatabase + 프로젝트 기준 절대 경로)로 확인.
    → 현민: 원래 씬이 필요하면 `git checkout -- Assets/LHM/Test_Boss.unity` 로 되돌리기 (커밋본 = `82d87de`, 손으로 붙인 Driver 는 미커밋이라 없음).
    → 재확인: 툴을 다시 실행하면 `Test_Boss 1.unity` 로 만들겠다는 대화상자가 나오는지.
  - 참고 (기존 문제, T31 무관): `Parameter 'State'/'AttackStart'/'stopHowitzer' does not exist` 경고는 레거시 실행에서도 같이 나옴 (Animator 에 파라미터 없음, T36 에서 정리).
- 2026-11-26 3차 (수정 컴파일 후, 씬은 재생성 안 함 — `Test_Boss.unity` 12:43 그대로, Editor.log 12:49 기준):
  - ✅ #5 Punishable 피격 `× 1.20 = 6.0` 4회. ✅ #6 `× 1.00` (Telegraph·Attacking·Resting).
  - ✅ #7·#8 Howitzer(Attacking) 중 F4 → Groggy, 패턴 즉시 정지. Resting 중 F4 → Groggy, Resting 중 F3 → Stunned. ✅ #9 Groggy 중 F3 → False.
  - ✅ #10 F6 후 1타 → `PhaseChanged → 2` → `루프 시작 (페이즈 2)`. 예외 0건, Bridge 타임아웃 0건.
  - ➖ Howitzer 끝까지 실행된 회차 없음 (1회는 F4 로 중단, 1회는 플레이 종료 시점) → **타임아웃 수정 재확인 남음**.
  - ➖ 테스트 씬 툴 재실행 안 함 → **덮어쓰기 방지 재확인 남음**.
  - 관찰: Howitzer 를 인터럽트하면 떠 있던 포탄은 회수되지 않고 포탄 자체 Waiting 타임아웃으로 풀에 돌아감 (`[HowitzerCannonball] Waiting 타임아웃` 3회).
    `ForceStopAllPatterns` 는 발사체를 회수하지 않음 (레거시 동작 그대로). Stunned·Groggy 때 발사체를 지울지는 기획 판단 → 10장 질문.
- 2026-11-26 4차 (Editor.log 12:52 기준, 씬 재생성 안 함):
  - ✅ Telegraph(Stomp) 중 F4 → Groggy, Resting 중 F3 → Stunned, Howitzer 중 F4 → Groggy(패턴 정지), Charge 중 F3 → Stunned(패턴 정지). 예외 0건, Bridge 타임아웃 0건.
  - 관찰: 인터럽트된 패턴은 다시 하지 않고 순환의 다음 패턴으로 넘어감 (Stomp 예고 중 Groggy → 다음은 MachinegunBurst). `Begin` 때 순번이 이미 넘어가는 설계 그대로 — T38 선택기에서 다시 정함.
  - ➖ Howitzer 이번에도 F4 로 중단 → 타임아웃 수정 재확인 여전히 남음. 테스트 씬 툴 재실행도 남음.
- 2026-11-26 5차 (Editor.log 12:58 기준):
  - ✅ **덮어쓰기 방지**: 툴 실행 → `Test_Boss 1.unity` 새로 생성 (새 GUID), `Test_Boss.unity` 는 12:43 그대로.
  - ✅ **Howitzer 타임아웃 수정**: Howitzer 2회 모두 `포탄 낙하 시작` → `모든 포탄 낙하 완료` → `패턴 실행 완료`, Bridge 타임아웃 0건.
  - ✅ 순환 2바퀴 (Stomp → MachinegunBurst → Howitzer → Charge → Stomp …), Punishable `× 1.20`. 예외 0건.
  - 관찰 (기존 툴 코드, T31 무관): `[TestScene] Light2D 타입을 찾지 못해 Global Light 2D 를 만들지 않았다` — 새 씬이 어둡게 보이면 수동 추가.
- **요약**: 5-1 #1·#2·#4~#11 통과. 남은 것 #3(예고 0.2 → 0.4 유지), #12(토글 Off) — 현민 판단으로 생략 가능.

## 6. 기획서와 다르게 한 점
| 기획서 / BACKLOG | 구현 | 이유 |
|---|---|---|
| (명시 없음) 레거시 패턴 예고값 | 전부 0.4 (최소 예고) | 레거시 패턴에 자체 예비동작 있음. 패턴별 예고는 v2 패턴(B-3)에서 |
| T31 "2P 전환" 언급 없음 | 토글 On 에서 기존 전환 정리 후 Driver 루프 2P 재시작 | 기존 블록의 `ChangeState(Idle)` 가 Driver 와 충돌 (현민 승인 기본안 1). T33 에서 교체 |
| `RunAsync(ctx, ct)` | `RunAsync(BossController, ct)` | 컨텍스트 타입은 T37 이후 (현민 승인 기본안 3) |

## 7. 결정 사항
- 레거시 순환·시간값은 Driver 인스펙터 배열 (T68 에서 Bridge 와 함께 제거) — 현민 승인 기본안 2.
- 반격 틈 값은 boss.md 6장 + T38 임시 매핑(P3→Stomp, P5→MachinegunBurst, P4→Howitzer, P2→Charge) 기준.
- Driver 준비 실패 시 기존 흐름으로 동작 (보스가 멈추는 것보다 테스트에 유리).

## 8. 알려진 문제 / 미완료
- 걷기 = T32, PhaseTransition 상태·무적 = T33, 전환 연출 = T34, Animator 8~12 = T36, 선택기 = T38, 벽 충돌 Stunned = T44, 그로기 게이지 = T60·T61.

## 9. 리뷰어에게 요청
- `OnStateChanged` 중첩(Idle 핸들러 안 `Begin` → Telegraph 핸들러, 동기 완료 패턴 → `NotifyAttackEnded`)에서 `ApplyFlowState` 순서와 CTS 정리가 맞는지.
- `CancelPattern` → `ct.Register(StopLegacy)` 동기 콜백 → `ForceStopAllPatterns` → `UnlockFacing` 경로의 재진입 안전성.
- `RunPatternAsync` 의 `isCanceled || ct.IsCancellationRequested` 판정이 늦은 알림(T30 리스크)을 충분히 막는지.
- BossController `ChangeState` v2 가드 위치(애니메이터 설정 뒤, switch 앞)와 `TakeDamage` 배율 적용 위치(전환 판정 전).
- 2P 전환을 새 머신 생성으로 처리한 임시 방식.
- (0장) T29·T30·T27·T28·T22 리뷰 범위.

## 9-1. 추가 변경 — 테스트 씬 툴 (현민 요청 2026-11-26 "매번 업데이트할 때마다 툴로")
| 파일 | 변경 |
|---|---|
| `Assets/LHM/0_Script/Boss/New/Editor/BossTestSceneBuilder.cs` | 기본 보스 프리팹(`Assets/LHM/2_Prefabs/Boss.prefab`) 자동 지정, "보스 v2 흐름" 섹션(토글 기본 On·Config 지정), `SetupFlowV2`(새 씬 보스 인스턴스에 Driver 추가·참조·토글), `ResolveFlowConfig`(지정 → 검색 → 없으면 새로 생성), `AssignBool`, 메뉴 `Boss Arena (기본값으로 바로 생성)` |
- 프리팹 원본은 수정하지 않는다 (새 씬의 인스턴스 오버라이드만). 기존 씬은 열지 않음, 같은 이름 씬은 새 이름으로.
- ⚠️ **규칙 확인 필요**: Config 에셋이 하나도 없을 때 `AssetDatabase.CreateAsset` 으로 **새 `.asset`** 을 만든다 (기존 에셋 덮어쓰기 없음, `GenerateUniqueAssetPath`). AI_RULES 0장 2번 예외는 "새 Test_* 씬"만 명시 → 아래 규칙 변경 제안.
- 정적 점검: 줄 120자 이하, `[MenuItem]`·`CreateInstance`·`DestroyImmediate`·`AssetDatabase.FindAssets/CreateAsset/GenerateUniqueAssetPath`·`SerializedPropertyType.Boolean` 사용. 직렬화 필드 이름(`_config`·`_boss`·`_attackPatterns`·`_useFlowV2`·`_flowDriver`)은 상수 — 이름이 바뀌면 경고 로그로 드러남.

## 9-2. 규칙 변경 제안 (현민 판단)
- AI_RULES 0장 2번 예외에 추가: "테스트 씬 툴은 필요한 **Config SO 가 프로젝트에 없을 때만** 기본값으로 **새 `.asset`** 을 만들 수 있다 (기존 에셋 수정·덮어쓰기 금지)."
- 작업 절차에 추가: "씬 테스트가 필요한 작업은 `BossTestSceneBuilder`(테스트 씬 툴)도 함께 갱신해, 메뉴 한 번으로 그 작업을 테스트할 수 있는 새 Test_* 씬이 나오게 한다." (현민 요청 2026-11-26)

## 10. 질문 (현민 답변 필요)
- (T22) 체력바 색상 경계도 `>= 0.5f` 로 맞출지 (기본: 그대로).
- (T28) Player 채널 동기화 유지 여부 (기본 유지).
- (T28, 2차 테스트에서 발견) `GameClockDriver` 가 `Time.unscaledDeltaTime` 을 상한 없이 `GameClock.Tick` 에 넘긴다 → 에디터·로딩 프레임 정지 한 번에 FSM 타이머(예고·틈·휴식)가 통째로 건너뛰어질 수 있음.
  `Math.Min(unscaledDeltaTime, Time.maximumDeltaTime)` 로 상한을 둘지 (기본: 이번 작업 범위 밖이라 그대로, 승인 시 `[경량]` 으로 1줄 수정).
- (T31 3차 관찰) 패턴이 Stunned·Groggy 로 끊길 때 이미 쏜 발사체(곡사 포탄 등)를 남길지 지울지. 지금은 남음 (포탄은 자체 타임아웃으로 회수).
  기획서에 명시 없음 (전환·사망 때만 회수, T33). 기본: 그대로.

## 11. 다음 할 일
1. 현민: 컴파일 확인 → 에디터 작업 → 씬 테스트 (5-1) → 결과 알려주기 → 커밋 → Gemini 리뷰
2. 다음 작업: T32 (Resting 걷기) — 새 세션 권장

## 11-1. 커밋 제안 (현민이 판단해서 직접 커밋)
- **(현재) T31 본체는 `c343e0e` 로 커밋됨.** 남은 변경:
  - `[fix] T31 씬 테스트 반영: Bridge 타임아웃 시계, 테스트 씬 툴 v2 자동 연결·덮어쓰기 방지` —
    `Assets/LHM/0_Script/Boss/New/Gimmick/LegacyPatternBridge.cs`, `Gimmick/BossFlowDriver.cs`(툴팁),
    `Assets/LHM/0_Script/Boss/New/Editor/BossTestSceneBuilder.cs`, `Assets/LHM/8_MD/HANDOFF.md`
  - `[asset] BossFlowConfig 에셋` — `Assets/LHM/3_ScriptableObject/BossFlowConfig.asset` (+ `.meta`)
  - `Assets/LHM/Test_Boss.unity` — 툴이 덮어쓴 것. 되돌릴지(`git checkout`) 새 내용으로 `[asset]` 커밋할지 현민 판단.
- (이전 제안, 참고) 코드: `[feat] T31 BossFlowDriver + BossController useFlowV2 연결, LegacyPatternBridge` —
  `Assets/LHM/0_Script/Boss/New/Gimmick/BossFlowDriver.cs`, `IBossPattern.cs`, `LegacyPatternBridge.cs`
  (**`.meta` 3개 함께 커밋**), `Assets/LHM/0_Script/Boss/New/BossController.cs`,
  `Assets/LHM/0_Script/Boss/New/Editor/BossTestSceneBuilder.cs`,
  `Assets/LHM/8_MD/HANDOFF.md`, `Assets/LHM/8_MD/BACKLOG.md`
  (툴만 따로 나누려면 `[chore] 테스트 씬 툴에 v2 흐름 자동 연결` — `BossTestSceneBuilder.cs`)
- 씬: `Assets/LHM/Test_Boss.unity` (Driver 추가·참조 연결·토글) + `BossFlowConfig.asset`(+`.meta`, 아직 안 했으면) → `[asset] Test_Boss 보스에 BossFlowDriver 연결` 별도 커밋 권장.
