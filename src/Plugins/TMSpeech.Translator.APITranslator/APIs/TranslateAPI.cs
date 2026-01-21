using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using TMSpeech.Translator.APITranslator.Utils;

namespace TMSpeech.Translator.APITranslator.APIs
{
    public class MTranServerResponse
    {
        public string result { get; set; }
    }
    public static class TranslateAPI
    {
        /*
         * The key of this field is used as the content for `translateAPIBox` in the `SettingPage`.
         * If you'd like to add a new API, please insert the key-value pair here.
         */
        public static readonly Dictionary<string, Func<string, string, CancellationToken, APITranslatorConfig, Task<string>>>
            TRANSLATE_FUNCTIONS = new()
        {
            { "Google", Google },
            { "Google2", Google2 },
            { "Ollama", Ollama },
            { "OpenAI", OpenAI },
            { "DeepL", DeepL },
            { "OpenRouter", OpenRouter },
            { "Youdao", Youdao },
            { "MTranServer", MTranServer },
            { "Baidu", Baidu },
            { "LibreTranslate", LibreTranslate },
        };
        public static readonly List<string> LLM_BASED_APIS = new()
        {
            "Ollama", "OpenAI", "OpenRouter"
        };
        public static readonly List<string> NO_CONFIG_APIS = new()
        {
            "Google", "Google2"
        };

        private static readonly HttpClient client = new HttpClient()
        {
            Timeout = TimeSpan.FromSeconds(8)
        };
        private static int openai_fallback_index = 0;

