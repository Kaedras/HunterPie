﻿using System;
using System.Threading;
using HunterPie.Logger;

namespace HunterPie.Core
{

    public class Game
    {
        // Game classes
        public Player Player;
        // TODO: Turn this into a list
        public Monster FirstMonster;
        public Monster SecondMonster;
        public Monster ThirdMonster;
        public Synchandler synchandler;
        public Monster HuntedMonster
        {
            get
            {
                if (FirstMonster.IsTarget)
                {
                    return FirstMonster;
                }
                else if (SecondMonster.IsTarget)
                {
                    return SecondMonster;
                }
                else if (ThirdMonster.IsTarget)
                {
                    return ThirdMonster;
                }
                else
                {
                    return null;
                }
            }
        }
        private DateTime clock = DateTime.UtcNow;
        private DateTime Clock
        {
            get => clock;
            set
            {
                if (value != clock)
                {
                    clock = value;
                    _onClockChange();
                }
            }
        }
        public DateTime? Time { get; private set; }
        public bool IsActive { get; private set; }

        // Threading
        ThreadStart scanGameThreadingRef;
        Thread scanGameThreading;

        // Clock event

        public delegate void ClockEvent(object source, EventArgs args);
        /* This Event is dispatched every 10 seconds to update the rich presence */
        public event ClockEvent OnClockChange;

        protected virtual void _onClockChange() => OnClockChange?.Invoke(this, EventArgs.Empty);

        public void CreateInstances()
        {
            Player = new Player();
            FirstMonster = new Monster(1);
            SecondMonster = new Monster(2);
            ThirdMonster = new Monster(3);
            if (UserSettings.PlayerConfig.HunterPie.Sync.Enabled)
            {
                synchandler = new Synchandler();
                Player.synchandler = synchandler;
                FirstMonster.synchandler = synchandler;
                SecondMonster.synchandler = synchandler;
                ThirdMonster.synchandler = synchandler;
            }
        }

        public void DestroyInstances()
        {
            Player = null;
            FirstMonster = null;
            SecondMonster = null;
            ThirdMonster = null;
            if (UserSettings.PlayerConfig.HunterPie.Sync.Enabled)
            {
                synchandler = null;
            }
        }

        public void StartScanning()
        {
            StartGameScanner();
            HookEvents();
            Player.StartScanning();
            FirstMonster.StartThreadingScan();
            SecondMonster.StartThreadingScan();
            ThirdMonster.StartThreadingScan();
            Debugger.Warn(GStrings.GetLocalizationByXPath("/Console/String[@ID='MESSAGE_GAME_SCANNER_INITIALIZED']"));
            IsActive = true;
            if (UserSettings.PlayerConfig.HunterPie.Sync.Enabled)
            {
                synchandler.startSyncThread();
            }
        }

        public void StopScanning()
        {
            Debugger.Warn(GStrings.GetLocalizationByXPath("/Console/String[@ID='MESSAGE_GAME_SCANNER_STOP']"));
            UnhookEvents();
            FirstMonster.StopThread();
            SecondMonster.StopThread();
            ThirdMonster.StopThread();
            Player.StopScanning();
            scanGameThreading.Abort();
            IsActive = false;
            if (UserSettings.PlayerConfig.HunterPie.Sync.Enabled)
            {
                synchandler.stopSyncThread();
            }
        }

        private void HookEvents() => Player.OnZoneChange += OnZoneChange;

        public void UnhookEvents() => Player.OnZoneChange -= OnZoneChange;

        public void OnZoneChange(object source, EventArgs e)
        {
            if (Player.InPeaceZone) Time = null;
            else { Time = DateTime.UtcNow; }

            if (UserSettings.PlayerConfig.HunterPie.Sync.Enabled)
            {
                if (synchandler.isPartyLeader && synchandler.partyExists())
                {
                    synchandler.deleteSession();
                }
            }
        }

        private void StartGameScanner()
        {
            scanGameThreadingRef = new ThreadStart(GameScanner);
            scanGameThreading = new Thread(scanGameThreadingRef)
            {
                Name = "Scanner_Game"
            };
            scanGameThreading.Start();
        }

        private void GameScanner()
        {

            while (Memory.Scanner.GameIsRunning)
            {
                if (DateTime.UtcNow - Clock >= new TimeSpan(0, 0, 10)) Clock = DateTime.UtcNow;

                // Since monsters are independent, we still need to sync them with eachother
                // to use the lockon
                FirstMonster.AliveMonsters = SecondMonster.AliveMonsters = ThirdMonster.AliveMonsters = (new bool[3]
                {
                    FirstMonster.IsActuallyAlive,
                    SecondMonster.IsActuallyAlive,
                    ThirdMonster.IsActuallyAlive
                });
                Thread.Sleep(UserSettings.PlayerConfig.Overlay.GameScanDelay);
            }
            Thread.Sleep(1000);
            GameScanner();
        }

    }
}
