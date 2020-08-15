using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Web;
using Debugger = HunterPie.Logger.Debugger;

namespace HunterPie.Core
{
    public class Synchandler
    {
        private const string serverUrl = "http://mhwsync.herokuapp.com";

        private int delay
        {
            get => UserSettings.PlayerConfig.HunterPie.Sync.Delay;
        }

        private int retries = 5;
        private Thread syncThreadReference;
        private static bool stopThread = false;
        private string sessionUrlString = "";
        private string _sessionID = "";

        public string sessionID
        {
            get => _sessionID;
            set
            {
                _sessionID = HttpUtility.UrlEncode(value);
                sessionUrlString = serverUrl + "/session/" + sessionID + partyLeader;
            }
        }

        private string _partyLeader = "";

        public string partyLeader
        {
            get => _partyLeader;
            set
            {
                _partyLeader = HttpUtility.UrlEncode(value);
                sessionUrlString = serverUrl + "/session/" + sessionID + partyLeader;
            }
        }

        public bool isInParty { get; set; } = false;
        public bool isPartyLeader { get; set; } = false;
        public List<List<Part>> parts { get; set; } = new List<List<Part>>(3);
        public List<List<Ailment>> ailments { get; set; } = new List<List<Ailment>>(3);

        public Synchandler()
        {
            for (int i = 0; i < 3; i++)
            {
                parts.Add(new List<Part>());
                ailments.Add(new List<Ailment>());
            }
        }

        ~Synchandler()
        {
            stopSyncThread();
        }

        public void startSyncThread()
        {
            syncThreadReference = new Thread(new ThreadStart(syncThread));
            syncThreadReference.Start();
        }

        public static void stopSyncThread()
        {
            stopThread = true;
        }

        private string get(string url)
        {
            try
            {
                Debug.Assert(!string.IsNullOrEmpty(url));
                WebRequest request = WebRequest.Create(Uri.EscapeUriString(url));
                WebResponse response = request.GetResponse();
                Stream stream = response.GetResponseStream();
                StreamReader reader = new StreamReader(stream);
                string str = reader.ReadToEnd();
                reader.Close();
                response.Close();
                return str;
            }
            catch (Exception e)
            {
                Debugger.Error("[Sync] " + e.GetType() + " occurred in get(" + url + "): " + e.Message);
                return "false";
            }
        }

        private void syncThread()
        {
            //check if server can be reached
            while (!isServerAlive())
            {
                if (stopThread) //check if thread should stop
                {
                    return;
                }
                Debugger.Error("[Sync] Could not reach server, " + retries + " retries remaining");
                if (retries-- > 0)
                {
                    Thread.Sleep(500);
                }
                else
                {
                    Debugger.Error("[Sync] Giving up on trying to reach server");
                    return;
                }
            }

            Debugger.Log("[Sync] Connected to server");

            bool msgShown = false;

            while (!stopThread)
            {
                //check if party leader has synchronisation enabled, if not continuously check if anything has changed
                isInParty = partyExists();
                if (!isInParty)
                {
                    msgShown = false;
                }
                while (!isInParty)
                {
                    if (stopThread) //check if thread should stop
                    {
                        return;
                    }
                    isInParty = partyExists();
                    Thread.Sleep(1000);
                }

                //party leader has synchronisation enabled
                if (!msgShown)
                {
                    Debugger.Log("[Sync] Entered session");
                    msgShown = true;
                }
                if (isPartyLeader) //send data to server
                {
                    pushAllPartHP();
                    pushAllAilmentBuildup();
                }
                else //request data from server
                {
                    pullAllPartHP();
                    pullAllAilmentBuildup();
                }
                Thread.Sleep(delay);
            }

            quitSession();
        }

        public bool isServerAlive()
        {
            if (get(serverUrl) == "its alive")
            {
                return true;
            }
            return false;
        }

        public bool createPartyIfNotExist()
        {
            if (partyExists())
            {
                return true;
            }

            if (get(sessionUrlString + "/create") == "true")
            {
                Debugger.Log("[Sync] Created session");
                return true;
            }

            return false;
        }

        public bool partyExists()
        {
            if (string.IsNullOrEmpty(sessionUrlString) || string.IsNullOrEmpty(partyLeader))
            {
                return false;
            }
            if (get(sessionUrlString + "/exists") == "true")
            {
                return true;
            }
            return false;
        }

