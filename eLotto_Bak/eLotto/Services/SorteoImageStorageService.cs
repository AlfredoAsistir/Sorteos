namespace eLotto.Services
{
    public interface ISorteoImageStorageService
    {
        Task<string> SaveAsync(IFormFile image, CancellationToken cancellationToken);
        void Delete(string relativePath);
    }

    public class SorteoImageStorageService : ISorteoImageStorageService
    {
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".webp" };
        private readonly IWebHostEnvironment _environment;

        public SorteoImageStorageService(IWebHostEnvironment environment) => _environment = environment;

        public async Task<string> SaveAsync(IFormFile image, CancellationToken cancellationToken)
        {
            if (image == null || image.Length == 0 || image.Length > 5 * 1024 * 1024)
                throw new InvalidOperationException("La imagen debe tener un tamaño máximo de 5 MB.");
            var extension = Path.GetExtension(image.FileName);
            if (!AllowedExtensions.Contains(extension))
                throw new InvalidOperationException("Solo se permiten imágenes JPG, PNG o WEBP.");
            var folder = Path.Combine(_environment.ContentRootPath, "wwwroot", "uploads", "sorteos");
            Directory.CreateDirectory(folder);
            var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
            await using var stream = File.Create(Path.Combine(folder, fileName));
            await image.CopyToAsync(stream, cancellationToken);
            return $"/uploads/sorteos/{fileName}";
        }

        public void Delete(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) return;
            var fileName = Path.GetFileName(relativePath);
            var path = Path.Combine(_environment.ContentRootPath, "wwwroot", "uploads", "sorteos", fileName);
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
