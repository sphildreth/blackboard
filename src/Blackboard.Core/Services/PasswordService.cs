using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Blackboard.Core.Configuration;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

namespace Blackboard.Core.Services;

public interface IPasswordService
{
    string HashPassword(string password, string salt);
    string GenerateSalt();
    bool VerifyPassword(string password, string hashedPassword, string salt);
    bool ValidatePasswordComplexity(string password, SecuritySettings settings);
    string GenerateSecurePassword(int length = 12);
}

public class PasswordService : IPasswordService
{
    public string HashPassword(string password, string salt)
    {
        // Use PBKDF2 for secure password hashing
        var saltBytes = Convert.FromBase64String(salt);
        var hash = Convert.ToBase64String(KeyDerivation.Pbkdf2(
            password,
            saltBytes,
            KeyDerivationPrf.HMACSHA256,
            10000,
            256 / 8));

        return hash;
    }

    public string GenerateSalt()
    {
        var saltBytes = new byte[128 / 8];
        using var rng = RandomNumberGenerator.Create();
        rng.GetNonZeroBytes(saltBytes);
        return Convert.ToBase64String(saltBytes);
    }

    public bool VerifyPassword(string password, string hashedPassword, string salt)
    {
        try
        {
            var testHash = HashPassword(password, salt);
            return testHash == hashedPassword;
        }
        catch
        {
            return false;
        }
    }

    public bool ValidatePasswordComplexity(string password, SecuritySettings settings)
    {
        if (string.IsNullOrEmpty(password))
            return false;

        if (password.Length < settings.PasswordMinLength)
            return false;

        if (!settings.RequirePasswordComplexity)
            return true;

        // Check for complexity requirements
        var hasUpper = Regex.IsMatch(password, @"[A-Z]");
        var hasLower = Regex.IsMatch(password, @"[a-z]");
        var hasDigit = Regex.IsMatch(password, @"\d");
        var hasSpecial = Regex.IsMatch(password, @"[^\w\s]");

        return hasUpper && hasLower && hasDigit && hasSpecial;
    }

    public string GenerateSecurePassword(int length = 12)
    {
        const string validChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890!@#$%^&*";
        var password = new StringBuilder();
        using var rng = RandomNumberGenerator.Create();

        // Ensure at least one character from each required category
        password.Append(GetRandomChar("ABCDEFGHIJKLMNOPQRSTUVWXYZ", rng));
        password.Append(GetRandomChar("abcdefghijklmnopqrstuvwxyz", rng));
        password.Append(GetRandomChar("1234567890", rng));
        password.Append(GetRandomChar("!@#$%^&*", rng));

        // Fill the rest randomly
        for (var i = 4; i < length; i++) password.Append(GetRandomChar(validChars, rng));

        // Shuffle the password
        return new string(password.ToString().OrderBy(x => GetRandomInt(rng)).ToArray());
    }

    private static char GetRandomChar(string chars, RandomNumberGenerator rng)
    {
        var bytes = new byte[4];
        rng.GetBytes(bytes);
        var randomIndex = Math.Abs(BitConverter.ToInt32(bytes, 0)) % chars.Length;
        return chars[randomIndex];
    }

    private static int GetRandomInt(RandomNumberGenerator rng)
    {
        var bytes = new byte[4];
        rng.GetBytes(bytes);
        return BitConverter.ToInt32(bytes, 0);
    }
}