using System;
using System.Threading.Tasks;
using TMSpeech.Translator.APITranslator.Utils;

namespace TMSpeech.Translator.APITranslator.Loops
{
    /// <summary>
    /// DisplayLoop handles the display management logic.
    /// Retrieves translated text from the display queue and manages display timing.
    /// </summary>
    public class DisplayLoop
    {
        private readonly TranslationTaskQueue _displayQueue;
        private string _lastDisplayedText = "";

        public event Action<string>? OnTranslationDisplayed;

        public DisplayLoop(TranslationTaskQueue displayQueue)
        {
            _displayQueue = displayQueue ?? throw new ArgumentNullException(nameof(displayQueue));
        }

        /// <summary>
        /// Process one display update from the display queue.
        /// </summary>
        public async Task<(string translatedText, int delayMs)> ProcessDisplay()
        {
            var (translatedText, isChoke) = _displayQueue.Output;
            System.Diagnostics.Debug.WriteLine($"[DisplayLoop] Output from queue: '{translatedText}', IsChoke: {isChoke}");

            if (!string.IsNullOrEmpty(translatedText))
            {
                // Filter out empty translations (only notice prefix)
                string cleanedText = RegexPatterns.NoticePrefix().Replace(translatedText, string.Empty).Trim();
                if (!string.IsNullOrEmpty(cleanedText))
                {
                    // 参考项目的去重方式：直接比较完整的翻译文本
                    // 只有当翻译结果和上次不同时，才输出
                    if (string.CompareOrdinal(_lastDisplayedText, translatedText) != 0)
                    {
                        _lastDisplayedText = translatedText;
                        System.Diagnostics.Debug.WriteLine($"[DisplayLoop] Firing OnTranslationDisplayed: '{translatedText}'");
                        OnTranslationDisplayed?.Invoke(translatedText);

                        // If the original sentence is a complete sentence, add delay for better visual experience.
                        if (isChoke)
                            return (translatedText, 720);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[DisplayLoop] Skipped duplicate: '{translatedText}'");
                    }
                }
            }

            return (translatedText, 40);
        }

        /// <summary>
        /// Get the last displayed text.
        /// </summary>
        public string GetLastDisplayedText() => _lastDisplayedText;

        /// <summary>
        /// Reset the display loop state.
        /// </summary>
        public void Reset()
        {
            _lastDisplayedText = "";
        }
    }
}
