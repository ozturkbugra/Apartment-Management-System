using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace ApartmanAidatTakip.Helpers
{
    /// <summary>
    /// Google Authenticator (RFC 6238 - TOTP) ile uyumlu iki adımlı doğrulama yardımcı sınıfı.
    /// Harici bir NuGet paketine ihtiyaç duymadan HMAC-SHA1 tabanlı 6 haneli, 30 saniyelik kodlar üretir/doğrular.
    /// </summary>
    public static class TwoFactorHelper
    {
        private const string Base32Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        private const int StepSeconds = 30;
        private const int CodeDigits = 6;

        /// <summary>
        /// Kullanıcıya özel rastgele bir gizli anahtar (Base32) üretir.
        /// </summary>
        public static string GenerateSecret(int byteLength = 20)
        {
            var bytes = new byte[byteLength];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            return Base32Encode(bytes);
        }

        /// <summary>
        /// Authenticator uygulamasının okuyacağı otpauth:// URI'sini oluşturur (QR kodun içeriği).
        /// </summary>
        public static string GetOtpAuthUri(string secret, string account, string issuer)
        {
            string label = Uri.EscapeDataString(issuer + ":" + account);
            string encodedIssuer = Uri.EscapeDataString(issuer);
            return $"otpauth://totp/{label}?secret={secret}&issuer={encodedIssuer}&algorithm=SHA1&digits={CodeDigits}&period={StepSeconds}";
        }

        /// <summary>
        /// Kullanıcının girdiği kodu, saat kaymalarını tolere edecek şekilde (±window adım) doğrular.
        /// </summary>
        public static bool ValidateCode(string secret, string code, int window = 1)
        {
            if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(code))
                return false;

            code = code.Trim().Replace(" ", "");
            if (code.Length != CodeDigits)
                return false;

            long counter = GetCurrentCounter();
            for (long i = -window; i <= window; i++)
            {
                if (GenerateCode(secret, counter + i) == code)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Kullanıcı Authenticator'a erişemediğinde kullanabileceği tek kullanımlık yedek kodlar üretir.
        /// Düz metin döner; DB'ye yalnızca hash'lenmiş halleri (HashRecoveryCode) yazılmalıdır.
        /// </summary>
        public static List<string> GenerateRecoveryCodes(int count = 8)
        {
            var codes = new List<string>();
            using (var rng = RandomNumberGenerator.Create())
            {
                for (int i = 0; i < count; i++)
                {
                    var bytes = new byte[5];
                    rng.GetBytes(bytes);
                    string hex = BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
                    codes.Add(hex.Substring(0, 5) + "-" + hex.Substring(5, 5)); // örn: a3f9c-1b7e2
                }
            }
            return codes;
        }

        /// <summary>
        /// Yedek kodu, DB'de saklamak/karşılaştırmak için SHA-256 ile hash'ler (büyük/küçük harf ve tire duyarsız).
        /// </summary>
        public static string HashRecoveryCode(string code)
        {
            string normalized = (code ?? "").Trim().ToLowerInvariant().Replace(" ", "");
            using (var sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(normalized));
                return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
            }
        }

        private static long GetCurrentCounter()
        {
            var unixSeconds = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
            return unixSeconds / StepSeconds;
        }

        private static string GenerateCode(string secret, long counter)
        {
            byte[] key = Base32Decode(secret);
            byte[] counterBytes = BitConverter.GetBytes(counter);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(counterBytes);

            using (var hmac = new HMACSHA1(key))
            {
                byte[] hash = hmac.ComputeHash(counterBytes);
                int offset = hash[hash.Length - 1] & 0x0F;
                int binary = ((hash[offset] & 0x7F) << 24)
                           | ((hash[offset + 1] & 0xFF) << 16)
                           | ((hash[offset + 2] & 0xFF) << 8)
                           | (hash[offset + 3] & 0xFF);
                int otp = binary % (int)Math.Pow(10, CodeDigits);
                return otp.ToString().PadLeft(CodeDigits, '0');
            }
        }

        private static string Base32Encode(byte[] data)
        {
            var result = new StringBuilder((data.Length + 4) / 5 * 8);
            int buffer = 0, bitsLeft = 0;
            foreach (byte b in data)
            {
                buffer = (buffer << 8) | b;
                bitsLeft += 8;
                while (bitsLeft >= 5)
                {
                    int index = (buffer >> (bitsLeft - 5)) & 0x1F;
                    bitsLeft -= 5;
                    result.Append(Base32Chars[index]);
                }
            }
            if (bitsLeft > 0)
            {
                int index = (buffer << (5 - bitsLeft)) & 0x1F;
                result.Append(Base32Chars[index]);
            }
            return result.ToString();
        }

        private static byte[] Base32Decode(string input)
        {
            input = input.TrimEnd('=').ToUpperInvariant().Replace(" ", "");
            int byteCount = input.Length * 5 / 8;
            var result = new byte[byteCount];

            int buffer = 0, bitsLeft = 0, index = 0;
            foreach (char c in input)
            {
                int val = Base32Chars.IndexOf(c);
                if (val < 0)
                    continue; // geçersiz karakterleri yok say

                buffer = (buffer << 5) | val;
                bitsLeft += 5;
                if (bitsLeft >= 8)
                {
                    result[index++] = (byte)((buffer >> (bitsLeft - 8)) & 0xFF);
                    bitsLeft -= 8;
                }
            }
            return result;
        }
    }
}
