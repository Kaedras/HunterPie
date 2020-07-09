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

        private bool isMonsterIndexValid(int index)
        {
            return (index >= 0 && index <= 2);
        }

        private string get(string url)
        {
            try
            {
                Debug.Assert(!string.IsNullOrEmpty(url));
                if (url != serverUrl)
                { //if function is not called by isServerAlive()
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
                        return;
                    }
                    Thread.Sleep(1000);
                    isInParty = partyExists();
                }
                while (!isInParty);

                if (isPartyLeader) //send data to server
                {
                    for (int i = 0; i < 3; i++)
                    {
                        bool result;
                        result = pushAllPartHP(i);
                        Debug.Assert(result);
                        result = pushAllAilmentBuildup(i);
                        Debug.Assert(result);
                    }
                }
                else //request data from server
                {
                    for (int i = 0; i < 3; i++)
                    {
                        bool result = pullAllPartHP(i);
                        Debug.Assert(result);
                        result = pullAllAilmentBuildup(i);
                        Debug.Assert(result);
                    }
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
            Debug.Assert(partyExists());
            Debug.Assert(isPartyLeader);

            if (partyExists() && isPartyLeader)
            {
                string result = get(sessionUrlString + "/delete");
                if (result == "true")
                {
                    SessionID = "";
                    PartyLeader = "";
                    return true;
                }
                Debugger.Error("error in Synchandler.deleteSession(), message: " + result);
            }
            return false;
        }

        public bool replaceMonster(int monsterIndex)
        {
            Debug.Assert(isMonsterIndexValid(monsterIndex));
            Debug.Assert(isPartyLeader);

            if (isMonsterIndexValid(monsterIndex) && isPartyLeader)
            {
                string result = get(sessionUrlString + "/monster/" + monsterIndex + "/replace");
                try
                {
                    return bool.Parse(result);
                }
                catch (Exception e)
                {
                    Debugger.Error("error in Synchandler.replaceMonster(" + monsterIndex + "), message: " + e.Message);
                }
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
            Debug.Assert(isMonsterIndexValid(monsterIndex));
            Debug.Assert(isPartyLeader);
            Debug.Assert(partIndex < parts[monsterIndex].Count);
            Debug.Assert(partIndex >= 0);

            if (isMonsterIndexValid(monsterIndex) && isPartyLeader)
            {
                if (partIndex < parts[monsterIndex].Count && partIndex >= 0)
                {
                    float hp = parts[monsterIndex][partIndex].Health;
                    if (float.IsNaN(hp))
                    {
                        hp = 0;
                    }
                    string result = get(sessionUrlString + "/monster/" + monsterIndex + "/part/" + partIndex + "/hp/" + (int)hp);
                    try
                    {
                        return bool.Parse(result);
                    }
                    catch (Exception e)
                    {
                        Debugger.Error("error in Synchandler.pushPartHP(" + monsterIndex + ", " + partIndex + "), message: " + e.Message + "\nresult: " + result);
                    }
                }
            }
            return false;
        }

        public bool pushAilmentBuildup(int monsterIndex, int ailmentIndex)
        {
            Debug.Assert(isMonsterIndexValid(monsterIndex));
            Debug.Assert(isPartyLeader);
            Debug.Assert(ailmentIndex < ailments[monsterIndex].Count);
            Debug.Assert(ailmentIndex >= 0);

            if (isMonsterIndexValid(monsterIndex) && isPartyLeader)
            {
                if (ailmentIndex < ailments[monsterIndex].Count && ailmentIndex >= 0)
                {
                    float buildup = ailments[monsterIndex][ailmentIndex].Buildup;
                    if (float.IsNaN(buildup))
                    {
                        buildup = 0;
                    }
                    string result = get(sessionUrlString + "/monster/" + monsterIndex + "/ailment/" + ailmentIndex + "/buildup/" + (int)buildup);
                    try
                    {
                        return bool.Parse(result);
                    }
                    catch (Exception e)
                    {
                        Debugger.Error("error in Synchandler.pushAilmentBuildup(" + monsterIndex + ", " + ailmentIndex + "), message: " + e.Message + "\nresult: " + result);
                    }
                }
            }
            return false;
        }

        public bool pullPartHP(int monsterIndex, int partIndex)
        {
            Debug.Assert(isMonsterIndexValid(monsterIndex));
            Debug.Assert(!isPartyLeader);
            Debug.Assert(partIndex < parts[monsterIndex].Count);
            Debug.Assert(partIndex >= 0);

            if (isMonsterIndexValid(monsterIndex) && !isPartyLeader)
            {
                if (partIndex < parts[monsterIndex].Count && partIndex >= 0)
                {
                    string result = get(sessionUrlString + "/monster/" + monsterIndex + "/part/" + partIndex + "/hp");
                    //if (result != "false")
                    {
                        try
                        {
                            parts[monsterIndex][partIndex].Health = int.Parse(result);
                            return true;
                        }
                        catch (Exception e)
                        {
                            Debugger.Error("Exception occured in Synchandler.pullPartHP(" + monsterIndex + ", " + partIndex + "), message: " + e.Message);
                        }
                    }
                }
            }
            return false;
        }

        public bool pullAilmentBuildup(int monsterIndex, int ailmentIndex)
        {
            Debug.Assert(isMonsterIndexValid(monsterIndex));
            Debug.Assert(!isPartyLeader);
            Debug.Assert(ailmentIndex < ailments[monsterIndex].Count);
            Debug.Assert(ailmentIndex >= 0);

            if (isMonsterIndexValid(monsterIndex) && !isPartyLeader)
            {
                if (ailmentIndex < ailments[monsterIndex].Count && ailmentIndex >= 0)
                {
                    string result = get(sessionUrlString + "/monster/" + monsterIndex + "/ailment/" + ailmentIndex + "/buildup");
                    //if (result != "false")
                    {
                        try
                        {
                            ailments[monsterIndex][ailmentIndex].Buildup = int.Parse(result);//todo: check if monster has been captured
                            return true;
                        }
                        catch (Exception e)
                        {
                            Debugger.Error("Exception occured in Synchandler.pullAilmentBuildup(" + monsterIndex + ", " + ailmentIndex + "), message: " + e.Message);
                        }
                    }
                }
            }
            return false;
        }

        public bool pushAllPartHP(int monsterIndex)
        {
            Debug.Assert(isMonsterIndexValid(monsterIndex));
            Debug.Assert(isPartyLeader);

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
            Debug.Assert(isMonsterIndexValid(monsterIndex));
            Debug.Assert(isPartyLeader);

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
            Debug.Assert(isMonsterIndexValid(monsterIndex));
            Debug.Assert(!isPartyLeader);

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
            Debug.Assert(isMonsterIndexValid(monsterIndex));
            Debug.Assert(!isPartyLeader);

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
