---
name: scene-test-writer
description: 현민이 Unity 씬에 직접 배치해서 확인할 "씬 테스트 절차"와 디버그 표시를 작성한다. 구현을 마치고 HANDOFF를 쓸 때, "테스트 방법", "어떻게 확인해", "씬에서 확인", "디버그 표시" 같은 말이 나올 때 사용한다. Unity를 실행하거나 테스트를 돌리지 않는다.
---

# scene-test-writer

> `.claude/skills/scene-test-writer/` 와 `.agents/skills/scene-test-writer/` 에 같은 내용으로 둔다.

기준은 `AI_RULES.md` 6장과 `docs/SCENE_TEST.md` 다. **결과는 현민만 판정한다.** 이 스킬은 절차를 쓰는 것까지만 한다.

## 1. 에디터 작업 (HANDOFF 5번)

현민이 그대로 따라 할 수 있게 **메뉴 경로·오브젝트 이름·필드 이름**을 정확히 쓴다.

```
1. Project 창 Assets/_Project/Config/ 우클릭 → Create → UnMachine/Config/Boss → 이름 BossConfig_Ch1
2. Test_Boss 씬의 Boss 오브젝트 선택 → Add Component → BossDebugView
3. BossController 컴포넌트의 Config 칸에 BossConfig_Ch1 드래그
```

- 새 스크립트를 붙일 오브젝트, 새 레이어·태그, Physics2D 충돌 매트릭스 변경이 필요하면 빠짐없이 쓴다.
- 기존 `.asset` 값과 새 필드 초기값이 다르면 "필드 / 현재 값 → 바꿀 값"으로 쓴다.

## 2. 씬 테스트 절차 (HANDOFF 5-1번)

| # | 조작 | 기대 결과 (기획서 근거) | 결과 (현민) |
|---|---|---|---|
| 1 | Play → 보스 앞 3 Unit에 선다 | 디버그 라벨 콤보 `Stomp → Burst3` (boss.md 6-3 근거리) | ⬜ |
| 2 | 보스 Debug 메뉴 "Debug_체력 250으로" → 플레이어 공격 1회 | 라벨 Phase 2, Console `[Boss] Phase 1 → 2` 1회 (boss.md 6-2) | ⬜ |

작성 규칙:
- 한 줄에 조작 하나, 기대 결과 하나. 기대 결과는 **눈으로 볼 수 있는 것**(위치, 애니메이션, 디버그 라벨, Console 로그)으로 쓴다.
- 기획서 근거가 있으면 `spec 파일 장`을 괄호로 붙인다.
- 바뀐 동작은 전부 한 줄 이상 확인되게 한다. 바뀌지 않아야 하는 기존 동작(회귀)도 1~2줄 넣는다.
- **경계 상황**을 따로 적는다: 정확히 경계값(체력 50%, 거리 5·30), 연속·동시 입력, 풀에서 재사용된 두 번째 개체, 오브젝트 비활성·파괴 직후, 일시정지 중.
- **Console에 나오면 안 되는 것**: `NullReferenceException`, `MissingReferenceException`, 해당 기능의 "누락" 에러 로그.

## 3. 디버그 표시 (코드)

씬에서 바로 보이지 않는 값이 기대 결과에 들어가면, 확인 수단을 코드로 같이 만든다 (`docs/SCENE_TEST.md` 3장).

```csharp
#if UNITY_EDITOR
private void OnDrawGizmosSelected()
{
    if (_config == null) return;
    Gizmos.color = Color.yellow;
    Gizmos.DrawWireSphere(transform.position, _config.NearRange);   // 근거리 5
    Gizmos.color = Color.red;
    Gizmos.DrawWireSphere(transform.position, _config.FarRange);    // 원거리 30
}

[ContextMenu("Debug_체력 250으로")]
private void Debug_SetHalfHp() => _health.SetForDebug(_config.MaxHp * 0.5f);
#endif
```

- 빌드에서 빠지게 `#if UNITY_EDITOR` / `[Conditional("UNITY_EDITOR")]`.
- 디버그 표시를 위해 게임 로직 구조를 바꾸지 않는다. 읽기 전용 프로퍼티를 여는 정도까지.

## 4. 하지 않는 것

- Unity·batchmode·테스트 실행, 결과를 "통과"로 미리 적기 (결과 칸은 ⬜로 둔다)
- 씬·프리팹 파일 직접 수정 (배치는 1번 에디터 작업으로)
- 현민이 요청하지 않은 자동 테스트 작성
