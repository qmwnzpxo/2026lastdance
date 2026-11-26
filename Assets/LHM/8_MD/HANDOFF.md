# docs/HANDOFF.md — 인수인계 문서

> 작업이 끝날 때마다 **덮어쓴다.** 이전 기록은 git 히스토리에 남는다.
> 다음 사람(또는 AI)이 이 문서 하나만 읽고 바로 이어서 작업할 수 있어야 한다.
> 작성은 Claude 또는 현민 (사람)만 한다. Gemini는 `docs/REVIEW.md` 에 쓴다.
> **커밋은 현민이 직접 한다.** Claude는 "커밋 제안"만 적는다.

---

## 기본 정보
- 작업 제목: Resting 걷기 (`BossLocomotion`)
- BACKLOG 작업 ID: T32
- 기획서 근거: boss.md 4장(걷는 속도 3)·5장(Resting "플레이어 쪽으로 걷기")
- 작성자: Claude
- 작성 일시: 2026-11-26
- 모드: 일반
- 브랜치: `main`
- 기준 커밋: `4b49e82` (T32 착수 시 기록)
- 커밋 상태: 미커밋 (코드 변경 있음)
- 상태: 🎮 현민 씬 테스트 대기
- 이전 작업: T31 — 커밋됨(`c343e0e`, `4b49e82`). 씬 테스트 5-1 #1·#2·#4~#11 통과, #3·#12 생략 가능(현민 판단). Gemini 리뷰 전

## 0. 이전 작업 리뷰 범위 (Gemini)
- T31 은 `git diff 82d87de 4b49e82` (요청 사항은 `4b49e82` 의 HANDOFF 9장).
- T29·T30 은 `git diff ae589ab 82d87de`, T27·T28·T22 는 `git diff d60f4f9 ae589ab`.

## 1. 목표
- v2 흐름(`useFlowV2` On)의 Resting 동안 보스가 플레이어 쪽으로 걷고, x 거리 1.5 Unit 이내면 멈춘다.
- 속도는 World 채널 배율을 따른다 → F2 히트스톱 중 멈춤. 걷기 값은 `BossFlowConfig`.
- 범위 밖: 걷기 애니메이션(runboost 0.5배속, T54), 부스터 이펙트·사운드, 토글 Off(레거시 RestingState 그대로).

