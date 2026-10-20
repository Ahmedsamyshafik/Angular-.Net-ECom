using System.Text.Json;

using Ecom.Core.DTO.CartDto.CartModels;
using Ecom.Core.Interfaces;


using StackExchange.Redis;

namespace ECom.Infrastructure.Redis
{
    public class RedisCartRepository : ICartRepository
    {
        private readonly IDatabase _database;
        public RedisCartRepository(IConnectionMultiplexer connection)
        {
            _database = connection.GetDatabase();
        }

        //Get
        public async Task<Cart?> GetCartAsync(string id, CancellationToken cancellationToken = default)
        {
            //try catch
            try
            {
            var cartJson = await _database.StringGetAsync(id);

            if (cartJson.IsNullOrEmpty)
                return null;

            return JsonSerializer.Deserialize<Cart>(cartJson.ToString()!);

            }
            catch
            {
                return null;
            }
        }
        //Set & Update
        public async Task<bool> SetCartAsync(string id, Cart cart, CancellationToken cancellationToken = default)
        {
            var cartJson = JsonSerializer.Serialize(cart);

            return await _database.StringSetAsync(
                id,
                cartJson);
        }
        //Delete
        public async Task<bool> DeleteCartAsync(string id, CancellationToken cancellationToken = default)
        {
            var res = await _database.KeyDeleteAsync(id);
            return res;
        }




    }
}
