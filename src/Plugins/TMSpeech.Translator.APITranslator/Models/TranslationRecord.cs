using System;

namespace TMSpeech.Translator.APITranslator.Models
{
    /// <summary>
    /// 翻译历史记录
    /// 用于存储翻译结果，供用户查看和导出
    /// </summary>
    public class TranslationRecord
    {
        /// <summary>
        /// 原文
        /// </summary>
        public string SourceText { get; set; } = "";

        /// <summary>
        /// 译文（禁用翻译时为空）
        /// </summary>
        public string TranslatedText { get; set; } = "";

        /// <summary>
        /// 目标语言
        /// </summary>
        public string TargetLanguage { get; set; } = "";

        /// <summary>
        /// 使用的 API（禁用翻译时为空）
        /// </summary>
        public string ApiUsed { get; set; } = "";

        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}
