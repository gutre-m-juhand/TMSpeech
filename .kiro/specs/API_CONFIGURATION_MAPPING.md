# API 配置映射指南

本文档说明 UI 配置字段与各个翻译 API 所需参数的映射关系。

## UI 配置字段

| 字段名 | 类型 | 说明 |
|--------|------|------|
| ApiName | 下拉菜单 | 选择的翻译 API |
| TargetLanguage | 下拉菜单 | 目标语言 |
| TimeoutMs | 文本框 | 请求超时时间（毫秒） |
| ContextAwareEnabled | 复选框 | 是否启用上下文感知翻译 |
| ContextCount | 文本框 | 上下文条数 |
| ApiUrl | 文本框 | API 端点地址 |
| ApiKey | 密码框 | API 密钥 |
| ModelName | 文本框 | LLM 模型名称 |
| Temperature | 滑块 | 温度参数（0-1） |
| Prompt | 文本区域 | 系统提示词 |
| ApiConfig | 文本区域 | API 特定配置（JSON 格式） ✅ |

## API 配置映射表

### 1. Google（免配置）
**UI 配置需求：** 无
**说明：** 使用 Google 翻译免费 API，无需任何配置

### 2. Google2（免配置）
**UI 配置需求：** 无
**说明：** 使用 Google 字典扩展 API，无需任何配置

### 3. MTranServer（本地轻量级翻译）
**UI 配置需求：**
- `ApiUrl` (必需) - 本地 API 地址，如 `http://localhost:8989/translate`

**说明：** 本地部署的轻量级翻译服务，占用内存少

### 4. LibreTranslate（开源翻译）
**UI 配置需求：**
- `ApiUrl` (可选) - 默认 `http://localhost:5000/translate`
- `ApiKey` (可选) - 如果服务需要认证

**说明：** 开源翻译服务，可本地部署或使用公共服务

### 5. DeepL（专业翻译）
**UI 配置需求：**
- `ApiUrl` (可选) - 默认 `https://api.deepl.com/v2/translate`
- `ApiKey` (必需) - DeepL API 密钥

**获取方式：** https://www.deepl.com/pro

**说明：** 专业翻译服务，支持多种语言

### 6. Baidu（百度翻译）
**UI 配置需求：**
- `ApiConfig` (必需) - JSON 格式：
```json
{
  "appId": "your_baidu_app_id",
  "appSecret": "your_baidu_app_secret"
}
```

**获取方式：** https://fanyi-api.baidu.com/

**说明：** 百度翻译 API，需要 App ID 和密钥

### 7. Youdao（有道翻译）
**UI 配置需求：**
- `ApiConfig` (必需) - JSON 格式：
```json
{
  "appKey": "your_youdao_app_key",
  "appSecret": "your_youdao_app_secret"
}
```

**获取方式：** https://ai.youdao.com/

**说明：** 有道翻译 API，需要 App Key 和密钥

### 8. OpenAI（GPT 模型）
**UI 配置需求：**
- `ApiUrl` (可选) - 默认 `https://api.openai.com/v1/chat/completions`
- `ApiKey` (必需) - OpenAI API 密钥
- `ModelName` (可选) - 默认 `gpt-3.5-turbo`
- `Temperature` (可选) - 默认 0.7
- `Prompt` (可选) - 自定义系统提示词

**获取方式：** https://platform.openai.com/

**说明：** 使用 OpenAI GPT 模型进行翻译，支持上下文感知

### 9. Ollama（本地 LLM）
**UI 配置需求：**
- `ApiUrl` (可选) - 默认 `http://localhost:11434/api/chat`
- `ModelName` (必需) - 模型名称，如 `llama2`
- `Temperature` (可选) - 默认 0.7
- `Prompt` (可选) - 自定义系统提示词

**获取方式：** https://ollama.ai/

**说明：** 本地 LLM 服务，支持多种开源模型，支持上下文感知

