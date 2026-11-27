# UI v5 (Figma "HUD v5 — Chip · Boss · Dialogue")

씬·프리팹·팀원 스크립트를 **하나도 수정하지 않고** 런타임에 붙는 UI입니다.
이 폴더(`Assets/SYS/UI_v5`)만 추가/삭제하면 끝납니다.

## 어떻게 붙나
- 게임 시작 시 `V5Root`가 `[UI v5]` 오브젝트를 만들고 DontDestroyOnLoad.
- 씬이 바뀔 때마다 `PlayerState`, `BossController`(+`BossGroggyDriver`), 구 `DamageHandler`,
  `DialogueManager`를 타입으로 찾아 **값을 읽기만** 합니다. 이벤트 구독·필드 변경 없음.
- 기존 UI(Player UI, Boss Health Bar, BossHealthBar, BossGroggyBar, 대화 패널)는
  `CanvasGroup.alpha = 0`으로 **화면에서만** 가립니다. 활성 상태·로직은 그대로라 팀원 코드가 깨지지 않습니다.
- 기존 HUD가 없던 씬(예: 거점)에는 v5 HUD도 뜨지 않습니다.
- 대화는 `DialogueManager`의 패널/텍스트를 따라 그리는 미러 방식이라 타이핑·스킵·이벤트는 원본이 처리합니다.

## 켜고 끄기
| 방법 | 범위 |
|---|---|
| 메뉴 `Unmachine/UI v5/이 PC에서 끄기` | 이 PC만 (EditorPrefs, git에 안 올라감) |
| `Resources/UIv5Settings.asset` → `enabledByDefault` | 모두 (빌드 포함) |
| 플레이 중 **F8** | v5 ↔ 기존 UI 즉시 비교 (에디터·개발빌드만) |

## 설정 (`UIv5Settings.asset`)
- 폰트: 숫자 RobotoCondensed-Bold SDF, 한글 Galmuri11-Bold SDF / GalmuriMono11 SDF (기존 에셋 참조만)
- 플레이어 초상: `zenis.png` 얼굴 영역 크롭 (`playerPortraitUV`)
- 보스 이름/부제, 보스바 위치(`bossBarAtBottom`, 기본 하단 — boss.md 15장 원안은 상단)
- 화자별 초상·태그(`speakers`). 비어 있으면 기본 규칙: 제니스 → 플레이어 초상, "용병" → MERC, 시스템 → SYS
- 정렬: HUD -1(팀원 페이드·로딩 캔버스 아래), 대화 1(레터박스 위)

## 구성
| 파일 | 내용 |
|---|---|
| `V5Root` | 부트스트랩, 씬 스캔(0.5초), 기존 UI 가리기, 대화/시네마틱 중 HUD 숨김 |
| `V5PlayerHud` | 초상·HP 숫자·HP 바(고스트/눈금)·SP·검게이지 5칸 |
| `V5ChipWidget` | 칩 타일·모드명·[B] 키캡·보유칩 다이아, 전환 연출 |
| `V5BossBar` | 엠블럼·이름/상태태그·해칭 HP·50% 페이즈 마커·그로기 바·피격 파편 |
| `V5BossPrompt` | 그로기 중 보스 머리 위 "낙섬!" |
| `V5DialogueBand` | 하단 대화 밴드(초상 168, 이름틱, 본문, 진행 셰브론) |
| `V5Shape` | 스프라이트 없이 그리는 벡터 도형 Graphic |
