# docs/ARCHITECTURE.md — UnMachine 코드 구조

> 기획서(`docs/spec/*.md`)의 시스템을 코드로 어떻게 나누는지 정한 문서다. 규칙은 `AI_RULES.md`.
> 이 문서의 구조를 바꾸는 결정은 `docs/DECISIONS.md` 에 기록하고, 문서는 현민 승인 후 Claude가 갱신한다.
> 클래스 이름은 **제안**이다. 이미 있는 코드(예: `BossController`, `BossAttackPatterns`, `BossBT`)는 그 이름을 유지한다.
> 각 시스템 끝의 **씬 확인 포인트**는 현민이 씬 테스트에서 반드시 확인할 동작이다. Claude는 이것이 눈으로 보이게 디버그 표시를 준비한다 (`docs/SCENE_TEST.md`).

---

## 1. 현재 구조 (현민이 채움)

> 실제 `Assets/` 구조가 2장과 다르면 여기에 적는다. AI는 이 칸을 2장보다 우선한다.

- 스크립트 루트: `Assets/_Project/Scripts/` (예정)
- 이미 있는 시스템: 보스(FSM+BT), 대화·알림(UniTask), 분기 대화(SO 노드 그래프), 오브젝트 풀
- 기존 코드 중 규칙과 다른 부분(코루틴 등): 요청 없이 바꾸지 않는다

---

## 2. 계층과 의존 방향

```
┌──────────────────────────────────────────────────────────┐
│ Runtime (MonoBehaviour 어댑터)  UnMachine.Runtime.asmdef  │
│  Player / Enemy / Boss / Room / Supply / Cinematic / UI   │
│  - 입력 수집, 물리 쿼리, Animator·VFX·사운드 반영         │
└───────────────▲──────────────────────────────────────────┘
                │ 참조 (한 방향)
┌───────────────┴──────────────────────────────────────────┐
│ Core (순수 C#)                  UnMachine.Core.asmdef     │
│  Time / Random / Combat / StateMachine / BT / Stats /     │
│  Drop / Wave 흐름 계산 / 보스 콤보 선택                   │
│  - MonoBehaviour·Transform·Physics2D·Time 참조 금지       │
└──────────────────────────────────────────────────────────┘
Editor → Runtime + Core   (자동 테스트 어셈블리는 현민이 요청할 때만 추가)
```

### 2-1. 어셈블리

| asmdef | 참조 | 비고 |
|---|---|---|
| `UnMachine.Core` | (없음, UniTask만 허용) | 게임 규칙 로직 전부 |
| `UnMachine.Runtime` | Core, Unity.InputSystem, Unity.Cinemachine, UniTask | MonoBehaviour |
| `UnMachine.Editor` | Runtime, Core | Editor 플랫폼만 |

### 2-2. 설정 데이터 (ScriptableObject)

| SO | 기획서 출처 | 주요 필드 |
|---|---|---|
| `PlayerConfig` | player.md 기본 사양 | 체력 100, 스태미나 100, 회복 10/s, 검게이지 50·획득 5, 걷기 10, 달리기 20, 공격력 3, 공격 속도, 점프 5, 공격 범위 2, 일섬 범위 10, 피격 무적, 선입력 0.15초 |
| `StatAllocationConfig` | player.md 스탯 분배 표 | 총 10Pt, 단일 스탯 최대 3Pt, 스탯별 1Pt 상승치·MAX |
| `SkillConfig` (스킬별) | skills.md | 일섬(게이지 ≥10, 무적 12F, 거리), 프로 모션(진입 비용, 슬로우 0.1, 기록 간격 0.05s), 낙섬 QTE 판정, 연섬 3타 |
| `EnemyConfig` (타입별 A~D) | enemy.md | 시야 각·거리, 감지 게이지 속도, 사거리, 경보 반경 |
| `CombatRulesConfig` | enemy.md 공격 슬롯 | 최대 공격 슬롯 수 |
| `BossConfig` | boss.md | 체력 500, 이동 3, 부스터 10, 공격력 3, 페이즈 기준 50%, 휴식 2/1초, 콤보 거리 5/30, 연속 허용 2회, 드론 제한 |
| `BossPatternConfig` (패턴별) | boss.md 7장 | 데미지, 시전 시간, 범위, 판정 박스 |
| `DropTableConfig` | supply.md | 포션 55 / 섬광탄 30 / 칩 10 / 코인 5 %, 상자 HP 3, 크기별 수량 1/2/3 |
| `RoomWaveConfig` (룸별) | level.md | 웨이브 목록(스폰 지점·적 타입), 개방 대기 1.5초 |
| `LifeCoinConfig` | player.md 라이프 코인 | 초기 3코인 |

