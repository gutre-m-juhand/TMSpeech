using System;

namespace TMSpeech.Translator.APITranslator.Models
{
    /// <summary>
    /// 翻译上文信息
    /// 用于存储历史翻译结果，用于上文感知翻译
    /// </summary>
    public class TranslationContext
    {
        /// <summary>
        /// 原文
        /// </summary>
        public string SourceText { get; set; } = "";

        /// <summary>
        /// 译文
        /// </summary>
        public string TranslatedText { get; set; } = "";

        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// 目标语言
        /// </summary>
        public string TargetLanguage { get; set; } = "";
    }
}
