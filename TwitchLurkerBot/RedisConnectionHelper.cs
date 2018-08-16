using ServiceStack.Redis;
using ServiceStack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TwitchLurkerBot {
    public class RedisConnectorHelper {

        private static BasicRedisClientManager manager;

        private static readonly object SyncLock = new object();

        public static IRedisClient GetDatabase() {
            if (manager == null) {
                lock (SyncLock) {
                    try {
                        manager = new BasicRedisClientManager("localhost");
                    }
                    catch (Exception e) {
                        Console.WriteLine(e);
                        Console.WriteLine(e.InnerException);
                        return null;
                    }
                }
            }
            return manager.GetClient();
        }
    }
}
