//todo 여러군데서 사용되고 있는거 맞나?

using System.Security.Cryptography;
using System.Text;

namespace DevFlexer.RemoteConfigurationService.Hosting
{
    public static class Hasher
    {
        public static string CreateHash(byte[] bytes)
        {
            using var hash = SHA1.Create();
            var hashBytes = hash.ComputeHash(bytes);

            var sb = new StringBuilder(hashBytes.Length * 2);
            foreach (var b in hashBytes)
            {
                sb.Append(b.ToString("X2"));
            }
            return sb.ToString();
        }
    }
}
