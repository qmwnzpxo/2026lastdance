using System.Collections.Generic;

// 영구 저장 데이터. JSON 파일로 저장된다.
[System.Serializable]
public class SaveData
{
    // ── 진행도 ───────────────────────────
    public int currentChapter;
    public int currentStage;

    // ── 구역 저장 (SectionSave) ───────────
    public string sectionScene = "";  // 마지막으로 저장된 챕터 씬 이름. 비어 있으면 저장 없음
    public string sectionName = "";   // 그 씬의 구역 오브젝트 이름 (SpawnPoint의 부모, 예: "1-3-L"). 비어 있으면 씬 시작 위치
    public SectionPlayerState sectionPlayer = new SectionPlayerState(); // 구역 저장 시점의 플레이어 상태

    // ── 기본 스탯 ────────────────────────
    public float baseMaxHP = 100f;
    public float baseMaxStamina = 100f;
    public float baseMaxDashGauge = 100f;
    public float baseRegenStamina = 10f;
    public float baseDashGaugeGain = 10f;
    public float baseMoveSpeed = 10f;
    public float baseRunSpeed = 20f;
    public float baseJumpForce = 5f;
    public float baseDamage = 3f;
    public float baseAttackSpeed = 1f;
    public float baseDashBox = 1f;
    public float baseAttackBox = 1f;

    // ── 업그레이드 레벨 ──────────────────────
    public int[] upgradeLevels = new int[(int)UpgradeStat.Length];

    // ── 선택지 누적 기록 ───────────────────
    public List<StageChoice> choices = new List<StageChoice>();

    // ── 카르마 ────────────────────────────
    public float karma;         // -100 ~ 100

    // ── 동기화 파편 ───────────────────────
    public float totalShard;    // 총 획득 파편
    public float usedShard;     // 스탯에 사용한 파편
    public float RemainShard => totalShard - usedShard; // 남은 파편 = 총 획득 - 사용

    // ── 적 난이도 누적치 ──────────────────
    public int brutalChoiceCount;   // 잔인한 선택지 횟수
    public float difficultyScore;   // 난이도 점수

}

/// <summary>구역 저장 시점의 플레이어 상태 (HP·스태미나·대시 게이지·섬광탄·칩)</summary>
[System.Serializable]
public class SectionPlayerState
{
    public bool valid;   // JsonUtility는 null을 저장하지 않으므로 저장 여부를 따로 표시
    public float hp;
    public float stamina;
    public float dashGauge;
    public int flashCount;
    public List<PlayerChip> ownedChips = new List<PlayerChip>();
    public PlayerChip currentChip = PlayerChip.pink;
}