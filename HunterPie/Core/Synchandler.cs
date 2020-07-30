﻿using System;
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
        private string serverUrl = "http://" + UserSettings.PlayerConfig.HunterPie.Sync.ServerUrl;

        private int delay
        {
            get
            {
                return UserSettings.PlayerConfig.HunterPie.Sync.Delay;
            }
        }

        private int retries = 5;
        private Thread syncThreadReference;
        private bool stopThread = false;
        private string sessionUrlString = "";
        private string _sessionID = "";

        public string SessionID
        {
            get => _sessionID;
            set
            {
                _sessionID = HttpUtility.UrlEncode(value);
                sessionUrlString = serverUrl + "/session/" + SessionID + PartyLeader;
            }
        }

        private string _partyLeader = "";

        public string PartyLeader
        {
            get => _partyLeader;
            set
            {
                _partyLeader = HttpUtility.UrlEncode(value);
                sessionUrlString = serverUrl + "/session/" + SessionID + PartyLeader;
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
            if (serverUrl == "http://")
            {
                Debugger.Error("[Sync] Sync server url is empty");
                return;
            }
            syncThreadReference = new Thread(new ThreadStart(syncThread));
            syncThreadReference.Start();
        }

        public void stopSyncThread()
        {
            stopThread = true;
        }

        private string get(string url)
        {
            try
            {
                Debug.Assert(!string.IsNullOrEmpty(url));
                if (url != serverUrl)
                { //if function is not called by isServerAlive()
                    Debug.Assert(!string.IsNullOrEmpty(PartyLeader));
                    Debug.Assert(!string.IsNullOrEmpty(SessionID));
                }
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
                Debugger.Error("[Sync] Exception occured in get(" + url + "): " + e.Message);
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

            Debugger.Log("[Sync] Connected to server " + serverUrl);

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
                return true;
            }

            return false;
        }

        public bool partyExists()
        {
            if (string.IsNullOrEmpty(sessionUrlString) || string.IsNullOrEmpty(PartyLeader))
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
            Debug.Assert(partyExists());
            if (partyExists())
            {
                if (isPartyLeader)
                {
                    get(sessionUrlString + "/delete");
                }
                SessionID = "";
                PartyLeader = "";
                isInParty = false;
                isPartyLeader = false;
                Debugger.Log("[Sync] Left session");
            }
        }

        public void removeMonster(int monsterIndex)
        {
            get(sessionUrlString + "/monster/" + monsterIndex + "/remove");
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
                get(sessionUrlString + "/monster/" + monsterIndex + "/part/" + partIndex + "/hp/" + (int)hp);
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
                get(sessionUrlString + "/monster/" + monsterIndex + "/ailment/" + ailmentIndex + "/buildup/" + (int)buildup);
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
            catch (Exception e)
            {
                Debugger.Error("[Sync] Exception occured in pullPartHP: " + e.Message);
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
            catch (Exception e)
            {
                Debugger.Error("[Sync] Exception occured in pullAilmentBuildup: " + e.Message);
                Debugger.Error("[Sync] Return value: " + result);
            }
        }

        public void pushAllPartHP()
        {
            try
            {
                for (int i = 0; i < 3; i++)
                {
                    for (int j = 0; j < parts[i].Count; j++)
                    {
                        pushPartHP(i, j);
                    }
                }
            }
            catch (IndexOutOfRangeException) //should never be reached and will be removed after some testing
            {
                Debugger.Error("[Sync] <CRITICAL> IndexOutOfRangeException in pushAllPartHP");
            }
        }

        private void pushAllAilmentBuildup()
        {
            try
            {
                for (int i = 0; i < 3; i++)
                {
                    for (int j = 0; j < ailments[i].Count; j++)
                    {
                        pushAilmentBuildup(i, j);
                    }
                }
            }
            catch (IndexOutOfRangeException) //should never be reached and will be removed after some testing
            {
                Debugger.Error("[Sync] <CRITICAL> IndexOutOfRangeException in pushAllAilmentBuildup");
            }
        }

        private void pullAllPartHP()
        {
            try
            {
                for (int i = 0; i < 3; i++)
                {
                    for (int j = 0; j < parts[i].Count; j++)
                    {
                        pullPartHP(i, j);
                    }
                }
            }
            catch (IndexOutOfRangeException) //should never be reached and will be removed after some testing
            {
                Debugger.Error("[Sync] <CRITICAL> IndexOutOfRangeException in pullAllPartHP");
            }
        }

        private void pullAllAilmentBuildup()
        {
            try
            {
                for (int i = 0; i < 3; i++)
                {
                    for (int j = 0; j < ailments[i].Count; j++)
                    {
                        pullAilmentBuildup(i, j);
                    }
                }
            }
            catch (IndexOutOfRangeException) //should never be reached and will be removed after some testing
            {
                Debugger.Error("[Sync] <CRITICAL> IndexOutOfRangeException in pullAllAilmentBuildup");
            }
        }
    }
}
