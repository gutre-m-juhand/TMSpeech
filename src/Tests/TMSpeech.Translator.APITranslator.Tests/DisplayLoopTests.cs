using System;
using System.Threading.Tasks;
using TMSpeech.Translator.APITranslator.Loops;
using TMSpeech.Translator.APITranslator.Utils;
using Xunit;

namespace TMSpeech.Translator.APITranslator.Tests
{
    public class DisplayLoopTests
    {
        [Fact]
        public async Task ProcessDisplay_WithValidTranslation_TriggersEvent()
        {
            // Arrange
            var displayQueue = new TranslationTaskQueue();
            var displayLoop = new DisplayLoop(displayQueue);
            string? capturedText = null;

            displayLoop.OnTranslationDisplayed += (text) =>
            {
                capturedText = text;
            };

            // Enqueue a translation
            displayQueue.Enqueue(token => Task.FromResult(("Hello translated", false)), "Hello");

            // Act
            await Task.Delay(100); // Give time for queue to process
            var (text, delay) = await displayLoop.ProcessDisplay();

            // Assert
            Assert.NotNull(capturedText);
            Assert.Equal("Hello translated", capturedText);
        }

        [Fact]
        public async Task ProcessDisplay_WithCompleteSentence_ReturnsChokeDelay()
        {
            // Arrange
            var displayQueue = new TranslationTaskQueue();
            var displayLoop = new DisplayLoop(displayQueue);

            // Enqueue a translation with complete sentence (isChoke=true)
            displayQueue.Enqueue(token => Task.FromResult(("Translated.", true)), "Original.");

            // Act
            await Task.Delay(100);
            var (text, delay) = await displayLoop.ProcessDisplay();

            // Assert
            Assert.Equal(720, delay);
        }

        [Fact]
        public async Task ProcessDisplay_WithIncompleteSentence_ReturnsNormalDelay()
        {
            // Arrange
            var displayQueue = new TranslationTaskQueue();
            var displayLoop = new DisplayLoop(displayQueue);

            // Enqueue a translation with incomplete sentence (isChoke=false)
            displayQueue.Enqueue(token => Task.FromResult(("Translated", false)), "Original");

            // Act
            await Task.Delay(100);
            var (text, delay) = await displayLoop.ProcessDisplay();

            // Assert
            Assert.Equal(40, delay);
        }

        [Fact]
        public async Task ProcessDisplay_WithEmptyTranslation_DoesNotTriggerEvent()
        {
            // Arrange
            var displayQueue = new TranslationTaskQueue();
            var displayLoop = new DisplayLoop(displayQueue);
            int eventCount = 0;

            displayLoop.OnTranslationDisplayed += (text) =>
            {
                eventCount++;
            };

            // Act
            var (text, delay) = await displayLoop.ProcessDisplay();

            // Assert
            Assert.Equal(0, eventCount);
        }

        [Fact]
        public void GetLastDisplayedText_ReturnsLastText()
        {
            // Arrange
            var displayQueue = new TranslationTaskQueue();
            var displayLoop = new DisplayLoop(displayQueue);

            // Act
            displayQueue.Enqueue(token => Task.FromResult(("Test translation", false)), "Test");
            var last = displayLoop.GetLastDisplayedText();

            // Assert
            Assert.Empty(last); // Not displayed yet
        }

        [Fact]
        public void Reset_ClearsLastDisplayedText()
        {
            // Arrange
            var displayQueue = new TranslationTaskQueue();
            var displayLoop = new DisplayLoop(displayQueue);

            // Act
            displayLoop.Reset();
            var last = displayLoop.GetLastDisplayedText();

            // Assert
            Assert.Empty(last);
        }

        [Fact]
        public async Task ProcessDisplay_WithErrorMessage_TriggersEvent()
        {
            // Arrange
            var displayQueue = new TranslationTaskQueue();
            var displayLoop = new DisplayLoop(displayQueue);
            string? capturedText = null;

            displayLoop.OnTranslationDisplayed += (text) =>
            {
                capturedText = text;
            };

            // Enqueue an error message
            displayQueue.Enqueue(token => Task.FromResult(("[ERROR] Translation failed", false)), "Original");

            // Act
            await Task.Delay(100);
            var (text, delay) = await displayLoop.ProcessDisplay();

            // Assert
            Assert.NotNull(capturedText);
            Assert.Contains("[ERROR]", capturedText);
        }
    }
}
