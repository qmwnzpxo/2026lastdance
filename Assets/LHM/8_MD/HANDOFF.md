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
- 상태: 🎮 현민 씬 테스트 대기
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
1. Unity Console 컴파일 에러 0 확인. 새 `.meta` 3개 생성 확인 (`IBossPattern`·`LegacyPatternBridge`·`BossFlowDriver`).
2. (T30 에서 안 했으면) `Create > UnMachine > Config > Boss Flow` 로 `BossFlowConfig` 에셋 생성, 초기값 확인.
3. `Test_Boss` 씬 → 보스 오브젝트(BossController 있는 것) 선택 → `Add Component > BossFlowDriver`.
4. BossFlowDriver 인스펙터:
   - `Config` ← `BossFlowConfig` 에셋
   - `Boss` ← 같은 오브젝트의 BossController
   - `Attack Patterns` ← BossController 의 `Attack Patterns` 필드에 연결된 것과 같은 BossAttackPatterns
   - `Legacy Cycle` 4개 항목 값 확인 (위 3장 표)
5. BossController 인스펙터 → `v2 흐름` 헤더: `Flow Driver` ← 방금 추가한 BossFlowDriver, `Use Flow V2` 체크.
6. 씬에 `GameClockDriver` 가 있는지 확인 (없으면 빈 오브젝트에 추가 — 없으면 v2 루프가 Telegraph 에서 멈춤).
7. 씬 저장 (토글 Off 테스트는 체크만 끄면 됨).

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
- (현민 확인 필요)

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

## 10. 질문 (현민 답변 필요)
- (T22) 체력바 색상 경계도 `>= 0.5f` 로 맞출지 (기본: 그대로).
- (T28) Player 채널 동기화 유지 여부 (기본 유지).

## 11. 다음 할 일
1. 현민: 컴파일 확인 → 에디터 작업 → 씬 테스트 (5-1) → 결과 알려주기 → 커밋 → Gemini 리뷰
2. 다음 작업: T32 (Resting 걷기) — 새 세션 권장

## 11-1. 커밋 제안 (현민이 판단해서 직접 커밋)
- 코드: `[feat] T31 BossFlowDriver + BossController useFlowV2 연결, LegacyPatternBridge` —
  `Assets/LHM/0_Script/Boss/New/Gimmick/BossFlowDriver.cs`, `IBossPattern.cs`, `LegacyPatternBridge.cs`
  (**`.meta` 3개 함께 커밋**), `Assets/LHM/0_Script/Boss/New/BossController.cs`,
  `Assets/LHM/8_MD/HANDOFF.md`, `Assets/LHM/8_MD/BACKLOG.md`
- 씬: `Assets/LHM/Test_Boss.unity` (Driver 추가·참조 연결·토글) + `BossFlowConfig.asset`(+`.meta`, 아직 안 했으면) → `[asset] Test_Boss 보스에 BossFlowDriver 연결` 별도 커밋 권장.
