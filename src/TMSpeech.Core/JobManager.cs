using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using TMSpeech.Core.Plugins;
using TMSpeech.Core.Services.Notification;

namespace TMSpeech.Core
{
    public enum JobStatus
    {
        Stopped,
        Running,
        Paused,
    }

    public static class JobManagerFactory
    {
        private static Lazy<JobManager> _instance = new(() => new JobManagerImpl());
        public static JobManager Instance => _instance.Value;
    }

    public abstract class JobManager
    {
        private JobStatus _status;

        public JobStatus Status
        {
            get => _status;
            set
            {
                _status = value;
                StatusChanged?.Invoke(this, value);
            }
        }

        public long RunningSeconds { get; protected set; }

        public event EventHandler<JobStatus> StatusChanged;
        public event EventHandler<SpeechEventArgs> TextChanged;
        public event EventHandler<SpeechEventArgs> SentenceDone;
        public event EventHandler<TranslationEventArgs> TranslationCompleted;
        public event EventHandler<long> RunningSecondsChanged;

        protected void OnTextChanged(SpeechEventArgs e) => TextChanged?.Invoke(this, e);
        protected void OnSentenceDone(SpeechEventArgs e) => SentenceDone?.Invoke(this, e);
        protected void OnTranslationCompleted(TranslationEventArgs e) => TranslationCompleted?.Invoke(this, e);
        protected void OnUpdateRunningSeconds(long seconds) => RunningSecondsChanged?.Invoke(this, seconds);

        public abstract void Start();
        public abstract void Pause();
        public abstract void Stop();
        public abstract Task<string> TestTranslateAsync(string text, string sourceLanguage, string targetLanguage);
    }

    public class JobManagerImpl : JobManager
    {
        private readonly PluginManager _pluginManager;


        internal JobManagerImpl()
        {
            _pluginManager = PluginManagerFactory.GetInstance();
        }

        private IAudioSource? _audioSource;
        private IRecognizer? _recognizer;
        private ITranslator? _translator;
        private HashSet<string> _sensitiveWords;
        private bool _disableInThisSentence = false;
        private string logFile;
        private string currentText = "";

        private void InitAudioSource()
        {
            var configAudioSource = ConfigManagerFactory.Instance.Get<string>(AudioSourceConfigTypes.AudioSource);
            var config = ConfigManagerFactory.Instance.Get<string>(AudioSourceConfigTypes.GetPluginConfigKey(configAudioSource));

            _audioSource = _pluginManager.AudioSources[configAudioSource];
            if (_audioSource != null)
            {
                _audioSource.LoadConfig(config);
                _audioSource.DataAvailable -= OnAudioSourceOnDataAvailable;
                _audioSource.DataAvailable += OnAudioSourceOnDataAvailable;
                _audioSource.ExceptionOccured -= OnPluginRunningExceptionOccurs;
                _audioSource.ExceptionOccured += OnPluginRunningExceptionOccurs;
            }
        }

        private Timer? _timer;


        private void OnAudioSourceOnDataAvailable(object? o, byte[] data)
        {
            // Console.WriteLine(o?.GetHashCode().ToString("x8") ?? "<null>");
            _recognizer?.Feed(data);
        }

        private void InitRecognizer()
        {
            var configRecognizer = ConfigManagerFactory.Instance.Get<string>(RecognizerConfigTypes.Recognizer);
            var config = ConfigManagerFactory.Instance.Get<string>(RecognizerConfigTypes.GetPluginConfigKey(configRecognizer));
            // default config
            if ((configRecognizer == null || configRecognizer.Length == 0) && _pluginManager.Recognizers.Count > 0)
            {
                configRecognizer = _pluginManager.Recognizers.Keys.First();
            }
            _recognizer = _pluginManager.Recognizers[configRecognizer];

            if (_recognizer != null)
            {
                _recognizer.LoadConfig(config);
                // https://stackoverflow.com/a/1104269
                // use -= first to prevent duplication.
                _recognizer.TextChanged -= OnRecognizerOnTextChanged;
                _recognizer.TextChanged += OnRecognizerOnTextChanged;
                _recognizer.SentenceDone -= OnRecognizerOnSentenceDone;
                _recognizer.SentenceDone += OnRecognizerOnSentenceDone;
                _recognizer.ExceptionOccured -= OnPluginRunningExceptionOccurs;
                _recognizer.ExceptionOccured += OnPluginRunningExceptionOccurs;
            }
        }

