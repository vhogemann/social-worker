# PYTHON_SANDBOX.md — Self-Hosted Python Execution Sandbox

## Overview

An isolated, containerized Python runtime that the LLM can invoke to execute arbitrary Python code during chat. The sandbox runs as a separate Docker microservice with no network access, strict resource limits, and a stateless execution model.

## Architecture

```
┌─────────────┐  internal HTTP   ┌──────────────────┐
│   api (.NET)│ ────────────────> │  sandbox (FastAPI)│
│             │  POST /execute    │  air-gapped       │
│  ExecutePy- │ <──────────────── │  resource-limited │
│  thonTool   │  {stdout,stderr}  │  stateless        │
└─────────────┘                   └──────────────────┘
       │                                  │
       │ connects to                      │ on `sandbox-net`
       │ sandbox-net                      │ (internal: true)
       │ (in addition to                  │
       │  default bridge)                 │
                                         │
                               ┌─────────┴──────────┐
                               │  /app/libs (readonly)│
                               │  hybrid module lib   │
                               └─────────────────────┘
```

## Directory Layout

```
sandbox/
├── Dockerfile
├── requirements.txt
├── server.py                   # FastAPI entry point
├── runner.py                   # subprocess-based code execution
├── libs/                       # host-side library directory
│   └── .gitkeep

docker-compose.yml              # add sandbox service
```

**API side** (new files):

```
api/SocialWorker.Api/
├── Features/
│   └── Chat/
│       └── Tools/
│           └── ExecutePythonTool.cs
└── Infrastructure/
    └── Sandbox/
        ├── SandboxOptions.cs
        └── SandboxClient.cs
```

**Test side** (new files):

```
api/SocialWorker.Api.Tests/
├── SandboxClientTests.cs
└── ExecutePythonToolTests.cs
```

## Implementation Phases

### Phase 1 — Sandbox Service

#### `sandbox/requirements.txt`

```
fastapi==0.115.0
uvicorn==0.30.0
```

Minimal dependencies. The air-gapped container pre-installs commonly useful libraries:

```
numpy==2.5.0
pandas==2.2.0
sympy==1.13.0
```

These are installed at image build time (no PyPI access at runtime).

#### `sandbox/runner.py`

Core execution logic:

- Writes user code to a temp file (not `exec()` — separate process for clean kill, memory isolation, natural stdout capture)
- Executes via `subprocess.run` with `timeout=5`, `capture_output=True`
- Returns `{ stdout: str, stderr: str, exit_code: int, execution_time_ms: int }`
- Sets a restricted `__builtins__` environment by pre-pending a prologue that removes dangerous builtins (`__import__`, `eval`, `exec`, `open`, `compile`) and installs an import hook that only allows modules on an allowlist
- Temp file is deleted unconditionally in `finally`
- Process-level `SIGALRM` as a secondary timeout guard (kills even if subprocess hangs before user code starts)

**Module allowlist** (subject to growth):

```
math, json, collections, itertools, functools, operator, typing,
random, statistics, datetime, decimal, fractions, re, string,
textwrap, difflib, bisect, heapq, array, struct, hashlib, base64,
uuid, enum, dataclasses, copy, pprint, itertools, collections.abc,
pathlib.Path (blocked), os (blocked), subprocess (blocked),
socket (blocked), shutil (blocked), sys (restricted)
```

The import hook raises `ImportError` for any module not on the allowlist.

#### `sandbox/server.py`

FastAPI app with two endpoints:

```
POST /execute
  Request:  { "code": str, "timeout_secs": int (default 5, max 30) }
  Response: { "stdout": str, "stderr": str, "exit_code": int, "execution_time_ms": float }
  Status:   200 on success (even if user code errors), 400 on invalid request, 500 on internal error

GET /health
  Response: { "status": "ok" }
```

- Runs on port 8000
- No CORS (internal-only, only accessed by api)
- Logs execution duration and truncated output for debugging

#### `sandbox/Dockerfile`

```dockerfile
FROM python:3.12-slim AS build
WORKDIR /app
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

FROM python:3.12-slim AS runtime
WORKDIR /app
COPY --from=build /usr/local/lib/python3.12/site-packages /usr/local/lib/python3.12/site-packages
COPY --from=build /usr/local/bin /usr/local/bin

# Create non-root user
RUN addgroup --system sandbox && adduser --system --ingroup sandbox sandbox

COPY server.py runner.py ./

# Create libs directory for hybrid modules
RUN mkdir -p /app/libs && chown sandbox:sandbox /app/libs

USER sandbox
EXPOSE 8000
CMD ["uvicorn", "server:app", "--host", "0.0.0.0", "--port", "8000"]
```

