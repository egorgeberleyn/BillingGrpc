using Grpc.Core;
namespace Billing.Services
{
    public class BillingService : Billing.BillingBase
    {
        private static List<UserProfile> _profiles = new()
        {
            new UserProfile { Name = "boris", Rating = 5000 },
            new UserProfile { Name = "maria", Rating = 1000 },
            new UserProfile { Name = "oleg", Rating = 800 }
        };
        private static List<Coin> _coins = new();
        private static int _count = 0;

        public override async Task ListUsers(None request, IServerStreamWriter<UserProfile> responseStream, ServerCallContext context)
        {
            foreach (var profile in _profiles)                           
                await responseStream.WriteAsync(profile);                            
        }

        public override async Task<Response> CoinsEmission(EmissionAmount request, ServerCallContext context)
        {
            if (request.Amount < _profiles.Count)
                return await Task.FromResult(new Response
                {
                    Status = Response.Types.Status.Failed,
                    Comment = "недостаточно монет для выполнения эмиссии"
                });
                       
            AllocateCoins(request.Amount);
            return await Task.FromResult(new Response
            {
                Status = Response.Types.Status.Ok,
                Comment = "эмиссия произведена"
            });
        }

        public override async Task<Response> MoveCoins(MoveCoinsTransaction request, ServerCallContext context)
        {                        
            var srcUser = GetProfileByName(request.SrcUser);
            var distUser = GetProfileByName(request.DstUser);
            
            if(srcUser is null || distUser is null)
                return await Task.FromResult(new Response
                {
                    Status = Response.Types.Status.Failed,
                    Comment = "отправитель или получатель не найден"
                });

            if (srcUser.Amount < request.Amount)
                return await Task.FromResult(new Response
                {
                    Status = Response.Types.Status.Failed,
                    Comment = "на балансе меньше монет"
                });

            srcUser.Amount -= request.Amount;
            distUser.Amount += request.Amount;
            ChangeUserHistory(request);
                        
            return await Task.FromResult(new Response
            {
                Status = Response.Types.Status.Ok,
                Comment = "транзакция завершена"
            });
        }

        public override async Task<Coin> LongestHistoryCoin(None request, ServerCallContext context)
        {
            var maxCoinByHistory = _coins.MaxBy(c => c.History.Split(' ').Length);
            return await Task.FromResult(maxCoinByHistory);
        }


        //Helpers
        private void AllocateCoins(long amount)
        {            
            var totalRating = _profiles.Sum(p => p.Rating);
            var coinsForCounting = amount - _profiles.Count;
            
            for(int i = 0; i < _profiles.Count; i++)
            {
                long userCoins = 0;
                if (i == _profiles.Count - 1)
                {
                    userCoins = amount - _count - 1;
                    AllocateToUser(userCoins, _profiles[i]);
                }
                else
                {
                    userCoins = Convert.ToInt32(_profiles[i].Rating / (totalRating / 100d) * (coinsForCounting / 100d));
                    AllocateToUser(userCoins, _profiles[i]);
                }
            }
        }

        private void AllocateToUser(long count, UserProfile profile)
        {
            profile.Amount += 1;
            _coins.Add(new Coin() { Id = ++_count, History = $"{profile.Name} " });

            for (int i = 0; i < count; i++)
            {
                profile.Amount += 1;
                _coins.Add(new Coin() { Id = ++_count, History = $"{profile.Name} " });
            }
        }

        private UserProfile GetProfileByName(string name) => _profiles.FirstOrDefault(p => p.Name == name);

        private void ChangeUserHistory(MoveCoinsTransaction request) =>
            _coins.Where(c => c.History.Split(' ')[^2] == request.SrcUser)
            .Take((int)request.Amount)
            .Select(c => c.History += $"{request.DstUser} ")
            .ToList();            
    }
}
