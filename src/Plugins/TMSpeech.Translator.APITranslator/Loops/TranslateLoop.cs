using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TMSpeech.Translator.APITranslator.APIs;
using TMSpeech.Translator.APITranslator.Utils;

namespace TMSpeech.Translator.APITranslator.Loops
{
    /// <summary>
    /// TranslateLoop handles the translation control logic.
    /// Dequeues text from pending queue, calls translation API, and enqueues results to display queue.
    /// Supports context-aware translation to improve translation quality.
    /// </summary>
    public class TranslateLoop
    {
        private readonly Queue<string> _pendingTextQueue;
        private readonly TranslationTaskQueue _displayQueue;
        private CancellationToken _cancellationToken;
        
        /// <summary>
        /// 保存最后一次翻译的原文，供 APITranslator 使用
        /// </summary>
        public string LastOriginalText { get; private set; } = string.Empty;

        public TranslateLoop(Queue<string> pendingTextQueue, TranslationTaskQueue displayQueue)
        {
            _pendingTextQueue = pendingTextQueue ?? throw new ArgumentNullException(nameof(pendingTextQueue));
            _displayQueue = displayQueue ?? throw new ArgumentNullException(nameof(displayQueue));
            _cancellationToken = CancellationToken.None;
        }

        /// <summary>
        /// Set the cancellation token for translation operations.
        /// </summary>
        public void SetCancellationToken(CancellationToken token)
        {
            _cancellationToken = token;
        }

        /// <summary>
        /// Process one translation task from the pending queue.
        /// Supports context-aware translation if contextManager is provided.
        /// </summary>
        public async Task ProcessTranslation(
            string targetLanguage,
            bool showLatency = false,
            Action<string>? logCallback = null,
            ContextManager? contextManager = null,
            bool contextAwareEnabled = false,
            string apiName = "Google",
            APITranslatorConfig config = null)
        {
            if (_pendingTextQueue.Count == 0)
                return;

            var originalText = _pendingTextQueue.Dequeue();
            LastOriginalText = originalText;  // 保存原文
            
            System.Diagnostics.Debug.WriteLine($"[TranslateLoop.ProcessTranslation] Received targetLanguage: '{targetLanguage}'");
            System.Diagnostics.Debug.WriteLine($"[TranslateLoop.ProcessTranslation] config.TargetLanguage: '{config?.TargetLanguage}'");
            System.Diagnostics.Debug.WriteLine($"[TranslateLoop.ProcessTranslation] apiName: '{apiName}'");
            
            logCallback?.Invoke($"[TRANSLATE_START] Original: '{originalText}', Target: {targetLanguage}, API: {apiName}");

            try
            {
                var sw = showLatency ? Stopwatch.StartNew() : null;
                
                string textToTranslate = originalText;

                // If context-aware translation is enabled, prepend previous context
                if (contextAwareEnabled && contextManager != null)
                {
                    string previousContext = contextManager.GetPreviousContext(count: 3);
                    if (!string.IsNullOrEmpty(previousContext))
                    {
                        textToTranslate = $"{previousContext} <[{originalText}]>";
                        logCallback?.Invoke($"[CONTEXT] Using context: '{previousContext}'");
                    }
                }

                // 根据配置的 API 名称调用相应的翻译函数
                string translatedText;
                if (TranslateAPI.TRANSLATE_FUNCTIONS.TryGetValue(apiName, out var translateFunc))
                {
                    System.Diagnostics.Debug.WriteLine($"[TranslateLoop.ProcessTranslation] Calling API with targetLanguage: '{targetLanguage}'");
                    translatedText = await translateFunc(textToTranslate, targetLanguage, _cancellationToken, config);
                }
                else
                {
                    translatedText = $"[ERROR] 不支持的翻译 API: {apiName}";
                }
                
                translatedText = translatedText.Replace("🔤", "");

                // If context was used, extract only the current sentence translation
                if (contextAwareEnabled && contextManager != null && textToTranslate.Contains("<["))
                {
                    // Try to extract text within <[...]>
                    var match = Regex.Match(translatedText, @"<\[([^\]]+)\]>");
                    if (match.Success)
                    {
                        translatedText = match.Groups[1].Value;
                        logCallback?.Invoke($"[CONTEXT] Extracted translation: '{translatedText}'");
                    }
                }

                if (sw != null)
                {
                    sw.Stop();
                    logCallback?.Invoke($"[TRANSLATE_API] Latency: {sw.ElapsedMilliseconds}ms, Result: '{translatedText}'");
                }
                else
                {
                    logCallback?.Invoke($"[TRANSLATE_API] Result: '{translatedText}'");
                }

                // Save to context manager for future translations
                if (contextManager != null && !translatedText.Contains("[ERROR]"))
                {
                    contextManager.AddContext(originalText, translatedText, targetLanguage);
                }

                bool isChoke = Array.IndexOf(TextUtil.PUNC_EOS, originalText[^1]) != -1;
                _displayQueue.Enqueue(token => Task.FromResult((translatedText, isChoke)), originalText);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                string errorMessage = $"[ERROR] Translation Failed: {ex.Message}";
                logCallback?.Invoke($"[TRANSLATE_ERROR] {errorMessage}");
                bool isChoke = Array.IndexOf(TextUtil.PUNC_EOS, originalText[^1]) != -1;
                _displayQueue.Enqueue(token => Task.FromResult((errorMessage, isChoke)), originalText);
            }
        }

        /// <summary>
        /// Get the count of pending translations.
        /// </summary>
        public int GetPendingCount() => _pendingTextQueue.Count;
    }
}
