# docs/HANDOFF.md — 인수인계 문서

> 작업이 끝날 때마다 **덮어쓴다.** 이전 기록은 git 히스토리에 남는다.
> 다음 사람(또는 AI)이 이 문서 하나만 읽고 바로 이어서 작업할 수 있어야 한다.
> 작성은 Claude 또는 현민 (사람)만 한다. Gemini는 `docs/REVIEW.md` 에 쓴다.
> **커밋은 현민이 직접 한다.** Claude는 "커밋 제안"만 적는다.

---

## 기본 정보
- 작업 제목: PhaseTransition 상태 연결 (v2 흐름)
- BACKLOG 작업 ID: T33
- 기획서 근거: boss.md 5장(PhaseTransition 완전 무적)·8장 전환 1(HP 50% 최초 하회, 1회, Groggy 보류)·17장 #13·#14
- 작성자: Claude
- 작성 일시: 2026-11-26
- 모드: 일반
- 브랜치: `main`
- 기준 커밋: `42cd5dd` (T33 착수 시 기록)
- 커밋 상태: 미커밋 (코드 변경 있음)
- 상태: 🎮 현민 씬 테스트 대기
- 이전 작업: T32 — 커밋됨(`42cd5dd`). 1차 로그 확인(Driver·Locomotion 정상, 예외 0). 걷기 자체는 현민 눈 확인 항목. Gemini 리뷰 전

## 0. 이전 작업 리뷰 범위 (Gemini)
- T32 는 `git diff 4b49e82 42cd5dd` (요청 사항은 `42cd5dd` 의 HANDOFF 9장).
- T31 은 `git diff 82d87de 4b49e82`, T29·T30 은 `git diff ae589ab 82d87de`, T27·T28·T22 는 `git diff d60f4f9 ae589ab`.

## 1. 목표
- v2 흐름(`useFlowV2` On)에서 HP 50% 최초 하회 → `BossFlowMachine.RequestPhaseTransition` (Groggy 면 보류) → PhaseTransition 동안 패턴 중단·발사체 회수·완전 무적 → Config 시간(1.5초) 뒤 2P Idle + `PhaseChanged(2)`.
- 디버그 F1 도 같은 경로. 전환은 1회.
- 범위 밖: 역재생·증기·붉은 틴트·흔들림(T34), 체력바 자물쇠 표시(T21), Animator State 12(T36). 토글 Off 는 레거시 그대로.

## 2. 변경 파일
| 파일 | 계층 | 변경 내용 |
|---|---|---|
| `Assets/LHM/0_Script/Boss/New/BossController.cs` | Runtime | `TakeDamage`·F1: v2 면 레거시 전환 블록 대신 `TryRequestFlowPhaseTransition()` (HP < 50% && 미전환 → Driver 요청, 받아들여졌을 때만 `hasPhaseTransitioned = true`). 새 public `EnterFlowPhaseTransition()`(ForceStopAllPatterns·부스터 끔·피격 깜빡임 해제·`ClearAllActiveProjectiles`·기존 전환 이펙트), `ApplyFlowPhase(int)`(currentPhase 갱신 → `PhaseChanged`). `ComputeInvulnerable` 에 PhaseTransition 추가. 중복이던 전환 이펙트 코드 → `SpawnPhaseTransitionEffect()` (레거시 TakeDamage·F1 도 이걸 호출, 동작 동일) |
| `Assets/LHM/0_Script/Boss/New/Gimmick/BossFlowDriver.cs` | Runtime (+30) | public `RequestPhaseTransition()` (루프 중만, 결과 로그). PhaseTransition 진입 시 `CancelPattern` + `_boss.EnterFlowPhaseTransition()`. 머신 `PhaseChanged` 구독(SetPhase 뒤) → `_boss.ApplyFlowPhase`, `ReleaseMachine` 에서 해제 |
| `Assets/LHM/8_MD/HANDOFF.md`, `BACKLOG.md` | docs | 본 문서, T33 → 🎮 |

- Core(`BossFlowMachine`)는 **수정 없음**: 전환·보류·1회 제한·`PhaseChanged`·`PhaseTransitionSeconds` 는 T30 에 이미 있음.
- 테스트 씬 툴(`BossTestSceneBuilder`)은 **수정 없음**: 새 참조·필드가 없다. T31 메뉴 그대로 쓰면 된다.

