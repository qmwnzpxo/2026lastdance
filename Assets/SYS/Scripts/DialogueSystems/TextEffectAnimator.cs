using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace DialogueSystem
{
    /// <summary>
    /// TMP 버텍스를 직접 조작해 shake / wave 효과를 구현합니다.
    /// DialogueUI와 같은 오브젝트에 부착
    /// </summary>
    public class TextEffectAnimator : MonoBehaviour
    {
        [Header("Shake")]
        [Tooltip("흔들림 강도 (픽셀 단위)")]
        [SerializeField] private float shakeIntensity = 1.5f;

        [Header("Wave")]
        [Tooltip("파동 높이 (픽셀 단위)")]
        [SerializeField] private float waveAmplitude = 4f;
        [Tooltip("글자 사이 위상 차이 (클수록 물결이 촘촘)")]
        [SerializeField] private float waveFrequency = 1.5f;
        [Tooltip("파동 속도")]
        [SerializeField] private float waveSpeed = 3f;

        private TextMeshProUGUI _text;
        private List<EffectRange> _ranges = new();
        private bool _active;

        public void SetEffectRanges(List<EffectRange> ranges, TextMeshProUGUI text)
        {
            _ranges = ranges;
            _text   = text;
            _active = ranges != null && ranges.Count > 0;
        }

        public void Clear()
        {
            _ranges.Clear();
            _text   = null;
            _active = false;
        }

        void Update()
        {
            if (!_active || _text == null) return;

            _text.ForceMeshUpdate();
            var info  = _text.textInfo;
            bool dirty = false;

            foreach (var range in _ranges)
            {
                for (int i = range.StartChar; i <= range.EndChar && i < info.characterCount; i++)
                {
                    var ch = info.characterInfo[i];
                    if (!ch.isVisible) continue;

                    var verts = info.meshInfo[ch.materialReferenceIndex].vertices;
                    int vi    = ch.vertexIndex;

                    switch (range.Type)
                    {
                        case TextEffectType.Shake: ApplyShake(verts, vi);   break;
                        case TextEffectType.Wave:  ApplyWave(verts, vi, i); break;
                    }
                    dirty = true;
                }
            }

            if (!dirty) return;
            // 수정된 vertices 배열을 메시에 반드시 복사한 뒤 UpdateGeometry 호출
            for (int i = 0; i < info.meshInfo.Length; i++)
            {
                info.meshInfo[i].mesh.vertices = info.meshInfo[i].vertices;
                _text.UpdateGeometry(info.meshInfo[i].mesh, i);
            }
        }

        void ApplyShake(Vector3[] v, int vi)
        {
            var offset = (Vector3)(Random.insideUnitCircle * shakeIntensity);
            for (int k = 0; k < 4; k++) v[vi + k] += offset;
        }

        void ApplyWave(Vector3[] v, int vi, int charIndex)
        {
            float y = Mathf.Sin(Time.time * waveSpeed + charIndex * waveFrequency) * waveAmplitude;
            for (int k = 0; k < 4; k++) v[vi + k] += new Vector3(0, y, 0);
        }
    }
}
