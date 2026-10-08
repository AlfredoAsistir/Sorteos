using System.Security.Cryptography;
using System.Text.Json;

namespace eLotto.Services
{
    public sealed record ScratchcardVisualResult(
        IReadOnlyList<string> Cells,
        string WinningLine);

    public static class ScratchcardVisualResultGenerator
    {
        public static readonly IReadOnlyList<string> Symbols =
            new[] { "money", "star", "gift", "trophy", "ticket", "crown" };

        private static readonly (string Code, int[] Cells)[] Lines =
        {
            ("row:0", new[] { 0, 1, 2 }),
            ("row:1", new[] { 3, 4, 5 }),
            ("row:2", new[] { 6, 7, 8 }),
            ("column:0", new[] { 0, 3, 6 }),
            ("column:1", new[] { 1, 4, 7 }),
            ("column:2", new[] { 2, 5, 8 }),
            ("diagonal:main", new[] { 0, 4, 8 }),
            ("diagonal:anti", new[] { 2, 4, 6 })
        };

        public static ScratchcardVisualResult Generate(
            bool isWinner,
            int? winningLineIndex = null)
        {
            return isWinner
                ? GenerateWinner(winningLineIndex)
                : GenerateLoser();
        }

        public static string SerializeCells(IReadOnlyList<string> cells) =>
            JsonSerializer.Serialize(cells);

        public static ScratchcardVisualResult Deserialize(
            string matrixJson,
            string winningLine)
        {
            var cells = JsonSerializer.Deserialize<string[]>(matrixJson)
                ?? Array.Empty<string>();
            return new ScratchcardVisualResult(cells, winningLine);
        }

        public static bool IsValid(
            ScratchcardVisualResult result,
            bool expectedWinner)
        {
            if (result?.Cells == null ||
                result.Cells.Count != 9 ||
                result.Cells.Any(cell => !Symbols.Contains(cell)))
                return false;

            var winningLines = Lines
                .Where(line =>
                    result.Cells[line.Cells[0]] == result.Cells[line.Cells[1]] &&
                    result.Cells[line.Cells[1]] == result.Cells[line.Cells[2]])
                .Select(line => line.Code)
                .ToArray();

            return expectedWinner
                ? !string.IsNullOrWhiteSpace(result.WinningLine) &&
                  winningLines.Contains(result.WinningLine)
                : string.IsNullOrWhiteSpace(result.WinningLine) &&
                  winningLines.Length == 0;
        }

        public static IReadOnlyList<int> GetWinningCells(string winningLine)
        {
            var line = Lines.SingleOrDefault(candidate => candidate.Code == winningLine);
            return line.Cells ?? Array.Empty<int>();
        }

        private static ScratchcardVisualResult GenerateWinner(int? winningLineIndex)
        {
            var lineIndex = winningLineIndex ?? RandomNumberGenerator.GetInt32(Lines.Length);
            if (lineIndex < 0 || lineIndex >= Lines.Length)
                throw new ArgumentOutOfRangeException(nameof(winningLineIndex));

            var cells = Enumerable.Range(0, 9)
                .Select(_ => RandomSymbol())
                .ToArray();
            var winningSymbol = RandomSymbol();
            var line = Lines[lineIndex];
            foreach (var cellIndex in line.Cells)
                cells[cellIndex] = winningSymbol;

            var result = new ScratchcardVisualResult(cells, line.Code);
            if (!IsValid(result, true))
                throw new InvalidOperationException(
                    "No fue posible generar una matriz ganadora válida.");

            return result;
        }

        private static ScratchcardVisualResult GenerateLoser()
        {
            for (var attempt = 0; attempt < 128; attempt++)
            {
                var result = new ScratchcardVisualResult(
                    Enumerable.Range(0, 9)
                        .Select(_ => RandomSymbol())
                        .ToArray(),
                    null);
                if (IsValid(result, false)) return result;
            }

            var fallback = new ScratchcardVisualResult(
                new[]
                {
                    "money", "star", "gift",
                    "trophy", "ticket", "crown",
                    "star", "gift", "trophy"
                },
                null);
            if (!IsValid(fallback, false))
                throw new InvalidOperationException(
                    "No fue posible generar una matriz no ganadora válida.");

            return fallback;
        }

        private static string RandomSymbol() =>
            Symbols[RandomNumberGenerator.GetInt32(Symbols.Count)];
    }
}
