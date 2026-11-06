using System.Net;
using System.Text;
using System.Text.Json;
using Localizer.Application.Drafting;
using Localizer.Infrastructure.Drafting;

namespace Localizer.Infrastructure.Tests.Drafting;

public sealed class OllamaTranslationDraftProviderTests
{
    private const string ChatAndEmbeddingTags =
        """
        {"models":[
          {"name":"nomic-embed-text:latest","capabilities":["embedding"]},
          {"name":"qwen3.5:4b","capabilities":["completion","vision","tools","thinking"]}
        ]}
        """;

    private const string DraftReply =
        """{"message":{"role":"assistant","content":"{\"text\":\"Empezar\"}"}}""";

    [TestCase(null)]
    [TestCase("")]
    [TestCase("  ")]
    public void ResolveModelName_UsesSoleChatModelWhenNoneConfigured(string? configured)
    {
        var resolved = OllamaTranslationDraftProvider.ResolveModelName(configured, ["qwen3.5:4b"]);

        Assert.That(resolved, Is.EqualTo("qwen3.5:4b"));
    }

    [Test]
    public void ResolveModelName_FailsWhenNoneConfiguredAmongMany()
    {
        Assert.That(
            () => OllamaTranslationDraftProvider.ResolveModelName(null, ["qwen3.5:4b", "mistral:latest"]),
            Throws.InvalidOperationException.With.Message.Contains("LOCALIZER_OLLAMA_MODEL")
                .And.Message.Contains("qwen3.5:4b, mistral:latest"));
    }

    [Test]
    public void ResolveModelName_FailsWhenNoChatModels()
    {
        Assert.That(
            () => OllamaTranslationDraftProvider.ResolveModelName("qwen3.5:4b", []),
            Throws.InvalidOperationException.With.Message.Contains("No chat-capable"));
    }

    [Test]
    public void ResolveModelName_ResolvesUntaggedNameToLatest()
    {
        var resolved = OllamaTranslationDraftProvider.ResolveModelName(
            "llama3.2",
            ["llama3.2:1b", "qwen3.5:4b", "llama3.2:latest"]);

        Assert.That(resolved, Is.EqualTo("llama3.2:latest"));
    }

    [Test]
    public void ResolveModelName_ResolvesUntaggedNameToSoleTag()
    {
        var resolved = OllamaTranslationDraftProvider.ResolveModelName("qwen3.5", ["qwen3.5:4b", "mistral:latest"]);

        Assert.That(resolved, Is.EqualTo("qwen3.5:4b"));
    }

    [Test]
    public void ResolveModelName_FailsWhenUntaggedNameIsAmbiguous()
    {
        Assert.That(
            () => OllamaTranslationDraftProvider.ResolveModelName("qwen3.5", ["qwen3.5:4b", "qwen3.5:9b"]),
            Throws.InvalidOperationException.With.Message.Contains("LOCALIZER_OLLAMA_MODEL"));
    }

    [Test]
    public void ResolveModelName_DoesNotSubstituteAnotherTag()
    {
        Assert.That(
            () => OllamaTranslationDraftProvider.ResolveModelName("qwen3.5:9b", ["qwen3.5:4b"]),
            Throws.InvalidOperationException.With.Message.Contains("qwen3.5:9b"));
    }

    [Test]
    public void ResolveModelName_DoesNotSubstituteSoleModelForConfiguredModel()
    {
        Assert.That(
            () => OllamaTranslationDraftProvider.ResolveModelName("llama3.2", ["qwen3.5:4b"]),
            Throws.InvalidOperationException.With.Message.Contains("llama3.2"));
    }

    [Test]
    public async Task DraftAsync_SkipsEmbeddingModelsAndSendsChatRequest()
    {
        using var handler = new FakeOllamaHandler(ChatAndEmbeddingTags, _ => Reply(HttpStatusCode.OK, DraftReply));
        var progress = new RecordingProgress();

        var result = await CreateProvider(handler).DraftAsync(CreateBatch(CreateItem()), progress);

        Assert.That(result.ProviderName, Is.EqualTo(OllamaTranslationDraftProvider.ProviderName));
        Assert.That(result.ModelName, Is.EqualTo("qwen3.5:4b"));
        Assert.That(result.Items.Single().Text, Is.EqualTo("Empezar"));
        Assert.That(result.ItemFailures, Is.Empty);
        Assert.That(progress.Completed, Is.EqualTo(new[] { 0, 1 }));

        using var chat = JsonDocument.Parse(handler.ChatBodies.Single());
        Assert.That(chat.RootElement.GetProperty("model").GetString(), Is.EqualTo("qwen3.5:4b"));
        Assert.That(chat.RootElement.GetProperty("format").GetString(), Is.EqualTo("json"));
        Assert.That(chat.RootElement.GetProperty("stream").GetBoolean(), Is.False);
        Assert.That(chat.RootElement.GetProperty("think").GetBoolean(), Is.False);
    }

