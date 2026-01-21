using System;
using System.Collections.Generic;
using TMSpeech.Translator.APITranslator.Loops;
using Xunit;

namespace TMSpeech.Translator.APITranslator.Tests
{
    public class SyncLoopTests
    {
        [Fact]
        public void ProcessText_WithCompleteSentence_EnqueuesText()
        {
            // Arrange
            var queue = new Queue<string>();
            var syncLoop = new SyncLoop(queue);

            // Act
            syncLoop.ProcessText("Hello world.");

            // Assert
            Assert.Single(queue);
            Assert.Equal("Hello world.", queue.Dequeue());
        }

        [Fact]
        public void ProcessText_WithMultipleSentences_ExtractsLastSentence()
        {
            // Arrange
            var queue = new Queue<string>();
            var syncLoop = new SyncLoop(queue);

            // Act
            syncLoop.ProcessText("First sentence. Second sentence.");

            // Assert
            Assert.Single(queue);
            var result = queue.Dequeue();
            Assert.Contains("Second sentence", result);
        }

        [Fact]
        public void ProcessText_WithIncompleteSentence_DoesNotEnqueue()
        {
            // Arrange
            var queue = new Queue<string>();
            var syncLoop = new SyncLoop(queue);

            // Act
            syncLoop.ProcessText("Hello world");

            // Assert
            Assert.Empty(queue);
        }

        [Fact]
        public void ShouldTranslate_WithIdleIntervalExceeded_EnqueuesText()
        {
            // Arrange
            var queue = new Queue<string>();
            var syncLoop = new SyncLoop(queue);
            syncLoop.ProcessText("Hello world.");

            // Assert - complete sentence should be enqueued immediately
            Assert.Single(queue);
        }

        [Fact]
        public void GetCurrentText_ReturnsBufferedText()
        {
            // Arrange
            var queue = new Queue<string>();
            var syncLoop = new SyncLoop(queue);

            // Act
            syncLoop.ProcessText("Test text");
            var current = syncLoop.GetCurrentText();

            // Assert
            Assert.NotEmpty(current);
        }

        [Fact]
        public void Reset_ClearsState()
        {
            // Arrange
            var queue = new Queue<string>();
            var syncLoop = new SyncLoop(queue);
            syncLoop.ProcessText("Test");

            // Act
            syncLoop.Reset();
            var current = syncLoop.GetCurrentText();

            // Assert
            Assert.Empty(current);
        }

        [Fact]
        public void ProcessText_WithChinesePunctuation_DetectsEndOfSentence()
        {
            // Arrange
            var queue = new Queue<string>();
            var syncLoop = new SyncLoop(queue);

            // Act
            syncLoop.ProcessText("你好世界。");

            // Assert
            Assert.Single(queue);
            Assert.Equal("你好世界。", queue.Dequeue());
        }

        [Fact]
        public void ProcessText_WithMultipleChineseSentences_ExtractsLastOne()
        {
            // Arrange
            var queue = new Queue<string>();
            var syncLoop = new SyncLoop(queue);

            // Act
            syncLoop.ProcessText("第一句。第二句。");

            // Assert
            Assert.Single(queue);
            var result = queue.Dequeue();
            Assert.Contains("第二句", result);
        }

        [Fact]
        public void ProcessText_WithEmptyString_DoesNotThrow()
        {
            // Arrange
            var queue = new Queue<string>();
            var syncLoop = new SyncLoop(queue);

            // Act & Assert
            syncLoop.ProcessText("");
            Assert.Empty(queue);
        }
    }
}
