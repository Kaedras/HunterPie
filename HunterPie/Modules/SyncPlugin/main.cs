using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Web;
using HunterPie.Core;
using Debugger = HunterPie.Logger.Debugger;

namespace HunterPie.Plugins
{
    public class SyncPlugin : IPlugin
    {
        private const int Delay = 200;
        private const string ServerUrl = "http://mhwsync.herokuapp.com";
        private string partyLeader = "";
        private int retries = 5;
        private string sessionID = "";
        private string sessionUrlString = "";
        private Thread syncThreadReference;

        public Game Context { get; set; }
        public string Description { get; set; }
        public string Name { get; set; }
        private bool isInParty { get; set; } = false;
        private bool isPartyLeader { get; set; } = false;
        private bool monsterThreadsStopped { get; set; } = false;

        private string PartyLeader
        {
            get => partyLeader;
            set
            {
                partyLeader = HttpUtility.UrlEncode(value);
                sessionUrlString = ServerUrl + "/session/" + SessionID + PartyLeader;
            }
        }

        private string SessionID
        {
            get => sessionID;
            set
            {
                sessionID = HttpUtility.UrlEncode(value);
                sessionUrlString = ServerUrl + "/session/" + SessionID + PartyLeader;
            }
        }

        public void Initialize(Game context)
        {
            Name = "SyncPlugin";
            Description = "Part and ailment synchronization for HunterPie";

            while (!isServerAlive())
            {
                if (retries-- > 0)
                {
                    log("Could not reach server, " + retries + " retries remaining");
                    Thread.Sleep(500);
                }
                else
                {
                    log("Stopping module initialization");
                    return;
                }
            }

            Context = context;
            
            Context.Player.OnSessionChange += OnSessionChange;
            Context.Player.OnCharacterLogout += OnCharacterLogout;
            Context.Player.OnZoneChange += OnZoneChange;
            Context.FirstMonster.OnHPUpdate += OnHPUpdate;
            Context.FirstMonster.OnMonsterDespawn += OnMonsterDespawn;
            Context.FirstMonster.OnMonsterDeath += OnMonsterDeath;
            Context.SecondMonster.OnHPUpdate += OnHPUpdate;
            Context.SecondMonster.OnMonsterDespawn += OnMonsterDespawn;
            Context.SecondMonster.OnMonsterDeath += OnMonsterDeath;
            Context.ThirdMonster.OnHPUpdate += OnHPUpdate;
            Context.ThirdMonster.OnMonsterDespawn += OnMonsterDespawn;
            Context.ThirdMonster.OnMonsterDeath += OnMonsterDeath;
        }

        public void Unload()
        {
            Context.Player.OnSessionChange -= OnSessionChange;
            Context.Player.OnCharacterLogout -= OnCharacterLogout;
            Context.Player.OnZoneChange -= OnZoneChange;
            Context.FirstMonster.OnHPUpdate -= OnHPUpdate;
            Context.FirstMonster.OnMonsterDespawn -= OnMonsterDespawn;
            Context.FirstMonster.OnMonsterDeath -= OnMonsterDeath;
            Context.SecondMonster.OnHPUpdate -= OnHPUpdate;
            Context.SecondMonster.OnMonsterDespawn -= OnMonsterDespawn;
            Context.SecondMonster.OnMonsterDeath -= OnMonsterDeath;
            Context.ThirdMonster.OnHPUpdate -= OnHPUpdate;
            Context.ThirdMonster.OnMonsterDespawn -= OnMonsterDespawn;
            Context.ThirdMonster.OnMonsterDeath -= OnMonsterDeath;

            for(int i = 0; i < Context.FirstMonster.Ailments.Count; i++) {
                monster.Ailments[i].OnBuildupChange -= OnBuildupChange;
            }
            for (int i = 0; i < Context.SecondMonster.Ailments.Count; i++) {
                monster.Ailments[i].OnBuildupChange -= OnBuildupChange;
            }
            for (int i = 0; i < Context.ThirdMonster.Ailments.Count; i++) {
                monster.Ailments[i].OnBuildupChange -= OnBuildupChange;
            }
        }

        private void clearMonster(int monsterIndex)
        {
            get(sessionUrlString + "/monster/" + monsterIndex + "/clear");
        }

        private bool createPartyIfNotExist()
        {
            if (partyExists())
            {
                return true;
            }

            if (get(sessionUrlString + "/create") == "true")
            {
                log("Created session");
                return true;
            }

            return false;
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
                log(e.GetType() + " occurred in get(" + url + "): " + e.Message);
                return "false";
            }
        }

        private void initializeAilments(Monster monster)
        {
            throw new NotImplementedException();
            /*
            for (int i = 0; i < monster.Ailments.Count; i++)
            {
                monster.Ailments[i].OnBuildupChange += OnBuildupChange;
            }
            */
        }

