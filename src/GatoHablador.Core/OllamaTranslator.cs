using System.Net.Http.Json;
using System.Text.Json;

namespace GatoHablador.Core;

public sealed class OllamaTranslator : ITranslator
{
    public const string DefaultModel = "translategemma:4b";

    private static readonly Uri ChatEndpoint = new("http://127.0.0.1:11434/api/chat");
    private readonly HttpClient _httpClient;

    public OllamaTranslator(string model, HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        Model = ValidateModel(model);
        _httpClient = httpClient;
    }

    public string Model { get; }

    public async ValueTask<TranslationResult> TranslateAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (string.IsNullOrWhiteSpace(text))
        {
            return new TranslationResult(text, false, $"ollama:{Model}");
        }

        if (text.Length > 4096)
        {
            throw new ArgumentOutOfRangeException(nameof(text), "El texto supera el límite de 4096 caracteres.");
        }

        var body = new OllamaChatRequest(
            Model,
            [new OllamaMessage("user", BuildPrompt(text))],
            Stream: false,
            new OllamaOptions(0.1));

        using var response = await _httpClient.PostAsJsonAsync(ChatEndpoint, body, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Ollama respondió {(int)response.StatusCode}. Comprueba que el modelo '{Model}' esté instalado.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!document.RootElement.TryGetProperty("message", out var message)
            || !message.TryGetProperty("content", out var contentElement)
            || contentElement.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("Ollama devolvió una respuesta sin traducción.");
        }

        var translated = contentElement.GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(translated))
        {
            throw new InvalidOperationException("Ollama devolvió una traducción vacía.");
        }

        return new TranslationResult(translated, true, $"ollama:{Model}");
    }

    private static string BuildPrompt(string text) =>
        "You are a professional English (en) to Spanish (es) translator. "
        + "Your goal is to accurately convey the meaning and nuances of the original English text "
        + "while adhering to Spanish grammar, vocabulary, and cultural sensitivities. "
        + "Preserve player names, World of Warcraft item names, links, acronyms, and numbers. "
        + "Produce only the Spanish translation, without explanations or commentary. "
        + "Please translate the following English text into Spanish:\n\n\n"
        + text;

    private static string ValidateModel(string model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var value = model.Trim();
        if (value.Length is < 1 or > 128
            || value.Any(character => !char.IsAsciiLetterOrDigit(character)
                && character is not '.' and not '_' and not '-' and not ':' and not '/'))
        {
            throw new ArgumentException(
                "El nombre del modelo solo puede contener letras, números, punto, guion, guion bajo, dos puntos y barra.",
                nameof(model));
        }

        return value;
    }

    private sealed record OllamaChatRequest(
        string Model,
        IReadOnlyList<OllamaMessage> Messages,
        bool Stream,
        OllamaOptions Options);

    private sealed record OllamaMessage(string Role, string Content);

    private sealed record OllamaOptions(double Temperature);
}
