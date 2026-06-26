using UnityEngine;
using UnityEngine.UI;

// HUDAttackMode — drives the four per-mode HUD shaders (등장 intro / 반복 loop / 소멸 outro).
//
// Setup:
//   - Canvas (Screen Space - Overlay).
//   - Four full-screen RawImages, each with a Material using one of:
//       UI/HUD_Ichisen, UI/HUD_Promotion, UI/HUD_Rensen, UI/HUD_Rakusen
//   - Assign them below. Leave them disabled; the manager enables the active one.
//
// Runtime:
//   SetMode(Mode.Rakusen);  // 공격모드 진입 → 오프닝(등장) 재생 후 반복
//   Replay();               // 현재 모드 오프닝 다시 재생
//   Deactivate();           // 소멸(off)
//
// Test keys (Editor/Build): 1=일섬 2=프로모션 3=연섬 4=낙섬, 0=OFF(소멸), Space=오프닝 리플레이.
//   * New Input System only? Set Player Settings > Active Input Handling = "Both",
//     or wire these to your own input and call the public methods.
public class HUDAttackMode : MonoBehaviour
{
    public enum Mode { Ichisen, Promotion, Rensen, Rakusen } // 일섬, 프로모션, 연섬, 낙섬

    [Header("Per-mode full-screen RawImages")]
    public RawImage ichisen;
    public RawImage promotion;
    public RawImage rensen;
    public RawImage rakusen;

    [Header("Timing (seconds)")]
    public float introTime = 0.6f;   // 등장
    public float outroTime = 0.45f;  // 소멸

    [Header("Test Keys")]
    public bool    enableTestKeys = true;
    public KeyCode keyIchisen   = KeyCode.Alpha1;
    public KeyCode keyPromotion = KeyCode.Alpha2;
    public KeyCode keyRensen    = KeyCode.Alpha3;
    public KeyCode keyRakusen   = KeyCode.Alpha4;
    public KeyCode keyReplay    = KeyCode.Space; // 오프닝 리플레이
    public KeyCode keyOff       = KeyCode.Alpha0; // 소멸 off

    RawImage[] _all;
    RawImage   _cur;
    Material   _mat;
    Mode       _curMode;
    float _t;
    int   _state; // 0 idle, 1 intro, 2 loop, 3 outro

    void Awake()
    {
        _all = new[] { ichisen, promotion, rensen, rakusen };
        foreach (var r in _all)
        {
            if (r == null) continue;
            r.material = new Material(r.material); // instance per image
            r.enabled = false;
        }
    }

    void Update()
    {
        if (enableTestKeys) HandleTestKeys();

        if (_cur == null) return;
        _mat.SetFloat("_Aspect", (float)Screen.width / Mathf.Max(1, Screen.height));

        if (_state == 1) // intro: _Anim 0->1
        {
            _t += Time.deltaTime;
            float k = Mathf.Clamp01(_t / Mathf.Max(0.0001f, introTime));
            _mat.SetFloat("_Anim", k);
            _mat.SetFloat("_Fade", 1f);
            if (k >= 1f) _state = 2;
        }
        else if (_state == 3) // outro: _Fade 1->0
        {
            _t += Time.deltaTime;
            float k = Mathf.Clamp01(_t / Mathf.Max(0.0001f, outroTime));
            _mat.SetFloat("_Fade", 1f - k);
            if (k >= 1f) { _cur.enabled = false; _cur = null; _state = 0; }
        }
    }

    void HandleTestKeys()
    {
        if (Input.GetKeyDown(keyIchisen))   SetMode(Mode.Ichisen);
        if (Input.GetKeyDown(keyPromotion)) SetMode(Mode.Promotion);
        if (Input.GetKeyDown(keyRensen))    SetMode(Mode.Rensen);
        if (Input.GetKeyDown(keyRakusen))   SetMode(Mode.Rakusen);
        if (Input.GetKeyDown(keyReplay))    Replay();
        if (Input.GetKeyDown(keyOff))       Deactivate();
    }

    RawImage Pick(Mode m)
    {
        switch (m)
        {
            case Mode.Ichisen:   return ichisen;
            case Mode.Promotion: return promotion;
            case Mode.Rensen:    return rensen;
            case Mode.Rakusen:   return rakusen;
        }
        return promotion;
    }

    // 공격모드 진입 — 해당 모드 오프닝(등장) 재생
    public void SetMode(Mode m)
    {
        foreach (var r in _all) if (r != null && r != Pick(m)) r.enabled = false;
        _cur = Pick(m);
        if (_cur == null) { Debug.LogWarning("[HUDAttackMode] RawImage for " + m + " not assigned."); return; }
        _curMode = m;
        _mat = _cur.material;
        _cur.enabled = true;
        StartIntro();
    }

    // 현재 모드 오프닝 다시 재생
    public void Replay()
    {
        if (_cur == null) return;
        StartIntro();
    }

    void StartIntro()
    {
        _mat.SetFloat("_Fade", 1f);
        _mat.SetFloat("_Anim", 0f);
        _t = 0f; _state = 1;
    }

    // 공격모드 해제 — 소멸(off)
    public void Deactivate()
    {
        if (_cur == null) return;
        _t = 0f; _state = 3;
    }

    public bool IsActive => _cur != null && _state != 3;
    public Mode CurrentMode => _curMode;
}
