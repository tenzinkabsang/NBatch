# NBatch Package Structure Recommendations
**For Mass Adoption - Starting Fresh**

## Executive Summary

With no existing users to worry about, you can optimize purely for adoption and developer experience. The recommendation is: **Start modular from day one with crystal-clear conventions**.

---

## Recommended Package Structure

### Core Packages

#### 1. **NBatch.Core** (The Foundation)
- **Contents**: Pure abstractions, interfaces, base classes
- **Dependencies**: None (zero dependencies)
- **Purpose**: For advanced users and custom implementations
```
NBatch.Abstractions
├─ IReader<T>
├─ IWriter<T>
├─ IProcessor<T, TResult>
├─ IJob
├─ IStep
└─ Base classes for common patterns
```

#### 2. **NBatch** (The Default Experience)
- **Contents**: In-memory implementation + core abstractions
- **Dependencies**: NBatch.Core only
- **Purpose**: Get started in 60 seconds - perfect for:
  - Tutorials and demos
  - Development and testing
  - Small batch jobs that don't need persistence
  - Proof of concepts

```csharp
// Install-Package NBatch
var job = Job.CreateBuilder("MyJob")
    .AddStep(...)
    .Build();
await job.RunAsync();
```

### Persistence Providers

#### 3. **NBatch.Persistence.SqlServer**
- **Dependencies**: NBatch.Core + Microsoft.Data.SqlClient
- **When to use**: Production jobs with SQL Server

#### 4. **NBatch.Persistence.PostgreSql**  
- **Dependencies**: NBatch.Core + Npgsql
- **When to use**: Production jobs with PostgreSQL

#### 5. **NBatch.Persistence.MySql**
- **Dependencies**: NBatch.Core + MySqlConnector
- **When to use**: Production jobs with MySQL

#### 6. **NBatch.Persistence.Sqlite**
- **Dependencies**: NBatch.Core + Microsoft.Data.Sqlite
- **When to use**: Embedded scenarios, local dev, testing

#### 7. **NBatch.Persistence.Cosmos** (Future)
- **Dependencies**: NBatch.Core + Azure.Cosmos
- **When to use**: Cloud-native batch processing

### Extension Packages

#### 8. **NBatch.Extensions.DependencyInjection**
- **Contents**: ServiceCollection extensions for ASP.NET Core
- **Dependencies**: NBatch.Core + Microsoft.Extensions.DependencyInjection
```csharp
services.AddNBatch(options => {
    options.UseSqlServer(connectionString);
});
```

#### 9. **NBatch.Extensions.Logging** (Optional)
- **Contents**: Structured logging integration
- **Dependencies**: NBatch.Core + Microsoft.Extensions.Logging

#### 10. **NBatch.Extensions.Metrics** (Optional)
- **Contents**: OpenTelemetry/metrics integration
- **Dependencies**: NBatch.Core + System.Diagnostics.DiagnosticSource

---

## Why This Structure Wins for Adoption

### 1. **Clear Entry Points**
```
Beginner → NBatch (in-memory)
Production → NBatch + NBatch.Persistence.SqlServer
Advanced → NBatch.Core + custom implementations
```

### 2. **Progressive Complexity**
- Day 1: `Install-Package NBatch` → working in minutes
- Week 1: Add `NBatch.Persistence.SqlServer` → production ready
- Month 1: Explore custom readers/writers with NBatch.Core

### 3. **Follows .NET Ecosystem Patterns**
Developers already understand this from:
- Entity Framework Core
- Serilog
- MassTransit
- StackExchange.Redis

### 4. **Marketing Benefits**
**Landing Page Hero Section:**
```
Get Started:    Install-Package NBatch
For Production: Install-Package NBatch.Persistence.SqlServer
```

Simple, memorable, actionable.

---

## Package Naming Conventions

✅ **DO:**
- Use `NBatch.*` for all official packages
- Use `.Persistence.*` for database providers
- Use `.Extensions.*` for optional features
- Use `.Integrations.*` for third-party integrations (future: Hangfire, Quartz)

