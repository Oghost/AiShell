# 🤖 AiShell - Intelligent Terminal for Windows

[![Build Status](https://github.com/Oghost/AiShell/workflows/Build%20and%20Test/badge.svg)](https://github.com/Oghost/AiShell/actions)
[![Release](https://img.shields.io/github/v/release/Oghost/AiShell)](https://github.com/Oghost/AiShell/releases)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Downloads](https://img.shields.io/github/downloads/Oghost/AiShell/total)](https://github.com/Oghost/AiShell/releases)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple)](https://dotnet.microsoft.com/)
[![Windows](https://img.shields.io/badge/Platform-Windows-blue)](https://www.microsoft.com/windows)

**AiShell** is an intelligent terminal for Windows that combines the power of local artificial intelligence with the familiarity of traditional command line. Transform natural language into precise Windows commands using Phi-3.5 models executed locally with onnxruntime.

## ✨ Key Features

- 🧠 **Local AI**: Uses Phi-3.5-mini with onnxruntime (no data sent to cloud)
- ⚡ **Hardware Acceleration**: Auto-detection of CUDA, DirectML, OpenVINO
- 🎨 **Modern Interface**: Colors, customizable themes and auto-completion with TAB
- 🔌 **Plugin System**: ffmpeg, git, curl and network tools included
- 🧠 **Adaptive Learning**: Improves with usage and remembers your patterns
- 🛡️ **Security**: Safe mode by default, trusted software only
- 🌐 **Optional Cloud LLM**: Support for GPT-4 and Claude with prefixed commands

## 🚀 Quick Start

### System Requirements
- Windows 10/11
- .NET 8.0 or higher
- 4GB RAM minimum (8GB recommended)
- NVIDIA GPU (optional, for CUDA acceleration)

### Installation

1. **Download the latest release**
   ```bash
   # Or compile from source
   git clone https://github.com/Oghost/AiShell.git
   cd AiShell
   dotnet build -c Release
   ```

2. **First run**
   ```bash
   .\AiShell.exe
   ```
   
   The system will automatically detect your hardware and download the optimized Phi-3.5 model.

## 💡 Usage Examples

### Basic Natural Commands
```bash
# File operations
> list video files in this folder
dir *.mp4 *.avi *.mkv *.mov

# Network
> ping google continuously
ping -t google.com

# System
> show processes using most memory
tasklist /fo table
```

### Video Processing with ffmpeg
```bash
# Cut video
> cut video input.mp4 from minute 1 to minute 3
ffmpeg -ss 00:01:00 -to 00:03:00 -i input.mp4 -c copy output.mp4

# Convert to GIF
> convert video.mp4 to animated gif
ffmpeg -i video.mp4 -vf "fps=10,scale=320:-1" output.gif
```

### Smart Environment Variables
```bash
# Define an IP camera
> alias CAM1 192.168.1.50

# Use the variable later
> ping CAM1
ping 192.168.1.50
```

### On-Demand Cloud LLM
```bash
# Use GPT-4 for complex commands
> gpt help me create a script to backup my documents

# Use Claude for analysis
> claude analyze my network performance
```

## 🌍 Multi-language Support

AiShell automatically detects the language of your input and responds accordingly:

```bash
# English
> list all pdf files
dir *.pdf

# Spanish
> listar todos los archivos pdf
dir *.pdf

# French
> lister tous les fichiers pdf
dir *.pdf
```

## ⚙️ Configuration

### Special Commands
| Command | Description |
|---------|-------------|
| `help` | Show complete help |
| `vars` | View environment variables |
| `alias <name> <value>` | Create alias |
| `clear` | Clear screen |
| `exit` | Exit AiShell |

## 🔧 Development

### Project Structure
```
AiShell/
├── Core/           # Main engine and session management
├── LLM/            # onnxruntime and Phi integration
├── Plugins/        # Plugin system (ffmpeg, git, etc.)
├── Storage/        # Local database and shared knowledge
├── UI/             # Console interface with Spectre.Console
├── Security/       # Validation and trusted software
└── Installers/     # Automatic dependency management
```

### Build from Source
```bash
git clone https://github.com/Oghost/AiShell.git
cd AiShell
dotnet restore
dotnet build -c Release
```

### Run Tests
```bash
dotnet test
```

## 🤝 Contributing

Contributions are welcome! Please:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add: new feature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

### Ideas for Contributing
- 🔌 New plugins (Docker, AWS CLI, etc.)
- 🌍 Translation to other languages
- 🎨 New UI themes
- 🧠 AI engine improvements
- 📚 Documentation and examples

## 📋 Roadmap

### Version 1.1
- [ ] Voice assistant integration
- [ ] Custom keyboard shortcuts
- [ ] Advanced auto-completion
- [ ] Plugin marketplace

### Version 1.2
- [ ] Docker container support
- [ ] WSL integration
- [ ] PowerShell module
- [ ] Cloud sync for settings

## 🏆 Performance Benchmarks

| Hardware | Model | Speed (tokens/sec) | Memory Usage |
|----------|-------|-------------------|--------------|
| Intel i7 (CPU) | Phi-3.5-mini INT8 | ~15-25 | 1.5GB |
| NVIDIA RTX 4060 | Phi-3.5-mini FP16 | ~80-120 | 2.5GB |
| NVIDIA RTX 4090 | Phi-3.5-mini FP16 | ~200-300 | 2.5GB |

## 📝 License

This project is licensed under the MIT License. See [LICENSE](LICENSE) for details.

## 🙏 Acknowledgments

- **Microsoft**: For Phi-3.5 and onnxruntime
- **Spectre.Console**: For the excellent console UI library
- **.NET Community**: For tools and libraries

## 📞 Contact

- **Author**: Oghost
- **GitHub**: [@Oghost](https://github.com/Oghost)
- **Issues**: [GitHub Issues](https://github.com/Oghost/AiShell/issues)

---

**⭐ If you like the project, don't forget to give it a star!**

Made with ❤️ by the open source community

## 🔧 Development Setup

### Prerequisites
```bash
# Install .NET 8.0 SDK
winget install Microsoft.DotNet.SDK.8

# Install Git (if not already installed)
winget install Git.Git

# Optional: Install Visual Studio Code
winget install Microsoft.VisualStudioCode
```

### Environment Setup
```bash
# Clone and setup
git clone https://github.com/Oghost/AiShell.git
cd AiShell

# Restore packages
dotnet restore

# Build in debug mode
dotnet build

# Run the application
dotnet run
```

### Testing
```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test category
dotnet test --filter Category=Unit
```

## 📊 Telemetry and Privacy

AiShell respects your privacy:

- ✅ **Local AI Processing**: All AI operations run locally on your machine
- ✅ **No Data Collection**: No personal data is sent to external servers
- ✅ **Optional Telemetry**: Performance metrics are optional and anonymous
- ✅ **Transparent**: All code is open source and auditable

Cloud LLM features (GPT-4, Claude) are optional and require explicit user activation with API keys.

## 🐛 Troubleshooting

### Common Issues

**Model Download Fails**
```bash
# Manual download
# 1. Go to https://huggingface.co/microsoft/Phi-3.5-mini-instruct-onnx
# 2. Download the appropriate model file
# 3. Place in: %APPDATA%\AiShell\Models\
```

**CUDA Not Detected**
```bash
# Check NVIDIA drivers
nvidia-smi

# Update drivers from: https://www.nvidia.com/drivers
```

**Permission Errors**
```bash
# Run as administrator (if needed)
# Or check folder permissions in %APPDATA%\AiShell\
```

### Debug Mode
```bash
# Enable verbose logging
AiShell.exe --verbose

# Check logs in
type "%APPDATA%\AiShell\logs\aishell.log"
```

## 🎯 Use Cases

### For Developers
- Quick git operations
- File management
- Network diagnostics
- System monitoring

### For System Administrators
- Service management
- Network troubleshooting
- Batch operations
- System monitoring

### For Content Creators
- Video processing with ffmpeg
- File organization
- Batch conversions
- Media analysis

### For Power Users
- Complex file operations
- Automation tasks
- System optimization
- Learning new commands

---

*Last updated: 2025-06-01 22:12:00 UTC by Oghost*