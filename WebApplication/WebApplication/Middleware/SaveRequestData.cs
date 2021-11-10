using Microsoft.AspNetCore.Http;
using MongoDB.Bson;
using MongoDB.Driver;
using Newtonsoft.Json;
using StackExchange.Redis;

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace WebApplication.Middleware
{
    public class SaveRequestData
    {
        private MongoClient _mongoClient;
        private IConnectionMultiplexer _multiplexer;
        private Random _random;

        private const string _redisCacheKey = "monga_cache";

        public SaveRequestData(RequestDelegate next, MongoClient mongoClient, IConnectionMultiplexer multiplexer)
        {
            _mongoClient = mongoClient;
            _multiplexer = multiplexer;
            _random = new Random();
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var db = _mongoClient.GetDatabase("RequestData");
            var collection = db.GetCollection<BsonDocument>("data");

            using (var reader = new StreamReader(context.Request.Body))
            {
                var document = new BsonDocument
                {
                    { "request_id", context.TraceIdentifier },
                    { "request_body", await reader.ReadToEndAsync() },
                };
                await collection.InsertOneAsync(document);

                if (context.Request.Body.CanSeek) context.Request.Body.Seek(0, SeekOrigin.Begin);
            }

            var dbRedis = _multiplexer.GetDatabase(0);

            var ttl = await dbRedis.KeyExistsAsync(_redisCacheKey) ? await dbRedis.KeyTimeToLiveAsync(_redisCacheKey) : null;
            long? delta = null;

            if (ttl.HasValue)
            {
                var cache = await dbRedis.StringGetAsync(_redisCacheKey);

                if (cache.HasValue)
                {
                    var response = JsonConvert.DeserializeObject<CacheObj>(cache);
                    delta = response.Delta;

                    if (DateTimeOffset.Now.ToUnixTimeSeconds() - response.Delta * 1 * Math.Log(_random.Next(0, 1)) < ttl.Value.Ticks)
                    {
                        await context.Response.WriteAsync($"<html><body>Data saved. Data get from cache:<br />{response.Data}</body></html>");
                        return;
                    }
                }
            }

            var data = string.Join("<br />", collection.Aggregate().ToList().Select(q => JsonConvert.SerializeObject(q)));
            await dbRedis.StringSetAsync(_redisCacheKey, JsonConvert.SerializeObject(new CacheObj
            {
                Delta = (delta ?? DateTimeOffset.Now.ToUnixTimeSeconds()) - DateTimeOffset.Now.ToUnixTimeSeconds(),
                Data = data
            }), TimeSpan.FromSeconds(20));
            await context.Response.WriteAsync($"<html><body>Data saved. Data get from DB:<br />{data}</body></html>");
            return;
        }
    }
}
