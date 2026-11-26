# docs/BACKLOG.md — 기능 분해 결과와 작업 상태

> 큰 기능을 **리뷰 한 번에 검토 가능한 크기**(파일 4개·300줄 이하)의 작업으로 나눈 목록이다. (AI_RULES 1장)
> 초안은 Claude가 `/breakdown` 으로 작성하고, **현민이 승인한 작업만** 진행한다.
> 작업이 끝나면 Claude가 상태를 갱신한다 (커밋은 현민). 완료된 기능 묶음은 지우지 않고 아래로 내린다.
> 완료 기준은 **씬에서 현민이 확인할 수 있는 동작**으로 쓴다. 로직(Core) 작업은 바로 뒤 어댑터 작업의 씬 테스트에서 확인한다.
>
> 상태: ⬜ 대기 / ✅ 승인됨 (진행 가능) / 🔨 진행 중 / 🎮 현민 씬 테스트 대기 / 👀 리뷰 중 / 🏁 완료 (현민 커밋) / ⛔ 보류
> 유형: **로직**(Core 순수 클래스) / **어댑터**(MonoBehaviour 연결) / **연출**(VFX·애니메이션·사운드 호출) / **에디터**(현민 수작업)
> 〔임시값〕: boss.md 에서 `<!-- 임시(` 주석이 달린 값을 SO 초기값으로 쓰는 작업. 해당 항목이 확정되면 SO 값·문구를 다시 확인한다.

---

## 기능: 1챕터 보스 「판타스틱 베이비」 v2 재분해

- 요청 원문: [기능] 1챕터 보스 「판타스틱 베이비」 v2 재분해
- 기획서 근거: `Assets/LHM/8_MD/boss.md` (v2 확정) 5·6·7·8·11·16·17장
- 핵심 재미: **큰 놈의 동작을 읽고, 내 액션으로 받아치고, 쓰러뜨려서 끝장낸다**
  - 읽고 = Telegraph(눈 색) + 패턴 선택기 / 받아치고 = 패턴 P1~P6 + Punishable·Stunned / 끝장낸다 = 그로기 게이지 + 낙섬 QTE
