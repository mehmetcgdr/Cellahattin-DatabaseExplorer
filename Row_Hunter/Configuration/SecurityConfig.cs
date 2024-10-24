using Microsoft.Extensions.Configuration;
using System.Configuration;
using System.IO;

namespace Cellahattin.Configuration
{
    public class SecurityConfig
    {
        private static SecurityConfig _instance;
        private static readonly object _lock = new object();
        private readonly IConfiguration _configuration;

        public string SymmetricKey { get; private set; }
        public string EncryptionKey { get; private set; }
        public byte[] Salt { get; private set; }

        private SecurityConfig()
        {
            _configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            LoadConfiguration();
        }

        private void LoadConfiguration()
        {
            SymmetricKey = _configuration.GetValue<string>("SecuritySettings:SymmetricKey");
            EncryptionKey = _configuration.GetValue<string>("SecuritySettings:EncryptionKey");
            Salt = _configuration.GetSection("SecuritySettings:Salt").Get<byte[]>();

            ValidateConfiguration();
        }

        private void ValidateConfiguration()
        {
            if (string.IsNullOrEmpty(SymmetricKey))
                throw new ConfigurationException("SymmetricKey is not configured in appsettings.json");

            if (string.IsNullOrEmpty(EncryptionKey))
                throw new ConfigurationException("EncryptionKey is not configured in appsettings.json");

            if (Salt == null || Salt.Length == 0)
                throw new ConfigurationException("Salt is not configured in appsettings.json");
        }

        public static SecurityConfig Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new SecurityConfig();
                        }
                    }
                }
                return _instance;
            }
        }
    }

}