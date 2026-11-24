---
name: unity-guard
description: Unity 6·C# 함정 점검. C# 코드를 작성·수정·리뷰할 때, 또는 "async void", "Find", "timeScale", "velocity", "GetComponent", "풀", "meta" 같은 말이 나올 때 사용한다. AI_RULES 2장 금지 패턴과 Core/Runtime 경계 위반을 찾아 대체 코드를 제시한다.
---

# unity-guard

> 이 파일은 `.claude/skills/unity-guard/` 와 `.agents/skills/unity-guard/` 에 **같은 내용으로** 둔다. 한쪽을 고치면 다른 쪽도 고친다.

기준은 `AI_RULES.md` 2장·3장이다. 대상은 `Assets/Plugins/` 를 제외한 `.cs` 파일 (또는 지정된 파일·diff).
줄 끝에 `// guard-allow: <이유>` 가 있으면 건너뛴다.

## 절차

1. 아래 패턴으로 검색한다 (grep 등). 주석·문자열 안의 일치는 제외한다.

| 찾을 것 | 검색 패턴 (예) | 대체 |
|---|---|---|
| async void | `async void` | `async UniTaskVoid` + `.Forget()`, 또는 `async UniTask` |
| 신규 코루틴 | `StartCoroutine`, `IEnumerator` (테스트 폴더 제외) | UniTask |
| 취소 없는 대기 | `UniTask.Delay(`, `UniTask.WaitUntil(` 중 `cancellationToken` 없는 것 | `cancellationToken: token` 추가 (풀 객체는 풀 반환 토큰) |
| 런타임 검색 | `GameObject.Find`, `FindObjectOfType`, `FindObjectsOfType`, `FindWithTag` | 직렬화 참조·주입. 초기화에서만 `FindAnyObjectByType` |
| Update 내 GetComponent | `Update`/`FixedUpdate`/`LateUpdate` 본문의 `GetComponent` | `Awake` 캐시 |
| public 직렬화 필드 | MonoBehaviour/ScriptableObject의 `public <타입> <이름>;` 필드 | `[SerializeField] private` + 프로퍼티 |
| Unity 6 이름 변경 | `.velocity` (Rigidbody2D), `.drag`, `.angularDrag` | `linearVelocity`, `linearDamping`, `angularDamping` |
| 게임플레이 timeScale | `Time.timeScale` (일시정지 코드 외) | `GameClock` 채널 |
| 직접 deltaTime | 게임플레이 코드의 `Time.deltaTime`, `Time.time` | 채널 `DeltaTime` |
| Core의 Unity 의존 | `Scripts/Core/` 안의 `MonoBehaviour`, `GameObject`, `Transform`, `Physics2D`, `Time.`, `UnityEngine.Random`, `Debug.Log` | 순수 클래스로, 값은 인자로 받기 |
| 난수 | `UnityEngine.Random`, `new System.Random()` (주입 없이) | `IRandom` 주입 |
| Unity null 함정 | `UnityEngine.Object` 파생 타입 변수에 `?.`, `??`, `??=`, `is null` | `!= null` / `== null` |
| 풀 객체 파괴 | 풀에서 얻은 객체의 `Destroy(` | `Release` |
| 문자열 호출 | `SendMessage(`, `Invoke("`, `animator.SetTrigger("` 등 문자열 반복 | 직접 호출, `Animator.StringToHash` 상수 |
| Resources | `Resources.Load` | 직렬화 참조 |
| 직접 로그 | `Debug.Log` (GameLog 내부 제외) | `GameLog` |
| 매 프레임 할당 | Update 계열 안의 `new List`, LINQ(`.Where(`, `.Select(`, `.ToList(`), 문자열 `+`/보간, 람다 | 버퍼 재사용, NonAlloc |
| 물리 할당 | `Physics2D.RaycastAll`, `OverlapCircleAll`, `BoxCastAll` | 배열 버퍼 오버로드 + `ContactFilter2D` |
| 이벤트 해제 누락 | `+=` 로 구독했는데 같은 클래스에 `-=` 가 없음 | `OnDisable`/`OnDestroy` 에서 해제 |
| 직렬화 이름 변경 | diff에서 `[SerializeField]` 필드 이름이 바뀜 | `[FormerlySerializedAs("old")]` |
| 매직 넘버 | 게임플레이 코드의 속도·시간·데미지 숫자 리터럴 (0, 1, -1 제외) | `*Config` SO 필드 |

2. 추가 확인 (파일 단위):
   - AI가 `.unity` / `.prefab` / `.asset` / `.controller` / `.anim` / `.playable` 을 텍스트로 고쳤으면 위반 (현민의 에디터 작업 결과는 제외)
   - 디버그 코드가 `#if UNITY_EDITOR` / `[Conditional]` 로 빌드에서 빠지는가
   - 파일 하나에 public 타입 하나, 파일 이름 = 타입 이름

3. 결과를 아래 형식으로만 보고한다. 문제가 없으면 "Unity 함정 문제 없음" 한 줄.

```
파일:줄 | 문제 | 대체 코드
```

직접 수정 요청이 없으면 고치지 않는다 (리뷰어는 절대 수정하지 않는다).