## 3. 동작 변화와 리스크
- **토글 Off**: 변화 없음 (전환 이펙트 코드만 helper 로 옮김, 호출 순서 동일).
- **토글 On** 흐름:
  1. 피격으로 HP < 250 → Driver 요청 → 상태 `PhaseTransition` (라벨 `PhaseTransition 1.50s`).
  2. 진입 순간: 패턴 CTS 취소 + `ForceStopAllPatterns`(돌진 관성 포함), 발사체 회수, 부스터 끔, 피격 깜빡임 해제, 기존 화면 이펙트 1회. `InvulnerableChanged → True`.
  3. 전환 중: 피격 무시 (`TakeDamage` 상태 필터에서 제외 — 피해 로그 안 나옴). F3·F4 거절. 걷기 멈춤.
  4. 1.5초 뒤: `PhaseChanged → 2` (Director 등 구독자) → Idle → 다음 패턴. `InvulnerableChanged → False`. 2P Resting 0.8초, 반격 틈 ×0.8.
  - Groggy 중 하회: 요청은 보류(인스펙터 `Debug Phase Transition Pending` ✔) → Groggy 4초 끝난 직후 PhaseTransition.
  - 같은 타격으로 HP ≤ 0: PhaseTransition 진입 후 바로 Dead (Dead 최우선).
- 설계 선택:
  - v2 의 `PhaseChanged` 는 **전환이 끝날 때** 1회 (BACKLOG T33 "Config 시간 뒤 2P Idle + PhaseChanged(2)"). 레거시는 하회 즉시 알림 → v2 에서는 Director 의 2P 기믹이 1.5초 늦게 시작.
  - `currentPhase` 도 전환 끝날 때 2 로 바뀜. `GetCurrentPhase()`(HP 계산)는 하회 즉시 2.
  - 무적은 별도 플래그 없이 상태값 하나로 판정 (`ComputeInvulnerable` 단일 기준 유지).
  - v2 에서는 `StopAllCoroutines` 를 부르지 않음 (레거시 상태 코루틴이 없고, 드론 상승 등 다른 코루틴을 끊지 않게).
- 리스크:
  - **회수 안 되는 탄**: `ClearAllActiveProjectiles` 는 캐논볼·총알·경고 마커만 추적. AirBurst·Bounce 펠릿은 추적 리스트가 없어 남을 수 있음 (레거시도 같음).
  - **체력바 자물쇠 없음**: 표시(T21) 전이라 무적은 Console `[Boss] InvulnerableChanged → True` 와 피해 로그 부재로만 확인.
  - **Animator State 12 없음 (T36)**: `SetInteger("State", 12)` 는 전이가 없어 직전 애니가 유지될 수 있음.
  - F1 은 `damageHandler.SetHP` 를 안 부름 (기존 F1 그대로) → DamageHandler 쪽 HP UI 는 다음 피격 때 맞춰짐.

## 4. 정적 점검 (Claude — 실행 아님)
| 항목 | 결과 |
|---|---|
| 컴파일 가능성 자체 점검 | 문제 없음으로 판단. `BossFlowMachine.RequestPhaseTransition()`·`PhaseChanged`·`IsPhaseTransitionPending`(public, T30). `GameLog.Info/Warn(string, Object)` 시그니처 확인. `SetBoosterActive(bool)`·`CancelHitInvincibility`·`ClearAllActiveProjectiles` 기존 메서드. 새 줄 120자 이하 (PowerShell 글자 수 확인. 899줄 128자는 기존 코드) |
| unity-guard | 문제 없음. 새 코루틴·async 없음. `Time.*` 신규 사용 없음. `UnityEngine.Object` 에 `?.`·`??` 없음 (`attackPatterns != null`). 새 이벤트 구독(`PhaseChanged`)은 `ReleaseMachine` 에서 해제. `Debug.Log` 신규 없음 (`GameLog`). 기존 이펙트 코드의 `fx.GetComponent`·`Camera.main` 은 이동만 (Update 경로 아님, 전환 1회) |
| spec-check | ✅ 8장 "HP 50% 최초 하회, 1회" (`< 0.5`, 정확히 50% 는 1P) / ✅ 8장 "Groggy 중이면 끝난 직후" (T30 머신) / ✅ 8장 1 "진행 중 패턴·발사체 정리" / ✅ 5장·8장 "완전 무적" / ⚠️ 전환 시간 1.5초 기획서 값 없음 (Config 〔임시값〕) / ➖ 8장 2·3 연출 T34, 15장 자물쇠 T21 |

## 5. 에디터 작업 (현민)
**빠른 방법 (테스트 씬 툴, T31 그대로)**
1. Unity Console 컴파일 에러 0 확인 (새 파일 없음 → 새 `.meta` 없음).
2. 메뉴 `UnMachine > Test Scene > Boss Arena (기본값으로 바로 생성)` → 새 `Test_Boss N.unity` → Console `[TestScene] 생성 완료: … v2 흐름 On` 확인.
   - 이미 만든 T31/T32 테스트 씬을 그대로 써도 된다 (추가 연결 없음).
3. (선택) `BossFlowConfig.asset` 인스펙터 페이즈 전환 시간 1.5 확인.

