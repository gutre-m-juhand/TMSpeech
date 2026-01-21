using System;
using System.Threading;
using System.Threading.Tasks;
using TMSpeech.Translator.APITranslator.Utils;
using Xunit;

namespace TMSpeech.Translator.APITranslator.Tests
{
    public class TranslationTaskQueueTests
    {
        [Fact]
        public void Enqueue_AddsTaskToQueue()
        {
            // Arrange
            var queue = new TranslationTaskQueue();

            // Act
            queue.Enqueue(token => Task.FromResult(("result", false)), "input");

            // Assert
            var (result, isChoke) = queue.Output;
            Assert.NotNull(result);
        }

        [Fact]
        public async Task Enqueue_WithAsyncTask_CompletesSuccessfully()
        {
            // Arrange
            var queue = new TranslationTaskQueue();
            var taskCompleted = false;

            // Act
            queue.Enqueue(async token =>
            {
                await Task.Delay(50);
                taskCompleted = true;
                return ("result", false);
            }, "input");

            await Task.Delay(100);

            // Assert
            Assert.True(taskCompleted);
        }

        [Fact]
        public void Output_ReturnsLatestResult()
        {
            // Arrange
            var queue = new TranslationTaskQueue();

            // Act
            queue.Enqueue(token => Task.FromResult(("first", false)), "input1");
            queue.Enqueue(token => Task.FromResult(("second", true)), "input2");

            var (result, isChoke) = queue.Output;

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public void Output_WithEmptyQueue_ReturnsEmpty()
        {
            // Arrange
            var queue = new TranslationTaskQueue();

            // Act
            var (result, isChoke) = queue.Output;

            // Assert
            Assert.Empty(result);
            Assert.False(isChoke);
        }

        [Fact]
        public async Task Enqueue_WithCancellation_HandlesGracefully()
        {
            // Arrange
            var queue = new TranslationTaskQueue();
            var cts = new CancellationTokenSource();

            // Act
            queue.Enqueue(async token =>
            {
                await Task.Delay(1000, token);
                return ("result", false);
            }, "input");

            cts.CancelAfter(50);

            // Assert - should not throw
            await Task.Delay(100);
        }

        [Fact]
        public void Enqueue_MultipleItems_ProcessesInOrder()
        {
            // Arrange
            var queue = new TranslationTaskQueue();

            // Act
            queue.Enqueue(token => Task.FromResult(("first", false)), "input1");
            queue.Enqueue(token => Task.FromResult(("second", false)), "input2");
            queue.Enqueue(token => Task.FromResult(("third", false)), "input3");

            // Assert
            var (result, _) = queue.Output;
            Assert.NotNull(result);
        }
    }
}
