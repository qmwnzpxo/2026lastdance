using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

public class BossBase : MonoBehaviour
{
    [Header("Stats")]
    public float maxHp = 150f;
    public float currentHp;

    [Header("References")]
    [SerializeField] private BossShield bossShield;
    [SerializeField] private LazerPatten lazerPatten;
    [SerializeField] private Panel panel;

    private CancellationTokenSource _lifeCts;
    private CancellationTokenSource _patternCts;

    public bool IsShieldActive { get; private set; } = true;
    public bool IsDead { get; private set; } = false;

    int number = 0;
    private bool _battleLoopStarted = false;
    private int _currentPatternIndex = 0;
    private float battleStartDelay = 3f;
    private void Awake()
    {
        currentHp = maxHp;
        _lifeCts = new CancellationTokenSource();
    }

    private void Start()
    {
        BossLoop(_lifeCts.Token).Forget();
    }

    private void OnDestroy()
    {
        _lifeCts.Cancel();
        _lifeCts.Dispose();
        _patternCts?.Cancel();
        _patternCts?.Dispose();
    }

    //private async UniTaskVoid BossLoop(CancellationToken lifeToken)
    //{
    //    await UniTask.Delay(1000, cancellationToken: lifeToken);

    //    ActivateShield(true);

    //    while (!IsDead && !lifeToken.IsCancellationRequested)
    //    {
    //        await RunShieldPhase(lifeToken);


    //        if (IsDead) break;
    //        await RunVulnerablePhase(lifeToken);
    //    }

    //    await OnDead(lifeToken);
    //}

    public void StartBattle()
    {
        if (_battleLoopStarted) return; // 이미 시작됐으면 무시
        _battleLoopStarted = true;
        BossLoopImmediate(_lifeCts.Token).Forget();
    }
    private void StartBattleTest() => StartBattle();

    private async UniTaskVoid BossLoop(CancellationToken lifeToken)
    {
        await UniTask.Delay((int)(battleStartDelay * 1000), cancellationToken: lifeToken);

        if (_battleLoopStarted) return; // StartBattle이 먼저 호출됐으면 중단
        _battleLoopStarted = true;

        await RunBattle(lifeToken);
    }
    private async UniTaskVoid BossLoopImmediate(CancellationToken lifeToken)
    {
        await RunBattle(lifeToken);
    }
    private async UniTask RunBattle(CancellationToken lifeToken)
    {
        ActivateShield(true);

        while (!IsDead && !lifeToken.IsCancellationRequested)
        {
            await RunShieldPhase(lifeToken);

            if (IsDead) break;

            await RunVulnerablePhase(lifeToken);
        }

        await OnDead(lifeToken);
    }
    private async UniTask RunShieldPhase(CancellationToken lifeToken)
    {
        _patternCts = CancellationTokenSource.CreateLinkedTokenSource(lifeToken);
        CancellationToken pt = _patternCts.Token;

        try
        {
            switch (_currentPatternIndex)
            {
                case 0:
                    lazerPatten?.ResetLazers();
                    await RunLazerPattern(pt);
                    break;
                case 1:
                    panel?.ResetPanel();
                    await RunPanelPattern(pt);
                    break;
            }

            ActivateShield(false);
        }
        finally 
        { 
  
            _patternCts.Dispose();
            _patternCts = null;
        }
    }



    private async UniTask RunVulnerablePhase(CancellationToken lifeToken)
    {
        // 취약 구간 동안 HP 체크
        float vulnerableDuration = 8f;
        float elapsed = 0f;

        while (elapsed < vulnerableDuration && !IsDead && !lifeToken.IsCancellationRequested)
        {
            elapsed += Time.deltaTime;
            await UniTask.Yield(lifeToken);
        }

        if (!IsDead)
        {
           
            _currentPatternIndex = (_currentPatternIndex + 1) % 2;
            ActivateShield(true);
            Debug.Log("방어막 재활성화");
        }
    }


    private async UniTask RunLazerPattern(CancellationToken token)
    {
        if (lazerPatten == null) return;
        lazerPatten.gameObject.SetActive(true);
        lazerPatten.ResetLazers(); // 아래 확장 메서드 참고

        await UniTask.WaitUntil(
            () => lazerPatten.allDestory,
            cancellationToken: token
        );

        Debug.Log("레이저 파훼 완료");
    }

    private async UniTask RunPanelPattern(CancellationToken token)
    {
        if (panel == null) return;

        // 패턴 루프는 Forget으로 띄우고
        panel.StartPatternAsync(token).Forget();

        // 파훼 조건(패널 오브젝트 전부 파괴)만 별도로 대기
        await UniTask.WaitUntil(
            () => panel.IsPanelCleared,
            cancellationToken: token
        );

        Debug.Log("패널 파훼 완료");
    }
    public void TakeDamage(float dmg)
    {
        if (IsShieldActive) return; // 무적
        if (IsDead) return;

        currentHp -= dmg;
        Debug.Log($"[Boss] HP: {currentHp}/{maxHp}");

        if (currentHp <= 0f)
        {
            currentHp = 0f;
            IsDead = true;
        }
    }

    private void ActivateShield(bool active)
    {
        IsShieldActive = active;
        if (bossShield != null)
            bossShield.SetShieldActive(active);

        Debug.Log($"방어막 {(active ? "ON" : "OFF")}");
    }

    private async UniTask OnDead(CancellationToken token)
    {
        Debug.Log("[Boss] 사망");
       
        await UniTask.Delay(1000, cancellationToken: CancellationToken.None);
        gameObject.SetActive(false);
    }
}

