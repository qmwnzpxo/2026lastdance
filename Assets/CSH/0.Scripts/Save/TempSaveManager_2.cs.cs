using UnityEngine;

public class TempSaveManager_2 : TempSaveManager
{
    // "진행 중" 플래그 키만 PlayerPrefs에 유지
    private const string SESSION_KEY = "StageSessionActive";

    private TempSaveData _data;
    public TempSaveData Data => _data;


    // ─── 초기화(게임 시작) ────────────────────────────────────
    #region
    protected override void Awake()
    {
        base.Awake();

        // 이전 세션에 플래그가 남아있으면 = 비정상 종료
        // 임시 데이터 없이 새로 시작
        if (PlayerPrefs.HasKey(SESSION_KEY))
        {
            Debug.Log("[TempSave] 이전 세션 비정상 종료 감지 → 초기화");
            PlayerPrefs.DeleteKey(SESSION_KEY);
        }

        _data = new TempSaveData();
    }
    #endregion

    // ─── 세션 관리 ───────────────────────────────────────────
    #region
    // 스테이지 시작 시 호출
    public void BeginSession()
    {
        _data = new TempSaveData();
        PlayerPrefs.SetInt(SESSION_KEY, 1);
        PlayerPrefs.Save();
        Debug.Log("[TempSave] 세션 시작");
    }
    #endregion

    // ─── 파편, 카르마 획득 ────────────────────────────────────
    #region
    // AddShard, RecordChoice 등 → SaveToDisk() 호출 없음, 메모리만 수정
    public void AddShard(int amount)
    {
        if (amount <= 0) return;
        _data.pendingShard += amount;
        Debug.Log($"[TempSave] 샤드 획득: +{amount} (누적: {_data.pendingShard})");
    }

    // RecordChoice에서 karmaChange로 카르마 변화 기록 → AddKarma()는 별도 호출 가능
    public void AddKarma(float amount) => _data.pendingKarma += amount;

    public void RecordChoice(string choiceId, int selectedIndex, float karmaChange = 0f)
    {
        var existing = _data.choices.Find(c => c.choiceId == choiceId);
        if (existing != null)
        {
            _data.pendingKarma -= existing.karmaChange;
            existing.selectedIndex = selectedIndex;
            existing.karmaChange = karmaChange;
        }
        else
        {
            _data.choices.Add(new StageChoice
            {
                choiceId = choiceId,
                selectedIndex = selectedIndex,
                karmaChange = karmaChange
            });
        }
        _data.pendingKarma += karmaChange;
    }
    #endregion

    // ─── 이벤트 완료 ───────────────────────────────────────
    #region
    public void CompleteEvent(string eventId)
    {
        if (!_data.completedEvents.Contains(eventId))
            _data.completedEvents.Add(eventId);
    }
    public bool IsEventCompleted(string eventId)
        => _data.completedEvents.Contains(eventId);
    #endregion


    // ─── 스테이지 종료 ───────────────────────────────────────
    #region
    // 스테이지 클리어 → SaveData에 반영 후 플래그 제거
    public void CommitAndClear()
    {
        SaveData saveData = SaveManager.Instance.Load();

        saveData.currentStage++;
        saveData.karma += _data.pendingKarma;
        saveData.totalShard += _data.pendingShard;

        foreach (var choice in _data.choices)
        {
            if (choice.karmaChange < 0f) saveData.brutalChoiceCount++;

            var existing = saveData.choices.Find(c => c.choiceId == choice.choiceId);
            if (existing != null)
            {
                existing.selectedIndex = choice.selectedIndex;
                existing.karmaChange = choice.karmaChange;
            }
            else saveData.choices.Add(choice);
        }

        SaveManager.Instance.Save(saveData);

        // 정상 클리어 → 플래그 제거
        PlayerPrefs.DeleteKey(SESSION_KEY);
        PlayerPrefs.Save();

        _data = new TempSaveData();
        Debug.Log($"[TempSave] 클리어 확정 → 샤드: {saveData.totalShard}, 카르마: {saveData.karma}");
    }

    // 챕터 재시작
    public void Clear()
    {
        PlayerPrefs.DeleteKey(SESSION_KEY);
        PlayerPrefs.Save();
        _data = new TempSaveData();
        Debug.Log("[TempSave] 세션 초기화");
    }
    #endregion

}