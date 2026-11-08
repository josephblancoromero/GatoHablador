namespace GatoHablador.Core;

public interface ITranslator
{
    ValueTask<TranslationResult> TranslateAsync(string text, CancellationToken cancellationToken = default);
}

public sealed record TranslationResult(string Text, bool WasTranslated, string Engine);

public sealed class GlossaryTranslator : ITranslator
{
    private static readonly Dictionary<string, string> Phrases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["hello"] = "hola",
        ["hi"] = "hola",
        ["thanks"] = "gracias",
        ["thank you"] = "gracias",
        ["you're welcome"] = "de nada",
        ["need healer"] = "necesitamos sanador",
        ["need tank"] = "necesitamos tanque",
        ["need dps"] = "necesitamos DPS",
        ["pull the boss"] = "atrae al jefe",
        ["wait"] = "espera",
        ["ready"] = "listo",
        ["are you ready?"] = "¿estás listo?",
        ["good game"] = "buena partida",
        ["invite me"] = "invítame",
        ["where are you?"] = "¿dónde estás?",
        ["follow me"] = "sígueme",
        ["help"] = "ayuda",
    };

    public ValueTask<TranslationResult> TranslateAsync(string text, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalized = text.Trim();
        if (Phrases.TryGetValue(normalized, out var translation))
        {
            return ValueTask.FromResult(new TranslationResult(translation, true, "glosario-local"));
        }

        return ValueTask.FromResult(new TranslationResult(text, false, "glosario-local"));
    }
}

