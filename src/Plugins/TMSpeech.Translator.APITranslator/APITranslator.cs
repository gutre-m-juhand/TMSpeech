using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TMSpeech.Core.Plugins;
using TMSpeech.Translator.APITranslator.APIs;
using TMSpeech.Translator.APITranslator.Loops;
using TMSpeech.Translator.APITranslator.Utils;

namespace TMSpeech.Translator.APITranslator;

/// <summary>
/// API 翻译器实现
/// 
/// 使用三循环架构处理翻译：
/// - SyncLoop：文本缓冲和合并，检测句子结束，判断是否需要翻译
/// - TranslateLoop：调用翻译 API，处理异常和超时
/// - DisplayLoop：管理显示队列，实现延迟显示逻辑
/// </summary>
public class APITranslator : ITranslator
{
    public string GUID => "TMSpeech.Translator.APITranslator";
    public string Name => "API 翻译器";
    public string Description => "通过 API 服务进行文本翻译，支持多种翻译服务";
    public string Version => "1.0.0";
    public string SupportVersion => "any";
    public string Author => "Built-in";
    public string Url => "";
    public string License => "MIT License";
    public string Note => "使用外部 API 进行文本翻译";

    public bool Available => true;

    public event EventHandler<TranslationEventArgs>? TranslationCompleted;
    public event EventHandler<Exception>? ExceptionOccured;

    // Configuration
    private APITranslatorConfig _config = new();
    private bool _isRunning;
    private readonly object _lockObject = new();

    // Queues and loops
    private Queue<string> _pendingTextQueue = new();
    private TranslationTaskQueue _displayQueue = new();
    private SyncLoop? _syncLoop;
    private TranslateLoop? _translateLoop;
    private DisplayLoop? _displayLoop;

    // Timers
    private Timer? _syncTimer;
    private Timer? _translateTimer;
    private Timer? _displayTimer;

    // Cancellation
    private CancellationTokenSource? _cancellationTokenSource;

    // Target language (set by Translate method)
    private string _targetLanguage = "en";

    // Async operation flags to prevent concurrent execution
    private bool _translateLoopRunning = false;
    private bool _displayLoopRunning = false;

    // History log for debugging
    private List<string> _historyLog = new();
    private readonly object _historyLock = new();
    private const string HISTORY_LOG_FILE = "translator_history.log";

    // Context-aware translation
    private ContextManager? _contextManager;
    private bool _contextAwareEnabled = false;

    // Constants
    // 频率控制参数
    // Sherpa 输出间隔 ~20-40ms，比 LiveCaption (~100ms+) 快约 75ms
    // 在参考项目的 MAX_SYNC_INTERVAL 基础上加 75ms 补偿
    private const int MAX_SYNC_INTERVAL = 6;       // 3 + 3 = 150ms（加 75ms 补偿）
    private const int MAX_IDLE_INTERVAL = 100;     // 100 × 25ms = 2500ms（等待完整从句）
    private const int SYNC_LOOP_INTERVAL_MS = 25;
    private const int TRANSLATE_LOOP_INTERVAL_MS = 40;
    private const int DISPLAY_LOOP_INTERVAL_MS = 40;

    /// <summary>
    /// 判断是否应该启用上下文感知翻译
    /// 只对谷歌和 LLM 类 API 启用
    /// </summary>
    private bool ShouldEnableContextAware(string apiName)
    {
        return apiName switch
        {
            "Google" => true,
            "Google2" => true,
            "OpenAI" => true,
            "Ollama" => true,
            "OpenRouter" => true,
            _ => false  // MT、DeepL、LibreTranslate 等不启用
        };
    }

    /// <summary>
    /// 创建配置编辑器
    /// </summary>
    public IPluginConfigEditor CreateConfigEditor() => new APITranslatorConfigEditor();

    /// <summary>
    /// 加载配置
    /// </summary>
    public void LoadConfig(string config)
    {
        if (!string.IsNullOrEmpty(config))
        {
            try
            {
                _config = JsonSerializer.Deserialize<APITranslatorConfig>(config) ?? new();
            }
            catch
            {
                _config = new APITranslatorConfig();
            }
        }
    }

    /// <summary>
    /// 初始化翻译器
    /// </summary>
    public void Init()
    {
        // 初始化翻译器资源
        // 例如：验证 API 连接、加载本地缓存等
    }