    [Test]
    public async Task DraftAsync_TreatsModelsWithoutCapabilitiesAsChatCapable()
    {
        using var handler = new FakeOllamaHandler(
            """{"models":[{"name":"mistral:latest"}]}""",
            _ => Reply(HttpStatusCode.OK, DraftReply));

        var result = await CreateProvider(handler).DraftAsync(CreateBatch(CreateItem()));

        Assert.That(result.ModelName, Is.EqualTo("mistral:latest"));
    }

    [Test]
    public void DraftAsync_RejectsConfiguredEmbeddingModel()
    {
        using var handler = new FakeOllamaHandler(ChatAndEmbeddingTags, _ => Reply(HttpStatusCode.OK, DraftReply));

        Assert.That(
            () => CreateProvider(handler, "nomic-embed-text").DraftAsync(CreateBatch(CreateItem())),
            Throws.InvalidOperationException.With.Message.Contains("Available: qwen3.5:4b"));
        Assert.That(handler.ChatBodies, Is.Empty);
    }

    [Test]
    public void DraftAsync_ReportsWhenOnlyEmbeddingModelsAreInstalled()
    {
        using var handler = new FakeOllamaHandler(
            """{"models":[{"name":"nomic-embed-text:latest","capabilities":["embedding"]}]}""",
            _ => Reply(HttpStatusCode.OK, DraftReply));

        Assert.That(
            () => CreateProvider(handler).DraftAsync(CreateBatch(CreateItem())),
            Throws.InvalidOperationException.With.Message.Contains("No chat-capable"));
    }

    [Test]
    public void DraftAsync_ReportsUnreachableServer()
    {
        using var handler = new FakeOllamaHandler(new HttpRequestException("connection refused"));

        Assert.That(
            () => CreateProvider(handler).DraftAsync(CreateBatch(CreateItem())),
            Throws.InvalidOperationException.With.Message.Contains("Could not reach Ollama at http://ollama.test/")
                .And.Message.Contains("LOCALIZER_OLLAMA_URL"));
    }

    [Test]
    public void DraftAsync_ReportsModelListHttpFailure()
    {
        using var handler = new FakeOllamaHandler(HttpStatusCode.InternalServerError, "boom");

        Assert.That(
            () => CreateProvider(handler).DraftAsync(CreateBatch(CreateItem())),
            Throws.InvalidOperationException.With.Message.Contains("500"));
    }

    [Test]
    public async Task DraftAsync_KeepsSuccessfulItemsWhenOneFails()
    {
        using var handler = new FakeOllamaHandler(
            ChatAndEmbeddingTags,
            body => body.Contains("ui.bad", StringComparison.Ordinal)
                ? Reply(HttpStatusCode.InternalServerError, "boom")
                : Reply(HttpStatusCode.OK, DraftReply));
        var progress = new RecordingProgress();

        var result = await CreateProvider(handler).DraftAsync(
            CreateBatch(CreateItem(), CreateItem("ui.bad")),
            progress);

        Assert.That(result.Items.Select(static item => item.Key), Is.EqualTo(new[] { "ui.start" }));
        Assert.That(result.ItemFailures.Single(), Does.StartWith("ui.bad (es):"));
        Assert.That(progress.Completed, Is.EqualTo(new[] { 0, 1, 2 }));
    }

    [Test]
    public void DraftAsync_FailsWhenEveryItemFails()
    {
        using var handler = new FakeOllamaHandler(
            ChatAndEmbeddingTags,
            _ => Reply(HttpStatusCode.InternalServerError, "boom"));

        Assert.That(
            () => CreateProvider(handler).DraftAsync(CreateBatch(CreateItem())),
            Throws.InvalidOperationException.With.Message.Contains("All draft requests failed"));
    }

    [Test]
    public void DraftAsync_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        using var handler = new FakeOllamaHandler(ChatAndEmbeddingTags, _ =>
        {
            cancellation.Cancel();
            return Reply(HttpStatusCode.OK, DraftReply);
        });

