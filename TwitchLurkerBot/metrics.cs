using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Reflection;

namespace TwitchLurkerBot {
    public static class metrics {
        static private int channelCount, subgiftcount;
        static private float dollarsSpent;
        static private subgift latest;
        static public readonly float tier1 = 4.99f, tier2 = 9.99f, tier3 = 24.99f;

        public static void initialize() {
            int count = 1;
            foreach (string channel in Redis.getAllDiscoveredChannels()) {
                Program.joinChannel_async(channel);
                if (count % 100 == 0)
                    Console.WriteLine($"Joined {count} channels");
                count++;
            }

        }

        public static void tryparsegift(string gift, out subgift newGift) {
            //Got a Tier1 1 Month sub in channel squillakilla by Subaru_Kayak
            string gifter, channel;
            int tier, month;
            newGift = null;
            try {
                gifter = gift.Split(' ')[10];
                channel = gift.Split(' ')[8];
                tier = int.Parse(gift.Split(' ')[2].Last().ToString());
                month = int.Parse(gift.Split(' ')[3]);
                newGift = new subgift(gifter: gifter, channel: channel, tier: tier, month: month);
            }
            catch (Exception e) {
                Console.WriteLine(e);
                return;
            }
        }
    }
}
