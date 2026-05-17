// AfterImagePool.cs
// 씬 단위 싱글턴 풀 — AfterImageSpawner들이 공유해서 사용합니다.
// 씬에 빈 GameObject 하나 만들고 이 컴포넌트를 붙여주세요.

using System.Collections.Generic;
using UnityEngine;

public class AfterImagePool : MonoBehaviour
{
    public static AfterImagePool Instance { get; private set; }

    [Tooltip("씬 시작 시 미리 생성해둘 풀 크기. 대시 같은 짧은 연출은 15~20 권장.")]
    [SerializeField] private int initialSize = 20;

    private readonly Queue<AfterImageInstance> _free = new();

    // ── 초기화 ──────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        for (int i = 0; i < initialSize; i++)
            _free.Enqueue(CreateInstance());
    }

    // ── 내부 생성 ────────────────────────────────────────────────────────────

    AfterImageInstance CreateInstance()
    {
        var go = new GameObject("AfterImage_pooled");
        go.SetActive(false);
        go.transform.SetParent(transform); // Pool 오브젝트 하위에 정리
        return go.AddComponent<AfterImageInstance>();
    }

    // ── 외부 인터페이스 ──────────────────────────────────────────────────────

    /// <summary>풀에서 인스턴스 하나를 빌립니다.</summary>
    public AfterImageInstance Rent()
    {
        if (_free.Count > 0)
        {
            var inst = _free.Dequeue();
            inst.gameObject.SetActive(true);
            return inst;
        }

        // 풀 소진 — 추가 할당 (빈번하면 initialSize 늘릴 것)
        Debug.LogWarning("[AfterImagePool] Pool exhausted — allocating new instance. initialSize를 늘려주세요.");
        var newInst = CreateInstance();
        newInst.gameObject.SetActive(true);
        return newInst;
    }

    /// <summary>사용이 끝난 인스턴스를 풀에 반납합니다.</summary>
    public void Return(AfterImageInstance inst)
    {
        inst.gameObject.SetActive(false);
        _free.Enqueue(inst);
    }
}
