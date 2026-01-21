namespace TMSpeech.Translator.APITranslator.APIs
{
    // Reasoning/thinking for all LLMs is disabled or minimal to reduce response time.
    
    public class BaseLLMRequestData
    {
        public string model { get; set; }
        public List<BaseLLMConfig.Message> messages { get; set; }
        public double temperature { get; set; }
        public int max_tokens { get; set; } = 128;
        public bool stream { get; set; } = false;

        public BaseLLMRequestData(string model, List<BaseLLMConfig.Message> messages, double temperature)
        {
            this.model = model;
            this.messages = messages;
            this.temperature = temperature;
        }
    }
    
    public class IntegratedLLMRequestData : BaseLLMRequestData
    {
        // Some platforms do not return 400/422 errors; instead, they automatically ignore incorrect parameters.
        // This request data is used for these platforms to ensure that model thinking is disabled.
        
        public class Reasoning
        {
            public bool exclude { get; set; } = true;
            public bool enabled { get; set; } = false;
            public string effort { get; set; } = "low";
        }
        public class Thinking
        {
            public string type { get; set; } = "disabled";
        }
        
        public bool think { get; set; } = false;
        public bool enable_thinking { get; set; } = false;
        public string reasoning_effort { get; set; } = "low";
        public Reasoning reasoning { get; set; } = new Reasoning();
        public Thinking thinking { get; set; } = new Thinking();

        public IntegratedLLMRequestData(string model, List<BaseLLMConfig.Message> messages, double temperature)
            : base(model, messages, temperature)
        {
        }
    }
    
    public class OllamaRequestData : BaseLLMRequestData
    {
        public bool think { get; set; } = false;

        public OllamaRequestData(string model, List<BaseLLMConfig.Message> messages, double temperature)
            : base(model, messages, temperature)
        {
        }
    }
    
    public class OpenRouterRequestData : BaseLLMRequestData
    {
        public class Reasoning
        {
            public bool exclude { get; set; } = true;
            public bool enabled { get; set; } = false;
        }
        public Reasoning reasoning { get; set; } = new Reasoning();

        public OpenRouterRequestData(string model, List<BaseLLMConfig.Message> messages, double temperature)
            : base(model, messages, temperature)
        {
        }
    }
    
    public class AnthropicRequestData : BaseLLMRequestData
    {
        // Supported Platform: Anthropic, Zhipu (BigModel)
        public class Thinking
        {
            public string type { get; set; } = "disabled";
        }
        public Thinking thinking { get; set; } = new Thinking();

        public AnthropicRequestData(string model, List<BaseLLMConfig.Message> messages, double temperature)
            : base(model, messages, temperature)
        {
        }
    }
    
    public class AliyunRequestData : BaseLLMRequestData
    {
        // Supported Platform: Aliyun (Bailian), Silicon Flow
        public bool enable_thinking { get; set; } = false;

        public AliyunRequestData(string model, List<BaseLLMConfig.Message> messages, double temperature)
            : base(model, messages, temperature)
        {
        }
    }
    
    public class OpenAIRequestData : BaseLLMRequestData
    {
        // Supported Platform: OpenAI, Silicon Flow (For reasoning models)
        public class Reasoning
        {
            public string effort { get; set; } = "low";
        }
        public Reasoning reasoning { get; set; } = new Reasoning();

        public OpenAIRequestData(string model, List<BaseLLMConfig.Message> messages, double temperature)
            : base(model, messages, temperature)
        {
        }
    }
    
    public class XAIRequestData : BaseLLMRequestData
    {
        // Supported Platform: xAI (Grok)
        public string reasoning_effort { get; set; } = "low";

        public XAIRequestData(string model, List<BaseLLMConfig.Message> messages, double temperature)
            : base(model, messages, temperature)
        {
        }
    }
}
