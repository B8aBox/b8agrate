using System.Security.Cryptography;
using System.Text;

namespace B8aGrate.Application.Extensions;

public static class SqlExtensions
{
    public static string GetChecksum(this string sql) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql))).ToLowerInvariant();
}