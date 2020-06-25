﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using HunterPie.Core.Monsters;
using HunterPie.Memory;
using HunterPie.Logger;
using HunterPie.Core.Definitions;

namespace HunterPie.Core {
    public class Monster {
        // Private vars
        private long monsterAddress;
        private string id;
        private float health;
        private float stamina;
        private bool isTarget;
        private int isSelect; //0 = None, 1 = This, 2 = Other
        private float enrageTimer = 0;
        private float sizeMultiplier;

        public Synchandler synchandler;
        private long MonsterAddress
        {
            get => monsterAddress;
            set
            {
                if (value != monsterAddress)
                {
                    if (monsterAddress != 0)
                    {
                        IsAlive = IsActuallyAlive = false;
                        id = null;
                    }
                    monsterAddress = value;
                }
            }
        }
        public int MonsterNumber { get; private set; }

        // Monster basic info
        public string Name => GStrings.GetMonsterNameByID(Id);

        private MonsterInfo MonsterInfo => MonsterData.MonstersInfo[GameId];

        public string Id {
            get => id;
            set {
                if (value != null && id != value)
                {
                    if (IsAlive)
                    {
                        _onMonsterDespawn();
                        IsActuallyAlive = IsAlive = false;
                    }
                    if (Health > 0)
                    {
                        id = value;

                        GetMonsterWeaknesses();
                        IsAlive = true;
                        CreateMonsterParts(MonsterInfo.MaxParts);
                        GetMonsterPartsInfo();
                        Ailments.Clear();
                        do
                        {
                            GetMonsterAilments();
                            Thread.Sleep(10);
                        } while (Ailments.Count == 0);
                        GetMonsterSizeModifier();
                        CaptureThreshold = MonsterInfo.Capture;

                        IsActuallyAlive = true;
                        _onMonsterSpawn();
                    }
                } else if (value == null && id != null) {
                    id = value;
                    _onMonsterDespawn();
                }
            }
        }
        public int GameId { get; set; }
        public float SizeMultiplier {
            get { return sizeMultiplier; }
            set {
                if (value <= 0) return;
                if (value != sizeMultiplier) {
                    sizeMultiplier = value;
                    Debugger.Debug($"{Name} Size multiplier: {sizeMultiplier}");
                    _onCrownChange();
                }
            }
        }
        public string Crown {
            get { return MonsterInfo.GetCrownByMultiplier(SizeMultiplier); }
        }
        public float MaxHealth { get; private set; }
        public float Health {
            get { return health; }
            set {
                if (value != health) {
                    health = value;
                    _onHPUpdate();
                    if (value <= 0) {
                        // Clears monster ID since it's dead
                        Id = null;
                        IsActuallyAlive = IsAlive = false;
                        _onMonsterDeath();
                    }
                }
            }
        }
        public Dictionary<string, int> Weaknesses { get; private set; }
        public float HPPercentage { get; private set; } = 1;
        public bool IsTarget {
            get { return isTarget; }
            set {
                if (value != isTarget) {
                    isTarget = value;
                    _onTargetted();
                }
            }
        }
        public int IsSelect
        {
            get { return isSelect; }
            set {
                if (value != isSelect) {
                    isSelect = value;
                    _onTargetted();
                }
            }
        }
        public bool IsAlive = false;
        public bool IsActuallyAlive;
        public float EnrageTimer {
            get { return enrageTimer; }
            set {
                if (value != enrageTimer) {
                    if (value > 0 && enrageTimer == 0) {
                        _onEnrage();
                    } else if (value == 0 && enrageTimer > 0) {
                        _onUnenrage();
                    }
                    enrageTimer = value;
                    _OnEnrageUpdateTimerUpdate();
                }
            }
        }
        public float CaptureThreshold { get; private set; }
        public float EnrageTimerStatic { get; private set; }
        public bool IsEnraged {
            get { return enrageTimer > 0; }
        }
        public float Stamina {
            get { return stamina; }
            set {
                if ((int)value != (int)stamina) {
                    stamina = value;
                    _OnStaminaUpdate();
                }
            }
        }
        public float MaxStamina { get; private set; }
        public bool IsCaptured { get; private set; }
        public bool[] AliveMonsters = new bool[3] { false, false, false };

        public List<Part> Parts = new List<Part>();
        public List<Ailment> Ailments = new List<Ailment>();
        // Threading
        ThreadStart MonsterInfoScanRef;
        Thread MonsterInfoScan;