    /// <summary>
    /// 销毁翻译器
    /// </summary>
    public void Destroy()
    {
        Stop();
    }

    /// <summary>
    /// 启动翻译器
    /// </summary>
    public void Start()
    {
        lock (_lockObject)
        {
            if (_isRunning)
            {
                throw new InvalidOperationException("API 翻译器：已在运行中");
            }

            _isRunning = true;
            _cancellationTokenSource = new CancellationTokenSource();

            // Initialize queues and loops
            _pendingTextQueue = new Queue<string>();
            _displayQueue = new TranslationTaskQueue();
            _syncLoop = new SyncLoop(_pendingTextQueue);
            _translateLoop = new TranslateLoop(_pendingTextQueue, _displayQueue);
            _displayLoop = new DisplayLoop(_displayQueue);

            _translateLoop.SetCancellationToken(_cancellationTokenSource.Token);

            // Initialize context manager for context-aware translation
            _contextManager = new ContextManager(maxContextCount: 10);  // 增加到 10 条历史
            
            // 只对谷歌和 LLM 类 API 启用上下文感知翻译
            // REST API（如 MT）不支持上下文感知翻译
            _contextAwareEnabled = ShouldEnableContextAware(_config.ApiName);

            // Hook up display loop event
            _displayLoop.OnTranslationDisplayed += OnTranslationDisplayed;

            // Start timers
            _syncTimer = new Timer(SyncLoopTick, null, SYNC_LOOP_INTERVAL_MS, SYNC_LOOP_INTERVAL_MS);
            _translateTimer = new Timer(TranslateLoopTick, null, TRANSLATE_LOOP_INTERVAL_MS, TRANSLATE_LOOP_INTERVAL_MS);
            _displayTimer = new Timer(DisplayLoopTick, null, DISPLAY_LOOP_INTERVAL_MS, DISPLAY_LOOP_INTERVAL_MS);
        }
    }

    /// <summary>
    /// 停止翻译器
    /// </summary>
    public void Stop()
    {
        lock (_lockObject)
        {
            _isRunning = false;

            // Stop timers
            _syncTimer?.Dispose();
            _translateTimer?.Dispose();
            _displayTimer?.Dispose();
            _syncTimer = null;
            _translateTimer = null;
            _displayTimer = null;

            // Cancel pending operations
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;

            // Clear queues
            _pendingTextQueue?.Clear();
            _displayQueue = new TranslationTaskQueue();

            // Reset loops
            _syncLoop?.Reset();
            _displayLoop?.Reset();

            // Clear context manager
            _contextManager?.Clear();
            _contextManager = null;

            // Reset async operation flags
            _translateLoopRunning = false;
            _displayLoopRunning = false;
        }
    }

    /// <summary>
    /// 翻译文本（被动接收）
    /// 
    /// 识别器识别出新文本后调用此方法，文本被投入待处理队列。
    /// 三个循环在后台异步处理：
    /// 1. SyncLoop：缓冲和合并文本
    /// 2. TranslateLoop：调用翻译 API
    /// 3. DisplayLoop：管理显示
    /// </summary>
    public void Translate(string text, string sourceLanguage = "auto", string targetLanguage = "en", int eventType = 0)
    {
        if (!_isRunning)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        // Save target language for use in TranslateLoop
        _targetLanguage = targetLanguage;
        
        System.Diagnostics.Debug.WriteLine($"[APITranslator.Translate] Received targetLanguage: '{targetLanguage}'");
        System.Diagnostics.Debug.WriteLine($"[APITranslator.Translate] Saved to _targetLanguage: '{_targetLanguage}'");
        System.Diagnostics.Debug.WriteLine($"[APITranslator.Translate] config.TargetLanguage: '{_config.TargetLanguage}'");
        
        // 预处理文本：转小写
        string processedText = text.ToLower();
        
        // 如果是 SentenceDone 且没有标点，加句号
        if (eventType == 1 && processedText.Length > 0)
        {
            char lastChar = processedText[^1];
            if (Array.IndexOf(TextUtil.PUNC_EOS, lastChar) == -1)
            {
                processedText += "。";
            }
        }
        
        string eventName = eventType == 0 ? "TextChanged" : "SentenceDone";
        LogHistory($"[INPUT] {eventName}: '{processedText}' -> Target: {targetLanguage}");

        // Passively inject text into sync loop
        if (_syncLoop != null)
        {
            _syncLoop.ProcessText(processedText);
        }
    }

