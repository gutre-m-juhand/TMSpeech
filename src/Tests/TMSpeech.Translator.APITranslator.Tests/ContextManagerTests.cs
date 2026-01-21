using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMSpeech.Translator.APITranslator.Utils;
using Xunit;

namespace TMSpeech.Translator.APITranslator.Tests
{
    public class ContextManagerTests
    {
        [Fact]
        public void AddContext_WithValidData_ShouldStoreContext()
        {
            var manager = new ContextManager(maxContextCount: 3);
            manager.AddContext("Hello", "你好", "zh");

            Assert.Equal(1, manager.Count);
        }

        [Fact]
        public void AddContext_WithMultipleContexts_ShouldStoreAll()
        {
            var manager = new ContextManager(maxContextCount: 3);
            manager.AddContext("Hello", "你好", "zh");
            manager.AddContext("World", "世界", "zh");
            manager.AddContext("Test", "测试", "zh");

            Assert.Equal(3, manager.Count);
        }

        [Fact]
        public void GetPreviousContext_WithSingleContext_ShouldReturnIt()
        {
            var manager = new ContextManager(maxContextCount: 3);
            manager.AddContext("Hello", "你好", "zh");

            var context = manager.GetPreviousContext(count: 1);

            Assert.Contains("Hello", context);
        }

        [Fact]
        public void GetPreviousContext_WithMultipleContexts_ShouldReturnCombined()
        {
            var manager = new ContextManager(maxContextCount: 3);
            manager.AddContext("Hello", "你好", "zh");
            manager.AddContext("World", "世界", "zh");

            var context = manager.GetPreviousContext(count: 2);

            Assert.Contains("Hello", context);
            Assert.Contains("World", context);
        }

        [Fact]
        public void GetPreviousContext_WithEmptyManager_ShouldReturnEmpty()
        {
            var manager = new ContextManager(maxContextCount: 3);

            var context = manager.GetPreviousContext(count: 3);

            Assert.Equal(string.Empty, context);
        }

        [Fact]
        public void AddContext_ExceedingMaxCount_ShouldRemoveOldest()
        {
            var manager = new ContextManager(maxContextCount: 2);
            manager.AddContext("First", "第一", "zh");
            manager.AddContext("Second", "第二", "zh");
            manager.AddContext("Third", "第三", "zh");

            Assert.Equal(2, manager.Count);
            
            // Verify that "First" was removed
            var context = manager.GetPreviousContext(count: 2);
            Assert.DoesNotContain("First", context);
            Assert.Contains("Second", context);
            Assert.Contains("Third", context);
        }

        [Fact]
        public void Clear_ShouldRemoveAllContexts()
        {
            var manager = new ContextManager(maxContextCount: 3);
            manager.AddContext("Hello", "你好", "zh");
            manager.AddContext("World", "世界", "zh");

            manager.Clear();

            Assert.Equal(0, manager.Count);
            Assert.Equal(string.Empty, manager.GetPreviousContext(count: 3));
        }

        [Fact]
        public void AddContext_WithEmptySourceText_ShouldNotAdd()
        {
            var manager = new ContextManager(maxContextCount: 3);
            manager.AddContext("", "你好", "zh");

            Assert.Equal(0, manager.Count);
        }

        [Fact]
        public void AddContext_WithEmptyTranslatedText_ShouldNotAdd()
        {
            var manager = new ContextManager(maxContextCount: 3);
            manager.AddContext("Hello", "", "zh");

            Assert.Equal(0, manager.Count);
        }

        [Fact]
        public void GetPreviousContext_WithCountGreaterThanAvailable_ShouldReturnAll()
        {
            var manager = new ContextManager(maxContextCount: 3);
            manager.AddContext("Hello", "你好", "zh");
            manager.AddContext("World", "世界", "zh");

            var context = manager.GetPreviousContext(count: 10);

            Assert.Contains("Hello", context);
            Assert.Contains("World", context);
        }

        [Fact]
        public void GetPreviousContext_ShouldAddPunctuationIfMissing()
        {
            var manager = new ContextManager(maxContextCount: 3);
            manager.AddContext("Hello", "你好", "zh");

            var context = manager.GetPreviousContext(count: 1);

            // Should have added punctuation
            Assert.True(context.EndsWith("。") || context.EndsWith("."));
        }

        [Fact]
        public async Task ContextManager_IsThreadSafe()
        {
            var manager = new ContextManager(maxContextCount: 100);
            var tasks = new List<Task>();

            // Add contexts from multiple threads
            for (int i = 0; i < 10; i++)
            {
                int threadId = i;
                tasks.Add(Task.Run(() =>
                {
                    for (int j = 0; j < 10; j++)
                    {
                        manager.AddContext($"Text{threadId}_{j}", $"翻译{threadId}_{j}", "zh");
                    }
                }));
            }

            await Task.WhenAll(tasks);

            // Should have 100 contexts (10 threads * 10 contexts each)
            Assert.Equal(100, manager.Count);
        }
    }
}
