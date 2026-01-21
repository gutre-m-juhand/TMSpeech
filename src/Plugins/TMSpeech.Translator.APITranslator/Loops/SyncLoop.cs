using System;
using System.Collections.Generic;
using System.Text;
using TMSpeech.Translator.APITranslator.Utils;

namespace TMSpeech.Translator.APITranslator.Loops
{
    /// <summary>
    /// SyncLoop handles text buffering and merging logic with proper frequency control.
    /// 
    /// Frequency Control Algorithm:
    /// - idleCount: Increments when text remains unchanged (no new input)
    /// - syncCount: Increments when text changes but doesn't have sentence ending
    /// 
    /// Translation is triggered when:
    /// 1. Text ends with punctuation (sentence complete)
    /// 2. syncCount > MaxSyncInterval (text changed too many times without punctuation)
    /// 3. idleCount == MaxIdleInterval (text unchanged for too long)
    /// 
    /// This prevents excessive translation calls while ensuring timely translation.
    /// </summary>
    public class SyncLoop
    {
        private readonly Queue<string> _pendingTextQueue;
        private string _currentText = "";
        private int _idleCount = 0;
        private int _syncCount = 0;

        public SyncLoop(Queue<string> pendingTextQueue)
        {
            _pendingTextQueue = pendingTextQueue ?? throw new ArgumentNullException(nameof(pendingTextQueue));
        }

        /// <summary>
        /// Process incoming text and manage buffering/merging logic.
        /// This is called on every Translate() call from the recognizer.
        /// </summary>
        public void ProcessText(string incomingText)
        {
            if (string.IsNullOrEmpty(incomingText))
                return;

            string fullText = incomingText;

            // Preprocess
            fullText = RegexPatterns.Acronym().Replace(fullText, "$1$2");
            fullText = RegexPatterns.AcronymWithWords().Replace(fullText, "$1 $2");
            fullText = RegexPatterns.PunctuationSpace().Replace(fullText, "$1 ");
            fullText = RegexPatterns.CJPunctuationSpace().Replace(fullText, "$1");
            // Note: For certain languages (such as Japanese), LiveCaptions excessively uses `\n`.
            // Replace redundant `\n` within sentences with comma or period.
            fullText = TextUtil.ReplaceNewlines(fullText, TextUtil.MEDIUM_THRESHOLD);

            // Get the last sentence.
            int lastEOSIndex;
            if (fullText.Length > 0 && Array.IndexOf(TextUtil.PUNC_EOS, fullText[^1]) != -1)
                lastEOSIndex = fullText[0..^1].LastIndexOfAny(TextUtil.PUNC_EOS);
            else
                lastEOSIndex = fullText.LastIndexOfAny(TextUtil.PUNC_EOS);
            
            string latestCaption = fullText.Substring(lastEOSIndex + 1);

            // If the last sentence is too short, extend it by adding the previous sentence.
            // Note: LiveCaptions may generate multiple characters including EOS at once.
            if (lastEOSIndex > 0 && Encoding.UTF8.GetByteCount(latestCaption) < TextUtil.SHORT_THRESHOLD)
            {
                lastEOSIndex = fullText[0..lastEOSIndex].LastIndexOfAny(TextUtil.PUNC_EOS);
                latestCaption = fullText.Substring(lastEOSIndex + 1);
            }

            // Prepare for `OriginalCaption`. If Expanded, only retain the complete sentence.
            int lastEOS = latestCaption.LastIndexOfAny(TextUtil.PUNC_EOS);
            string captionToTranslate = latestCaption;
            if (lastEOS != -1)
                captionToTranslate = latestCaption.Substring(0, lastEOS + 1);

            // Update current text and check if it changed
            if (string.CompareOrdinal(_currentText, captionToTranslate) != 0)
            {
                _currentText = captionToTranslate;
                _idleCount = 0;

                // Check if text ends with punctuation (sentence complete)
                if (_currentText.Length > 0 && Array.IndexOf(TextUtil.PUNC_EOS, _currentText[^1]) != -1)
                {
                    // Sentence complete - enqueue immediately
                    _syncCount = 0;
                    _pendingTextQueue.Enqueue(_currentText);
                    _currentText = "";  // 投入后清空，防止 ShouldTranslate 重复投入
                }
                else if (Encoding.UTF8.GetByteCount(_currentText) >= TextUtil.SHORT_THRESHOLD)
                {
                    // Text is long enough but no punctuation - increment sync count
                    _syncCount++;
                }
            }
            else
            {
                // Text unchanged - increment idle count
                _idleCount++;
            }
        }

        /// <summary>
        /// Check if translation should be triggered based on sync/idle intervals.
        /// This is called periodically by the SyncLoop timer (every 25ms).
        /// 
        /// Triggers translation when:
        /// - syncCount > maxSyncInterval: Text changed multiple times without punctuation
        /// - idleCount == maxIdleInterval: Text unchanged for a while
        /// </summary>
        public bool ShouldTranslate(int maxSyncInterval, int maxIdleInterval)
        {
            if (_syncCount > maxSyncInterval || _idleCount == maxIdleInterval)
            {
                _syncCount = 0;
                _idleCount = 0;
                if (!string.IsNullOrEmpty(_currentText))
                {
                    _pendingTextQueue.Enqueue(_currentText);
                    _currentText = "";  // 清空已投入的文本，防止重复投入
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Get the current buffered text.
        /// </summary>
        public string GetCurrentText() => _currentText;

        /// <summary>
        /// Reset the sync loop state.
        /// </summary>
        public void Reset()
        {
            _currentText = "";
            _idleCount = 0;
            _syncCount = 0;
        }
    }
}
