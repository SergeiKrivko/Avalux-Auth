using System.Security.Cryptography;

namespace AvaluxAuth.Utils;

public static class RandomExtensions
{
    extension(RandomNumberGenerator random)
    {
        public string RandomString(int length = 32)
        {
            var randomNumber = new byte[length / 2];
            random.GetBytes(randomNumber);
            return Convert.ToHexStringLower(randomNumber).Replace("-", string.Empty);
        }
        public static string GetRandomString(int length = 32)
        {
            var randomNumber = RandomNumberGenerator.GetBytes(length / 2);
            return Convert.ToHexStringLower(randomNumber).Replace("-", string.Empty);
        }
    }
}