- 분해 일시: 2026-11-25 / 기준 HEAD: `7f41c13` / 승인: ✅ 전체 승인 (현민 2026-11-25, T49는 ⛔ 유지)
- 경로 표기: `New/` = `Assets/LHM/0_Script/Boss/New/`. 새 코드는 Q1대로 `New/Core/`(순수 C#) · `New/Gimmick/`(MonoBehaviour·SO). 패턴 어댑터는 `New/Gimmick/Pattern/` 하위 폴더에 둔다 (Q1 범위 안, asmdef 없음)
- 비동기 원칙: 새 코드는 UniTask + `CancellationToken`(패턴별·페이즈별 CTS). 기존 코루틴은 **Q19 확정(C: 단계 교체)** — 새 FSM·선택기·패턴을 새로 만들고, 미구현 패턴만 `LegacyPatternBridge` 로 임시 호출, 마지막에 레거시 제거(T68·T69)
- 시간 원칙: 새 패턴·상태 로직은 `GameClock` World 채널 dt 를 쓴다. **Q20 확정**: World 배율은 `CustomTimeManager.EnemyTimeScale` 을 따르고, 히트스톱은 `HitstopManager` 에도 위임
- 피해 출처: **Q21 확정(A)** — `IHitSourceReceiver` 를 새로 두고 CSH `PlayerDash`·`PlayerCombat` 에서 1~3줄로 함께 호출 (CSH 수정 허용)
- 범위 밖 (이번 분해에 없음): 10장 사망 연출 v2 차이(기존 DeadState 유지), 14장 부위 파괴(⏸), 거울(⏸), 13장 스프라이트 추가

### 0. 기존 작업 판정 (v2 기준)

| ID | 기존 내용 | 판정 | 이유 (한 줄) | 이후 |
|---|---|---|---|---|
| T1 | IRandom·SystemRandom·GameLog·UniTask 예외 | 🏁 유지 | v2 모든 Core 작업의 공용 기반 | 완료 |
| T2 | BossController 이벤트 훅 | 🏁 유지 | Director·체력바·그로기가 그대로 구독 | 완료. PhaseTransition 무적은 T33에서 `ComputeInvulnerable` 에 추가 |
| T3 | BossArenaDirector + IBossGimmick + ArenaConfig | 수정 | 1P 기믹이 없어지고 2P 레이저 벽만 남음, 방 경계는 P1 그림자 클램프에도 필요 | B-6 T3 |
| T4 | Director 배치 (에디터) | 수정 | T3 축소에 맞춰 확인 항목만 변경 | B-6 T4 |
| T5~T8 | 1P 회전 레이저 | 다른 보스로 이동 | v2 12장: 이 보스 스프라이트·패턴과 연결 없음 | 부록 |
| T9~T13 | 1P RGB 판넬 | 다른 보스로 이동 | v2 12장: 1페이즈 학습 구간을 흐림 | 부록 |
| T14 | ClosingWallModel | 수정 | 최소 폭 24 Unit(결정 #16), 아재 성공 Push 삭제 | B-6 T14 |
| T15 | LaserWallGimmick | 수정 | 레이저 벽이 P2 돌진의 벽 충돌(Stunned) 대상이 되어야 함 (8장) | B-6 T15 |
| T16 | BossMirror (거울·V 프롬프트) | 폐기 | 거울 보류, 아재 패턴이 낙섬 QTE로 통합됨 (12장) | — |
| T17 | 아재 커맨드 시퀀스·판정 | 폐기 | 아재 패턴 → 7장 결정타로 통합 | → T64 (QTE 판정) |
| T18 | 아재 커맨드 UI + PlayerMove 잠금 | 폐기 | 커맨드 입력이 없어져 이동 잠금도 불필요 | → T65 |
| T19 | Groggy 상태 + 2P 기본 쉴드 | 폐기 (대체) | 쉴드 개념 삭제, Groggy 진입이 게이지 100으로 바뀌고 새 FSM 상태가 됨 | → T30·T31·T61 |
| T20 | B-3 에디터 (벽·거울·커맨드 UI·Animator Groggy=8) | 수정 | 거울·커맨드 UI 삭제, Animator 정수값이 T31 기준으로 바뀜 | B-6 T20 |
| T21 | 체력바 무적 표시 (쉴드 포함) | 수정 | 쉴드 없음 → 자물쇠는 Appearing·PhaseTransition만 (15장) | B-7 T21 |
| T22 | 50% 경계·반복 방지·드론 제한 | 수정 | 50% 경계만 남김. 반복 방지·거리는 새 선택기(T37), 드론 제한은 P6(T50)으로 이동 | B-0 T22 |
| T23 | 파편·에너지탄 회수 누락 보강 | 유지 | P4 탄종·P6 드론이 그대로 쓰여 회수 누락이 여전히 문제 | B-0 T23 |
| T24 | 최종 통합 (에디터) | 수정 | 확인 대상이 레이저·판넬·거울 → 벽·충격파·포탄·드론·QTE UI로 바뀜 | B-7 T24 |
| T25 | 아재 결과 연결 | 폐기 | 아재 패턴 삭제, 결과 연결은 낙섬 QTE로 | → T65 |
| T26 | 테스트 씬 생성 툴 | 🏁 유지 | v2 패턴 테스트도 `Test_Boss.unity` 에서 진행 | 완료 |

### A. v2 기준 코드 현황 (재사용·주의 지점, 근거: 파일:줄)

| 항목 | 현황 | v2에서의 처리 |
|---|---|---|
| FSM `BossState` | BossController.cs:11-21 (Idle…Walk = 0~7), Animator `State` 정수로 사용 | 새 상태 5개를 **맨 끝**(8~12)에 추가 (T31) |
| BT 콤보 선택 | BossController.cs:661-727, 거리 2/8/9 고정 콤보 | v2 선택기(T37)로 대체. 토글 Off 경로에만 남았다가 T68에서 제거 (Q19) |
| 휴식 | BossController.cs:784 `2f : 1f` 하드코딩 | `BossFlowConfig` 1.5 / 0.8 (T30) |
| 페이즈 경계 | BossController.cs:983 `> 0.5f` → HP 250에서 2P | T22 |
| 코루틴 패턴 | BossAttackPatterns.cs:311 `ExecutePattern` (Stomp·Burst·Rapid·Howitzer·Cannon·Charge·Drone), `WaitForSeconds` 16곳 | Q19 C: Bridge로 임시 호출 → 패턴별 교체 → T69에서 제거 |
| 반복 방지 | BossAttackPatterns.cs:316-335 `UnityEngine.Random` 사용 | Core 선택기 + `IRandom` (T37) |
| 돌진 벽 감지 | BossAttackPatterns.cs:956-959 `RaycastAll` + 태그 `Wall` | 참고만, T44에서 레이어 마스크로 새로 (레이저 벽 포함) |
| 드론 제한 | BossAttackPatterns.cs:1051 `>= 1` 하드코딩 | T50 Config 필드 |
| 시간 | `Assets/Scripts/CustomTimeManager.cs` (Player·Enemy·Ghost 배율 + 히트스톱 배율), `CSH/0.Scripts/Hitstop/HitstopManager.cs:24` — **보스는 둘 다 안 씀** | Q20: GameClock 소스로 연결 (T28) |
| 피해 인터페이스 | `Assets/Scripts/IDamageable.cs:4` `TakeDamage(float)` — 출처(일섬·일반) 정보 없음 | Q21 A: `IHitSourceReceiver` 추가 + CSH 호출 (T61) |
| 낙섬 QTE | `CSH/0.Scripts/Player/Dashs/BlueDash.cs:65-95` — 1초 슬로우 중 Space 누르면 낙하 (성공/실패 2단) | Q22 A: 기존 흐름 유지, 보스 전용 타이밍 바 (T64·T65) |
| 페이즈 전환 연출 | BossController.cs:1259 `PhaseTransitionEffect` 코루틴 | 토글 On이면 T33·T34 경로 사용 |

### B. 작업 목록

#### B-0. 기반·기존 보정

| ID | 유형 | 작업 | 완료 기준 (씬) | 범위 밖 | 의존 | 근거 | 예상 파일 | 상태 |
|---|---|---|---|---|---|---|---|---|
| T1 | 로직 | 최소 공용 기반 (IRandom·SystemRandom·GameLog·UniTask 예외 등록) | — | — | — | — | Core/IRandom.cs 외 3 | 🏁 |
| T2 | 어댑터 | BossController 이벤트 훅 (BattleStarted·PhaseChanged·Died·InvulnerableChanged) | — | — | T1 | — | New/BossController.cs | 🏁 |
| T26 | 에디터 툴 | 테스트 씬 생성 툴 `UnMachine/Test Scene/Boss Arena` | — | — | — | — | New/Editor/BossTestSceneBuilder.cs | 🏁 |
| T27 | 로직 | `GameClock`: 채널 World·Player·UI, 채널 `Scale`, `HitStop(seconds)`(겹치면 긴 쪽 유지, World·Player만 0), `Tick(realDt)` → 채널별 `DeltaTime` | (T28 씬) 히트스톱 중 World·Player dt = 0·UI dt 진행, 0.2초 진행 중 0.5초 요청 → 0.5초 유지 | 프로 모션 채널 연동, 플레이어 코드 교체 | T1 | ARCH 3-1, boss.md 5장 Hit | Core/GameClock.cs, Core/ClockChannel.cs | 👀 |
| T28 | 어댑터 | `GameClockDriver` (Q20·Q25): 매 프레임 Tick, World 채널 배율 = `CustomTimeManager.EnemyTimeScale`, `HitStop` 은 `HitstopManager.TriggerHitstop(seconds, 0.05)` 에도 위임(배율 0.05 = CSH Light 프리셋과 동일, Driver 인스펙터 필드). **(CSH 수정, Q25)** `HitstopManager`: ① `finally` 에서 자기 CTS만 정리(다음 요청 CTS 취소·배율 복구 금지) ② `RemainingTime`(실제 초) 공개 ③ 겹치면 **종료 시각이 늦은 쪽 유지**(짧은 새 요청은 무시, 긴 요청은 연장) — 플레이어 기존 히트스톱에도 동일 적용. 인스펙터 읽기 전용(채널 dt·GameClock/HitstopManager 남은 시간), 에디터 전용 F2 = 히트스톱 0.5초 | F2 → 0.5초 동안 인스펙터 World dt 0 + 플레이어 0.05배 슬로우 → 1배속 복귀. F2 연타 → 정지가 누적되지 않고 긴 쪽만. 플레이어 공격 히트스톱(0.8초) 중 F2 → 0.8초가 잘리지 않음(HitstopManager 남은 시간 확인). 낙섬 QTE 슬로우 중 World dt도 느려짐 | 보스 로직의 dt 전환(T31~), `TimeScaleReceiver` 로그 정리 | T27 | ARCH 3-1, Q20·Q25 | Gimmick/GameClockDriver.cs, CSH/0.Scripts/Hitstop/HitstopManager.cs(수정) | 👀 |
| T22 | 어댑터 | (수정) 50% 경계만: `GetCurrentPhase` `>= 0.5f` → 1P, 체력바 표시도 동일 | HP 250 → 1P 유지, 249 → 2P 전환 1회 | 반복 방지(→T37), 드론 제한(→T50), 거리(→T37) | — | 5장 "HP 50% 최초 하회" | New/BossController.cs, New/BossHealthBar.cs | 👀 |
| T23 | 어댑터 | (유지) 회수 누락 보강: AirBurst·Bounce 파편을 기존 파편 풀로, 에너지탄 회수 경로 → 전환·사망 시 함께 회수 | 곡사 AirBurst/Bounce 직후 F1 → 파편 전부 사라짐, 에너지탄 비행 중 보스 사망 → 사라짐 | 새 패턴 회수(각 패턴 작업) | T2 | 6장 P4 탄종·P6, 8장 전환 1번 | New/HowitzerCannonball.cs, New/ConductorDrone.cs, New/EnergyBullet.cs, New/BossController.cs | ✅ |

#### B-1. 상태 흐름 (Telegraph · Punishable · Stunned · Groggy · PhaseTransition)

| ID | 유형 | 작업 | 완료 기준 (씬) | 범위 밖 | 의존 | 근거 | 예상 파일 | 상태 |
|---|---|---|---|---|---|---|---|---|
| T29 | 로직 | `BossFlowMachine` 기본 루프: `BossFlowState`(Idle·Telegraph·Attacking·Punishable·Resting), `Begin(PatternTiming)` → Telegraph(**최소 0.4초 보장**) → Attacking(외부 `NotifyAttackEnded(PatternResult)` 까지) → Punishable(패턴 값, 2P ×0.8) → Resting(1P 1.5 / 2P 0.8) → Idle, `StateChanged` 이벤트, `Tick(dt)` | (T31 씬) 상태 라벨이 순서대로 바뀜, 예고 0.2초로 넣어도 0.4초 유지, F1 후 휴식 0.8초 | 인터럽트 상태(T30), 패턴 내용 | T1 | 5장 상태표·흐름, 8장 휴식·틈 ×0.8, 9장 최소 0.4초 | Core/BossFlowMachine.cs, Core/BossFlowState.cs, Core/PatternTiming.cs, Core/PatternResult.cs | 🎮 |
| T30 | 로직 | 인터럽트 상태: `EnterStunned()`·`EnterGroggy()`·`RequestPhaseTransition()`·`EnterDead()`, 우선순위 Dead > PhaseTransition > Groggy > Stunned > 기본 루프, **Groggy 중 전환 요청은 보류 → Groggy 끝난 직후 PhaseTransition**, `DamageMultiplier`(기본 1.0 / Punishable·Stunned 1.2 / Groggy 1.5), `BossFlowConfig` SO(휴식·Stunned 1.5·Groggy 4·최소 예고 0.4·2P 틈 ×0.8·배율) 〔임시값: 배율 3단계, 상태명 PhaseTransition〕 | (T31 씬) F3 → Stunned 1.5초 → Resting, F4 → Groggy 4초, Groggy 중 F1 → Groggy 끝난 뒤 PhaseTransition, 인스펙터 배율 표시가 상태마다 1.0/1.2/1.5 | 그로기 게이지(T60), 전환 연출(T34) | T29 | 4장 배율, 5장 흐름, 7장 보류 규칙, 17장 #12~#14 | Core/BossFlowMachine.cs(확장), Core/BossDamageScale.cs, Gimmick/BossFlowConfig.cs | 🎮 |
| T31 | 어댑터 | `BossFlowDriver` + BossController 연결: `useFlowV2` 토글(Off = 기존 그대로). On이면 BattleStarted 이후 Idle/Attacking/Resting 코루틴 대신 Driver가 루프 소유. Attacking = `IBossPattern.RunAsync(ctx, ct)` — 이 작업에선 `LegacyPatternBridge`(기존 `ExecutePattern` 을 UniTask로 감쌈, `// guard-allow: 레거시 래핑`) 하나로 고정 순환. `TakeDamage` 에 배율 적용. `BossState` 끝에 Telegraph=8·Punishable=9·Stunned=10·Groggy=11·PhaseTransition=12 추가. 디버그: 머리 위 상태 라벨(에디터 전용), F3 Stunned·F4 Groggy | 토글 On → 착지 후 라벨 Idle→Telegraph→Attacking→Punishable→Resting 반복, Attacking 안에서 기존 패턴 실행, Punishable 중 공격 → 로그 피해 ×1.2, 사망 → 루프 즉시 중단·Dead만 진행. 토글 Off → T2 씬 테스트와 같은 동작 | 걷기(T32), 선택기(T38), 전환(T33) | T28·T30 | 5장, Q19 C | Gimmick/BossFlowDriver.cs, Gimmick/IBossPattern.cs, Gimmick/LegacyPatternBridge.cs, New/BossController.cs(+40) | 🎮 |
| T32 | 어댑터 | Resting 걷기: 플레이어 쪽으로 걷기(속도 3, 1.5 Unit 이내 정지), 방향 전환, World dt 사용. 걷기 값은 `BossFlowConfig` | Resting 중 보스가 플레이어 쪽으로 걷다가 가까우면 멈춤(떨림 없음), F2 히트스톱 중 멈춤 | 걷기 애니(runboost 0.5배속, T54) | T31 | 4장 걷기 3, 5장 Resting | Gimmick/BossLocomotion.cs, Gimmick/BossFlowDriver.cs(+) | 🎮 |
| T33 | 어댑터 | PhaseTransition 상태 연결 (토글 On): 50% 최초 하회 → `RequestPhaseTransition`(Groggy면 보류) → 진입 시 패턴 CTS 취소·`ForceStopAllPatterns`·발사체 회수·완전 무적(`ComputeInvulnerable` 에 추가 → `InvulnerableChanged` true) → Config 시간 뒤 2P Idle + `PhaseChanged(2)`. F1 디버그도 같은 경로 〔임시값: 상태명〕 | HP 249 → 라벨 PhaseTransition, 그동안 피해 0·발사체 전부 사라짐, 끝나면 2P 루프(휴식 0.8). Groggy 중 하회 → Groggy 끝난 뒤 전환. 전환은 1회만 | 역재생·틴트(T34) | T22·T31 | 5장, 8장 전환 1~4, 17장 #13·#14 | New/BossController.cs, Gimmick/BossFlowDriver.cs | ✅ |
| T34 | 연출 | 전환 연출: lending 역재생, 증기 분출, **붉은 틴트를 2P 동안 유지**, 화면 흔들림 호출 | 전환 시 역재생·증기·흔들림 1회, 이후 2P 내내 붉은 틴트, 눈은 예고 때만 색 | 포효 스프라이트(13장) | T33 | 8장 2~3, 9장 | Gimmick/BossPhaseTransitionView.cs | ✅ |
| T35 | 연출 | 피격: 흰색 번쩍임 + `GameClock.HitStop(0.05)` (Config). 기존 피격 점멸이 있으면 유지하고 히트스톱만 추가 | 피격마다 흰색 번쩍 + 짧은 정지, 연타해도 정지가 길어지지 않음 | 패턴별 히트스톱(T51) | T28·T31 | 5장 Hit | Gimmick/BossHitFeedback.cs | ✅ |
| T36 | 에디터 | Animator State 8~12 상태·전이, `BossFlowConfig` 에셋, Test_Boss 보스에 Driver·View 연결·`useFlowV2` On | 현민 플레이: T31~T35 완료 기준 재확인 | — | T31~T35 | — | (현민) | ✅ |

#### B-2. 패턴 선택기 + 예고

| ID | 유형 | 작업 | 완료 기준 (씬) | 범위 밖 | 의존 | 근거 | 예상 파일 | 상태 |
|---|---|---|---|---|---|---|---|---|
| T37 | 로직 | `PatternSelector`: `SelectionUnit`(P1~P6·C1~C4), 거리 구간(근 x<5 / 중 5≤x<20 / 원 x≥20) × 페이즈별 가중치, **체류 보정**(같은 구간 3초 이상 → 근:P1, 원:P2 가중치 ×2), **반복 방지**(같은 단위 2연속이면 다음엔 제외, 콤보 = 단위 1), 사용 불가 단위 대체(P6 → P5), `IRandom` + `DistanceBandTracker`(dt로 체류 시간) + `PatternTableConfig` SO(11장 표) 〔임시값: 2P 가중치〕 | (T38 씬) 고정 시드로 재시작하면 같은 선택 순서, 체류 3초 후 가중치 ×2 표시, 같은 단위 3연속 0회 | 콤보 실행(T55~) | T1 | 11장, 17장 #8·#10 | Core/PatternSelector.cs, Core/DistanceBandTracker.cs, Core/SelectionUnit.cs, Gimmick/PatternTableConfig.cs | ✅ |
| T38 | 어댑터 | 선택기 연결: Driver Idle → 선택기 → `PatternRegistry`(단위 → `IBossPattern`). 미구현 단위는 LegacyBridge 임시 매핑(P1·P3→Stomp, P2→Charge, P4→Howitzer, P5→MachinegunBurst, P6→DroneSpawn, 콤보 → 첫 패턴 + 경고 로그). 활성 드론 수로 P6 사용 가능 판정. 인스펙터 **"강제 선택 단위"**(에디터 전용, 패턴별 테스트용, 반복 방지 무시). 디버그 표시(구간·체류 시간·후보 가중치·최근 2개) | 가까이 서면 P3 위주, 근거리 3초 이상 → P1 가중치 ×2 표시, 같은 단위 3연속 없음, F1 후 2P 표(P6·콤보 후보 등장), 강제 단위 지정 시 그것만 반복 | 각 패턴 구현 | T31·T37 | 11장 | Gimmick/PatternRegistry.cs, Gimmick/PatternSelectorDebugView.cs, Gimmick/BossFlowDriver.cs(+) | ✅ |
| T39 | 연출 | 눈 색 예고: `TelegraphSignal`(Core: 단위 → 빨강 깜빡 2회 / 노랑 / 하양) + `BossTelegraphView`(눈 오버레이 색·깜빡임, Telegraph 동안만) | P2·P3 빨강 2회 깜빡, P4~P6 노랑, P1 하양, 예고 밖에서는 꺼짐 (T38 임시 매핑 상태로 확인) | 콤보 신호(T58), 2P 틴트(T34) | T38 | 9장, 17장 #7 | Core/TelegraphSignal.cs, Gimmick/BossTelegraphView.cs | ✅ |
| T40 | 에디터 | 보스 프리팹에 눈 오버레이 오브젝트(작은 원형 스프라이트), `PatternTableConfig` 에셋 | 현민 플레이: T38·T39 완료 기준 재확인 | — | T39 | — | (현민) | ✅ |

#### B-3. 패턴 P1~P6 (`IBossPattern` 구현 → Registry 임시 매핑 교체)

| ID | 유형 | 작업 | 완료 기준 (씬, 강제 선택 단위로) | 범위 밖 | 의존 | 근거 | 예상 파일 | 상태 |
|---|---|---|---|---|---|---|---|---|
| T41 | 로직 | `PatternTimeline`: 이름 있는 구간(예고·활성·복귀) 목록, `Tick(dt)` → 현재 구간·구간 진입 이벤트·`IsActive`. 프레임 표기 → 초 변환은 Config 주석(60fps) | (T42 씬) P3 판정 Gizmo가 활성 구간에만 켜짐 | — | T1 | 6장 원칙 "예고 길게, 공격 짧게, 복귀 길게" | Core/PatternTimeline.cs, Core/TimelineSegment.cs | ✅ |
| T42 | 어댑터 | **P3 짓밟기** `StompPattern` + `StompConfig`: 다리 들기 → 4프레임 정지 0.45초(예고) → 5~6프레임 0.1초 활성(전방 3 Unit, 피해 5, 넉백) → 복귀 0.9초 = Punishable 〔임시값: 짓밟기 해석 — 아트 확인 대기〕 | 판정 Gizmo가 약 0.1초만 표시, 정면 3 Unit 안에서만 피해 5, 뒤쪽·3 Unit 밖·일섬 무적 중 0, 직후 Punishable 0.9초 | 연출(T51), 애니 클립(T54) | T38·T41 | 6장 P3, 2장 | Gimmick/Pattern/StompPattern.cs, Gimmick/Pattern/StompConfig.cs | ✅ |
| T43 | 로직 | `ChargeRunModel`: 돌진 진행(속도·dt), 플레이어 통과 판정(상대 x 부호 변화), 결과(HitPlayer / HitWall / ReachedEnd), 통과 후 경과 시간(C1 Brake용) | (T44 씬) 결과 로그가 실제 상황과 일치 | — | T29 | 6장 P2, 8장 C1 | Core/ChargeRunModel.cs | ✅ |
| T44 | 어댑터 | **P2 질주 돌진** `ChargePattern` + `ChargeConfig`: 예고 0.6초(runboost 1프레임 정지), 속도 18 직선 돌진(World dt), 몸체 피해 6·넉백 강, 벽 판정 = Config 레이어 마스크(방 벽 + 레이저 벽). HitWall → `EnterStunned` 1.5초 + `GroggyHit(+25)` 이벤트(게이지 연결은 T61) / HitPlayer·ReachedEnd → 틈 0.6초 | 피하면 벽에서 멈추고 Stunned 1.5초 + 로그 "그로기 +25", 맞으면 Punishable 0.6초, 일섬 통과 시 피해 0, 점프로는 못 넘음 | 잔상·스파크(T52), Brake(T57) | T38·T43 | 6장 P2, 4장 돌진 18, 3장 단단한 벽 | Gimmick/Pattern/ChargePattern.cs, Gimmick/Pattern/ChargeConfig.cs | ✅ |
| T45 | 로직 | `JumpSlamModel`(웅크림 0.5 → 도약 → 체공 추적 1.2초(그림자 x = 플레이어 x, 방 경계 클램프) → 고정 0.4초 → 낙하 → 착지 → 틈 1.2초) + `ShockwaveModel`(좌우 이동 속도 12, 높이, 경계 도달 시 소멸) | (T46 씬) 그림자가 1.2초 추적 후 0.4초 고정, 방 밖으로 안 나감 | — | T41 | 6장 P1 | Core/JumpSlamModel.cs, Core/ShockwaveModel.cs | ✅ |
| T46 | 어댑터 | **P1 점프 내려찍기** `JumpSlamPattern` + `JumpSlamConfig` + `BossShockwave`(풀) + `BossShadowMarker`: 천장 + 5 Unit 화면 밖 → 몸체 콜라이더 Off + 피해 거부(**자물쇠 없음**, `IsInvulnerable` 변화 없음), 낙하 시 콜라이더 On, 착지 범위 피해 8 + 좌우 충격파(피해 4, 속도 12, 높이 1) | 보스가 화면 밖으로 사라지고 그림자가 따라오다 멈춤, 그림자 밖이면 착지 피해 0, 충격파는 점프로 넘으면 0·서 있으면 4, 화면 밖 동안 공격 불가·자물쇠 안 뜸, 착지 후 Punishable 1.2초, F1·사망 시 충격파 사라짐 | 연출(T51) | T3·T38·T45 | 6장 P1, 4장 점프 높이, 15장, 17장 #2·#11 | Gimmick/Pattern/JumpSlamPattern.cs, Gimmick/Pattern/JumpSlamConfig.cs, Gimmick/Pattern/BossShockwave.cs, Gimmick/Pattern/BossShadowMarker.cs | ✅ |
| T47 | 어댑터 | **P4 포드 곡사** `PodHowitzerPattern` + Config: 포드 들기 → 6발 0.25초 간격 → 경고 마커 1초 → 0.4초 간격 낙하(반경 1.5, 피해 6), 탄종 가중치는 기존 규칙, 기존 포탄·마커 풀 재사용, 발사 중 보스 제자리 = Attacking(피해 ×1.0), 발사 후 Punishable 0초 (Q24) | 마커 6개가 순서대로 뜨고 낙하, 마커 밖 피해 0, 탄종 3종 섞임, F1 시 포탄·마커·파편 전부 사라짐 | 포탄 일섬 베기(T62), 연출(T53) | T23·T38 | 6장 P4 | Gimmick/Pattern/PodHowitzerPattern.cs, Gimmick/Pattern/PodHowitzerConfig.cs | ✅ |
| T48 | 어댑터 | **P5 포드 직사 (회피 + 반격 버전)** `PodShotPattern` + Config: 포드 앞으로 0.4초 정지 → 3발 0.2초 간격 조준(피해 3, 속도 14, 기존 총알 풀), 2P 5발, 직후 Punishable 0.6초 〔임시값: 패링 없는 버전〕 | 0.4초 정지 후 3발이 발사 순간 플레이어 위치로, F1 후 5발, 끝나고 Punishable 0.6초 | 패링(T49) | T38 | 6장 P5, 17장 #1 | Gimmick/Pattern/PodShotPattern.cs, Gimmick/Pattern/PodShotConfig.cs | ✅ |
| T49 | 어댑터 | ⏳ **P5 패링 반사** (분리): 패링 → 반사탄이 보스에게(피해 10, 그로기 +15), 2P 마지막 1발 빨간 탄(패링 불가, 탄 색으로 구분) | 패링 성공 시 탄이 돌아가 보스 피해 10·게이지 +15, 빨간 탄은 패링해도 피해 | — | **패링 구현(CSH)**·T48·T61 | 6장 P5 ⏳, 7장 ⏳, 9장 ⏳ | Gimmick/Pattern/PodShotPattern.cs(+), New/BossProjectile.cs(반사 상태) | ⛔ |
| T50 | 어댑터 | **P6 포드 드론 사출** `PodDroneLaunchPattern` + Config: 포드 들기 → 한쪽 포드 위치에서 기존 지휘자 드론 사출(기존 드론 시스템 재사용), 동시 활성 상한을 Config 필드로(기존 `>= 1` 하드코딩 대체), 직후 Punishable 0.5초. 2P 전용 | 2P에서 드론이 포드 쪽에서 나와 8자 비행, 드론이 살아 있으면 P6 → P5 대체 로그, 드론 전멸 후 다시 P6 가능 | 연섬 판정(플레이어 쪽), 드론 내부 로직 | T38 | 6장 P6, 4장 드론 최대 1 | Gimmick/Pattern/PodDroneLaunchPattern.cs, Gimmick/Pattern/PodDroneConfig.cs, New/BossController.cs(드론 수 조회만) | ✅ |
| T51 | 연출 | P1·P3: P1 부스터 불꽃·착지 히트스톱 0.08·흔들림 강·흙먼지·파편 / P3 내려찍기 히트스톱 0.05·발밑 흙먼지 | 착지·내려찍는 순간 정지감과 이펙트가 판정 시점에 1회씩 | 아트 제작 | T42·T46 | 6장 P1·P3 연출 | Gimmick/Pattern/JumpSlamView.cs, Gimmick/Pattern/StompView.cs | ✅ |
| T52 | 연출 | P2: 돌진 잔상, 벽 충돌 흔들림, 머리 위 스파크(Stunned·Groggy 동안) | 돌진 중 잔상, 벽 충돌 시 흔들림 + 스파크, Stunned 끝나면 스파크 꺼짐 | — | T44 | 6장 P2, 7장 스파크 | Gimmick/Pattern/ChargeView.cs, Gimmick/BossStunView.cs | ✅ |
| T53 | 연출 | 포드 사격 P4·P5·P6: `포격` 시트 위로(P4·P6) / 앞으로(P5) 프레임 제어, 발사 섬광 | 곡사·사출은 포드가 위로, 직사는 앞으로 내려온 뒤 발사 | — | T47·T48·T50 | 2장, 6장 | Gimmick/Pattern/PodView.cs | ✅ |
| T54 | 에디터 | 패턴 애니 클립·Animator 상태(jumping·lending·runboost·Brake·Stomping/Down·포격), 그림자·충격파·스파크 프리팹, 패턴 Config 에셋, 레이어 마스크(플레이어·벽·레이저 벽) | 현민 플레이: T42~T53 완료 기준 재확인 | — | T51~T53 | 2장 | (현민) | ✅ |

#### B-4. 2페이즈 콤보 C1~C4

| ID | 유형 | 작업 | 완료 기준 (씬, 강제 선택 단위로) | 범위 밖 | 의존 | 근거 | 예상 파일 | 상태 |
|---|---|---|---|---|---|---|---|---|
| T55 | 로직 | `ComboPlan` + `ComboRunner`: 콤보 = 단계 목록(C1 P2→Brake→P3, C2 P1→P5, C3 P1×3, C4 P4→P2), 단계 사이 Punishable·Resting 생략, 마지막 단계만 틈(C3은 긴 틈), 결과 분기(C1: P2 HitPlayer → P3 생략·틈 0.6 / 통과 전 HitWall → Stunned) 〔임시값: C1 예외 2가지〕 | (T56·T57 씬) 단계 진행 로그가 규칙과 일치 | — | T29·T37 | 8장 콤보 표·C1 예외, 11장, 17장 #9·#10 | Core/ComboPlan.cs, Core/ComboRunner.cs, Core/ComboStep.cs | ✅ |
| T56 | 어댑터 | C2·C3 (점프 계열) 실행: Driver가 콤보 단계를 이어 실행 | C2: 착지 즉시 직사(틈 없음). C3: 3번 점프 사이 틈 없음, 마지막 착지 후에만 긴 틈 | — | T46·T48·T55 | 8장 | Gimmick/BossFlowDriver.cs(+), Gimmick/PatternRegistry.cs(+) | ✅ |
| T57 | 어댑터 | C1·C4 (돌진 계열): `ChargePattern` 에 "통과 후 0.3초 뒤 Brake 정지" 옵션, C1 예외, C4 = P4 **발사 직후**(낙하를 기다리지 않고) P2 | C1: 플레이어를 지나치고 0.3초 뒤 정지 → 짓밟기, 돌진에 맞으면 짓밟기 없이 0.6초 틈, 지나치기 전 벽 → Stunned. C4: 발사가 끝나자마자 돌진, 포탄은 계속 낙하 | — | T44·T47·T55 | 8장, 17장 #9 | Gimmick/Pattern/ChargePattern.cs(+), Gimmick/BossFlowDriver.cs(+), Gimmick/PatternRegistry.cs(+) | ✅ |
| T58 | 연출 | 콤보 시작 신호: 증기 분출 + 경고음, 첫 패턴의 눈 색 예고보다 **먼저** | 콤보 선택 시 증기·경고음 → 그다음 눈 색, 단독 패턴에서는 안 나옴 | — | T39·T56·T57 | 9장 | Gimmick/ComboSignalView.cs | ✅ |
| T59 | 에디터 | 증기 파티클·경고음 클립 연결 | 현민 플레이: T56~T58 재확인 | — | T58 | — | (현민) | ✅ |

#### B-5. 그로기 게이지 + 낙섬 QTE 결정타

| ID | 유형 | 작업 | 완료 기준 (씬) | 범위 밖 | 의존 | 근거 | 예상 파일 | 상태 |
|---|---|---|---|---|---|---|---|---|
| T60 | 로직 | `GroggyGauge`: 최대 100, `Add(GroggySource)`(벽 충돌 25·일섬 10·포탄 베기 5·일반 1·⏳패링 15, Config), 마지막 피격 3초 뒤부터 초당 5 감소, 100 → `Filled` 1회, Groggy 끝나면 0 | (T61 씬) 게이지 값이 규칙대로 증감 | — | T1 | 7장 표·감소 규칙, 4장 최대 100 | Core/GroggyGauge.cs, Core/GroggySource.cs, Gimmick/GroggyConfig.cs | ✅ |
| T70 | 어댑터 | 피해 출처 전달 (Q21 A): `HitSource`(Slash 일섬 / Normal 일반 / BlueFall 낙섬) + `IHitSourceReceiver.OnHitBy(HitSource)` 신설(`Assets/Scripts/` 의 `IDamageable` 옆), CSH `PlayerDash`(일섬·낙섬 판정 :421 부근)·`PlayerCombat`(:213 부근)에서 `TakeDamage` 와 같은 대상에 1~3줄로 호출. 낙섬 호출 지점은 착수 시 확인 | (T61 씬) 보스 인스펙터 "마지막 출처"가 일반 공격 → Normal, 일섬 → Slash, 낙섬 → BlueFall. 보스 외 적은 기존과 같은 피해(인터페이스 미구현 → 호출 없음), 플레이어 동작 변화 없음 | 보스 쪽 처리(T61) | T1 | 7장, Q21 | Core/HitSource.cs, Assets/Scripts/IHitSourceReceiver.cs, CSH/0.Scripts/Player/PlayerDash.cs(+3), CSH/0.Scripts/Player/PlayerCombat.cs(+3) | ✅ |
| T61 | 어댑터 | 게이지 연결: `BossGroggyDriver`(보스 오브젝트, `IHitSourceReceiver` 구현) → 출처별 `Add`, P2 HitWall → +25, `Filled` → `EnterGroggy`(4초, ×1.5). 디버그: 인스펙터 게이지 값·마지막 출처, 에디터 전용 F5 = +25 | 일반 공격 → +1, 일섬 명중 → +10, 벽 충돌 → +25, 3초 가만히 → 초당 5 감소, 100 → Groggy 4초(로그 피해 ×1.5) → 게이지 0 | UI(T63), QTE(T65) | T31·T44·T60·T70 | 7장 | Gimmick/BossGroggyDriver.cs, Gimmick/BossFlowDriver.cs(+Groggy 진입 연결) | ✅ |
| T62 | 어댑터 | 포탄 일섬 베기: 낙하 중 포탄이 `HitSource.Slash` 를 받으면 공중 폭발(플레이어 피해 없음) + 그로기 +5. 포탄 콜라이더가 일섬 BoxCast 마스크에 포함되는지 착수 시 확인 | 일섬으로 포탄을 지나가면 그 자리에서 폭발, 피해 0, 게이지 +5. 일반 공격으로는 안 베어짐 | — | T47·T61 | 6장 P4, 7장 | New/HowitzerCannonball.cs(+) | ✅ |
| T63 | 어댑터 | 그로기 UI: 체력바 아래 얇은 게이지 바, Groggy 진입 시 보스 머리 위 "낙섬!" 아이콘 | 바가 게이지 값대로 움직임, Groggy 동안만 아이콘 | 바 레이아웃(에디터) | T61 | 15장 | Gimmick/BossGroggyBar.cs, Gimmick/FinisherPromptView.cs | ✅ |
| T64 | 로직 | `FinisherQteJudge` (Q22 A, 보스 전용 타이밍 바): 커서 진행(dt), Perfect / Normal 구간, 입력 → Perfect 60 / Normal 30 / Fail 0, 시간 초과 Fail + `FinisherConfig`(바 1초·Perfect 0.1초·Normal 0.3초 〔임시값〕) | (T65 씬) 구간별 결과가 Config 값과 일치 | — | T1 | 7장, Q22 | Core/FinisherQteJudge.cs, Gimmick/FinisherConfig.cs | ✅ |
| T65 | 어댑터 | 낙섬 QTE 연결 (Q22 A): Groggy 중 보스가 `HitSource.BlueFall` 을 받으면 보스 쪽에서 QTE 바 표시(UI 채널 dt, 기존 BlueDash QTE·낙하 흐름은 그대로) → 결과 피해 **60 / 30 고정, Groggy ×1.5 미적용**(Q23), Fail → Groggy 즉시 종료 → Resting, 보류된 PhaseTransition 처리 | Groggy 중 낙섬 명중 → 바 표시, Perfect 60 / Normal 30(로그에 배율 없음) / Fail 0 + Groggy 즉시 끝. Groggy 아닐 때 낙섬은 기존대로. QTE 중 HP 50% 하회 → QTE·Groggy 끝난 뒤 전환. QTE 중 보스 사망 → 바 닫힘 | Perfect 연출(T66) | T61·T63·T64 | 7장, 5장, Q22·Q23 | Gimmick/FinisherQtePresenter.cs, Gimmick/FinisherQteView.cs, Gimmick/BossGroggyDriver.cs(+) | ✅ |
| T66 | 연출 | Perfect 연출: GameClock World 슬로우 + 컷인 | Perfect일 때만 슬로우·컷인, 끝나면 1배속 복귀 | 컷인 아트 | T28·T65 | 7장 | Gimmick/FinisherCutinView.cs | ✅ |
| T67 | 에디터 | 그로기 바·QTE 바·컷인 UI, "낙섬!" 아이콘, Config 에셋 | 현민 플레이: T61~T66 재확인 | — | T63~T66 | 15장 | (현민) | ✅ |

#### B-6. 2페이즈 아레나 — 레이저 벽

| ID | 유형 | 작업 | 완료 기준 (씬) | 범위 밖 | 의존 | 근거 | 예상 파일 | 상태 |
|---|---|---|---|---|---|---|---|---|
| T3 | 어댑터 | (수정) `BossArenaDirector` + `IBossGimmick` + `BossArenaConfig`: 1P 기믹 없음, PhaseChanged(2) → 레이저 벽 시작, Died → 전부 Cancel + ReleaseAll. 방 경계 40×30은 P1 그림자 클램프에도 사용 | Scene 뷰 Gizmo로 방 경계·레이저 벽 최소 폭 24 표시, 기믹 0개로도 시작·취소 로그가 1회씩 | 개별 기믹 | T2 | 3장, 8장 | Gimmick/BossArenaDirector.cs, Gimmick/IBossGimmick.cs, Gimmick/BossArenaConfig.cs | ✅ |
| T4 | 에디터 | (수정) Test_Boss·보스룸 씬에 Director 배치, Config 에셋 | 현민 플레이: T3 완료 기준 재확인 | — | T3 | — | (현민) | ✅ |
| T14 | 로직 | (수정) `ClosingWallModel`: 좌/우 벽 x, 수렴 속도, **최소 폭 24 Unit** 보장 (Config). `Push` 삭제 | (T15 씬) Gizmo에서 벽이 폭 24에서 멈춤 | 피해 판정 | T1 | 8장 레이저 벽, 17장 #16 | Core/ClosingWallModel.cs | ✅ |
| T15 | 어댑터 | (수정) `LaserWallGimmick` + `LaserWallConfig` + `LaserWallHitbox`: 양 끝 벽 이동, 플레이어 접촉 틱 피해, **돌진 벽 판정 레이어**에 포함(P2 Stunned 대상) | F1 → 양 끝에서 좁혀와 폭 24에서 정지, 닿으면 틱 피해, 2P 돌진이 레이저 벽에 닿으면 Stunned(T44 이후), 사망 → 사라짐 | — | T3·T14 | 8장 | Gimmick/LaserWallGimmick.cs, Gimmick/LaserWallConfig.cs, Gimmick/LaserWallHitbox.cs | ✅ |
| T20 | 에디터 | (수정) 레이저 벽 2개 프리팹(레이어 = 돌진 벽 마스크), Config 에셋 | 현민 플레이: T15 재확인 | — | T15 | — | (현민) | ✅ |

#### B-7. UI·통합·정리

| ID | 유형 | 작업 | 완료 기준 (씬) | 범위 밖 | 의존 | 근거 | 예상 파일 | 상태 |
|---|---|---|---|---|---|---|---|---|
| T21 | 어댑터 | (수정) 체력바 자물쇠: `InvulnerableChanged` 구독 → Appearing·PhaseTransition 동안만 표시. P1 화면 밖·0.2초 피격 무적에는 안 띄움 | 등장 낙하 중 자물쇠 → 전투 시작 시 사라짐, 전환 중 자물쇠, P1 화면 밖·피격 깜빡임 중엔 안 뜸 | 그로기 바(T63) | T33 | 15장 | New/BossHealthBar.cs, Gimmick/BossLockIndicator.cs | ✅ |
| T24 | 에디터 | (수정) 최종 통합 플레이: 1P → 그로기 → QTE → 2P(콤보·레이저 벽) → 사망 | 현민 플레이: 전환·사망 후 씬에 벽·충격파·포탄·파편·드론·QTE UI 0개 | — | T21·T23·T67·T20 | 전체 | (현민) | ✅ |
| T68 | 어댑터 | 레거시 정리 ① 흐름 (Q19 C): `useFlowV2` 토글, BT 빌드·Idle/Attacking/Resting 코루틴, 토글 Off 전용 `PhaseTransitionEffect` 경로를 BossController에서 제거, `LegacyPatternBridge` 삭제, Registry 임시 매핑 제거(모든 단위가 v2 패턴인지 확인) | 토글 없이 1P→2P→사망 전 과정이 T24와 같게 동작, 인스펙터에 토글 없음 | 패턴 파일 삭제(T69) | T24 | Q19 | New/BossController.cs, Gimmick/LegacyPatternBridge.cs(삭제), Gimmick/PatternRegistry.cs, Gimmick/BossFlowDriver.cs | ✅ |
| T69 | 어댑터 | 레거시 정리 ② 패턴: `BossAttackPatterns` 의 안 쓰는 코루틴 패턴(Stomp·Burst·Rapid·Howitzer·Cannon·Charge·DroneSpawn)·반복 방지·Animation Event 메서드 제거, BT 파일 삭제. 새 패턴이 쓰는 풀·드론·포탄 지원 코드는 남김 (착수 시 참조 검색으로 확인, `BossContext.cs` 참조 포함) | T68 확인 항목 재확인, Console에 Missing Animation Event 경고 없음 (Animator 클립 이벤트 정리는 에디터 작업으로 HANDOFF에) | — | T68 | Q19 | New/BossAttackPatterns.cs, New/BTNode.cs·BTComposite.cs·BTLeaves.cs(삭제) | ✅ |

> 파일 삭제(T68·T69)는 `.meta` 와 함께 지워야 하므로, 삭제 대상은 HANDOFF "에디터 작업"에 적고 현민이 에디터에서 지운다 (AI_RULES 7장 `.meta` 규칙).

**권장 순서**
T27→T28 → T22 → T29→T30→T31→T32→T33 → T37→T38 → T41→T42(P3) → T43→T44(P2) → T45→T46(P1, T3 선행) → T47·T48·T50 → T60→T70→T61→T62·T63 → T64→T65 → T55→T56→T57 → T14→T15 → T21 → T23
연출·에디터는 각 묶음의 어댑터가 끝난 뒤: (T34·T35→T36) (T39→T40) (T51~T53→T54) (T58→T59) (T66→T67) (T4) (T20) → T24 → T68 → T69. T49는 패링 구현 후.

### C. 질문

#### C-1. v2 질문 확정 (2026-11-25, 현민 — 추천안 그대로)

| Q | 쟁점 | 결정 | 반영 작업 |
|---|---|---|---|
| Q19 | 기존 코루틴 패턴(`BossAttackPatterns`) 교체 / 공존 | **C. 단계 교체** — 새 FSM·선택기·패턴은 UniTask로 새로, 미구현 패턴은 `LegacyPatternBridge` 로 임시 호출, 패턴 완성마다 Registry 매핑 교체, `useFlowV2` 토글로 기존 동작 복귀 가능, 마지막에 레거시 제거 | T31·T38·T68·T69 |
| Q20 | `GameClock` 신규 / 기존 시간 체계 연결 | Core `GameClock`(ARCH 3-1) 신규 + 어댑터가 World 배율을 `CustomTimeManager.EnemyTimeScale` 에서 읽고 `HitStop` 은 `HitstopManager.TriggerHitstop` 에도 위임 → 보스 히트스톱 때 플레이어도 0.05배 슬로우(Q25), 낙섬 QTE 슬로우가 보스에도 걸림 | T27·T28 |
| Q21 | 피해 출처(일섬 / 일반 / 낙섬) 구분 | **A.** `IHitSourceReceiver.OnHitBy(HitSource)` 신설, CSH `PlayerDash`·`PlayerCombat` 에 1~3줄 호출 추가 (**CSH 수정 허용**) | T70·T61·T62·T65 |
| Q22 | 낙섬 QTE 형식 | **A. 보스 전용 타이밍 바** — Groggy 보스가 낙섬(`HitSource.BlueFall`)에 맞는 순간 보스 쪽에서 바 표시. 기존 BlueDash QTE·낙하는 그대로 (CSH QTE 수정 없음) | T64·T65 |
| Q23 | QTE 데미지에 Groggy ×1.5 적용 | **미적용** — Perfect 60 / Normal 30 고정 | T65 |
| Q24 | P4 발사 중 반격 틈 = Punishable(×1.2)? | **아니다** — 발사 중 Attacking(×1.0), 발사 후 Punishable 0초. 6장 "반격 틈"은 "보스가 제자리라 때릴 수 있다"로 해석 | T47 |
| Q25 | `HitstopManager` 겹침 처리 (2026-11-25 추가) | **A. CSH `HitstopManager` 수정 허용** — 기존 코드는 새 요청 시 이전 루틴의 `finally` 가 다음 프레임에 새 요청 CTS를 취소·배율을 복구해 늦게 온 히트스톱이 ~1프레임에 끊길 수 있고, 남은 시간 API가 없어 "긴 쪽 유지"를 보스 쪽에서 판단할 수 없음 → `finally` 자기 CTS만 정리 + `RemainingTime` 공개 + 긴 쪽 유지. 보스 위임 배율 **0.05** (CSH Light 프리셋과 동일, 완전 정지 아님) | T28 |

**기획서에 없는 수치** — 답이 없으면 아래 추천값을 SO 초기값으로 시작하고 작업에 〔임시값〕으로 표시
- C3 마지막 착지 "긴 반격 틈": 1.8초 (P1 틈 1.2 × 1.5)
- 콤보 시작 신호(증기·경고음) 길이: 0.5초, 그 뒤 첫 패턴 예고 시작
- 페이즈 전환 연출 길이: 2초 (기존 `PhaseTransitionEffect` 와 비슷하게)
- 낙섬 QTE: 바 길이 1초, Perfect 구간 0.1초, Normal 구간 0.3초, 감지 반경 = 보스 몸체 콜라이더 + 1 Unit
- 레이저 벽: 수렴 속도 0.5 Unit/초, 틱 피해 5 / 0.5초 (v1 추천값 유지)
- 충격파 수명: 방 경계 도달 시 소멸 (최대 3초)

#### C-2. 기존 질문 상태 (v2 기준)

| Q | 기존 결정 | v2 상태 |
|---|---|---|
| Q1 | `New/Core/` + `New/Gimmick/`, 네임스페이스 `UnMachine.Core.Boss` / `UnMachine.Boss` | 유효 (패턴은 `Gimmick/Pattern/` 하위) |
| Q2 | T1에서 GameLog·IRandom | 유효 (완료) |
| Q3 | Core는 dt 인자, 어댑터만 `Time.deltaTime` | 유효 → GameClock 도입(T28) 후 어댑터도 채널 dt로 |
| Q4 | 무적 = 등장 + 2P 기본 쉴드 | ❌ **무효** — 2P 쉴드 삭제. 무적 = Appearing·PhaseTransition (+ P1 화면 밖은 자물쇠 없이) |
| Q5 | 레이저·판넬이 콤보와 동시에, 앵커는 보스 자식 | 다른 보스로 이동 (부록 참고용) |
| Q6 | 거리 2/8/9 유지 | v2로 대체 — 5 / 20 (결정 #8, T37) |
| Q7 | 2연속 허용, 3번째 교체 | 유효 — v2 11장 반복 방지와 같음 (T37) |
| Q8 | 아재 성공 → 그로기, 실패 → 피해 | ❌ **무효** — 그로기는 게이지 100으로 진입 |
| Q9 | 판넬 파괴 가능·판넬 안 | 다른 보스로 이동 (부록) |
| Q10 | 커맨드 입력 4개·3초·오답 즉시 실패 | ❌ **무효** — 아재 커맨드 삭제 |
| Q11 | 거울 = 아재 상호작용 지점 | ❌ **무효** — 거울 보류 |
| Q12 | 상호작용 키 V | ❌ **무효** |
| Q13 | 명칭 "밟기" | v2로 대체 — P3 "짓밟기" (아트 확인 대기, 17장 #3) |
| Q14 | `Cannon` 은 손대지 않음 | v2에 없음 (2페이즈 "대포" 삭제). Q19 C에 따라 T69에서 제거 |
| Q15 | 성장형 드론 = 커지는 에너지탄 | v2에서 "성장형 드론" 삭제. 기존 드론 코드는 손대지 않음 (P6이 그대로 재사용) |
| Q16 | 기믹 층은 FSM·BT와 별개 | 유효 — 레이저 벽만 해당 (T3) |
| Q17 | CSH `PlayerMove.cs` 잠금 플래그 허용 | 사용 안 함 (T18 폐기). CSH 수정은 Q21 확정으로 `PlayerDash`·`PlayerCombat` 출처 호출만 허용 (T70) |
| Q18 | 그로기 시간·배율·아재 실패 피해·쿨다운 | 해결 — 그로기 4초·×1.5 (4장·7장). 아재 값은 무효 |

---

## 부록: 다른 보스로 이동한 작업 (v1 분해 원문 보존, 2026-11-25)

> boss.md v2 12장: "1페이즈 회전 레이저 + RGB 판넬 → 다른 보스로 이동. 설계는 보존". 대상 보스가 정해지면 그 보스 기획서 기준으로 다시 분해한다. 아래는 v1 기준 원문이며 이 보스에서는 진행하지 않는다.

| ID | 유형 | 작업 | 완료 기준 (씬) | 의존 | 예상 파일 |
|---|---|---|---|---|---|
| T5 | 로직 | `RotatingBeam`(각도 진행: 시작각·속도·방향, dt 입력) + `DamageTickTimer`(대상별 피해 간격) | 빔 각도가 Config 속도대로 회전 (0 → 정지, 음수 → 역회전) | T1 | Core/RotatingBeam.cs, Core/DamageTickTimer.cs |
| T6 | 어댑터 | `RotatingLaserGimmick`: 앵커 2곳에서 빔, `Physics2D.Raycast`(NonAlloc)로 벽까지 길이, 플레이어 틱 피해, LineRenderer. `RotatingLaserConfig` | 빔 2개 회전, 벽에서 끊김, 닿으면 틱 피해, 페이즈 종료·사망 시 사라짐 | T3·T5 | Gimmick/RotatingLaserGimmick.cs, Gimmick/RotatingLaserConfig.cs |
| T7 | 연출 | 레이저 전조(경고선 깜빡임 → 발사), 페이드, 루프 사운드 | 경고 중엔 피해 0 | T6 | Gimmick/RotatingLaserView.cs |
| T8 | 에디터 | 앵커·LineRenderer 2개·Config·레이어 마스크 | 현민 플레이 | T7 | (현민) |
| T9 | 로직 | `PanelWanderPlanner`(경계 안 무작위 목표점, IRandom) + `PanelAttackCycle`(대기→전조→발사→쿨다운) | 목표점이 항상 경계 안, 고정 시드 재현 | T1 | Core/PanelWanderPlanner.cs, Core/PanelAttackCycle.cs |
| T10 | 어댑터 | `BossPanel` + `BossPanelGimmick` + `BossPanelConfig` + `PanelType` — 이동·파괴·재스폰만 | 경계 안 이동, 파괴 → 재스폰, 페이즈 종료 시 전부 사라짐 | T3·T9 | 4파일 |
| T11 | 어댑터 | `IPanelAttack` × 레드(조준 레이저)/블루(8방향 탄)/그린(돌진) | 타입별 공격, 페이즈 종료 시 발사체까지 사라짐 | T10 | 4파일 |
| T12 | 연출 | 판넬 색·전조·피격/파괴 이펙트 | 전조가 Config 시간만큼 보임 | T11 | Gimmick/BossPanelView.cs |
| T13 | 에디터 | 판넬 프리팹 3종, Config | 현민 플레이 | T12 | (현민) |

**판넬 안 (v1 Q9 확정)**

| 타입 | 역할 | 공격 | 추천 초기값 |
|---|---|---|---|
| 레드 A | 조준 압박 | 조준선 추적 → 고정 → 직선 레이저 | 추적 1.2초 / 고정 0.4초 / 발사 0.2초 / 피해 5 / 주기 4초 |
| 블루 B | 공간 봉쇄 | 정지 후 8방향 느린 탄 | 8발 / 탄속 4 / 피해 3 / 주기 5초 |
| 그린 C | 근접 위협 | 전조 깜빡임 → 플레이어 위치로 돌진 | 전조 0.6초 / 속도 12 / 피해 5 / 주기 6초 |
| 공통 | — | 파괴 가능, 일정 시간 뒤 재스폰 | 체력 15 / 재스폰 8초 / 각 1개 |

**v1 추천 수치**: 회전 레이저 45°/s·양쪽 반대 방향·굵기 0.4·틱 피해 3 / 0.3초·전조 1초 / 판넬 이동 속도 3·목표점 최소 거리 4
