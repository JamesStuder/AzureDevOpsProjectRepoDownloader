using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Text;
using System.Security.Cryptography;
using DevOps.BulkRepoDownloader.Models;

namespace DevOps.BulkRepoDownloader.DataAccess
{
    public class FileAccess
    {
        private const string EncryptionHeader = "ENC:DPAPI:CURUSR\n";

        private static readonly byte[] _entropy = "DevOps.BulkRepoDownloader.v1"u8.ToArray();

        /// <summary>
        /// Attempts to decrypt a given text that was previously encrypted with the DPAPI (Data Protection API) in the current user scope.
        /// </summary>
        /// <param name="text">The encrypted text that potentially begins with the specified encryption header.</param>
        /// <param name="json">The output parameter that will contain the decrypted JSON string if decryption is successful.</param>
        /// <returns>A boolean value indicating whether decryption was successful.</returns>
        private static bool TryDecrypt(string text, out string? json)
        {
            json = null;
            if (!text.StartsWith(EncryptionHeader)) return false;
            string b64 = text.Substring(EncryptionHeader.Length).Trim();
            try
            {
                byte[] protectedBytes = Convert.FromBase64String(b64);
                byte[] plainBytes = ProtectedData.Unprotect(protectedBytes, _entropy, DataProtectionScope.CurrentUser);
                json = Encoding.UTF8.GetString(plainBytes);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Encrypts the provided JSON string using the DPAPI (Data Protection API) with the current user scope.
        /// </summary>
        /// <param name="json">The JSON string to encrypt.</param>
        /// <returns>The encrypted string prefixed with the encryption header.</returns>
        private static string Encrypt(string json)
        {
            byte[] plainBytes = Encoding.UTF8.GetBytes(json);
            byte[] protectedBytes = ProtectedData.Protect(plainBytes, _entropy, DataProtectionScope.CurrentUser);
            string b64 = Convert.ToBase64String(protectedBytes);
            return EncryptionHeader + b64;
        }

        /// <summary>
        /// Loads the configuration from the specified file path in JSON format.
        /// </summary>
        /// <param name="path">The file path from which the configuration will be loaded.</param>
        /// <returns>A task that represents the asynchronous load operation. The task's result contains the loaded configuration object.</returns>
        public async Task<Config> LoadConfigAsync(string path)
        {
            try
            {
                string fileText = await File.ReadAllTextAsync(path);
                string json = fileText;
                bool wasEncrypted = TryDecrypt(fileText, out string? decrypted);
                if (wasEncrypted)
                {
                    json = decrypted!;
                }
                Config? config = JsonSerializer.Deserialize<Config>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = false,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                });
                config ??= new Config();
                
                if (!wasEncrypted)
                {
                    await SaveConfigAsync(path, config);
                }

                return config;
            }
            catch
            {
                return new Config();
            }
        }

        /// <summary>
        /// Saves the given configuration object to a file at the specified path in JSON format.
        /// </summary>
        /// <param name="path">The file path where the configuration will be saved.</param>
        /// <param name="config">The configuration object to save.</param>
        /// <returns>A task that represents the asynchronous save operation.</returns>
        public async Task SaveConfigAsync(string path, Config config)
        {
            string json = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
            string encrypted = Encrypt(json);
            await File.WriteAllTextAsync(path, encrypted);
        }
    }
}