        private bool isServerAlive()
        {
            if (get(ServerUrl) == "its alive")
            {
                return true;
            }
            return false;
        }

        private void log(string message)
        {
            Debugger.Module(message, Name);
        }

        private void OnBuildupChange(object source, MonsterAilmentEventArgs args)
        {
            throw new NotImplementedException();
        }

        private void OnCharacterLogout(object source, EventArgs args)
        {
            if (isInParty)
            {
                quitSession();
            }
        }

        private void OnHPUpdate(object source, EventArgs args)
        {
            if (isPartyLeader)
            {
                pushPartHP((Monster)source);
            }
        }

        private void OnMonsterDeath(object source, EventArgs args)
        {
            if (isInParty && isPartyLeader)
            {
                clearMonster(((Monster)source).MonsterNumber - 1);
            }
        }

        private void OnMonsterDespawn(object source, EventArgs args)
        {
            if (isInParty && isPartyLeader)
            {
                clearMonster(((Monster)source).MonsterNumber - 1);
            }
        }

        private void OnSessionChange(object source, EventArgs args)
        {
            if (isInParty)
            {
                quitSession();
                stopSyncThread();
            }
            SessionID = Context.Player.SessionID;
        }

        private void OnZoneChange(object source, EventArgs args)
        {
            if (Context.Player.InPeaceZone)
            {
                return;
            }

            for (int i = 0; i < Context.Player.PlayerParty.Members.Count; i++)
            {
                if (Context.Player.PlayerParty.Members[i].IsPartyLeader)
                {
                    PartyLeader = Context.Player.PlayerParty.Members[i].Name;
                }

                if (Context.Player.PlayerParty.Members[i].IsMe && Context.Player.PlayerParty.Members[i].IsPartyLeader && Context.Player.PlayerParty.Members[i].IsInParty)
                {
                    isPartyLeader = true;
                    isInParty = createPartyIfNotExist();
                }
                else if (Context.Player.PlayerParty.Members[i].IsMe && !Context.Player.PlayerParty.Members[i].IsPartyLeader && Context.Player.PlayerParty.Members[i].IsInParty)
                {
                    isPartyLeader = false;
                    isInParty = partyExists();
                    if (isInParty)
                    {
                        log("Entered session");
                    }
                    stopMonsterThreads();
                    startSyncThread();
                }
                else
                {
                    if (monsterThreadsStopped)
                    {
                        startMonsterThreads();
                    }
                }
            }
        }

        private bool partyExists()
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

        private void pullPartHP(Monster monster)
        {
            string result;
            for (int i = 0; i < monster.Parts.Count; i++)
            {
                result = get(sessionUrlString + "/monster/" + (monster.MonsterNumber - 1) + "/part/" + i + "/hp");
                try
                {
                    monster.Parts[i].Health = int.Parse(result);
                }
                catch (Exception e)
                {
                    log(e.GetType() + " in pullPartHP: " + e.Message);
                }
            }
        }

        private void pushPartHP(Monster monster)
        {
            float hp;
            string result;
            for (int i = 0; i < monster.Parts.Count; i++)
            {
                hp = monster.Parts[i].Health;
                if (float.IsNaN(hp))
                {
                    hp = 0;
                }
                result = get(sessionUrlString + "/monster/" + (monster.MonsterNumber - 1) + "/part/" + i + "/hp/" + (int)hp);
                if (result != "true")
                {
                    log("Error in pushPartHP: " + result);
                }
            }
        }

        private void quitSession()
        {
            if (isPartyLeader)
            {
                get(sessionUrlString + "/delete");
            }
            sessionID = "";
            partyLeader = "";
            isInParty = false;
            isPartyLeader = false;
            log("Left session");
        }

        private void startMonsterThreads()
        {
            Context.FirstMonster.StartThreadingScan();
            Context.SecondMonster.StartThreadingScan();
            Context.ThirdMonster.StartThreadingScan();
            monsterThreadsStopped = false;
            log("Started monster threads");
        }

        private void startSyncThread()
        {
            syncThreadReference = new Thread(syncThread);
            syncThreadReference.Start();
        }

        private void stopMonsterThreads()
        {
            Context.FirstMonster.StopThread();
            Context.SecondMonster.StopThread();
            Context.ThirdMonster.StopThread();
            monsterThreadsStopped = true;
            log("Stopped monster threads");
        }

        private void stopSyncThread()
        {
            syncThreadReference.Abort();
        }

        private void syncThread()
        {
            while (Context.IsActive)
            {
                pullPartHP(Context.FirstMonster);
                pullPartHP(Context.SecondMonster);
                pullPartHP(Context.ThirdMonster);
                Thread.Sleep(Delay);
            }
        }
    }
}
