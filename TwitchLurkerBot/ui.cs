using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TwitchLurkerBot {
    static public class ui {
        public static void draw() {
            int width = Console.WindowWidth, height = Console.WindowHeight;
            List<string> lines = new List<string>();
            lines.Add(getTitle(width));
            lines.Add("");
            lines.Add(getTopGifter());
            lines.Add("");
            lines.Add(getLatestSubGift());
            lines.Add("");
            lines.Add(getTotalSubMoney());
            lines.Add("");
            lines.Add(getDiscoveredChannelCount());
            lines.Add("");
            lines.Add(getDiscoverChannelLine());
            lines = lines.Concat(getPaddingLines(height - lines.Count - 1)).ToList();
            foreach (string line in lines) {
                Console.WriteLine(line);
            }
        }

        private static string getDiscoverChannelLine() {
            if(Program.discoveryTimer.TimeLeft > 0) {
                return $"discovering new channels in {TimeSpan.FromMilliseconds(Program.discoveryTimer.TimeLeft).ToString(@"mm\:ss")} ";
            }
            else {
                return "discovery in progress";
            }
        }

        private static IEnumerable<string> getPaddingLines(int v) {
            List<string> list = new List<string>();

            for (int i = 0; i < v; i++) {
                list.Add("");
            }

            return list;
        }

        private static string getTopGifter() {
            gifter topGifter = Redis.getTopGifter();
            return $"top gifter is {topGifter.user}, with {topGifter.money.ToString("c2")}EUR in {topGifter.count} gifts total.";
        }

        private static string getDiscoveredChannelCount() {
            return "total joined channels: " + Redis.getChannelCount();
        }

        private static string getTotalSubMoney() {
            return "total submoney: " + Redis.getTotalSubMoney().ToString("c2") + "EUR in " + Redis.getSubGiftCount() + " subgifts";
        }

        private static string getLatestSubGift() {
            return "latest subgift: " + Redis.getLatestSubgift();
        }

        private static string getTitle(int width) {
            string title = "TwitchLurkerBot";
            int padding = (width - title.Length) / 2;
            string line = getNTimesX(padding, '-') + title + getNTimesX(padding, '-');
            if (width % 2 == 0)
                line = line + "-";
            return line;
        }

        private static string getNTimesX(int n, char x) {
            string toReturn = "";
            for (int i = 0; i < n; i ++) {
                toReturn = toReturn + x;
            }
            return toReturn;
        }
    }
}
