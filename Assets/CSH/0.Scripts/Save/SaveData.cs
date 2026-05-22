using System.Collections.Generic;

// 영구 저장 데이터. JSON 파일로 저장된다.
[System.Serializable]
public class SaveData
{
    // ── 진행도 ───────────────────────────
    public int currentChapter;
    public int currentStage;
    
    // ── 영구 스탯 ────────────────────────
    public float maxHP;
    public float maxStamina;
    public float maxDashGauge;
    public float GaugePerAttack;
    public float moveSpeed;
    public float jumpForce;
    public float dashRange;
    public float attackPower;
    public float attackSpeed;
    public float attackRange;

    // ── 칩 ────────────────────────────────
    public PlayerChip currentChip;

    // ── 선택지 누적 기록 ───────────────────
    public List<StageChoice> choices = new List<StageChoice>();

    // ── 카르마 ────────────────────────────
    public float karma;         // -100 ~ 100

    // ── 동기화 파편 ───────────────────────
    public float totalShard;    // 총 획득 파편

    public float usedShard;     // 스탯에 사용한 파편

    // ── 적 난이도 누적치 ──────────────────
    public int brutalChoiceCount;   // 잔인한 선택지 횟수
    public float difficultyScore;   // 난이도 점수

}