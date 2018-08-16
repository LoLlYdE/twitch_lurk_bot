using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TwitchLurkerBot {
    class gifter {
        public readonly string user;
        public int count;
        public float money;

        public gifter(string name) {
            user = name;
            count = 0;
            money = 0f;
        }
    }
}