        public void quitSession()
        {
            if (isPartyLeader)
            {
                get(sessionUrlString + "/delete");
            }
            sessionID = "";
            partyLeader = "";
            isInParty = false;
            isPartyLeader = false;
            Debugger.Log("[Sync] Left session");
        }

        public void clearMonster(int monsterIndex)
        {
            get(sessionUrlString + "/monster/" + monsterIndex + "/clear");
        }

        public void pushPartHP(int monsterIndex, int partIndex)
        {
            try
            {
                float hp = parts[monsterIndex][partIndex].Health;
                if (float.IsNaN(hp))
                {
                    hp = 0;
                }
                string result = get(sessionUrlString + "/monster/" + monsterIndex + "/part/" + partIndex + "/hp/" + (int)hp);
                if (result != "true")
                {
                    if ((result == "error: session does not exist" && !isInParty) || result == "error 404") //if quitSession has been called while executing this function
                    {
                        return;
                    }
                    Debugger.Error("[Sync] pushPartHP: " + result);
                }
            }
            catch (IndexOutOfRangeException) //has only occurred on quest start and end so far
            {
                Debugger.Error("[Sync] IndexOutOfRangeException in pushPartHP");
            }
        }

        public void pushAilmentBuildup(int monsterIndex, int ailmentIndex)
        {
            try
            {
                float buildup = ailments[monsterIndex][ailmentIndex].Buildup;
                if (float.IsNaN(buildup))
                {
                    buildup = 0;
                }
                string result = get(sessionUrlString + "/monster/" + monsterIndex + "/ailment/" + ailmentIndex + "/buildup/" + (int)buildup);
                if (result != "true")
                {
                    if ((result == "error: session does not exist" && !isInParty) || result == "error 404") //if quitSession has been called while executing this function
                    {
                        return;
                    }
                    Debugger.Error("[Sync] pushAilmentBuildup: " + result);
                }
            }
            catch (IndexOutOfRangeException) //has only occurred on quest start and end so far
            {
                Debugger.Error("[Sync] IndexOutOfRangeException in pushAilmentBuildup");
            }
        }

        public void pullPartHP(int monsterIndex, int partIndex)
        {
            string result = get(sessionUrlString + "/monster/" + monsterIndex + "/part/" + partIndex + "/hp");
            try
            {
                parts[monsterIndex][partIndex].Health = int.Parse(result);
            }
            catch (IndexOutOfRangeException) //has only occurred on quest start and end so far
            {
                Debugger.Error("[Sync] IndexOutOfRangeException in pullPartHP");
            }
            catch (FormatException e)
            {
                if (result == "error: session does not exist") //if quitSession has been called while executing this function
                {
                    return;
                }
                Debugger.Error("[Sync] Exception occurred in pullPartHP: " + e.Message);
                Debugger.Error("[Sync] Return value: " + result);
            }
        }

        public void pullAilmentBuildup(int monsterIndex, int ailmentIndex)
        {
            string result = get(sessionUrlString + "/monster/" + monsterIndex + "/ailment/" + ailmentIndex + "/buildup");
            try
            {
                ailments[monsterIndex][ailmentIndex].Buildup = int.Parse(result);
            }
            catch (IndexOutOfRangeException) //has only occurred on quest start and end so far
            {
                Debugger.Error("[Sync] IndexOutOfRangeException in pullAilmentBuildup");
            }
            catch (FormatException e)
            {
                if (result == "error: session does not exist") //if quitSession has been called while executing this function
                {
                    return;
                }
                Debugger.Error("[Sync] Exception occurred in pullAilmentBuildup: " + e.Message);
                Debugger.Error("[Sync] Return value: " + result);
            }
        }

        public void pushAllPartHP()
        {
            for (int i = 0; i < parts.Count; i++)
            {
                for (int j = 0; j < parts[i].Count; j++)
                {
                    pushPartHP(i, j);
                }
            }
        }

        private void pushAllAilmentBuildup()
        {
            for (int i = 0; i < parts.Count; i++)
            {
                for (int j = 0; j < ailments[i].Count; j++)
                {
                    pushAilmentBuildup(i, j);
                }
            }
        }

        private void pullAllPartHP()
        {
            for (int i = 0; i < parts.Count; i++)
            {
                for (int j = 0; j < parts[i].Count; j++)
                {
                    pullPartHP(i, j);
                }
            }
        }

        private void pullAllAilmentBuildup()
        {
            for (int i = 0; i < parts.Count; i++)
            {
                for (int j = 0; j < ailments[i].Count; j++)
                {
                    pullAilmentBuildup(i, j);
                }
            }
        }
    }
}
