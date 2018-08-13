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

namespace TwitchLurkerBot {
    class Program {

        private static string blacklist, channels, giftsubs;
        private static ManualResetEvent OnConnectedEvent = new ManualResetEvent(false);
        private static List<TwitchClient> clients;
        private static TwitchAPI api;
        private static readonly string self = "llllllloyde_";
        private static readonly string oauth = "";
        private static readonly string client_id = "";


        static void Main(string[] args) {
            //setup the api
            api = new TwitchAPI();
            api.Settings.ClientId = client_id;
            api.Settings.AccessToken = oauth;

            //setup the client
            TwitchClient currentClient = new TwitchClient();
            ConnectionCredentials creds = new ConnectionCredentials(self, oauth);
            clients = new List<TwitchClient>();
            initializeFiles();
            cleanChannelsFile();
            initializeEvents(currentClient);
            currentClient.Initialize(creds);
            currentClient.Connect();
            OnConnectedEvent.WaitOne();
            clients.Add(currentClient);
            int fileCleanCounter = 5;
            while (true) {
                waitForFile(channels);
                string[] channelList = File.ReadAllLines(channels);

                //normalize the lists
                for (int i = 0; i < channelList.Length; i++) {
                    channelList[i] = channelList[i].ToLower();
                }

                //part from lists not in channel list or in blacklist
                foreach (JoinedChannel channel in getAllJoinedChannels()) {
                    if (!isInChannelList(channel.Channel.ToLower()) || isInBlacklist(channel.Channel.ToLower())) {
                        findClientConnectedTo(channel.Channel.ToLower()).LeaveChannel(channel);
                        Console.WriteLine($"Part from channel {channel.Channel}");
                    }
                }

                //join any channel thats only in the channellist with rudimentary error catching lul
                int joinQueueCounter = 0;
                foreach (string channel in channelList) {
                    // create a new client if connection limit is hit
                    if (joinQueueCounter >= 50) {
                        Console.WriteLine("Connection limit for current client hit, creating new client");
                        currentClient = new TwitchClient();
                        initializeEvents(currentClient);
                        currentClient.Initialize(creds);
                        currentClient.Connect();
                        OnConnectedEvent.WaitOne();
                        clients.Add(currentClient);
                        joinQueueCounter = 0;
                    }
                    if (!isInBlacklist(channel) && checkIfChannel(channel) && !checkIfAlreadyJoined(channel, getAllJoinedChannels())) {
                        currentClient.JoinChannel(channel);
                        ++joinQueueCounter;
                        Console.WriteLine($"Join channel {channel}");
                    }
                }

                //check every 15 seconds
                Console.WriteLine($"total channels at this time: {getAllJoinedChannels().Count}");
                Console.WriteLine($"total clients at this time: {clients.Count}");
                if (fileCleanCounter > 0)
                    Console.WriteLine($"Cleaning channels file in {fileCleanCounter} loops.");

                //clean channels file
                if (fileCleanCounter == 0) {
                    cleanChannelsFile();
                    fileCleanCounter = 5;
                }
                --fileCleanCounter;
                Thread.Sleep(30000);  // waiting 30s
                
            }
        }



        private static void cleanChannelsFile() {
            Console.WriteLine("cleaning channels file");
            int count = docleanChannelsFile();
            if (count > 0)
                Console.WriteLine($"removed {count} duplicates channel from the channels file");
            else
                Console.WriteLine("no duplicate lines found");
        }

        private static List<JoinedChannel> getAllJoinedChannels() {
            List<JoinedChannel> list = new List<JoinedChannel>();
            foreach (TwitchClient client in clients) {
                list = list.Concat(client.JoinedChannels).ToList();
            }
            return list;
        }

        private static TwitchClient findClientConnectedTo(string channel) {
            TwitchClient dummy = clients[0];
            channel = channel.ToLower();
            foreach (TwitchClient client in clients) {
                bool found = false;
                foreach (JoinedChannel toCheck in client.JoinedChannels) {
                    if (toCheck.Channel.ToLower().Equals(channel)) {
                        found = true;
                    }
                }
                if (found) {
                    return client;
                }
            }
            return dummy;
        }

        private static int docleanChannelsFile() {
            int oldLinesCount = 0, newLinesCount = 0;
            lock (channels) {
                waitForFile(channels);
                string[] lines = File.ReadAllLines(channels);
                oldLinesCount = lines.Length;
                string[] newlines = lines.Distinct().ToArray();
                newLinesCount = newlines.Length;
                File.WriteAllLines(channels, newlines);
            }
            return oldLinesCount - newLinesCount;
        }

