using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ServiceStack.Redis;

namespace TwitchLurkerBot {
    class Redis {
        private static IRedisClient database;
        private static readonly string channelsSet = "channelsSet", channelsList = "channelsList",
            blacklist = "blacklist", blackSet = "blackSet", subgifts = "subgifts", gifters = "gifters", subs_name_ids = "subs_name_ids",
            joinedChannelSet = "joined_channel";

        public static IRedisClient Database { get => RedisConnectorHelper.GetDatabase(); set => database = value; }

        public static void initialize() {
            var db = Database;
            db.Remove(joinedChannelSet);
        }


        public static void addChannel(string channel, string foundby) {
            var db = Database;
            db.SetEntryInHash("channel:" + channel, "found_by", foundby);
            db.AddItemToList(channelsList, channel);
            db.AddItemToSet(channelsSet, channel);
        }

        public static void addBlacklist(string channel, string reason) {
            var db = Database;
            if (db.SetContainsItem(channelsSet, channel)) {
                db.RemoveItemFromSet(channelsSet, channel);
                db.RemoveItemFromList(channelsList, channel);
            }
            db.SetEntryInHash("channel:" + channel, "blacklisted_reason", reason);
            db.AddItemToList(blacklist, channel);
            db.AddItemToSet(blackSet, channel);
        }

        public static void addGiftsub(subgift gift) {
            var db = Database;
            long id;
            id = db.GetListCount(subgifts);
            KeyValuePair<string, string> channel, gifter, tier, month, money;
            channel = new KeyValuePair<string, string>("channel", gift.channel);
            gifter = new KeyValuePair<string, string>("gifter", gift.gifter);
            tier = new KeyValuePair<string, string>("tier", gift.tier.ToString());
            month = new KeyValuePair<string, string>("month", gift.month.ToString());
            money = new KeyValuePair<string, string>("money", gift.money.ToString());
            KeyValuePair<string, string>[] set = new KeyValuePair<string, string>[] { channel, gifter, tier, month, money };
            addMoney(gift.money);
            db.Remove("latest_subgift");
            db.SetRangeInHash("latest_subgift", set);
            db.SetRangeInHash(id.ToString(), set);
            db.AddItemToList(gift.gifter, id.ToString());
            db.AddItemToList(subgifts, id.ToString());
            db.IncrementItemInSortedSet(gifters, gift.gifter, 1);
        }

        public static gifter getTopGifter() {
            var db = Database;
            Console.WriteLine(db.GetRangeFromSortedSetByHighestScore(gifters, 0, long.MaxValue).Count);
            string user = db.GetRangeFromSortedSetByHighestScore(gifters, 0, long.MaxValue)[0].ToString();
            gifter toReturn = new gifter(user);
            List<string> values = db.GetAllItemsFromList(user);
            foreach (string id in values) {
                string gift = db.GetValueFromHash(id.ToString(), "tier");
                toReturn.count++;
                int tier;
                if (!int.TryParse(gift, out tier))
                    tier = 0;
                switch (tier) {
                    case 1:
                        toReturn.money += metrics.tier1;
                        break;
                    case 2:
                        toReturn.money += metrics.tier2;
                        break;
                    case 3:
                        toReturn.money += metrics.tier3;
                        break;
                }
            }
            return toReturn;
        }

        public static float getTotalSubMoney() {
            var db = Database;
            long cents, EUR;
            long.TryParse(db.GetValue("cent").ToString(), out cents);
            cents = cents / 100;
            long.TryParse(db.GetValue("EUR").ToString(), out EUR);
            return (EUR + cents);
        }

        public static bool isInBlacklist(string channel) {
            return Database.SetContainsItem(blackSet, channel);
        }

        public static bool isInChannelsList(string channel) {
            return Database.SetContainsItem(channelsSet, channel);
        }

        public static bool isInJoinedList(string channel) {
            return Database.SetContainsItem(joinedChannelSet, channel);
        }

        public static void joinChannel(string channel) {
            Database.AddItemToSet(joinedChannelSet, channel);
        }

        public static void partChannel(string channel) {
            Database.RemoveItemFromSet(joinedChannelSet, channel);
        }

        public static subgift getLatestSubgift() {
            List<string> set = new List<string>();
            set = Database.GetValuesFromHash("latest_subgift", new string[] { "gifter", "channel", "tier", "month", "money" });
            string gifter, channel;
            int tier, month;
            float money;
            gifter = set[0];
            channel = set[1];
            tier = int.Parse(set[2]);
            month = int.Parse(set[3]);
            money = float.Parse(set[4]);
            long _tier, _month, _money;
            long.TryParse(tier.ToString(), out _tier);
            long.TryParse(month.ToString(), out _month);
            long.TryParse(money.ToString(), out _money);
            return new subgift(gifter.ToString(), channel.ToString(), (int)_tier, (int)_month, _money);

        }

        private static void addMoney(float amount) {
            int cents, centstoset = 0;
            if (int.TryParse(Database.GetValue("cent"), out cents)) {
                centstoset = ((int)(amount * 100) % 100) + cents;
                if (centstoset > 100) {
                    amount += 1;
                    centstoset -= 100;
                }
            }
            Database.SetEntry("cent", centstoset.ToString());
            Database.IncrementValueBy("EUR", (int)amount);
        }

        public static int getChannelCount() {
            return (int)Database.GetSetCount(channelsSet);
        }

        public static int getSubGiftCount() {
            return (int)Database.GetListCount(subgifts);
        }

        public static List<string> getAllDiscoveredChannels() {
            return Database.GetAllItemsFromList(channelsList);
        }
    }
}