        Assert.That(
            () => CreateProvider(handler).DraftAsync(
                CreateBatch(CreateItem(), CreateItem("ui.next")),
                cancellationToken: cancellation.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public void BuildUserPrompt_IncludesNotesContextConstraintsAndSyntax()
    {
        var prompt = OllamaTranslationDraftProvider.BuildUserPrompt(new TranslationDraftItemRequest
        {
            Key = "ui.coins",
            SourceLocale = "en",
            TargetLocale = "es",
            SourceText = "You have {0} coins",
            DeveloperNotes = "Keep the number placeholder.",
            Context = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["screen"] = "hud",
                ["tone"] = "neutral"
            },
            Constraints = new DraftConstraintHints
            {
                MaxGraphemes = 40,
                RequiredTerms = ["Steam"],
                ForbiddenTerms = ["beta"]
            },
            SyntaxProfile = "composite"
        });

        Assert.That(prompt, Does.Contain("Translate from en to es."));
        Assert.That(prompt, Does.Contain("Key: ui.coins"));
        Assert.That(prompt, Does.Contain("Syntax profile: composite"));
        Assert.That(prompt, Does.Contain("You have {0} coins"));
        Assert.That(prompt, Does.Contain("Developer notes:"));
        Assert.That(prompt, Does.Contain("Keep the number placeholder."));
        Assert.That(prompt, Does.Contain("- screen: hud"));
        Assert.That(prompt, Does.Contain("- tone: neutral"));
        Assert.That(prompt, Does.Contain("- Max graphemes: 40"));
        Assert.That(prompt, Does.Contain("- Required terms: Steam"));
        Assert.That(prompt, Does.Contain("- Forbidden terms: beta"));
    }

    [Test]
    public void ParseDraftContent_AcceptsMatchingJson()
    {
        var item = CreateItem();
        var result = OllamaTranslationDraftProvider.ParseDraftContent(
            item,
            """{"key":"ui.start","locale":"es","text":"Empezar"}""");

        Assert.That(result.Key, Is.EqualTo("ui.start"));
        Assert.That(result.TargetLocale, Is.EqualTo("es"));
        Assert.That(result.Text, Is.EqualTo("Empezar"));
    }

    [Test]
    public void ParseDraftContent_UsesRequestedIdentityWhenKeyLocaleOmitted()
    {
        var item = CreateItem();
        var result = OllamaTranslationDraftProvider.ParseDraftContent(item, """{"text":"Empezar"}""");

        Assert.That(result.Key, Is.EqualTo("ui.start"));
        Assert.That(result.TargetLocale, Is.EqualTo("es"));
        Assert.That(result.Text, Is.EqualTo("Empezar"));
    }

    [Test]
    public void ParseDraftContent_RejectsMismatchedKey()
    {
        var item = CreateItem();

        Assert.That(
            () => OllamaTranslationDraftProvider.ParseDraftContent(
                item,
                """{"key":"other","locale":"es","text":"Empezar"}"""),
            Throws.InvalidOperationException);
    }

    [Test]
    public void ParseDraftContent_ExtractsJsonFromSurroundingText()
    {
        var item = CreateItem();
        var result = OllamaTranslationDraftProvider.ParseDraftContent(
            item,
            "Here you go:\n{\"text\":\"Empezar\"}\n");

        Assert.That(result.Text, Is.EqualTo("Empezar"));
    }

    private static TranslationDraftItemRequest CreateItem(string key = "ui.start") =>
        new()
        {
            Key = key,
            SourceLocale = "en",
            TargetLocale = "es",
            SourceText = "Start",
            SyntaxProfile = "plain"
        };

    private static TranslationDraftBatchRequest CreateBatch(params TranslationDraftItemRequest[] items) =>
        new() { Items = items };

    private static OllamaTranslationDraftProvider CreateProvider(HttpMessageHandler handler, string? model = null)
    {
        var options = new OllamaDraftProviderOptions
        {
            BaseAddress = new Uri("http://ollama.test/"),
            ModelName = model
        };

        return new OllamaTranslationDraftProvider(
            new HttpClient(handler, disposeHandler: false) { BaseAddress = options.BaseAddress },
            options);
    }

    private static HttpResponseMessage Reply(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class RecordingProgress : IProgress<TranslationDraftProgress>
    {
        public List<int> Completed { get; } = [];

        public void Report(TranslationDraftProgress value) => Completed.Add(value.Completed);
    }

    private sealed class FakeOllamaHandler : HttpMessageHandler
    {
        private readonly Func<HttpResponseMessage> _tags;
        private readonly Func<string, HttpResponseMessage> _chat;

        public FakeOllamaHandler(string tagsJson, Func<string, HttpResponseMessage> chat)
        {
            _tags = () => Reply(HttpStatusCode.OK, tagsJson);
            _chat = chat;
        }

        public FakeOllamaHandler(HttpStatusCode tagsStatus, string tagsBody)
        {
            _tags = () => Reply(tagsStatus, tagsBody);
            _chat = static _ => throw new InvalidOperationException("Chat was not expected.");
        }

        public FakeOllamaHandler(HttpRequestException tagsFailure)
        {
            _tags = () => throw tagsFailure;
            _chat = static _ => throw new InvalidOperationException("Chat was not expected.");
        }

        public List<string> ChatBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/api/tags")
            {
                return _tags();
            }

            Assert.That(path, Is.EqualTo("/api/chat"));
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            ChatBodies.Add(body);
            return _chat(body);
        }
    }
}
