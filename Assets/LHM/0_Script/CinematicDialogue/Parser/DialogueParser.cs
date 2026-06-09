using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DialogSystem.CinematicDialogue
{
    /// <summary>
    /// 텍스트 내 커맨드/태그를 파싱하는 유틸리티.
    /// 원본 텍스트로부터 출력용 순수 텍스트와 커맨드를 분리하여 명령 큐에 담는다.
    /// </summary>
    public class DialogueCommand
    {
        // 커맨드 타입 정의
        public enum CommandType
        {
            Text,           // 일반 텍스트
            Speed,          // 타이핑 속도 변경: <speed=0.05>
            Wait,           // 대기: [wait:1.0]
            Event,          // 외부 이벤트 트리거: [event:CameraShake]
            Click,          // 클릭 대기: [click]
            Jump,           // 특정 ID로 점프: [jump:scene_1_05]
            Effect,         // 텍스트 이펙트: <shake>, <pulse> 등
        }

        public CommandType type;
        public string value;  // 커맨드 값 (텍스트, 스피드 값, 이벤트명 등)
        public float duration;  // 지속 시간 (Wait, 일부 Effect용)

        public DialogueCommand(CommandType t, string v, float d = 0f)
        {
            type = t;
            value = v;
            duration = d;
        }

        public override string ToString()
        {
            return $"[{type}] {value} ({duration}s)";
        }
    }

    /// <summary>
    /// 정규식을 사용하여 대사 텍스트를 파싱하고,
    /// 순수 텍스트와 커맨드를 분리하여 리스트로 변환한다.
    /// </summary>
    public class DialogueParser
    {
        // 정규식 패턴 정의

        // [tag:value] 형식 태그 매칭 - [click], [wait:1.5], [event:EventName], [jump:id] 등
        private static readonly Regex BracketTagPattern = new Regex(@"\[([a-zA-Z_]+):?([^\]]*)\]", RegexOptions.Compiled);
        
        // <tag> 또는 <tag=value> 형식 태그 매칭 - <speed=0.05>, <shake> 등
        private static readonly Regex AngleBracketTagPattern = new Regex(@"\<([a-zA-Z_]+)=?([^\>]*)\>", RegexOptions.Compiled);

        /// <summary>
        /// 대사 텍스트를 파싱하여 DialogueCommand 리스트로 변환한다.
        /// 텍스트와 태그를 순서대로 처리하며, 생성된 명령들을 큐에 담는다.
        /// </summary>
        /// <param name="rawText">원본 텍스트 (태그 포함)</param>
        /// <returns>실행할 커맨드의 순서 리스트</returns>
        public static List<DialogueCommand> Parse(string rawText)
        {
            List<DialogueCommand> commands = new List<DialogueCommand>();

            if (string.IsNullOrEmpty(rawText))
                return commands;

            // 모든 태그(대괄호, 삼각괄호)의 위치와 종류를 먼저 파악
            List<(int start, int end, Match match, bool isBracket)> allMatches = new List<(int, int, Match, bool)>();

            // 대괄호 태그 찾기: [tag:value]
            foreach (Match match in BracketTagPattern.Matches(rawText))
            {
                allMatches.Add((match.Index, match.Index + match.Length, match, true));
            }

            // 삼각괄호 태그 찾기: <tag=value>
            foreach (Match match in AngleBracketTagPattern.Matches(rawText))
            {
                allMatches.Add((match.Index, match.Index + match.Length, match, false));
            }

            // 위치 순으로 정렬하여 순차 처리
            allMatches.Sort((a, b) => a.start.CompareTo(b.start));

            int lastIndex = 0;
            foreach (var (start, end, match, isBracket) in allMatches)
            {
                // 이전 태그 이후부터 현재 태그 이전까지의 순수 텍스트 추출
                if (start > lastIndex)
                {
                    string pureText = rawText.Substring(lastIndex, start - lastIndex);
                    if (!string.IsNullOrEmpty(pureText))
                    {
                        commands.Add(new DialogueCommand(DialogueCommand.CommandType.Text, pureText));
                    }
                }

                // 태그 파싱하여 적절한 커맨드 생성
                if (isBracket)
                {
                    ParseBracketTag(match, commands);
                }
                else
                {
                    ParseAngleBracketTag(match, commands);
                }

                lastIndex = end;
            }

            // 마지막 태그 이후의 텍스트 처리
            if (lastIndex < rawText.Length)
            {
                string remainingText = rawText.Substring(lastIndex);
                if (!string.IsNullOrEmpty(remainingText))
                {
                    commands.Add(new DialogueCommand(DialogueCommand.CommandType.Text, remainingText));
                }
            }

            return commands;
        }

        /// <summary>
        /// 대괄호 형식 태그 파싱: [tag], [tag:value]
        /// 예: [click], [wait:1.5], [event:CameraShake], [jump:scene_1_05]
        /// </summary>
        private static void ParseBracketTag(Match match, List<DialogueCommand> commands)
        {
            string tagName = match.Groups[1].Value.ToLower();  // 태그 이름
            string tagValue = match.Groups[2].Value;  // 태그 값 (콜론 이후)

            switch (tagName)
            {
                case "click":
                    // 클릭 대기 커맨드
                    commands.Add(new DialogueCommand(DialogueCommand.CommandType.Click, ""));
                    break;

                case "wait":
                    // 지정된 시간 대기
                    if (float.TryParse(tagValue, out float waitDuration))
                    {
                        commands.Add(new DialogueCommand(DialogueCommand.CommandType.Wait, tagValue, waitDuration));
                    }
                    break;

                case "event":
                    // 외부 이벤트 콜백 호출
                    commands.Add(new DialogueCommand(DialogueCommand.CommandType.Event, tagValue));
                    break;

                case "jump":
                    // 특정 ID의 대사로 점프
                    commands.Add(new DialogueCommand(DialogueCommand.CommandType.Jump, tagValue));
                    break;

                default:
                    Debug.LogWarning($"[DialogueParser] 알 수 없는 대괄호 태그: {tagName}");
                    break;
            }
        }

        /// <summary>
        /// 삼각괄호 형식 태그 파싱: <tag> 또는 <tag=value>
        /// 예: <speed=0.05>, <shake>, <pulse>
        /// </summary>
        private static void ParseAngleBracketTag(Match match, List<DialogueCommand> commands)
        {
            string tagName = match.Groups[1].Value.ToLower();  // 태그 이름
            string tagValue = match.Groups[2].Value;  // 태그 값 (= 이후)

            switch (tagName)
            {
                case "speed":
                    // 타이핑 속도 변경 (글자당 초 단위)
                    if (float.TryParse(tagValue, out float speed) && speed > 0)
                    {
                        commands.Add(new DialogueCommand(DialogueCommand.CommandType.Speed, speed.ToString()));
                    }
                    break;

                case "shake":
                    // 텍스트 흔들림 이펙트
                    commands.Add(new DialogueCommand(DialogueCommand.CommandType.Effect, "shake"));
                    break;

                case "pulse":
                    // 텍스트 펄싱 이펙트
                    commands.Add(new DialogueCommand(DialogueCommand.CommandType.Effect, "pulse"));
                    break;

                case "emphasis":
                    // 텍스트 강조 이펙트
                    commands.Add(new DialogueCommand(DialogueCommand.CommandType.Effect, "emphasis"));
                    break;

                default:
                    Debug.LogWarning($"[DialogueParser] 알 수 없는 삼각괄호 태그: {tagName}");
                    break;
            }
        }

        /// <summary>
        /// 텍스트에서 모든 태그를 제거하고 순수 텍스트만 반환.
        /// UI에 표시할 때 사용된다.
        /// </summary>
        public static string ExtractPureText(string rawText)
        {
            if (string.IsNullOrEmpty(rawText))
                return rawText;

            // 모든 태그 제거
            string text = BracketTagPattern.Replace(rawText, "");
            text = AngleBracketTagPattern.Replace(text, "");

            return text;
        }

        /// <summary>
        /// 디버그용: 파싱 결과를 로그에 출력
        /// </summary>
        public static void DebugPrintCommands(List<DialogueCommand> commands)
        {
            Debug.Log($"=== 파싱된 커맨드 ({commands.Count}개) ===");
            for (int i = 0; i < commands.Count; i++)
            {
                Debug.Log($"[{i}] {commands[i]}");
            }
        }
    }
}