> 기획서 수치는 모두 "임시"다. SO 필드 초기값 = 기획서 값, 실제 튜닝은 `.asset` 에서 현민이 한다.

### 2-3. 공용 서비스 (씬마다 하나)

| 서비스 | 역할 | 접근 방식 |
|---|---|---|
| `GameClock` | 시간 채널 (3-1) | 정적 접근 허용 (Core 타입, 디버그용 배율 조작 가능) |
| `IRandom` | 시드 고정 난수 | 생성자 주입. 런타임 기본 구현 `SystemRandom(seed)` |
| `PoolRegistry` | 풀 보관 (3-9) | 씬의 `GameplayRoot` 가 생성·주입 |
| `AlertNetwork` | 적 경보 공유 (3-6) | `GameplayRoot` 가 주입 |
| `AttackSlotManager` | 공격 슬롯 (3-6) | `GameplayRoot` 가 주입 |

- 씬 하나(= 스테이지 하나)에 `GameplayRoot` 하나가 서비스를 만들고 하위 시스템에 넘긴다. 전역 싱글톤을 새로 만들지 않는다.

---

## 3. 시스템별 설계

### 3-1. 시간 — `GameClock` (Core)

- **근거:** 프로 모션(세계·유령 0.1배속 → 본체만 고속), 히트스톱, 무적 프레임, 일시정지
- **구조:** 채널 `World`, `Player`, `Ghost`, `UI`. 각 채널은 `Scale` 을 가지며 `DeltaTime = 실제 dt × 전역 일시정지 × 채널 Scale × 히트스톱`
  - 프로 모션 계획 단계: World 0.1, Ghost 0.1 / 실행 단계: World 0.1, Player 고속
  - 히트스톱: `GameClock.HitStop(seconds)` 동안 World·Player 0
  - 일시정지만 `Time.timeScale = 0` (UI 채널은 `unscaledDeltaTime` 기반)
- **어댑터 책임:** 각 MonoBehaviour는 자기 채널 dt를 쓰고, Animator는 `animator.speed = 채널 Scale` 로 맞춘다. Rigidbody2D는 채널 Scale을 곱한 속도를 직접 넣는다(3-4).
- **씬 확인 포인트:** 채널별 배율 곱, 히트스톱 중첩(긴 쪽 유지), 일시정지 시 UI만 진행

### 3-2. 플레이어 상태 — 우선순위 상태 머신 (Core + Runtime)

- **근거:** 사망 > 피격 > 회피(무적 이동) > 공격·스킬 > 이동·대기, 선입력 0.15초
- **구조:**
  - `PlayerStateMachine` (Core): 상태마다 `Priority`, `CanBeInterruptedBy(priority)`. 강제 전이(사망·피격)는 우선순위로 즉시, 일반 전이는 현재 상태가 끝나거나 캔슬 가능 구간일 때
  - 상태: `Idle`, `Walk`, `Run`, `Jump/Fall`, `WallRun`, `Attack(1~3타)`, `Skill(일섬/낙섬/연섬/프로모션)`, `GravityShift`(0.3초), `Hit`, `Dead`
  - `InputBuffer` (Core): 명령 + 입력 시각 저장, 0.15초 지나면 폐기, 상태가 받을 수 있을 때 꺼냄
  - `PlayerController` (Runtime): Input System 액션 → `InputBuffer`, 상태 결과 → 이동·애니메이션
- **씬 확인 포인트:** 공격 중 피격 → 즉시 Hit, 일섬은 공격·이동을 캔슬, 사망은 모든 상태를 무시, 0.14초 전 입력은 실행 / 0.16초 전 입력은 폐기

### 3-3. 자원·스탯 (Core)

