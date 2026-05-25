using System.Collections.Generic;
using UnityEngine;

// 거점 씬 저장을 위해 만든 세이브 매니저
public class HubSaveManager : MonoBehaviour
{
    [SerializeField] private PlayerState _playerState;
    [SerializeField] private PlayerMove _playerMove;
    [SerializeField] private PlayerCombat _playerCombat;
    [SerializeField] private PlayerDash _playerDash;

    private void Awake()
    {
        if(_playerState == null)
            _playerState = FindFirstObjectByType<PlayerState>();
    }

    private SaveData BuildSaveData()
    {
        // 기존 저장 데이터를 베이스로 사용 (진행도, 카르마 등 유지)
        SaveData data = SaveManager.Instance.Load();

        // 거점에서 수정 가능한 항목만 덮어씀
        data.maxHP = _playerState.MaxHP;
        data.maxStamina = _playerState.maxStamina;
        data.attackPower = _playerState.damage;
        data.moveSpeed = _playerMove.MoveSpeed;
        data.jumpForce = _playerMove.JumpForce;



        return data;
    }

    // 스탯 확정 버튼 클릭 시
    public void OnConfirmStats()
    {
        SaveManager.Instance.Save(BuildSaveData());
    }

    // 챕터 시작 버튼 클릭 시
    public void OnStartChapter()
    {
        SaveData data = BuildSaveData();
        data.currentChip = PlayerChip.pink; // 챕터 시작 시 기본 칩으로 초기화
        _playerState.ownedChips = new HashSet<PlayerChip>() { PlayerChip.pink }; // 보유 칩도 초기화
        SaveManager.Instance.Save(data);
        // SceneManager.LoadScene("첫번째 씬");
    }
}