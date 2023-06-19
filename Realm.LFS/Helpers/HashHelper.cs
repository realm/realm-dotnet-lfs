using System.Text;

namespace Realms.LFS;

internal static class HashHelper
{
    public static string MD5(string input)
    {
        var md5 = System.Security.Cryptography.MD5.Create();
        var inputBytes = Encoding.UTF8.GetBytes(input);
        var hash = md5.ComputeHash(inputBytes);

        var sb = new StringBuilder();
        foreach (var t in hash)
        {
            sb.Append(t.ToString("X2"));
        }
        return sb.ToString();
    }
}