        private void InitTranslator()
        {
            // 检查是否启用翻译
            var enableTranslator = ConfigManagerFactory.Instance.Get<bool>(TranslatorConfigTypes.EnableTranslator);
            if (!enableTranslator)
            {
                _translator = null;
                return;
            }

            // 获取配置的翻译器
            var configTranslator = ConfigManagerFactory.Instance.Get<string>(TranslatorConfigTypes.Translator);
            
            // 如果未配置翻译器，则不初始化
            if (string.IsNullOrWhiteSpace(configTranslator))
            {
                _translator = null;
                return;
            }

            var config = ConfigManagerFactory.Instance.Get<string>(TranslatorConfigTypes.GetPluginConfigKey(configTranslator));
            _translator = _pluginManager.Translators[configTranslator];

            if (_translator != null)
            {
                _translator.LoadConfig(config);
                // 订阅翻译完成事件
                _translator.TranslationCompleted -= OnTranslatorOnTranslationCompleted;
                _translator.TranslationCompleted += OnTranslatorOnTranslationCompleted;
                _translator.ExceptionOccured -= OnPluginRunningExceptionOccurs;
                _translator.ExceptionOccured += OnPluginRunningExceptionOccurs;
            }
        }

        private void OnRecognizerOnSentenceDone(object? sender, SpeechEventArgs args)
        {
            // Save the sentense to log
            if (logFile != null && logFile.Length > 0)
            {
                try
                {
                    File.AppendAllText(logFile, string.Format("{0:T}: {1}\n", DateTime.Now, args.Text.Text));
                }
                catch (Exception ex)
                {
                    NotificationManager.Instance.Notify(
                        $"写入识别日志失败: {ex.Message}",
                        "日志写入错误",
                        NotificationType.Warning);
                    System.Diagnostics.Debug.WriteLine($"Failed to write recognition log: {ex.Message}");
                    logFile = "";
                }
            }

            _disableInThisSentence = false;
            
            // 触发翻译（如果配置了翻译器）
            if (_translator != null && !string.IsNullOrWhiteSpace(args.Text.Text))
            {
                var targetLanguage = GetTargetLanguageFromConfig();
                System.Diagnostics.Debug.WriteLine($"[JobManager] Read TargetLanguage from config: '{targetLanguage}'");
                _translator.Translate(args.Text.Text, "auto", targetLanguage, 1);
            }

            OnSentenceDone(args);
            currentText = "";
        }

        private void OnRecognizerOnTextChanged(object? sender, SpeechEventArgs args)
        {
            currentText = args.Text.Text;
            if (!_disableInThisSentence)
            {
                var s = _sensitiveWords.FirstOrDefault(x => args.Text.Text.Contains(x));
                if (!string.IsNullOrEmpty(s))
                {
                    NotificationManager.Instance.Notify($"检测到敏感词：{s}", "敏感词", NotificationType.Warning);
                    _disableInThisSentence = true;
                }
            }

            // 触发翻译（如果配置了翻译器）
            if (_translator != null && !string.IsNullOrWhiteSpace(args.Text.Text))
            {
                var targetLanguage = GetTargetLanguageFromConfig();
                _translator.Translate(args.Text.Text, "auto", targetLanguage, 0);
            }

            OnTextChanged(args);
        }

        private string GetTargetLanguageFromConfig()
        {
            try
            {
                var configTranslator = ConfigManagerFactory.Instance.Get<string>(TranslatorConfigTypes.Translator);
                var configJson = ConfigManagerFactory.Instance.Get<string>(
                    TranslatorConfigTypes.GetPluginConfigKey(configTranslator));
                
                if (!string.IsNullOrEmpty(configJson))
                {
                    using var doc = JsonDocument.Parse(configJson);
                    if (doc.RootElement.TryGetProperty("TargetLanguage", out var targetLangElement))
                    {
                        var targetLang = targetLangElement.GetString();
                        if (!string.IsNullOrEmpty(targetLang))
                        {
                            return targetLang;
                        }
                    }
                }
            }
            catch { }
            
            return "zh-CN";  // 默认值
        }

