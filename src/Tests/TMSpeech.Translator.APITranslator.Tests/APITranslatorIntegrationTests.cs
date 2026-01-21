using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMSpeech.Translator.APITranslator;
using Xunit;

namespace TMSpeech.Translator.APITranslator.Tests
{
    public class APITranslatorIntegrationTests : IDisposable
    {
        private APITranslator? _translator;

        public void Dispose()
        {
            _translator?.Stop();
            _translator?.Destroy();
        }

        [Fact]
        public async Task Translate_WithCompleteSentence_ProducesTranslation()
        {
            // Arrange
            _translator = new APITranslator();
            _translator.LoadConfig("");
            _translator.Init();
            _translator.Start();

            string? translatedResult = null;
            var tcs = new TaskCompletionSource<bool>();

            _translator.TranslationCompleted += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.TranslatedText) && !args.TranslatedText.Contains("[Paused]"))
                {
                    translatedResult = args.TranslatedText;
                    tcs.TrySetResult(true);
                }
            };

            // Act
            _translator.Translate("Hello world.", "auto", "zh");

            // Wait for translation with timeout
            var completed = await Task.WhenAny(tcs.Task, Task.Delay(10000));

            // Assert
            Assert.NotNull(translatedResult);
            Assert.NotEmpty(translatedResult);
            Assert.DoesNotContain("[ERROR]", translatedResult);
        }

        [Fact]
        public async Task Translate_WithMultipleSentences_TranslatesEach()
        {
            // Arrange
            _translator = new APITranslator();
            _translator.LoadConfig("");
            _translator.Init();
            _translator.Start();

            var results = new List<string>();
            var tcs = new TaskCompletionSource<bool>();
            int expectedCount = 2;

            _translator.TranslationCompleted += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.TranslatedText) && !args.TranslatedText.Contains("[Paused]"))
                {
                    results.Add(args.TranslatedText);
                    if (results.Count >= expectedCount)
                    {
                        tcs.TrySetResult(true);
                    }
                }
            };

            // Act
            _translator.Translate("Hello.", "auto", "zh");
            await Task.Delay(500);
            _translator.Translate("World.", "auto", "zh");

            // Wait for translations with timeout
            var completed = await Task.WhenAny(tcs.Task, Task.Delay(15000));

            // Assert
            Assert.NotEmpty(results);
        }

        [Fact]
        public async Task Translate_WithShortSentences_BuffersAndMerges()
        {
            // Arrange
            _translator = new APITranslator();
            _translator.LoadConfig("");
            _translator.Init();
            _translator.Start();

            string? translatedResult = null;
            var tcs = new TaskCompletionSource<bool>();

            _translator.TranslationCompleted += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.TranslatedText) && !args.TranslatedText.Contains("[Paused]"))
                {
                    translatedResult = args.TranslatedText;
                    tcs.TrySetResult(true);
                }
            };

            // Act - simulate incremental input
            _translator.Translate("Hello", "auto", "zh");
            await Task.Delay(100);
            _translator.Translate("Hello world", "auto", "zh");
            await Task.Delay(100);
            _translator.Translate("Hello world.", "auto", "zh");

            // Wait for translation with timeout
            var completed = await Task.WhenAny(tcs.Task, Task.Delay(10000));

            // Assert
            Assert.NotNull(translatedResult);
            Assert.NotEmpty(translatedResult);
        }

        [Fact]
        public async Task Translate_WithChineseText_ProducesEnglishTranslation()
        {
            // Arrange
            _translator = new APITranslator();
            _translator.LoadConfig("");
            _translator.Init();
            _translator.Start();

            string? translatedResult = null;
            var tcs = new TaskCompletionSource<bool>();

            _translator.TranslationCompleted += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.TranslatedText) && !args.TranslatedText.Contains("[Paused]"))
                {
                    translatedResult = args.TranslatedText;
                    tcs.TrySetResult(true);
                }
            };

            // Act
            _translator.Translate("你好世界。", "auto", "en");

            // Wait for translation with timeout
            var completed = await Task.WhenAny(tcs.Task, Task.Delay(10000));

            // Assert
            Assert.NotNull(translatedResult);
            Assert.NotEmpty(translatedResult);
        }

        [Fact]
        public void Translate_BeforeStart_DoesNotThrow()
        {
            // Arrange
            _translator = new APITranslator();
            _translator.LoadConfig("");
            _translator.Init();

            // Act & Assert - should not throw
            _translator.Translate("Hello world.", "auto", "zh");
        }

        [Fact]
        public void Translate_AfterStop_DoesNotThrow()
        {
            // Arrange
            _translator = new APITranslator();
            _translator.LoadConfig("");
            _translator.Init();
            _translator.Start();
            _translator.Stop();

            // Act & Assert - should not throw
            _translator.Translate("Hello world.", "auto", "zh");
        }

        [Fact]
        public async Task Translate_WithEmptyString_DoesNotProduceTranslation()
        {
            // Arrange
            _translator = new APITranslator();
            _translator.LoadConfig("");
            _translator.Init();
            _translator.Start();

            int eventCount = 0;
            _translator.TranslationCompleted += (sender, args) =>
            {
                eventCount++;
            };

            // Act
            _translator.Translate("", "auto", "zh");
            await Task.Delay(1000);

            // Assert
            Assert.Equal(0, eventCount);
        }

        [Fact]
        public async Task Translate_WithWhitespaceOnly_DoesNotProduceTranslation()
        {
            // Arrange
            _translator = new APITranslator();
            _translator.LoadConfig("");
            _translator.Init();
            _translator.Start();

            int eventCount = 0;
            _translator.TranslationCompleted += (sender, args) =>
            {
                eventCount++;
            };

            // Act
            _translator.Translate("   ", "auto", "zh");
            await Task.Delay(1000);

            // Assert
            Assert.Equal(0, eventCount);
        }

        [Fact]
        public async Task Translate_MultipleStartStop_WorksCorrectly()
        {
            // Arrange
            _translator = new APITranslator();
            _translator.LoadConfig("");
            _translator.Init();

            string? translatedResult = null;
            var tcs = new TaskCompletionSource<bool>();

            _translator.TranslationCompleted += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.TranslatedText) && !args.TranslatedText.Contains("[Paused]"))
                {
                    translatedResult = args.TranslatedText;
                    tcs.TrySetResult(true);
                }
            };

            // Act
            _translator.Start();
            _translator.Translate("Hello.", "auto", "zh");

            var completed = await Task.WhenAny(tcs.Task, Task.Delay(10000));

            // Assert
            Assert.NotNull(translatedResult);
        }

        [Fact]
        public async Task Translate_WithEventTypes_LogsHistoryCorrectly()
        {
            // Arrange
            _translator = new APITranslator();
            _translator.LoadConfig("");
            _translator.Init();
            _translator.Start();

            string? translatedResult = null;
            var tcs = new TaskCompletionSource<bool>();

            _translator.TranslationCompleted += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.TranslatedText) && !args.TranslatedText.Contains("[Paused]"))
                {
                    translatedResult = args.TranslatedText;
                    tcs.TrySetResult(true);
                }
            };

            // Act
            // Test TextChanged event (eventType=0)
            _translator.Translate("Hello", "auto", "zh", 0);
            await Task.Delay(100);
            
            // Test SentenceDone event (eventType=1)
            _translator.Translate("Hello world.", "auto", "zh", 1);

            var completed = await Task.WhenAny(tcs.Task, Task.Delay(10000));

            // Get history log
            var history = _translator.GetHistoryLog();

            // Assert
            Assert.NotNull(translatedResult);
            Assert.NotEmpty(history);
            Assert.Contains("[INPUT]", history);
            Assert.Contains("TextChanged", history);
            Assert.Contains("SentenceDone", history);
            Assert.Contains("[OUTPUT]", history);
            Assert.Contains("[TRANSLATE_", history);
        }
    }
}