        // Game events
        public delegate void MonsterEnrageEvents(object source, MonsterUpdateEventArgs args);
        public delegate void MonsterEvents(object source, EventArgs args);
        public delegate void MonsterSpawnEvents(object source, MonsterSpawnEventArgs args);
        public delegate void MonsterUpdateEvents(object source, MonsterUpdateEventArgs args);
        public event MonsterSpawnEvents OnMonsterSpawn;
        public event MonsterEvents OnMonsterDespawn;
        public event MonsterEvents OnMonsterDeath;
        public event MonsterEvents OnTargetted;
        public event MonsterEvents OnCrownChange;
        public event MonsterUpdateEvents OnHPUpdate;
        public event MonsterUpdateEvents OnStaminaUpdate;
        public event MonsterEnrageEvents OnEnrage;
        public event MonsterEnrageEvents OnUnenrage;
        public event MonsterEnrageEvents OnEnrageTimerUpdate;
        

        protected virtual void _onMonsterSpawn() {
            MonsterSpawnEventArgs args = new MonsterSpawnEventArgs(this);
            OnMonsterSpawn?.Invoke(this, args);
        }

        protected virtual void _onMonsterDespawn() {
            if (UserSettings.PlayerConfig.HunterPie.Sync.Enabled)
            {
                synchandler.replaceMonster(MonsterNumber - 1);
            }
            OnMonsterDespawn?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void _onMonsterDeath() {
            OnMonsterDeath?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void _onHPUpdate() {
            OnHPUpdate?.Invoke(this, new MonsterUpdateEventArgs(this));
        }

        protected virtual void _onTargetted() {
            OnTargetted?.Invoke(this, EventArgs.Empty);
            if (UserSettings.PlayerConfig.HunterPie.Sync.Enabled && isTarget)
            {
                synchandler.activeMonster = MonsterNumber - 1;
            }
        }

        protected virtual void _onCrownChange() {
            OnCrownChange?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void _onEnrage() {
            OnEnrage?.Invoke(this, new MonsterUpdateEventArgs(this));
        }

        protected virtual void _onUnenrage() {
            OnUnenrage?.Invoke(this, new MonsterUpdateEventArgs(this));
        }

        protected virtual void _OnEnrageUpdateTimerUpdate() {
            OnEnrageTimerUpdate?.Invoke(this, new MonsterUpdateEventArgs(this));
        }

        protected virtual void _OnStaminaUpdate() {
            OnStaminaUpdate?.Invoke(this, new MonsterUpdateEventArgs(this));
        }

        public Monster(int initMonsterNumber) {
            MonsterNumber = initMonsterNumber;
        }

        ~Monster() {
            Id = null;
            if (UserSettings.PlayerConfig.HunterPie.Sync.Enabled)
            {
                if (synchandler.isInParty && synchandler.isPartyLeader)
                {
                    bool result = synchandler.replaceMonster(MonsterNumber - 1);
                    System.Diagnostics.Debug.Assert(result);
                }
            }
            Weaknesses?.Clear();
        }

        public void StartThreadingScan() {
            MonsterInfoScanRef = new ThreadStart(ScanMonsterInfo);
            MonsterInfoScan = new Thread(MonsterInfoScanRef) {
                Name = $"Scanner_Monster.{MonsterNumber}"
            };
            Debugger.Warn(GStrings.GetLocalizationByXPath("/Console/String[@ID='MESSAGE_MONSTER_SCANNER_INITIALIZED']").Replace("{MonsterNumber}", MonsterNumber.ToString()));
            MonsterInfoScan.Start();
        }

        public void StopThread() {
            MonsterInfoScan.Abort();
        }

        private void ScanMonsterInfo() {
            while (Scanner.GameIsRunning) {
                GetMonsterAddress();
                GetMonsterId();
                GetMonsterSizeModifier();
                GetMonsterStamina();
                GetMonsterAilments();
                GetMonsterPartsInfo();
                GetMonsterEnrageTimer();
                GetTargetMonsterAddress();
                Thread.Sleep(Math.Max(50, UserSettings.PlayerConfig.Overlay.GameScanDelay));
            }
            Thread.Sleep(1000);
            ScanMonsterInfo();
        }

        public void ClearParts() {
            IsAlive = false;
            Parts.Clear();
            Ailments.Clear();
            if (UserSettings.PlayerConfig.HunterPie.Sync.Enabled)
            {
                if (synchandler.isInParty)
                {
                    synchandler.clearParts(MonsterNumber - 1);
                    synchandler.clearAilments(MonsterNumber - 1);
                }
            }
#if DEBUG
            Debugger.Log($"Cleared parts: {Parts.Count} | {Ailments.Count}");
#endif
        }

        private void GetMonsterAddress() {
            Int64 Address = Memory.Address.BASE + Memory.Address.MONSTER_OFFSET;
            // This will give us the third monster's address, so we can find the second and first monster with it
            Int64 ThirdMonsterAddress = Scanner.READ_MULTILEVEL_PTR(Address, Memory.Address.Offsets.MonsterOffsets);
            switch (MonsterNumber) {
                case 3:
                    MonsterAddress = ThirdMonsterAddress;
                    break;
                case 2:
                    MonsterAddress = Scanner.Read<long>(ThirdMonsterAddress - 0x30) + 0x40;
                    break;
                case 1:
                    MonsterAddress = Scanner.Read<long>(Scanner.Read<long>(ThirdMonsterAddress - 0x30) + 0x10) + 0x40;
                    break;
                default:
                    break;
            }
        }

        private void GetMonsterHealth()
        {
            long MonsterHealthPtr = Scanner.Read<long>(MonsterAddress + Address.Offsets.MonsterHPComponentOffset);
            float MonsterTotalHealth = Scanner.Read<float>(MonsterHealthPtr + 0x60);
            float MonsterCurrentHealth = Scanner.Read<float>(MonsterHealthPtr + 0x64);

            if (MonsterCurrentHealth <= MonsterTotalHealth && MonsterTotalHealth > 0)
            {
                MaxHealth = MonsterTotalHealth;
                Health = MonsterCurrentHealth;
                HPPercentage = Health / MaxHealth == 0 ? 1 : Health / MaxHealth;
            } else
            {
                MaxHealth = 0;
                Health = 0;
                HPPercentage = 1;
            }
        }

        private void GetMonsterId()
        {
            long NamePtr = Scanner.Read<long>(MonsterAddress + Address.Offsets.MonsterNamePtr);
            string MonsterEm = Scanner.READ_STRING(NamePtr + 0x0C, 64);

            if (!string.IsNullOrEmpty(MonsterEm))
            {
                // Validates the em string
                string[] MonsterEmParsed = MonsterEm.Split('\\');
                if (MonsterEmParsed.ElementAtOrDefault(3) == null)
                {
                    Id = null;
                    return;
                }

                MonsterEm = MonsterEmParsed.LastOrDefault();
                if (MonsterEm.StartsWith("em"))
                {
                    GameId = Scanner.Read<int>(MonsterAddress + Address.Offsets.MonsterGameIDOffset);

                    if (!MonsterData.MonstersInfo.ContainsKey(GameId))
                    {
                        if (!MonsterEm.StartsWith("ems")) Debugger.Error($"Unknown Monster Detected: ID:{GameId} | ems: {MonsterEm}");
                        Id = null;
                        Health = 0;
                        MaxHealth = 0;
                        HPPercentage = 1;
                        return;
                    } else
                    {
                        GetMonsterHealth();
                        if (Id != MonsterInfo.Em) Debugger.Debug($"Found new monster ID: {GameId} ({MonsterEm}) #{MonsterNumber} @ 0x{MonsterAddress:X}");
                        Id = MonsterInfo.Em;
                        return;
                    }
                }
            }
            Id = null;
        }

        private void GetMonsterSizeModifier() {
            if (!IsAlive) return;
            float SizeModifier = Scanner.Read<float>(MonsterAddress + 0x7730);
            if (SizeModifier <= 0 || SizeModifier >= 2) SizeModifier = 1;
            SizeMultiplier = Scanner.Read<float>(MonsterAddress + 0x188) / SizeModifier;
        }

        private void GetMonsterWeaknesses() => Weaknesses = MonsterInfo.Weaknesses.ToDictionary(w => w.Id, w => w.Stars);
        

        private void GetMonsterEnrageTimer() {
            EnrageTimer = Scanner.Read<float>(MonsterAddress + 0x1BE54);
            EnrageTimerStatic = Scanner.Read<float>(MonsterAddress + 0x1BE54 + 0x4);
        }

        private void GetTargetMonsterAddress() {
            if (UserSettings.PlayerConfig.Overlay.MonstersComponent.UseLockonInsteadOfPin)
            {
                Int64 LockonAddress = Scanner.READ_MULTILEVEL_PTR(Address.BASE + Address.EQUIPMENT_OFFSET, Address.Offsets.PlayerLockonOffsets);
                // This will give us the monster target index
                int MonsterLockonIndex = Scanner.Read<int>(LockonAddress - 0x7C);
                if (MonsterLockonIndex == -1)
                {
                    IsTarget = false;
                    IsSelect = 0;
                    return;
                }
                // And this one will give us the actual monster index in that target slot
                LockonAddress = LockonAddress - 0x7C - 0x19F8;
                int MonsterIndexInSlot = Scanner.Read<int>(LockonAddress + (MonsterLockonIndex * 8));
                if (MonsterIndexInSlot > 2 || MonsterIndexInSlot < 0)
                {
                    IsTarget = false;
                    IsSelect = 0;
                    return;
                }
                // And then we get then we can finally get the monster index
                List<int> MonsterSlotIndexes = new List<int>();
                for (int i = 0; i < 3; i++) MonsterSlotIndexes.Add(Scanner.Read<int>(LockonAddress + 0x6C + (4 * i)));
                int[] MonsterIndexes = MonsterSlotIndexes.ToArray();
                if (MonsterIndexes[2] == -1 && MonsterIndexes[1] == -1 && AliveMonsters.Where(v => v == true).Count() == 1)
                {
                    IsTarget = IsAlive;
                } else if (MonsterIndexes[2] == -1 && MonsterIndexes[1] != -1 && AliveMonsters.Where(v => v == true).Count() == 2)
                {
                    if (!AliveMonsters[0])
                    {
                        IsTarget = MonsterIndexes[MonsterIndexInSlot] + 1 == (MonsterNumber - 1);
                    } else if (!AliveMonsters[1])
                    {
                        IsTarget = MonsterIndexes[MonsterIndexInSlot] * 2 == (MonsterNumber - 1);
                    } else
                    {
                        IsTarget = MonsterIndexes[MonsterIndexInSlot] == (MonsterNumber - 1);
                    }
                    
                } else
                {
                    IsTarget = MonsterIndexes[MonsterIndexInSlot] == (MonsterNumber - 1);
                }
                IsSelect = IsTarget ? 1 : 2;
            } else
            {
                Int64 TargettedMonsterAddress = Scanner.READ_MULTILEVEL_PTR(Address.BASE + Address.MONSTER_SELECTED_OFFSET, Address.Offsets.MonsterSelectedOffsets);
                Int64 selectedPtr = Scanner.Read<long>(Address.BASE + Address.MONSTER_TARGETED_OFFSET); //probably want an offset for this
                bool isSelect = Scanner.Read<long>(selectedPtr + 0x128) != 0x0 && Scanner.Read<long>(selectedPtr + 0x130) != 0x0 && Scanner.Read<long>(selectedPtr + 0x160) != 0x0;
                Int64 SelectedMonsterAddress = Scanner.Read<long>(selectedPtr + 0x148);
                IsTarget = TargettedMonsterAddress == 0 ? SelectedMonsterAddress == this.MonsterAddress : TargettedMonsterAddress == this.MonsterAddress;
                
                if (!isSelect)
                {
                    this.IsSelect = 0; // nothing is selected
                }
                else if (IsTarget)
                {
                    this.IsSelect = 1; // this monster is selected
                }
                else
                {
                    this.IsSelect = 2; // another monster is selected
                }
            }
            
        }

        private void CreateMonsterParts(int numberOfParts) {
            Parts.Clear();
            for (int i = 0; i < numberOfParts; i++) {
                Part part = new Part(MonsterInfo, MonsterInfo.Parts[i], i);
                Parts.Add(part);
            }
            if (UserSettings.PlayerConfig.HunterPie.Sync.Enabled)
            {
                synchandler.parts[MonsterNumber - 1] = Parts;
            }
        }

        private void GetMonsterPartsInfo()
        {
            if (!IsAlive) return;

            long MonsterPartPtr = Scanner.Read<long>(MonsterAddress + 0x1D058);
            // If the Monster Part Ptr is still 0, then the monster hasn't fully spawn yet
            if (MonsterPartPtr == 0x00000000) return;

            long MonsterPartAddress = MonsterPartPtr + 0x40;
            long MonsterRemovablePartAddress = MonsterPartPtr + 0x1FC8;

            int NormalPartIndex = 0;
            uint RemovablePartIndex = 0;

            for (int pIndex = 0; pIndex < MonsterInfo.MaxParts; pIndex++)
            {
                Part CurrentPart = Parts[pIndex];
                PartInfo CurrentPartInfo = MonsterInfo.Parts[pIndex];

                if (CurrentPartInfo.IsRemovable)
                {
                    if (CurrentPart.Address > 0)
                    {
                        sMonsterRemovablePart MonsterRemovablePartData = Scanner.Win32.Read<sMonsterRemovablePart>(CurrentPart.Address);
                        if (UserSettings.PlayerConfig.HunterPie.Sync.Enabled)
                        {
                            if (!synchandler.isPartyLeader && synchandler.isInParty)
                            {
                                MonsterRemovablePartData.Data.Health = CurrentPart.Health;
                            }
                        }
                        CurrentPart.SetPartInfo(MonsterRemovablePartData.Data);
                    } else
                    {
                        while (MonsterRemovablePartAddress < MonsterRemovablePartAddress + 0x120 * 32)
                        {
                            // Every 15 parts there's a 8 bytes gap between the old removable part block
                            // and the next part block
                            if (Scanner.Read<long>(MonsterRemovablePartAddress) <= 0xA0)
                            {
                                MonsterRemovablePartAddress += 0x8;
                            }
                            sMonsterRemovablePart MonsterRemovablePartData = Scanner.Win32.Read<sMonsterRemovablePart>(MonsterRemovablePartAddress);
                            
                            if (CurrentPartInfo.Skip || (MonsterRemovablePartData.unk3.Index == CurrentPartInfo.Index && MonsterRemovablePartData.Data.MaxHealth > 0))
                            {
                                Debugger.Debug($"Removable Part Structure <{Name}> [0x{MonsterRemovablePartAddress:X}]" + Helpers.Serialize(MonsterRemovablePartData));

                                CurrentPart.Address = MonsterRemovablePartAddress;
                                CurrentPart.IsRemovable = true;
                                if (UserSettings.PlayerConfig.HunterPie.Sync.Enabled)
                                {
                                    if (!synchandler.isPartyLeader && synchandler.isInParty)
                                    {
                                        MonsterRemovablePartData.Data.Health = CurrentPart.Health;
                                    }
                                }
                                CurrentPart.SetPartInfo(MonsterRemovablePartData.Data);
                                RemovablePartIndex++;
                                do
                                {
                                    MonsterRemovablePartAddress += 0x78;
                                } while (MonsterRemovablePartData.Equals(Scanner.Win32.Read<sMonsterRemovablePart>(MonsterRemovablePartAddress)));

                                break;
                            }
                            MonsterRemovablePartAddress += 0x78;
                        }

                    }

                } else
                {
                    if (CurrentPart.Address > 0)
                    {
                        sMonsterPart MonsterPartData = Scanner.Win32.Read<sMonsterPart>(CurrentPart.Address);
                        if (UserSettings.PlayerConfig.HunterPie.Sync.Enabled)
                        {
                            if (!synchandler.isPartyLeader && synchandler.isInParty)
                            {
                                MonsterPartData.Data.Health = CurrentPart.Health;
                            }
                        }
                        CurrentPart.SetPartInfo(MonsterPartData.Data);

                    } else
                    {
                        sMonsterPart MonsterPartData = Scanner.Win32.Read<sMonsterPart>(MonsterPartAddress + (NormalPartIndex * 0x1F8));
                        CurrentPart.Address = MonsterPartAddress + (NormalPartIndex * 0x1F8);
                        CurrentPart.Group = CurrentPartInfo.GroupId;

                        if (UserSettings.PlayerConfig.HunterPie.Sync.Enabled)
                        {
                            if (!synchandler.isPartyLeader && synchandler.isInParty)
                            {
                                MonsterPartData.Data.Health = CurrentPart.Health;
                            }
                        }
                        CurrentPart.SetPartInfo(MonsterPartData.Data);

                        Debugger.Debug($"Part Structure <{Name}> [0x{CurrentPart.Address}]" + Helpers.Serialize(MonsterPartData));

                        NormalPartIndex++;
                    }
                }
            }
        }

        private void GetMonsterStamina() {
            if (!IsAlive) return;
            Int64 MonsterStaminaAddress = MonsterAddress + 0x1C0F0;
            MaxStamina = Scanner.Read<float>(MonsterStaminaAddress + 0x4);
            float stam = Scanner.Read<float>(MonsterStaminaAddress);
            Stamina = stam <= MaxStamina ? stam : MaxStamina;
        }

        private void GetMonsterAilments() {
            if (!this.IsAlive) return;
            if (Ailments.Count > 0) {
                foreach (Ailment status in Ailments)
                {
                    if (status.Address == 0) continue;

                    float maxBuildup = Math.Max(0, Scanner.Read<float>(status.Address + 0x1C8));
                    float currentBuildup;
                    if (UserSettings.PlayerConfig.HunterPie.Sync.Enabled)
                    {
                        if (!synchandler.isPartyLeader && synchandler.isInParty)
                        {
                            currentBuildup = status.Buildup;
                        }
                        else
                        {
                            currentBuildup = Math.Max(0, Scanner.Read<float>(status.Address + 0x1B8));
                        }
                    }
                    else
                    {
                        currentBuildup = Math.Max(0, Scanner.Read<float>(status.Address + 0x1B8));
                    }
                    float maxDuration = Math.Max(0, Scanner.Read<float>(status.Address + 0x19C));
                    float currentDuration = Math.Max(0, Scanner.Read<float>(status.Address + 0x1F8));
                    byte counter = Scanner.Read<byte>(status.Address + 0x200);

                    status.SetAilmentInfo(status.ID, currentDuration, maxDuration, currentBuildup, maxBuildup, counter);
                }
            } else {
                Int64 StatusAddress = Scanner.Read<long>(MonsterAddress + 0x78);
                StatusAddress = Scanner.Read<long>(StatusAddress + 0x57A8);
                Int64 aHolder = StatusAddress;
                while (aHolder != 0) {
                    aHolder = Scanner.Read<long>(aHolder + 0x10);
                    if (aHolder != 0) {
                        StatusAddress = aHolder;
                    }
                }
                Int64 StatusPtr = StatusAddress + 0x40;
                while (StatusPtr != 0x0)
                {

                    Int64 MonsterInStatus = Scanner.Read<long>(StatusPtr + 0x188);

                    if (MonsterInStatus == MonsterAddress) {

                        int ID = Scanner.Read<int>(StatusPtr + 0x198);

                        if (ID > MonsterData.AilmentsInfo.Count || ID < 0)
                        {
                            StatusPtr = Scanner.Read<long>(StatusPtr + 0x18);
                            continue;
                        }

                        var AilmentInfo = MonsterData.AilmentsInfo.ElementAt(ID);
                        if (AilmentInfo.Group == "PARALYSIS") Debugger.Debug($"{StatusPtr:X}");
                        // Skip traps for non-capturable monsters
                        bool SkipTraps = MonsterInfo.Capture == 0 && AilmentInfo.Group == "TRAP";
                        if (AilmentInfo.CanSkip && !UserSettings.PlayerConfig.HunterPie.Debug.ShowUnknownStatuses || SkipTraps)
                        {
                            StatusPtr = Scanner.Read<long>(StatusPtr + 0x18);
                            continue;
                        } else
                        {
                            // TODO: Turn this into a struct def
                            float maxBuildup = Math.Max(0, Scanner.Read<float>(StatusPtr + 0x1C8));
                            float currentBuildup = Math.Max(0, Scanner.Read<float>(StatusPtr + 0x1B8));
                            float maxDuration = Math.Max(0, Scanner.Read<float>(StatusPtr + 0x19C));
                            float currentDuration = Math.Max(0, Scanner.Read<float>(StatusPtr + 0x1F8));
                            byte counter = Scanner.Read<byte>(StatusPtr + 0x200);

                            Ailment mAilment = new Ailment {
                                Address = StatusPtr
                            };
                            mAilment.SetAilmentInfo(ID, currentDuration, maxDuration, currentBuildup, maxBuildup, counter);
                            Ailments.Add(mAilment);
                        }
                    }
                    StatusPtr = Scanner.Read<long>(StatusPtr + 0x18);
                }
                if (UserSettings.PlayerConfig.HunterPie.Sync.Enabled)
                {
                    synchandler.ailments[MonsterNumber - 1] = Ailments;
                }
            }
        }

    }
}