        /// <summary>
        /// 翻译完成事件处理
        /// </summary>
        private void OnTranslatorOnTranslationCompleted(object? sender, TranslationEventArgs args)
        {
            // 发布翻译完成事件，供 UI 层处理
            OnTranslationCompleted(args);
        }

        private void StartRecognize()
        {
            InitSensitiveWords();
            InitAudioSource();
            InitRecognizer();
            InitTranslator();

            if (_audioSource == null || _recognizer == null)
            {
                Status = JobStatus.Stopped;
                NotificationManager.Instance.Notify("语音源或识别器初始化失败", "语音源或识别器为空！", NotificationType.Error);
                return;
            }


            try
            {
                _recognizer.Start();
            }
            catch (InvalidOperationException ex)
            {
                NotificationManager.Instance.Notify($"识别器启动失败：\n{ex.Message}", "启动失败",
                    NotificationType.Error);
                return;
            }
            catch (Exception ex)
            {
                NotificationManager.Instance.Notify($"识别器启动失败：\n{ex.Message}\n{ex.StackTrace}", "启动失败",
                    NotificationType.Error);
                return;
            }

            try
            {
                _audioSource.Start();
            }
            catch (InvalidOperationException ex)
            {
                _recognizer?.Stop();
                NotificationManager.Instance.Notify($"语音源启动失败：\n{ex.Message}", "启动失败",
                    NotificationType.Error);
                return;
            }
            catch (Exception ex)
            {
                _recognizer?.Stop();
                NotificationManager.Instance.Notify($"语音源启动失败：\n{ex.Message}\n{ex.StackTrace}", "启动失败",
                    NotificationType.Error);
                return;
            }

            // 启动翻译器（如果配置了）
            if (_translator != null)
            {
                try
                {
                    _translator.Start();
                }
                catch (Exception ex)
                {
                    NotificationManager.Instance.Notify($"翻译器启动失败：\n{ex.Message}", "启动失败",
                        NotificationType.Warning);
                    // 翻译器启动失败不影响识别功能，继续运行
                    _translator = null;
                }
            }

            var logPath = ConfigManagerFactory.Instance.Get<string>(GeneralConfigTypes.ResultLogPath).Trim();
            if (logPath.Length > 0)
            {
                Directory.CreateDirectory(logPath);
                logFile = Path.Combine(logPath, string.Format("{0:yy-MM-dd-HH-mm-ss}.txt", DateTime.Now));
            } else
            {
                logFile = "";
            }

            if (Status == JobStatus.Stopped) RunningSeconds = 0;

            Status = JobStatus.Running;

            _timer = new Timer(TimerCallback, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
        }

        private void InitSensitiveWords()
        {
            var sensitiveWords = ConfigManagerFactory.Instance.Get<string>(NotificationConfigTypes.SensitiveWords);
            if (string.IsNullOrWhiteSpace(sensitiveWords))
            {
                _sensitiveWords = new HashSet<string>();
                return;
            }

            _sensitiveWords = new HashSet<string>(sensitiveWords.Split(new[] { ',', '，', '\n' },
                StringSplitOptions.RemoveEmptyEntries));
        }

        private void OnPluginRunningExceptionOccurs(object? e, Exception ex)
        {
            NotificationManager.Instance.Notify($"插件运行异常:\n ({e?.GetType().Module.Name})：{ex.Message}",
                "插件异常", NotificationType.Error);
            // 只能在主线程stop。
            // Stop();
        }


        private void TimerCallback(object? state)
        {
            RunningSeconds++;
            OnUpdateRunningSeconds(RunningSeconds);
        }

        private void StopRecognize()
        {
            try
            {
                _audioSource?.Stop();
                _recognizer?.Stop();
                _translator?.Stop();
            }
            catch (Exception ex)
            {
                NotificationManager.Instance.Notify($"停止失败：\n{ex.Message}", "停止失败", NotificationType.Fatal);
            }

            if (currentText != null && currentText.Length > 0)
            {
                OnRecognizerOnSentenceDone(_recognizer, new SpeechEventArgs{Text=new TextInfo(currentText)});
                currentText = "";
            }

            if (_audioSource != null)
            {
                _audioSource.DataAvailable -= OnAudioSourceOnDataAvailable;
                _audioSource.ExceptionOccured -= OnPluginRunningExceptionOccurs;
            }

            if (_recognizer != null)
            {
                _recognizer.TextChanged -= OnRecognizerOnTextChanged;
                _recognizer.SentenceDone -= OnRecognizerOnSentenceDone;
                _recognizer.ExceptionOccured -= OnPluginRunningExceptionOccurs;
            }

            if (_translator != null)
            {
                _translator.TranslationCompleted -= OnTranslatorOnTranslationCompleted;
                _translator.ExceptionOccured -= OnPluginRunningExceptionOccurs;
            }

            _audioSource = null;
            _recognizer = null;
            _translator = null;
        }

        public override void Start()
        {
            if (Status == JobStatus.Running) return;
            StartRecognize();
        }

        public override void Pause()
        {
            if (Status == JobStatus.Running) StopRecognize();
            Status = JobStatus.Paused;

            _timer?.Dispose();
            _timer = null;
        }

        public override void Stop()
        {
            if (Status == JobStatus.Running) StopRecognize();
            Status = JobStatus.Stopped;

            // Clear text when stopped
            var emptyTextArg = new SpeechEventArgs();
            emptyTextArg.Text = new TextInfo(string.Empty);
            // OnSentenceDone(emptyTextArg); // TODO unable to save existing text.
            OnTextChanged(emptyTextArg);

            _timer?.Dispose();
            _timer = null;
        }

        public override async Task<string> TestTranslateAsync(string text, string sourceLanguage, string targetLanguage)
        {
            // 检查是否启用翻译
            var enableTranslator = ConfigManagerFactory.Instance.Get<bool>(TranslatorConfigTypes.EnableTranslator);
            if (!enableTranslator)
            {
                return "[ERROR] 翻译器未启用";
            }

            // 获取配置的翻译器
            var configTranslator = ConfigManagerFactory.Instance.Get<string>(TranslatorConfigTypes.Translator);
            if (string.IsNullOrWhiteSpace(configTranslator))
            {
                return "[ERROR] 未选择翻译器";
            }

            var translator = _pluginManager.Translators[configTranslator];
            if (translator == null)
            {
                return "[ERROR] 翻译器不可用";
            }

            try
            {
                // 加载配置
                var config = ConfigManagerFactory.Instance.Get<string>(
                    TranslatorConfigTypes.GetPluginConfigKey(configTranslator));
                translator.LoadConfig(config);

                // 短暂启动翻译器
                translator.Init();
                translator.Start();

                // 等待一下让三循环启动
                await Task.Delay(100);

                // 调用翻译 - 添加句号，直接 SentenceDone 级别调用
                var tcs = new TaskCompletionSource<string>();
                string result = "";
                
                EventHandler<TranslationEventArgs>? handler = null;
                handler = (s, e) =>
                {
                    result = e.TranslatedText;
                    translator.TranslationCompleted -= handler;
                    tcs.SetResult(result);
                };

                translator.TranslationCompleted += handler;
                
                // 开始计时 API 调用
                var apiSw = System.Diagnostics.Stopwatch.StartNew();
                translator.Translate(text + ".", sourceLanguage, targetLanguage, 1);  // eventType=1: SentenceDone，添加英文句号
                
                // 等待结果，最多 5 秒
                var testTask = tcs.Task;
                var completed = await Task.WhenAny(testTask, Task.Delay(5000));
                apiSw.Stop();

                // 停止翻译器
                translator.Stop();
                translator.Destroy();

                if (completed == testTask && !string.IsNullOrEmpty(result) && !result.Contains("[ERROR]"))
                {
                    // 返回结果和 API 调用耗时
                    return $"{result}|{apiSw.ElapsedMilliseconds}ms";
                }
                else
                {
                    return "[ERROR] 超时或无响应";
                }
            }
            catch (Exception ex)
            {
                return $"[ERROR] {ex.Message}";
            }
        }
    }
}
