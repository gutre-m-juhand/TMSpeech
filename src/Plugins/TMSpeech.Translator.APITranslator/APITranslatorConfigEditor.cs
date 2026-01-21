using System;
using System.Collections.Generic;
using System.Text.Json;
using TMSpeech.Core.Plugins;
using TMSpeech.Translator.APITranslator.APIs;

namespace TMSpeech.Translator.APITranslator;

/// <summary>
/// API 翻译器配置编辑器
/// </summary>
public class APITranslatorConfigEditor : IPluginConfigEditor
{
    private readonly Dictionary<string, object> _values = new();
    private readonly List<PluginConfigFormItem> _formItems = new();

    public event EventHandler<EventArgs>? FormItemsUpdated;
    public event EventHandler<EventArgs>? ValueUpdated;

    // API 分类常量
    private static readonly List<string> NO_CONFIG_APIS = new() { "Google", "Google2" };
    private static readonly List<string> REST_APIS = new() { "DeepL", "LibreTranslate", "Youdao", "Baidu", "MTranServer" };
    private static readonly List<string> LLM_APIS = new() { "OpenAI", "Ollama", "OpenRouter" };

    public APITranslatorConfigEditor()
    {
        // 初始化配置值
        _values["ApiName"] = "Google";
        _values["TargetLanguage"] = "en";
        _values["TimeoutMs"] = 10000;
        _values["ContextAwareEnabled"] = true;
        _values["ContextCount"] = 10;
        _values["ApiUrl"] = "";
        _values["ApiKey"] = "";
        _values["ModelName"] = "";
        _values["Temperature"] = 0.7f;
        _values["Prompt"] = "";
        _values["ApiConfig"] = "";  // API 特定配置

        // 创建表单项
        InitializeFormItems();
    }

    private void InitializeFormItems()
    {
        // 通用字段
        _formItems.Add(new PluginConfigFormItemOption
        (
            Key: "ApiName",
            Name: "API 选择",
            Options: new Dictionary<object, string>
            {
                { "Google", "Google (免配置)" },
                { "Google2", "Google2 (免配置)" },
                { "DeepL", "DeepL" },
                { "LibreTranslate", "LibreTranslate" },
                { "Youdao", "有道翻译" },
                { "Baidu", "百度翻译" },
                { "MTranServer", "MTranServer" },
                { "OpenAI", "OpenAI" },
                { "Ollama", "Ollama" },
                { "OpenRouter", "OpenRouter" }
            }
        ));

        _formItems.Add(new PluginConfigFormItemOption
        (
            Key: "TargetLanguage",
            Name: "目标语言",
            Options: new Dictionary<object, string>
            {
                { "zh-CN", "中文 (简体)" },
                { "zh-TW", "中文 (繁体)" },
                { "en-US", "英文 (美国)" },
                { "en-GB", "英文 (英国)" },
                { "ja-JP", "日文" },
                { "ko-KR", "韩文" },
                { "fr-FR", "法文" },
                { "th-TH", "泰文" }
            }
        ));

        _formItems.Add(new PluginConfigFormItemText
        (
            Key: "TimeoutMs",
            Name: "超时时间（毫秒）"
        ));

        _formItems.Add(new PluginConfigFormItemCheckbox
        (
            Key: "ContextAwareEnabled",
            Name: "启用上下文感知翻译"
        ));

        _formItems.Add(new PluginConfigFormItemText
        (
            Key: "ContextCount",
            Name: "上下文条数"
        ));

        // REST API 字段
        _formItems.Add(new PluginConfigFormItemText
        (
            Key: "ApiUrl",
            Name: "API 端点"
        ));

        _formItems.Add(new PluginConfigFormItemPassword
        (
            Key: "ApiKey",
            Name: "API 密钥"
        ));

        // LLM API 字段
        _formItems.Add(new PluginConfigFormItemText
        (
            Key: "ModelName",
            Name: "模型名称"
        ));

        _formItems.Add(new PluginConfigFormItemSlider
        (
            Key: "Temperature",
            Name: "温度参数",
            Min: 0,
            Max: 1,
            Step: 0.1f
        ));

        _formItems.Add(new PluginConfigFormItemTextArea
        (
            Key: "Prompt",
            Name: "系统提示词"
        ));

        _formItems.Add(new PluginConfigFormItemTextArea
        (
            Key: "ApiConfig",
            Name: "API 特定配置（JSON 格式）"
        ));
    }

