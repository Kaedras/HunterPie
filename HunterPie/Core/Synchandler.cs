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
        private string serverUrl = "http://" + UserSettings.PlayerConfig.HunterPie.Sync.ServerUrl;
        private int delay = UserSettings.PlayerConfig.HunterPie.Sync.Delay;
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
        public int activeMonster { get; set; } = 0;
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
            if (isPartyLeader)
            {
                deleteSession();
            }
        }

        public void startSyncThread()
        {
            while (!isServerAlive())
            {
                Debugger.Error("Could not reach server, " + retries + " retries remaining.");
                if (retries-- > 0)
                {
                    Thread.Sleep(1000);
                }
                else
                {
                    Debugger.Error("Giving up on trying to reach server.");
                    return;
                }
            }
            syncThreadReference = new Thread(new ThreadStart(syncThread));
            Thread.Sleep(200); //wait a bit to ensure everything else has been initialized
            syncThreadReference.Start();
        }

        public void stopSyncThread()
        {
            stopThread = true;
        }

        private bool isMonsterIndexValid(int index) {
            return (index >= 0 && index <= 2);
        }

        private string get(string url)
        {
            try
            {
                Debug.Assert(!string.IsNullOrEmpty(url));
                if (url != serverUrl) { //if function is not called by isServerAlive()
                    Debug.Assert(!string.IsNullOrEmpty(_partyLeader));
                    Debug.Assert(!string.IsNullOrEmpty(_sessionID));
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
                Debugger.Error("Exception occured in Synchandler.get(" + url + "), message: " + e.Message);
                return "false";
            }
        }

        private void syncThread()
        {
            while (!stopThread)
            {
                do //check if party leader has synchronisation enabled, if not check again later
                {
                    if (stopThread) //check if thread should stop
                    {
                        Debug.Assert(!isPartyLeader);
                        return;
                    }
                    isInParty = partyExists();
                    Thread.Sleep(1000);
                }
                while (!isInParty);

                if (isPartyLeader) //send data to server
                {
                    bool result;
                    result = pushAllPartHP(activeMonster);
                    Debug.Assert(result);
                    result = pushAllAilmentBuildup(activeMonster);
                    Debug.Assert(result);
                }
                else //request data from server
                {
                    bool result = pullAllPartHP(activeMonster);
                    Debug.Assert(result);
                    result = pullAllAilmentBuildup(activeMonster);
                    Debug.Assert(result);
                }

                Thread.Sleep(delay);
            }
            if (isPartyLeader) //cleanup
            {
                deleteSession();
            }
        }

        public bool isServerAlive()
        {
            if (!string.IsNullOrEmpty(serverUrl))
            {
                if (get(serverUrl) == "its alive")
                {
                    return true;
                }
            }
            return false;
        }

        public bool createPartyIfNotExist()
        {
            if (!string.IsNullOrEmpty(SessionID) && !string.IsNullOrEmpty(PartyLeader) && isPartyLeader)
            {
                if (!partyExists())
                {
                    if (get(sessionUrlString + "/create") == "true")
                    {
                        return true;
                    }
                }
                else
                {
                    return true;
                }
            }
            return false;
        }

        public bool partyExists()
        {
            if (!string.IsNullOrEmpty(SessionID) && !string.IsNullOrEmpty(PartyLeader))
            {
                if (get(sessionUrlString + "/exists") == "true")
                {
                    return true;
                }
            }
            return false;
        }

        public bool deleteSession()
        {
            if (partyExists() && isPartyLeader)
            {
                if (get(sessionUrlString + "/delete") == "true")
                {
                    SessionID = "";
                    PartyLeader = "";
                    return true;
                }
            }
            return false;
        }

        public bool replaceMonster(int monsterIndex)
        {
            if (isMonsterIndexValid(monsterIndex) && isPartyLeader)
            {
                return bool.Parse(get(sessionUrlString + "/monster/" + monsterIndex + "/replace"));
            }
            return false;
        }

        public void clearParts(int monsterIndex)
        {
            get(sessionUrlString + "/monster/" + monsterIndex + "/clearparts");
        }

        public void clearAilments(int monsterIndex)
        {
            get(sessionUrlString + "/monster/" + monsterIndex + "/clearailments");
        }

        public bool pushPartHP(int monsterIndex, int partIndex)
        {
            if (isMonsterIndexValid(monsterIndex) && isPartyLeader)
            {
                if (partIndex < parts[monsterIndex].Count && partIndex >= 0)
                {
                    float hp = parts[monsterIndex][partIndex].Health;
                    if (float.IsNaN(hp))
                    {
                        hp = 0;
                    }
                    return bool.Parse(get(sessionUrlString + "/monster/" + monsterIndex + "/part/" + partIndex + "/hp/" + (int)hp));
                }
            }
            return false;
        }

        public bool pushAilmentBuildup(int monsterIndex, int ailmentIndex)
        {
            if (isMonsterIndexValid(monsterIndex) && isPartyLeader)
            {
                if (ailmentIndex < ailments[monsterIndex].Count && ailmentIndex >= 0)
                {
                    float buildup = ailments[monsterIndex][ailmentIndex].Buildup;
                    if (float.IsNaN(buildup))
                    {
                        buildup = 0;
                    }
                    return bool.Parse(get(sessionUrlString + "/monster/" + monsterIndex + "/ailment/" + ailmentIndex + "/buildup/" + (int)buildup));
                }
            }
            return false;
        }

        public bool pullPartHP(int monsterIndex, int partIndex)
        {
            if (isMonsterIndexValid(monsterIndex) && !isPartyLeader)
            {
                if (partIndex < parts[monsterIndex].Count && partIndex >= 0)
                {
                    string result = get(sessionUrlString + "/monster/" + monsterIndex + "/part/" + partIndex + "/hp");
                    if (result != "false")
                    {
                        parts[monsterIndex][partIndex].Health = int.Parse(result);
                        return true;
                    }
                }
            }
            return false;
        }

        public bool pullAilmentBuildup(int monsterIndex, int ailmentIndex)
        {
            if (isMonsterIndexValid(monsterIndex) && !isPartyLeader)
            {
                if (ailmentIndex < ailments[monsterIndex].Count && ailmentIndex >= 0)
                {
                    string result = get(sessionUrlString + "/monster/" + monsterIndex + "/ailment/" + ailmentIndex + "/buildup");
                    if (result != "false")
                    {
                        ailments[monsterIndex][ailmentIndex].Buildup = int.Parse(result);
                        return true;
                    }
                }
            }
            return false;
        }

        public bool pushAllPartHP(int monsterIndex)
        {
            if (isMonsterIndexValid(monsterIndex) && isPartyLeader)
            {
                for (int i = 0; i < parts[monsterIndex].Count; i++)
                {
                    if (!pushPartHP(monsterIndex, i))
                    {
                        return false;
                    }
                }
            }
            else
            {
                return false;
            }
            return true;
        }

        private bool pushAllAilmentBuildup(int monsterIndex)
        {
            if (isMonsterIndexValid(monsterIndex) && isPartyLeader)
            {
                for (int i = 0; i < ailments[monsterIndex].Count; i++)
                {
                    if (!pushAilmentBuildup(monsterIndex, i))
                    {
                        return false;
                    }
                }
            }
            else
            {
                return false;
            }
            return true;
        }

        private bool pullAllPartHP(int monsterIndex)
        {
            if (isMonsterIndexValid(monsterIndex) && !isPartyLeader)
            {
                for (int i = 0; i < parts[monsterIndex].Count; i++)
                {
                    if (!pullPartHP(monsterIndex, i))
                    {
                        return false;
                    }
                }
            }
            else
            {
                return false;
            }
            return true;
        }

        private bool pullAllAilmentBuildup(int monsterIndex)
        {
            if (isMonsterIndexValid(monsterIndex) && !isPartyLeader)
            {
                for (int i = 0; i < ailments[monsterIndex].Count; i++)
                {
                    if (!pullAilmentBuildup(monsterIndex, i))
                    {
                        return false;
                    }
                }
            }
            else
            {
                return false;
            }
            return true;
        }
    }
}
