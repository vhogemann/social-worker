using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Chat.Adapters;
using SocialWorker.Api.Features.Chat.Models;
using SocialWorker.Api.Features.ReplyAgents.Models;
using SocialWorker.Api.Features.ReplyAgents.Services;
using SocialWorker.Api.Infrastructure.Llm;
using SocialWorker.Api.Infrastructure.Search;
using SocialWorker.Api.Features.Sources;
using Xunit;

namespace SocialWorker.Api.Tests.Features.ReplyAgents;

public sealed class ReplyGenerationServiceTests : SqliteTestBase
{
    private sealed class FakeLlmAdapter : ILlmProviderAdapter
    {
        private readonly Queue<OpenAiModels.ChatCompletionResponse> _responses = new();

        public void Enqueue(OpenAiModels.ChatCompletionResponse response)
        {
            _responses.Enqueue(response);
        }

        public IAsyncEnumerable<OpenAiModels.StreamChunk> CompleteStreamAsync(
            OpenAiModels.ChatCompletionRequest request, LlmCredentials credentials, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public Task<OpenAiModels.ChatCompletionResponse?> CompleteAsync(
            OpenAiModels.ChatCompletionRequest request, LlmCredentials credentials, CancellationToken ct)
        {
            if (_responses.Count == 0)
            {
                return Task.FromResult<OpenAiModels.ChatCompletionResponse?>(null);
            }

            return Task.FromResult<OpenAiModels.ChatCompletionResponse?>(_responses.Dequeue());
        }
    }

    private sealed class FakeSearchEngine : ISearchEngine
    {
        public List<string> QueriedTerms { get; } = new();
        public List<SearchResult> ResultsToReturn { get; set; } = new();

        public Task<List<SearchResult>> SearchAsync(string query, CancellationToken ct)
        {
            QueriedTerms.Add(query);
            return Task.FromResult(ResultsToReturn);
        }
    }

    private static PlatformCommentItem CreateComment(string id, string text, string platform = "Bluesky") =>
        new(platform, id, null, "root-1", "user1", "did:user1", text, DateTime.UtcNow, 0, Array.Empty<string>(), Array.Empty<PlatformCommentImageAttachment>());

    private static EnrichedCommentContext CreateContext(PlatformCommentItem comment) =>
        new(comment, Array.Empty<ParsedLinkContext>(), Array.Empty<ParsedImageContext>(), comment.Text);

    [Fact]
    public async Task GenerateReplyAsync_DirectReply_ReturnsSucceeded()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        db.LlmProviders.Add(LlmProvider.Create("TestProvider", "OpenRouter", "https://api.openai.com/v1", "gpt-4o", "dummy-key", isDefault: true));
        await db.SaveChangesAsync();

        var adapter = new FakeLlmAdapter();
        adapter.Enqueue(new OpenAiModels.ChatCompletionResponse
        {
            Choices = new List<OpenAiModels.ChatCompletionChoice>
            {
                new()
                {
                    Message = new OpenAiModels.ChatCompletionMessage
                    {
                        Role = "assistant",
                        Content = "Os dados do IBGE e do Ministério da Fazenda comprovam a criação de 2 milhões de vagas."
                    }
                }
            }
        });

        var providerService = new LlmProviderService();
        var service = new ReplyGenerationService(db, providerService, adapter, NullLogger<ReplyGenerationService>.Instance);

        var comment = CreateComment("c1", "Isso e mentira, o desemprego so aumentou!");
        var enrichedContext = CreateContext(comment);

        var result = await service.GenerateReplyAsync(
            enrichedContext,
            "Criamos 2 milhoes de empregos formais este ano.",
            "Defenda os fatos com firmeza.",
            "Bluesky",
            null,
            user);

        Assert.True(result.Success);
        Assert.Equal("Os dados do IBGE e do Ministério da Fazenda comprovam a criação de 2 milhões de vagas.", result.ReplyText);
    }

    [Fact]
    public async Task GenerateReplyAsync_WithSearchEngine_ExecutesWebSearchAndReturnsFinalReply()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        db.LlmProviders.Add(LlmProvider.Create("TestProvider", "OpenRouter", "https://api.openai.com/v1", "gpt-4o", "dummy-key", isDefault: true));
        await db.SaveChangesAsync();

        var searchEngine = new FakeSearchEngine();
        searchEngine.ResultsToReturn = new List<SearchResult>
        {
            new("CAGED 2025: Brasil cria recorde de postos de trabalho", "https://gov.br/trabalho/dados", "Dados oficiais mostram saldo positivo expressivo.")
        };