- `PlayerStats`: 기본값(`PlayerConfig`) + 분배 포인트(`StatAllocation`) → 최종 수치. 체력·스태미나·검게이지는 `Resource`(현재/최대, 회복, 소모) 클래스
- `StatAllocation`: 10Pt, 단일 3Pt 제한, [설정 완료] 전까지는 임시 값 (`Draft` → `Commit`)
- `LifeCoinWallet`: 초기 3, 사망 시 1 소모 → 0이면 `ChapterRestart` 이벤트
- 스태미나 회복은 **대기·걷기 상태에서만** (상태 머신이 회복 허용 여부를 알려줌)
- **씬 확인 포인트:** 3Pt 초과 투자 거부, 총 10Pt 초과 거부, 취소 시 원복, 코인 0에서 사망 → 챕터 재시작

### 3-4. 이동·중력 전환 (Runtime 중심, 계산은 Core)

- **근거:** 중력 발판 접촉 + 점프/일섬 → 중력 벡터를 발판 회전값으로 변경, 스프라이트 180° 반전, 카메라 고정
- **구조:** `Rigidbody2D.gravityScale = 0`, `GravityDirection` (Core 값 객체)으로 매 `FixedUpdate` 직접 중력 가속. 점프·낙하·벽 판정은 "현재 중력 기준 아래"로 계산
- 벽 타기: 벽 접촉 + 점프/일섬 → `WallRun` 상태, 스태미나 지속 소모, 다시 점프/일섬 시 해제
- 바닥·벽 판정은 `Physics2D.BoxCast` + `ContactFilter2D` (NonAlloc)
- **씬 확인 포인트:** 중력 방향별 "아래" 벡터와 점프 방향, 스태미나 0에서 벽 타기 해제

### 3-5. 전투 공통 (Core + Runtime)

- `DamageInfo` (struct): 양, 출처, 방향, 타입(근접/투사체/범위)
- `Hitbox` / `Hurtbox` (Runtime): 트리거 콜라이더. 판정 결과는 Core의 `Health` + `Invincibility` 로
- 패링: 플레이어 기본 공격 Hitbox ↔ 적 투사체 → 투사체 `Parry()`
- 일섬: 점멸 이동(충돌 무시) + 경로 `BoxCast` 로 경로상 모든 적 판정, 무적 12F
- 칩 모드: `IChipMode` 전략 (분홍=일섬, 빨강=프로모션, 파랑=낙섬, 초록=연섬). R키로 보유 칩 순환, 챕터 이동 시 초기화
- **씬 확인 포인트:** 무적 중 피해 무시, 패링 조건, 칩 순환 순서·보유하지 않은 칩 건너뛰기, 챕터 초기화

### 3-6. 일반 적 AI — 코드 Behavior Tree (Core + Runtime)

- **근거:** enemy.md BT 구조 (피격·사망 > 위험 회피 > 공격 > 추격·포위 > 수색·복귀 > 순찰), 타입 A~D
- **구조:**
  - BT 노드 (Core, 보스와 공용): `Selector`, `Sequence`, `Condition`, `Action`, `Wait`. 상태 `Running/Success/Failure`
  - `EnemyBlackboard`: 플레이어 위치, 마지막 목격 위치, 경보 수신 여부, 슬롯 보유 여부, 쿨타임
  - `Perception` (Runtime): 부채꼴 시야 → 감지 게이지 (Core `DetectionGauge`)
  - `AlertNetwork` (Core): 순찰병(D) 게이지 100% → 반경 내 적에게 플레이어 좌표 전달
  - `AttackSlotManager` (Core): 최대 슬롯, `TryAcquire/Release`. **피격·사망·풀 반환 시 반드시 Release**
  - 지형 예외: 낭떠러지·벽 감지 시 정지·회전 (Runtime 레이캐스트)
- **씬 확인 포인트:** 슬롯이 가득 차면 공격 대신 포위 대기, 피격 시 슬롯 반환, 게이지 100% 전 이탈 시 하락, 경보 반경 밖 적은 무시, BT 우선순위 순서

### 3-7. 보스 — FSM + BT + 패턴 (기존 구조 유지)

