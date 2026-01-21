using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMSpeech.Translator.APITranslator;
using TMSpeech.Translator.APITranslator.Loops;
using TMSpeech.Translator.APITranslator.Utils;
using Xunit;

namespace TMSpeech.Translator.APITranslator.Tests
{
    public class ContextAwareTranslationTests
    {
        [Fact]
        public async Task TranslateLoop_WithContextManager_ShouldUseContext()
        {
            var pendingQueue = new Queue<string>();
            var displayQueue = new TranslationTaskQueue();
            var contextManager = new ContextManager(maxContextCount: 6);
            var translateLoop = new TranslateLoop(pendingQueue, displayQueue);

            // Add some context
            contextManager.AddContext("Hello", "你好", "zh");
            contextManager.AddContext("World", "世界", "zh");

            // Enqueue text to translate
            pendingQueue.Enqueue("How are you?");

            var logMessages = new List<string>();
            Action<string> logCallback = (msg) => logMessages.Add(msg);

            // Process translation with context
            await translateLoop.ProcessTranslation(
                targetLanguage: "zh",
                showLatency: false,
                logCallback: logCallback,
                contextManager: contextManager,
                contextAwareEnabled: true
            );

            // Verify that context was used
            var contextLog = logMessages.Find(m => m.Contains("[CONTEXT]"));
            Assert.NotNull(contextLog);
            Assert.Contains("Hello", contextLog);
            Assert.Contains("World", contextLog);
        }

        [Fact]
        public async Task TranslateLoop_WithoutContextManager_ShouldTranslateDirectly()
        {
            var pendingQueue = new Queue<string>();
            var displayQueue = new TranslationTaskQueue();
            var translateLoop = new TranslateLoop(pendingQueue, displayQueue);

            // Enqueue text to translate
            pendingQueue.Enqueue("Hello");

            var logMessages = new List<string>();
            Action<string> logCallback = (msg) => logMessages.Add(msg);

            // Process translation without context
            await translateLoop.ProcessTranslation(
                targetLanguage: "zh",
                showLatency: false,
                logCallback: logCallback,
                contextManager: null,
                contextAwareEnabled: false
            );

            // Verify that no context was used
            var contextLog = logMessages.Find(m => m.Contains("[CONTEXT]"));
            Assert.Null(contextLog);
        }

        [Fact]
        public async Task TranslateLoop_ShouldSaveTranslationToContext()
        {
            var pendingQueue = new Queue<string>();
            var displayQueue = new TranslationTaskQueue();
            var contextManager = new ContextManager(maxContextCount: 6);
            var translateLoop = new TranslateLoop(pendingQueue, displayQueue);

            // Enqueue text to translate
            pendingQueue.Enqueue("Hello");

            var logMessages = new List<string>();
            Action<string> logCallback = (msg) => logMessages.Add(msg);

            // Process translation
            await translateLoop.ProcessTranslation(
                targetLanguage: "zh",
                showLatency: false,
                logCallback: logCallback,
                contextManager: contextManager,
                contextAwareEnabled: true
            );

            // Verify that translation was saved to context
            Assert.Equal(1, contextManager.Count);
        }

        [Fact]
        public async Task TranslateLoop_WithContextAwareDisabled_ShouldNotUseContext()
        {
            var pendingQueue = new Queue<string>();
            var displayQueue = new TranslationTaskQueue();
            var contextManager = new ContextManager(maxContextCount: 6);
            var translateLoop = new TranslateLoop(pendingQueue, displayQueue);

            // Add some context
            contextManager.AddContext("Hello", "你好", "zh");

            // Enqueue text to translate
            pendingQueue.Enqueue("World");

            var logMessages = new List<string>();
            Action<string> logCallback = (msg) => logMessages.Add(msg);

            // Process translation with context disabled
            await translateLoop.ProcessTranslation(
                targetLanguage: "zh",
                showLatency: false,
                logCallback: logCallback,
                contextManager: contextManager,
                contextAwareEnabled: false  // Disabled
            );

            // Verify that context was not used
            var contextLog = logMessages.Find(m => m.Contains("[CONTEXT]"));
            Assert.Null(contextLog);
        }

        [Fact]
        public void APITranslator_ShouldInitializeContextManager()
        {
            var translator = new APITranslator();
            translator.LoadConfig("");
            translator.Init();
            translator.Start();

            // Translator should be running with context manager initialized
            Assert.True(translator.Available);

            translator.Stop();
        }

        [Fact]
        public async Task APITranslator_MultipleTranslations_ShouldBuildContext()
        {
            var translator = new APITranslator();
            translator.LoadConfig("");
            translator.Init();
            translator.Start();

            // First translation
            translator.Translate("Hello", "auto", "zh", 0);
            await Task.Delay(1000);

            // Second translation (should have context from first)
            translator.Translate("World", "auto", "zh", 0);
            await Task.Delay(1000);

            var history = translator.GetHistoryLog();
            
            // Verify that translations were logged
            Assert.Contains("[INPUT]", history);

            translator.Stop();
        }
    }
}
