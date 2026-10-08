#nullable enable
using eLotto.Core.Models;

namespace eLotto.Core.Services;

public interface ISorteoTransparencyFileStore
{
    bool Exists(Sorteos sorteo);
    Task<byte[]?> ReadAsync(Sorteos sorteo, CancellationToken cancellationToken);
    Task WriteOnceAsync(Sorteos sorteo, byte[] pdf, CancellationToken cancellationToken);
}

public sealed class SorteoTransparencyFileStore : ISorteoTransparencyFileStore
{
    public static string DefaultDirectory => Path.Combine(AppContext.BaseDirectory, "transparencia");

    public static string ResolveDirectory(string contentRoot, string? webRoot, string? configuredPath)
    {
        var root = Path.GetFullPath(contentRoot);
        var directory = Path.GetFullPath(string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(root, "App_Data", "transparencia")
            : Path.IsPathRooted(configuredPath) ? configuredPath : Path.Combine(root, configuredPath));
        var publicRoot = Path.GetFullPath(string.IsNullOrWhiteSpace(webRoot)
            ? Path.Combine(root, "wwwroot") : webRoot);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (directory.Equals(publicRoot, comparison) ||
            directory.StartsWith(Path.TrimEndingDirectorySeparator(publicRoot) + Path.DirectorySeparatorChar, comparison))
            throw new InvalidOperationException("El directorio de transparencia no puede estar dentro de wwwroot.");
        return directory;
    }

    private readonly string _directory;

    public SorteoTransparencyFileStore(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("La ruta de transparencia es obligatoria.", nameof(directory));
        _directory = Path.GetFullPath(directory);
    }

    public bool Exists(Sorteos sorteo)
    {
        var path = PathFor(sorteo);
        if (!File.Exists(path)) return false;
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        Span<byte> header = stackalloc byte[5];
        if (stream.Length < header.Length)
            throw new InvalidOperationException("El documento de transparencia almacenado no es un PDF válido.");
        stream.ReadExactly(header);
        if (!HasPdfHeader(header))
            throw new InvalidOperationException("El documento de transparencia almacenado no es un PDF válido.");
        return true;
    }

    public async Task<byte[]?> ReadAsync(Sorteos sorteo, CancellationToken cancellationToken)
    {
        var path = PathFor(sorteo);
        if (!File.Exists(path)) return null;
        var pdf = await File.ReadAllBytesAsync(path, cancellationToken);
        if (!HasPdfHeader(pdf))
            throw new InvalidOperationException("El documento de transparencia almacenado no es un PDF válido.");
        return pdf;
    }

    public async Task WriteOnceAsync(Sorteos sorteo, byte[] pdf, CancellationToken cancellationToken)
    {
        if (!HasPdfHeader(pdf))
            throw new InvalidOperationException("No se puede publicar un PDF de transparencia inválido.");

        Directory.CreateDirectory(_directory);
        var target = PathFor(sorteo);
        if (Exists(sorteo)) return;
        var temporary = Path.Combine(_directory, $".{sorteo.Id}-{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                81920, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(pdf, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
            try { File.Move(temporary, target); }
            catch (IOException) when (Exists(sorteo)) { }
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public string PathFor(Sorteos sorteo)
    {
        ArgumentNullException.ThrowIfNull(sorteo);
        return Path.Combine(_directory, $"{sorteo.Id}-{sorteo.Fecha.Ticks}.pdf");
    }

    private static bool HasPdfHeader(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 5 && bytes[0] == '%' && bytes[1] == 'P' && bytes[2] == 'D' &&
        bytes[3] == 'F' && bytes[4] == '-';
}
