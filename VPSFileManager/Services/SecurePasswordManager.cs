using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace VPSFileManager.Services
{
    /// <summary>
    /// Gerenciador seguro de senhas com proteção em memória
    /// Usa AES com chave única do processo para máxima segurança
    /// (ProtectedMemory não disponível no .NET 8)
    /// </summary>
    public class SecurePasswordManager
    {
        private byte[]? _encryptedPasswordInMemory;
        private readonly byte[] _processKey;
        private readonly byte[] _processIV;

        public SecurePasswordManager()
        {
            // Gerar chave única para este processo
            _processKey = new byte[32]; // AES-256
            _processIV = new byte[16];  // AES IV

            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(_processKey);
                rng.GetBytes(_processIV);
            }

            // Misturar com dados únicos do processo para bind à instância
            var pid = System.Diagnostics.Process.GetCurrentProcess().Id;
            var pidBytes = BitConverter.GetBytes(pid);

            for (int i = 0; i < pidBytes.Length; i++)
            {
                _processKey[i] ^= pidBytes[i];
            }
        }

        /// <summary>
        /// Armazena senha de forma segura na memória (Process-scoped via AES)
        /// </summary>
        public void SetPasswordInMemory(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                _encryptedPasswordInMemory = null;
                return;
            }

            try
            {
                var bytes = Encoding.UTF8.GetBytes(password);

                // Criptografar com AES usando chave única do processo
                using (var aes = Aes.Create())
                {
                    aes.Key = _processKey;
                    aes.IV = _processIV;

                    using (var encryptor = aes.CreateEncryptor())
                    {
                        _encryptedPasswordInMemory = encryptor.TransformFinalBlock(bytes, 0, bytes.Length);
                    }
                }

                // Limpar array original
                Array.Clear(bytes, 0, bytes.Length);

                // Tentar zerar string (best effort)
                SecureClearString(ref password);
            }
            catch
            {
                _encryptedPasswordInMemory = null;
            }
        }

        /// <summary>
        /// Usa senha de forma segura (tempo mínimo em memória)
        /// </summary>
        public void UsePassword(Action<string> useAction)
        {
            if (_encryptedPasswordInMemory == null)
                throw new InvalidOperationException("Password not set in memory");

            byte[]? decrypted = null;
            string? password = null;

            try
            {
                // Descriptografar temporariamente
                using (var aes = Aes.Create())
                {
                    aes.Key = _processKey;
                    aes.IV = _processIV;

                    using (var decryptor = aes.CreateDecryptor())
                    {
                        decrypted = decryptor.TransformFinalBlock(_encryptedPasswordInMemory, 0, _encryptedPasswordInMemory.Length);
                    }
                }

                password = Encoding.UTF8.GetString(decrypted);

                // Usar senha (tempo mínimo em memória claro)
                useAction(password);
            }
            finally
            {
                // Limpeza agressiva SEMPRE acontece
                if (decrypted != null)
                {
                    Array.Clear(decrypted, 0, decrypted.Length);
                }

                if (password != null)
                {
                    SecureClearString(ref password);
                }

                // Forçar coleta de lixo
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
                GC.WaitForPendingFinalizers();
            }
        }

        /// <summary>
        /// Limpa senha protegida da memória
        /// </summary>
        public void Clear()
        {
            if (_encryptedPasswordInMemory != null)
            {
                Array.Clear(_encryptedPasswordInMemory, 0, _encryptedPasswordInMemory.Length);
                _encryptedPasswordInMemory = null;
            }

            Array.Clear(_processKey, 0, _processKey.Length);
            Array.Clear(_processIV, 0, _processIV.Length);

            GC.Collect();
        }

        /// <summary>
        /// Tenta zerar string na memória (best effort - não garantido)
        /// </summary>
        private static void SecureClearString(ref string? str)
        {
            if (str == null) return;

            try
            {
                unsafe
                {
                    fixed (char* ptr = str)
                    {
                        for (int i = 0; i < str.Length; i++)
                        {
                            ptr[i] = '\0';
                        }
                    }
                }
            }
            catch
            {
                // Ignorar erros (string pode ser read-only)
            }
            finally
            {
                str = null;
            }
        }
    }
}
