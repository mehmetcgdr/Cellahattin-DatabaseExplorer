using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using Cellahattin.Configuration;
using System;
using System.Configuration;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Cellahattin.Encryption;

public static class EncryptionHelper
{
    private static readonly string SYMMETRIC_KEY = SecurityConfig.Instance.SymmetricKey;
    private static readonly byte[] _saltbytes = SecurityConfig.Instance.Salt;

    public static void CreatePasswordHash(this string password, out byte[] passwordHash, out byte[] passwordSalt)
    {
        using (var hmac = new HMACSHA512())
        {
            passwordSalt = hmac.Key;
            passwordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
        }
    }

    public static bool VerifyPasswordHash(this string password, byte[] storedHash, byte[] storedSalt)
    {
        using (var hmac = new HMACSHA512(storedSalt))
        {
            var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));

            for (int i = 0; i < computedHash.Length; i++)
                if (computedHash[i] != storedHash[i])
                    return false;
        }

        return true;
    }


    public static string EncryptString(this string plainText, string smk)
    {

        byte[] bytes = Encoding.ASCII.GetBytes(plainText);
        byte[] encryptedBytes = ctrNoPaddingENC(bytes, smk);

        return Convert.ToBase64String(encryptedBytes);

    }

    public static string DecryptString(this string encryptedText, string smk)
    {
        byte[] encryptedBytesFromBase64String = Convert.FromBase64String(encryptedText);
        byte[] decryptedBytesFromEncedBytes = ctrNoPaddingENC(encryptedBytesFromBase64String, smk);

        string decedString = Encoding.ASCII.GetString(decryptedBytesFromEncedBytes);

        return decedString;
    }

    public static string DecryptString(this string encryptedText)
    {
        byte[] encryptedBytesFromBase64String = Convert.FromBase64String(encryptedText);
        byte[] decryptedBytesFromEncedBytes = ctrNoPaddingENC(encryptedBytesFromBase64String, SYMMETRIC_KEY);

        string decedString = Encoding.ASCII.GetString(decryptedBytesFromEncedBytes);

        return decedString;
    }


    public static (byte[], byte[]) ENCDECValues(string smk)
    {
        byte[] _k = Encoding.ASCII.GetBytes(smk);
        byte[] _iv = Encoding.ASCII.GetBytes(SecurityConfig.Instance.EncryptionKey);

        return (_k, _iv);
    }

    public static ICipherParameters GetKeyParamWithIV(string smk)
    {
        var (_k, _iv) = ENCDECValues(smk);
        return new ParametersWithIV(new KeyParameter(_k), _iv, 0, 16);
    }

    public static byte[] ctrNoPaddingENC(byte[] plainTextData, string smk)
    {

        ICipherParameters keyParamWithIV = GetKeyParamWithIV(smk);

        IBlockCipher symmetricBlockCipher = new ThreefishEngine(256);
        IBlockCipherMode symmetricBlockMode = new KCtrBlockCipher(symmetricBlockCipher);
        BufferedBlockCipher ctrCipher = new BufferedBlockCipher(symmetricBlockMode);
        ctrCipher.Init(true, keyParamWithIV);

        int blockSize = ctrCipher.GetBlockSize();
        byte[] cipherTextData = new byte[ctrCipher.GetOutputSize(plainTextData.Length)];
        int processLength = ctrCipher.ProcessBytes(plainTextData, 0, plainTextData.Length, cipherTextData, 0);
        int finalLength = ctrCipher.DoFinal(cipherTextData, processLength);

        byte[] finalCipherTextData = new byte[processLength + finalLength];
        Array.Copy(cipherTextData, 0, finalCipherTextData, 0, finalCipherTextData.Length);

        return finalCipherTextData;
    }

    public static byte[] ctrNoPaddingDEC(byte[] cipherTextData, string smk)
    {

        ICipherParameters keyParamWithIV = GetKeyParamWithIV(smk);

        IBlockCipher symmetricBlockCipher = new ThreefishEngine(256);
        IBlockCipherMode symmetricBlockMode = new KCtrBlockCipher(symmetricBlockCipher);
        BufferedBlockCipher ctrCipher = new BufferedBlockCipher(symmetricBlockMode);
        ctrCipher.Init(false, keyParamWithIV);

        int blockSize = ctrCipher.GetBlockSize();
        byte[] plainTextData = new byte[ctrCipher.GetOutputSize(cipherTextData.Length)];
        int processLength = ctrCipher.ProcessBytes(cipherTextData, 0, cipherTextData.Length, plainTextData, 0);
        int finalLength = ctrCipher.DoFinal(plainTextData, processLength);

        byte[] finalPlainTextData = new byte[processLength + finalLength];
        Array.Copy(plainTextData, 0, finalPlainTextData, 0, finalPlainTextData.Length);
        return finalPlainTextData;
    }

    public static bool IsEncrypted(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;

        try
        {
            Span<byte> buffer = new byte[text.Length];
            if (!Convert.TryFromBase64String(text, buffer, out _))
                return false;

            string decrypted = DecryptString(text, SYMMETRIC_KEY);
            return !string.IsNullOrEmpty(decrypted);
        }
        catch
        {
            return false;
        }
    }
}