using Google.Apis.Auth.OAuth2;
using System.Threading;
using System.Text.Json;

namespace PenguinTwitchBot.Extensions
{
    public static class GoogleCredentialExtensions
    {
        private const string GttsJsonFileName = "gtts.json";

        /// <summary>
        /// Load Google credentials from gtts.json file in the content root directory.
        /// Uses CredentialFactory pattern recommended by Google Cloud libraries.
        /// </summary>
        /// <param name="environment">The web host environment to get the content root path</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>A GoogleCredential object</returns>
        public static async Task<GoogleCredential> LoadGoogleCredentialAsync(this IWebHostEnvironment environment, CancellationToken cancellationToken = default)
        {
            var credentialPath = Path.Combine(environment.ContentRootPath, GttsJsonFileName);

            if (!File.Exists(credentialPath))
            {
                throw new FileNotFoundException(
                    $"Google credentials file not found at '{credentialPath}'. " +
                    $"Please ensure 'gtts.json' exists in the application root directory.");
            }

            try
            {
                var json = await File.ReadAllTextAsync(credentialPath, cancellationToken);
                using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
                var serviceAccountCredential = ServiceAccountCredential.FromServiceAccountData(stream);
                return serviceAccountCredential.ToGoogleCredential();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to load Google credentials from '{credentialPath}'. " +
                    $"Ensure the file is a valid Google service account JSON key.", ex);
            }
        }
    }
}