**수동 방법**: T31 수동 연결이 돼 있는 씬이면 추가 작업 없음.

## 5-1. 씬 테스트 절차 (현민)
**준비**: 위 에디터 작업, Game 뷰 + Console, 보스 `BossFlowDriver` 인스펙터 "읽기 전용 표시"(`Debug State`·`Debug Phase`·`Debug Phase Transition Pending`) 보이게.

| # | 조작 | 기대 결과 | 근거 |
|---|---|---|---|
| 1 | 착지 후 루프 중 F6 (HP 250 = 정확히 50%) | 전환 없음, 라벨 `P1` 유지 | 8장 "50% 하회" |
| 2 | 이어서 보스 1타 | 라벨 `PhaseTransition 1.50s` 카운트다운, Console `페이즈 전환 요청 → True`, `PhaseTransition 진입`, `InvulnerableChanged → True`, 기존 화면 이펙트 1회 | 8장 전환 1·2 |
| 3 | 전환 중 계속 공격 | 피해 로그(`[Boss] 피해 …`) 안 나옴, HP 그대로 | 5장 완전 무적 |
| 4 | Howitzer·Machinegun 탄이 떠 있을 때 F1 | 캐논볼·총알·경고 마커 즉시 사라짐, 진행 중 패턴 멈춤 (돌진이면 미끄러짐 없음) | 8장 1 |
| 5 | 1.5초 대기 | `PhaseChanged → 2`, `InvulnerableChanged → False`, 라벨 `P2`, 다음 패턴 Telegraph 시작, 이후 Resting `0.80s` | 8장, 5장 |
| 6 | 2P 에서 계속 공격 / F1 다시 | 전환 다시 안 일어남 (로그 없음) | 8장 1회 |
| 7 | 새 플레이: F4 (Groggy 4초) 중 F1 | `요청 → True (상태 Groggy, 보류 True)`, Groggy 끝까지 유지 → 끝난 직후 PhaseTransition → 2P | 17장 #13 |
| 8 | 새 플레이: Resting 중 F1 | 걷기 즉시 멈춤 → PhaseTransition | T32 연동 |
| 9 | 새 플레이: Telegraph 중 F1 | 패턴 시작 안 하고 PhaseTransition, 끝난 뒤 정상 루프 | 인터럽트 |
| 10 | 전환 중 F3 / F4 | `EnterStunned → False` / `EnterGroggy → False`, 전환 유지 | 5장 우선순위 |
| 11 | 토글 Off 씬(기존 씬)에서 F1 | 기존처럼 즉시 2P 전환 (레거시 동작 그대로) | 회귀 확인 |

**경계 상황**
- HP 251 에서 큰 한 방으로 0 이하 → PhaseTransition 로그 뒤 바로 Dead·사망 연출 (2P 로 가지 않음).
- 전환 직전 피격 깜빡임 중 → 전환 진입 때 깜빡임 멈추고 스프라이트 보임.
- 전환이 끝나는 프레임에 공격 → 피해 들어가도 됨 (Idle).

**Console 에 나오면 안 되는 것**: `InvalidOperationException`(Begin/NotifyAttackEnded 상태 오류), `NullReferenceException`, `[Boss] useFlowV2 가 꺼져 있는데 …`, 전투 중 `루프가 돌지 않을 때 RequestPhaseTransition`, `PhaseChanged → 2` 두 번.

## 5-2. 현민 씬 테스트 결과
- 2026-11-26 1차 (Editor.log 13:34 기준, Claude 로그 확인). 컴파일 성공(`Tundra build success`) → 테스트 씬 툴로 `Test_Boss.unity` 생성(v2 On) → 플레이 1회. 이 씬 보스 maxHP 250 → 경계 125.
  - ✅ #1: F6 → `HP 125/250 (경계) → 계산 페이즈 1` , 전환 없음.
  - ✅ #2: Resting 중 1타(124) → `InvulnerableChanged → True` → `발사체 회수` → `PhaseTransition 진입` → `요청 → True (보류 False)`.
  - ✅ #5: `PhaseChanged → 2` 1회 → `InvulnerableChanged → False` → 다음 패턴(MachinegunBurst) 시작. 알림 → 루프 순서 정상. (1.5초·Resting 0.8초 길이는 로그로 확인 불가)
  - ✅ #6: 2P 에서 F1 2회 → 전환·`PhaseChanged` 재발생 없음.
  - ✅ 2P 에서 F4 Groggy 진입·Howitzer 중단, Punishable ×1.2 피해 정상.
  - ✅ 예외 0건 (`InvalidOperationException`·`NullReferenceException` 없음).
  - ⚠️ Animator 경고 `Parameter 'State'/'stopHowitzer'/'AttackStart' does not exist` 39건 — T32 플레이(42건)·그 이전에도 있던 테스트 씬 보스 Animator 문제. T33 원인 아님 (T36 Animator 작업 때 확인).
  - ➖ #3 전환 중 공격: 그 사이 피해 로그 0건이지만 실제로 공격했는지 로그로 모름 → 현민 확인.
  - ➖ #4(탄 떠 있을 때 전환)·#7(Groggy 중 하회 보류)·#8~#11·경계 상황 이번 회차 없음.

