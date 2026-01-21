using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMSpeech.Translator.APITranslator.Models;

namespace TMSpeech.Translator.APITranslator.Utils
{
    /// <summary>
    /// 翻译上文管理器
    /// 管理历史翻译结果，用于上文感知翻译以提升翻译质量
    /// </summary>
    public class ContextManager
    {
        private readonly Queue<TranslationContext> _contexts = new();
        private readonly int _maxContextCount;
        private readonly object _lockObject = new();

        public ContextManager(int maxContextCount = 6)
        {
            _maxContextCount = maxContextCount;
        }

        /// <summary>
        /// 添加翻译上文
        /// </summary>
        public void AddContext(string sourceText, string translatedText, string targetLanguage)
        {
            if (string.IsNullOrEmpty(sourceText) || string.IsNullOrEmpty(translatedText))
                return;

            lock (_lockObject)
            {
                // 如果超过最大数量，移除最旧的
                if (_contexts.Count >= _maxContextCount)
                    _contexts.Dequeue();

                _contexts.Enqueue(new TranslationContext
                {
                    SourceText = sourceText,
                    TranslatedText = translatedText,
                    TargetLanguage = targetLanguage,
                    Timestamp = DateTime.Now
                });
            }
        }

        /// <summary>
        /// 获取历史上文（用于翻译）
        /// 合并最近的 N 条历史记录作为上文
        /// </summary>
        public string GetPreviousContext(int count = 3)
        {
            lock (_lockObject)
            {
                if (_contexts.Count == 0)
                    return string.Empty;

                // 获取最后 N 条记录
                var contextList = _contexts.TakeLast(Math.Min(count, _contexts.Count)).ToList();
                if (contextList.Count == 0)
                    return string.Empty;

                // 合并历史上文
                var sb = new StringBuilder();
                foreach (var ctx in contextList)
                {
                    if (!string.IsNullOrEmpty(ctx.SourceText))
                    {
                        sb.Append(ctx.SourceText);
                        
                        // 确保以标点符号结尾
                        if (!EndsWithPunctuation(ctx.SourceText))
                        {
                            sb.Append("。");
                        }
                    }
                }

                return sb.ToString();
            }
        }

        /// <summary>
        /// 检查字符串是否以标点符号结尾
        /// </summary>
        private static bool EndsWithPunctuation(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            char lastChar = text[^1];
            return Array.IndexOf(TextUtil.PUNC_EOS, lastChar) != -1;
        }

        /// <summary>
        /// 清空所有上文
        /// </summary>
        public void Clear()
        {
            lock (_lockObject)
            {
                _contexts.Clear();
            }
        }

        /// <summary>
        /// 获取当前上文数量
        /// </summary>
        public int Count
        {
            get
            {
                lock (_lockObject)
                {
                    return _contexts.Count;
                }
            }
        }
    }
}
