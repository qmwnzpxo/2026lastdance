using System.Collections.Generic;
using UnityEngine;

// 거점 씬 저장을 위해 만든 세이브 매니저 
public class HubSaveManager : MonoBehaviour
{
    [SerializeField] private PlayerState playerState;
    [SerializeField] private PlayerMove playerMove;
    [SerializeField] private PlayerCombat playerCombat;
    [SerializeField] private PlayerDash playerDash;
    [SerializeField] private PlayerUpgrader playerUpgrader;

    private void Awake()
    {
        if(playerState == null)
            playerState = FindFirstObjectByType<PlayerState>();
        if(playerMove == null)
            playerMove = FindFirstObjectByType<PlayerMove>();
        if(playerCombat == null)
            playerCombat = FindFirstObjectByType<PlayerCombat>();
        if(playerDash == null)
            playerDash = FindFirstObjectByType<PlayerDash>();
        if(playerUpgrader == null)
            playerUpgrader = FindFirstObjectByType<PlayerUpgrader>();

        playerUpgrader?.LoadAndApply();
    }


    public void OnCommitStats()
    {
        SaveManager.Instance.Save(BuildSaveData());
    }

    public void OnStartChapter()
    {
        SaveData data = BuildSaveData();
        playerState.currentChip = PlayerChip.pink;
        playerState.ownedChips = new HashSet<PlayerChip>() { PlayerChip.pink };
        SaveManager.Instance.Save(data);
        // TODO: 씬 매니저 제작후 현재 저장된 챕터에 맞는 씬으로 이동하는 코드 추가 할것
    }

    private SaveData BuildSaveData()
    {
        SaveData data = SaveManager.Instance.Load();
        return data;
    }

}