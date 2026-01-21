using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace TMSpeech.Translator.APITranslator.APIs
{
    /// <summary>
    /// 统一的 API 翻译器配置类
    /// 包含通用配置和各 API 特定的配置字段
    /// </summary>
    public class APITranslatorConfig : INotifyPropertyChanged
    {
        // 通用配置
        private string _apiName = "Google";
        private string _targetLanguage = "en";
        private string _sourceLanguage = "en";
        private int _timeoutMs = 10000;
        private bool _contextAwareEnabled = true;
        private int _contextCount = 10;

        // REST API 配置
        private string _apiUrl = "";
        private string _apiKey = "";

        // LLM API 配置
        private string _modelName = "";
        private float _temperature = 0.7f;
        private string _prompt = "";
        private string _apiConfig = "";  // API 特定配置（JSON 格式）

        public event PropertyChangedEventHandler? PropertyChanged;

        public void OnPropertyChanged([CallerMemberName] string propName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        // 通用配置属性
        public string ApiName
        {
            get => _apiName;
            set { _apiName = value; OnPropertyChanged(); }
        }

        public string TargetLanguage
        {
            get => _targetLanguage;
            set { _targetLanguage = value; OnPropertyChanged(); }
        }

        public string SourceLanguage
        {
            get => _sourceLanguage;
            set { _sourceLanguage = value; OnPropertyChanged(); }
        }

        public int TimeoutMs
        {
            get => _timeoutMs;
            set { _timeoutMs = value; OnPropertyChanged(); }
        }

        public bool ContextAwareEnabled
        {
            get => _contextAwareEnabled;
            set { _contextAwareEnabled = value; OnPropertyChanged(); }
        }

        public int ContextCount
        {
            get => _contextCount;
            set { _contextCount = value; OnPropertyChanged(); }
        }

        // API 配置属性 - 统一用 ApiUrl
        public string ApiUrl
        {
            get => _apiUrl;
            set { _apiUrl = value; OnPropertyChanged(); }
        }

        public string ApiKey
        {
            get => _apiKey;
            set { _apiKey = value; OnPropertyChanged(); }
        }

        // LLM API 配置属性
        public string ModelName
        {
            get => _modelName;
            set { _modelName = value; OnPropertyChanged(); }
        }

        public float Temperature
        {
            get => _temperature;
            set { _temperature = value; OnPropertyChanged(); }
        }

        public string Prompt
        {
            get => _prompt;
            set { _prompt = value; OnPropertyChanged(); }
        }

        // API 特定配置（JSON 格式）
        public string ApiConfig
        {
            get => _apiConfig;
            set { _apiConfig = value; OnPropertyChanged(); }
        }

        [JsonIgnore]
        public static Dictionary<string, string> SupportedLanguages => new()
        {
            { "zh-CN", "zh-CN" },
            { "zh-TW", "zh-TW" },
            { "en-US", "en-US" },
            { "en-GB", "en-GB" },
            { "ja-JP", "ja-JP" },
            { "ko-KR", "ko-KR" },
            { "fr-FR", "fr-FR" },
            { "th-TH", "th-TH" },
        };
    }

    public class TranslateAPIConfig : INotifyPropertyChanged
    {
        [JsonIgnore]
        public static Dictionary<string, string> SupportedLanguages => new()
        {
            { "zh-CN", "zh-CN" },
            { "zh-TW", "zh-TW" },
            { "en-US", "en-US" },
            { "en-GB", "en-GB" },
            { "ja-JP", "ja-JP" },
            { "ko-KR", "ko-KR" },
            { "fr-FR", "fr-FR" },
            { "th-TH", "th-TH" },
        };

        public event PropertyChangedEventHandler? PropertyChanged;

        public void OnPropertyChanged([CallerMemberName] string propName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }

    public class BaseLLMConfig : TranslateAPIConfig
    {
        public class Message
        {
            public string role { get; set; }
            public string content { get; set; }
        }

        private string modelName = "";
        private double temperature = 1.0;

        public string ModelName
        {
            get => modelName;
            set
            {
                modelName = value;
                OnPropertyChanged("ModelName");
            }
        }
        public double Temperature
        {
            get => temperature;
            set
            {
                temperature = value;
                OnPropertyChanged("Temperature");
            }
        }
    }

    public class OllamaConfig : BaseLLMConfig
    {
        public class Response
        {
            public string model { get; set; }
            public DateTime created_at { get; set; }
            public Message message { get; set; }
            public bool done { get; set; }
            public long total_duration { get; set; }
            public int load_duration { get; set; }
            public int prompt_eval_count { get; set; }
            public long prompt_eval_duration { get; set; }
            public int eval_count { get; set; }
            public long eval_duration { get; set; }
        }

        private string apiUrl = "http://localhost:11434";

        public string ApiUrl
        {
            get => apiUrl;
            set
            {
                apiUrl = value;
                OnPropertyChanged("ApiUrl");
            }
        }
    }

    public class OpenAIConfig : BaseLLMConfig
    {
        public class Choice
        {
            public int index { get; set; }
            public Message message { get; set; }
            public string logprobs { get; set; }
            public string finish_reason { get; set; }
        }
        public class Usage
        {
            public int prompt_tokens { get; set; }
            public int completion_tokens { get; set; }
            public int total_tokens { get; set; }
            public int prompt_cache_hit_tokens { get; set; }
            public int prompt_cache_miss_tokens { get; set; }
        }
        public class Response
        {
            public string id { get; set; }
            public string @object { get; set; }
            public int created { get; set; }
            public string model { get; set; }
            public List<Choice> choices { get; set; }
            public Usage usage { get; set; }
            public string system_fingerprint { get; set; }
        }

        private string apiKey = "";
        private string apiUrl = "";

        public string ApiKey
        {
            get => apiKey;
            set
            {
                apiKey = value;
                OnPropertyChanged("ApiKey");
            }
        }
        public string ApiUrl
        {
            get => apiUrl;
            set
            {
                apiUrl = value;
                OnPropertyChanged("ApiUrl");
            }
        }
    }

    public class OpenRouterConfig : BaseLLMConfig
    {
        private string apiKey = "";
        public string ApiKey
        {
            get => apiKey;
            set
            {
                apiKey = value;
                OnPropertyChanged();
            }
        }
    }

    public class DeepLConfig : TranslateAPIConfig
    {
        [JsonIgnore]
        public new static Dictionary<string, string> SupportedLanguages => new()
        {
            { "zh-CN", "ZH-HANS" },
            { "zh-TW", "ZH-HANT" },
            { "en-US", "EN-US" },
            { "en-GB", "EN-GB" },
            { "ja-JP", "JA" },
            { "ko-KR", "KO" },
            { "fr-FR", "FR" },
            { "th-TH", "TH" },
        };

        private string apiKey = "";
        private string apiUrl = "https://api.deepl.com/v2/translate";

        public string ApiKey
        {
            get => apiKey;
            set
            {
                apiKey = value;
                OnPropertyChanged("ApiKey");
            }
        }
        public string ApiUrl
        {
            get => apiUrl;
            set
            {
                apiUrl = value;
                OnPropertyChanged("ApiUrl");
            }
        }
    }
    public class YoudaoConfig : TranslateAPIConfig
    {
        public class TranslationResult
        {
            public string errorCode { get; set; }
            public string query { get; set; }
            public List<string> translation { get; set; }
            public string l { get; set; }
            public string tSpeakUrl { get; set; }
            public string speakUrl { get; set; }
        }

        [JsonIgnore]
        public new static Dictionary<string, string> SupportedLanguages => new()
        {
            { "zh-CN", "zh-CHS" },
            { "zh-TW", "zh-CHT" },
            { "en-US", "en" },
            { "en-GB", "en" },
            { "ja-JP", "ja" },
            { "ko-KR", "ko" },
            { "fr-FR", "fr" },
            { "th-TH", "th" },
        };

        private string appKey = "";
        private string appSecret = "";
        private string apiUrl = "https://openapi.youdao.com/api";

        public string AppKey
        {
            get => appKey;
            set
            {
                appKey = value;
                OnPropertyChanged("AppKey");
            }
        }

        public string AppSecret
        {
            get => appSecret;
            set
            {
                appSecret = value;
                OnPropertyChanged("AppSecret");
            }
        }

        public string ApiUrl
        {
            get => apiUrl;
            set
            {
                apiUrl = value;
                OnPropertyChanged("ApiUrl");
            }
        }
    }

    public class MTranServerConfig : TranslateAPIConfig
    {
        [JsonIgnore]
        public new static Dictionary<string, string> SupportedLanguages => new()
        {
            { "zh-CN", "zh" },
            { "zh-TW", "zh" },
            { "en-US", "en" },
            { "en-GB", "en" },
            { "ja-JP", "ja" },
            { "ko-KR", "ko" },
            { "fr-FR", "fr" },
            { "th-TH", "th" },
        };

        private string apiKey = "";
        private string apiUrl = "http://localhost:8989/translate";
        private string sourceLanguage = "en";

        public string ApiKey
        {
            get => apiKey;
            set
            {
                apiKey = value;
                OnPropertyChanged("ApiKey");
            }
        }
        public string ApiUrl
        {
            get => apiUrl;
            set
            {
                apiUrl = value;
                OnPropertyChanged("ApiUrl");
            }
        }

        public string SourceLanguage
        {
            get => sourceLanguage;
            set
            {
                sourceLanguage = value;
                OnPropertyChanged("SourceLanguage");
            }
        }

        public class Response
        {
            public string result { get; set; }
        }
    }

    public class BaiduConfig : TranslateAPIConfig
    {
        public class TransResult
        {
            public string src { get; set; }
            public string dst { get; set; }
        }

        public class TranslationResult
        {
            public string error_code { get; set; }
            public string from { get; set; }
            public string to { get; set; }
            public List<TransResult> trans_result { get; set; }
        }

        [JsonIgnore]
        public new static Dictionary<string, string> SupportedLanguages => new()
        {
            { "zh-CN", "zh" },
            { "zh-TW", "cht" },
            { "en-US", "en" },
            { "en-GB", "en" },
            { "ja-JP", "jp" },
            { "ko-KR", "kor" },
            { "fr-FR", "fra" },
            { "th-TH", "th" },
        };

        private string appId = "";
        private string appSecret = "";
        private string apiUrl = "https://fanyi-api.baidu.com/api/trans/vip/translate";

        public string AppId
        {
            get => appId;
            set
            {
                appId = value;
                OnPropertyChanged("AppId");
            }
        }

        public string AppSecret
        {
            get => appSecret;
            set
            {
                appSecret = value;
                OnPropertyChanged("AppSecret");
            }
        }

        public string ApiUrl
        {
            get => apiUrl;
            set
            {
                apiUrl = value;
                OnPropertyChanged("ApiUrl");
            }
        }
    }

    public class LibreTranslateConfig : TranslateAPIConfig
    {
        [JsonIgnore]
        public new static Dictionary<string, string> SupportedLanguages => new()
        {
            { "zh-CN", "zh" },
            { "zh-TW", "zh" },
            { "en-US", "en" },
            { "en-GB", "en" },
            { "ja-JP", "ja" },
            { "ko-KR", "ko" },
            { "fr-FR", "fr" },
            { "th-TH", "th" },
        };

        private string apiKey = "";
        private string apiUrl = "http://localhost:5000/translate";

        public string ApiKey
        {
            get => apiKey;
            set
            {
                apiKey = value;
                OnPropertyChanged("ApiKey");
            }
        }
        public string ApiUrl
        {
            get => apiUrl;
            set
            {
                apiUrl = value;
                OnPropertyChanged("ApiUrl");
            }
        }

        public class Response
        {
            public string translatedText { get; set; }
        }
    }
}