## 2. 변경 파일
| 파일 | 계층 | 변경 내용 |
|---|---|---|
| `Assets/LHM/0_Script/Boss/New/Gimmick/BossLocomotion.cs` | Runtime (신규, 일반 C# 클래스) | `Begin()`(목표 x 속도 0 으로 제어 시작), `Tick(worldRate)`(Update: 플레이어 x 거리 → 목표 x 속도), `ApplyFixed()`(FixedUpdate: `linearVelocity.x` 반영, y 유지), `Stop()`(x 속도 즉시 0), 순수 함수 `ComputeWalkVelocityX(distanceX, speed, stopDistance)`. 디버그용 `IsActive`·`TargetVelocityX`·`DistanceX` |
| `Assets/LHM/0_Script/Boss/New/Gimmick/BossFlowDriver.cs` | Runtime (+60) | Awake 에서 보스의 `Rigidbody2D` 를 `TryGetComponent` → `BossLocomotion` 생성 (없으면 Error + 비활성화). Resting 진입 `Begin`, Resting 이탈·`StartLoop`·`OnDisable` 에서 `Stop`. Update `Tick(GetWorldRate())`, 새 `FixedUpdate` 에서 `ApplyFixed`. 읽기 전용 표시 3개 + 머리 위 라벨에 `Walk vx … dx …` 줄 (Resting 중만) |
| `Assets/LHM/0_Script/Boss/New/Gimmick/BossFlowConfig.cs` | Runtime (+13) | "휴식 걷기" 섹션: `_restWalkSpeed = 3`, `_restWalkStopDistance = 1.5` + 읽기 프로퍼티 |
| `Assets/LHM/8_MD/HANDOFF.md`, `BACKLOG.md` | docs | 본 문서, T32 → 🎮 |

- 테스트 씬 툴(`BossTestSceneBuilder`)은 **수정 없음**: 새로 연결할 참조가 없다 (Rigidbody2D 는 런타임에 같은 오브젝트에서 찾고, Config 새 필드는 기본값). T31 메뉴 그대로 쓰면 된다.

## 3. 동작 변화와 리스크
- **토글 Off**: 변화 없음 (Driver 가 루프를 안 돌림).
- **토글 On**: Resting(1P 1.5초 / 2P 0.8초) 동안
  - 플레이어와 x 거리 > 1.5 → 플레이어 쪽으로 x 속도 `3 × World 배율`. ≤ 1.5 → x 속도 0. 뒤로는 안 감 → 경계에서 떨지 않음.
  - 방향 전환은 기존 `BossController.Update → FacePlayer()` 가 매 프레임 처리 (변경 없음).
  - Resting 진입 첫 FixedUpdate 에서 패턴이 남긴 x 속도도 0 이 됨 (T31 리스크 "Resting 중 미끄러짐" 해소 기대).
  - Resting 을 벗어나면(Idle·Stunned·Groggy·Dead) 즉시 x 속도 0. 2P 전환(`StartLoop(2)`)·Driver 비활성화 때도 0.
- 설계 선택:
  - World 반영 방식: 속도 × `GetWorldRate()` (= 히트스톱 중 0, 평소 `GameClock.World.Scale`). 물리가 위치를 적분하므로 World dt 로 직접 위치를 옮기지 않고 같은 비율을 속도에 곱함. `Time.*` 직접 사용 없음.
  - 속도 계산은 Update(이번 프레임 World 값), Rigidbody 반영은 FixedUpdate (AI_RULES 2장). `Stop()` 만 상태 전환 순간 즉시 0 으로 설정.
  - `BossLocomotion` 을 MonoBehaviour 가 아닌 일반 클래스로 → 에디터 연결 0개.
- 리스크:
  - **걷기 애니메이션 없음 (T54)**: Idle 포즈로 미끄러지듯 이동. 레거시처럼 부스터 이펙트·사운드도 안 켬.
  - **히트스톱 중 방향 전환**: `FacePlayer` 는 기존 코드라 히트스톱 중에도 뒤집힘 (이동만 멈춤).
  - **World 배율 + 일시정지 이중 적용**: `World.Scale` 에 `Time.timeScale` 이 곱해져 있고 물리도 `timeScale` 을 따름. 지금은 timeScale 이 0/1 만 쓰여 문제 없음. 0.5 같은 값이 생기면 이중으로 느려짐.
  - **플레이어 참조 없음**: `GetPlayer()` 가 null 이면 제자리 (에러 로그 없음).
  - 기존 `BossFlowConfig.asset` 에는 새 필드가 없음 → Unity 가 기본값(3, 1.5)으로 채움. 인스펙터에서 보이는지 확인.

## 4. 정적 점검 (Claude — 실행 아님)
| 항목 | 결과 |
|---|---|
| 컴파일 가능성 자체 점검 | 문제 없음으로 판단. `Component.TryGetComponent<T>(out T)`·`Rigidbody2D.linearVelocity`·`.position`(Unity 6), `BossController.GetPlayer()`(public, 기존). 네임스페이스 `UnMachine.Boss` 동일, `GameClock`·`ClockChannel.Scale`·`IsHitStopped` public. 새 직렬화 필드는 추가만 (이름 변경 없음). 3파일 모두 줄 120자 이하 (PowerShell 글자 수 확인) |
| unity-guard | 문제 없음. `Time.deltaTime`·`Time.*` 없음 (World 채널). 물리 속도는 FixedUpdate (Stop 의 즉시 0 만 예외 — 상태 전환 1회). `Update` 안 `GetComponent`·할당 없음 (`TryGetComponent` 는 Awake). `UnityEngine.Object` 에 `?.`·`??` 없음 (`player == null` 비교). 새 코루틴·async 없음. 매직 넘버 없음 (Config). 에디터 라벨 문자열은 `#if UNITY_EDITOR` + `guard-allow` |
| spec-check | ✅ 4장 걷는 속도 3 / ✅ 5장 Resting 1.5·0.8초 동안 플레이어 쪽으로 걷기 / ⚠️ 정지 거리 1.5 는 기획서 값 없음 (BACKLOG T32·기존 RestingState 값, 〔임시값〕 툴팁) / ➖ 걷기 애니(13장 #5 runboost 0.5배속) T54 |

## 5. 에디터 작업 (현민)
**빠른 방법 (테스트 씬 툴, T31 그대로)**
1. Unity Console 컴파일 에러 0 확인. 새 `.meta` 1개 생성 확인 (`BossLocomotion.cs.meta`).
2. 메뉴 `UnMachine > Test Scene > Boss Arena (기본값으로 바로 생성)` → 새 `Test_Boss N.unity` 생성 → Console `[TestScene] 생성 완료: … v2 흐름 On` 확인.
   - 이미 만든 T31 테스트 씬(`Test_Boss 1.unity` 등)을 그대로 써도 된다 (추가 연결 없음).
3. `Assets/LHM/3_ScriptableObject/BossFlowConfig.asset` 인스펙터에 "휴식 걷기" 섹션(걷기 속도 3, 정지 거리 1.5)이 보이는지 확인.

**수동 방법**: T31 수동 연결이 돼 있는 씬이면 추가 작업 없음.

## 5-1. 씬 테스트 절차 (현민)
**준비**: 위 에디터 작업, Game 뷰 + Console, 보스 `BossFlowDriver` 인스펙터 "읽기 전용 표시"(`Debug Walk Active`·`Debug Walk Velocity X`·`Debug Player Distance X`) 보이게.

| # | 조작 | 기대 결과 | 근거 |
|---|---|---|---|
| 1 | 플레이 → 착지 후 첫 Resting 까지 대기, 플레이어는 보스에서 멀리(> 3 Unit) 서 있기 | 라벨 `Resting 1.50s` 아래 `Walk vx ±3.0 dx …`, 보스가 플레이어 쪽으로 걸어옴 | 4장 걷기 3, 5장 |
| 2 | 보스 가까이(1.5 Unit 이내) 서 있기 | `Walk vx 0.0`, 보스 멈춤. **좌우로 떨지 않음** | BACKLOG T32 |
| 3 | Resting 중 플레이어가 보스 뒤로 넘어가기 | 보스가 돌아서서(FacePlayer) 반대쪽으로 걸음, vx 부호 바뀜 | 방향 전환 |
| 4 | Resting 끝 → Telegraph | 보스 즉시 멈춤, 라벨에서 `Walk` 줄 사라짐 | 5장 |
| 5 | Resting 중 F2 (히트스톱 0.5초) | 히트스톱 동안 `Walk vx 0.0`·보스 멈춤, 끝나면 다시 걸음. 라벨 남은 시간도 멈춤 | T32 완료 기준 |
| 6 | Resting 중 F3 / F4 | Stunned / Groggy 로 바뀌며 즉시 멈춤 (미끄러짐 없음) | 5장 인터럽트 |
| 7 | Charge(돌진) 끝난 직후 Resting | 돌진 관성으로 미끄러지지 않고 걷기만 함 | T31 리스크 |
| 8 | HP 50% 하회 (F6 후 1타) | 2P 루프 시작 후 Resting `0.80s` 동안 같은 걷기 | 8장 |
| 9 | Resting 중 보스 사망 | 즉시 멈추고 기존 사망 연출 | 5장 Dead |
| 10 | `BossFlowConfig` 걷기 속도 6 / 정지 거리 4 로 바꾸고 플레이 (플레이 전에 — Awake 에서 읽음) | 속도·멈추는 거리가 바뀜 | Config 튜닝 |

**경계 상황**
- 보스와 플레이어 x 거리가 정확히 1.5 근처에서 플레이어가 조금씩 움직이기 → 보스가 걷다/멈추다 하지만 뒤로 가거나 떨지 않음.
- 플레이어가 보스 위로 점프해 넘어가기 → 거리 1.5 이내 구간은 멈춤, 넘어간 뒤 반대로 걸음.
- 벽 끝에 붙은 플레이어 → 보스가 1.5 앞에서 멈춤 (벽 통과 없음).

**Console 에 나오면 안 되는 것**: `[BossFlowDriver] 보스 오브젝트에 Rigidbody2D 없음`, `NullReferenceException`, `ArgumentOutOfRangeException`(걷기 값 음수), T31 예외들(`InvalidOperationException` 등).

## 5-2. 현민 씬 테스트 결과
- 2026-11-26 1차 (Editor.log 13:19 기준, Claude 로그 확인). T32 컴파일 성공(`Tundra build success`) 후 플레이 1회, `BossLocomotion.cs.meta` 생성됨.
  - ✅ Driver 준비 정상: `루프 시작 (페이즈 1)`, `Rigidbody2D 없음` 에러 없음 → Locomotion 생성됨.
  - ✅ Resting 진입 8회 (패턴 4종 뒤 + Groggy·Stunned 뒤). Resting 중 피격 `× 1.00`.
  - ✅ #6 일부: Resting 중 F3 → Stunned → Resting. Punishable(Stomp) 중 F4 → Groggy, Howitzer 중 F3 → Stunned. 예외 0건.
  - ➖ **걷기 자체는 로그로 확인 불가** (걷기 로그 없음) → #1·#2·#3·#4·#7 **현민 눈으로 확인 필요**.
  - ➖ #5: F2 를 Howitzer(Attacking) 중에 눌러 Resting 히트스톱 미확인. ➖ #8(2P)·#9(사망)·#10(Config) 이번 회차 없음.

## 6. 기획서와 다르게 한 점
| 기획서 / BACKLOG | 구현 | 이유 |
|---|---|---|
| (명시 없음) 정지 거리 | 1.5 Unit (Config, 〔임시값〕) | BACKLOG T32 값 = 기존 RestingState 값 |
| 레거시 Resting: 2P 는 부스터 비행 애니·이펙트 | 이동만 (애니·이펙트 없음) | 걷기 애니는 T54, 2P 이동 연출은 기획서에 없음 |

## 7. 결정 사항
- 걷기 속도는 World 배율을 속도에 곱하는 방식 (위치 직접 이동 아님) — Rigidbody2D 중력·충돌 유지.
- `BossLocomotion` 은 Driver 가 소유하는 일반 클래스 (에디터 연결 없음).

## 8. 알려진 문제 / 미완료
- 걷기 애니 = T54, PhaseTransition 상태·무적 = T33, 전환 연출 = T34, Animator 8~12 = T36, 선택기 = T38, 벽 충돌 Stunned = T44.

## 9. 리뷰어에게 요청
- `OnStateChanged` 에서 `StopWalking`(previous == Resting) 위치가 Dead 조기 반환보다 앞인지, 중첩 전이(Resting → Idle → Telegraph)에서 순서 문제 없는지.
- Update 에서 목표 속도 계산 / FixedUpdate 에서 반영으로 나눈 방식, `Stop()` 의 즉시 반영.
- `GetWorldRate()` (히트스톱 0 / `World.Scale`) 가 "World dt 사용" 요구에 맞는지, 일시정지 이중 적용 리스크.
- (0장) T31 이전 범위.

## 9-1. 규칙 변경 제안 (현민 판단, T31 에서 이어짐)
- AI_RULES 0장 2번 예외에 추가: "테스트 씬 툴은 필요한 **Config SO 가 프로젝트에 없을 때만** 기본값으로 **새 `.asset`** 을 만들 수 있다 (기존 에셋 수정·덮어쓰기 금지)."
- 작업 절차에 추가: "씬 테스트가 필요한 작업은 `BossTestSceneBuilder`(테스트 씬 툴)도 함께 갱신해, 메뉴 한 번으로 그 작업을 테스트할 수 있는 새 Test_* 씬이 나오게 한다." (현민 요청 2026-11-26)

## 10. 질문 (현민 답변 필요)
- (T32) 정지 거리 1.5 유지? 기획서 4장 표에 넣을지 (기본: Config 1.5 유지).
- (T32) 2P Resting 이동도 걷기(속도 3)로 충분한지, 레거시처럼 빠른 부스터 이동이 필요한지 (기본: 걷기, 기획서 5장 기준).
- (T22) 체력바 색상 경계도 `>= 0.5f` 로 맞출지 (기본: 그대로).
- (T28) Player 채널 동기화 유지 여부 (기본 유지).
- (T28) `GameClockDriver` 의 `Time.unscaledDeltaTime` 상한 없음 → 프레임 정지 한 번에 FSM 타이머가 건너뛰어질 수 있음. 상한을 둘지 (기본: 그대로, 승인 시 `[경량]` 1줄).
- (T31) 패턴이 Stunned·Groggy 로 끊길 때 이미 쏜 발사체를 남길지 지울지 (기본: 그대로).

## 11. 다음 할 일
1. 현민: 컴파일 확인 → 에디터 작업 → 씬 테스트 (5-1) → 결과 알려주기 → 커밋 → Gemini 리뷰
2. 다음 작업: T33 (PhaseTransition 상태·무적) — 새 세션 권장

## 11-1. 커밋 제안 (현민이 판단해서 직접 커밋)
- `[feat] T32 Resting 걷기: BossLocomotion, BossFlowConfig 걷기 값` —
  `Assets/LHM/0_Script/Boss/New/Gimmick/BossLocomotion.cs` (**`.meta` 함께 커밋**),
  `Assets/LHM/0_Script/Boss/New/Gimmick/BossFlowDriver.cs`,
  `Assets/LHM/0_Script/Boss/New/Gimmick/BossFlowConfig.cs`,
  `Assets/LHM/8_MD/HANDOFF.md`, `Assets/LHM/8_MD/BACKLOG.md`
- `BossFlowConfig.asset` 은 인스펙터에서 값을 바꿔 저장했을 때만 `[tune]` 으로 별도 커밋.
