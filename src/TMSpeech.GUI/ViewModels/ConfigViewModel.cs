using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia.Media;
using ReactiveUI;
using TMSpeech.Core;
using TMSpeech.Core.Plugins;

namespace TMSpeech.GUI.ViewModels
{
    class ConfigJsonValueAttribute : Attribute
    {
        public string Key { get; }

        public ConfigJsonValueAttribute(string key)
        {
            Key = key;
        }

        public ConfigJsonValueAttribute()
        {
        }
    }


    public abstract class SectionConfigViewModelBase : ViewModelBase
    {
        protected virtual string SectionName => "";

        private string PropertyToKey(PropertyInfo prop)
        {
            var key = prop.GetCustomAttributes(typeof(ConfigJsonValueAttribute), false)
                .Select(u => u as ConfigJsonValueAttribute)
                .FirstOrDefault()?.Key;

            if (key != null) 
            {
                System.Diagnostics.Debug.WriteLine($"[PropertyToKey] Property {prop.Name} has explicit key: {key}");
                return key;
            }
            
            var defaultKey = $"{SectionName}.{prop.Name}";
            System.Diagnostics.Debug.WriteLine($"[PropertyToKey] Property {prop.Name} using default key: {defaultKey}");
            return defaultKey;
        }

        public virtual Dictionary<string, object> Serialize()
        {
            var ret = new Dictionary<string, object>();
            this.GetType().GetProperties()
                .Where(p => p.GetCustomAttributes(typeof(ConfigJsonValueAttribute), false).Length > 0)
                .ToList()
                .ForEach(p =>
                {
                    var value = p.GetValue(this);
                    ret[PropertyToKey(p)] = value;
                    System.Diagnostics.Debug.WriteLine($"[Serialize] Property: {p.Name}, Key: {PropertyToKey(p)}, Value: {value}");
                });
            return ret;
        }

        public static object? ChangeType(object? value, Type type)
        {
            // 特殊处理List<int>类型
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                var elementType = type.GetGenericArguments()[0];
                if (elementType == typeof(int))
                {
                    // 处理 List<int>
                    if (value is System.Text.Json.JsonElement jsonElement)
                    {
                        var intList = new List<int>();
                        foreach (var item in jsonElement.EnumerateArray())
                        {
                            intList.Add(item.GetInt32());
                        }
                        return intList;
                    }
                    else
                    {
                        throw new InvalidCastException($"Expected JsonElement for List<int> property!");
                    }
                }
            }
            return Convert.ChangeType(value, type);
        }

