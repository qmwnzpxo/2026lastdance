using System.Collections.Generic;

// 현재 오류로 인해 세이브들은 사용하지 않음
/// <summary>
/// 임시 저장 데이터. PlayerPrefs에 저장되어 게임을 꺼도 유지된다.
/// 스테이지 클리어 시 필요한 데이터를 SaveData로 넘긴 뒤 소멸.
/// 챕터 재시작 시에는 반영 없이 바로 소멸.
/// </summary>
[System.Serializable]
public class TempSaveData
{
    // ── 부활 위치 ─────────────────────────────────────────────
    public float respawnX;
    public float respawnY;
    public bool hasRespawnPoint;

    // ── 스테이지 진행도 ───────────────────────────────────────
    // 스테이지 내에서 완료된 이벤트/구간 ID 목록
    // 씬 재진입 시 이미 클리어한 구간을 건너뛰는 데 사용
    public List<string> completedEvents = new List<string>();

    // ── 선택지 결과 ───────────────────────────────────────────
    public List<StageChoice> choices = new List<StageChoice>();

    // ── 카르마 누적치 ─────────────────────────────────────────
    // 스테이지 진행 중 누적된 카르마 변화량
    // 클리어 시 SaveData.karma에 합산
    public float pendingKarma;


    // ── 파편 누적치 ─────────────────────────────────────────
    // 스테이지 진행 중 얻은 파편 총량 (남은 양은 SaveData.totalShard - SaveData.usedShard로 계산 가능)
    // 클리어 시 SaveData.totalShard에 합산
    public float pendingShard;
}

/// <summary>선택지 1건의 기록.</summary>
[System.Serializable]
public class StageChoice
{
    public string choiceId;       // 선택지 고유 ID (예: "ch1_stage2_fork_A")
    public int selectedIndex;     // 선택한 옵션 인덱스
    public float karmaChange;     // 이 선택으로 발생한 카르마 변화량
}