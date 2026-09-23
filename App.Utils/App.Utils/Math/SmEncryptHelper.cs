using System;
using System.IO;
using System.Text;
using Org.BouncyCastle.Asn1.GM;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Paddings;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Utilities;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;

namespace App.Utils
{
    /// <summary>
    /// 国密帮助类(SM2/SM3/SM4)
    /// </summary>
    public static class SmEncryptHelper
    {
        //-------------------------------------------------------------------
        // SM3 哈希
        //-------------------------------------------------------------------
        //SM3：哈希（替代 SHA-256），Org.BouncyCastle.Crypto.Digests.SM3Digest
        /// <summary>SM3 哈希</summary>
        public static string Sm3Hash(string input)
        {
            var digest = new Org.BouncyCastle.Crypto.Digests.SM3Digest();
            byte[] inputBytes = System.Text.Encoding.UTF8.GetBytes(input);
            digest.BlockUpdate(inputBytes, 0, inputBytes.Length);
            byte[] result = new byte[digest.GetDigestSize()];
            digest.DoFinal(result, 0);
            return BitConverter.ToString(result).Replace("-", "").ToLowerInvariant();
        }


        //-------------------------------------------------------------------
        // SM4 对称密钥算法
        //-------------------------------------------------------------------
        /// <summary>SM4 ECB 加密（PKCS7 填充，密文输出 Base64；key 取 16 字节）</summary>
        public static string Sm4Encrypt(string plaintext, string key)
        {
            byte[] keyBytes = NormalizeKey(key);
            byte[] plainBytes = Encoding.UTF8.GetBytes(plaintext ?? "");

            PaddedBufferedBlockCipher cipher = new PaddedBufferedBlockCipher(new EcbBlockCipher(new SM4Engine()));
            cipher.Init(true, new KeyParameter(keyBytes));

            byte[] outBytes = new byte[cipher.GetOutputSize(plainBytes.Length)];
            int len = cipher.ProcessBytes(plainBytes, 0, plainBytes.Length, outBytes, 0);
            len += cipher.DoFinal(outBytes, len);

            return Convert.ToBase64String(outBytes, 0, len);
        }

        /// <summary>SM4 ECB 解密（输入为 Sm4Encrypt 产出的 Base64 密文）</summary>
        public static string Sm4Decrypt(string ciphertextBase64, string key)
        {
            byte[] keyBytes = NormalizeKey(key);
            byte[] cipherBytes = Convert.FromBase64String(ciphertextBase64 ?? "");

            PaddedBufferedBlockCipher cipher = new PaddedBufferedBlockCipher(new EcbBlockCipher(new SM4Engine()));
            cipher.Init(false, new KeyParameter(keyBytes));

            byte[] outBytes = new byte[cipher.GetOutputSize(cipherBytes.Length)];
            int len = cipher.ProcessBytes(cipherBytes, 0, cipherBytes.Length, outBytes, 0);
            len += cipher.DoFinal(outBytes, len);

            return Encoding.UTF8.GetString(outBytes, 0, len);
        }

        /// <summary>SM4 生成随机密钥（16 字节）</summary>
        public static string Sm4CreateKey()
        {
            byte[] keyBytes = new byte[16];
            new SecureRandom().NextBytes(keyBytes);
            return Convert.ToBase64String(keyBytes);
        }


        //-------------------------------------------------------------------
        // SM2 不对称密钥算法
        //-------------------------------------------------------------------
        /// <summary>SM2 加密（国密标准 C1C3C2，密文输出 Base64）</summary>
        public static string Sm2Encrypt(string input, string publicKey)
        {
            byte[] plainBytes = Encoding.UTF8.GetBytes(input);

            ECPublicKeyParameters pubKey =
                (ECPublicKeyParameters)PublicKeyFactory.CreateKey(PemToBytes(publicKey));

            SM2Engine engine = new SM2Engine(SM2Engine.Mode.C1C3C2);
            engine.Init(true, new ParametersWithRandom(pubKey, new SecureRandom()));
            byte[] cipherBytes = engine.ProcessBlock(plainBytes, 0, plainBytes.Length);

            return Convert.ToBase64String(cipherBytes);
        }

