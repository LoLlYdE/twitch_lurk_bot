using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using TwitchLib.Api;
using TwitchLib.Client;
using TwitchLib.Client.Models;
using System.Reflection;
using System.Threading;
using TwitchLib.Client.Events;
using TwitchLib.Api.Models.Helix.Streams.GetStreams;
using TwitchLib.Api.Models.Helix.Users.GetUsers;
using System.Threading.Tasks;
using System.Configuration;

namespace TwitchLurkerBot {
    class Program {

        private static ManualResetEvent DrawEvent = new ManualResetEvent(true);
        private static List<TwitchClient> clients;
        private static TwitchAPI api;
        private static string self, oauth, client_id;
        private static int min_viewers, discovery_time_in_ms, max_connections_per_client;
        private static int limit = 700;
        public static int currentChannelCounter;
        private static ConnectionCredentials creds;
        public static TimerPlus discoveryTimer;

        static void Main(string[] args) {
            //setup confid
            Console.WriteLine("loading config");
            self = ConfigurationManager.AppSettings["self"];
            oauth = ConfigurationManager.AppSettings["oauth"];
            client_id = ConfigurationManager.AppSettings["client_id"];
            min_viewers = int.Parse(ConfigurationManager.AppSettings["min_viewers"]);
            discovery_time_in_ms = int.Parse(ConfigurationManager.AppSettings["discovery_time_in_ms"]);
            max_connections_per_client = int.Parse(ConfigurationManager.AppSettings["max_connections_per_client"]);

            //setup the api
            Console.WriteLine("inizialize api");
            api = new TwitchAPI();
            api.Settings.ClientId = client_id;
            api.Settings.AccessToken = oauth;

            //initialize things
            Console.WriteLine("inizialize things");
            TwitchClient currentClient = new TwitchClient();
            creds = new ConnectionCredentials(self, oauth);
            clients = new List<TwitchClient>();
            initializeEvents(currentClient);
            currentClient.Initialize(creds);
            currentChannelCounter = 0;

            //setup the client
            Console.WriteLine("inizialize setting up client");
            currentClient.Connect();
            clients.Add(currentClient);

            //setup redis 
            Console.WriteLine("inizialize Redis");
            Redis.initialize();

            //initialize metrics (duuhh...)
            Console.WriteLine("inizialize metrics");
            metrics.initialize();

            // start tasks
            Console.WriteLine("inizialize tasks");
            discoveryTimer = new TimerPlus();
            discoveryTimer.AutoReset = true;
            discoveryTimer.Interval = discovery_time_in_ms;
            discoveryTimer.Elapsed += DiscoveryTimer_Elapsed;
            discoveryTimer.Start();

            TimerPlus uiUpdate = new TimerPlus();
            uiUpdate.AutoReset = true;
            uiUpdate.Interval = 1000; // 1 second
            uiUpdate.Elapsed += UiUpdate_Elapsed;
            uiUpdate.Start();


            while (true) {
                DrawEvent.WaitOne();
                DrawEvent.Reset();
                ui.draw();
            }
        }

        private static void UiUpdate_Elapsed(object sender, System.Timers.ElapsedEventArgs e) {
            DrawEvent.Set();
        }

        private static void DiscoveryTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e) {
            addChannelsActively();
            DrawEvent.Set();
        }

        private async static void partFrom_async(string channel) {
            foreach (TwitchClient client in clients) {
                client.LeaveChannel(channel);
            }
            Redis.partChannel(channel);
            DrawEvent.Set();
        }

        public async static void joinChannel_async(string channel) {
            lock (currentChannelCounter as object) {
                if (currentChannelCounter == max_connections_per_client) {
                    TwitchClient newone = new TwitchClient();
                    initializeEvents(newone);
                    newone.Initialize(creds);
                    newone.Connect();
                    clients.Add(newone);
                    doJoinChannel(newone, channel);
                    currentChannelCounter = 1;
                }
                else {
                    doJoinChannel(clients.Last(), channel);
                    currentChannelCounter++;
                }
            }
            DrawEvent.Set();
        }

        private static void doJoinChannel(TwitchClient client, string channel) {
            while (!client.IsConnected) {
                Thread.Sleep(100);
            }
            Redis.joinChannel(channel);
            client.JoinChannel(channel);
        }

        public static int addChannelsActively() {
            List<TwitchLib.Api.Models.Helix.Streams.GetStreams.Stream> list = getTopChannels();
            List<string> listToAdd = new List<string>();
            List<string> listToQuery = new List<string>();
            int counter = 99;
            foreach (var item in list) {
                if (counter != 0) {
                    listToQuery.Add(item.UserId);
                }
                else {
                    counter = 99;
                    listToAdd = listToAdd.Concat(getNamesFromIDs(listToQuery)).ToList();
                    listToQuery.RemoveRange(0, listToQuery.Count);
                }
                --counter;
            }

            foreach (string item in listToAdd) {
                addChannel(item, "found in directory");
            }
            return listToAdd.Count;
        }

        private static List<string> getNamesFromIDs(List<string> userIds) {
            Task<GetUsersResponse> resp = api.Users.helix.GetUsersAsync(ids: userIds);
            resp.Wait();
            List<string> toReturn = new List<string>();
            foreach (User item in resp.Result.Users) {
                toReturn.Add(item.Login);
            }
            return toReturn;
        }

