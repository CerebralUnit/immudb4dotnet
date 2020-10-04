using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodeNotary.ImmuDb.ImmudbProto;
using CodeNotary.ImmuDb.Roots;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Empty = Google.Protobuf.WellKnownTypes.Empty;

namespace CodeNotary.ImmuDb
{
    public class ImmuClient : IDisposable
    {
        private ImmuService.ImmuServiceClient client;
        private Channel channel;
        private RootHolder rootHolder = new RootHolder();

        private ILogger<ImmuClient> logger;
        private Empty emptyArgument = new Empty();
        private string authToken;
        private string activeDatabaseName = "defaultdb";

        public ImmuClient(string address, int port = 3322, ILoggerFactory loggerFactory = null)
        {
            this.logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<ImmuClient>();

            this.channel = new Channel(address, port, ChannelCredentials.Insecure);

            this.client = new ImmuService.ImmuServiceClient(this.channel);
        }

        public async Task LoginAsync(string user, string password, string databaseName = null)
        {
            var loginRequest = new LoginRequest()
            {
                User = ByteString.CopyFromUtf8(user),
                Password = ByteString.CopyFromUtf8(password),
            };

            var result = await this.client.LoginAsync(loginRequest, new CallOptions() { });

            this.authToken = result.Token;

            if (!result.Warning.IsEmpty)
            {
                this.logger.LogWarning(result.Warning.ToStringUtf8());
            }

            this.logger.LogInformation("Successfull login, token {0}", result.Token);

            if (!string.IsNullOrEmpty(databaseName))
            {
                await this.UseDatabaseAsync(databaseName);
            }
        }

        public async Task LogoutAsync()
        {
            if (this.client != null && !string.IsNullOrEmpty(this.authToken))
            {
                await this.client.LogoutAsync(this.emptyArgument, this.getSecurityHeader());
                this.authToken = null;
            }
        }

        public async Task<IEnumerable<string>> GetDatabasesAsync()
        {
            var databases = await this.client.DatabaseListAsync(this.emptyArgument, this.getSecurityHeader());

            return databases.Databases.Select(db => db.Databasename);
        }

        public async Task UseDatabaseAsync(string databaseName, bool createIfNotExists = true)
        {
            var databases = await this.GetDatabasesAsync();

            if (!databases.Contains(databaseName))
            {
                if (createIfNotExists)
                {
                    await this.CreateDatabaseAsync(databaseName);
                }
                else
                {
                    throw new Exception($"Database {databaseName} does not exists");
                }
            }

            var result = await this.client.UseDatabaseAsync(new Database() { Databasename = databaseName }, this.getSecurityHeader());

            this.activeDatabaseName = databaseName;

            this.logger.LogInformation($"Current database is {databaseName}");

            this.authToken = result.Token;
        }

        public async Task CreateDatabaseAsync(string databaseName)
        {
            await this.client.CreateDatabaseAsync(new Database() { Databasename = databaseName }, this.getSecurityHeader());

            this.logger.LogInformation($"Created database {databaseName}");
        }

        public void Close()
        {
            try
            {
                if (this.client != null && !string.IsNullOrEmpty(this.authToken))
                {
                    this.client.Logout(this.emptyArgument, this.getSecurityHeader());
                    this.authToken = null;
                }

                if (this.channel != null && this.channel.State != ChannelState.Shutdown)
                {
                    this.channel.ShutdownAsync();
                }
            }
            catch (Exception ex)
            {
                //catch all when we called it from dispose
                if (!this.disposedValue)
                {
                    throw;
                }
                else
                {
                    this.logger.LogError(ex, "Exception on close");
                }
            }

            this.logger.LogInformation($"Connection closed");
        }

        public async Task SetAsync(string key, string value)
        {
            var content = new Content()
            {
                Timestamp = (ulong)DateTime.UtcNow.ToTimestamp().Seconds,
                Payload = ByteString.CopyFromUtf8(value)
            };

            await this.SetRawAsync(key, content.ToByteArray());
        }

        public async Task SetAsync<T>(string key, T value) where T : class
        {
            await this.SetAsync(key, JsonConvert.SerializeObject(value));
        }

        public async Task SetRawAsync(string key, byte[] value)
        {
            var request = new KeyValue()
            {
                Key = ByteString.CopyFromUtf8(key),
                Value = ByteString.CopyFrom(value)
            };

            await this.client.SetAsync(request, this.getSecurityHeader());
        }

        public bool TryGet(string key, out string value)
        {
            try
            {
                value = this.GetAsync(key).Result;

                return true;
            }
            catch
            {
                value = null;
                return false;
            }
        }