## 6. 기획서와 다르게 한 점
| 기획서 / BACKLOG | 구현 | 이유 |
|---|---|---|
| (명시 없음) 전환 시간 | 1.5초 (`BossFlowConfig`, T30 〔임시값〕) | 연출(T34) 길이에 맞춰 튜닝 |
| 레거시: 하회 즉시 `PhaseChanged` | v2: 전환 끝날 때 `PhaseChanged` | BACKLOG T33, 8장 4 "→ 2페이즈 루프" 순서 |

## 7. 결정 사항
- v2 전환 정리(패턴·탄·부스터·깜빡임)는 BossController `EnterFlowPhaseTransition` 한 곳, 호출은 Driver 의 PhaseTransition 진입 한 곳.
- 무적 판정은 상태값(`PhaseTransition`)만으로 — 별도 플래그 없음.

## 8. 알려진 문제 / 미완료
- 전환 연출 = T34, 자물쇠 = T21, Animator 8~12 = T36, 걷기 애니 = T54, 선택기 = T38, 벽 충돌 Stunned = T44.

## 9. 리뷰어에게 요청
- `TryRequestFlowPhaseTransition` 이 `hasPhaseTransitioned` 를 요청 수락 시에만 세우는 것 (거절 시 재시도) 이 맞는지.
- PhaseTransition 진입 시 Driver 의 `CancelPattern` → `EnterFlowPhaseTransition` → (같은 타격) `ChangeState(Dead)` 순서.
- `PhaseChanged` 구독을 `SetPhase` 뒤에 두어 시작 페이즈를 다시 알리지 않는 방식.
- `SpawnPhaseTransitionEffect` 추출로 레거시 동작이 바뀌지 않았는지.
- (0장) T32 이전 범위.

## 9-1. 규칙 변경 제안 (현민 판단, T31 에서 이어짐)
- AI_RULES 0장 2번 예외에 추가: "테스트 씬 툴은 필요한 **Config SO 가 프로젝트에 없을 때만** 기본값으로 **새 `.asset`** 을 만들 수 있다 (기존 에셋 수정·덮어쓰기 금지)."
- 작업 절차에 추가: "씬 테스트가 필요한 작업은 `BossTestSceneBuilder`(테스트 씬 툴)도 함께 갱신해, 메뉴 한 번으로 그 작업을 테스트할 수 있는 새 Test_* 씬이 나오게 한다." (현민 요청 2026-11-26)

## 10. 질문 (현민 답변 필요)
- (T33) AirBurst·Bounce 펠릿이 전환 때 남으면 추적·회수를 추가할지 (기본: 그대로, 보이면 별도 작업).
- (T33) 2P 기믹 시작(`PhaseChanged`)이 전환 끝(1.5초 뒤)이어도 괜찮은지 (기본: 끝날 때).
- (T32) 정지 거리 1.5 유지? 기획서 4장 표에 넣을지 (기본: Config 1.5 유지).
- (T32) 2P Resting 이동도 걷기(속도 3)로 충분한지 (기본: 걷기).
- (T22) 체력바 색상 경계도 `>= 0.5f` 로 맞출지 (기본: 그대로).
- (T28) Player 채널 동기화 유지 여부 (기본 유지).
- (T28) `GameClockDriver` 의 `Time.unscaledDeltaTime` 상한 여부 (기본: 그대로).
- (T31) 패턴이 Stunned·Groggy 로 끊길 때 이미 쏜 발사체를 남길지 지울지 (기본: 그대로).

## 11. 다음 할 일
1. 현민: 컴파일 확인 → 에디터 작업 → 씬 테스트 (5-1) → 결과 알려주기 → 커밋 → Gemini 리뷰
2. 다음 작업: T37 (BACKLOG 순서) 또는 T34 (전환 연출) — 새 세션 권장

## 11-1. 커밋 제안 (현민이 판단해서 직접 커밋)
- `[feat] T33 PhaseTransition 상태 연결: 50% 하회 전환 요청·패턴/탄 정리·전환 무적` —
  `Assets/LHM/0_Script/Boss/New/BossController.cs`,
  `Assets/LHM/0_Script/Boss/New/Gimmick/BossFlowDriver.cs`,
  `Assets/LHM/8_MD/HANDOFF.md`, `Assets/LHM/8_MD/BACKLOG.md`
- 새 파일 없음 → 새 `.meta` 없음.
