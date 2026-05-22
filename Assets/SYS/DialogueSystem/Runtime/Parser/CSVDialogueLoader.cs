using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace DialogueSystem
{
    /// <summary>
    /// CSV 파일을 읽어 DialogueEntry 목록으로 변환하는 정적 유틸리티.
    ///
    /// ─── CSV 컬럼 순서 ─────────────────────────────────────────────────────────
    ///  0  ID          : 고유 ID (예: NPC001_001)
    ///  1  Speaker     : 화자 이름
    ///  2  Portrait    : 포트레이트 키 (Resources/Portraits/ 하위 파일명)
    ///  3  Text        : 대화 텍스트 (효과 태그 포함, 쌍따옴표로 감싸기 권장)
    ///  4  Next        : 다음 대화 ID (비워두면 종료)
    ///  5  Choices     : 선택지 "표시텍스트|이동ID;표시텍스트2|이동ID2" 형식
    ///  6  BGM         : BGM 키
    ///  7  Voice       : 보이스 키
    ///  8  CameraFX    : none / shake / zoom_in / zoom_out
    ///  9  AutoTime    : 자동 진행 초 (0 = 수동)
    /// 10  Condition   : 표시 조건 플래그 키
    /// 11  SetFlag     : 대화 후 세팅할 플래그 키
    ///
    /// ─── 주석 처리 ─────────────────────────────────────────────────────────────
    ///  # 또는 // 로 시작하는 행은 무시
    ///
    /// ─── 선택지 형식 예시 ───────────────────────────────────────────────────────
    ///  "네, 좋아요|D010;아니요, 싫어요|D020;모르겠어요|D030"
    ///
    /// ─── 텍스트 이스케이프 ─────────────────────────────────────────────────────
    ///  쉼표를 포함한 텍스트는 반드시 "쌍따옴표"로 감싸세요.
    ///  텍스트 내 쌍따옴표는 ""(두 번)으로 이스케이프합니다.
    /// ────────────────────────────────────────────────────────────────────────────
    /// </summary>
    public static class CSVDialogueLoader
    {
        // ── 공개 로드 메서드 ────────────────────────────────────────────────────

        /// <summary>CSV 텍스트 문자열에서 바로 파싱</summary>
        public static List<DialogueEntry> LoadFromText(string csvText)
        {
            var entries = new List<DialogueEntry>();
            var lines   = csvText.Replace("\r\n", "\n").Split('\n');

            for (int i = 1; i < lines.Length; i++) // 헤더(0번 줄) 건너뜀
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line))              continue;
                if (line.StartsWith("#") || line.StartsWith("//")) continue;

                var cols = ParseLine(line);
                var id   = Col(cols, 0);
                if (string.IsNullOrEmpty(id)) continue;

                var entry = new DialogueEntry
                {
                    id              = id,
                    speaker         = Col(cols, 1),
                    portraitKey     = Col(cols, 2),
                    rawText         = Col(cols, 3),
                    nextId          = Col(cols, 4),
                    bgmKey          = Col(cols, 6),
                    voiceKey        = Col(cols, 7),
                    cameraEffect    = Col(cols, 8),
                    autoAdvanceTime = TryFloat(Col(cols, 9)),
                    conditionFlag   = Col(cols, 10),
                    setFlag         = Col(cols, 11),
                    choices         = ParseChoices(Col(cols, 5)),
                };

                entries.Add(entry);
            }

            Debug.Log($"[CSVDialogueLoader] {entries.Count}개 대화 로드 완료");
            return entries;
        }

        /// <summary>절대 경로 CSV 파일에서 로드</summary>
        public static List<DialogueEntry> LoadFromFile(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogError($"[CSVDialogueLoader] 파일 없음: {path}");
                return new List<DialogueEntry>();
            }
            return LoadFromText(File.ReadAllText(path, Encoding.UTF8));
        }

        /// <summary>Resources 폴더 내 TextAsset에서 로드 (경로: "Dialogue/npc_main" 형식)</summary>
        public static List<DialogueEntry> LoadFromResources(string resourcePath)
        {
            var asset = Resources.Load<TextAsset>(resourcePath);
            if (asset == null)
            {
                Debug.LogError($"[CSVDialogueLoader] Resources에서 찾을 수 없음: {resourcePath}");
                return new List<DialogueEntry>();
            }
            return LoadFromText(asset.text);
        }

        // ── 내부 파서 ───────────────────────────────────────────────────────────

        /// <summary>RFC 4180 호환 CSV 한 줄 파싱 (쌍따옴표 이스케이프 처리)</summary>
        private static List<string> ParseLine(string line)
        {
            var result  = new List<string>();
            var sb      = new StringBuilder();
            bool inQuote = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (inQuote)
                {
                    if (c == '"')
                    {
                        // "" → 이스케이프된 쌍따옴표
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            sb.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuote = false;
                        }
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
                else
                {
                    if      (c == '"') inQuote = true;
                    else if (c == ',') { result.Add(sb.ToString()); sb.Clear(); }
                    else               sb.Append(c);
                }
            }
            result.Add(sb.ToString());
            return result;
        }

        private static List<ChoiceData> ParseChoices(string raw)
        {
            var list = new List<ChoiceData>();
            if (string.IsNullOrEmpty(raw)) return list;

            foreach (var part in raw.Split(';'))
            {
                var pipe = part.IndexOf('|');
                if (pipe < 0) continue;
                list.Add(new ChoiceData
                {
                    text   = part[..pipe].Trim(),
                    nextId = part[(pipe + 1)..].Trim(),
                });
            }
            return list;
        }

        private static string Col(List<string> cols, int idx) =>
            idx < cols.Count ? cols[idx].Trim() : string.Empty;

        private static float TryFloat(string s) =>
            float.TryParse(s, System.Globalization.NumberStyles.Float,
                           System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0f;
    }
}
