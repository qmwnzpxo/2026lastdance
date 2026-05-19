//using UnityEngine;

//public class SaveManager : PersistentSingleton<SaveManager>
//{
//    private const string SAVE_KEY = "SaveData";

//    public void Save(SaveData data)
//    {
//        string json = JsonUtility.ToJson(data);
//        PlayerPrefs.SetString(SAVE_KEY, json);
//        PlayerPrefs.Save();
//    }

//    public SaveData Load()
//    {
//        if (!PlayerPrefs.HasKey(SAVE_KEY))
//            return new SaveData();

//        string json = PlayerPrefs.GetString(SAVE_KEY);
//        return JsonUtility.FromJson<SaveData>(json);
//    }
//}
using System.IO;
using UnityEngine;

/// <summary>
/// 영구 저장 데이터를 JSON 파일로 관리하는 싱글톤.
/// 저장 위치: Application.persistentDataPath/save.json
/// </summary>
public class SaveManager : PersistentSingleton<SaveManager>
{
    private string _savePath;

    // ── 저장 ──────────────────────────────────────────────────
    public void Save(SaveData data)
    {
        string json = JsonUtility.ToJson(data, prettyPrint: true);
        File.WriteAllText(_savePath, json);
        Debug.Log($"[SaveManager] 저장 완료: {_savePath}");
    }

    // ── 로드 ──────────────────────────────────────────────────
    /// <summary>
    /// 저장 파일이 없으면 기본값 SaveData를 반환한다.
    /// </summary>
    public SaveData Load()
    {
        if (!File.Exists(_savePath))
        {
            Debug.Log("[SaveManager] 저장 파일 없음 → 기본값 반환");
            return new SaveData();
        }

        string json = File.ReadAllText(_savePath);
        return JsonUtility.FromJson<SaveData>(json);
    }

    // ── 삭제 (디버그/초기화용) ────────────────────────────────
    public void DeleteSave()
    {
        if (File.Exists(_savePath))
            File.Delete(_savePath);
        Debug.Log("[SaveManager] 저장 파일 삭제");
    }

    // ── 초기화 ────────────────────────────────
    protected override void Awake()
    {
        base.Awake();
        _savePath = Path.Combine(Application.persistentDataPath, "save.json");
    }
}