using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using DialogSystem.CinematicDialogue;
using System.Threading;
using System;

[System.Serializable]
public struct CinematicActor // 배우
{
    public string actorId;
    public Transform actorTransform;
}

[System.Serializable]
public struct CinematicMoveAction // 좌표 이동
{
    public string eventName;
    public string targetActorId;

    [Header("현재 위치 기준 이동량 (X: 좌/우, Y: 상/하)")]
    public Vector2 moveOffset;
    public float duration;
}

[System.Serializable]
public struct CinematicFlipAction // 좌우 반전
{
    public string eventName;
    public string targetActorId;
    public bool isFaceFlip;         // 체크하면 반전, 해제하면 그대로
}

[System.Serializable]
public struct CinematicAnimAction // 애니메이션
{
    public string eventName;        // JSON에서 호출할 이벤트 이름 (예: "용병A_놀람")
    public string targetActorId;    // 애니메이션을 실행할 배우의 ID
    public string triggerName;      // Animator에 설정해둔 Trigger 파라미터 이름 (예: "Surprise")
    public float waitDuration;      // [추가] 이 애니메이션 연출을 위해 대화를 멈출 시간 (초)
}

public class CinematicActionController : MonoBehaviour
{
    [Header("1. 씬에 등장하는 배우(NPC) 목록")]
    public List<CinematicActor> actors = new List<CinematicActor>();

    [Header("2. 실행할 연출(상대적 이동) 목록")]
    public List<CinematicMoveAction> moveActions = new List<CinematicMoveAction>();

    [Header("3. 실행할 연출(애니메이션) 목록")]
    public List<CinematicAnimAction> animActions = new List<CinematicAnimAction>(); 

    [Header("4. 실행할 연출(좌우 반전) 목록")]
    public List<CinematicFlipAction> flipActions = new List<CinematicFlipAction>();

    private void Start()
    {
        DialogueManager.Instance.onEventTriggered += OnDialogueEvent;
        DialogueManager.Instance.onActionTriggered += OnDialogueActionAsync;
    }

    private void OnDestroy()
    {
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.onEventTriggered -= OnDialogueEvent;
            DialogueManager.Instance.onActionTriggered -= OnDialogueActionAsync;
        }
    }

    private void OnDialogueEvent(string eventName)
    {
        Debug.Log($"[CinematicActionController] 이벤트 감지: {eventName}");

        // 취소 토큰 가져오기
        CancellationToken ct = default;
        if (DialogueManager.Instance != null && DialogueManager.Instance.PlaybackCts != null)
        {
            ct = DialogueManager.Instance.PlaybackCts.Token;
        }

        // 1. 이동 액션 검사
        foreach (var action in moveActions)
        {
            if (action.eventName == eventName)
            {
                Transform actor = GetActor(action.targetActorId);

                if (actor != null)
                {
                    Vector3 targetPos = actor.position + (Vector3)action.moveOffset;
                    MoveNpcAsync(actor, targetPos, action.duration, ct).Forget();
                }
            }
        }

        foreach (var action in flipActions)
        {
            if (action.eventName == eventName)
            {
                Transform actor = GetActor(action.targetActorId);
                if (actor != null)
                {
                    SpriteRenderer spriteRenderer = actor.GetComponentInChildren<SpriteRenderer>();
                    if (spriteRenderer != null)
                    {
                        spriteRenderer.flipX = action.isFaceFlip;
                        Debug.Log($"[CinematicAction] {action.targetActorId}의 시선이 {(action.isFaceFlip ? "왼쪽" : "오른쪽")}으로 변경됨");
                    }
                }
            }
        }

        // 2. [추가됨] 애니메이션 액션 검사
        foreach (var action in animActions)
        {
            if (action.eventName == eventName)
            {
                Transform actor = GetActor(action.targetActorId);

                if (actor != null)
                {
                    // 배우 오브젝트(또는 그 자식)에 있는 Animator 컴포넌트를 찾습니다.
                    Animator anim = actor.GetComponentInChildren<Animator>();
                    if (anim != null)
                    {
                        // 지정된 Trigger를 발동시켜 애니메이션을 재생합니다.
                        anim.SetTrigger(action.triggerName);
                        Debug.Log($"[CinematicActionController] {action.targetActorId}의 '{action.triggerName}' 애니메이션 실행!");
                    }
                    else
                    {
                        Debug.LogWarning($"[CinematicActionController] {action.targetActorId}에게 Animator 컴포넌트가 없습니다!");
                    }
                }
            }
        }
    }

    private async UniTask OnDialogueActionAsync(string actionName, CancellationToken ct)
    {
        // 1. 이동 액션
        foreach (var action in moveActions)
        {
            if (action.eventName == actionName)
            {
                Transform actor = GetActor(action.targetActorId);
                if (actor != null)
                {
                    Vector3 targetPos = actor.position + (Vector3)action.moveOffset;
                    // 이동이 끝날 때까지 대기
                    await MoveNpcAsync(actor, targetPos, action.duration, ct);
                    return;
                }
            }
        }

        foreach (var action in flipActions)
        {
            if (action.eventName == actionName)
            {
                Transform actor = GetActor(action.targetActorId);
                if (actor != null)
                {
                    SpriteRenderer spriteRenderer = actor.GetComponentInChildren<SpriteRenderer>();
                    if (spriteRenderer != null)
                    {
                        spriteRenderer.flipX = action.isFaceFlip;
                        Debug.Log($"[CinematicAction] {action.targetActorId}의 시선이 {(action.isFaceFlip ? "왼쪽" : "오른쪽")}으로 변경됨");
                    }
                }
            }
        }

        // 2. 애니메이션 액션
        foreach (var action in animActions)
        {
            if (action.eventName == actionName)
            {
                Transform actor = GetActor(action.targetActorId);
                if (actor != null)
                {
                    Animator anim = actor.GetComponentInChildren<Animator>();
                    if (anim != null) anim.SetTrigger(action.triggerName);

                    // 연출을 보여주기 위해 설정한 시간만큼 대기
                    await UniTask.Delay(TimeSpan.FromSeconds(action.waitDuration), cancellationToken: ct);
                    return;
                }
            }
        }
    }

    private async UniTask MoveNpcAsync(Transform npc, Vector3 targetPos, float duration, CancellationToken ct)
    {
        if (npc == null) return;

        Vector3 startPos = npc.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            npc.position = Vector3.Lerp(startPos, targetPos, elapsed / duration);
            await UniTask.Yield();
        }

        npc.position = targetPos;
    }

    private Transform GetActor(string actorId)
    {
        foreach (var actor in actors)
        {
            if (actor.actorId == actorId)
            {
                return actor.actorTransform;
            }
        }
        return null;
    }

}