### 10. OpenRouter（LLM 路由）
**UI 配置需求：**
- `ApiUrl` (可选) - 默认 `https://openrouter.ai/api/v1/chat/completions`
- `ApiKey` (必需) - OpenRouter API 密钥
- `ModelName` (可选) - 默认 `openai/gpt-3.5-turbo`
- `Temperature` (可选) - 默认 0.7
- `Prompt` (可选) - 自定义系统提示词

**获取方式：** https://openrouter.ai/

**说明：** LLM 路由服务，支持多个模型提供商，支持上下文感知

## 配置流程

### UI 字段显示规则

不同的 API 会显示不同的配置字段：

**通用字段（所有 API 都显示）：**
- ApiName、TargetLanguage、TimeoutMs、ContextAwareEnabled、ContextCount

**免配置 API（Google、Google2）：**
- 只显示通用字段

**REST API（DeepL、LibreTranslate、Youdao、Baidu、MTranServer）：**
- 显示通用字段 + ApiUrl + ApiKey
- 对于需要特定参数的 API（Baidu、Youdao），使用 ApiConfig 字段输入 JSON

**LLM API（OpenAI、Ollama、OpenRouter）：**
- 显示通用字段 + ApiUrl + ApiKey + ModelName + Temperature + Prompt
- 支持上下文感知翻译

### 数据流向
```
UI 界面
  ↓
ConfigViewModel (监听 PluginConfig 变化)
  ↓
ConfigManager (保存配置)
  ↓
APITranslator (读取配置)
  ↓
TranslateLoop (传递配置)
  ↓
TranslateAPI 函数 (使用配置进行翻译)
```

### 关键实现细节

1. **配置序列化**
   - `APITranslatorConfigEditor.GenerateConfig()` 将所有字段序列化为 JSON
   - 包括 `ApiConfig` 字段

2. **配置反序列化**
   - `APITranslatorConfigEditor.LoadConfigString()` 从 JSON 反序列化
   - 恢复所有字段，包括 `ApiConfig`

3. **API 特定参数解析**
   - 各 API 函数从 `config.ApiConfig` 中解析 JSON
   - 例如：Baidu 解析 `appId` 和 `appSecret`

## 错误处理

### 常见错误信息

| 错误 | 原因 | 解决方案 |
|------|------|--------|
| `ApiUrl 未配置` | REST API 的 ApiUrl 为空 | 在设置中输入正确的 API 地址 |
| `API Key 未配置` | API 密钥为空 | 在 API 密钥字段中输入您的密钥 |
| `AppId 或 AppSecret 未配置` | ApiConfig 中缺少必需字段 | 检查 JSON 格式和字段名称 |
| `模型名称未配置` | LLM 模型名称为空 | 在模型名称字段中输入模型名称 |
| `HTTP 错误` | API 返回错误状态码 | 检查 API 密钥和请求参数 |
| `请求超时` | 请求超过 30 秒 | 检查网络连接或使用更快的 API |

## 测试配置

使用"测试"按钮验证配置：

1. 选择 API
2. 输入必需的配置参数
3. 点击"测试"按钮
4. 查看返回的翻译结果或错误信息

## 支持的语言

所有 API 都支持以下语言对：

| 代码 | 语言 |
|------|------|
| zh-CN | 中文（简体） |
| zh-TW | 中文（繁体） |
| en-US | 英文（美国） |
| en-GB | 英文（英国） |
| ja-JP | 日文 |
| ko-KR | 韩文 |
| fr-FR | 法文 |
| th-TH | 泰文 |

## 上下文感知翻译

启用 `ContextAwareEnabled` 后，系统会：

1. 保存最近的翻译历史
2. 在翻译新文本时，将历史作为上下文发送给 API
3. 帮助 API 更好地理解语境，提高翻译质量

**支持的 API：** OpenAI、Ollama、OpenRouter（LLM 类 API）

## 注意事项

- **安全性**: 不要在代码中硬编码 API 密钥，始终通过配置输入
- **验证**: 在使用前验证所有必需的配置字段
- **错误处理**: 提供清晰的错误信息帮助用户诊断问题
- **超时**: 设置合理的超时时间，避免长时间等待
- **成本**: 某些 API（如 OpenAI）可能产生费用，请注意使用成本