        private static List<TwitchLib.Api.Models.Helix.Streams.GetStreams.Stream> getTopChannels() {
            List<TwitchLib.Api.Models.Helix.Streams.GetStreams.Stream> list = new List<TwitchLib.Api.Models.Helix.Streams.GetStreams.Stream>();
            bool done = false;
            string next = "";
            while (!done) {
                GetStreamsResponse resp = FetchChannels(next);
                List<TwitchLib.Api.Models.Helix.Streams.GetStreams.Stream> toAdd = resp.Streams.ToList();
                list = list.Concat(toAdd).ToList();
                done = toAdd.Last().ViewerCount < min_viewers;
                next = resp.Pagination.Cursor;
                Thread.Sleep(limit);
            }

            return list;
        }

        private static GetStreamsResponse FetchChannels(string next) {
            if(next.Length < 1) {
                Task<GetStreamsResponse> task = api.Streams.helix.GetStreamsAsync(first: 100);
                task.Wait();
                return task.Result;
            }
            else {
                Task<GetStreamsResponse> task = api.Streams.helix.GetStreamsAsync(first: 100, after: next);
                task.Wait();
                return task.Result;
            }
        }

        private static bool isInBlacklist(string channel) {
            channel = channel.ToLower();
            return Redis.isInBlacklist(channel);
        }

        private static bool isInChannelList(string channel) {
            channel = channel.ToLower();
            return Redis.isInChannelsList(channel);
        }

        private static void initializeEvents(TwitchClient client) {
            client.OnGiftedSubscription += Client_OnGiftedSubscription;
            client.OnMessageReceived += Client_OnMessageReceived;
            client.OnFailureToReceiveJoinConfirmation += Client_OnFailureToReceiveJoinConfirmation;
        }

        private static void Client_OnFailureToReceiveJoinConfirmation(object sender, OnFailureToReceiveJoinConfirmationArgs e) {
            //Console.WriteLine($"Failed to connect to {e.Exception.Channel}, adding it to blacklist");
            addToBlacklist(e.Exception.Channel, "failed to connect");
        }

        private static void addToBlacklist(string channel, string reason) {
            partFrom_async(channel);
            Redis.addBlacklist(channel, reason);
        }

        private static void Client_OnMessageReceived(object sender, OnMessageReceivedArgs e) {
            foreach (var item in e.ChatMessage.Badges) {
                if (item.Key.ToLower().Equals("partner"))
                    addChannel(e.ChatMessage.Username.ToLower(), "talked in chat");
            }
            if (e.ChatMessage.Message.ToLower().Contains(self.ToLower())) {
                string channel = e.ChatMessage.Channel, user = e.ChatMessage.Username;
                addToBlacklist(channel, "got mentioned in");
                addToBlacklist(user, "mentioned me");
            }
        }

        public static void addChannel(string channel, string reason) {

            bool isChannel = checkIfChannel(channel.ToLower());
            bool isBlacklisted = isInBlacklist(channel.ToLower());
            bool isJoined = Redis.isInJoinedList(channel.ToLower());


            if (!isChannel || isBlacklisted ||isJoined)
                return;

            Redis.addChannel(channel, reason);
            joinChannel_async(channel);
        }

        private static void Client_OnGiftedSubscription(object sender, OnGiftedSubscriptionArgs e) {
            if (!e.GiftedSubscription.MsgParamRecipientDisplayName.ToLower().Equals(self))
                return;
            string toWrite = $"Got a {e.GiftedSubscription.MsgParamSubPlan} {e.GiftedSubscription.MsgParamMonths} Month sub in channel {e.Channel} by {e.GiftedSubscription.DisplayName}";
            //Console.WriteLine(toWrite);
            string[] linesToWrite = { toWrite };
            subgift next;
            metrics.tryparsegift(toWrite, out next);
            Redis.addGiftsub(next);
            DrawEvent.Set();
        }

        static bool ContainsOnly(string stringToCheck, char[] contains) {
            for (int i = 0; i < stringToCheck.Length; i++) {
                if (!contains.Contains(stringToCheck.ToCharArray()[i])) 
                    return false;
            }
            return true;
        }

        static bool checkIfChannel(string toCheck) {
            char[] legalChars = "abcdefghijklmnopqrstuvwxyz0123456789_".ToCharArray();
            toCheck = toCheck.ToLower();
            bool a, b, c, d;
            a = toCheck.Split(' ').Length == 1;
            b = ContainsOnly(toCheck, legalChars);
            c = toCheck.Length >= 3;
            d = toCheck.Length <= 25;
            return (a && b && c && d);
        }

        public static bool isFileReady(string filename) {
            try {
                using (FileStream inputStream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.None))
                    return inputStream.Length > 0;
            }
            catch (Exception e) {
                Console.WriteLine(e);
                return false;
            }
        }
        public static void waitForFile(string filename) {
            while (!isFileReady(filename)) {
                //Console.WriteLine($"Couldnt access {Path.GetFileName(filename)}, retrying in 2 seconds");
                Thread.Sleep(2000);
            }
        }

    }
}
