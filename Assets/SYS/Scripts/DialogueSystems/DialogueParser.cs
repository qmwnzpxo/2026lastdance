using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DialogueSystem
{
    // ════════════════════════════════════════════════════════════
    //  텍스트 효과 타입 & 데이터
    // ════════════════════════════════════════════════════════════

    public enum TextEffectType { Shake, Wave }

    public struct EffectRange
    {
        public TextEffectType Type;
        public int StartChar;   // 효과 시작 글자 인덱스 (visible 기준)
        public int EndChar;     // 효과 종료 글자 인덱스 (inclusive)
    }

    public class ProcessedText
    {
        public string TmpText;                          // TMP에 전달할 최종 텍스트
        public int VisibleLength;                       // 실제 표시되는 글자 수
        public List<EffectRange> EffectRanges = new();
        public Dictionary<int, float> PauseAt  = new(); // 글자 인덱스 → 멈춤 시간(초)
    }

    // ════════════════════════════════════════════════════════════
    //  DialogueParser  (CSV 파싱 + 태그 처리 통합)
    // ════════════════════════════════════════════════════════════

    public static class DialogueParser
    {
        // ─── CSV ─────────────────────────────────────────────────

        // 헤더 순서: id, speaker, portrait, text, next_id, choices, command, auto_delay, speed_override
        public static Dictionary<string, DialogueEntry> ParseCSV(string csv, out string firstId)
        {
            var result = new Dictionary<string, DialogueEntry>();
            firstId = null;

            foreach (var rawLine in csv.Split('\n'))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("//") || line.StartsWith("id,"))
                    continue;

                var cols = SplitCSVLine(line);
                if (cols.Length < 5 || string.IsNullOrEmpty(cols[0])) continue;

                var entry = new DialogueEntry
                {
                    Id            = cols[0].Trim(),
                    Speaker       = cols[1].Trim(),
                    Portrait      = cols[2].Trim(),
                    RawText       = cols[3].Trim(),
                    NextId        = cols[4].Trim(),
                    Choices       = ParseChoices(cols.Length > 5 ? cols[5] : ""),
                    Command       = cols.Length > 6 ? cols[6].Trim() : "",
                    AutoDelay     = cols.Length > 7 && float.TryParse(cols[7], out float d) ? d : 0f,
                    SpeedOverride = cols.Length > 8 && float.TryParse(cols[8], out float s) ? s : 0f,
                };

                // CSV에서 처음 등장하는 유효한 id를 firstId로 기록
                if (firstId == null) firstId = entry.Id;
                result[entry.Id] = entry;
            }
            return result;
        }

        // 큰따옴표 안의 쉼표를 구분자로 처리하지 않음
        private static string[] SplitCSVLine(string line)
        {
            var result  = new List<string>();
            bool inQuotes = false;
            var current = new StringBuilder();

            foreach (char c in line)
            {
                if (c == '"')             { inQuotes = !inQuotes; continue; }
                if (c == ',' && !inQuotes){ result.Add(current.ToString()); current.Clear(); continue; }
                current.Append(c);
            }
            result.Add(current.ToString());
            return result.ToArray();
        }

        // "라벨1=id1|라벨2=id2" 형식
        private static List<ChoiceEntry> ParseChoices(string raw)
        {
            var choices = new List<ChoiceEntry>();
            if (string.IsNullOrEmpty(raw)) return choices;

            foreach (var part in raw.Split('|'))
            {
                var kv = part.Split('=');
                if (kv.Length == 2)
                    choices.Add(new ChoiceEntry { Label = kv[0].Trim(), TargetId = kv[1].Trim() });
            }
            return choices;
        }

        // ─── 텍스트 태그 처리 ────────────────────────────────────
        //
        //  지원 태그:
        //    {shake}텍스트{/shake}          떨림 효과
        //    {wave}텍스트{/wave}            물결 효과
        //    {big}텍스트{/big}              150% 크기 (TMP size 태그로 변환)
        //    {big=200%}텍스트{/big}         크기 직접 지정
        //    {color=#FFD700}텍스트{/color}  색상 (TMP color 태그로 변환)
        //    {pause=0.8}                    타이핑 멈춤 (초)

        private static readonly Regex TagRegex = new Regex(
            @"\{(/?)(shake|wave|big|color|pause)(?:=([^}]*))?\}",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex TmpTagRegex = new Regex(@"<[^>]+>", RegexOptions.Compiled);

        public static ProcessedText ProcessTags(string raw)
        {
            var result = new ProcessedText();
            if (string.IsNullOrEmpty(raw)) { result.TmpText = ""; return result; }

            var tmpSb       = new StringBuilder();
            var effectStack = new Stack<(TextEffectType type, int start)>();
            int visibleIndex = 0;
            int lastPos      = 0;

            foreach (Match m in TagRegex.Matches(raw))
            {
                string before = raw.Substring(lastPos, m.Index - lastPos);
                tmpSb.Append(before);
                visibleIndex += CountVisible(before);
                lastPos = m.Index + m.Length;

                bool   isClose  = m.Groups[1].Value == "/";
                string tagName  = m.Groups[2].Value.ToLower();
                string param    = m.Groups[3].Value;

                switch (tagName)
                {
                    case "shake":
                    case "wave":
                        var et = tagName == "shake" ? TextEffectType.Shake : TextEffectType.Wave;
                        if (!isClose)
                        {
                            effectStack.Push((et, visibleIndex));
                        }
                        else if (effectStack.Count > 0 && effectStack.Peek().type == et)
                        {
                            // 같은 타입의 여는 태그와 짝이 맞을 때만 닫음 (중첩 섞임 방지)
                            var (type, start) = effectStack.Pop();
                            result.EffectRanges.Add(new EffectRange
                                { Type = type, StartChar = start, EndChar = visibleIndex - 1 });
                        }
                        else
                        {
                            Debug.LogWarning($"[DialogueParser] 태그 순서가 맞지 않습니다: {{/{tagName}}}");
                        }
                        break;

                    case "big":
                        tmpSb.Append(isClose ? "</size>" : $"<size={( string.IsNullOrEmpty(param) ? "150%" : param )}>");
                        break;

                    case "color":
                        tmpSb.Append(isClose ? "</color>" : $"<color={param}>");
                        break;

                    case "pause":
                        if (!isClose && float.TryParse(param, out float pauseVal))
                            result.PauseAt[visibleIndex] = pauseVal;
                        break;
                }
            }

            string tail = raw.Substring(lastPos);
            tmpSb.Append(tail);
            visibleIndex += CountVisible(tail);

            result.TmpText       = tmpSb.ToString();
            result.VisibleLength = visibleIndex;
            return result;
        }

        private static int CountVisible(string s) =>
            TmpTagRegex.Replace(s, "").Length;
    }
}