        var adapter = new FakeLlmAdapter();
        adapter.Enqueue(new OpenAiModels.ChatCompletionResponse
        {
            Choices = new List<OpenAiModels.ChatCompletionChoice>
            {
                new()
                {
                    Message = new OpenAiModels.ChatCompletionMessage
                    {
                        Role = "assistant",
                        ToolCalls = new List<OpenAiModels.OpenAiToolCall>
                        {
                            new()
                            {
                                Id = "call_search_1",
                                Function = new OpenAiModels.OpenAiToolCallFunction
                                {
                                    Name = "web_search",
                                    Arguments = JsonSerializer.Serialize(new { query = "dados oficiais caged vagas 2025" })
                                }
                            }
                        }
                    }
                }
            }
        });

        adapter.Enqueue(new OpenAiModels.ChatCompletionResponse
        {
            Choices = new List<OpenAiModels.ChatCompletionChoice>
            {
                new()
                {
                    Message = new OpenAiModels.ChatCompletionMessage
                    {
                        Role = "assistant",
                        Content = "Conforme o relatório oficial do CAGED, o saldo de empregos bateu recorde. Os dados são públicos e incontestáveis."
                    }
                }
            }
        });

        var providerService = new LlmProviderService();
        var service = new ReplyGenerationService(db, providerService, adapter, NullLogger<ReplyGenerationService>.Instance, searchEngine);

        var comment = CreateComment("c2", "Fonte: vozes da sua cabeca. Duvido provar.", "MetaThreads");
        var enrichedContext = CreateContext(comment);

        var result = await service.GenerateReplyAsync(
            enrichedContext,
            "O Brasil fechou o ano com recorde historico no CAGED.",
            "Defenda a tese rigorosamente com base em evidencias.",
            "MetaThreads",
            null,
            user);

