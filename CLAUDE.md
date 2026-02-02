# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

ATT_Wrapper is a Windows Forms (.NET Framework 4.8) application that wraps Jatlas automated testing scripts. It provides a GUI for executing hardware validation tests, parsing test output, and uploading results to various reporting services (NextCloud, Feishu, Calydon).

**Target Framework:** .NET Framework 4.8
**Architecture:** WinForms with Material Design skin
**Language:** C# with Russian UI but English code documentation

## Build & Development Commands

### Building the Project
```bash
# Build Debug configuration
msbuild ATT_Wrapper.sln /p:Configuration=Debug

# Build Release configuration
msbuild ATT_Wrapper.sln /p:Configuration=Release

# Or use Visual Studio:
# Open ATT_Wrapper.sln in Visual Studio and press F5
```

### Running the Application
```bash
# Run from bin folder after building
.\bin\Debug\ATT_Wrapper.exe
# or
.\bin\Release\ATT_Wrapper.exe
```

### Dependencies
All dependencies are managed via NuGet (packages.config). Run NuGet restore if packages are missing:
```bash
nuget restore ATT_Wrapper.sln
```

Key dependencies:
- **MaterialSkin** (2.2.3.1) - Material Design UI theme
- **Serilog** (4.3.0) - Logging framework
- **Newtonsoft.Json** (13.0.4) - JSON handling for mappings.json
- **Costura.Fody** (6.0.0) - Embeds dependencies into single executable

## Core Architecture

### Process Execution Layer
The application uses **Windows PseudoConsole API** to execute batch scripts while capturing ANSI-colored output:

- **ProcessExecutor** (`Services/ProcessExecutor.cs`): Manages PseudoConsole lifecycle, sends input, captures raw output with ANSI codes
- Scripts are located at: `C:\jatlas\scripts\win_scripts\` (hardcoded in `JatlasTestRunnerForm.cs:27`)
- All scripts are `.bat` files invoked via `cmd.exe /c`

### Output Processing Pipeline
Raw console output → BufferLines → ParseANSI → ExtractLogic → DisplayUI

1. **ConsoleOutputHandler** (`Services/ConsoleOutputHandler.cs`):
   - Buffers raw output chunks until complete lines appear
   - Handles ANSI line resets (carriage returns, cursor positioning)
   - Extracts final line state from multi-frame ANSI animations
   - Splits processing into two paths: **Parsing** (for logic) and **Rendering** (for UI)
   - Auto-detects "Press any key" prompts and auto-continues

2. **ILogParser** Interface → **JatlasTestParser** / **JatlasUpdateParser**:
   - Parsers use **LogPatterns.cs** (pre-compiled regex patterns)
   - Extract structured results: `LogResult { Level, Message, GroupKey }`
   - LogLevels: `Pass`, `Fail`, `Error`, `Progress`
   - GroupKey format: `"uploader:nextcloud"`, `"uploader:webhook"`, etc.

3. **IConsoleRenderer** Interface → **RichTextBoxConsoleRenderer**:
   - Decouples UI rendering from parsing logic
   - Appends ANSI-colored text to RichTextBox with color interpretation

### Test Result Management

**MappingManager** (`Services/MappingManager.cs`):
- Singleton pattern with lazy initialization
- Loads `mappings.json` at startup
- Pre-compiles regex patterns for performance
- Maps log messages → `(GroupName, UFN)` pairs for result grid
- Example: `"cpu temperature"` → `("CPU", "Temperature")`

**ResultsGridController** (`UI/Helpers/ResultsGridController.cs`):
- Manages DataGridView displaying test results
- Groups results by category (CPU, Memory, Display, etc.)
- Auto-updates or creates rows based on mapping
- Handles PASS/FAIL/ERROR status with color coding

**ReportStatusManager** (`UI/Helpers/ReportStatusManager.cs`):
- Tracks upload status for each uploader service
- Updates icon + label pairs on the form
- Resets state before each test run

### Form Lifecycle & Threading

**JatlasTestRunnerForm** (`UI/Forms/JatlasTestRunnerForm.cs`):
- Main form uses `IsTaskRunning` property to manage button states
- All ProcessExecutor callbacks use `BeginInvoke` for thread-safety
- **RunTest()** method:
  1. Clears UI (grid, log, uploader status)
  2. Initializes ProcessExecutor + ConsoleOutputHandler
  3. Sets ANSI environment variables (`TERM=xterm-256color`, `FORCE_COLOR=1`)
  4. Starts process with full command line
  5. Registers callbacks: `OnOutputReceived` → `HandleOutput`, `OnExited` → `HandleExit`

## Key Configuration Files

### mappings.json
JSON array defining test result mappings:
```json
{
  "pattern": "cpu temperature",    // Regex pattern (case-insensitive)
  "group": "CPU",                   // Category name
  "ufn": "Temperature"              // Unique Field Name (row identifier)
}
```
- Patterns can include capture groups: `"USB port ([\\w-]+)"` → `"ufn": "USB Port $1"`
- Longest matching pattern wins (sorted by pattern length)
- Compiled at startup for performance

### Logging Setup
- **Serilog** configured in `SetupLogging()`
- Log file: `logs/jatlas_runner.log` (cleared on each app start)
- Log sinks: Debug window + File
- Custom enricher: `CallerEnricher` adds method names to log entries

## Common Test Scenarios

### Running Tests
All test buttons call `RunTest(parser, script, args, onFinished)`:
- **Update**: `update.bat` → Uses `JatlasUpdateParser` → Checks for Jatlas updates
- **Common ATT**: `run-jatlas-auto.bat -l common --stage dev` → Hardware validation
- **Special ATT**: `run-jatlas-auto.bat -l special --stage dev` → Special tests (network bitrate, etc.)
- **Aging ATT**: `run-jatlas-auto.bat -l aging --stage dev` → Stress tests
- **Common Offline**: `run-jatlas-auto.bat -l common --offline` → No network required

### Uploader Services
The parser detects upload results via patterns:
- **NextCloud**: JSON + HTML report upload
- **Feishu**: Feishu bot webhook
- **Calydon**: Custom webhook (`calydonqc.com`)

Error detection includes:
- Network errors (connection failures, DNS resolution)
- Uploader-specific errors (logged as `uploader:name` failures)
- Host mapping (e.g., `feishu.cn` → `feishubot`, `calydonqc.com` → `webhook`)

## Code Style Notes

**From global CLAUDE.md:**
- Responses must be concise, no pleasantries
- Return only changed code blocks with minimal context
- Every method requires detailed XML documentation in English
- Comments must be concise and descriptive
- Communication in Russian, technical docs in English
- Follow Separation of Concerns and Single Source of Truth

**Namespace Structure:**
```
ATT_Wrapper
├── Models/          - Data models (LogResult, MappingItem)
├── Services/        - Business logic (ProcessExecutor, MappingManager, GitRunner, etc.)
├── Parsing/         - Log parsing (JatlasTestParser, JatlasUpdateParser, LogPatterns)
├── Interfaces/      - Abstractions (ILogParser, IConsoleRenderer, IResultsGridController, etc.)
└── UI/
    ├── Forms/       - Main form (JatlasTestRunnerForm)
    ├── Controls/    - Custom controls (CustomMaterialButton, ToolStripLoadingSpinner)
    └── Helpers/     - UI helpers (ReportStatusManager, ResultsGridController, ThemeManager)
```

## Important Implementation Details

1. **ANSI Processing**: The app handles complex ANSI sequences including:
   - Line resets (`\r`, cursor positioning)
   - Multi-frame animations (progress bars)
   - Color codes (used for PASS/FAIL detection)
   - "Press any key" auto-continuation

2. **State Management**:
   - `JatlasTestParser` maintains internal state machine for multi-line parsing (e.g., NextCloud continuation)
   - `IsTaskRunning` property automatically manages button enable/disable states
   - ReportStatusManager tracks upload progress independently

3. **Thread Safety**:
   - All UI updates from ProcessExecutor callbacks use `BeginInvoke`
   - ConsoleOutputHandler uses `lock (_bufferLock)` for line buffer operations
   - RichTextBox rendering is always invoked on UI thread

4. **Performance Optimizations**:
   - Regex patterns compiled once at startup (`RegexOptions.Compiled`)
   - MappingManager uses Singleton with lazy initialization
   - Line buffer consolidation (50k char limit before forced flush)

## Testing Notes

- No automated test framework is currently configured in the csproj
- xUnit packages are referenced but no test projects exist
- Manual testing via UI buttons is the primary validation method
