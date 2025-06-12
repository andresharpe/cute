# Intro  

Buttercup is a self-hosted, open-source content management backend built for flexibility, speed, and freedom. Designed to be Contentful API compatible, this project gives you full control of your content infrastructure with no throttling, no paywalls, and ultimate extensibility.  

Built with .NET and Entity Framework Core (EF Core), data is stored in PostgreSQL, and the system can operate both online (connected) and offline (disconnected). It can even sync with a live Contentful environment, acting as a cache, proxy, or a fully autonomous CMS.  

# Features  

- **Contentful REST APIs**: Full support for Delivery, Preview, and Management APIs  
- **GraphQL API**: Built-in GraphQL endpoint for flexible queries  
- **Webhooks**: Automatic webhook invocation for event-driven integrations  
- **PostgreSQL or Sqlite Storage**: Robust and reliable persistence layer  
- **Connected Mode**: Proxy or cache Contentful in real-time  
- **Disconnected Mode**: Operate fully independently after syncing  
- **Schema & Content Sync**: Easy synchronization with existing Contentful environments  
- **Open & Extensible**: Fully open-source with customization points  
- **Ultra-fast**: Designed for low-latency, high-throughput workloads  
- **No Throttling**: Unlimited API access without rate limits  

# Typical Use Cases  

- **Local Development**: Build and test applications without needing Contentful subscriptions.  
- **Enterprise Hosting**: Self-hosted solution to meet regulatory, compliance, or internal network requirements.  
- **Disaster Recovery**: Serve as a fallback CMS in case of Contentful downtime.  
- **Proxy Caching**: Act as an intelligent cache to minimize Contentful API usage and costs.  
- **Data Replication**: Periodically sync Contentful environments into your infrastructure.  
- **Cost Management**: Avoid usage-based billing and enjoy predictable hosting costs.  
- **Customization**: Extend and modify the behavior, schema validations, and workflows.  

# Tech Stack  

- **Backend**: .NET 9, ASP.NET Core, EF Core  
- **Database**: PostgreSQL  
- **API**: REST + GraphQL (HotChocolate)  
- **Webhooks**: Outbound integrations  
- Contentful csharp client library

# Deployment  

- Docker  
- Kubernetes  
- Direct hosting  

# Cute CLI Extension  

Buttercup commands are under `cute buttercup`, designed to manage configuration, sync, and server operations for Buttercup.  

## Commands  

### 1. `cute buttercup config`  
Setup or update a Buttercup profile (database, Contentful settings, server options).  

**Switches:**  

| Switch                  | Required? | Description                                                                 |
|-------------------------|-----------|-----------------------------------------------------------------------------|
| `--profile <profile-name>` | No        | Profile name (default: buttercup-default)                                   |
| `--db-connection <string>` | Yes       | PostgreSQL connection string                                                |
| `--space-id <id>`         | Yes       | Contentful Space ID                                                         |
| `--environment-id <id>`   | Yes       | Contentful Environment ID (default: master)                                 |
| `--management-token <token>` | Yes    | Content Management API token                                                |
| `--delivery-token <token>` | Yes      | Content Delivery API token                                                  |
| `--preview-token <token>`  | Yes      | Content Preview API token                                                   |
| `--port <port>`            | No        | Port to run server on (default: 5000)                                       |
| `--host <hostname>`        | No        | Host IP/name (default: localhost)                                           |
| `--auto-sync-schema <true/false>` | No | Sync schema automatically on server start (default: true)                   |
| `--auto-sync-content <true/false>` | No | Sync content automatically on server start (default: true)                  |
| `--reset`                  | No        | Reset profile if it already exists and reconfigure                          |  

### 2. `cute buttercup sync-schema`  
Pull Contentful content types and map them dynamically to Postgres tables.  

**Switches:**  

| Switch                  | Required? | Description                                                                 |
|-------------------------|-----------|-----------------------------------------------------------------------------|
| `--profile <profile-name>` | No        | Use a specific profile (default: buttercup-default)                         |
| `--force`                 | No        | Force re-sync schema even if already synced                                 |  

### 3. `cute buttercup sync-content`  
Pull entries from Contentful and populate the Postgres database.  

**Switches:**  

| Switch                  | Required? | Description                                                                 |
|-------------------------|-----------|-----------------------------------------------------------------------------|
| `--profile <profile-name>` | No        | Use a specific profile (default: buttercup-default)                         |
| `--full`                  | No        | Full content pull (wipe existing and reload everything)                     |
| `--delta`                 | No        | Only pull new or changed content (incremental sync)                         |
| `--types <list>`          | No        | Only sync specific content types (comma-separated)                          |  

### 4. `cute buttercup run-server`  
Launch the Buttercup server (REST APIs, GraphQL, Webhooks).  

**Switches:**  

| Switch                  | Required? | Description                                                                 |
|-------------------------|-----------|-----------------------------------------------------------------------------|
| `--profile <profile-name>` | No        | Use a specific profile (default: buttercup-default)                         |
| `--port <port>`            | No        | Override server port from config                                            |
| `--host <hostname>`        | No        | Override server host from config                                            |
| `--no-auto-sync`           | No        | Skip auto sync of schema and content even if config says true               |
| `--disable-webhooks`       | No        | Disable webhook listener (default: enabled)                                 |  

# Quick Overview Table  

| Command                | Purpose                                                                 |
|------------------------|-------------------------------------------------------------------------|
| `config`               | Set up Buttercup configuration and Contentful/API keys                 |
| `sync-schema`          | Create or update database schema dynamically                           |
| `sync-content`         | Populate database with content (full or delta)                         |
| `run-server`           | Start API server with Delivery, Preview, Management, GraphQL, Webhooks |  

# Notes  

- **Profiles (`--profile`)** allow multiple spaces/environments/databases to be managed easily.  
- Defaults always favor ease-of-use but allow explicit overrides.  
- Validation will check database access + Contentful API tokens at config time.