- **근거:** `docs/spec/boss.md` 전체
- **구조 (이미 정해진 3분할):**
  - `BossController` (FSM): Appearing → Idle → Attacking → Resting → … / BoosterJump, Walk, Hit, Dead. 페이즈 전환(체력 50% 최초 1회 플래그)
  - `BossBT` 네임스페이스: Attacking 진입 시 거리(5/30)로 콤보 선택, 동일 패턴 2회 연속이면 직전 제외 랜덤 교체
  - `BossAttackPatterns` (MonoBehaviour): 판정·이펙트·사운드, 투사체는 `TrackedPool` 로 생성 → 페이즈 전환·사망 시 `ReleaseAll`
  - 드론: 지렁이 드론(지휘자 + 몸체 8). 몸체는 지휘자의 **위치 기록을 간격만큼 뒤따라감**(기록 버퍼, Core), 파괴된 칸은 공백 유지
- **Core로 뺄 것:** 콤보 선택(거리·페이즈 → 콤보), 반복 방지 선택기(`IRandom` 사용), 페이즈 판정, 드론 몸체 추종 위치 계산
- **기획서 불일치 (spec/boss.md 13장):** 밟기/하단 공격 명칭, 2페이즈 "대포" 패턴 미정의, "성장형 드론" 미정의, 거리 조건 겹침 — 구현 전 현민 확인
- **씬 확인 포인트:** 50% 경계(250/500에서 전환 여부), 전환 1회만, 거리별 콤보, 같은 패턴 3연속 불가, 드론 활성 시 소환 대신 연사, 몸체 공백 유지

### 3-8. 룸·웨이브 (Core 흐름 + Runtime 연출)

- **근거:** level.md 웨이브 타임라인 — 대기 → 트리거 → 문 잠금 + 카메라 고정 → 스폰(등장 중 무적) → 난전 → 전멸 → 1.5초 → 개방
- **구조:**
  - `WaveSequence` (Core): 웨이브 목록 진행, 남은 적 수 추적, 전멸 판정 (시간 대기는 RoomController 쪽)
  - `RoomController` (Runtime): `async UniTask RunAsync(CancellationToken)` 로 타임라인 실행. 문 = 콜라이더 활성화, 카메라 = Cinemachine `CinemachineConfiner2D` 경계 교체
  - 스폰 연출: 투명 스폰 / 화면 사각지대(천장·벽 끝). 스폰 지점은 룸 프리팹의 자식 Transform (현민이 배치)
  - 라이프 코인 부활: 같은 룸에서 재개, **이미 죽인 적은 다시 스폰하지 않음** → `WaveSequence` 가 처치 기록 유지
- **씬 확인 포인트:** 마지막 적 처치 시에만 완료, 부활 후 처치한 적 재스폰 안 함, 웨이브 순서

### 3-9. 오브젝트 풀링 (Runtime)

- `UnityEngine.Pool.ObjectPool<T>` + 얇은 `TrackedPool<T>` (활성 목록, `ReleaseAll`)
- 대상: 적 전 타입, 보급품 상자, 투사체(총알·포탄·경고 마커·파편), 드론, 드랍 아이템, 히트 VFX
- 풀 객체는 `IPoolable.OnSpawn/OnDespawn` 에서 상태 완전 초기화 (체력, 슬롯, BT 블랙보드, 구독, 진행 중 UniTask 취소)
- 비동기 작업은 풀 반환 시 취소되는 토큰(`CancellationTokenSource` 를 OnDespawn에서 Cancel)을 쓴다. `destroyCancellationToken` 은 풀 반환에서 취소되지 않는다.

### 3-10. 보급품·드랍 (Core + Runtime)

- `DropRoller` (Core): `DropTableConfig` + `IRandom` → 아이템 목록. 예외 규칙: 칩 4종 보유 시 칩 → 포션, 체력 최대에서 포션 획득은 소멸만, 섬광탄 3개 초과면 바닥에 남음
- 상자: 모든 공격이 타격당 HP 1 감소, 3회 파괴, 크기별 수량 1/2/3
- **씬 확인 포인트:** 고정 시드로 상자 여러 개 파괴 시 드랍 비율, 칩 4종 보유 대체, 섬광탄 한도

### 3-11. 시네마틱·대화 (Runtime, 기존 시스템 유지)

