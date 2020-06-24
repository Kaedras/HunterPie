using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Web;
using HunterPie.Logger;

namespace HunterPie.Core
{
    public class Synchandler
    {
        private readonly string serverUrl = "http://" + UserSettings.PlayerConfig.HunterPie.Sync.ServerUrl;
        private readonly int delay = UserSettings.PlayerConfig.HunterPie.Sync.Delay;
        private int retries = 5;
        private Thread syncThreadReference;
        private bool stopThread = false;

        public bool hasSession { get; set; } = false;
        public string Session { get; set; } = "";
        public bool isHost { get; set; } = false;
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
            if (isHost)
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
            Thread.Sleep(200);
            syncThreadReference.Start();
        }

        public void stopSyncThread()
        {
            stopThread = true;
        }

        private static string get(string url)
        {
            try
            {
                Uri escapedUrl = new Uri(Uri.EscapeUriString(url));
                WebRequest request = WebRequest.Create(escapedUrl);
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
                Debugger.Error("Exception occured in Synchandler.get, message: " + e.Message);
                return "false";
            }
        }

        private void syncThread()
        {
            while (!stopThread)
            {
                while (!hasSession)
                {
                    hasSession = sessionExists();
                    Thread.Sleep(1000);
                }
                if (isHost)
                {
                    bool result;
                    result = pushAllPartHP(activeMonster);
                    System.Diagnostics.Debug.Assert(result);
                    result = pushAllAilmentBuildup(activeMonster);
                    System.Diagnostics.Debug.Assert(result);
                }
                else
                {
                    bool result = pullAllPartHP(activeMonster);
                    System.Diagnostics.Debug.Assert(result);
                    result = pullAllAilmentBuildup(activeMonster);
                    System.Diagnostics.Debug.Assert(result);
                }

                Thread.Sleep(delay);
            }
            if (isHost)
            {
                deleteSession();
            }
        }

        public bool isServerAlive()
        {
            if (get(serverUrl + "/sessions") != "false")
            {
                return true;
            }
            return false;
        }

        public bool createSessionIfNotExist(string ID)
        {

            if (ID.Length > 0)
            {
                Session = HttpUtility.UrlEncode(ID);
                if (!sessionExists())
                {
                    if (get(serverUrl + "/session/" + Session + "/create") == "true")
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

        public bool sessionExists()
        {
            if (Session.Length > 0)
            {
                if (get(serverUrl + "/session/" + Session + "/exists") == "true")
                {
                    return true;
                }
            }
            return false;
        }

        public bool deleteSession()
        {
            if (sessionExists())
            {
                if (get(serverUrl + "/session/" + Session + "/delete") == "true")
                {
                    Session = "";
                    return true;
                }
            }
            return false;
        }

        public bool replaceMonster(int monsterIndex)
        {
            if (monsterIndex >= 0 && monsterIndex <= 2 && isHost)
            {
                return bool.Parse(get(serverUrl + "/session/" + Session + "/monster/" + monsterIndex + "/replace"));
            }
            return false;
        }

        public void clearParts(int monsterIndex)
        {
            get(serverUrl + "/session/" + Session + "/monster/" + monsterIndex + "/clearparts");
        }

        public void clearAilments(int monsterIndex)
        {
            get(serverUrl + "/session/" + Session + "/monster/" + monsterIndex + "/clearailments");
        }

        public bool pushPartHP(int monsterIndex, int partIndex)
        {

            if (monsterIndex >= 0 && monsterIndex <= 2 && isHost)
            {
                if (partIndex <= parts[monsterIndex].Count && partIndex >= 0)
                {
                    return bool.Parse(get(serverUrl + "/session/" + Session + "/monster/" + monsterIndex + "/part/" + partIndex + "/hp/" + (int)parts[monsterIndex][partIndex].Health));
                }
            }
            return false;
        }

        public bool pushAilmentBuildup(int monsterIndex, int ailmentIndex)
        {

            if (monsterIndex >= 0 && monsterIndex <= 2 && isHost)
            {
                if (ailmentIndex <= ailments[monsterIndex].Count && ailmentIndex >= 0)
                {
                    return bool.Parse(get(serverUrl + "/session/" + Session + "/monster/" + monsterIndex + "/ailment/" + ailmentIndex + "/buildup/" + (int)ailments[monsterIndex][ailmentIndex].Buildup));
                }
            }
            return false;
        }

        public bool pullPartHP(int monsterIndex, int partIndex)
        {

            if (monsterIndex >= 0 && monsterIndex <= 2)
            {
                if (partIndex <= parts[monsterIndex].Count && partIndex >= 0)
                {
                    string result = get(serverUrl + "/session/" + Session + "/monster/" + monsterIndex + "/part/" + partIndex + "/hp");
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

            if (monsterIndex >= 0 && monsterIndex <= 2)
            {
                if (ailmentIndex <= ailments[monsterIndex].Count && ailmentIndex >= 0)
                {
                    string result = get(serverUrl + "/session/" + Session + "/monster/" + monsterIndex + "/ailment/" + ailmentIndex + "/buildup");
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

            if (monsterIndex >= 0 && monsterIndex <= 2 && isHost)
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

            if (monsterIndex >= 0 && monsterIndex <= 2 && isHost)
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

            if (monsterIndex >= 0 && monsterIndex <= 2)
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

            if (monsterIndex >= 0 && monsterIndex <= 2)
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
