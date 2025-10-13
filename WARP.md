# WARP.md

This file provides guidance to WARP (warp.dev) when working with code in this repository.

## Project Overview

**Cute** is a cross-platform CLI tool that brings advanced features including bulk operations, web server mode, AI generation, and language translation to working with Contentful CMS. It's a .NET 9 global tool distributed via NuGet.

### Key Capabilities
- Download/upload content in multiple formats (Excel, CSV, TSV, JSON, YAML)
- Bulk operations (publish/unpublish, edit, search & replace, delete)
- AI-powered content generation using OpenAI GPT models
- Content translation using multiple AI translation services
- External data synchronization (APIs, databases, WikiData)
- Server modes (scheduler, webhooks) with OpenTelemetry support
- TypeScript/C# type scaffolding for content models

## Essential Commands

### Development Setup & Build
```powershell
# Build the entire solution
dotnet build

# Build and pack for NuGet distribution
dotnet build --configuration Release
dotnet pack --configuration Release

# Run the main CLI tool locally
dotnet run --project source/Cute

# Install as global tool locally for testing
dotnet tool uninstall -g cute
dotnet tool install --global --add-source ./nupkg cute

# Run tests
dotnet test
```

### Testing the CLI Tool
```powershell
# After installing as global tool, test basic functionality
cute --help
cute --version

# Test login configuration (requires Contentful credentials)
cute login

# Display space information
cute info

# Test content operations (replace <contentType> with actual content type)
cute content download --content-type-id <contentType> --format excel
cute content upload --help
```

### Running as Server
```powershell
# Run in scheduler mode (requires cuteSchedule content type configured)
cute server scheduler --port 8080

# Run in webhooks mode
cute server webhooks --port 8080
```

### Development Workflow
```powershell
# Clean and rebuild
dotnet clean && dotnet build

# Run specific tests
dotnet test --filter "TestName"

# Check for warnings as errors (already enabled in projects)
dotnet build --verbosity normal
```

## Architecture Overview

### Project Structure
- **source/Cute/** - Main CLI application (executable, packaged as NuGet tool)
- **source/Cute.Lib/** - Core library with all business logic and services
- **source/Cute.PythonServer/** - Python server component for extended functionality
- **tests/Cute.Unit.Tests/** - XUnit test suite with FluentAssertions

### Core Architecture Patterns

**Command Pattern Implementation:**
- CLI built using `Spectre.Console.Cli` framework
- Commands inherit from base classes: `BaseLoggedInCommand<T>`, `BaseServerCommand<T>`
- Settings classes define command-line parameters and validation
- Each major feature area has its own command namespace (Content, Chat, Server, etc.)

**Service Layer Architecture:**
- Heavy use of dependency injection for service registration
- Services handle external integrations: Contentful API, OpenAI, translation services
- Configuration managed through .env files and user secrets
- Extensive use of async/await patterns throughout

**Data Processing Pipeline:**
- Content transformation supports multiple formats (Excel, CSV, JSON, YAML, TSV)
- Template engine using Scriban for data mapping and transformations
- GraphQL queries for complex Contentful data retrieval
- Bulk operations with progress reporting via Spectre.Console

### Key Technologies & Dependencies
- **.NET 9** - Target framework
- **Spectre.Console** - CLI framework and rich terminal UI
- **Contentful.csharp** - Official Contentful SDK
- **Azure.AI.OpenAI** - AI content generation
- **Serilog** - Structured logging
- **ClosedXML** - Excel file processing
- **CsvHelper** - CSV processing
- **YamlDotNet** - YAML processing
- **Dapper** - Database connectivity for sync operations

### Configuration Management
- Uses `dotenv.net` for environment variable management
- Supports multiple AI services: OpenAI, Azure Translator, Google Translate, DeepL
- Contentful credentials stored via secure configuration (user secrets, .env)
- Server mode supports OpenTelemetry for observability

## Development Guidelines

### Code Style & Quality
- **TreatWarningsAsErrors** enabled on all projects
- Nullable reference types enabled
- Use of implicit usings for cleaner code
- Consistent async/await patterns
- Rich console output using Spectre.Console markup

### Testing Approach
- XUnit with FluentAssertions for readable test assertions
- Focus on testing business logic in Cute.Lib
- Integration tests for external service interactions
- Coverage collection enabled via coverlet

### AI Integration Patterns
Content generation and translation features require:
1. Configuration stored in Contentful content types (`cuteContentGenerate`, `cuteLanguage`)
2. Template-based prompts using Scriban syntax
3. Batch processing with progress reporting
4. Error handling for AI service rate limits and failures

### External Service Integration
- **Contentful**: Primary CMS integration using official SDK
- **Database Support**: SQL Server, PostgreSQL, MySQL, SQLite via provider pattern
- **Translation Services**: Multi-provider support with fallback strategies
- **File Processing**: Support for multiple formats with consistent interface

### Bulk Operations Design
Operations follow a consistent pattern:
1. Query/filter content entries
2. Transform data using configurable mappings
3. Validate changes before applying
4. Apply changes with progress reporting
5. Publish/unpublish as specified

## Important Notes

- This is a **global .NET tool** distributed via NuGet, not a traditional application
- Commands often require Contentful authentication (`cute login`)
- AI features require external service credentials (OpenAI, translation services)
- Server modes are designed for production deployment scenarios
- Content type scaffolding can generate TypeScript interfaces for frontend integration
- Extensive use of Contentful's content model for configuration storage

## Testing Scenarios

When developing new features, test these common workflows:
1. **Authentication flow**: `cute login` with various credential configurations
2. **Content download/upload cycle**: Test with different formats and content types
3. **AI generation**: Test with various prompt configurations and models
4. **Server modes**: Test scheduler and webhook functionality
5. **External sync**: Test database and API synchronization features