    public IReadOnlyList<PluginConfigFormItem> GetFormItems()
    {
        return _formItems.AsReadOnly();
    }

    public IReadOnlyDictionary<string, object> GetAll()
    {
        return _values;
    }

    public void SetValue(string key, object value)
    {
        System.Diagnostics.Debug.WriteLine($"[APITranslatorConfigEditor.SetValue] Setting {key} = {value}");
        _values[key] = value;
        System.Diagnostics.Debug.WriteLine($"[APITranslatorConfigEditor.SetValue] _values[{key}] is now: {_values[key]}");
        ValueUpdated?.Invoke(this, EventArgs.Empty);
    }

    public object GetValue(string key)
    {
        return _values.TryGetValue(key, out var value) ? value : "";
    }

    public string GenerateConfig()
    {
        System.Diagnostics.Debug.WriteLine($"[APITranslatorConfigEditor.GenerateConfig] _values['TargetLanguage'] = {(_values.TryGetValue("TargetLanguage", out var tgt) ? tgt : "NOT FOUND")}");
        
        var config = new APITranslatorConfig
        {
            ApiName = _values.TryGetValue("ApiName", out var apiName) ? apiName?.ToString() ?? "Google" : "Google",
            TargetLanguage = _values.TryGetValue("TargetLanguage", out var tgt2) ? tgt2?.ToString() ?? "en" : "en",
            SourceLanguage = "auto",  // 源语言始终为自动检测
            TimeoutMs = int.TryParse(_values.TryGetValue("TimeoutMs", out var timeout) ? timeout?.ToString() : "10000", out var ms) ? ms : 10000,
            ContextAwareEnabled = _values.TryGetValue("ContextAwareEnabled", out var contextEnabled) && (bool)contextEnabled,
            ContextCount = int.TryParse(_values.TryGetValue("ContextCount", out var contextCount) ? contextCount?.ToString() : "10", out var cc) ? cc : 10,
            ApiUrl = _values.TryGetValue("ApiUrl", out var url) ? url?.ToString() ?? "" : "",
            ApiKey = _values.TryGetValue("ApiKey", out var key) ? key?.ToString() ?? "" : "",
            ModelName = _values.TryGetValue("ModelName", out var model) ? model?.ToString() ?? "" : "",
            Temperature = float.TryParse(_values.TryGetValue("Temperature", out var temp) ? temp?.ToString() : "0.7", out var t) ? t : 0.7f,
            Prompt = _values.TryGetValue("Prompt", out var prompt) ? prompt?.ToString() ?? "" : "",
            ApiConfig = _values.TryGetValue("ApiConfig", out var apiConfig) ? apiConfig?.ToString() ?? "" : ""
        };

        System.Diagnostics.Debug.WriteLine($"[APITranslatorConfigEditor.GenerateConfig] Generated TargetLanguage: {config.TargetLanguage}");
        return JsonSerializer.Serialize(config);
    }

    public void LoadConfigString(string config)
    {
        if (string.IsNullOrEmpty(config)) return;

        try
        {
            var cfg = JsonSerializer.Deserialize<APITranslatorConfig>(config);
            if (cfg != null)
            {
                _values["ApiName"] = cfg.ApiName;
                _values["TargetLanguage"] = cfg.TargetLanguage;
                _values["TimeoutMs"] = cfg.TimeoutMs;
                _values["ContextAwareEnabled"] = cfg.ContextAwareEnabled;
                _values["ContextCount"] = cfg.ContextCount;
                _values["ApiUrl"] = cfg.ApiUrl;
                _values["ApiKey"] = cfg.ApiKey;
                _values["ModelName"] = cfg.ModelName;
                _values["Temperature"] = cfg.Temperature;
                _values["Prompt"] = cfg.Prompt;
                _values["ApiConfig"] = cfg.ApiConfig;
            }
        }
        catch
        {
            // 加载失败，使用默认值
        }
    }

    private bool IsRestApi(string apiName) => REST_APIS.Contains(apiName);
    private bool IsLlmApi(string apiName) => LLM_APIS.Contains(apiName);
}