❌ **DON'T:**
- Mix naming styles (NBatch.SqlServer vs NBatch.Persistence.PostgreSql)
- Use abbreviations (NBatch.PS for PowerShell, NBatch.EF for Entity Framework)
- Create packages without clear category (NBatch.Stuff, NBatch.Helpers)

---

## Version Strategy

### Synchronized Versioning
All official packages ship with the same version number.

```
NBatch                          1.0.0
NBatch.Core                     1.0.0
NBatch.Persistence.SqlServer    1.0.0
NBatch.Persistence.PostgreSql   1.0.0
```

**Why?**
- Eliminates version confusion
- Users know compatible versions instantly
- Simplifies documentation
- Follows Microsoft's approach with EF Core

### Breaking Changes
When one package has breaking changes, bump all packages to next major version.

---

## Documentation Strategy

### Quick Start (Homepage)

```markdown
# NBatch - Batch Processing for .NET

## 60 Second Start
Install-Package NBatch

var job = Job.CreateBuilder("MyJob")
    .AddStep("Process Products", 
        reader: new CsvReader<Product>("products.csv"),
        writer: new InMemoryWriter<Product>())
    .Build();
    
await job.RunAsync();

## Production Ready (SQL Server)
Install-Package NBatch.Persistence.SqlServer

var job = Job.CreateBuilder("MyJob", connectionString)
    .UseSqlServer()
    .AddStep(...)
    .Build();

## Other Databases
- PostgreSQL: Install-Package NBatch.Persistence.PostgreSql
- MySQL: Install-Package NBatch.Persistence.MySql
- SQLite: Install-Package NBatch.Persistence.Sqlite
```

### Package Decision Tree

```
Do you need persistence?
├─ No → NBatch (in-memory)
└─ Yes → What database?
    ├─ SQL Server → NBatch.Persistence.SqlServer
    ├─ PostgreSQL → NBatch.Persistence.PostgreSql
    ├─ MySQL → NBatch.Persistence.MySql
    └─ SQLite → NBatch.Persistence.Sqlite

Do you need ASP.NET Core integration?
└─ Yes → NBatch.Extensions.DependencyInjection

Building custom implementations?
└─ Yes → NBatch.Core (zero dependencies)
```

---

## Initial Release Strategy

### Phase 1: Core Foundation (v1.0.0)
Ship these together:
```
✅ NBatch.Core
✅ NBatch (in-memory)
✅ NBatch.Persistence.SqlServer
✅ NBatch.Extensions.DependencyInjection
```

### Phase 2: Expand Ecosystem (v1.1.0)
```
✅ NBatch.Persistence.PostgreSql
✅ NBatch.Persistence.MySql
✅ NBatch.Extensions.Logging
```

### Phase 3: Cloud & Advanced (v1.2.0+)
```
✅ NBatch.Persistence.Cosmos
✅ NBatch.Persistence.MongoDB
✅ NBatch.Extensions.Metrics
```

**Why staged releases?**
- Get feedback early from SQL Server users (largest .NET audience)
- Iterate on abstractions before expanding
- Build community momentum progressively
- Avoid maintenance burden of 10+ packages on day one

---

## NuGet Package Metadata

### Common Fields (All Packages)
```xml
<PackageId>NBatch.*</PackageId>
<Authors>Tenzin Kabsang</Authors>
<Description>See specific package</Description>
<PackageLicenseExpression>MIT</PackageLicenseExpression>
<PackageProjectUrl>https://github.com/tenzinkabsang/NBatch</PackageProjectUrl>
<RepositoryUrl>https://github.com/tenzinkabsang/NBatch</RepositoryUrl>
<PackageTags>batch;batch-processing;etl;spring-batch;job;task</PackageTags>
<PackageIcon>icon.png</PackageIcon>
<PackageReadmeFile>README.md</PackageReadmeFile>
```

### Package-Specific Descriptions

**NBatch.Core:**
> Core abstractions for NBatch batch processing framework. Install this for building custom implementations.

**NBatch:**
> Simple batch processing framework for .NET with in-memory job persistence. Perfect for getting started. For production use, install a persistence provider like NBatch.Persistence.SqlServer.