        public virtual void Deserialize(IReadOnlyDictionary<string, object> dict)
        {
            this.GetType().GetProperties()
                .Where(p => p.GetCustomAttributes(typeof(ConfigJsonValueAttribute), false).Length > 0)
                .ToList()
                .ForEach(p =>
                {
                    if (!dict.ContainsKey(PropertyToKey(p))) 
                    {
                        System.Diagnostics.Debug.WriteLine($"[Deserialize] Property {p.Name} not found in dict with key {PropertyToKey(p)}");
                        return;
                    }
                    var value = dict[PropertyToKey(p)];
                    var type = p.PropertyType;

                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"[Deserialize] Setting {p.Name} = {value} (type: {type.Name})");
                        p.SetValue(this, ChangeType(value, type));
                        System.Diagnostics.Debug.WriteLine($"[Deserialize] Successfully set {p.Name}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"属性 {p.Name} 转换失败: {ex.Message}");
                    }
                });
        }

        public void Load()
        {
            var dict = ConfigManagerFactory.Instance.GetAll();
            System.Diagnostics.Debug.WriteLine($"[Load] All config keys: {string.Join(", ", dict.Keys)}");
            
            var filteredDict = dict.Where(x => ConfigManager.IsInSection(x.Key, SectionName))
                .ToDictionary(x => x.Key, x => x.Value);
            
            System.Diagnostics.Debug.WriteLine($"[Load] Filtered keys for section '{SectionName}': {string.Join(", ", filteredDict.Keys)}");
            
            Deserialize(filteredDict);
        }

        public void Apply()
        {
            var dict = Serialize();
            System.Diagnostics.Debug.WriteLine($"[SectionConfigViewModelBase.Apply] Section: {SectionName}, Serialized: {JsonSerializer.Serialize(dict)}");
            ConfigManagerFactory.Instance.BatchApply(dict.Where(u => u.Value != null)
                .ToDictionary(x => x.Key, x => x.Value));
        }

        public SectionConfigViewModelBase()
        {
            System.Diagnostics.Debug.WriteLine($"[SectionConfigViewModelBase] Constructor called for {this.GetType().Name}");
            Load();
            System.Diagnostics.Debug.WriteLine($"[SectionConfigViewModelBase] Load() completed");
            
            this.PropertyChanged += (sender, args) =>
            {
                var propName = args.PropertyName;
                var type = sender.GetType();
                System.Diagnostics.Debug.WriteLine($"[SectionConfigViewModelBase.PropertyChanged] Property changed: {propName}");

                var prop = sender.GetType().GetProperty(propName);
                if (prop == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[SectionConfigViewModelBase.PropertyChanged] Property {propName} not found");
                    return;
                }

                if (prop.GetCustomAttributes(false)
                    .Any(u => u.GetType() == typeof(ConfigJsonValueAttribute)))
                {
                    System.Diagnostics.Debug.WriteLine($"[SectionConfigViewModelBase.PropertyChanged] Property {propName} has ConfigJsonValue attribute, calling Apply()");
                    Apply();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[SectionConfigViewModelBase.PropertyChanged] Property {propName} does NOT have ConfigJsonValue attribute");
                }
            };
            System.Diagnostics.Debug.WriteLine($"[SectionConfigViewModelBase] PropertyChanged event handler registered");
        }
    }

    public class ConfigViewModel : ViewModelBase
    {
        public GeneralSectionConfigViewModel GeneralSectionConfig { get; } = new GeneralSectionConfigViewModel();

        public AppearanceSectionConfigViewModel AppearanceSectionConfig { get; } =
            new AppearanceSectionConfigViewModel();

        public AudioSectionConfigViewModel AudioSectionConfig { get; } = new AudioSectionConfigViewModel();
        public RecognizeSectionConfigViewModel RecognizeSectionConfig { get; } = new RecognizeSectionConfigViewModel();
        public NotificationConfigViewModel NotificationConfig { get; } = new NotificationConfigViewModel();
        public TranslatorConfigViewModel TranslatorConfig { get; } = new TranslatorConfigViewModel();

        [ObservableAsProperty]
        public bool IsNotRunning { get; }

        [Reactive]
        public int CurrentTab { get; set; } = 1;

        public ConfigViewModel()
        {
            Observable.Return(JobManagerFactory.Instance.Status != JobStatus.Running).Merge(
                Observable.FromEventPattern<JobStatus>(
                    x => JobManagerFactory.Instance.StatusChanged += x,
                    x => JobManagerFactory.Instance.StatusChanged -= x
                ).Select(x => x.EventArgs != JobStatus.Running)
            ).ToPropertyEx(this, x => x.IsNotRunning);
        }
    }

    public class GeneralSectionConfigViewModel : SectionConfigViewModelBase
    {
        protected override string SectionName => GeneralConfigTypes.SectionName;

        [Reactive]
        [ConfigJsonValue]
        public string Language { get; set; }

        public ObservableCollection<KeyValuePair<string, string>> LanguagesAvailable { get; } =
        [
            new KeyValuePair<string, string>("zh-cn", "简体中文"),
            new KeyValuePair<string, string>("en-us", "English"),
        ];

        //[Reactive]
        //[ConfigJsonValue]
        //public string UserDir { get; set; } = "D:\\TMSpeech";

        [Reactive]
        [ConfigJsonValue]
        public string ResultLogPath { get; set; }

        [Reactive]
        [ConfigJsonValue]
        public bool LaunchOnStartup { get; set; }

        [Reactive]
        [ConfigJsonValue]
        public bool StartOnLaunch { get; set; }

        [Reactive]
        [ConfigJsonValue]
        public bool AutoUpdate { get; set; }

        // Left, Top, Width, Height
        [Reactive]
        [ConfigJsonValue]
        public List<int> MainWindowLocation { get; set; } = [];
    }

    public class AppearanceSectionConfigViewModel : SectionConfigViewModelBase
    {
        protected override string SectionName => AppearanceConfigTypes.SectionName;

        public List<FontFamily> FontsAvailable { get; private set; }

        [Reactive]
        [ConfigJsonValue]
        public uint ShadowColor { get; set; }


        [Reactive]
        [ConfigJsonValue]
        public int ShadowSize { get; set; }


        [Reactive]
        [ConfigJsonValue]
        public string FontFamily { get; set; }

        [Reactive]
        [ConfigJsonValue]
        public int FontSize { get; set; }

        [Reactive]
        [ConfigJsonValue]
        public uint FontColor { get; set; }

        [Reactive]
        [ConfigJsonValue]
        public uint MouseHover { get; set; }

        [Reactive]
        [ConfigJsonValue]
        public int TextAlign { get; set; }

        [Reactive]
        [ConfigJsonValue(AppearanceConfigTypes.BackgroundColor)]
        public uint BackgroundColor { get; set; }

        public List<KeyValuePair<int, string>> TextAligns { get; } =
        [
            new KeyValuePair<int, string>(AppearanceConfigTypes.TextAlignEnum.Left, "左对齐"),
            new KeyValuePair<int, string>(AppearanceConfigTypes.TextAlignEnum.Center, "居中对齐"),
            new KeyValuePair<int, string>(AppearanceConfigTypes.TextAlignEnum.Right, "右对齐"),
            new KeyValuePair<int, string>(AppearanceConfigTypes.TextAlignEnum.Justify, "两端对齐"),
        ];

        public AppearanceSectionConfigViewModel()
        {
            FontsAvailable = FontManager.Current.SystemFonts.ToList();
        }
    }

    public class NotificationConfigViewModel : SectionConfigViewModelBase
    {
        protected override string SectionName => NotificationConfigTypes.SectionName;


        public List<KeyValuePair<int, string>> NotificaitonTypes { get; } =
        [
            new KeyValuePair<int, string>(NotificationConfigTypes.NotificationTypeEnum.None, "关闭通知"),
            new KeyValuePair<int, string>(NotificationConfigTypes.NotificationTypeEnum.System, "系统通知 (暂不支持 macOS)"),
            // new KeyValuePair<int, string>(NotificationTypeEnum.Custom, "TMSpeech 通知"),
        ];

        [Reactive]
        [ConfigJsonValue]
        public int NotificationType { get; set; } = NotificationConfigTypes.NotificationTypeEnum.System;

        [Reactive]
        [ConfigJsonValue]
        public string SensitiveWords { get; set; } = "";
    }

    public class AudioSectionConfigViewModel : SectionConfigViewModelBase
    {
        [Reactive]
        [ConfigJsonValue]
        public string AudioSource { get; set; }

        [ObservableAsProperty]
        public IReadOnlyDictionary<string, Core.Plugins.IAudioSource> AudioSourcesAvailable { get; }

        [ObservableAsProperty]
        public IPluginConfigEditor? ConfigEditor { get; }

        [Reactive]
        public string PluginConfig { get; set; } = "";

        public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

        public IReadOnlyDictionary<string, Core.Plugins.IAudioSource> Refresh()
        {
            var plugins = Core.Plugins.PluginManagerFactory.GetInstance().AudioSources;
            if (AudioSource == "" && plugins.Count >= 1)
                AudioSource = plugins.First().Key;
            return plugins;
        }

        public override Dictionary<string, object> Serialize()
        {
            var ret = new Dictionary<string, object>
            {
                { "audio.source", AudioSource },
            };
            // 不在此处保存插件配置，由 PluginConfig 属性变化时单独触发保存
            // 避免 AudioSource 切换时，将旧配置保存到新音频源的配置键

            return ret;
        }

        public override void Deserialize(IReadOnlyDictionary<string, object> dict)
        {
            if (dict.ContainsKey(AudioSourceConfigTypes.AudioSource))
            {
                AudioSource = dict[AudioSourceConfigTypes.AudioSource]?.ToString() ?? "";
            }

            if (dict.ContainsKey(AudioSourceConfigTypes.GetPluginConfigKey(AudioSource)))
            {
                PluginConfig = dict[AudioSourceConfigTypes.GetPluginConfigKey(AudioSource)]?.ToString() ?? "";
            }
        }

        public AudioSectionConfigViewModel()
        {
            this.RefreshCommand = ReactiveCommand.Create(() => { });
            this.RefreshCommand.Merge(Observable.Return(Unit.Default))
                .SelectMany(u => Observable.FromAsync(() => Task.Run(() => Refresh())))
                .ToPropertyEx(this, x => x.AudioSourcesAvailable);

            this.WhenAnyValue(u => u.AudioSource, u => u.AudioSourcesAvailable)
                .Where((u) => u.Item1 != null && u.Item2 != null)
                .Select(u => u.Item1)
                .Where(x => !string.IsNullOrEmpty(x))
                .DistinctUntilChanged()
                .Select(x => AudioSourcesAvailable.FirstOrDefault(u => u.Key == x))
                .Select(x =>
                {
                    var plugin = x.Value;
                    var editor = plugin?.CreateConfigEditor();
                    var config = ConfigManagerFactory.Instance.Get<string>(
                        AudioSourceConfigTypes.GetPluginConfigKey(AudioSource));
                    editor?.LoadConfigString(config);
                    return editor;
                })
                .ToPropertyEx(this, x => x.ConfigEditor);


            this.WhenAnyValue(x => x.ConfigEditor)
                .Subscribe(x =>
                {
                    var config =
                        ConfigManagerFactory.Instance.Get<string>(
                            AudioSourceConfigTypes.GetPluginConfigKey(AudioSource));
                    PluginConfig = config;
                });

            // 监听 PluginConfig 变化，手动保存到正确的配置键
            this.WhenAnyValue(x => x.PluginConfig)
                .Skip(1) // 跳过初始值
                .Subscribe(config =>
                {
                    if (!string.IsNullOrEmpty(AudioSource))
                    {
                        ConfigManagerFactory.Instance.Apply(
                            AudioSourceConfigTypes.GetPluginConfigKey(AudioSource),
                            config
                        );
                    }
                });
        }
    }


    public class RecognizeSectionConfigViewModel : SectionConfigViewModelBase
    {
        protected override string SectionName => "";

        [Reactive]
        [ConfigJsonValue]
        public string Recognizer { get; set; } = "";

        [ObservableAsProperty]
        public IReadOnlyDictionary<string, Core.Plugins.IRecognizer> RecognizersAvailable { get; }

        [ObservableAsProperty]
        public IPluginConfigEditor? ConfigEditor { get; }

        [Reactive]
        public string PluginConfig { get; set; } = "";

        public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

        public IReadOnlyDictionary<string, Core.Plugins.IRecognizer> Refresh()
        {
            var plugins = Core.Plugins.PluginManagerFactory.GetInstance().Recognizers;
            if (Recognizer == "" && plugins.Count >= 1)
                Recognizer = plugins.First().Key;
            return plugins;
        }

        public override Dictionary<string, object> Serialize()
        {
            var ret = new Dictionary<string, object>
            {
                { RecognizerConfigTypes.Recognizer, Recognizer },
            };
            // 不在此处保存插件配置，由 PluginConfig 属性变化时单独触发保存
            // 避免 Recognizer 切换时，将旧配置保存到新识别器的配置键

            return ret;
        }

        public override void Deserialize(IReadOnlyDictionary<string, object> dict)
        {
            if (dict.ContainsKey(RecognizerConfigTypes.Recognizer))
            {
                Recognizer = dict[RecognizerConfigTypes.Recognizer]?.ToString() ?? "";
            }

            if (dict.ContainsKey(RecognizerConfigTypes.GetPluginConfigKey(Recognizer)))
            {
                PluginConfig = dict[RecognizerConfigTypes.GetPluginConfigKey(Recognizer)]?.ToString() ?? "";
            }
        }

        public RecognizeSectionConfigViewModel()
        {
            this.RefreshCommand = ReactiveCommand.Create(() => { });
            this.RefreshCommand.Merge(Observable.Return(Unit.Default))
                .SelectMany(u => Observable.FromAsync(() => Task.Run(() => Refresh())))
                .ToPropertyEx(this, x => x.RecognizersAvailable);

            this.WhenAnyValue(u => u.Recognizer, u => u.RecognizersAvailable)
                .Where((u) => u.Item1 != null && u.Item2 != null)
                .Select(u => u.Item1)
                .Where(x => !string.IsNullOrEmpty(x))
                .DistinctUntilChanged()
                .Select(x => RecognizersAvailable.FirstOrDefault(u => u.Key == x))
                .Select(x =>
                {
                    var plugin = x.Value;
                    var editor = plugin?.CreateConfigEditor();
                    var config = ConfigManagerFactory.Instance.Get<string>(
                        RecognizerConfigTypes.GetPluginConfigKey(Recognizer));
                    editor?.LoadConfigString(config);
                    return editor;
                })
                .ToPropertyEx(this, x => x.ConfigEditor);

            this.WhenAnyValue(x => x.ConfigEditor)
                .Subscribe(x =>
                {
                    var config = ConfigManagerFactory.Instance.Get<string>(
                        RecognizerConfigTypes.GetPluginConfigKey(Recognizer));
                    PluginConfig = config;
                });

            // 监听 PluginConfig 变化，手动保存到正确的配置键
            this.WhenAnyValue(x => x.PluginConfig)
                .Skip(1) // 跳过初始值
                .Subscribe(config =>
                {
                    if (!string.IsNullOrEmpty(Recognizer))
                    {
                        ConfigManagerFactory.Instance.Apply(
                            RecognizerConfigTypes.GetPluginConfigKey(Recognizer),
                            config
                        );
                    }
                });
        }
    }

    public class TranslatorConfigViewModel : SectionConfigViewModelBase
    {
        protected override string SectionName => TranslatorConfigTypes.SectionName;

        [Reactive]
        [ConfigJsonValue(TranslatorConfigTypes.EnableTranslator)]
        public bool EnableTranslator { get; set; }

        [Reactive]
        [ConfigJsonValue(TranslatorConfigTypes.Translator)]
        public string Translator { get; set; } = "";

        [ObservableAsProperty]
        public IReadOnlyDictionary<string, Core.Plugins.ITranslator> TranslatorsAvailable { get; }

        [Reactive]
        public Core.Plugins.IPluginConfigEditor? ConfigEditor { get; set; }

        [Reactive]
        public string PluginConfig { get; set; } = "";

        [Reactive]
        public string TestResult { get; set; } = "";

        [Reactive]
        public Avalonia.Media.IBrush TestResultColor { get; set; } = Avalonia.Media.Brushes.Transparent;

        public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
        public ReactiveCommand<Unit, Unit> TestTranslateCommand { get; }

        public IReadOnlyDictionary<string, Core.Plugins.ITranslator> Refresh()
        {
            var plugins = PluginManagerFactory.GetInstance().Translators;
            if (Translator == "" && plugins.Count >= 1)
                Translator = plugins.First().Key;
            return plugins;
        }

        private void UpdateConfigEditor()
        {
            if (ConfigEditor != null)
            {
                ConfigEditor.ValueUpdated -= OnConfigEditorValueUpdated;
            }

            if (!EnableTranslator || string.IsNullOrEmpty(Translator))
            {
                ConfigEditor = null;
                PluginConfig = "";
                return;
            }

            var plugins = PluginManagerFactory.GetInstance().Translators;
            if (plugins.TryGetValue(Translator, out var plugin))
            {
                var editor = plugin.CreateConfigEditor();
                var config = ConfigManagerFactory.Instance.Get<string>(
                    TranslatorConfigTypes.GetPluginConfigKey(Translator));
                editor.LoadConfigString(config);
                
                PluginConfig = editor.GenerateConfig();
                editor.ValueUpdated += OnConfigEditorValueUpdated;
                ConfigEditor = editor;
            }
        }

        private void OnConfigEditorValueUpdated(object? s, EventArgs e)
        {
            if (ConfigEditor == null || string.IsNullOrEmpty(Translator))
                return;

            PluginConfig = ConfigEditor.GenerateConfig();
            var key = TranslatorConfigTypes.GetPluginConfigKey(Translator);
            ConfigManagerFactory.Instance.Apply(key, PluginConfig);
        }

        public TranslatorConfigViewModel()
        {
            this.RefreshCommand = ReactiveCommand.Create(() => { });
            this.RefreshCommand.Merge(Observable.Return(Unit.Default))
                .SelectMany(u => Observable.FromAsync(() => Task.Run(() => Refresh())))
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToPropertyEx(this, x => x.TranslatorsAvailable);

            this.WhenAnyValue(x => x.Translator, x => x.EnableTranslator)
                .Subscribe(_ => UpdateConfigEditor());

            this.WhenAnyValue(x => x.PluginConfig)
                .Skip(1)
                .Subscribe(config =>
                {
                    if (!string.IsNullOrEmpty(Translator))
                    {
                        var key = TranslatorConfigTypes.GetPluginConfigKey(Translator);
                        ConfigManagerFactory.Instance.Apply(key, config);
                    }
                });

            this.RefreshCommand.Execute().Subscribe();
            this.TestTranslateCommand = ReactiveCommand.CreateFromTask(TestTranslateAsync);
        }

        private async Task TestTranslateAsync()
        {
            if (string.IsNullOrEmpty(Translator))
            {
                TestResult = "❌ 请先选择翻译器";
                TestResultColor = Avalonia.Media.Brushes.Red;
                return;
            }

            try
            {
                TestResult = "⏳ 测试中...";
                TestResultColor = Avalonia.Media.Brushes.Orange;

                string result = await JobManagerFactory.Instance.TestTranslateAsync(
                    "hello world",
                    "en",
                    "zh-CN"
                );

                if (!result.Contains("[ERROR]"))
                {
                    var parts = result.Split('|');
                    if (parts.Length == 2)
                    {
                        TestResult = $"✅ 成功 ({parts[1]}): {parts[0]}";
                    }
                    else
                    {
                        TestResult = $"✅ 成功: {result}";
                    }
                    TestResultColor = Avalonia.Media.Brushes.Green;
                }
                else
                {
                    TestResult = $"❌ {result}";
                    TestResultColor = Avalonia.Media.Brushes.Red;
                }
            }
            catch (Exception ex)
            {
                TestResult = $"❌ 错误: {ex.Message}";
                TestResultColor = Avalonia.Media.Brushes.Red;
            }
        }
    }
}
