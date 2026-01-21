using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TMSpeech.Core.Plugins
{
    /// <summary>
    /// 翻译事件参数
    /// </summary>
    public class TranslationEventArgs
    {
        /// <summary>
        /// 翻译插件实际使用的原文
        /// </summary>
        public string OriginalText { get; set; } = string.Empty;
        
        public string TranslatedText { get; set; } = string.Empty;
        public string SourceLanguage { get; set; } = string.Empty;
        public string TargetLanguage { get; set; } = string.Empty;
        public DateTime Time { get; set; } = DateTime.Now;
    }

    public interface ITranslator : IPlugin, IRunable
    {
        /// <summary>
        /// 翻译文本（异步，无返回值）
        /// 
        /// 识别器识别出新文本后调用此方法，翻译器在后台异步处理翻译。
        /// 翻译完成后通过 TranslationCompleted 事件通知结果。
        /// </summary>
        /// <param name="text">待翻译文本</param>
        /// <param name="sourceLanguage">源语言代码（如 "zh", "en"），默认自动检测</param>
        /// <param name="targetLanguage">目标语言代码</param>
        /// <param name="eventType">事件类型：0=TextChanged（文本变化），1=SentenceDone（句子完成）</param>
        void Translate(string text, string sourceLanguage = "auto", string targetLanguage = "en", int eventType = 0);

        /// <summary>
        /// 翻译完成事件
        /// 
        /// 翻译完成后触发此事件，包含原文本和翻译结果。
        /// 订阅者可以通过此事件获取翻译结果并进行后续处理（如显示、保存等）。
        /// </summary>
        event EventHandler<TranslationEventArgs> TranslationCompleted;

        /// <summary>
        /// 获取支持的语言列表
        /// </summary>
        /// <returns>语言代码列表</returns>
        Task<List<string>> GetSupportedLanguagesAsync();
    }
}
