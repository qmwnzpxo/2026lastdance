using UnityEngine;

/// <summary>
/// 스테이지 진행 중 임시 데이터를 PlayerPrefs로 관리하는 싱글톤.
/// 게임을 꺼도 데이터가 유지되며, 명시적으로 Clear()를 호출해야 소멸한다.
///
/// 저장 시점:
///   - 체크포인트 통과     → RegisterRespawn()
///   - 이벤트/구간 완료    → CompleteEvent()
///   - 선택지 결정         → RecordChoice()
///   - 기타 카르마 변경    → AddKarma()
///
/// 소멸 시점:
///   - 스테이지 클리어     → CommitAndClear()  (SaveData에 반영 후 소멸)
///   - 챕터 재시작         → Clear()           (반영 없이 소멸)
/// </summary>
public class TempSaveManager : PersistentSingleton<TempSaveManager>
{
    private const string TEMP_KEY = "TempSaveData";

    private TempSaveData _data;
    public TempSaveData Data => _data;

    // ────────────────────────────────────────────────────────
    #region 초기화

    protected override void Awake()
    {
        base.Awake();
        _data = LoadFromDisk() ?? new TempSaveData();
    }

    #endregion

    // ────────────────────────────────────────────────────────
    #region 부활 위치

    public void RegisterRespawn(Vector2 position)
    {
        _data.respawnX = position.x;
        _data.respawnY = position.y;
        _data.hasRespawnPoint = true;
        SaveToDisk();
        Debug.Log($"[TempSave] 체크포인트 등록: {position}");
    }

    /// <summary>등록된 체크포인트가 없으면 null 반환 → 호출부에서 스테이지 초기 위치 사용.</summary>
    public Vector2? GetRespawnPosition()
    {
        if (!_data.hasRespawnPoint) return null;
        return new Vector2(_data.respawnX, _data.respawnY);
    }

    #endregion

    // ────────────────────────────────────────────────────────
    #region 선택지 + 카르마

    /// <summary>
    /// 선택지 결정 시 호출. 같은 choiceId 재선택 시 이전 karmaChange를 차감 후 교체.
    /// </summary>
    public void RecordChoice(string choiceId, int selectedIndex, float karmaChange = 0f)
    {
        var existing = _data.choices.Find(c => c.choiceId == choiceId);
        if (existing != null)
        {
            _data.pendingKarma -= existing.karmaChange;
            _data.pendingShard += existing.karmaChange; // 카르마 변화량을 파편으로 환산하여 누적 (예시: 1 카르마 = 1 파편)
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
        SaveToDisk();
        Debug.Log($"[TempSave] 선택지: {choiceId} → {selectedIndex} / 카르마 {karmaChange:+0.#;-0.#} (누적: {_data.pendingKarma})");
    }

    public bool HasChoice(string choiceId)
        => _data.choices.Exists(c => c.choiceId == choiceId);

    public int GetChoice(string choiceId)
    {
        var entry = _data.choices.Find(c => c.choiceId == choiceId);
        return entry != null ? entry.selectedIndex : -1;
    }

    /// <summary>선택지 외 이벤트로 카르마를 직접 변경할 때 호출.</summary>
    public void AddKarma(float amount)
    {
        _data.pendingKarma += amount;
        SaveToDisk();
        Debug.Log($"[TempSave] 카르마 변경: {amount:+0.#;-0.#} (누적: {_data.pendingKarma})");
    }

    public void UsedShard(float amount)
    {
        _data.pendingShard -= amount;
        SaveToDisk();
        Debug.Log($"[TempSave] 파편 사용: {amount} (남은 누적: {_data.pendingShard})");
    }

    public float GetPendingKarma() => _data.pendingKarma;

    public float GetPendingShard() => _data.pendingShard;

    #endregion

    // ────────────────────────────────────────────────────────
    #region 클리어 / 초기화

    /// <summary>
    /// 스테이지 클리어 시 호출.
    /// TempSave의 진행도·선택지·카르마를 SaveData에 반영한 뒤 초기화.
    /// </summary>
    public void CommitAndClear()
    {
        SaveData saveData = SaveManager.Instance.Load();

        // 진행도
        saveData.currentStage++;

        // 카르마 합산
        saveData.karma += _data.pendingKarma;

        // 선택지 누적 (중복 choiceId는 덮어씀)
        foreach (var choice in _data.choices)
        {
            var existing = saveData.choices.Find(c => c.choiceId == choice.choiceId);
            if (existing != null)
            {
                existing.selectedIndex = choice.selectedIndex;
                existing.karmaChange = choice.karmaChange;
            }
            else
            {
                saveData.choices.Add(choice);
            }
        }

        SaveManager.Instance.Save(saveData);
        Debug.Log($"[TempSave] 클리어 확정 → 카르마: {saveData.karma}, 스테이지: {saveData.currentStage}");

        Clear();
    }

    /// <summary>
    /// 챕터 재시작 시 호출. SaveData에 반영하지 않고 바로 초기화.
    /// </summary>
    public void Clear()
    {
        _data = new TempSaveData();
        PlayerPrefs.DeleteKey(TEMP_KEY);
        PlayerPrefs.Save();
        Debug.Log("[TempSave] 임시 저장 초기화");
    }

    #endregion

    // ────────────────────────────────────────────────────────
    #region 이벤트/구간 완료

    public void CompleteEvent(string eventId)
    {
        if (_data.completedEvents.Contains(eventId))
        {
            Debug.LogError($"[TempSave] 이벤트 중복 완료 시도: {eventId}");
            return;
        }
        _data.completedEvents.Add(eventId);
        SaveToDisk();
        Debug.Log($"[TempSave] 이벤트 완료: {eventId}");
    }

    public bool IsEventCompleted(string eventId)
        => _data.completedEvents.Contains(eventId);

    #endregion

    // ────────────────────────────────────────────────────────
    #region 디스크 I/O

    private void SaveToDisk()
    {
        PlayerPrefs.SetString(TEMP_KEY, JsonUtility.ToJson(_data));
        PlayerPrefs.Save();
    }

    private TempSaveData LoadFromDisk()
    {
        if (!PlayerPrefs.HasKey(TEMP_KEY)) return null;
        return JsonUtility.FromJson<TempSaveData>(PlayerPrefs.GetString(TEMP_KEY));
    }

    #endregion
}