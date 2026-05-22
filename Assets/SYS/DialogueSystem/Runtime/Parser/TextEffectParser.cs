using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DialogueSystem
{
    // ──────────────────────────────────────────────────────────────────────────
    // 지원하는 커스텀 태그 목록
    // ──────────────────────────────────────────────────────────────────────────
    //  [shake]  [/shake]    ── 글자 흔들기
    //  [wave]   [/wave]     ── 물결 움직임
    //  [rainbow][/rainbow]  ── 무지개 색상 순환
    //  [bigpop] [/bigpop]   ── 커졌다 줄어드는 팝 효과
    //  [break]  [/break]    ── 글자 산산조각
    //  [fast]   [/fast]     ── 타이프라이터 빠르게
    //  [slow]   [/slow]     ── 타이프라이터 느리게
    //  [pause=1.5]          ── 해당 위치에서 1.5초 대기
    //  [color=#FF6600] [/color] ── TMP color 태그로 변환
    //  [size=150]  [/size]  ── TMP size 태그로 변환 (%)
    //  [b]  [/b]            ── 볼드
    //  [i]  [/i]            ── 이탤릭
    //  [n]                  ── 줄바꿈
    // ──────────────────────────────────────────────────────────────────────────

    public enum TextEffectType
    {
        Shake,
        Wave,
        Rainbow,
        BigPop,
        Break,
        Typefast,
        Typeslow,
    }

    [Serializable]
    public struct EffectRegion
    {
        public TextEffectType type;
        public int startChar;   // TMP visible char 인덱스 (시작)
        public int endChar;     // TMP visible char 인덱스 (끝, 포함)
        public string param;
    }

    public struct ParsedDialogueText
    {
        /// <summary>TMP에 그대로 넣을 텍스트 (TMP 리치텍스트 태그 포함)</summary>
        public string cleanText;

        /// <summary>프레임마다 버텍스를 조작해야 하는 애니메이션 이펙트 목록</summary>
        public List<EffectRegion> effects;

        /// <summary>특정 글자 위치에서 타이프라이터를 멈춰야 하는 목록 (charIdx, 초)</summary>
        public List<(int charIdx, float duration)> pauses;
    }

    /// <summary>
    /// 커스텀 태그를 파싱해 cleanText + EffectRegion 목록을 반환.
    /// CSV나 인스펙터에 입력한 rawText를 DisplayEntry 직전에 한 번 호출.
    /// </summary>
    public static class TextEffectParser
    {
        private static readonly Regex _tagRx = new Regex(
            @"\[(?<close>/)?(?<tag>[a-zA-Z]+)(?:=(?<param>[^\]]*))?\]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static ParsedDialogueText Parse(string rawText)
        {
            var result = new ParsedDialogueText
            {
                effects = new List<EffectRegion>(),
                pauses  = new List<(int, float)>()
            };

            if (string.IsNullOrEmpty(rawText))
            {
                result.cleanText = string.Empty;
                return result;
            }

            var sb = new StringBuilder();

            // 열린 커스텀 이펙트 태그 스택 (type, 시작 visible char index, param)
            var stack = new Stack<(TextEffectType type, int startChar, string param)>();

            int charCount = 0; // visible char 카운터 (TMP 기준)
            int pos       = 0;

            while (pos < rawText.Length)
            {
                var m = _tagRx.Match(rawText, pos);

                // 태그가 더 없으면 나머지 텍스트 전부 출력
                if (!m.Success)
                {
                    var tail = rawText[pos..];
                    sb.Append(tail);
                    charCount += VisibleLen(tail);
                    break;
                }

                // 태그 앞의 일반 텍스트
                if (m.Index > pos)
                {
                    var plain = rawText[pos..m.Index];
                    sb.Append(plain);
                    charCount += VisibleLen(plain);
                }
                pos = m.Index + m.Length;

                bool   isClose = m.Groups["close"].Success;
                string tag     = m.Groups["tag"].Value.ToLower();
                string param   = m.Groups["param"].Value;

                switch (tag)
                {
                    // ── 커스텀 애니메이션 이펙트 ──────────────────────────────
                    case "shake":   HandleCustom(TextEffectType.Shake,    isClose, param, stack, result.effects, ref charCount); break;
                    case "wave":    HandleCustom(TextEffectType.Wave,     isClose, param, stack, result.effects, ref charCount); break;
                    case "rainbow": HandleCustom(TextEffectType.Rainbow,  isClose, param, stack, result.effects, ref charCount); break;
                    case "bigpop":  HandleCustom(TextEffectType.BigPop,   isClose, param, stack, result.effects, ref charCount); break;
                    case "break":   HandleCustom(TextEffectType.Break,    isClose, param, stack, result.effects, ref charCount); break;
                    case "fast":    HandleCustom(TextEffectType.Typefast, isClose, param, stack, result.effects, ref charCount); break;
                    case "slow":    HandleCustom(TextEffectType.Typeslow, isClose, param, stack, result.effects, ref charCount); break;

                    // ── TMP 태그로 변환 ────────────────────────────────────────
                    case "color":
                        sb.Append(isClose ? "</color>" : $"<color={param}>");
                        break;
                    case "size":
                        sb.Append(isClose ? "</size>" : $"<size={param}%>");
                        break;
                    case "b":
                        sb.Append(isClose ? "</b>" : "<b>");
                        break;
                    case "i":
                        sb.Append(isClose ? "</i>" : "<i>");
                        break;

                    // ── 특수 단독 태그 ─────────────────────────────────────────
                    case "pause":
                        if (float.TryParse(param,
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out float dur))
                        {
                            result.pauses.Add((charCount, dur));
                        }
                        break;
                    case "n":
                        sb.Append('\n');
                        charCount++;
                        break;

                    default:
                        // 알 수 없는 태그는 그대로 출력
                        sb.Append(m.Value);
                        break;
                }
            }

            result.cleanText = sb.ToString();
            return result;
        }

        // ── 헬퍼 ────────────────────────────────────────────────────────────────

        private static void HandleCustom(
            TextEffectType type, bool isClose, string param,
            Stack<(TextEffectType, int, string)> stack,
            List<EffectRegion> effects,
            ref int charCount)
        {
            if (!isClose)
            {
                stack.Push((type, charCount, param));
            }
            else if (stack.Count > 0)
            {
                var top = stack.Pop();
                effects.Add(new EffectRegion
                {
                    type      = top.Item1,
                    startChar = top.Item2,
                    endChar   = charCount - 1,
                    param     = top.Item3,
                });
            }
        }

        /// <summary>TMP 리치텍스트 태그를 제외한 실제 표시 글자 수 계산</summary>
        private static int VisibleLen(string s)
        {
            int count  = 0;
            bool inTag = false;
            foreach (char c in s)
            {
                if      (c == '<') inTag = true;
                else if (c == '>') inTag = false;
                else if (!inTag)   count++;
            }
            return count;
        }
    }
}