**NBatch.Persistence.SqlServer:**
> SQL Server persistence provider for NBatch. Enables job restart, failure recovery, and execution history.

**NBatch.Extensions.DependencyInjection:**
> ASP.NET Core dependency injection extensions for NBatch. Simplifies service registration and configuration.

---

## Migration Path (From Current NBatch)

Since we're assuming no existing users, you can:

1. **Archive the old package**
   - Mark current NBatch as deprecated on NuGet
   - Point to new package structure in deprecation message

2. **Fresh start with new structure**
   - Release as NBatch 2.0.0 with new architecture
   - Clear break from v1.x
   - No backward compatibility concerns

3. **Communication**
   ```
   NBatch 2.0 represents a complete rewrite with modular architecture.
   - Breaking change: Package structure is now modular
   - Benefits: Smaller dependencies, clearer choices, better performance
   - Migration: See migration guide at https://nbatch.dev/migrate
   ```

---

## Sample Project Structure

```
NBatch/
├── src/
│   ├── NBatch.Core/
│   │   ├── Abstractions/
│   │   ├── Common/
│   │   └── NBatch.Core.csproj
│   │
│   ├── NBatch/
│   │   ├── InMemory/
│   │   ├── Builders/
│   │   └── NBatch.csproj (depends on NBatch.Core)
│   │
│   ├── NBatch.Persistence.SqlServer/
│   │   ├── Repository/
│   │   ├── Migrations/
│   │   └── NBatch.Persistence.SqlServer.csproj
│   │
│   ├── NBatch.Persistence.PostgreSql/
│   │   └── ...
│   │
│   └── NBatch.Extensions.DependencyInjection/
│       └── ServiceCollectionExtensions.cs
│
├── samples/
│   ├── QuickStart.InMemory/
│   ├── Production.SqlServer/
│   ├── AspNetCore.Integration/
│   └── Custom.Implementation/
│
├── tests/
│   ├── NBatch.Core.Tests/
│   ├── NBatch.Tests/
│   └── NBatch.Persistence.SqlServer.Tests/
│
└── docs/
    ├── getting-started.md
    ├── architecture.md
    └── migration-guide.md
```

---

## Marketing & Discovery Strategy

### 1. README.md Optimizations
```markdown
# 🚀 NBatch - Batch Processing for .NET

[Quickstart] [Documentation] [Samples] [API Reference]

## Install
Install-Package NBatch

## Features
✅ Restart failed jobs where they left off
✅ Skip malformed records with configurable limits  
✅ Chunk processing with transaction support
✅ Built-in readers: CSV, Database, Custom
✅ Built-in writers: Database, File, Custom
✅ Spring Batch-inspired, but simpler
```

### 2. NuGet SEO Keywords
```
batch, batch-processing, etl, data-processing, job-processing,
spring-batch, quartz, hangfire-alternative, bulk-processing,
csv-processing, file-processing, database-batch, .net-batch
```

### 3. Sample Projects Repository
Create **NBatch.Samples** repo with:
- CSV to Database
- Database to Database
- REST API to Database  
- File processing with error handling
- ASP.NET Core background job
- Custom reader/writer implementation

### 4. Blog Post Series
- "Introducing NBatch 2.0"
- "Why We Chose a Modular Architecture"
- "Migrating from Hangfire/Quartz to NBatch"
- "Building Custom Batch Processors"

### 5. Community Engagement
- Stack Overflow tag: `nbatch`
- Reddit posts in r/dotnet, r/csharp
- .NET community standups
- Conference talks (NDC, .NET Conf)

---

## Success Metrics

### Initial Goals (First 6 Months)
- ⭐ 100+ GitHub stars
- 📦 10,000+ NuGet downloads total
- 📝 10+ community samples/tutorials
- 🐛 Active issue resolution (<48hr response)
- 📖 Complete API documentation

### Long-term Goals (12 Months)
- ⭐ 500+ GitHub stars  
- 📦 50,000+ NuGet downloads
- 🏢 5+ companies using in production
- 📚 Third-party tutorials/courses
- 🌍 Multi-language documentation

---

## Key Differentiators