        /// <summary>SM2 解密（输入为 Sm2Encrypt 产出的 Base64 密文）</summary>
        public static string Sm2Decrypt(string input, string privateKey)
        {
            byte[] cipherBytes = Convert.FromBase64String(input);

            ECPrivateKeyParameters priKey =
                (ECPrivateKeyParameters)PrivateKeyFactory.CreateKey(PemToBytes(privateKey));

            SM2Engine engine = new SM2Engine(SM2Engine.Mode.C1C3C2);
            engine.Init(false, priKey);
            byte[] plainBytes = engine.ProcessBlock(cipherBytes, 0, cipherBytes.Length);

            return Encoding.UTF8.GetString(plainBytes);
        }


        /// <summary>生成 SM2 公钥和私钥对（国密推荐曲线 sm2p256v1，PEM 格式）</summary>
        public static (string PublicKey, string PrivateKey) Sm2CreateKeyPair()
        {
            // SM2 国家标准推荐曲线参数（同 GM/T 0003）
            X9ECParameters ecParams = GMNamedCurves.GetByName("sm2p256v1");
            ECDomainParameters domainParams = new ECDomainParameters(
                ecParams.Curve, ecParams.G, ecParams.N, ecParams.H);

            // 生成密钥对
            ECKeyPairGenerator generator = new ECKeyPairGenerator();
            generator.Init(new ECKeyGenerationParameters(domainParams, new SecureRandom()));
            AsymmetricCipherKeyPair keyPair = generator.GenerateKeyPair();

            // 私钥 -> PKCS#8 标准结构（BEGIN PRIVATE KEY）
            PrivateKeyInfo privateKeyInfo = PrivateKeyInfoFactory.CreatePrivateKeyInfo(keyPair.Private);
            string privateKey = Pem(privateKeyInfo.GetDerEncoded(), "PRIVATE KEY");

            // 公钥 -> X.509 SubjectPublicKeyInfo 标准结构（BEGIN PUBLIC KEY）
            SubjectPublicKeyInfo publicKeyInfo = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(keyPair.Public);
            string publicKey = Pem(publicKeyInfo.GetDerEncoded(), "PUBLIC KEY");

            return (publicKey, privateKey);
        }

        /// <summary>DER 字节转标准 PEM 文本（64 字符换行）</summary>
        private static string Pem(byte[] der, string label)
        {
            string base64 = Convert.ToBase64String(der);
            var sb = new System.Text.StringBuilder();
            sb.Append("-----BEGIN ").Append(label).AppendLine("-----");
            for (int i = 0; i < base64.Length; i += 64)
            {
                sb.AppendLine(base64.Substring(i, Math.Min(64, base64.Length - i)));
            }
            sb.Append("-----END ").Append(label).AppendLine("-----");
            return sb.ToString();
        }


        /// <summary>PEM 文本转 DER 字节（去头尾标记、去换行）</summary>
        private static byte[] PemToBytes(string pem)
        {
            string base64 = pem
                .Replace("-----BEGIN PUBLIC KEY-----", "")
                .Replace("-----END PUBLIC KEY-----", "")
                .Replace("-----BEGIN PRIVATE KEY-----", "")
                .Replace("-----END PRIVATE KEY-----", "")
                .Replace("-----BEGIN EC PRIVATE KEY-----", "")
                .Replace("-----END EC PRIVATE KEY-----", "")
                .Replace("\r", "").Replace("\n", "").Trim();
            return Convert.FromBase64String(base64);
        }

        /// <summary>SM4 密钥规范化为 16 字节（128 位）：UTF-8 不足补 0，超出截断</summary>
        private static byte[] NormalizeKey(string key)
        {
            byte[] raw = Encoding.UTF8.GetBytes(key ?? "");
            byte[] keyBytes = new byte[16];
            Array.Copy(raw, keyBytes, Math.Min(raw.Length, 16));
            return keyBytes;
        }

    }

}