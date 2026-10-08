using System.Security.Cryptography;

namespace eLotto.Core.Services;

public interface IReferralCodeGenerator
{
    string Generate();
}

public sealed class ReferralCodeGenerator : IReferralCodeGenerator
{
    public const int CodeLength = 8;
    public const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    public string Generate() => Create();

    public static string Create()
    {
        Span<char> code = stackalloc char[CodeLength];
        for (var index = 0; index < code.Length; index++)
            code[index] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];

        return new string(code);
    }

    public static bool IsValid(string code) =>
        code?.Length == CodeLength &&
        code.All(character => Alphabet.Contains(character));
}
