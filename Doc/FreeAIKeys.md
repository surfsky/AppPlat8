https://github.com/alistaitsacle/free-llm-api-keys

1. GPT-5.5（第三方兼容）
BaseURL：https://aiapiv2.pekpik.com/v1
bash
运行
curl https://aiapiv2.pekpik.com/v1/chat/completions \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer sk-qyJFCHtjAKsj6KDyPWumo7ECLnrFTgRO5NaEIXviynJo7By7" \
  -d '{
    "model": "gpt-5.5",
    "messages": [{"role": "user", "content": "Hello!"}]
  }'

2. Claude Opus 4.7（官方，格式不同）
BaseURL：https://api.anthropic.com/v1/messages
bash
运行
curl https://api.anthropic.com/v1/messages \
  -H "Content-Type: application/json" \
  -H "x-api-key: sk-LKZvSBaVYbH0P6P0F6llhFn67KWPJVIaHbfpXAZvNuzofaBS" \
  -H "anthropic-version: 2023-06-01" \
  -d '{
    "model": "claude-opus-4-7",
    "max_tokens": 1024,
    "messages": [{"role": "user", "content": "Hello!"}]
  }'

3. Gemini（OpenAI 兼容）
BaseURL：https://generativelanguage.googleapis.com/v1beta/openai
bash
运行
curl https://generativelanguage.googleapis.com/v1beta/openai/chat/completions \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer PASTE_KEY_HERE" \
  -d '{
    "model": "gemini-3.5-flash",
    "messages": [{"role": "user", "content": "Hello!"}]
  }'

4. DeepSeek（官方兼容）
BaseURL：https://api.deepseek.com/v1
bash
运行
curl https://api.deepseek.com/v1/chat/completions \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer PASTE_KEY_HERE" \
  -d '{
    "model": "deepseek-v3",
    "messages": [{"role": "user", "content": "Hello!"}]
  }'

5. Kimi（阿里云百炼兼容）
BaseURL：https://dashscope.aliyuncs.com/compatible-mode/v1
bash
运行
curl https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer PASTE_KEY_HERE" \
  -d '{
    "model": "kimi-k2-thinking",
    "messages": [{"role": "user", "content": "Hello!"}]
  }'
