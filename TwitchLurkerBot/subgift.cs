using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TwitchLurkerBot {
    public class subgift {
        public string channel, gifter;
        public int tier, month;
        public float money;

        public subgift(string gifter, string channel, int tier, int month) {
            this.channel = channel;
            this.gifter = gifter;
            this.tier = tier;
            this.month = month;
            switch (tier) {
                case 1:
                    money = metrics.tier1;
                    break;
                case 2:
                    money = metrics.tier2;
                    break;
                case 3:
                    money = metrics.tier3;
                    break;
            }
        }

        public subgift(string gifter, string channel, int tier, int month, float money) {
            this.channel = channel;
            this.gifter = gifter;
            this.tier = tier;
            this.month = month;
            this.money = money;
        }

        public override string ToString() {
            return $"from {gifter} in {channel} tier {tier} month {month} worth {money.ToString("c2")}";
        }
    }
}
