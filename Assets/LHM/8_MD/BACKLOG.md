# docs/BACKLOG.md — 기능 분해 결과와 작업 상태

> 큰 기능을 **리뷰 한 번에 검토 가능한 크기**(대략 파일 4개·300줄 이하)의 작업으로 나눈 목록이다. (AI_RULES 1장)
> 초안은 Claude가 `/breakdown` 으로 작성하고, **현민이 승인한 작업만** 진행한다.
> 작업이 끝나면 Claude가 상태를 갱신한다 (커밋은 현민). 완료된 기능 묶음은 지우지 않고 아래로 내린다.
> 완료 기준은 **씬에서 현민이 확인할 수 있는 동작**으로 쓴다.
>
> 상태: ⬜ 대기 / ✅ 승인됨 (진행 가능) / 🔨 진행 중 / 🎮 현민 씬 테스트 대기 / 👀 리뷰 중 / 🏁 완료 (현민 커밋) / ⛔ 보류
> 유형: **로직**(Core 순수 클래스) / **어댑터**(MonoBehaviour 연결) / **연출**(VFX·애니메이션·사운드 호출) / **에디터**(현민 수작업)

---

## 기능: 1챕터 보스 「판타스틱 베이비」 완성

- 요청 원문: 1챕터 보스 「판타스틱 베이비」 완성 — 1페이즈는 회전 레이저+판넬 사이를 피하며 딜, 2페이즈는 좁혀오는 레이저 벽 압박 속에서 아재 패턴 커맨드로 반격하는 긴장감
- 기획서 근거: `Assets/LHM/8_MD/boss.md` 전체 (특히 2장·12장·13장)
- 분해 일시: 2026-11-25 / 기준 HEAD: `1942cbe` / 승인: ⬜ 대기 (현민이 ✅로 바꿈)
- 경로 표기: 아래 `New/` = `Assets/LHM/0_Script/Boss/New/`. 새 코드는 **`New/Core/`(순수 C#)**, **`New/Gimmick/`(MonoBehaviour·SO)** 에 두는 안 (→ Q1)
- 비동기 원칙: 기존 코루틴(BossController·BossAttackPatterns·드론)은 **그대로 둔다**. 새 코드만 UniTask + `CancellationToken`(페이즈별 CTS, 풀 반환 시 Cancel) 사용. 새 코드는 `StartCoroutine` 금지.

### 0. 현민 답변 반영 (2026-11-25)

| Q | 결정 |
|---|---|
| Q1 | 새 코드는 `New/Core/`(순수 C#) + `New/Gimmick/`(MonoBehaviour·SO), asmdef 없음. 네임스페이스 `UnMachine.Core.Boss` / `UnMachine.Boss` |
| Q2 | T1에서 `GameLog`·`IRandom` 최소 버전 생성 |
| Q3 | Core는 dt를 인자로 받고, 어댑터만 `Time.deltaTime` 사용(`// guard-allow: GameClock 미도입`) |
| Q4 | **확정**: 무적 = ① 등장 중 ② **2페이즈 기본 쉴드** — 아재 성공 → 그로기 동안만 쉴드 해제(딜 타임). 1페이즈는 쉴드 없음(레이저·판넬 피하며 딜). 0.2초 피격 무적에는 자물쇠 안 띄움(깜빡임 과다). 아재가 너무 어려우면 막히므로 실패해도 재시도 무제한(쿨다운만) |
| Q5 | 보스는 고정되지 않음. 레이저·판넬은 기존 콤보와 **동시에** 계속 돎 → 레이저 앵커는 보스 자식(이동·좌우 반전 따라감). "중앙 상단 스폰"은 보스 초기 배치 위치(에디터) |
| Q6 | 거리 2/8/9는 임시 축소 값 → **그대로 둠** (T22에서 제외) |
| Q7 | 3번째에 교체로 수정 (T22) |
| Q8 | 아재 성공 → 보스 **그로기**, 실패 → 플레이어 **피해** |
| Q9 | **확정**: 판넬 파괴 가능, 공격은 아래 "판넬 안" |
| Q10 | **확정**: 커맨드 입력(↑↓←→ + Space). 4방향 3개 + Space 마무리(총 4입력), 제한 3초, **오답 1회 = 즉시 실패**. 입력 중 플레이어 이동 잠금(방향키가 이동 축 `Horizontal`과 겹침 — PlayerMove.cs:136), 보스는 진행 중 패턴만 마치고 새 콤보는 보류, 레이저 벽은 계속 좁혀옴(긴장감) |
| Q11 | **A안 확정**: 거울 = 아재 패턴 상호작용 지점(같은 물건). 방 하단 정중앙 고정, 거울 앞에서 V → 커맨드. 거울 자체의 다른 기능(반사 등) 없음 |
| Q12 | **확정: V** (플레이어·대화에서 미사용: 사용 중 키 = Space, R, LeftShift, F, Esc, E, W, Q, H, P, G, B). 키는 `AjaeConfig` 의 KeyCode 필드라 인스펙터에서 바꿀 수 있음. 입력 읽기는 기존 플레이어와 같은 legacy `Input` (어댑터 한 곳에만) |
| Q13 | 명칭 "밟기"로 통일 (코드 enum `Stomp` 유지, 로그·UI 표기만 "밟기") |
| Q14 | `Cannon` 은 임시 패턴 → 손대지 않음 |
| Q15 | "성장형 드론" = 지휘자 드론의 점점 커지는 에너지탄 |
| Q16 | FSM·BT와 별개의 "기믹" 층으로 진행 |
| Q17 | CSH `PlayerMove.cs` 수정 허용 → 커맨드 입력 중 이동 축(:136-137)과 점프(Space, :400) 잠금 플래그 추가 |

**판넬 안 (Q9 확정)** — 3종이 서로 다른 회피 방식을 요구하게 함

| 타입 | 역할 | 공격 | 추천 초기값 (튜닝용) |
|---|---|---|---|
| 레드 A | 조준 압박 | 플레이어 조준선 표시(추적) → 고정 → 직선 레이저 발사. 고정 순간 옆으로 피하는 패턴 | 추적 1.2초 / 고정 0.4초 / 발사 0.2초 / 피해 5 / 주기 4초 |
| 블루 B | 공간 봉쇄 | 제자리 정지 후 8방향 원형 느린 탄 (기존 총알 풀 재사용) | 8발 / 탄속 4 / 피해 3 / 주기 5초 |
| 그린 C | 근접 위협 | 전조 깜빡임 → 플레이어 위치로 몸통 돌진(접촉 피해) → 다시 배회 | 전조 0.6초 / 돌진 속도 12 / 피해 5 / 주기 6초 |
| 공통 | — | 파괴 가능, 파괴 후 일정 시간 뒤 보스 몸에서 재스폰(1페이즈 압박 유지) | 체력 각 15 / 재스폰 8초 / 동시 각 1개 |

### A. 구현 현황 (boss.md 장별, 근거: `New/` 파일:줄)

| 장 | 항목 | 상태 | 근거 / 메모 |
|---|---|---|---|
| 1 | 등장 연출(상공 낙하·착지) | 구현됨 | BossController.cs:538-573 |
| 2 | 보스룸 40×30 공간 | 미구현(에디터) | 코드 대상 아님. 경계 값을 쓰는 코드 없음 |
| 2 | 1P 보스 중앙 상단 스폰 | 미구현(에디터) | 보스는 고정 안 함(Q5) → 초기 배치 위치만 중앙 상단으로. 낙하 후 기존처럼 이동 (BossController.cs:740-803) |
| 2 | 1P 양옆 연결부 회전 레이저 | 미구현 | New/에 없음. Old_stage1 `Phase1LaserPattern.cs`(전체 주석) 참고만 |
| 2 | 1P 레드A/블루B/그린C 판넬 | 미구현 | New/에 없음. Old_stage1 `Phase1PanelPattern.cs`·`Panel.cs`(주석) 참고만 |
| 2 | 2P 하단 중앙 거울 | 미구현 | — |
| 2 | 2P 좁혀오는 레이저 벽 | 미구현 | Old_stage1 `OutsiedLaser.cs`(주석) 참고만 |
| 2 | 2P 아재 패턴 공간 보장 | 미구현 | — |
| 3 | FSM + BT + Patterns 3분할 | 구현됨 | BossController.cs:579-615, 620-686 / BTNode·BTComposite·BTLeaves / BossAttackPatterns.cs:311-385 |
| 4 | 체력 500·걷기 3·부스터 10·공격 3 | 구현됨 | BossController.cs:47-50 (SO 아님, MonoBehaviour 필드) |
| 4 | 피격 무적 0.2초·점멸 3회 | 구현됨 | BossController.cs:75-77, 1165-1186 |
| 4 | 동시 활성 드론 최대 1 | 부분 | BossAttackPatterns.cs:1051 `>= 1` 하드코딩, `maxDroneCount = 2`(BossController.cs:117)는 풀 크기에만 사용(:272) |
| 4 | 드론 상승 속도 5 / (0,3,0) | 구현됨 | BossController.cs:114-115 |
| 5 | 상태 Appearing~Dead | 구현됨 | BossController.cs:10-20, 579-615 |
| 6-2 | 50% 이상 1P / 미만 2P | 부분 | BossController.cs:942 `> 0.5f` → HP 정확히 250에서 2P (기획은 1P). BossHealthBar.cs:84도 동일 |
| 6-3 | 거리별 콤보 5 / 30 | 부분 | BossController.cs:628-644, 657-673 거리 2 / 8 / 9 사용, 8~9 데드존을 폴백(:680-683)으로 메움 → Q6 |
| 6-4 | 동일 패턴 2회 연속 → 3번째 교체 | 부분 | BossAttackPatterns.cs:316-335 — `count >= 2` 라 **2번째에** 교체됨(1 회차 차이). 2P 근접 콤보 "밟기→밟기"가 항상 깨짐 → Q7 |
| 6-5 | 휴식 2초 / 1초 | 구현됨 | BossController.cs:743 (하드코딩) |
| 7 | 밟기·기관총 3연발·연사·곡사포·돌진·드론 | 구현됨 | BossAttackPatterns.cs:422, 457, 479, 560, 843, 1046 |
| 7-1 | 곡사포 탄종 3종 가중치 | 구현됨 | BossAttackPatterns.cs SelectRandomAmmoType / HowitzerCannonball.cs |
| 8 | 피격 무적 | 구현됨 | 4장과 동일 |
| 9 | 지휘자 드론 ∞ 경로·3초 에너지탄·점점 커짐 | 구현됨 | ConductorDrone.cs:19, 160-161, 188 / EnergyBullet.cs:17, 88 |
| 9 | 몸체 8개 추종·파괴 칸 공백 유지 | 구현됨 | BodyDrone.cs:111 (`bodyIndex * 8` 고정 오프셋) |
| 9 | 지휘자 파괴 시 흩어짐·1~3초 사격 | 구현됨 | BodyDrone.cs:139-164, 205-215 |
| 10-1 | 페이즈 전환 1~7단계 | 구현됨 | BossController.cs:1096-1150 (F1 디버그 :994-1052 중복 코드) |
| 10-1/11 | 전환 시 발사체 일괄 회수 | 부분 | BossController.cs:494-533 은 포탄·총알·경고마커만. AirBurst/Bounce 파편은 풀 대신 `Instantiate`(HowitzerCannonball.cs:300, 353), 에너지탄도 `Instantiate`(ConductorDrone.cs:201) → 회수 안 됨 |
| 10-2 | 사망 정리·애니·BGM·2초·비활성 | 구현됨 | BossController.cs:1251-1298 (드론 회수 :1265-1273) |
| 11 | 풀 6종 | 부분 | 파편 풀 2종은 만들어지지만(:391, :424) 실제 생성은 Instantiate |
| 12 | 체력바 표시 | 부분 | BossHealthBar.cs 존재(Slider·색·페이즈 텍스트). "상단 중앙 길게"는 씬 배치 문제 |
| 12 | 무적 시 자물쇠/쉴드 | 미구현 | BossHealthBar.cs에 관련 필드·API 없음, BossController도 무적 상태를 외부에 알리지 않음 |
| 12 | 아재 패턴 커맨드 UI (G키) | 미구현 | — (참고: CSH/0.Scripts/Player/playerState.cs:114 에 Shift+G 사용 중 → Q12) |
| 13 | 불일치 메모 | — | 질문 Q13~Q16 |
| 공통 | GameLog / GameClock / IRandom / TrackedPool / asmdef | 미구현 | 프로젝트에 없음 (Packages에 UniTask·InputSystem 1.14.2·Cinemachine 3.1.6 있음) → Q2·Q3 |

### B. 작업 목록

#### B-0. 기반·연결 (모든 기믹의 선행)

| ID | 유형 | 작업 | 완료 기준 (씬) | 범위 밖 | 의존 | 예상 파일 | 상태 |
|---|---|---|---|---|---|---|---|
| T1 | 로직 | 최소 공용 기반: `IRandom`·`SystemRandom(seed)`·`GameLog`(Conditional) + UniTask 미관측 예외 → GameLog 등록 | 에디터 컴파일 에러 0. T2 씬 테스트에서 GameLog 출력이 Console에 보임 | GameClock(→Q3), 기존 Debug.Log 교체 | Q1·Q2 | Core/IRandom.cs, Core/SystemRandom.cs, Gimmick/GameLog.cs, Gimmick/UniTaskExceptionBootstrap.cs | 🏁 |
| T2 | 어댑터 | BossController 이벤트 훅: `BattleStarted`(등장 끝), `PhaseChanged(int)`, `Died`, `InvulnerableChanged(bool)` + `IsInvulnerable` 읽기 프로퍼티. 기존 코루틴·흐름은 그대로, 발생 지점에 호출만 추가 | 플레이: 착지 1초 후 BattleStarted 로그 1회 / F1 → PhaseChanged(2) 1회(다시 F1 눌러도 추가 없음) / 사망 → Died 1회 / 등장 중 Invulnerable=true → 끝나면 false | 페이즈 경계 버그 수정(T22), 체력바 표시 | T1 | New/BossController.cs (+40줄 내외) | 🎮 |
| T3 | 어댑터 | `BossArenaDirector` + `IBossGimmick`(`UniTask RunAsync(ct)`, `ReleaseAll()`) : 페이즈별 CTS 보유, BattleStarted→1P 기믹 시작, PhaseChanged(2)→1P Cancel+ReleaseAll 후 2P 시작, Died→전부 Cancel+ReleaseAll. `BossArenaConfig`(방 경계 40×30, 중앙 상단·하단 중앙 앵커 오프셋) | Scene 뷰 Gizmo로 방 경계·앵커 표시. 인스펙터 읽기 전용 "현재 페이즈 / 실행 중 기믹 수". 기믹 0개로도 시작·취소 로그가 순서대로 1회씩 | 개별 기믹 | T2 | Gimmick/BossArenaDirector.cs, Gimmick/IBossGimmick.cs, Gimmick/BossArenaConfig.cs | ✅ |
| T4 | 에디터 | 보스룸 씬에 Director 배치, Config 에셋 생성·연결, 방 크기 40×30 이상 확인 | 현민 플레이 확인 (T3 완료 기준 재확인) | — | T3 | (현민) | ✅ |

#### B-1. 1페이즈 — 회전 레이저

| ID | 유형 | 작업 | 완료 기준 (씬) | 범위 밖 | 의존 | 예상 파일 | 상태 |
|---|---|---|---|---|---|---|---|
| T5 | 로직 | `RotatingBeam`(각도 진행: 시작각·속도·방향, dt 입력) + `DamageTickTimer`(대상별 피해 간격) | T6 Gizmo에서 빔 각도가 Config 속도대로 회전 (속도 0 → 정지, 음수 → 역회전) | 레이캐스트·표시 | T1 | Core/RotatingBeam.cs, Core/DamageTickTimer.cs | ✅ |
| T6 | 어댑터 | `RotatingLaserGimmick`(IBossGimmick): 보스 양옆 연결부 앵커 2곳에서 빔, `Physics2D.Raycast`(NonAlloc)로 벽까지 길이, 플레이어 `IDamageable` 틱 피해, LineRenderer 끝점 갱신. `RotatingLaserConfig` | 1P 시작 시 양옆에서 빔 2개가 회전, 빔이 벽에서 끊김, 닿아 있으면 틱 간격마다 플레이어 HP 감소, F1 → 즉시 사라짐, 사망 → 사라짐 | 전조·이펙트 | T3·T5 | Gimmick/RotatingLaserGimmick.cs, Gimmick/RotatingLaserConfig.cs | ✅ |
| T7 | 연출 | 레이저 전조(경고선 깜빡임 → 발사), 시작/종료 페이드, 발사 루프 사운드 호출 | 시작 전 경고선이 Config 시간만큼 보인 뒤 피해 판정 시작(경고 중엔 피해 0) | 셰이더 제작 | T6 | Gimmick/RotatingLaserView.cs | ✅ |
| T8 | 에디터 | 보스 프리팹 양옆 앵커·LineRenderer 2개·Config 에셋·플레이어 레이어 마스크 | 현민 플레이 확인 | — | T7 | (현민) | ✅ |

#### B-2. 1페이즈 — RGB 판넬

| ID | 유형 | 작업 | 완료 기준 (씬) | 범위 밖 | 의존 | 예상 파일 | 상태 |
|---|---|---|---|---|---|---|---|
| T9 | 로직 | `PanelWanderPlanner`(방 경계 안 무작위 목표점, IRandom, 도착 판정·최소 이동거리) + `PanelAttackCycle`(대기→전조→발사→쿨다운 상태 전이, dt 입력) | T10 Gizmo에서 목표점이 항상 방 경계 안, 고정 시드 재시작 시 같은 경로 | 공격 내용 | T1 | Core/PanelWanderPlanner.cs, Core/PanelAttackCycle.cs | ✅ |
| T10 | 어댑터 | `BossPanel`(이동·`IDamageable` 피격·체력 0 → 파괴·풀 반환 시 CTS Cancel·상태 초기화) + `BossPanelGimmick`(보스 몸 위치에서 A/B/C 스폰, 파괴 후 재스폰 대기, 활성 목록, ReleaseAll) + `BossPanelConfig`(타입별 배열). **이동·파괴만, 공격 없음** | 1P 시작 시 보스 몸에서 R·G·B 판넬이 나와 경계 안에서만 무작위 이동(Gizmo 목표점), 플레이어 공격으로 파괴 → 재스폰 시간 뒤 다시 등장, F1 → 전부 사라지고 재스폰 대기도 취소, 다시 플레이해도 이전 위치·체력 안 남음 | 공격, 파괴 연출 | T3·T9 | Gimmick/BossPanel.cs, Gimmick/BossPanelGimmick.cs, Gimmick/BossPanelConfig.cs, Gimmick/PanelType.cs | ✅ |
| T11 | 어댑터 | 판넬 타입별 공격 전략 `IPanelAttack` × A(조준 레이저)/B(8방향 탄)/C(돌진) — "판넬 안". B 탄은 기존 `BossController.GetBulletFromPool()` 재사용(→ 기존 일괄 회수에 포함) | 레드: 조준선 추적→고정→발사, 고정 후 피하면 피해 0 / 블루: 정지 후 8발 원형 / 그린: 깜빡임 후 플레이어 위치로 돌진·접촉 피해. F1 시 발사체까지 사라짐 | 연출 | T10 | Gimmick/IPanelAttack.cs, Gimmick/PanelAttackRed.cs, Gimmick/PanelAttackBlue.cs, Gimmick/PanelAttackGreen.cs | ✅ |
| T12 | 연출 | 판넬 색·전조 깜빡임·피격/파괴 이펙트·사운드 호출 | 전조가 공격 전 Config 시간만큼 보임, 파괴 시 이펙트 1회 | 아트 제작 | T11 | Gimmick/BossPanelView.cs | ✅ |
| T13 | 에디터 | 판넬 프리팹 3종(SpriteRenderer·Collider2D·View), Config 에셋 | 현민 플레이 확인 | — | T12 | (현민) | ✅ |

#### B-3. 2페이즈 — 레이저 벽·거울·아재 패턴

| ID | 유형 | 작업 | 완료 기준 (씬) | 범위 밖 | 의존 | 예상 파일 | 상태 |
|---|---|---|---|---|---|---|---|
| T14 | 로직 | `ClosingWallModel`: 좌/우 벽 x 위치, 수렴 속도, **최소 간격(중앙 안전지대) 보장**, `Push(amount)`(아재 성공 시 후퇴용) | T15 Gizmo에서 벽이 최소 간격에서 멈춤, Push 시 바깥으로 이동 후 다시 수렴 | 피해 판정 | T1 | Core/ClosingWallModel.cs | ✅ |
| T15 | 어댑터 | `LaserWallGimmick`(IBossGimmick): 양 끝 벽 Transform 이동, 접촉 틱 피해(트리거), `LaserWallConfig` | F1 → 화면 양 끝에서 벽이 좁혀오다 최소 간격에서 정지, 닿으면 틱 피해, 사망 → 사라짐 | 아재 연동 | T3·T14 | Gimmick/LaserWallGimmick.cs, Gimmick/LaserWallConfig.cs, Gimmick/LaserWallHitbox.cs | ✅ |
| T16 | 어댑터 | `BossMirror`(상호작용 오브젝트): 2P 시작 시 하단 정중앙 등장, 반경 안 플레이어 감지 → "V" 프롬프트, 상호작용 키 입력 시 `Interacted` 이벤트, 쿨다운 중 무시. 키는 Config KeyCode(기본 V) | 2P 진입 시 거울 등장, 반경 안에서만 V 프롬프트·로그, 반경 밖 V 무시, 사망 시 사라짐 | 커맨드 판정 | T3 | Gimmick/BossMirror.cs, Gimmick/InteractPromptView.cs | ✅ |
| T17 | 로직 | `AjaeCommandSequence`(IRandom으로 방향 3개 + Space 마무리 생성) + `AjaeCommandJudge`(입력 진행·오답 1회 즉시 실패·제한시간 → Success/Fail) + `AjaeConfig`(길이·제한시간·반경·쿨다운·키·실패 피해) | T18 팝업에서 길이·제한시간이 Config 값과 같음, 오답 1회에 즉시 Fail, 시간 초과 Fail | UI | T1 | Core/AjaeCommandSequence.cs, Core/AjaeCommandJudge.cs, Gimmick/AjaeConfig.cs | ✅ |
| T18 | 연출 | 커맨드 UI: 화면 중앙 대형 팝업(화살표 칸·진행 하이라이트·남은 시간 바) + Presenter(입력→Judge, 결과→View, 입력 중 플레이어 이동·점프 잠금) + `PlayerMove` 에 외부 잠금 플래그 `SetInputLocked(bool)` 추가. 판정은 Judge만 | V → 팝업, 맞는 키마다 칸 점등, 틀리면 실패 표시, 시간 초과 실패, 입력 중 방향키·Space로 플레이어가 움직이거나 점프하지 않음, 끝나면(성공·실패·시간 초과·보스 사망 모두) 팝업 닫히고 이동 복구 | 결과 효과 | T16·T17 | Gimmick/AjaeCommandPresenter.cs, Gimmick/AjaeCommandView.cs, CSH/0.Scripts/Player/PlayerMove.cs (잠금 플래그만) | ✅ |
| T19 | 어댑터 | BossController 그로기·쉴드: `BossState.Groggy`(enum **맨 끝에 추가** → 기존 Animator `State` 정수값 유지), `EnterGroggy(seconds)`(ForceStopAllPatterns·이동 정지·피해 허용, UniTask 대기 + 사망 시 취소 → 끝나면 Idle), `SetShield(bool)`(2P 기본 쉴드, TakeDamage에서 거부, `InvulnerableChanged` 발생). 기존 코루틴은 그대로 | 디버그 키로 그로기 → 보스가 공격·이동 멈추고 Config 시간 뒤 Idle 복귀, 그로기 중 사망하면 Dead만 진행. 쉴드 On이면 피해 0 | 아재 연결 | T2 | New/BossController.cs, Gimmick/BossGroggyConfig.cs | ✅ |
| T25 | 어댑터 | 아재 결과 연결: 성공 → `EnterGroggy` + 그로기 동안 쉴드 해제(Q4), 실패 → 플레이어 피해 + 재시도 쿨다운. 입력 중 보스 새 콤보 보류 | 성공 시 보스 그로기·자물쇠 사라짐·딜 가능 → 끝나면 쉴드 복귀 / 실패 시 플레이어 HP가 Config만큼 감소, 쿨다운 동안 V 무시 | — | T15·T18·T19 | Gimmick/AjaeOutcomeHandler.cs (+Director 소폭) | ✅ |
| T20 | 에디터 | 벽 2개·거울 프리팹, 커맨드 UI 캔버스(중앙), Config 에셋, Animator에 Groggy 상태(State 값 = 8) 추가 | 현민 플레이 확인 | — | T25 | (현민) | ✅ |

#### B-4. UI·회수·기존 보정

| ID | 유형 | 작업 | 완료 기준 (씬) | 범위 밖 | 의존 | 예상 파일 | 상태 |
|---|---|---|---|---|---|---|---|
| T21 | 어댑터 | 체력바 무적 표시: `InvulnerableChanged` 구독 → 자물쇠 아이콘/쉴드 오버레이 On/Off. 0.2초 피격 무적은 표시 안 함 | 등장 낙하 중 자물쇠 → 전투 시작 시 사라짐 / 2P 진입 시 자물쇠 → 그로기 중 사라짐 → 그로기 끝 다시 표시 / 피격 깜빡임 중엔 자물쇠 안 뜸 | 체력바 레이아웃 | T19 | New/BossHealthBar.cs, Gimmick/BossShieldIndicator.cs | ✅ |
| T22 | 어댑터 | 기존 보정: 50% 경계(`>= 0.5`→1P, 체력바 동일), 반복 방지 3번째 교체, 드론 제한 하드코딩 → 필드. **거리 2/8/9는 그대로**(임시 축소 값). 코루틴 구조는 손대지 않음 | HP 250 → 1P 유지, 249 → 2P / 같은 패턴 2연속은 허용, 3번째에 교체 로그 / 드론 1마리 활성 시 연사 대체 | 거리 조정, 콤보 재설계 | — | New/BossController.cs, New/BossAttackPatterns.cs, New/BossHealthBar.cs | ✅ |
| T23 | 어댑터 | 회수 누락 보강: 파편 2종을 기존 파편 풀 사용으로, 에너지탄 회수 경로 추가 → 전환·사망 시 함께 회수 | 곡사포 AirBurst/Bounce 직후 F1 → 파편 전부 사라짐, 드론 에너지탄 비행 중 보스 사망 → 사라짐 | 새 기믹(T3에서 처리) | T2 | New/HowitzerCannonball.cs, New/ConductorDrone.cs, New/EnergyBullet.cs, New/BossController.cs | ✅ |
| T24 | 에디터 | 체력바 상단 중앙 가로 배치, 자물쇠/쉴드 이미지 연결, 최종 통합 플레이(1P→2P→사망 전 과정) | 현민 플레이 확인: 전환·사망 후 씬에 레이저·판넬·벽·거울·UI·발사체 0개 | — | T21~T23 | (현민) | ✅ |
| T26 | 에디터 툴 | 테스트 씬 생성 툴 `UnMachine/Test Scene/Boss Arena` (AI_RULES 0장 2번 예외): 40×30 방(바닥·천장·좌우 벽, ground 레이어), 카메라, Global Light 2D, 위치 마커, 플레이어·보스 프리팹 배치, 보스 player 참조 연결. 같은 이름 씬은 덮어쓰지 않음 | 메뉴 실행 → `Test_Boss.unity` 생성, Play 시 플레이어가 바닥에 서고 보스가 낙하·착지 | 기존 씬 수정, 보스 프리팹 제작 | — | New/Editor/BossTestSceneBuilder.cs | 🎮 |

권장 순서: T1→T2→T3→T4 → T22 → (B-1 ‖ B-2) → T19 → T21 → B-3(T14~T18 → T25 → T20) → T23 → T24

### C. 남은 질문 (현민 답변 필요) — 답변 완료분은 0절로 이동

- Q18. 그로기 시간(추천 4초), 그로기 중 받는 피해 배율(추천 1배 = 쉴드 해제만), 아재 실패 피해(추천 10), 재시도 쿨다운(추천 3초)?

**기획서에 없는 수치** — 답이 없으면 아래 추천값을 SO 초기값으로 시작 (현민이 인스펙터에서 튜닝)
- 회전 레이저: 회전 속도 45°/s, 양쪽 반대 방향, 길이 = 벽까지(최대 40), 굵기 0.4, 틱 피해 3 / 0.3초, 전조 1초, 상시 발사 (On 6초 / Off 3초 주기도 가능)
- 판넬: 판넬 안 표 값, 이동 속도 3, 목표점 최소 거리 4
- 레이저 벽: 수렴 속도 0.5/초, 최소 간격 12(중앙 안전지대), 시작 = 방 양 끝, 틱 피해 5 / 0.5초, 아재 성공 시 Push 0(그로기만)
- 아재: 길이 4(방향 3 + Space), 제한 3초, 상호작용 반경 2
- 무적 표시: 자물쇠 아이콘, 페이드 0.2초