        Assert.True(result.Success);
        Assert.Contains("CAGED", result.ReplyText);
        Assert.Single(searchEngine.QueriedTerms);
        Assert.Equal("dados oficiais caged vagas 2025", searchEngine.QueriedTerms[0]);
    }

    [Fact]
    public async Task GenerateReplyAsync_PlatformLimitTruncation_TruncatesWhenExceeded()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        db.LlmProviders.Add(LlmProvider.Create("TestProvider", "OpenRouter", "https://api.openai.com/v1", "gpt-4o", "dummy-key", isDefault: true));
        await db.SaveChangesAsync();

        var adapter = new FakeLlmAdapter();
        adapter.Enqueue(new OpenAiModels.ChatCompletionResponse
        {
            Choices = new List<OpenAiModels.ChatCompletionChoice>
            {
                new()
                {
                    Message = new OpenAiModels.ChatCompletionMessage
                    {
                        Role = "assistant",
                        Content = new string('A', 400)
                    }
                }
            }
        });

        var providerService = new LlmProviderService();
        var service = new ReplyGenerationService(db, providerService, adapter, NullLogger<ReplyGenerationService>.Instance);

        var comment = CreateComment("c3", "Discordo.");
        var enrichedContext = CreateContext(comment);

        var result = await service.GenerateReplyAsync(
            enrichedContext,
            "Post original.",
            "Persona.",
            "Bluesky",
            null,
            user);

        Assert.True(result.Success);
        Assert.Equal(300, result.ReplyText!.Length);
    }

    [Fact]
    public async Task GenerateReplyAsync_NoConfiguredProvider_ReturnsFailed()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);

        var adapter = new FakeLlmAdapter();
        var providerService = new LlmProviderService();
        var service = new ReplyGenerationService(db, providerService, adapter, NullLogger<ReplyGenerationService>.Instance);

        var comment = CreateComment("c4", "Comentario.");
        var enrichedContext = CreateContext(comment);

        var result = await service.GenerateReplyAsync(
            enrichedContext,
            "Post original.",
            "Persona.",
            "Bluesky",
            null,
            user);

        Assert.False(result.Success);
        Assert.Contains("No configured LLM provider", result.ErrorMessage);
    }

    private sealed class MockTranscriberForReply : ITranscriptExtractionService
    {
        public Task<bool> HealthAsync(CancellationToken ct) => Task.FromResult(true);

        public Task<TranscriptExtractionResult> ExtractAsync(string videoUrl, string outputPath, CancellationToken ct) =>
            Task.FromResult(new TranscriptExtractionResult(true, outputPath, 120.0, "pt", null));

        public Task<TranscriptDocument?> ReadTranscriptAsync(string transcriptPath, CancellationToken ct) =>
            Task.FromResult<TranscriptDocument?>(new TranscriptDocument("https://www.youtube.com/watch?v=xyz", "pt", 120.0, "O palestrante admite claramente que os dados foram tirados de contexto"));
    }

    [Fact]
    public async Task GenerateReplyAsync_WithChatToolExecutor_ExecutesVideoTranscriptionToolAndReturnsReply()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        db.LlmProviders.Add(LlmProvider.Create("TestProvider", "OpenRouter", "https://api.openai.com/v1", "gpt-4o", "dummy-key", isDefault: true));
        await db.SaveChangesAsync();

        var transcriber = new MockTranscriberForReply();
        var transcribeTool = new SocialWorker.Api.Features.Chat.Tools.TranscribeVideoTool(transcriber);
        var tools = new List<SocialWorker.Api.Features.Chat.Tools.IChatTool> { transcribeTool };
        var toolExecutor = new SocialWorker.Api.Features.Chat.Services.ChatToolExecutor(tools, NullLogger<SocialWorker.Api.Features.Chat.Services.ChatToolExecutor>.Instance);

        var adapter = new FakeLlmAdapter();
        adapter.Enqueue(new OpenAiModels.ChatCompletionResponse
        {
            Choices = new List<OpenAiModels.ChatCompletionChoice>
            {
                new()
                {
                    Message = new OpenAiModels.ChatCompletionMessage
                    {
                        Role = "assistant",
                        ToolCalls = new List<OpenAiModels.OpenAiToolCall>
                        {
                            new()
                            {
                                Id = "call_transcribe_1",
                                Function = new OpenAiModels.OpenAiToolCallFunction
                                {
                                    Name = "transcribe_video",
                                    Arguments = JsonSerializer.Serialize(new { url = "https://www.youtube.com/watch?v=xyz" })
                                }
                            }
                        }
                    }
                }
            }
        });

        adapter.Enqueue(new OpenAiModels.ChatCompletionResponse
        {
            Choices = new List<OpenAiModels.ChatCompletionChoice>
            {
                new()
                {
                    Message = new OpenAiModels.ChatCompletionMessage
                    {
                        Role = "assistant",
                        Content = "No próprio vídeo enviado, aos 2 minutos o autor admite que os números foram tirados de contexto. Fatos continuam fatos."
                    }
                }
            }
        });

        var providerService = new LlmProviderService();
        var service = new ReplyGenerationService(
            db, providerService, adapter, NullLogger<ReplyGenerationService>.Instance, null, toolExecutor, tools);

        var comment = CreateComment("c5", "Assista esse video e tente refutar: https://www.youtube.com/watch?v=xyz");
        var enrichedContext = CreateContext(comment);

        var result = await service.GenerateReplyAsync(
            enrichedContext,
            "Nossos dados são auditados e transparentes.",
            "Argument Winner: defenda a tese e desmonte objeções.",
            "Bluesky",
            null,
            user);

        Assert.True(result.Success);
        Assert.Contains("números foram tirados de contexto", result.ReplyText);
    }

    [Fact]
    public async Task GenerateReplyAsync_MultiTurnToolCalling_ForcesReplyOnFinalTurn()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        db.LlmProviders.Add(LlmProvider.Create("TestProvider", "OpenRouter", "https://api.openai.com/v1", "gpt-4o", "dummy-key", isDefault: true));
        await db.SaveChangesAsync();

        var searchEngine = new FakeSearchEngine
        {
            ResultsToReturn = new List<SearchResult>
            {
                new("Título 1", "https://example.com/1", "Dados de inflação e dívida pública controlados.")
            }
        };

        var adapter = new FakeLlmAdapter();
        for (var i = 0; i < 4; i++)
        {
            adapter.Enqueue(new OpenAiModels.ChatCompletionResponse
            {
                Choices = new List<OpenAiModels.ChatCompletionChoice>
                {
                    new()
                    {
                        Message = new OpenAiModels.ChatCompletionMessage
                        {
                            Role = "assistant",
                            ToolCalls = new List<OpenAiModels.OpenAiToolCall>
                            {
                                new()
                                {
                                    Id = $"call_search_{i}",
                                    Function = new OpenAiModels.OpenAiToolCallFunction
                                    {
                                        Name = "web_search",
                                        Arguments = JsonSerializer.Serialize(new { query = $"consulta {i}" })
                                    }
                                }
                            }
                        }
                    }
                }
            });
        }

        adapter.Enqueue(new OpenAiModels.ChatCompletionResponse
        {
            Choices = new List<OpenAiModels.ChatCompletionChoice>
            {
                new()
                {
                    Message = new OpenAiModels.ChatCompletionMessage
                    {
                        Role = "assistant",
                        Content = "A taxa de juros elevada decorre da política monetária do BC e a dívida pública é integralmente denominada em moeda soberana."
                    }
                }
            }
        });

        var providerService = new LlmProviderService();
        var service = new ReplyGenerationService(
            db, providerService, adapter, NullLogger<ReplyGenerationService>.Instance, searchEngine);

        var comment = CreateComment("c6", "Os bancos mandam no país e lucram 1 trilhão por ano!");
        var enrichedContext = CreateContext(comment);

        var result = await service.GenerateReplyAsync(
            enrichedContext,
            "Se a dívida pública fosse um problema real, os bancos parariam de comprar títulos.",
            "Argument Winner: defenda a tese com firmeza.",
            "MetaThreads",
            null,
            user);

        Assert.True(result.Success);
        Assert.NotNull(result.ReplyText);
        Assert.Contains("moeda soberana", result.ReplyText);
    }

    [Fact]
    public async Task GenerateReplyAsync_WhenLlmOutputsTextToolCalls_ParsesExecutesToolAndReturnsFinalReply()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        db.LlmProviders.Add(LlmProvider.Create("TestProvider", "OpenRouter", "https://api.openai.com/v1", "gpt-4o", "dummy-key", isDefault: true));
        await db.SaveChangesAsync();

        var searchEngine = new FakeSearchEngine();
        searchEngine.ResultsToReturn = new List<SearchResult>
        {
            new("Votação PEC 6x1", "https://senado.leg.br/noticias", "Senadores debatem PEC da jornada 6x1.")
        };

        var adapter = new FakeLlmAdapter();
        // LLM outputs raw XML-style <tool_call> in message Content without filling ToolCalls collection
        adapter.Enqueue(new OpenAiModels.ChatCompletionResponse
        {
            Choices = new List<OpenAiModels.ChatCompletionChoice>
            {
                new()
                {
                    Message = new OpenAiModels.ChatCompletionMessage
                    {
                        Role = "assistant",
                        Content = "<tool_call>web_search<arg_key>query</arg_key><arg_value>PEC do 6x1 Senado votação</arg_value></tool_call>"
                    }
                }
            }
        });

        // Next turn after tool execution: LLM outputs actual reply text
        adapter.Enqueue(new OpenAiModels.ChatCompletionResponse
        {
            Choices = new List<OpenAiModels.ChatCompletionChoice>
            {
                new()
                {
                    Message = new OpenAiModels.ChatCompletionMessage
                    {
                        Role = "assistant",
                        Content = "Companheiro, a tramitação da PEC do 6x1 segue em debate no Congresso e os registros são públicos no Senado."
                    }
                }
            }
        });

        var providerService = new LlmProviderService();
        var service = new ReplyGenerationService(
            db, providerService, adapter, NullLogger<ReplyGenerationService>.Instance, searchEngine);

        var comment = CreateComment("c7", "Flavio votou a favor da PEC do 6x1!", "MetaThreads");
        var enrichedContext = CreateContext(comment);

        var result = await service.GenerateReplyAsync(
            enrichedContext,
            "Post original sobre escala 6x1.",
            "Persona do Sentinela Vira-Voto.",
            "MetaThreads",
            null,
            user);

        Assert.True(result.Success);
        Assert.NotNull(result.ReplyText);
        Assert.DoesNotContain("<tool_call>", result.ReplyText);
        Assert.Contains("PEC do 6x1", result.ReplyText);
        Assert.Single(searchEngine.QueriedTerms);
        Assert.Equal("PEC do 6x1 Senado votação", searchEngine.QueriedTerms[0]);
    }

    [Fact]
    public async Task GenerateReplyAsync_WhenLlmOutputContainsToolCallTagsOnFinalTurn_FailsWithoutLeakingToolCallSyntax()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        db.LlmProviders.Add(LlmProvider.Create("TestProvider", "OpenRouter", "https://api.openai.com/v1", "gpt-4o", "dummy-key", isDefault: true));
        await db.SaveChangesAsync();

        var adapter = new FakeLlmAdapter();
        adapter.Enqueue(new OpenAiModels.ChatCompletionResponse
        {
            Choices = new List<OpenAiModels.ChatCompletionChoice>
            {
                new()
                {
                    Message = new OpenAiModels.ChatCompletionMessage
                    {
                        Role = "assistant",
                        Content = "<tool_call>web_search<arg_key>query</arg_key><arg_value>alguma busca</arg_value></tool_call>"
                    }
                }
            }
        });

        var providerService = new LlmProviderService();
        // Without search engine or tools configured to execute it, or if it leaks at the end
        var service = new ReplyGenerationService(
            db, providerService, adapter, NullLogger<ReplyGenerationService>.Instance);

        var comment = CreateComment("c8", "Pergunta qualquer.", "MetaThreads");
        var enrichedContext = CreateContext(comment);

        var result = await service.GenerateReplyAsync(
            enrichedContext,
            "Post original.",
            "Persona.",
            "MetaThreads",
            null,
            user);

        Assert.False(result.Success);
        Assert.Null(result.ReplyText);
    }
}

