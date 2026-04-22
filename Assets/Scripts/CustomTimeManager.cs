using UnityEngine;

public class CustomTimeManager : PersistentSingleton<CustomTimeManager>
{
    // 플레이어에게 적용되는 시간 배율 (기본값 1)
    public float PlayerTimeScale { get; private set; } = 1f;

    
    void Start()
    {
        
    }

    void Update()
    {
        
    }
}