- 시네마틱 3종(대화형/장면형/영상형): Timeline(`PlayableDirector`) + Cinemachine 카메라(정지형 / 좌우 무빙형 / 자유형), 영상형은 `VideoPlayer`
- 대화: 기존 SO 노드 그래프 + UniTask 시스템 유지. 말풍선은 액션 유지(입력 차단 없음)
- 시네마틱 중에는 `GameClock` 의 World 채널 정지 또는 입력 맵 전환 (Input System action map `Cinematic`)
- Timeline·애니메이터 에셋은 AI가 수정하지 않는다 → 시그널 수신 코드(`SignalReceiver` 대응 메서드)만 작성

### 3-12. UI (Runtime)

- HUD: 체력(지연 감소 바), 스태미나(고갈 점멸), 검게이지(최대 시 이펙트), 칩 아이콘, 보스 체력바(무적 시 자물쇠)
- 구조: `HudPresenter` 가 `PlayerStats`·`BossHealth` 이벤트를 구독해 View 갱신. View는 상태를 바꾸지 않는다
- QTE(낙섬 타이밍 바, 아재 패턴 커맨드): 판정은 Core `QteJudge`(Perfect/Normal/Fail 구간), 표시만 Runtime
- **씬 확인 포인트:** QTE 구간 판정 경계

---

## 4. 구현 순서 제안 (BACKLOG 분해 기준)

1. 기반: asmdef, `GameClock`, `IRandom`, `GameLog`, `TrackedPool`, 테스트 씬(`Test_Sandbox`)
2. 플레이어: 상태 머신 + 입력 버퍼 → 이동·점프 → 기본 공격·패링 → 스탯·자원
3. 전투 공통: Hitbox/Hurtbox, 무적, 칩 모드 → 스킬(일섬 → 연섬 → 낙섬 QTE → 프로 모션)
4. 적: BT 노드 → 타입 A → 슬롯 → 타입 B·C → 순찰병·경보
5. 룸·웨이브 → 보급품·드랍 → 라이프 코인
6. 보스(기존 코드 정리·디버그 표시 보강) → 드론
7. 중력 전환·벽 타기 (물리 리스크가 커서 별도)
8. 시네마틱·대화 연동, HUD

각 단계는 **로직(Core) → 어댑터(MonoBehaviour) → 연출 → 에디터 작업·씬 테스트** 로 나눈다.

---

## 5. 기획서에 아직 없는 결정 (질문 목록)

- 플레이어 물리: Rigidbody2D Dynamic + 직접 중력(제안) vs Kinematic 캐릭터 컨트롤러
- 공격 속도 "3"의 의미 (배속인지 초당 횟수인지), 걷는 속도 10 vs 분배 표 기본값 5 불일치
- 검게이지 최대 50(기본 사양) vs 100(분배 표·UI) 불일치
- 프로 모션 "본체 고속"의 배율
- 세이브: 체크포인트 삭제 이후 저장 대상(스탯 분배만?)

---

## 부록 A. 코드 템플릿 (AI_RULES 3장 — 프로젝트 전체에서 이 형태로 통일)

순수 로직 (Core):

```csharp
namespace UnMachine.Core.Combat
{
    public sealed class Invincibility
    {
        private readonly float _duration;
        private float _remaining;

        public Invincibility(float duration) { _duration = duration; }

        public bool IsActive => _remaining > 0f;

        public void Trigger() { _remaining = _duration; }

        public void Tick(float dt) { if (_remaining > 0f) _remaining -= dt; }
    }
}
```

어댑터 (MonoBehaviour):

```csharp
namespace UnMachine.Player
{
    public sealed class PlayerHurtbox : MonoBehaviour
    {
        [SerializeField] private PlayerConfig _config;
        private Invincibility _invincibility;

        private void Awake()
        {
            if (_config == null) { GameLog.Error(this, "PlayerConfig 누락"); enabled = false; return; }
            _invincibility = new Invincibility(_config.HitInvincibleSeconds);
        }

        private void Update() => _invincibility.Tick(GameClock.Player.DeltaTime);
    }
}
```

설정 (ScriptableObject):

```csharp
[CreateAssetMenu(menuName = "UnMachine/Config/Player")]
public sealed class PlayerConfig : ScriptableObject
{
    [Tooltip("피격 무적 시간(초). spec: player.md 기본 사양")]
    [SerializeField, Min(0f)] private float _hitInvincibleSeconds = 0.2f;
    public float HitInvincibleSeconds => _hitInvincibleSeconds;
}
```
