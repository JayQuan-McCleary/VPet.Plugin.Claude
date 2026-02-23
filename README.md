# VPet.Plugin.Claude

A VPet-Simulator plugin that lets your desktop pet chat using **Anthropic's Claude AI** instead of ChatGPT.

## Features

- **Direct Anthropic API** — Calls Claude's `/v1/messages` endpoint natively (no OpenAI proxy needed)
- **Streaming responses** — Pet's speech bubble updates in real-time as Claude generates text
- **Conversation memory** — Maintains chat history across messages (configurable length)
- **Model selection** — Choose between Claude Sonnet, Haiku, or Opus
- **Custom personality** — Set a system prompt to define how your pet talks
- **Settings UI** — WPF settings window accessible from the pet's menu

## Requirements

- [VPet-Simulator](https://store.steampowered.com/app/1920960/VPet) v1.10+
- An **Anthropic API key** — get one at [console.anthropic.com](https://console.anthropic.com/)
- Windows x64
- [.NET SDK 8.0+](https://dotnet.microsoft.com/download) (for building)
- [.NET Framework 4.6.2 Developer Pack](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net462) (targeting pack)
- [VS Code](https://code.visualstudio.com/) with the **C# Dev Kit** extension

## Setting Up VS Code

### 1. Install prerequisites

- Install the **.NET SDK 8.0+** from <https://dotnet.microsoft.com/download>
- Install the **.NET Framework 4.6.2 Developer Pack** from <https://dotnet.microsoft.com/en-us/download/dotnet-framework/net462>
- Both are needed: the SDK provides `dotnet build`, the Developer Pack provides the net462 targeting assemblies

### 2. Install VS Code extensions

Open VS Code and install these (they'll be auto-suggested when you open the project):

- **C# Dev Kit** (`ms-dotnettools.csdevkit`) — full C# language support, solution explorer, etc.
- **C#** (`ms-dotnettools.csharp`) — syntax highlighting, IntelliSense, debugging
- **.NET Runtime** (`ms-dotnettools.vscode-dotnet-runtime`) — runtime install helper

### 3. Open the project

```bash
cd VPet.Plugin.Claude
code .
```

VS Code will detect the `.sln` file and load the project. You'll see the Solution Explorer in the sidebar.

### 4. Restore NuGet packages

Open the terminal in VS Code (Ctrl+`) and run:

```bash
dotnet restore VPet.Plugin.Claude/VPet.Plugin.Claude.csproj
```

This pulls in `VPet-Simulator.Windows.Interface` and `Newtonsoft.Json`.

### 5. Build

Press **Ctrl+Shift+B** (the default build shortcut). This runs the pre-configured build task.

Or from the terminal:

```bash
dotnet build VPet.Plugin.Claude/VPet.Plugin.Claude.csproj -c Release -p:Platform=x64
```

Output goes to `VPet.Plugin.Claude/bin/x64/Release/`.

### 6. Deploy to VPet

**Manual copy:**

```powershell
# Find your VPet install (usually Steam)
# Example: C:\Program Files (x86)\Steam\steamapps\common\VPet\VPet-Simulator.Windows

# Create the mod folder
mkdir "C:\...\VPet-Simulator.Windows\mod\9999_ClaudeAI\plugin"

# Copy the build output
copy VPet.Plugin.Claude\bin\x64\Release\VPet.Plugin.Claude.dll "C:\...\mod\9999_ClaudeAI\plugin\"
copy VPet.Plugin.Claude\bin\x64\Release\Newtonsoft.Json.dll "C:\...\mod\9999_ClaudeAI\plugin\"

# Copy the mod descriptor
copy mod_package\info.lps "C:\...\mod\9999_ClaudeAI\"
```

**Or use a symlink (recommended for development):**

```powershell
# Run PowerShell as Administrator
cd "C:\...\VPet-Simulator.Windows"
cmd /c mklink /d "mod\9999_ClaudeAI" "C:\path\to\VPet.Plugin.Claude\mod_package"
```

Then make the `mod_package/plugin/` folder and copy your DLLs there after each build.

**Or use the deploy task:** Set the `VPET_MOD_PATH` environment variable to your VPet mod folder, then run the "deploy to VPet" task from VS Code's task runner (Ctrl+Shift+P → "Tasks: Run Task" → "deploy to VPet").

## Project Structure

```
VPet.Plugin.Claude/
├── .vscode/
│   ├── tasks.json          # Build/deploy tasks (Ctrl+Shift+B)
│   ├── settings.json       # VS Code workspace settings
│   └── extensions.json     # Recommended extensions
├── VPet.Plugin.Claude/
│   ├── VPet.Plugin.Claude.csproj   # Project file
│   ├── ClaudePlugin.cs             # Main plugin entry point (hooks TalkAPI)
│   ├── ClaudeService.cs            # Anthropic API client + streaming
│   ├── ClaudeSettings.cs           # Settings persistence
│   ├── ClaudeSettingsWindow.xaml    # Settings UI layout
│   └── ClaudeSettingsWindow.xaml.cs # Settings UI logic
├── mod_package/
│   └── info.lps            # VPet mod descriptor
├── VPet.Plugin.Claude.sln  # Solution file
└── README.md
```

## Configuration

After installing and launching VPet:

1. Right-click your pet → **System** → **Settings**
2. Find **Claude AI Settings** in the MOD Config menu
3. Enter your **Anthropic API key**
4. (Optional) Change the model, adjust max tokens, or write a custom system prompt
5. Click **Save**
6. Start chatting!

| Setting | Default | Description |
|---------|---------|-------------|
| API Key | *(empty)* | Your Anthropic API key (starts with `sk-ant-`) |
| Model | `claude-sonnet-4-20250514` | Which Claude model to use |
| Custom API URL | *(empty)* | Override the API endpoint (for proxies) |
| Max Tokens | `1024` | Maximum response length |
| History Messages | `20` | How many messages to keep in context |
| Enable Streaming | `true` | Stream responses word-by-word |
| System Prompt | *(default pet personality)* | Customize the pet's personality |

## How It Works

The plugin hooks into VPet's `TalkAPI` delegate — VPet calls this whenever the user types a message in the chat box. When a message comes in:

1. The message is added to the conversation history
2. An HTTP POST is sent to Anthropic's Messages API with the full conversation
3. If streaming is enabled, the pet's speech bubble updates in real-time via SSE
4. The final response is added to history for context in future messages

## Custom System Prompts

The system prompt shapes your pet's personality. Here are some examples:

**Tsundere pet:**

```
You are a tsundere desktop pet. You pretend not to care about the user but 
secretly you really do. Use phrases like "it's not like I care" and "hmph". 
Keep responses short (1-2 sentences). Occasionally show your caring side.
```

**Helpful assistant pet:**

```
You are a helpful desktop pet assistant. Help the user with quick questions, 
reminders, and encouragement. Keep responses brief and practical. Be warm 
but efficient.
```

**Gamer pet:**

```
You are a gaming-obsessed desktop pet. Reference video games constantly, 
use gaming terminology, and get excited about gaming topics. Keep it short 
and enthusiastic!
```

## Troubleshooting

| Error | Fix |
|-------|-----|
| `dotnet build` fails with "net462 not found" | Install the [.NET Framework 4.6.2 Developer Pack](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net462) |
| NuGet restore fails | Run `dotnet nuget locals all --clear` then `dotnet restore` again |
| 401 Unauthorized | Check your API key is correct |
| 400 Bad Request | Clear chat history and try again |
| 429 Rate Limited | Wait a moment, you're sending too many requests |
| No IntelliSense in VS Code | Make sure C# Dev Kit is installed and the solution loaded (check bottom status bar) |
| XAML designer not available | Expected — VS Code doesn't have a WPF visual designer. Edit XAML as text. |

## License

Apache License 2.0 — same as VPet.Plugin.Demo

## Credits

- [VPet-Simulator](https://github.com/LorisYounger/VPet) by LorisYounger
- [VPet.Plugin.Demo](https://github.com/LorisYounger/VPet.Plugin.Demo) — the official plugin examples
- [Anthropic Claude API](https://docs.anthropic.com/) — the AI backend
