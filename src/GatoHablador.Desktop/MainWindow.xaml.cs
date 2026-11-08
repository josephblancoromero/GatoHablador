using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using GatoHablador.Core;

namespace GatoHablador.Desktop;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _timer;
    private readonly MessageAssembler _assembler = new();
    private readonly GlossaryTranslator _glossary = new();
    private readonly HttpClient _httpClient = new(new HttpClientHandler
    {
        AllowAutoRedirect = false,
    })
    {
        Timeout = TimeSpan.FromSeconds(90),
    };
    private ITranslator _translator;
    private bool _busy;
    private CaptureSettings _settings = new(0, 0, 2);

    public MainWindow()
    {
        InitializeComponent();
        _translator = _glossary;
        DataContext = this;
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(33),
        };
        _timer.Tick += Timer_Tick;
    }

    public ObservableCollection<ChatRow> Messages { get; } = [];

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadSettings(out _settings))
        {
            MessageBox.Show(this, "Usa números enteros. El tamaño de celda debe estar entre 1 y 12.",
                "Configuración inválida", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            _translator = CreateSelectedTranslator();
        }
        catch (ArgumentException exception)
        {
            MessageBox.Show(this, exception.Message, "Modelo inválido", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _assembler.Reset();
        _timer.Start();
        StartButton.IsEnabled = false;
        StopButton.IsEnabled = true;
        StatusText.Text = "Buscando World of Warcraft…";
    }

    private void StopButton_Click(object sender, RoutedEventArgs e) => StopCapture();

    private void ClearButton_Click(object sender, RoutedEventArgs e) => Messages.Clear();

    private async void TestOllamaButton_Click(object sender, RoutedEventArgs e)
    {
        TestOllamaButton.IsEnabled = false;
        StatusText.Text = "Probando Ollama local…";
        try
        {
            var translator = new OllamaTranslator(ModelBox.Text, _httpClient);
            var result = await translator.TranslateAsync("Hello! Ready for the dungeon?");
            StatusText.Text = $"Ollama listo · prueba: {result.Text}";
        }
        catch (TaskCanceledException)
        {
            StatusText.Text = "Ollama no respondió a tiempo. Verifica que esté ejecutándose.";
        }
        catch (HttpRequestException)
        {
            StatusText.Text = "Ollama no responde en 127.0.0.1:11434.";
        }
        catch (Exception exception)
        {
            StatusText.Text = $"Ollama no disponible: {exception.Message}";
        }
        finally
        {
            TestOllamaButton.IsEnabled = true;
        }
    }

    private async void Timer_Tick(object? sender, EventArgs e)
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        try
        {
            var capture = BridgeCapture.TryCapture(_settings);
            if (!capture.Success)
            {
                StatusText.Text = capture.Error ?? "No se pudo capturar el puente.";
                return;
            }

            StatusText.Text = "Puente localizado · esperando mensajes";
            var frame = BridgeProtocol.BitsToFrame(capture.Bits!);
            if (!BridgeProtocol.TryDecode(frame, out var chunk) || !_assembler.TryAdd(chunk, out var payload))
            {
                return;
            }

            if (!BridgeProtocol.TryParseMessage(payload, out var message))
            {
                StatusText.Text = "Mensaje descartado: formato inválido";
                return;
            }

            TranslationResult translated;
            string? translationWarning = null;
            try
            {
                translated = await _translator.TranslateAsync(message.Text);
            }
            catch (Exception exception) when (_translator is OllamaTranslator
                && exception is not OperationCanceledException)
            {
                translated = await _glossary.TranslateAsync(message.Text);
                translationWarning = "Ollama falló; se usó el glosario local";
            }
            Messages.Insert(0, new ChatRow(
                message.ReceivedAt.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                message.Author,
                message.Text,
                translated.Text,
                translated.Engine));
            while (Messages.Count > 200)
            {
                Messages.RemoveAt(Messages.Count - 1);
            }

            StatusText.Text = translationWarning ?? (translated.WasTranslated
                ? "Mensaje traducido"
                : "Mensaje recibido · sin coincidencia en el glosario");
        }
        catch (Exception exception)
        {
            StatusText.Text = $"Error: {exception.Message}";
        }
        finally
        {
            _busy = false;
        }
    }

    private bool TryReadSettings(out CaptureSettings settings)
    {
        settings = new CaptureSettings(0, 0, 2);
        if (!int.TryParse(OffsetXBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var x)
            || !int.TryParse(OffsetYBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var y)
            || !int.TryParse(CellSizeBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var size)
            || size is < 1 or > 12)
        {
            return false;
        }

        settings = new CaptureSettings(x, y, size);
        return true;
    }

    private ITranslator CreateSelectedTranslator() => TranslatorBox.SelectedIndex == 0
        ? new OllamaTranslator(ModelBox.Text, _httpClient)
        : _glossary;

    private void StopCapture()
    {
        _timer.Stop();
        StartButton.IsEnabled = true;
        StopButton.IsEnabled = false;
        StatusText.Text = "Detenido";
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        _httpClient.Dispose();
        base.OnClosed(e);
    }
}

public sealed record ChatRow(string Time, string Author, string Original, string Translation, string Engine);

