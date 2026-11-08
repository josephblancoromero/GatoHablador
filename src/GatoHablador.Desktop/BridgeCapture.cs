using System.Runtime.InteropServices;
using GatoHablador.Core;

namespace GatoHablador.Desktop;

public sealed record CaptureSettings(int OffsetX, int OffsetY, int CellSize);

public sealed record CaptureResult(bool Success, bool[]? Bits = null, string? Error = null);

public static class BridgeCapture
{
    private const int DibRgbColors = 0;
    private const uint BiRgb = 0;
    private const uint SourceCopy = 0x00CC0020;
    private const uint CaptureLayeredWindows = 0x40000000;

    public static CaptureResult TryCapture(CaptureSettings settings)
    {
        if (!TryFindGameWindow(out var window))
        {
            return new CaptureResult(false, Error: "World of Warcraft no está visible.");
        }

        if (!GetClientRect(window, out var client) || !ClientToScreen(window, ref client.TopLeft))
        {
            return new CaptureResult(false, Error: "No se pudo localizar el área del juego.");
        }

        var width = BridgeProtocol.Columns * settings.CellSize;
        var height = BridgeProtocol.Rows * settings.CellSize;
        if (width <= 0 || height <= 0 || width > 2048 || height > 2048)
        {
            return new CaptureResult(false, Error: "La región configurada no es válida.");
        }

        try
        {
            var pixels = CaptureBgra(
                client.TopLeft.X + settings.OffsetX,
                client.TopLeft.Y + settings.OffsetY,
                width,
                height);
            var bits = new bool[BridgeProtocol.Columns * BridgeProtocol.Rows];
            for (var row = 0; row < BridgeProtocol.Rows; row++)
            {
                for (var column = 0; column < BridgeProtocol.Columns; column++)
                {
                    var x = (column * settings.CellSize) + (settings.CellSize / 2);
                    var y = (row * settings.CellSize) + (settings.CellSize / 2);
                    var offset = ((y * width) + x) * 4;
                    var blue = pixels[offset];
                    var green = pixels[offset + 1];
                    var red = pixels[offset + 2];
                    bits[(row * BridgeProtocol.Columns) + column] = red + green + blue >= 480;
                }
            }

            return new CaptureResult(true, bits);
        }
        catch (InvalidOperationException exception)
        {
            return new CaptureResult(false, Error: $"Captura no disponible: {exception.Message}");
        }
    }

    private static byte[] CaptureBgra(int sourceX, int sourceY, int width, int height)
    {
        var screen = GetDC(0);
        if (screen == 0)
        {
            throw new InvalidOperationException("GetDC falló.");
        }

        nint memory = 0;
        nint bitmap = 0;
        nint previous = 0;
        try
        {
            memory = CreateCompatibleDC(screen);
            bitmap = CreateCompatibleBitmap(screen, width, height);
            if (memory == 0 || bitmap == 0)
            {
                throw new InvalidOperationException("No se pudo preparar el búfer gráfico.");
            }

            previous = SelectObject(memory, bitmap);
            if (previous == 0 || !BitBlt(memory, 0, 0, width, height, screen, sourceX, sourceY,
                    SourceCopy | CaptureLayeredWindows))
            {
                throw new InvalidOperationException("No se pudo copiar la región del puente.");
            }

            var info = new BitmapInfo
            {
                Header = new BitmapInfoHeader
                {
                    Size = checked((uint)Marshal.SizeOf<BitmapInfoHeader>()),
                    Width = width,
                    Height = -height,
                    Planes = 1,
                    BitCount = 32,
                    Compression = BiRgb,
                },
            };
            var pixels = new byte[checked(width * height * 4)];
            if (GetDIBits(memory, bitmap, 0, checked((uint)height), pixels, ref info, DibRgbColors) == 0)
            {
                throw new InvalidOperationException("No se pudieron leer los píxeles capturados.");
            }

            return pixels;
        }
        finally
        {
            if (previous != 0 && memory != 0)
            {
                _ = SelectObject(memory, previous);
            }
            if (bitmap != 0)
            {
                _ = DeleteObject(bitmap);
            }
            if (memory != 0)
            {
                _ = DeleteDC(memory);
            }
            _ = ReleaseDC(0, screen);
        }
    }

    private static bool TryFindGameWindow(out nint result)
    {
        nint found = 0;
        EnumWindows((window, _) =>
        {
            if (!IsWindowVisible(window) || GetWindowTextLength(window) == 0)
            {
                return true;
            }

            var title = new char[GetWindowTextLength(window) + 1];
            _ = GetWindowText(window, title, title.Length);
            var value = new string(title).TrimEnd('\0');
            if (!value.Contains("World of Warcraft", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            found = window;
            return false;
        }, 0);
        result = found;
        return result != 0;
    }

    private delegate bool EnumWindowsCallback(nint window, nint parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsCallback callback, nint parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint window);

    [DllImport("user32.dll", EntryPoint = "GetWindowTextLengthW")]
    private static extern int GetWindowTextLength(nint window);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetWindowTextW")]
    private static extern int GetWindowText(nint window, [Out] char[] text, int maximum);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(nint window, out NativeRect rectangle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClientToScreen(nint window, ref NativePoint point);

    [DllImport("user32.dll")]
    private static extern nint GetDC(nint window);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(nint window, nint deviceContext);

    [DllImport("gdi32.dll")]
    private static extern nint CreateCompatibleDC(nint deviceContext);

    [DllImport("gdi32.dll")]
    private static extern nint CreateCompatibleBitmap(nint deviceContext, int width, int height);

    [DllImport("gdi32.dll")]
    private static extern nint SelectObject(nint deviceContext, nint graphicObject);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(nint graphicObject);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(nint deviceContext);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BitBlt(nint destination, int x, int y, int width, int height,
        nint source, int sourceX, int sourceY, uint operation);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(nint deviceContext, nint bitmap, uint firstScanLine, uint scanLineCount,
        [Out] byte[] pixels, ref BitmapInfo info, uint usage);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public NativePoint TopLeft;
        public NativePoint BottomRight;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint ImageSize;
        public int XPixelsPerMeter;
        public int YPixelsPerMeter;
        public uint ColorsUsed;
        public uint ColorsImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public BitmapInfoHeader Header;
        public uint Colors;
    }
}