        public static async Task<string> OpenAI(string text, string targetLanguage = "en", CancellationToken token = default, APITranslatorConfig config = null)
        {
            string apiUrl = "https://api.openai.com/v1/chat/completions";
            string apiKey = "";
            string modelName = "gpt-3.5-turbo";
            float temperature = 0.7f;
            string prompt = "You are a professional translator. Translate the following text to {language}. Only return the translated text without any explanation.";

            if (config != null)
            {
                if (!string.IsNullOrEmpty(config.ApiUrl))
                    apiUrl = config.ApiUrl;
                apiKey = config.ApiKey ?? apiKey;
                if (!string.IsNullOrEmpty(config.ModelName))
                    modelName = config.ModelName;
                if (config.Temperature > 0)
                    temperature = config.Temperature;
                if (!string.IsNullOrEmpty(config.Prompt))
                    prompt = config.Prompt;
            }

            // 验证必需的配置
            if (string.IsNullOrEmpty(apiKey))
            {
                return "[ERROR] OpenAI API Key 未配置 - 请在 API 密钥字段中输入您的 OpenAI API 密钥";
            }

            try
            {
                // 获取语言名称
                var languageNames = new Dictionary<string, string>
                {
                    { "zh-CN", "Simplified Chinese" },
                    { "zh-TW", "Traditional Chinese" },
                    { "en-US", "English" },
                    { "en-GB", "English" },
                    { "ja-JP", "Japanese" },
                    { "ko-KR", "Korean" },
                    { "fr-FR", "French" },
                    { "th-TH", "Thai" }
                };

                string languageName = languageNames.TryGetValue(targetLanguage, out var name) ? name : targetLanguage;
                string finalPrompt = prompt.Replace("{language}", languageName);

                // 构建消息列表，支持上下文
                var messages = new List<object>
                {
                    new { role = "system", content = finalPrompt },
                    new { role = "user", content = $"🔤 {text} 🔤" }
                };

                var requestData = new
                {
                    model = modelName,
                    messages = messages,
                    temperature = temperature
                };

                string jsonContent = JsonSerializer.Serialize(requestData);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                using (var httpClient = new HttpClient() { Timeout = TimeSpan.FromSeconds(30) })
                {
                    httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                    HttpResponseMessage response = await httpClient.PostAsync(apiUrl, content, token);

                    if (response.IsSuccessStatusCode)
                    {
                        string responseString = await response.Content.ReadAsStringAsync();
                        using var jsonDoc = JsonDocument.Parse(responseString);
                        var root = jsonDoc.RootElement;

                        if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                        {
                            var firstChoice = choices[0];
                            if (firstChoice.TryGetProperty("message", out var message) &&
                                message.TryGetProperty("content", out var translatedText))
                            {
                                return translatedText.GetString()?.Trim() ?? "[ERROR] 无翻译结果";
                            }
                        }
                        return "[ERROR] 无翻译结果";
                    }
                    else
                    {
                        string errorBody = await response.Content.ReadAsStringAsync();
                        return $"[ERROR] OpenAI HTTP {response.StatusCode}: {errorBody}";
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return "[ERROR] 请求超时（30秒）";
            }
            catch (HttpRequestException ex)
            {
                return $"[ERROR] 连接失败: {ex.Message}";
            }
            catch (Exception ex)
            {
                return $"[ERROR] {ex.GetType().Name}: {ex.Message}";
            }
        }

        public static async Task<string> Ollama(string text, string targetLanguage = "en", CancellationToken token = default, APITranslatorConfig config = null)
        {
            string apiUrl = "http://localhost:11434/api/chat";
            string modelName = "llama2";
            float temperature = 0.7f;
            string prompt = "You are a professional translator. Translate the following text to {language}. Only return the translated text without any explanation.";

            if (config != null)
            {
                if (!string.IsNullOrEmpty(config.ApiUrl))
                    apiUrl = config.ApiUrl;
                if (!string.IsNullOrEmpty(config.ModelName))
                    modelName = config.ModelName;
                if (config.Temperature > 0)
                    temperature = config.Temperature;
                if (!string.IsNullOrEmpty(config.Prompt))
                    prompt = config.Prompt;
            }

            // 验证必需的配置
            if (string.IsNullOrEmpty(modelName))
            {
                return "[ERROR] Ollama 模型名称未配置 - 请在模型名称字段中输入模型名称（如 llama2）";
            }

            try
            {
                // 获取语言名称
                var languageNames = new Dictionary<string, string>
                {
                    { "zh-CN", "Simplified Chinese" },
                    { "zh-TW", "Traditional Chinese" },
                    { "en-US", "English" },
                    { "en-GB", "English" },
                    { "ja-JP", "Japanese" },
                    { "ko-KR", "Korean" },
                    { "fr-FR", "French" },
                    { "th-TH", "Thai" }
                };

                string languageName = languageNames.TryGetValue(targetLanguage, out var name) ? name : targetLanguage;
                string finalPrompt = prompt.Replace("{language}", languageName);

                // 构建消息列表，支持上下文
                var messages = new List<object>
                {
                    new { role = "system", content = finalPrompt },
                    new { role = "user", content = $"🔤 {text} 🔤" }
                };

                var requestData = new
                {
                    model = modelName,
                    messages = messages,
                    stream = false
                };

                string jsonContent = JsonSerializer.Serialize(requestData);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                using (var httpClient = new HttpClient() { Timeout = TimeSpan.FromSeconds(30) })
                {
                    if (!apiUrl.StartsWith("http"))
                    {
                        apiUrl = "http://" + apiUrl;
                    }

                    HttpResponseMessage response = await httpClient.PostAsync(apiUrl, content, token);

                    if (response.IsSuccessStatusCode)
                    {
                        string responseString = await response.Content.ReadAsStringAsync();
                        using var jsonDoc = JsonDocument.Parse(responseString);
                        var root = jsonDoc.RootElement;

                        if (root.TryGetProperty("message", out var message) &&
                            message.TryGetProperty("content", out var translatedText))
                        {
                            return translatedText.GetString()?.Trim() ?? "[ERROR] 无翻译结果";
                        }
                        return "[ERROR] 无翻译结果";
                    }
                    else
                    {
                        string errorBody = await response.Content.ReadAsStringAsync();
                        return $"[ERROR] Ollama HTTP {response.StatusCode}: {errorBody}";
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return "[ERROR] 请求超时（30秒）";
            }
            catch (HttpRequestException ex)
            {
                return $"[ERROR] 连接失败: {ex.Message}";
            }
            catch (Exception ex)
            {
                return $"[ERROR] {ex.GetType().Name}: {ex.Message}";
            }
        }

        public static async Task<string> OpenRouter(string text, string targetLanguage = "en", CancellationToken token = default, APITranslatorConfig config = null)
        {
            string apiUrl = "https://openrouter.ai/api/v1/chat/completions";
            string apiKey = "";
            string modelName = "openai/gpt-3.5-turbo";
            float temperature = 0.7f;
            string prompt = "You are a professional translator. Translate the following text to {language}. Only return the translated text without any explanation.";

            if (config != null)
            {
                if (!string.IsNullOrEmpty(config.ApiUrl))
                    apiUrl = config.ApiUrl;
                apiKey = config.ApiKey ?? apiKey;
                if (!string.IsNullOrEmpty(config.ModelName))
                    modelName = config.ModelName;
                if (config.Temperature > 0)
                    temperature = config.Temperature;
                if (!string.IsNullOrEmpty(config.Prompt))
                    prompt = config.Prompt;
            }

            // 验证必需的配置
            if (string.IsNullOrEmpty(apiKey))
            {
                return "[ERROR] OpenRouter API Key 未配置 - 请在 API 密钥字段中输入您的 OpenRouter API 密钥";
            }

            try
            {
                // 获取语言名称
                var languageNames = new Dictionary<string, string>
                {
                    { "zh-CN", "Simplified Chinese" },
                    { "zh-TW", "Traditional Chinese" },
                    { "en-US", "English" },
                    { "en-GB", "English" },
                    { "ja-JP", "Japanese" },
                    { "ko-KR", "Korean" },
                    { "fr-FR", "French" },
                    { "th-TH", "Thai" }
                };

                string languageName = languageNames.TryGetValue(targetLanguage, out var name) ? name : targetLanguage;
                string finalPrompt = prompt.Replace("{language}", languageName);

                // 构建消息列表，支持上下文
                var messages = new List<object>
                {
                    new { role = "system", content = finalPrompt },
                    new { role = "user", content = $"🔤 {text} 🔤" }
                };

                var requestData = new
                {
                    model = modelName,
                    messages = messages,
                    temperature = temperature
                };

                string jsonContent = JsonSerializer.Serialize(requestData);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                using (var httpClient = new HttpClient() { Timeout = TimeSpan.FromSeconds(30) })
                {
                    httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                    HttpResponseMessage response = await httpClient.PostAsync(apiUrl, content, token);

                    if (response.IsSuccessStatusCode)
                    {
                        string responseString = await response.Content.ReadAsStringAsync();
                        using var jsonDoc = JsonDocument.Parse(responseString);
                        var root = jsonDoc.RootElement;

                        if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                        {
                            var firstChoice = choices[0];
                            if (firstChoice.TryGetProperty("message", out var message) &&
                                message.TryGetProperty("content", out var translatedText))
                            {
                                return translatedText.GetString()?.Trim() ?? "[ERROR] 无翻译结果";
                            }
                        }
                        return "[ERROR] 无翻译结果";
                    }
                    else
                    {
                        string errorBody = await response.Content.ReadAsStringAsync();
                        return $"[ERROR] OpenRouter HTTP {response.StatusCode}: {errorBody}";
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return "[ERROR] 请求超时（30秒）";
            }
            catch (HttpRequestException ex)
            {
                return $"[ERROR] 连接失败: {ex.Message}";
            }
            catch (Exception ex)
            {
                return $"[ERROR] {ex.GetType().Name}: {ex.Message}";
            }
        }

        public static async Task<string> Google(string text, string targetLanguage = "en", CancellationToken token = default, APITranslatorConfig config = null)
        {
            System.Diagnostics.Debug.WriteLine($"[Google] Called with text={text}, targetLanguage={targetLanguage}");

            // 映射目标语言：zh-CN -> zh, zh-TW -> zh-TW, en-US -> en 等
            string mappedLanguage = targetLanguage;
            if (targetLanguage == "zh-CN")
                mappedLanguage = "zh-CN";
            else if (targetLanguage == "zh-TW")
                mappedLanguage = "zh-TW";
            else if (targetLanguage.Contains("-"))
                mappedLanguage = targetLanguage.Split("-")[0];  // 取前缀，如 en-US -> en

            string encodedText = Uri.EscapeDataString(text);
            var url = $"https://clients5.google.com/translate_a/t?" +
                      $"client=dict-chrome-ex&sl=auto&" +
                      $"tl={mappedLanguage}&" +
                      $"q={encodedText}";

            HttpResponseMessage response;
            try
            {
                response = await client.GetAsync(url, token);
            }
            catch (OperationCanceledException ex)
            {
                if (ex.Message.StartsWith("The request"))
                    return $"[ERROR] Translation Failed: The request was canceled due to timeout (> 8 seconds), " +
                           $"please use a faster API or check network connection.";
                throw;
            }
            catch (Exception ex)
            {
                return $"[ERROR] Translation Failed: {ex.Message}";
            }

            if (response.IsSuccessStatusCode)
            {
                string responseString = await response.Content.ReadAsStringAsync();

                var responseObj = JsonSerializer.Deserialize<List<List<string>>>(responseString);

                string translatedText = responseObj[0][0];
                return translatedText;
            }
            else
                return $"[ERROR] Translation Failed: HTTP Error - {response.StatusCode}";
        }

        public static async Task<string> Google2(string text, string targetLanguage = "en", CancellationToken token = default, APITranslatorConfig config = null)
        {
            string apiKey = "AIzaSyA6EEtrDCfBkHV8uU2lgGY-N383ZgAOo7Y";
            string strategy = "2";

            // 映射目标语言：zh-CN -> zh-CN, zh-TW -> zh-TW, en-US -> en 等
            string mappedLanguage = targetLanguage;
            if (targetLanguage.Contains("-"))
                mappedLanguage = targetLanguage.Split("-")[0];  // 取前缀，如 en-US -> en

            string encodedText = Uri.EscapeDataString(text);
            string url = $"https://dictionaryextension-pa.googleapis.com/v1/dictionaryExtensionData?" +
                         $"language={mappedLanguage}&" +
                         $"key={apiKey}&" +
                         $"term={encodedText}&" +
                         $"strategy={strategy}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("x-referer", "chrome-extension://mgijmajocgfcbeboacabfgobmjgjcoja");

            HttpResponseMessage response;
            try
            {
                response = await client.SendAsync(request, token);
            }
            catch (OperationCanceledException ex)
            {
                if (ex.Message.StartsWith("The request"))
                    return $"[ERROR] Translation Failed: The request was canceled due to timeout (> 8 seconds), " +
                           $"please use a faster API or check network connection.";
                throw;
            }
            catch (Exception ex)
            {
                return $"[ERROR] Translation Failed: {ex.Message}";
            }

            if (response.IsSuccessStatusCode)
            {
                string responseBody = await response.Content.ReadAsStringAsync();

                using var jsonDoc = JsonDocument.Parse(responseBody);
                var root = jsonDoc.RootElement;

                if (root.TryGetProperty("translateResponse", out JsonElement translateResponse))
                {
                    string translatedText = translateResponse.GetProperty("translateText").GetString();
                    return translatedText;
                }
                else
                    return "[ERROR] Translation Failed: Unexpected API response format";
            }
            else
                return $"[ERROR] Translation Failed: HTTP Error - {response.StatusCode}";
        }

        public static async Task<string> DeepL(string text, string targetLanguage = "en", CancellationToken token = default, APITranslatorConfig config = null)
        {
            string apiUrl = "https://api.deepl.com/v2/translate";
            string apiKey = "";

            if (config != null)
            {
                if (!string.IsNullOrEmpty(config.ApiUrl))
                    apiUrl = config.ApiUrl;
                apiKey = config.ApiKey ?? apiKey;
            }

            // 验证必需的配置
            if (string.IsNullOrEmpty(apiKey))
            {
                return "[ERROR] DeepL API Key 未配置 - 请在 API 密钥字段中输入您的 DeepL API 密钥";
            }

            try
            {
                // 映射目标语言
                string mappedLanguage = DeepLConfig.SupportedLanguages.TryGetValue(
                    targetLanguage, out var langValue) ? langValue : targetLanguage;

                var requestData = new
                {
                    text = new[] { text },
                    target_lang = mappedLanguage
                };

                string jsonContent = JsonSerializer.Serialize(requestData);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                using (var httpClient = new HttpClient() { Timeout = TimeSpan.FromSeconds(30) })
                {
                    httpClient.DefaultRequestHeaders.Clear();
                    httpClient.DefaultRequestHeaders.Add("Authorization", $"DeepL-Auth-Key {apiKey}");

                    HttpResponseMessage response = await httpClient.PostAsync(apiUrl, content, token);

                    if (response.IsSuccessStatusCode)
                    {
                        string responseString = await response.Content.ReadAsStringAsync();
                        using var jsonDoc = JsonDocument.Parse(responseString);
                        var root = jsonDoc.RootElement;

                        if (root.TryGetProperty("translations", out var translations) && translations.GetArrayLength() > 0)
                        {
                            var firstTranslation = translations[0];
                            if (firstTranslation.TryGetProperty("text", out var translatedText))
                            {
                                return translatedText.GetString() ?? "[ERROR] 无翻译结果";
                            }
                        }
                        return "[ERROR] 无翻译结果";
                    }
                    else
                    {
                        string errorBody = await response.Content.ReadAsStringAsync();
                        return $"[ERROR] DeepL HTTP {response.StatusCode}: {errorBody}";
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return "[ERROR] 请求超时（30秒）";
            }
            catch (HttpRequestException ex)
            {
                return $"[ERROR] 连接失败: {ex.Message}";
            }
            catch (Exception ex)
            {
                return $"[ERROR] {ex.GetType().Name}: {ex.Message}";
            }
        }

        public static async Task<string> Youdao(string text, string targetLanguage = "en", CancellationToken token = default, APITranslatorConfig config = null)
        {
            // Youdao API 需要 AppKey 和 AppSecret，从 ApiConfig JSON 中解析
            string appKey = "";
            string appSecret = "";
            string apiUrl = "https://openapi.youdao.com/api";

            if (config != null)
            {
                if (!string.IsNullOrEmpty(config.ApiUrl))
                    apiUrl = config.ApiUrl;

                // 从 ApiConfig JSON 中解析 AppKey 和 AppSecret
                if (!string.IsNullOrEmpty(config.ApiConfig))
                {
                    try
                    {
                        var apiConfigObj = JsonSerializer.Deserialize<JsonElement>(config.ApiConfig);
                        if (apiConfigObj.TryGetProperty("appKey", out var appKeyElem))
                            appKey = appKeyElem.GetString() ?? "";
                        if (apiConfigObj.TryGetProperty("appSecret", out var appSecretElem))
                            appSecret = appSecretElem.GetString() ?? "";
                    }
                    catch
                    {
                        // 解析失败，使用空值
                    }
                }
            }

            // 验证必需的配置
            if (string.IsNullOrEmpty(appKey) || string.IsNullOrEmpty(appSecret))
            {
                return "[ERROR] Youdao AppKey 或 AppSecret 未配置 - 请在 API 特定配置中提供 JSON: {\"appKey\":\"your_app_key\",\"appSecret\":\"your_app_secret\"}";
            }

            try
            {
                // 映射目标语言
                string mappedLanguage = YoudaoConfig.SupportedLanguages.TryGetValue(
                    targetLanguage, out var langValue) ? langValue : targetLanguage;

                // 生成随机数和时间戳
                string salt = DateTime.Now.Millisecond.ToString();

                // 生成签名: MD5(appKey + q + salt + appSecret)
                string signStr = appKey + text + salt + appSecret;
                string sign = ComputeMD5(signStr);

                // 构建请求
                var requestData = new Dictionary<string, string>
                {
                    { "q", text },
                    { "from", "auto" },
                    { "to", mappedLanguage },
                    { "appKey", appKey },
                    { "salt", salt },
                    { "sign", sign }
                };

                var content = new FormUrlEncodedContent(requestData);

                using (var httpClient = new HttpClient() { Timeout = TimeSpan.FromSeconds(30) })
                {
                    HttpResponseMessage response = await httpClient.PostAsync(apiUrl, content, token);

                    if (response.IsSuccessStatusCode)
                    {
                        string responseString = await response.Content.ReadAsStringAsync();
                        var responseObj = JsonSerializer.Deserialize<YoudaoConfig.TranslationResult>(responseString);

                        if (responseObj?.translation != null && responseObj.translation.Count > 0)
                        {
                            return responseObj.translation[0];
                        }
                        else if (responseObj?.errorCode != null && responseObj.errorCode != "0")
                        {
                            return $"[ERROR] Youdao API Error: {responseObj.errorCode}";
                        }
                        else
                        {
                            return "[ERROR] 无翻译结果";
                        }
                    }
                    else
                    {
                        string errorBody = await response.Content.ReadAsStringAsync();
                        return $"[ERROR] Youdao HTTP {response.StatusCode}: {errorBody}";
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return "[ERROR] 请求超时（30秒）";
            }
            catch (HttpRequestException ex)
            {
                return $"[ERROR] 连接失败: {ex.Message}";
            }
            catch (Exception ex)
            {
                return $"[ERROR] {ex.GetType().Name}: {ex.Message}";
            }
        }

        public static async Task<string> MTranServer(string text, string targetLanguage = "en", CancellationToken token = default, APITranslatorConfig config = null)
        {
            System.Diagnostics.Debug.WriteLine($"[MTranServer] ===== CALLED =====");
            System.Diagnostics.Debug.WriteLine($"[MTranServer] config is NULL: {config == null}");
            if (config != null)
            {
                System.Diagnostics.Debug.WriteLine($"[MTranServer] config.ApiUrl: {config.ApiUrl}");
                System.Diagnostics.Debug.WriteLine($"[MTranServer] config.ApiName: {config.ApiName}");
                System.Diagnostics.Debug.WriteLine($"[MTranServer] config.SourceLanguage: {config.SourceLanguage}");
            }

            // MTranServer 是一个轻量级离线翻译服务，占用内存少
            // 默认本地地址: http://localhost:8989/translate

            string apiUrl = "";
            string sourceLanguage = "en";
            string apiKey = "";

            if (config != null)
            {
                apiUrl = config.ApiUrl;
                sourceLanguage = config.SourceLanguage ?? sourceLanguage;
                apiKey = config.ApiKey ?? apiKey;
            }

            // 如果 ApiUrl 为空，直接报错
            if (string.IsNullOrEmpty(apiUrl))
            {
                return "[ERROR] ApiUrl 未配置 - 请在设置中输入 MTranServer 的 API 地址";
            }

            System.Diagnostics.Debug.WriteLine($"[MTranServer] Final URL: {apiUrl}");
            System.Diagnostics.Debug.WriteLine($"[MTranServer] Text: {text}, TargetLang: {targetLanguage}");

            try
            {
                // 映射目标语言 - 使用完整的语言代码（如 zh-Hans 而不是 zh）
                string mappedLanguage = targetLanguage;
                if (targetLanguage == "zh-CN")
                    mappedLanguage = "zh-Hans";
                else if (targetLanguage == "zh-TW")
                    mappedLanguage = "zh-Hant";

                var requestData = new
                {
                    text = text,
                    to = mappedLanguage,
                    from = sourceLanguage
                };

                string jsonContent = System.Text.Json.JsonSerializer.Serialize(requestData);
                System.Diagnostics.Debug.WriteLine($"[MTranServer] Request JSON: {jsonContent}");

                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                using (var httpClient = new HttpClient() { Timeout = TimeSpan.FromSeconds(30) })
                {
                    httpClient.DefaultRequestHeaders.Clear();

                    if (!string.IsNullOrEmpty(apiKey))
                    {
                        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
                    }

                    if (!apiUrl.StartsWith("http"))
                    {
                        apiUrl = "http://" + apiUrl;
                    }

                    System.Diagnostics.Debug.WriteLine($"[MTranServer] About to POST to: {apiUrl}");
                    HttpResponseMessage response = await httpClient.PostAsync(apiUrl, content, token);
                    System.Diagnostics.Debug.WriteLine($"[MTranServer] Got response: {response.StatusCode}");

                    if (response.IsSuccessStatusCode)
                    {
                        string responseString = await response.Content.ReadAsStringAsync();
                        System.Diagnostics.Debug.WriteLine($"[MTranServer] Success! Response: {responseString}");
                        var responseObj = System.Text.Json.JsonSerializer.Deserialize<MTranServerResponse>(responseString);
                        return responseObj?.result ?? "[ERROR] 无翻译结果";
                    }
                    else
                    {
                        string errorBody = await response.Content.ReadAsStringAsync();
                        System.Diagnostics.Debug.WriteLine($"[MTranServer] HTTP Error {response.StatusCode}: {errorBody}");
                        return $"[ERROR] MTranServer HTTP {response.StatusCode}: {errorBody}";
                    }
                }
            }
            catch (OperationCanceledException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MTranServer] TIMEOUT: {ex.Message}");
                return "[ERROR] 请求超时（30秒）";
            }
            catch (HttpRequestException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MTranServer] HTTP ERROR: {ex.Message}");
                return $"[ERROR] 连接失败: {ex.Message}";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MTranServer] EXCEPTION: {ex.GetType().Name} - {ex.Message}");
                return $"[ERROR] {ex.GetType().Name}: {ex.Message}";
            }
        }

        public static async Task<string> Baidu(string text, string targetLanguage = "en", CancellationToken token = default, APITranslatorConfig config = null)
        {
            // Baidu API 需要 AppId 和 AppSecret，从 ApiConfig JSON 中解析
            string appId = "";
            string appSecret = "";
            string apiUrl = "https://fanyi-api.baidu.com/api/trans/vip/translate";

            if (config != null)
            {
                if (!string.IsNullOrEmpty(config.ApiUrl))
                    apiUrl = config.ApiUrl;

                // 从 ApiConfig JSON 中解析 AppId 和 AppSecret
                if (!string.IsNullOrEmpty(config.ApiConfig))
                {
                    try
                    {
                        var apiConfigObj = JsonSerializer.Deserialize<JsonElement>(config.ApiConfig);
                        if (apiConfigObj.TryGetProperty("appId", out var appIdElem))
                            appId = appIdElem.GetString() ?? "";
                        if (apiConfigObj.TryGetProperty("appSecret", out var appSecretElem))
                            appSecret = appSecretElem.GetString() ?? "";
                    }
                    catch
                    {
                        // 解析失败，使用空值
                    }
                }
            }

            // 验证必需的配置
            if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(appSecret))
            {
                return "[ERROR] Baidu AppId 或 AppSecret 未配置 - 请在 API 特定配置中提供 JSON: {\"appId\":\"your_app_id\",\"appSecret\":\"your_app_secret\"}";
            }

            try
            {
                // 映射目标语言
                string mappedLanguage = BaiduConfig.SupportedLanguages.TryGetValue(
                    targetLanguage, out var langValue) ? langValue : targetLanguage;

                // 生成随机数和时间戳
                string salt = DateTime.Now.Millisecond.ToString();
                long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                // 生成签名: MD5(appId + q + salt + appSecret)
                string signStr = appId + text + salt + appSecret;
                string sign = ComputeMD5(signStr);

                // 构建请求
                var requestData = new Dictionary<string, string>
                {
                    { "q", text },
                    { "from", "auto" },
                    { "to", mappedLanguage },
                    { "appid", appId },
                    { "salt", salt },
                    { "sign", sign }
                };

                var content = new FormUrlEncodedContent(requestData);

                using (var httpClient = new HttpClient() { Timeout = TimeSpan.FromSeconds(30) })
                {
                    HttpResponseMessage response = await httpClient.PostAsync(apiUrl, content, token);

                    if (response.IsSuccessStatusCode)
                    {
                        string responseString = await response.Content.ReadAsStringAsync();
                        var responseObj = JsonSerializer.Deserialize<BaiduConfig.TranslationResult>(responseString);

                        if (responseObj?.trans_result != null && responseObj.trans_result.Count > 0)
                        {
                            return responseObj.trans_result[0].dst;
                        }
                        else if (responseObj?.error_code != null)
                        {
                            return $"[ERROR] Baidu API Error: {responseObj.error_code}";
                        }
                        else
                        {
                            return "[ERROR] 无翻译结果";
                        }
                    }
                    else
                    {
                        string errorBody = await response.Content.ReadAsStringAsync();
                        return $"[ERROR] Baidu HTTP {response.StatusCode}: {errorBody}";
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return "[ERROR] 请求超时（30秒）";
            }
            catch (HttpRequestException ex)
            {
                return $"[ERROR] 连接失败: {ex.Message}";
            }
            catch (Exception ex)
            {
                return $"[ERROR] {ex.GetType().Name}: {ex.Message}";
            }
        }

        public static async Task<string> LibreTranslate(string text, string targetLanguage = "en", CancellationToken token = default, APITranslatorConfig config = null)
        {
            string apiUrl = "http://localhost:5000/translate";
            string apiKey = "";

            if (config != null)
            {
                if (!string.IsNullOrEmpty(config.ApiUrl))
                    apiUrl = config.ApiUrl;
                apiKey = config.ApiKey ?? apiKey;
            }

            if (string.IsNullOrEmpty(apiUrl))
            {
                return "[ERROR] LibreTranslate ApiUrl 未配置";
            }

            try
            {
                // 映射目标语言
                string mappedLanguage = LibreTranslateConfig.SupportedLanguages.TryGetValue(
                    targetLanguage, out var langValue) ? langValue : targetLanguage;

                var requestData = new
                {
                    q = text,
                    source = "auto",
                    target = mappedLanguage,
                    format = "text",
                    api_key = apiKey
                };

                string jsonContent = System.Text.Json.JsonSerializer.Serialize(requestData);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                using (var httpClient = new HttpClient() { Timeout = TimeSpan.FromSeconds(30) })
                {
                    if (!apiUrl.StartsWith("http"))
                    {
                        apiUrl = "http://" + apiUrl;
                    }

                    HttpResponseMessage response = await httpClient.PostAsync(apiUrl, content, token);

                    if (response.IsSuccessStatusCode)
                    {
                        string responseString = await response.Content.ReadAsStringAsync();
                        var responseObj = System.Text.Json.JsonSerializer.Deserialize<LibreTranslateConfig.Response>(responseString);
                        return responseObj?.translatedText ?? "[ERROR] 无翻译结果";
                    }
                    else
                    {
                        string errorBody = await response.Content.ReadAsStringAsync();
                        return $"[ERROR] LibreTranslate HTTP {response.StatusCode}: {errorBody}";
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return "[ERROR] 请求超时（30秒）";
            }
            catch (HttpRequestException ex)
            {
                return $"[ERROR] 连接失败: {ex.Message}";
            }
            catch (Exception ex)
            {
                return $"[ERROR] {ex.GetType().Name}: {ex.Message}";
            }
        }

        // Helper methods for cryptographic operations
        private static string ComputeMD5(string input)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(input);
                byte[] hashBytes = md5.ComputeHash(inputBytes);
                return Convert.ToHexString(hashBytes).ToLower();
            }
        }

        private static string ComputeSHA256(string input)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(input);
                byte[] hashBytes = sha256.ComputeHash(inputBytes);
                return Convert.ToHexString(hashBytes).ToLower();
            }
        }

        public class ConfigDictConverter : JsonConverter<Dictionary<string, List<TranslateAPIConfig>>>
        {
            public override Dictionary<string, List<TranslateAPIConfig>> Read(
                ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType != JsonTokenType.StartObject)
                    throw new JsonException("Expected a StartObject token.");
                var configs = new Dictionary<string, List<TranslateAPIConfig>>();

                reader.Read();
                while (reader.TokenType == JsonTokenType.PropertyName)
                {
                    string key = reader.GetString();
                    reader.Read();

                    var configType = Type.GetType($"TMSpeech.Translator.APITranslator.APIs.{key}Config");
                    TranslateAPIConfig config;

                    if (reader.TokenType == JsonTokenType.StartArray)
                    {
                        var list = new List<TranslateAPIConfig>();
                        reader.Read();

                        while (reader.TokenType != JsonTokenType.EndArray)
                        {
                            if (configType != null && typeof(TranslateAPIConfig).IsAssignableFrom(configType))
                                config = (TranslateAPIConfig)JsonSerializer.Deserialize(ref reader, configType, options);
                            else
                                config = (TranslateAPIConfig)JsonSerializer.Deserialize(ref reader, typeof(TranslateAPIConfig), options);

                            list.Add(config);
                            reader.Read();
                        }
                        configs[key] = list;
                    }
                    else
                        throw new JsonException("Expected a StartObject token or a StartArray token.");

                    reader.Read();
                }

                if (reader.TokenType != JsonTokenType.EndObject)
                    throw new JsonException("Expected an EndObject token.");
                return configs;
            }

            public override void Write(
                Utf8JsonWriter writer, Dictionary<string, List<TranslateAPIConfig>> value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();
                foreach (var kvp in value)
                {
                    writer.WritePropertyName(kvp.Key);
                    var configType = Type.GetType($"TMSpeech.Translator.APITranslator.APIs.{kvp.Key}Config");

                    if (kvp.Value is IEnumerable<TranslateAPIConfig> configList)
                    {
                        writer.WriteStartArray();
                        foreach (var config in configList)
                        {
                            if (configType != null && typeof(TranslateAPIConfig).IsAssignableFrom(configType))
                                JsonSerializer.Serialize(writer, config, configType, options);
                            else
                                JsonSerializer.Serialize(writer, config, typeof(TranslateAPIConfig), options);
                        }
                        writer.WriteEndArray();
                    }
                    else
                        throw new JsonException($"Unsupported config type: {kvp.Value.GetType()}");
                }
                writer.WriteEndObject();
            }
        }
    }
}
