using System;
using System.Collections.Generic;
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
        /// <returns>The decrypted string if successful; otherwise, the original text.</returns>
        public static string DecryptPAT(string? text)
        {
            if (string.IsNullOrEmpty(text) || !text.StartsWith(EncryptionHeader))
            {
                return text ?? string.Empty;
            }
            string b64 = text.Substring(EncryptionHeader.Length).Trim();
            try
            {
                byte[] protectedBytes = Convert.FromBase64String(b64);
                byte[] plainBytes = ProtectedData.Unprotect(protectedBytes, _entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch
            {
                return text;
            }
        }

        /// <summary>
        /// Encrypts the provided string using the DPAPI (Data Protection API) with the current user scope.
        /// </summary>
        /// <param name="text">The text to encrypt.</param>
        /// <returns>The encrypted string prefixed with the encryption header.</returns>
        public static string EncryptPAT(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }
            if (text.StartsWith(EncryptionHeader))
            {
                return text;
            }
            byte[] plainBytes = Encoding.UTF8.GetBytes(text);
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
                

                Config? config = JsonSerializer.Deserialize<Config>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = false,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                });
                config ??= new Config();
                
                if (config.Orgs != null)
                {
                    foreach (OrgConfig org in config.Orgs)
                    {
                        org.PAT = DecryptPAT(org.PAT);
                    }
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
            // Encrypt PATs before saving
            if (config.Orgs != null)
            {
                foreach (OrgConfig org in config.Orgs)
                {
                    org.PAT = EncryptPAT(org.PAT);
                }
            }

            string json = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            await File.WriteAllTextAsync(path, json);

            // Decrypt back in memory so the app can continue using them
            if (config.Orgs != null)
            {
                foreach (OrgConfig org in config.Orgs)
                {
                    org.PAT = DecryptPAT(org.PAT);
                }
            }
        }
    }
}