        private static bool isInBlacklist(string channel) {
            channel = channel.ToLower();
            bool isMatch = false;
            lock (blacklist) {
                waitForFile(blacklist);
                using (StreamReader sr = File.OpenText(blacklist)) {
                    string[] lines = File.ReadAllLines(blacklist);
                    for (int x = 0; x < lines.Length - 1; x++) {
                        if (channel == lines[x]) {
                            sr.Close();
                            //Console.WriteLine($"found {channel} in blacklist");
                            isMatch = true;
                        }
                    }
                    if (!isMatch) {
                        sr.Close();
                    }
                }
            }

            return isMatch;
        }

        private static bool isInChannelList(string channel) {
            channel = channel.ToLower();
            bool isMatch = false;
            lock (channels) {
                waitForFile(channels);
                using (StreamReader sr = File.OpenText(channels)) {
                    string[] lines = File.ReadAllLines(channels);
                    for (int x = 0; x < lines.Length - 1; x++) {
                        if (channel == lines[x]) {
                            sr.Close();
                            //Console.WriteLine($"found {channel} in channels");
                            isMatch = true;
                        }
                    }
                    if (!isMatch) {
                        sr.Close();
                    }
                }
            }

            return isMatch;
        }

        private static bool checkIfAlreadyJoined(string channel, IReadOnlyList<JoinedChannel> joinedChannels) {
            List<string> list = new List<string>();
            foreach (JoinedChannel joinedChannel in joinedChannels) {
                list.Add(joinedChannel.Channel.ToLower());
            }
            return list.Contains(channel.ToLower());
        }

        private static void initializeEvents(TwitchClient client) {
            client.OnGiftedSubscription += Client_OnGiftedSubscription;
            client.OnMessageReceived += Client_OnMessageReceived;
            client.OnConnected += Client_OnConnected;
            client.OnFailureToReceiveJoinConfirmation += Client_OnFailureToReceiveJoinConfirmation;
        }

        private static void Client_OnFailureToReceiveJoinConfirmation(object sender, OnFailureToReceiveJoinConfirmationArgs e) {
            Console.WriteLine($"Failed to connect to {e.Exception.Channel}, adding it to blacklist");
            waitForFile(blacklist);
            File.AppendAllLines(blacklist, new string[] { e.Exception.Channel });
        }

        private static void Client_OnConnected(object sender, OnConnectedArgs e) {
            OnConnectedEvent.Set();
            OnConnectedEvent.Reset();
        }

        private static void Client_OnMessageReceived(object sender, OnMessageReceivedArgs e) {
            foreach (var item in e.ChatMessage.Badges) {
                if (item.Key.ToLower().Equals("partner"))
                    addChannel(e.ChatMessage.Username.ToLower());
            }
        }

        private static void addChannel(string channel) {

            bool isChannel = checkIfChannel(channel.ToLower());
            bool isBlacklisted = isInBlacklist(channel.ToLower());
            bool isJoined = isInChannelList(channel.ToLower());

            if (!isChannel || isBlacklisted || isJoined)
                return;

            Console.WriteLine($"Adding channel {channel}");
            lock (channels) {
                waitForFile(channels);
                File.AppendAllLines(channels, new string[] { channel.ToLower() });
            }
        }

        private static void Client_OnGiftedSubscription(object sender, OnGiftedSubscriptionArgs e) {
            if (!e.GiftedSubscription.MsgParamRecipientDisplayName.ToLower().Equals(self))
                return;
            string toWrite = $"Got a {e.GiftedSubscription.MsgParamSubPlan} {e.GiftedSubscription.MsgParamMonths} Month sub in channel {e.Channel} by {e.GiftedSubscription.DisplayName}";
            Console.WriteLine(toWrite);
            string[] linesToWrite = { toWrite };
            waitForFile(giftsubs);
            File.AppendAllLines(giftsubs, linesToWrite);
        }

        private static void initializeFiles() {
            channels = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "channels.txt");
            blacklist = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "blacklist.txt");
            giftsubs = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "giftsubs.txt");
            if (!File.Exists(channels)) {
                File.Create(channels);
                File.WriteAllLines(channels, new string[] { "// list of all channels to join" , "llllllloyde_" });
            }
            if (!File.Exists(blacklist)) {
                File.Create(blacklist);
                File.WriteAllLines(blacklist, new string[] { "// list of all channels NOT to join (overrides channels.txt)" });
            }
            if (!File.Exists(giftsubs)) {
                File.Create(giftsubs);
                File.WriteAllLines(giftsubs, new string[] { "// all gotten giftsubs" });
            }
            Console.WriteLine($"using channel path: {channels}");
            Console.WriteLine($"using blacklist path: {blacklist}");
            Console.WriteLine($"using giftsubs path: {giftsubs}");
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
                Console.WriteLine($"Couldnt access {Path.GetFileName(filename)}, retrying in 2 seconds");
                Thread.Sleep(2000);
            }
        }

    }
}
