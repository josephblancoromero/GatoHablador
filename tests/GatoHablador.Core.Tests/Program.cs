using GatoHablador.Core;

var tests = new (string Name, Action Run)[]
{
    ("CRC-16 known vector", CrcKnownVector),
    ("Frame bit roundtrip", FrameBitRoundtrip),
    ("Chunked UTF-8 message", ChunkedMessage),
    ("Corrupt frame rejection", CorruptFrame),
    ("Local glossary", Glossary),
    ("Ollama loopback request", OllamaLoopback),
};

var failures = 0;
foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception exception)
    {
        failures++;
        Console.Error.WriteLine($"FAIL {test.Name}: {exception.Message}");
    }
}

return failures == 0 ? 0 : 1;

static void CrcKnownVector()
{
    var value = Crc16Ccitt.Compute("123456789"u8);
    Assert(value == 0x29B1, $"Expected 0x29B1, received 0x{value:X4}.");
}

static void FrameBitRoundtrip()
{
    var original = Enumerable.Range(0, BridgeProtocol.FrameSize).Select(index => (byte)index).ToArray();
    var decoded = BridgeProtocol.BitsToFrame(BridgeProtocol.FrameToBits(original));
    Assert(original.SequenceEqual(decoded), "Frame changed during the bit conversion.");
}

static void ChunkedMessage()
{
    var original = new ChatMessage("CHAT_MSG_PARTY", "Thrall-Area52", "Hello! Necesito ayuda con el jefe 🐉. " + new string('x', 90), DateTimeOffset.Now);
    var frames = BridgeProtocol.Packetize(42, BridgeProtocol.EncodeMessage(original));
    Assert(frames.Count > 1, "The fixture should span several frames.");

    var assembler = new MessageAssembler();
    byte[]? joined = null;
    foreach (var frame in frames.Reverse())
    {
        Assert(BridgeProtocol.TryDecode(frame, out var chunk), "A valid frame was rejected.");
        if (assembler.TryAdd(chunk, out var completed))
        {
            joined = completed;
        }
    }

    Assert(joined is not null, "The message was not assembled.");
    Assert(BridgeProtocol.TryParseMessage(joined, out var parsed), "The message payload was rejected.");
    Assert(parsed.Event == original.Event && parsed.Author == original.Author && parsed.Text == original.Text,
        "The parsed message does not match the source.");
}

static void CorruptFrame()
{
    var frame = BridgeProtocol.Packetize(1, "hello"u8)[0];
    frame[10] ^= 0x40;
    Assert(!BridgeProtocol.TryDecode(frame, out _), "CRC corruption was accepted.");
}

static void Glossary()
{
    var result = new GlossaryTranslator().TranslateAsync("Need healer").AsTask().GetAwaiter().GetResult();
    Assert(result.WasTranslated && result.Text == "necesitamos sanador", "Glossary translation failed.");
}

static void OllamaLoopback()
{
    var handler = new StubHandler(
        "{\"message\":{\"role\":\"assistant\",\"content\":\"¡Hola!\"},\"done\":true}");
    using var client = new HttpClient(handler);
    var result = new OllamaTranslator("translategemma:4b", client)
        .TranslateAsync("Hello!").AsTask().GetAwaiter().GetResult();

    Assert(result.WasTranslated && result.Text == "¡Hola!", "Ollama response was not parsed.");
    Assert(handler.RequestUri == new Uri("http://127.0.0.1:11434/api/chat"),
        "The translator did not use the fixed loopback endpoint.");
    Assert(handler.RequestBody?.Contains("translategemma:4b", StringComparison.Ordinal) == true,
        "The selected model was not sent.");
    Assert(handler.RequestBody?.Contains("Hello!", StringComparison.Ordinal) == true,
        "The source text was not sent.");
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

sealed class StubHandler(string responseBody) : HttpMessageHandler
{
    public Uri? RequestUri { get; private set; }

    public string? RequestBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        RequestUri = request.RequestUri;
        RequestBody = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);
        return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(responseBody, System.Text.Encoding.UTF8, "application/json"),
        };
    }
}

