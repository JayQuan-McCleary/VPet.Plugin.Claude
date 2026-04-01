# VPet.Plugin.Claude

A VPet-Simulator plugin that lets your desktop pet chat using real AI — supports **Claude, ChatGPT, Gemini, Groq, and local LLMs**.

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3672196855) | [Buy me a coffee](https://ko-fi.com/theapplesalesman)

## Supported Providers

| Provider | Models | Cost |
|----------|--------|------|
| **Anthropic (Claude)** | Haiku, Sonnet, Opus | ~$5 for ~25,000 messages (Haiku) |
| **OpenAI Compatible** | GPT-4o-mini, plus Groq, Together AI, OpenRouter | Varies; Groq has free tier |
| **Google AI (Gemini)** | Gemini 2.5 Flash, Gemini 2.5 Pro | Free tier available |
| **Local LLMs** | Ollama, LM Studio, any OpenAI-compatible endpoint | Free (your hardware) |

## Features

- **Multi-provider support** — Switch between AI providers from the settings menu
- **Streaming responses** — Pet's speech bubble updates in real-time as the AI generates text
- **Conversation memory** — Maintains chat history across messages (configurable length)
- **Custom personality** — Set a system prompt to define how your pet talks
- **Localization** — English, 简体中文, 繁體中文, 日本語, 한국어
- **Settings UI** — WPF settings window accessible from the pet's menu
- **Legacy migration** — Existing Claude-only settings are automatically migrated

## Requirements

- [VPet-Simulator](https://store.steampowered.com/app/1920960/VPet) v1.10+
- An API key from your chosen provider (or a local LLM running)
- Windows x64

### For building from source

- [.NET SDK 8.0+](https://dotnet.microsoft.com/download)
- [VS Code](https://code.visualstudio.com/) with the **C# Dev Kit** extension

## Quick Start

1. Subscribe on the [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3672196855)
2. Launch VPet, enable the mod in Settings → MOD, restart
3. Open Settings → MOD Config → **AI Chat Settings**
4. Select your AI provider, paste your API key, choose a model
5. For local LLMs: set Custom API URL (e.g. `http://localhost:11434/v1/chat/completions` for Ollama)
6. Click Save
7. Settings → Usage Patterns → Custom Chat Interface → Select Claude/ChatGPT/Gemini

## Where to get API keys

| Provider | URL |
|----------|-----|
| Anthropic | [console.anthropic.com](https://console.anthropic.com/) |
| OpenAI | [platform.openai.com](https://platform.openai.com/) |
| Google AI | [aistudio.google.com](https://aistudio.google.com/) |
| Groq (free) | [console.groq.com](https://console.groq.com/) |
| Local LLM | No key needed |

## Configuration

| Setting | Default | Description |
|---------|---------|-------------|
| AI Provider | Anthropic | Which provider to use |
| API Key | *(empty)* | Your provider's API key |
| Model | *(provider default)* | Which model to use |
| Custom API URL | *(empty)* | Override the API endpoint (for local LLMs or proxies) |
| Max Tokens | `1024` | Maximum response length |
| History Messages | `20` | How many messages to keep in context |
| Enable Streaming | `true` | Stream responses word-by-word |
| System Prompt | *(default pet personality)* | Customize the pet's personality |

## Project Structure

```
VPet.Plugin.Claude/
├── VPet.Plugin.Claude/
│   ├── VPet.Plugin.Claude.csproj   # Project file
│   ├── ClaudePlugin.cs             # Main plugin entry point
│   ├── ClaudeTalkBox.cs            # TalkBox implementation
│   ├── ILLMProvider.cs             # Provider interface
│   ├── LLMProvider.cs              # Provider enum
│   ├── LLMService.cs               # API client + streaming
│   ├── LLMSettings.cs              # Settings persistence
│   ├── ClaudeSettingsWindow.xaml    # Settings UI layout
│   ├── ClaudeSettingsWindow.xaml.cs # Settings UI logic
│   └── Providers/
│       ├── AnthropicProvider.cs     # Anthropic Claude adapter
│       ├── OpenAICompatibleProvider.cs  # OpenAI/Groq/etc adapter
│       └── GoogleAIProvider.cs      # Google AI Gemini adapter
├── mod_package/
│   ├── info.lps                    # VPet mod descriptor
│   └── lang/                      # Translations (zh-Hans, zh-Hant, ja, ko)
├── VPet.Plugin.Claude.sln
└── README.md
```

## Building

```bash
dotnet restore VPet.Plugin.Claude/VPet.Plugin.Claude.csproj
dotnet build VPet.Plugin.Claude/VPet.Plugin.Claude.csproj -c Release -p:Platform=x64
```

Output goes to `VPet.Plugin.Claude/bin/x64/Release/`.

## Custom System Prompts

The system prompt shapes your pet's personality. Examples:

**Tsundere:**
```
You are a tsundere desktop pet. You pretend not to care about the user but
secretly you really do. Use phrases like "it's not like I care" and "hmph".
Keep responses short (1-2 sentences).
```

**Helpful assistant:**
```
You are a helpful desktop pet assistant. Help the user with quick questions,
reminders, and encouragement. Keep responses brief and practical.
```

**Gamer:**
```
You are a gaming-obsessed desktop pet. Reference video games constantly,
use gaming terminology, and get excited about gaming topics.
```

## Troubleshooting

| Error | Fix |
|-------|-----|
| 401 Unauthorized | Check your API key is correct |
| 400 Bad Request | Clear chat history and try again |
| 429 Rate Limited | Wait a moment, or switch to a provider with a free tier |
| Gemini 404 | Use `gemini-2.5-flash` instead of `gemini-2.0-flash` |
| Pet stuck thinking | Check API key and credit balance |

## License

Apache License 2.0

## Credits

- [VPet-Simulator](https://github.com/LorisYounger/VPet) by LorisYounger
- [Anthropic Claude API](https://docs.anthropic.com/)
- [OpenAI API](https://platform.openai.com/docs)
- [Google AI Gemini API](https://ai.google.dev/)