### vs Spring Batch (Java)
✅ Simpler API surface
✅ Better async/await support
✅ More .NET-idiomatic
✅ Lighter weight

### vs Hangfire
✅ Purpose-built for batch processing
✅ Better support for readers/writers
✅ Chunk processing with transactions
✅ Restart capability built-in

### vs Quartz.NET
✅ Higher-level abstractions
✅ Built-in skip policies
✅ Reader/Writer/Processor pattern
✅ Less boilerplate

**Position NBatch as:** "The simple, reliable batch processing framework for .NET - inspired by Spring Batch but built for modern .NET"

---

## Critical First Impressions

When a developer lands on your GitHub or NuGet page, they should immediately see:

1. **What it does** (1 sentence)
   > "Simple, reliable batch processing for .NET - read, transform, write data with built-in error handling and restart capability"

2. **How to install** (1 line)
   > `Install-Package NBatch`

3. **Working code** (< 20 lines)
   > [See sample in README]

4. **Why choose this** (3 bullets)
   > - Spring Batch simplicity meets .NET async/await
   > - Restart failed jobs where they left off
   > - Zero ceremony for simple jobs, powerful when you need it

5. **Next steps** (links)
   > [Documentation] [Samples] [API Docs]

---

## Final Recommendation

**Ship NBatch 2.0 with this structure:**

```
Required for v1.0.0:
✅ NBatch.Core
✅ NBatch (in-memory)
✅ NBatch.Persistence.SqlServer
✅ NBatch.Extensions.DependencyInjection

Nice to have for v1.0.0:
⚠️ NBatch.Persistence.PostgreSql (if time permits)

Defer to v1.1.0:
⏸️ NBatch.Persistence.MySql
⏸️ NBatch.Persistence.Sqlite
⏸️ NBatch.Extensions.Logging
```

**Why this minimal viable release?**
- Gets core value to users fastest
- SQL Server covers majority of .NET shops
- Allows validation of abstractions before expanding
- Easier to maintain quality with fewer packages
- Can iterate based on real user feedback

**Success looks like:**
A developer can go from `Install-Package NBatch` to a working batch job in under 5 minutes, with a clear path to production using `NBatch.Persistence.SqlServer` when ready.

---

## Bonus: README Template

```markdown
# NBatch

Simple batch processing for .NET - inspired by Spring Batch

[![NuGet](https://img.shields.io/nuget/v/NBatch.svg)](https://www.nuget.org/packages/NBatch/)
[![Downloads](https://img.shields.io/nuget/dt/NBatch.svg)](https://www.nuget.org/packages/NBatch/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

## Quick Start

```csharp
// Install-Package NBatch
var job = Job.CreateBuilder("ProductImport")
    .AddStep("Import CSV",
        reader: new CsvReader<Product>("products.csv"),
        writer: new DbWriter<Product>(connectionString))
    .Build();

await job.RunAsync();
```

## Features

✅ **Restart Capability** - Jobs resume where they left off after failures  
✅ **Skip Policies** - Continue processing when encountering bad records  
✅ **Chunk Processing** - Process large datasets in configurable batches  
✅ **Transaction Support** - Rollback chunks on failure  
✅ **Extensible** - Custom readers, writers, processors  
✅ **Zero Ceremony** - Simple jobs require minimal code  

## Installation

```bash
# In-memory (development)
Install-Package NBatch

# Production (SQL Server)
Install-Package NBatch.Persistence.SqlServer

# ASP.NET Core integration
Install-Package NBatch.Extensions.DependencyInjection
```

## Documentation

- [Getting Started](docs/getting-started.md)
- [Samples](samples/)
- [API Reference](https://nbatch.dev/api)

## Why NBatch?

| NBatch | Alternatives |
|--------|-------------|
| Purpose-built for batch/ETL | Hangfire/Quartz are job schedulers |
| Reader → Processor → Writer | Manual implementation needed |
| Built-in restart & skip logic | Build it yourself |
| Chunk transactions | Manual transaction management |

## License

MIT © Tenzin Kabsang
```

---

**Last Updated:** February 2026  
**Author:** Claude (Anthropic)