        public bool TryGet<T>(string key, out T value) where T : class
        {
            if (this.TryGet(key, out var json))
            {
                value = JsonConvert.DeserializeObject<T>(json);
                return true;
            }
            else
            {
                value = default;
                return false;
            }
        }

        public async Task<string> GetAsync(string key)
        {
            var result = await this.GetRawAsync(key);

            try
            {
                var content = Content.Parser.ParseFrom(result);

                return content.Payload.ToStringUtf8();
            }
            catch (InvalidProtocolBufferException)
            {
                return ByteString.CopyFrom(result).ToStringUtf8();
            }
        }

        public async Task<T> GetAsync<T>(string key) where T : class
        {
            var json = await this.GetAsync(key);

            return JsonConvert.DeserializeObject<T>(json);
        }

        public async Task<byte[]> GetRawAsync(string key)
        {
            var request = new Key()
            {
                Key_ = ByteString.CopyFromUtf8(key),
            };

            var result = await this.client.GetAsync(request, this.getSecurityHeader());

            return result.Value.ToByteArray();
        }

        public async Task<string> SafeGetAsync(string key)
        {
            var result = await this.SafeGetRawAsync(key);

            try
            {
                var content = Content.Parser.ParseFrom(result);

                return content.Payload.ToStringUtf8();
            }
            catch (InvalidProtocolBufferException)
            {
                return ByteString.CopyFrom(result).ToStringUtf8();
            }
        }

        public async Task<byte[]> SafeGetRawAsync(string key)
        {
            var root = this.getActiveDatabaseRoot();

            var request = new SafeGetOptions()
            {
                Key = ByteString.CopyFromUtf8(key),
                RootIndex = new Index() { Index_ = root.Index }
            };

            var result = await this.client.SafeGetAsync(request, this.getSecurityHeader());

            CryptoUtils.Verify(result.Proof, result.Item, root);

            this.rootHolder.SetRoot(this.activeDatabaseName, new Root() { Root_ = result.Proof.Root, Index = result.Proof.At });

            return result.Item.Value.ToByteArray();
        }

        public async Task SafeSetAsync(string key, string value)
        {
            var content = new Content()
            {
                Timestamp = (ulong)DateTime.UtcNow.ToTimestamp().Seconds,
                Payload = ByteString.CopyFromUtf8(value)
            };

            await this.SafeSetRawAsync(key, content.ToByteArray());
        }

        public async Task SafeSetRawAsync(string key, byte[] value)
        {
            var root = this.getActiveDatabaseRoot();

            var request = new SafeSetOptions()
            {
                Kv = new KeyValue()
                {
                    Key = ByteString.CopyFromUtf8(key),
                    Value = ByteString.CopyFrom(value)
                },

                RootIndex = new Index() { Index_ = root.Index }
            };

            var proof = await this.client.SafeSetAsync(request, this.getSecurityHeader());

            var item = new Item()
            {
                Index = proof.Index,
                Key = request.Kv.Key,
                Value = request.Kv.Value
            };

            CryptoUtils.Verify(proof, item, root);

            this.rootHolder.SetRoot(this.activeDatabaseName, new Root() { Root_ = proof.Root, Index = proof.At });
        }

        public byte[] GetRoots()
        {
            return this.rootHolder.ToByteArray();
        }

        public void InitRoots(byte[] roots)
        {
            this.rootHolder.FromByteArray(roots);
        }

        private Root getActiveDatabaseRoot()
        {
            if (this.rootHolder.GetRoot(this.activeDatabaseName) == null)
            {
                var root = this.client.CurrentRoot(this.emptyArgument, this.getSecurityHeader());

                this.rootHolder.SetRoot(this.activeDatabaseName, root);
            }

            return this.rootHolder.GetRoot(this.activeDatabaseName);
        }

        private Metadata getSecurityHeader()
        {
            if (string.IsNullOrEmpty(this.authToken))
            {
                throw new System.Security.Authentication.AuthenticationException("You need to log in before performing this operation");
            }

            var metadata = new Metadata();

            metadata.Add("authorization", "Bearer " + this.authToken);

            return metadata;
        }

        #region IDisposable Support

        private bool disposedValue;

        protected virtual void Dispose(bool _disposing)
        {
            if (!this.disposedValue)
            {
                this.disposedValue = true;
                this.Close();
            }
        }

        ~ImmuClient()
        {
            this.Dispose(false);
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion IDisposable Support
    }
}