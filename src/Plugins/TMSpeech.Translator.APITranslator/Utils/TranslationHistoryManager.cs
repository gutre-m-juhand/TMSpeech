using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMSpeech.Translator.APITranslator.Models;

namespace TMSpeech.Translator.APITranslator.Utils
{
    /// <summary>
    /// 翻译历史管理器（单例）
    /// 用于管理翻译和识别历史记录
    /// </summary>
    public class TranslationHistoryManager
    {
        private static readonly Lazy<TranslationHistoryManager> _instance = 
            new(() => new TranslationHistoryManager());

        public static TranslationHistoryManager Instance => _instance.Value;

        private List<TranslationRecord> _history = new();
        private readonly object _lockObject = new();

        private TranslationHistoryManager()
        {
        }

        /// <summary>
        /// 添加翻译记录
        /// </summary>
        public void AddTranslationRecord(string sourceText, string translatedText, string targetLanguage, string apiUsed)
        {
            lock (_lockObject)
            {
                _history.Add(new TranslationRecord
                {
                    SourceText = sourceText,
                    TranslatedText = translatedText,
                    TargetLanguage = targetLanguage,
                    ApiUsed = apiUsed,
                    Timestamp = DateTime.Now
                });
            }
        }

        /// <summary>
        /// 添加识别记录（禁用翻译时）
        /// </summary>
        public void AddRecognitionRecord(string sourceText, string targetLanguage)
        {
            lock (_lockObject)
            {
                _history.Add(new TranslationRecord
                {
                    SourceText = sourceText,
                    TranslatedText = "",
                    TargetLanguage = targetLanguage,
                    ApiUsed = "",
                    Timestamp = DateTime.Now
                });
            }
        }

        /// <summary>
        /// 获取历史记录
        /// </summary>
        public List<TranslationRecord> GetHistory()
        {
            lock (_lockObject)
            {
                return new List<TranslationRecord>(_history);
            }
        }

        /// <summary>
        /// 清空历史记录
        /// </summary>
        public void Clear()
        {
            lock (_lockObject)
            {
                _history.Clear();
            }
        }

        /// <summary>
        /// 导出历史记录为 CSV
        /// </summary>
        public async Task<string> ExportToCsvAsync(string filePath)
        {
            try
            {
                var history = GetHistory();
                
                if (history.Count == 0)
                {
                    return "[ERROR] 历史记录为空";
                }

                // 构建 CSV 内容
                var csvLines = new List<string>
                {
                    "时间戳,原文,译文,目标语言,API"
                };

                foreach (var record in history)
                {
                    string timestamp = record.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
                    string sourceText = EscapeCsvField(record.SourceText);
                    string translatedText = EscapeCsvField(record.TranslatedText);
                    string targetLanguage = record.TargetLanguage;
                    string apiUsed = record.ApiUsed;

                    csvLines.Add($"{timestamp},{sourceText},{translatedText},{targetLanguage},{apiUsed}");
                }

                // 写入文件
                await File.WriteAllLinesAsync(filePath, csvLines, System.Text.Encoding.UTF8);
                return $"[SUCCESS] 历史记录已导出到: {filePath}";
            }
            catch (Exception ex)
            {
                return $"[ERROR] 导出失败: {ex.Message}";
            }
        }

        /// <summary>
        /// CSV 字段转义
        /// </summary>
        private string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field))
                return "\"\"";

            if (field.Contains(",") || field.Contains("\"") || field.Contains("\n"))
            {
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            }

            return field;
        }
    }
}
