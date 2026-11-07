using System.Security.Cryptography;

namespace Servora.Application.Identity;

/// <summary>Generates random temporary passwords that satisfy any supported password policy.</summary>
public static class TemporaryPassword
{
    private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lower = "abcdefghijkmnopqrstuvwxyz";
    private const string Digits = "23456789";
    private const string Symbols = "!#$%&*+-=?@_";

    /// <summary>Generates a password with upper / lower case letters, digits and symbols using a CSPRNG.</summary>
    /// <param name="length">The length (clamped to 16–128).</param>
    /// <returns>The password.</returns>
    public static string Generate(int length = 20)
    {
        length = Math.Clamp(length, 16, 128);
        var all = Upper + Lower + Digits + Symbols;
        var chars = new char[length];
        chars[0] = Upper[RandomNumberGenerator.GetInt32(Upper.Length)];
        chars[1] = Lower[RandomNumberGenerator.GetInt32(Lower.Length)];
        chars[2] = Digits[RandomNumberGenerator.GetInt32(Digits.Length)];
        chars[3] = Symbols[RandomNumberGenerator.GetInt32(Symbols.Length)];
        for (var i = 4; i < length; i++)
        {
            chars[i] = all[RandomNumberGenerator.GetInt32(all.Length)];
        }

        // Fisher-Yates shuffle so the guaranteed classes are not always at the start.
        for (var i = length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);
    }
}