    /// <summary>
    /// 获取支持的语言列表（异步）
    /// </summary>
    public async Task<List<string>> GetSupportedLanguagesAsync()
    {
        // 测试模式：返回固定语言列表
        await Task.Delay(100);
        return new List<string>
        {
            "auto",  // 自动检测
            "zh",    // 中文
            "en",    // 英文
            "ja",    // 日文
            "ko",    // 韩文
            "es",    // 西班牙文
            "fr",    // 法文
            "de",    // 德文
            "ru",    // 俄文
        };
    }

    /// <summary>
    /// SyncLoop timer tick - process text buffering and merging
    /// </summary>
    private void SyncLoopTick(object? state)
    {
        if (!_isRunning || _syncLoop == null)
            return;

        try
        {
            _syncLoop.ShouldTranslate(MAX_SYNC_INTERVAL, MAX_IDLE_INTERVAL);
        }
        catch (Exception ex)
        {
            ExceptionOccured?.Invoke(this, ex);
        }
    }

    /// <summary>
    /// TranslateLoop timer tick - process translation
    /// </summary>
    private void TranslateLoopTick(object? state)
    {
        if (!_isRunning || _translateLoop == null || _translateLoopRunning)
            return;

        try
        {
            _translateLoopRunning = true;
            _ = ProcessTranslateLoopAsync();
        }
        catch (Exception ex)
        {
            ExceptionOccured?.Invoke(this, ex);
            _translateLoopRunning = false;
        }
    }

    private async Task ProcessTranslateLoopAsync()
    {
        try
        {
            await _translateLoop!.ProcessTranslation(
                _targetLanguage,
                false,
                LogHistory,
                _contextManager,
                _contextAwareEnabled,
                _config.ApiName,
                _config);
        }
        catch (Exception ex)
        {
            ExceptionOccured?.Invoke(this, ex);
        }
        finally
        {
            _translateLoopRunning = false;
        }
    }

    /// <summary>
    /// DisplayLoop timer tick - process display updates
    /// </summary>
    private void DisplayLoopTick(object? state)
    {
        if (!_isRunning || _displayLoop == null || _displayLoopRunning)
            return;

        try
        {
            _displayLoopRunning = true;
            _ = ProcessDisplayLoopAsync();
        }
        catch (Exception ex)
        {
            ExceptionOccured?.Invoke(this, ex);
            _displayLoopRunning = false;
        }
    }

    private async Task ProcessDisplayLoopAsync()
    {
        try
        {
            await _displayLoop!.ProcessDisplay();
        }
        catch (Exception ex)
        {
            ExceptionOccured?.Invoke(this, ex);
        }
        finally
        {
            _displayLoopRunning = false;
        }
    }

    /// <summary>
    /// Handle translation display event
    /// </summary>
    private void OnTranslationDisplayed(string translatedText)
    {
        LogHistory($"[OUTPUT] Translated: '{translatedText}'");
        
        // 从 TranslateLoop 获取翻译插件实际使用的原文
        string originalText = _translateLoop?.LastOriginalText ?? string.Empty;
        
        TranslationCompleted?.Invoke(this, new TranslationEventArgs
        {
            OriginalText = originalText,
            TranslatedText = translatedText,
            SourceLanguage = "auto",
            TargetLanguage = _config.TargetLanguage,
            Time = DateTime.Now
        });
    }

    /// <summary>
    /// Log message to history with timestamp
    /// </summary>
    private void LogHistory(string message)
    {
        lock (_historyLock)
        {
            string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
            _historyLog.Add(logEntry);
            
            // Keep only last 1000 entries in memory
            if (_historyLog.Count > 1000)
            {
                _historyLog.RemoveRange(0, _historyLog.Count - 1000);
            }
            
            // Also write to file
            try
            {
                string logPath = Path.Combine(Path.GetTempPath(), HISTORY_LOG_FILE);
                File.AppendAllText(logPath, logEntry + Environment.NewLine);
            }
            catch
            {
                // Silently ignore file write errors
            }
        }
    }

    /// <summary>
    /// Get history log as string
    /// </summary>
    public string GetHistoryLog()
    {
        lock (_historyLock)
        {
            return string.Join(Environment.NewLine, _historyLog);
        }
    }
}