Two-stage build keeps the image lean (build deps like `gcc` don't ship to runtime).

### Phase 2 — Docker Compose Integration

Add to `docker-compose.yml`:

```yaml
sandbox:
  build:
    context: ./sandbox
    dockerfile: Dockerfile
  networks:
    - sandbox-net
  environment:
    - PYTHONUNBUFFERED=1
    - MAX_EXECUTION_SECONDS=30
  volumes:
    - ./sandbox/libs:/app/libs:ro
  deploy:
    resources:
      limits:
        cpus: "0.5"
        memory: 128M
  restart: unless-stopped

networks:
  sandbox-net:
    internal: true
    driver: bridge
```

The `api` service must be connected to both the default network and `sandbox-net`:

```yaml
api:
  # ... existing config ...
  networks:
    - default
    - sandbox-net
  depends_on:
    db:
      condition: service_healthy
    sandbox:                        # add dependency
      condition: service_started
```

Design rationale for `internal: true`:
- LLM-generated code cannot make outbound HTTP requests (no data exfiltration, no scanning the LAN)
- Code cannot reach the API, the DB, or SearXNG — it's fully air-gapped
- Only the API can initiate requests to the sandbox
- The sandbox process cannot initiate any connections

### Phase 3 — API Infrastructure (C# Client)

#### `Infrastructure/Sandbox/SandboxOptions.cs`

```csharp
public sealed class SandboxOptions
{
    public const string SectionName = "Sandbox";
    public string BaseUrl { get; init; } = "http://sandbox:8000";
    public int DefaultTimeoutSeconds { get; init; } = 5;
    public int MaxTimeoutSeconds { get; init; } = 30;
}
```

#### `Infrastructure/Sandbox/SandboxClient.cs`

```csharp
public sealed class SandboxClient
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<SandboxOptions> _options;

    public SandboxClient(HttpClient httpClient, IOptions<SandboxOptions> options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<SandboxResult> ExecuteAsync(SandboxRequest request, CancellationToken ct)
    {
        var timeout = Math.Min(request.TimeoutSecs ?? _options.Value.DefaultTimeoutSeconds, _options.Value.MaxTimeoutSeconds);
        var payload = new { code = request.Code, timeout_secs = timeout };
        var json = JsonSerializer.Serialize(payload);
        var httpResponse = await _httpClient.PostAsync(
            $"{_options.Value.BaseUrl}/execute",
            new StringContent(json, Encoding.UTF8, "application/json"),
            ct);
        httpResponse.EnsureSuccessStatusCode();
        var body = await httpResponse.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<SandboxResult>(body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower })!;
    }
}

public sealed record SandboxRequest(string Code, int? TimeoutSecs = null);
public sealed record SandboxResult(string Stdout, string Stderr, int ExitCode, double ExecutionTimeMs);
```

Register in `Program.cs`:

```csharp
builder.Services.Configure<SandboxOptions>(builder.Configuration.GetSection(SandboxOptions.SectionName));
builder.Services.AddHttpClient<SandboxClient>(client =>
{
    // BaseUrl set via SandboxOptions, not on HttpClient
});
```

Add to `docker-compose.yml` env section for `api`:

```yaml
Sandbox__BaseUrl: http://sandbox:8000
Sandbox__DefaultTimeoutSeconds: 5
Sandbox__MaxTimeoutSeconds: 30
```

### Phase 4 — Chat Tool

#### `Features/Chat/Tools/ExecutePythonTool.cs`

```csharp
public sealed record ExecutePythonArgs(string Code);

[RegisterTool]
public sealed class ExecutePythonTool : ChatToolBase<ExecutePythonArgs, string>
{
    private readonly SandboxClient _sandbox;

    public ExecutePythonTool(SandboxClient sandbox)
    {
        _sandbox = sandbox;
    }

    public override string Name => "execute_python";
    public override string Description => "Execute Python code in an isolated sandbox and return the output. " +
        "The sandbox has no network access, a 5-second timeout, and only standard library modules " +
        "(math, json, collections, itertools, etc.) plus numpy and pandas are available. " +
        "Use this for calculations, data analysis, or any task that benefits from running Python code.";

    public override JsonElement Parameters => JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new
        {
            code = new
            {
                type = "string",
                description = "The Python code to execute"
            }
        },
        required = new[] { "code" }
    });

    public override async Task<string> ExecuteAsync(ExecutePythonArgs args, Guid? draftId, Guid userId, CancellationToken ct)
    {
        var result = await _sandbox.ExecuteAsync(new SandboxRequest(args.Code), ct);

        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(result.Stdout))
            sb.AppendLine("STDOUT:").AppendLine(result.Stdout);

        if (!string.IsNullOrEmpty(result.Stderr))
            sb.AppendLine("STDERR:").AppendLine(result.Stderr);

        sb.AppendLine($"Exit code: {result.ExitCode}");
        sb.AppendLine($"Execution time: {result.ExecutionTimeMs:F1}ms");

        return sb.ToString();
    }
}
```

Register in `Program.cs`:

```csharp
builder.Services.AddScoped<IChatTool, ExecutePythonTool>();
```

### Phase 5 — Tests

#### `SandboxClientTests.cs` (unit, mocked HTTP)

```csharp
public sealed class SandboxClientTests
{
    [Fact]
    public async Task ExecuteAsync_Returns_SandboxResult_On_Success()
    {
        var handler = new MockHttpMessageHandler(async req =>
        {
            req.RequestUri!.AbsolutePath.Should().Be("/execute");
            var body = await req.Content!.ReadAsStringAsync();
            body.Should().Contain("print(1+1)");

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {"stdout": "2\n", "stderr": "", "exit_code": 0, "execution_time_ms": 1.5}
                    """, Encoding.UTF8, "application/json")
            };
        });
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://sandbox:8000") };
        var opts = Options.Create(new SandboxOptions());
        var sandbox = new SandboxClient(client, opts);

        var result = await sandbox.ExecuteAsync(new SandboxRequest("print(1+1)"));

        result.Stdout.Trim().Should().Be("2");
        result.ExitCode.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_Throws_On_NonSuccess()
    {
        var handler = new MockHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://sandbox:8000") };
        var sandbox = new SandboxClient(client, Options.Create(new SandboxOptions()));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            sandbox.ExecuteAsync(new SandboxRequest("x = 1"), CancellationToken.None));
    }
}
```

#### `ExecutePythonToolTests.cs` (unit, mocked SandboxClient)

```csharp
public sealed class ExecutePythonToolTests
{
    [Fact]
    public async Task ExecuteAsync_Formats_Result_Correctly()
    {
        var mock = new Mock<SandboxClient>(MockBehavior.Strict, Mock.Of<HttpClient>(), Options.Create(new SandboxOptions()));
        mock.Setup(x => x.ExecuteAsync(It.IsAny<SandboxRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SandboxResult("42\n", "", 0, 2.3));

        var tool = new ExecutePythonTool(mock.Object);
        var result = await tool.ExecuteAsync(new ExecutePythonArgs("print(42)"), null, Guid.NewGuid(), CancellationToken.None);

        result.Should().Contain("42");
        result.Should().Contain("Exit code: 0");
        result.Should().Contain("Execution time: 2.3ms");
    }

    [Fact]
    public async Task ExecuteAsync_Includes_Stderr()
    {
        var mock = new Mock<SandboxClient>(MockBehavior.Strict, Mock.Of<HttpClient>(), Options.Create(new SandboxOptions()));
        mock.Setup(x => x.ExecuteAsync(It.IsAny<SandboxRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SandboxResult("", "Traceback...", 1, 0.5));

        var tool = new ExecutePythonTool(mock.Object);
        var result = await tool.ExecuteAsync(new ExecutePythonArgs("1/0"), null, Guid.NewGuid(), CancellationToken.None);

        result.Should().Contain("Traceback");
        result.Should().Contain("Exit code: 1");
    }
}
```

### Phase 6 — Hybrid Library Mechanism

The `/app/libs` directory mounted into the sandbox at `/app/libs` (read-only) is added to `sys.path` by the runner before execution. This allows the LLM to import custom modules that have been placed there.

**How modules are added:**

The `.gitkeep` placeholder is removed from `sandbox/libs/`. A new API endpoint or management command allows approved scripts to be saved there. For v1, the process is manual:
1. Developer or trusted user writes a Python module (e.g., `markdown_tools.py`)
2. Saves it to `./sandbox/libs/` on the host
3. The next sandbox execution can `import markdown_tools`

**Future extension (post-v1):**
A tool `save_python_module(name, code)` that the LLM can use to save frequently-used utility scripts to the libs directory. The API would write to `sandbox/libs/` on the host (writable on the host, read-only inside the sandbox).

## Edge Cases

| Scenario | Behavior |
|---|---|
| Code times out (>5s) | `subprocess.TimeoutExpired` caught, returns stderr with "Execution timed out after 5s", exit_code = -1 |
| Code imports blocked module (os, socket) | ImportHook raises `ImportError: module 'os' is not allowed in the sandbox` |
| Code prints huge output (100K+ lines) | `stdout` truncated at 10KB, remainder in `stderr` with truncation note |
| Code enters infinite loop | Process killed by timeout guard, partial output returned |
| Code forks (os.fork) | Blocked by module allowlist; if somehow executed, cgroup `pids_limit` caps at 64 PIDs |
| Code writes to disk | Non-root user + read-only libs mount + temp dir cleaned up; writes to `/tmp` are ephemeral |
| Code allocates too much memory | cgroup `memory: 128M` triggers OOM kill, exit_code = -9 |
| Syntax error in code | Python syntax error returned in stderr, exit_code = 1 |
| Sandbox container is down | `SandboxClient` throws `HttpRequestException`, tool returns error to LLM which can retry |

## What Stays In Scope

| Item | In v1? |
|---|---|
| Text stdout/stderr capture | Yes |
| Module allowlist (os/socket/subprocess blocked) | Yes |
| Container air-gap (`internal: true` network) | Yes |
| Resource limits (0.5 CPU, 128MB RAM, 5s timeout) | Yes |
| Non-root user in container | Yes |
| Hybrid library volume (read-only) | Yes |
| ExecutePythonTool for chat | Yes |
| Unit tests for C# client + tool | Yes |
| FastAPI server unit tests | Yes |
| Plot/image output (base64) | No (v2) |
| `save_python_module` tool | No (v2) |
| Frontend "Run Python" panel | No (v2) |
| Persistent venv per chat session | No |
| npm/pip install from within sandbox | No (air-gapped by design) |