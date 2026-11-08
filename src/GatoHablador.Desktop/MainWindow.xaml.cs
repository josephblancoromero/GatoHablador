using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using GatoHablador.Core;

namespace GatoHablador.Desktop;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _timer;
    private readonly MessageAssembler _assembler = new();
    private readonly ITranslator _translator = new GlossaryTranslator();
    private bool _busy;
    private CaptureSettings _settings = new(0, 0, 2);

    public MainWindow()
    {
        InitializeComponent();
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

        _assembler.Reset();
        _timer.Start();
        StartButton.IsEnabled = false;
        StopButton.IsEnabled = true;
        StatusText.Text = "Buscando World of Warcraft…";
    }

    private void StopButton_Click(object sender, RoutedEventArgs e) => StopCapture();

    private void ClearButton_Click(object sender, RoutedEventArgs e) => Messages.Clear();

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

            var translated = await _translator.TranslateAsync(message.Text);
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

            StatusText.Text = translated.WasTranslated
                ? "Mensaje traducido"
                : "Mensaje recibido · sin coincidencia en el glosario";
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
        base.OnClosed(e);
    }
}

public sealed record ChatRow(string Time, string Author, string Original, string Translation, string Engine);

