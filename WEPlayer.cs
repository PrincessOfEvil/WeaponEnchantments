﻿using Microsoft.Xna.Framework;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.Default;
using Terraria.ModLoader.IO;
using Terraria.UI;
using WeaponEnchantments.Common;
using WeaponEnchantments.Common.Configs;
using WeaponEnchantments.Common.Globals;
using WeaponEnchantments.Common.Utility;
using WeaponEnchantments.Effects;
using WeaponEnchantments.Items;
using WeaponEnchantments.UI;
using static WeaponEnchantments.Common.EnchantingRarity;
using static WeaponEnchantments.Common.Globals.EnchantedItemStaticMethods;

namespace WeaponEnchantments {
    public class WEPlayer : ModPlayer {
        public static bool WorldOldItemsReplaced = false;
        public bool usingEnchantingTable;
        public int enchantingTableTier;
        public int highestTableTierUsed;
        public bool itemInEnchantingTable;
        public bool[] enchantmentInEnchantingTable = new bool[EnchantingTable.maxEnchantments];
        public Item itemBeingEnchanted;
        public EnchantingTable enchantingTable;
        public WeaponEnchantmentUI enchantingTableUI;
        public ConfirmationUI confirmationUI;
        public bool canLifeSteal = false;
        public float lifeSteal = 0f;
        public float lifeStealRollover = 0f;
        public int allForOneTimer = 0;
        public Item[] equipArmor;
        public bool[] equipArmorStatsUpdated;
        public Item trackedWeapon;
        public Item trackedHoverItem;
        public Item infusionConsumeItem = null;
        public string previousInfusedItemName = "";

        int hoverItemIndex = 0;
        int hoverItemChest = 0;
        public Item trackedTrashItem = new Item();
        public bool disableLeftShiftTrashCan = ItemSlot.Options.DisableLeftShiftTrashCan;
        public Dictionary<int, int> buffs = new Dictionary<int, int>();
        public Dictionary<string, StatModifier> statModifiers = new Dictionary<string, StatModifier>();
        public Dictionary<string, StatModifier> appliedStatModifiers = new Dictionary<string, StatModifier>();
        public Dictionary<string, StatModifier> eStats = new Dictionary<string, StatModifier>();

        // Currently just a function that gets the current player equipment state.
        public PlayerEquipment LastPlayerEquipment;
        public PlayerEquipment PlayerEquipment => new PlayerEquipment(this.Player);

        public enum EditableStat {
            AttackSpeed,
            ArmorPenetration,
            BonusManaRegen,
            CriticalStrikeChance,
            Damage,
            Defense,
            JumpSpeedBoost,
            Knockback,
            LifeRegen,
            LifeSteal,
            ManaCost,
            ManaRegen,
            MaxFallSpeed,
            MaxHP,
            MaxMinions,
            MaxMP,
            MoveAcceleration,
            MoveSlowdown,
            MoveSpeed,
            Size,
            Speed,
            WingTime
        }

        #region Default Hooks
        public override void Load() {
            IL.Terraria.Player.ItemCheck_MeleeHitNPCs += HookItemCheck_MeleeHitNPCs;
        }
        public override void OnEnterWorld(Player player) {

            #region Debug

            if (LogMethods.debugging) ($"\\/OnEnterWorld({player.S()})").Log();

            #endregion

            InfusionManager.SetUpVanillaWeaponInfusionPowers();

            if (!WorldOldItemsReplaced) {
                OldItemManager.ReplaceAllOldItems();
                WorldOldItemsReplaced = true;
            }

            OldItemManager.ReplaceAllPlayerOldItems(player);

            #region Debug

            if (LogMethods.debugging) ($"/\\OnEnterWorld({player.S()})").Log();

            #endregion
        }
        public static void HookItemCheck_MeleeHitNPCs(ILContext il) {
            //Make vanilla crit roll 0
            var c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.After,
                i => i.MatchCall(out _),
                i => i.MatchLdcI4(1),
                i => i.MatchLdcI4(101),
                i => i.MatchCallvirt(out _),
                i => i.MatchLdloc(7),
                i => i.MatchBgt(out _),
                i => i.MatchLdcI4(1)
            )) { throw new Exception("Failed to find instructions HookItemCheck_MeleeHitNPCs"); }
            c.Emit(OpCodes.Pop);
            c.Emit(OpCodes.Ldc_I4_0);
        }
        public override void Initialize() {
            enchantingTable = new EnchantingTable();
            enchantingTableUI = new WeaponEnchantmentUI();
            int modSlotCount = Player.GetModPlayer<ModAccessorySlotPlayer>().SlotCount;
            int armorCount = Player.armor.Length / 2 + modSlotCount;
            equipArmor = new Item[armorCount];
            equipArmorStatsUpdated = new bool[armorCount];
            trackedWeapon = new Item();
            confirmationUI = new ConfirmationUI();
            for (int i = 0; i < equipArmor.Length; i++) {
                equipArmor[i] = new Item();
            }
        }
        public override void SaveData(TagCompound tag) {

            for (int i = 0; i < EnchantingTable.maxItems; i++) {
                tag["enchantingTableItem" + i.ToString()] = enchantingTable.item[i];
            }

            for (int i = 0; i < EnchantingTable.maxEssenceItems; i++) {
                tag["enchantingTableEssenceItem" + i.ToString()] = enchantingTable.essenceItem[i];
            }

            tag["infusionConsumeItem"] = infusionConsumeItem;
            tag["highestTableTierUsed"] = highestTableTierUsed;
        }
        public override void LoadData(TagCompound tag) {
            for (int i = 0; i < EnchantingTable.maxItems; i++) {
                if (tag.Get<Item>("enchantingTableItem" + i.ToString()).IsAir) {
                    enchantingTable.item[i] = new Item();
                }
                else {
                    enchantingTable.item[i] = tag.Get<Item>("enchantingTableItem" + i.ToString());
                }
            }

            for (int i = 0; i < EnchantingTable.maxEssenceItems; i++) {
                if (tag.Get<Item>("enchantingTableEssenceItem" + i.ToString()).IsAir) {
                    enchantingTable.essenceItem[i] = new Item();
                }
                else {
                    enchantingTable.essenceItem[i] = tag.Get<Item>("enchantingTableEssenceItem" + i.ToString());
                }
            }

            infusionConsumeItem = tag.Get<Item>("infusionConsumeItem");
            if (infusionConsumeItem.IsAir)
                infusionConsumeItem = null;

            highestTableTierUsed = tag.Get<int>("highestTableTierUsed");
        }
        public override bool ShiftClickSlot(Item[] inventory, int context, int slot) {
            if (!usingEnchantingTable)
                return false;

            bool stop = false;
            Item enchantingTableSlotItem = null;

            //Check itemSlot(s)
            for (int j = 0; j < EnchantingTable.maxItems; j++) {
                if (enchantingTableUI.itemSlotUI[j].contains) {
                    stop = true;
                    enchantingTableSlotItem = enchantingTableUI.itemSlotUI[j].Item;
                }
            }

            //Check enchantmentSlots
            for (int j = 0; j < EnchantingTable.maxEnchantments && !stop; j++) {
                if (enchantingTableUI.enchantmentSlotUI[j].contains) {
                    stop = true;
                    enchantingTableSlotItem = enchantingTableUI.enchantmentSlotUI[j].Item;
                }
            }

            //Check essenceSlots
            for (int j = 0; j < EnchantingTable.maxEssenceItems && !stop; j++) {
                if (enchantingTableUI.essenceSlotUI[j].contains) {
                    stop = true;
                    enchantingTableSlotItem = enchantingTableUI.essenceSlotUI[j].Item;
                }
            }

            //Prevent Trashing item TODO: Edit this if you ever make ammo bags enchantable
            if (stop) {
                bool itemWillBeTrashed = true;
                for (int i = 49; i >= 0 && itemWillBeTrashed; i--) {
                    if (Player.inventory[i].IsAir || (Player.inventory[i].type == enchantingTableSlotItem.type && Player.inventory[i].stack < Player.inventory[i].maxStack))
                        itemWillBeTrashed = false;
                }

                if (itemWillBeTrashed)
                    return true;
            }

            //Move Item
            if (!stop) {
                CheckShiftClickValid(ref inventory[slot], true);

                return true;
            }

            return false;
        }
        public Item[] GetEquipArmor(bool getArrayOnly = false) {
            Item[] currentEquipArmor = new Item[equipArmor.Length];
            int vanillaArmorLength = Player.armor.Length / 2;
            var loader = LoaderManager.Get<AccessorySlotLoader>();
            for (int j = 0; j < equipArmor.Length; j++) {
                bool checkItemChanged = true;
                if (j < vanillaArmorLength) {
                    currentEquipArmor[j] = Player.armor[j];
                }
                else {
                    int num = j - vanillaArmorLength;
                    if (loader.ModdedIsAValidEquipmentSlotForIteration(num, Player) && !loader.Get(num).FunctionalItem.vanity) {
                        currentEquipArmor[j] = loader.Get(num).FunctionalItem;
                    }
                    else {
                        checkItemChanged = false;
                        currentEquipArmor[j] = new Item();
                    }
                }

                if (checkItemChanged && !getArrayOnly)
                    equipArmorStatsUpdated[j] = !ItemChanged(currentEquipArmor[j], equipArmor[j]);
            }

            return currentEquipArmor;
        }
        public override void PostUpdateMiscEffects() {
            ApplyPostMiscEnchants();
        }
        public override void PostUpdate() {
            /*Troubleshooting Localization
            ModItem modItem = Main.HoverItem.ModItem;
            if (modItem != null) {
                if(modItem is Enchantment enchantment) {
                    string typeNameString = "Mods.WeaponEnchantments.EnchantmentTypeNames." + enchantment.EnchantmentTypeName;
                    typeNameString.Log();
                    string displayName = Language.GetTextValue(typeNameString) + Language.GetTextValue("Mods.WeaponEnchantments.Enchantment");
                    string rarityString = "Mods.WeaponEnchantments.DisplayTierNames." + displayTierNames[enchantment.EnchantmentTier];
                    Main.NewText(displayName + Language.GetTextValue(rarityString));
                }
			}*/

            //int vanillaArmorLength = Player.armor.Length / 2;
            //var loader = LoaderManager.Get<AccessorySlotLoader>();
            //Check if armor changed
            Item[] currentArmor = GetEquipArmor();

            for (int j = 0; j < equipArmor.Length; j++) {
                /*if (j < vanillaArmorLength) {
                    armor = Player.armor[j];
                }
                else {
                    int num = j - vanillaArmorLength;
                    if (loader.ModdedIsAValidEquipmentSlotForIteration(num, Player))
                        armor = loader.Get(num).FunctionalItem;
                    else
                        armor = new Item();
                }*/

                bool armorStatsUpdated = equipArmorStatsUpdated[j];
                if (!armorStatsUpdated) {
                    Item armor = currentArmor[j];
                    armor.CheckRemoveEnchantments(Player);
                    UpdatePotionBuffs(ref armor, ref equipArmor[j]);
                    UpdatePlayerStats(ref armor, ref equipArmor[j]);
                    if (equipArmor[j].TryGetEnchantedItem(out EnchantedItem eaGlobal)) {
                        eaGlobal.equippedInArmorSlot = false;
                    }

                    if (armor.TryGetEnchantedItem(out EnchantedItem aGlobal)) {
                        aGlobal.equippedInArmorSlot = true;
                    }

                    equipArmor[j] = armor;
                }
            }

            if (Main.mouseItem.IsAir) {
                Player.HeldItem.CheckWeapon(ref trackedWeapon, Player, 0);
            }
            else if (IsEnchantable(Main.mouseItem)) {
                Main.mouseItem.CheckWeapon(ref trackedWeapon, Player, 1);
            }

			if (Main.HoverItem != null && IsWeaponItem(Main.HoverItem) && Main.HoverItem.TryGetEnchantedItem(out EnchantedItem hGlobal) && !hGlobal.trackedWeapon && !hGlobal.hoverItem) {

                #region Debug

                if (LogMethods.debugging) ($"\\/Start hoverItem check").Log();

                #endregion

                Item newItem = null;
                if (usingEnchantingTable && EnchantedItemStaticMethods.IsSameEnchantedItem(enchantingTableUI.itemSlotUI[0].Item, Main.HoverItem))
                    newItem = enchantingTableUI.itemSlotUI[0].Item;

                if (newItem != null && EnchantedItemStaticMethods.IsSameEnchantedItem(Player.inventory[hoverItemIndex], Main.HoverItem))
                    newItem = Player.inventory[hoverItemIndex];

                if (newItem != null && Player.chest != -1) {
                    Item[] inventory = null;
                    switch (hoverItemChest) {
                        case > -1:
                            inventory = Main.chest[hoverItemChest].item;
                            break;
                        case -2:
                            inventory = Player.bank.item;
                            break;
                        case -3:
                            inventory = Player.bank2.item;
                            break;
                        case -4:
                            inventory = Player.bank3.item;
                            break;
                        case -5:
                            inventory = Player.bank4.item;
                            break;
                    }

                    if (EnchantedItemStaticMethods.IsSameEnchantedItem(inventory[hoverItemIndex], Main.HoverItem))
                        newItem = inventory[hoverItemIndex];
                }

                if (newItem == null) {
                    for (int i = 0; i < Player.inventory.Length; i++) {
                        if (IsWeaponItem(Player.inventory[i])) {
                            if (EnchantedItemStaticMethods.IsSameEnchantedItem(Player.inventory[i], Main.HoverItem)) {
                                hoverItemIndex = i;
                                newItem = Player.inventory[i];

                                break;
                            }
                        }
                    }
                }

                if (Player.chest != -1 && newItem == null) {
                    Item[] inventory = null;
                    switch (Player.chest) {
                        case > -1:
                            inventory = Main.chest[Player.chest].item;
                            break;
                        case -2:
                            inventory = Player.bank.item;
                            break;
                        case -3:
                            inventory = Player.bank2.item;
                            break;
                        case -4:
                            inventory = Player.bank3.item;
                            break;
                        case -5:
                            inventory = Player.bank4.item;
                            break;
                    }

                    for (int i = 0; i < inventory.Length; i++) {
                        Item chestItem = inventory[i];
                        if (IsWeaponItem(chestItem)) {
                            if (EnchantedItemStaticMethods.IsSameEnchantedItem(chestItem, Main.HoverItem)) {
                                hoverItemIndex = i;
                                newItem = chestItem;
                                hoverItemChest = Player.chest;

                                break;
                            }
                        }
                    }
                }

                #region Debug

                if (LogMethods.debugging) ($"newItem: " + newItem.S()).Log();

                #endregion

                bool checkWeapon = ItemChanged(newItem, trackedHoverItem, true);

                #region Debug

                if (LogMethods.debugging) ($"checkWeapon: " + ItemChanged(newItem, trackedHoverItem, true)).Log();

                #endregion

                //Check HeldItem
                if (checkWeapon) {
                    if (newItem.TryGetEnchantedItem(out EnchantedItem nGlobal))
                        nGlobal.hoverItem = true;

                    if (trackedHoverItem.TryGetEnchantedItem(out EnchantedItem tGlobal)) {
                        tGlobal.hoverItem = false;
                    }

                    trackedHoverItem = newItem;
                    UpdateItemStats(ref newItem);
                }

                #region Debug

                if (LogMethods.debugging) ($"/\\End hoverItem check Item: " + (newItem != null ? newItem.Name : "null ")).Log();

                #endregion
            }
            else {
                bool trackedHoverItemGlobalExists = trackedHoverItem.TryGetEnchantedItem(out EnchantedItem trackedHoverItemEI);
                bool newHoverItemExists = Main.HoverItem.TryGetEnchantedItem(out EnchantedItem hoverItemEI) && hoverItemEI.hoverItem == false || Main.HoverItem == null || Main.HoverItem.IsAir;
                if (trackedHoverItemGlobalExists && newHoverItemExists) {

                    #region Debug

                    if (LogMethods.debugging) ($"remove hoverItem: {trackedHoverItem.S()}").Log();

                    #endregion

                    trackedHoverItemEI.hoverItem = false;
                    trackedHoverItem = null;
                }
            }

            foreach (int key in buffs.Keys) {
                Player.AddBuff(key, 1);
            }

            if (allForOneTimer > 0) {
                allForOneTimer--;
                if (allForOneTimer == 0) {
                    SoundEngine.PlaySound(SoundID.Unlock);
                }
            }
        }
        public override void Kill(double damage, int hitDirection, bool pvp, PlayerDeathReason damageSource) {
            if (Player.difficulty == 1 || Player.difficulty == 2) {
                for (int i = 0; i < Player.inventory.Length; i++) {
                    if (Player.inventory[i].TryGetEnchantedItem(out EnchantedItem iGlobal)) {
                        iGlobal.appliedStatModifiers.Clear();
                    }
                }
                for (int i = 0; i < Player.armor.Length; i++) {
                    if (Player.armor[i].TryGetEnchantedItem(out EnchantedItem iGlobal)) {
                        iGlobal.appliedStatModifiers.Clear();
                    }
                }
            }
        }
        public override void ResetEffects() {
            lifeSteal = 0f;
            canLifeSteal = false;
            bool updatePlayerStat = false;
            foreach (string key in statModifiers.Keys) {
                string name = key.RemoveInvert().RemovePrevent();
                if (Player.GetType().GetField(name) != null) {
                    appliedStatModifiers.Remove(key);
                    updatePlayerStat = true;
                }
            }
            if (updatePlayerStat)
                UpdatePlayerStat();
        }
        public override void ModifyHitNPC(Item item, NPC target, ref int damage, ref float knockback, ref bool crit) {
            ApplyModifyHitEnchants(item, target, ref damage, ref knockback, ref crit);
        }
        public override void ModifyHitNPCWithProj(Projectile proj, NPC target, ref int damage, ref float knockback, ref bool crit, ref int hitDirection) {
            proj.TryGetGlobalProjectile(out WEProjectile weProj); // Try not using a global for this maybe
            Item item = weProj.sourceItem;

            ApplyModifyHitEnchants(item, target, ref damage, ref knockback, ref crit);
        }
        public override void OnHitNPC(Item item, NPC target, int damage, float knockback, bool crit) {
            ApplyOnHitEnchants(item, target, damage, knockback, crit);
        }
        public override void OnHitNPCWithProj(Projectile proj, NPC target, int damage, float knockback, bool crit) {
            proj.TryGetGlobalProjectile(out WEProjectile weProj);
            Item item = weProj.sourceItem;

            ApplyOnHitEnchants(item, target, damage, knockback, crit, proj);
        }
        public override IEnumerable<Item> AddStartingItems(bool mediumCoreDeath) {
            List<Item> items = new List<Item>();
            if (WEMod.serverConfig.DCUStart) {
                Item item = new Item(ItemID.DrillContainmentUnit);
                items.Add(item);
            }

            return items;
        }
        public override bool? CanAutoReuseItem(Item item) {
            bool? result = ApplyAutoReuseEnchants(item);
            if (result.HasValue) {
                return result.Value;
            }
            return base.CanAutoReuseItem(item);
        }

        #endregion

        #region Enchantment hooks
        private struct StatDamageClass {
            public StatDamageClass(EditableStat editableStat, DamageClass damageClass) {
                EditableStat = editableStat;
                DamageClass = damageClass;
            }
            public EditableStat EditableStat;
            public DamageClass DamageClass;
        }
        private IEnumerable<EnchantmentEffect> GetRelevantEffects(Item heldItem = null) {
            // Always use all equipment effects
            IEnumerable<EnchantmentEffect> allEffects = PlayerEquipment.GetArmorEnchantmentEffects();

            // If a held item isn't specified and the currently held item is a weapon, use that as the currently held item.
            if (heldItem == null && IsWeaponItem(Player.HeldItem)) {
                heldItem = Player.HeldItem;
            }

            // If a held item has been set
            if (heldItem.TryGetEnchantedItem(out EnchantedItem enchantedHeldItem)) {
                // Use that item's enchantments too
                IEnumerable<EnchantmentEffect> heldItemEffects = PlayerEquipment.ExtractEnchantmentEffects(enchantedHeldItem);
                allEffects = allEffects.Concat(heldItemEffects);
            }

            return allEffects;
        }
        public void ApplyPostMiscEnchants() {
            IEnumerable<EnchantmentEffect> allEffects = GetRelevantEffects();

            List<IPassiveEffect> passiveEffects = new List<IPassiveEffect>();
            List<StatEffect> statEffects = new List<StatEffect>();

            // Divide effects based on what is needed.
            foreach (EnchantmentEffect effect in allEffects) {
                if (effect.GetType().GetInterface(nameof(IPassiveEffect)) != null)
                    passiveEffects.Add((IPassiveEffect)effect);

                if (effect is StatEffect)
                    statEffects.Add((StatEffect)effect);
            }

            // Apply all PostUpdateMiscEffects
            foreach (IPassiveEffect effect in passiveEffects) {
                effect.PostUpdateMiscEffects(this);
            }

            PlayerEquipment newEquipment = PlayerEquipment;
            if (newEquipment != LastPlayerEquipment) {
                LastPlayerEquipment = newEquipment;
            }

            // Apply them if there's any. TODO: Make sure changes _actually_ have to be made to save on time.
            if (statEffects.Any())
                ApplyStatEffects(statEffects);

        }
        public bool? ApplyAutoReuseEnchants(Item item) {
            IEnumerable<EnchantmentEffect> allEffects = GetRelevantEffects();

            // Divide effects based on what is needed.
            bool takeEnchantment = false;
            foreach (EnchantmentEffect effect in allEffects) {
                if (effect.GetType().GetInterface(nameof(ICanAutoReuseItem)) != null) {
                    bool? result = ((ICanAutoReuseItem)effect).CanAutoReuseItem(item);
                    if (result == null) {
                        continue;
                    } else if (result == true) {
                        takeEnchantment = true;
                    } else {
                        return false;
                    }
                }
            }

            return takeEnchantment ? true : null;
        }
        private void ApplyStatEffects(IEnumerable<StatEffect> StatEffects) {

            // Set up to combine all stat modifiers. We must also keep wether or not it's a vanilla attribute.
            Dictionary<StatDamageClass, StatModifier> statModifiers = new Dictionary<StatDamageClass, StatModifier>();
            Dictionary<StatDamageClass, int> statCounts = new Dictionary<StatDamageClass, int>();

            foreach (StatEffect statEffect in StatEffects) {
                DamageClass dc = statEffect.GetType().GetInterface(nameof(IClassedEffect)) != null ? ((IClassedEffect)statEffect).damageClass : null;
                StatDamageClass statDC = new StatDamageClass(statEffect.statName, dc);
                if (!statModifiers.ContainsKey(statDC)) {
                    // If the stat name isn't on the dictionary add it
                    statModifiers.Add(statDC, statEffect.StatModifier);
                    statCounts.Add(statDC, 1);
                }
                else {
                    // If the stat name is on the dictionary, combine it's modifiers
                    statModifiers[statDC] = statEffect.EStatModifier.CombineWith(statModifiers[statDC]);
                    statCounts[statDC] += 1;
                }
            }

            // TODO use statCounts[eb] and dampening factor to make stats weaker on stacking.

            foreach (StatDamageClass eb in statModifiers.Keys) {
                ModifyStat(eb.EditableStat, statModifiers[eb], eb.DamageClass);
            }
        }
        private void ModifyStat(EditableStat es, StatModifier sm, DamageClass dc = null) {
            switch (es) {
                case EditableStat.ArmorPenetration:
                    if (dc == null)
                        return;

                    Player.GetArmorPenetration(dc) = sm.ApplyTo(Player.GetArmorPenetration(dc));
                    break;
                case EditableStat.BonusManaRegen:
                    Player.manaRegenBonus = (int)sm.ApplyTo(Player.manaRegenBonus);
                    break;
                case EditableStat.CriticalStrikeChance:
                    if (dc == null)
                        return;

                    Player.GetCritChance(dc) = sm.ApplyTo(Player.GetCritChance(dc));
                    break;
                case EditableStat.Damage:
                    if (dc == null)
                        return;

                    Player.GetDamage(dc) = sm.CombineWith(Player.GetDamage(dc));
                    break;
                case EditableStat.Defense:
                    Player.statDefense = (int)sm.ApplyTo(Player.statDefense);
                    break;
                case EditableStat.JumpSpeedBoost:
                    Player.jumpSpeedBoost = sm.ApplyTo(Player.jumpSpeedBoost);
                    break;
                /*case EditableStat.Knockback:
                    if (dc == null)
                        return;

                    Player.GetKnockback(dc) = sm.CombineWith(Player.GetKnockback(dc));
                    break;*/
                case EditableStat.LifeRegen:
                    Player.lifeRegen = (int)sm.ApplyTo(Player.lifeRegen);
                    break;
                case EditableStat.LifeSteal:
                    canLifeSteal = true;
                    lifeSteal = sm.ApplyTo(lifeSteal);
                    break;
                /*case EditableStat.ManaCost:
                    if (dc == null)
                        return;

                    Player.GetManaCost(item) = (int)sm.ApplyTo(Player.GetManaCost(item));
                    break;*/
                case EditableStat.ManaRegen:
                    Player.manaRegen = (int)sm.ApplyTo(Player.manaRegen);
                    break;
                case EditableStat.MaxHP:
                    Player.statLifeMax2 = (int)sm.ApplyTo(Player.statLifeMax2);
                    break;
                case EditableStat.MaxMinions:
                    Player.maxMinions = (int)sm.ApplyTo(Player.maxMinions);
                    break;
                case EditableStat.MaxMP:
                    Player.statManaMax2 = (int)sm.ApplyTo(Player.statManaMax2);
                    break;
                case EditableStat.MaxFallSpeed:
                    Player.maxFallSpeed = sm.ApplyTo(Player.maxFallSpeed);
                    break;
                case EditableStat.MoveAcceleration:
                    Player.runAcceleration = sm.ApplyTo(Player.runAcceleration);
                    break;
                case EditableStat.MoveSlowdown:
                    Player.runSlowdown = sm.ApplyTo(Player.runSlowdown);
                    break;
                case EditableStat.MoveSpeed:
                    Player.moveSpeed = sm.ApplyTo(Player.moveSpeed);
                    break;
                /*case EditableStat.Size:
                    Player.GetAdjustedItemScale(item) = sm.ApplyTo(GetAdjustedItemScale(item))
                    break;*/
                case EditableStat.AttackSpeed:
                    if (dc == null)
                        return;
                    Player.GetAttackSpeed(dc) = sm.ApplyTo(Player.GetAttackSpeed(dc));
                    break;
                case EditableStat.WingTime:
                    Player.wingTimeMax = (int)sm.ApplyTo(Player.wingTimeMax);
                    break;
            }
        }
        public void ApplyModifyHitEnchants(Item item, NPC target, ref int damage, ref float knockback, ref bool crit, int hitDirection = 0, Projectile proj = null) {
            // Not using hitDirection yet.

            IEnumerable<EnchantmentEffect> allEffects = GetRelevantEffects(item);

            // Make sure they implement IOnHitEffects
            IEnumerable<EnchantmentEffect> relevantEffects = allEffects.Where(it => it.GetType().GetInterface(nameof(IModifyHitEffect)) != null);

            foreach (IModifyHitEffect effect in relevantEffects) {
                effect.OnModifyHit(target, this, item, ref damage, ref knockback, ref crit, hitDirection, proj);
            }
        }
        public void ApplyOnHitEnchants(Item item, NPC target, int damage, float knockback, bool crit, Projectile proj = null) {
            IEnumerable<EnchantmentEffect> allEffects = GetRelevantEffects(item);

            // Make sure they implement IOnHitEffects
            IEnumerable<EnchantmentEffect> relevantEffects = allEffects.Where(it => it.GetType().GetInterface(nameof(IOnHitEffect)) != null);

            foreach (IOnHitEffect effect in relevantEffects) {
                effect.OnAfterHit(target, this, item, damage, knockback, crit, proj); // Doesnt have to be reference damage, but it is for now.
            }

            //if (target.type == NPCID.TargetDummy)
            //    return;

            int oneForAllDamage = 0;// ActivateOneForAll();

            ApplyLifeSteal(item, target, damage, oneForAllDamage);
        }

        #endregion

        #region Enchantment Stat effect definitions
        public void ApplyLifeSteal(Item item, NPC npc, int damage, int oneForAllDamage) {
            if (!canLifeSteal)
                return;

            Player player = Player;

            // TODO: Make stack with one for all
            float healTotal = (damage + oneForAllDamage) * lifeSteal * (player.moonLeech ? 0.5f : 1f) + lifeStealRollover;

            int heal = (int)healTotal;

            if (player.statLife < player.statLifeMax2) {

                //Player hp less than max
                if (heal > 0 && player.lifeSteal > 0f) {
                    //Vanilla lifesteal mitigation
                    int vanillaLifeStealValue = (int)Math.Round(heal * ConfigValues.AffectOnVanillaLifeStealLimit);
                    player.lifeSteal -= vanillaLifeStealValue;

                    Projectile.NewProjectile(item.GetSource_ItemUse(item), npc.Center, new Vector2(0, 0), ProjectileID.VampireHeal, 0, 0f, player.whoAmI, player.whoAmI, heal);
                }

                //Life Steal Rollover
                lifeStealRollover = healTotal - heal;
            }
            else {
                //Player hp is max
                lifeStealRollover = 0f;
            }
        }

        #endregion

        public bool CheckShiftClickValid(ref Item item, bool moveItem = false) {
            if (WEModSystem.PromptInterfaceActive)
                return false;

            bool valid = false;
            if (Main.mouseItem.IsAir) {
                //Trash Item
                if (!Player.trashItem.IsAir) {
                    if (Player.trashItem.TryGetEnchantedItem(out EnchantedItem tGlobal) && !tGlobal.trashItem) {
                        if (trackedTrashItem.TryGetEnchantedItem(out EnchantedItem trackedTrashGlobal))
                            trackedTrashGlobal.trashItem = false;

                        tGlobal.trashItem = true;
                    }
                }
                else if (trackedTrashItem.TryGetEnchantedItem(out EnchantedItem trackedTrashGlobal)) {
                    trackedTrashGlobal.trashItem = false;
                }

                bool hoveringOverTrash = false;
                if (!item.IsAir) {
                    if (item.TryGetEnchantedItem(out EnchantedItem iGlobal) && iGlobal.trashItem)
                        hoveringOverTrash = true;
                }

                bool allowShiftClick = WEMod.clientConfig.AllowShiftClickMoveFavoritedItems;
                bool canMoveItem = !item.favorited || allowShiftClick;

                if (!hoveringOverTrash && canMoveItem) {
                    Item tableItem = enchantingTableUI.itemSlotUI[0].Item;

                    if (item.type == PowerBooster.ID && enchantingTableUI.itemSlotUI[0].Item.TryGetEnchantedItem(out EnchantedItem tableItemGlobal) && !tableItemGlobal.PowerBoosterInstalled) {
                        //Power Booster
                        if (moveItem) {
                            tableItemGlobal.PowerBoosterInstalled = true;
                            if (item.stack > 1) {
                                item.stack--;
                            }
                            else {
                                item = new Item();
                            }

                            SoundEngine.PlaySound(SoundID.Grab);
                        }

                        valid = true;
                    }
                    else {
                        //Check/Move item
                        for (int i = 0; i < EnchantingTable.maxItems; i++) {
                            if (enchantingTableUI.itemSlotUI[i].Valid(item)) {
                                if (!item.IsAir) {
                                    bool doNotSwap = false;
                                    if (item.TryGetEnchantedItem(out EnchantedItem iGlobal)) {
                                        if (iGlobal.equippedInArmorSlot && !tableItem.IsAir) {
                                            bool tryingToSwapArmor = IsAccessoryItem(item) && !IsArmorItem(item) && (IsAccessoryItem(tableItem) || IsArmorItem(tableItem));
                                            bool armorTypeDoesntMatch = item.headSlot > -1 && tableItem.headSlot == -1 || item.bodySlot > -1 && tableItem.bodySlot == -1 || item.legSlot > -1 && tableItem.legSlot == -1;
                                            if (tryingToSwapArmor || armorTypeDoesntMatch)
                                                doNotSwap = true;//Fix for Armor Modifiers & Reforging setting item.accessory to true to allow reforging armor
                                        }
                                    }

                                    if (!doNotSwap) {
                                        if (moveItem) {
                                            enchantingTableUI.itemSlotUI[i].Item = item.Clone();
                                            item = itemInEnchantingTable ? itemBeingEnchanted : new Item();
                                            SoundEngine.PlaySound(SoundID.Grab);
                                        }

                                        valid = true;

                                        break;
                                    }
                                }
                            }
                        }

                        if (!valid) {
                            //Check/Move Enchantment
                            if (item.ModItem is Enchantment enchantment) {
                                int uniqueItemSlot = WEUIItemSlot.FindSwapEnchantmentSlot(enchantment, enchantingTableUI.itemSlotUI[0].Item);
                                for (int i = 0; i < EnchantingTable.maxEnchantments; i++) {
                                    if (!enchantingTableUI.enchantmentSlotUI[i].Valid(item))
                                        continue;

                                    if (item.IsAir)
                                        continue;

                                    if (enchantingTableUI.enchantmentSlotUI[i].Item.IsAir && uniqueItemSlot == -1) {
                                        //Empty slot or not a unique enchantment
                                        if (moveItem) {
                                            int s = i;
                                            //Utility
                                            int maxIndex = EnchantingTable.maxEnchantments - 1;
                                            if (enchantment.Utility && enchantingTableUI.enchantmentSlotUI[maxIndex].Item.IsAir) {
                                                bool utilitySlotAllowedOnItem = WEUIItemSlot.SlotAllowedByConfig(tableItem, 1);
                                                if (utilitySlotAllowedOnItem)
                                                    s = maxIndex;
                                            }

                                            enchantingTableUI.enchantmentSlotUI[s].Item = item.Clone();
                                            enchantingTableUI.enchantmentSlotUI[s].Item.stack = 1;
                                            if (item.stack > 1) {
                                                item.stack--;
                                            }
                                            else {
                                                item = new Item();
                                            }

                                            SoundEngine.PlaySound(SoundID.Grab);
                                        }

                                        valid = true;

                                        break;
                                    }
                                    else {
                                        bool uniqueEnchantmentOnItem = enchantingTableUI.enchantmentSlotUI[i].CheckUniqueSlot(enchantment, uniqueItemSlot);
                                        if (uniqueItemSlot != -1 && uniqueEnchantmentOnItem && item.type != enchantingTableUI.enchantmentSlotUI[i].Item.type) {
                                            //Check unique can swap
                                            if (moveItem) {
                                                Item returnItem = enchantingTableUI.enchantmentSlotUI[i].Item.Clone();
                                                enchantingTableUI.enchantmentSlotUI[i].Item = item.Clone();
                                                enchantingTableUI.enchantmentSlotUI[i].Item.stack = 1;
                                                if (!returnItem.IsAir) {
                                                    if (item.stack > 1) {
                                                        Player.QuickSpawnItem(Player.GetSource_Misc("PlayerDropItemCheck"), returnItem);
                                                        item.stack--;
                                                    }
                                                    else {
                                                        item = returnItem;
                                                    }
                                                }
                                                else {
                                                    item = new Item();
                                                }

                                                SoundEngine.PlaySound(SoundID.Grab);
                                            }

                                            valid = true;

                                            break;
                                        }
                                    }
                                }
                            }
                        }

                        //Check/Move Essence
                        if (!valid) {
                            for (int i = 0; i < EnchantingTable.maxEssenceItems; i++) {
                                if (enchantingTableUI.essenceSlotUI[i].Valid(item)) {
                                    if (!item.IsAir) {
                                        bool canTransfer = false;
                                        if (enchantingTableUI.essenceSlotUI[i].Item.IsAir) {
                                            //essence slot empty
                                            if (moveItem) {
                                                enchantingTableUI.essenceSlotUI[i].Item = item.Clone();
                                                item = new Item();
                                            }

                                            canTransfer = true;
                                        }
                                        else {
                                            //Essence slot not empty
                                            if (enchantingTableUI.essenceSlotUI[i].Item.stack < EnchantmentEssence.maxStack) {
                                                if (moveItem) {
                                                    int ammountToTransfer;
                                                    if (item.stack + enchantingTableUI.essenceSlotUI[i].Item.stack > EnchantmentEssence.maxStack) {
                                                        ammountToTransfer = EnchantmentEssence.maxStack - enchantingTableUI.essenceSlotUI[i].Item.stack;
                                                        item.stack -= ammountToTransfer;
                                                    }
                                                    else {
                                                        ammountToTransfer = item.stack;
                                                        item.stack = 0;
                                                    }

                                                    enchantingTableUI.essenceSlotUI[i].Item.stack += ammountToTransfer;
                                                }

                                                canTransfer = true;
                                            }
                                        }

                                        //Common to all essence transfer
                                        if (canTransfer) {
                                            if (moveItem)
                                                SoundEngine.PlaySound(SoundID.Grab);

                                            valid = true;

                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                if (!valid && moveItem) {
                    //Pick up item
                    Main.mouseItem = item.Clone();
                    item = new Item();
                }
                else if (valid && !moveItem && !hoveringOverTrash) {
                    Main.cursorOverride = 9;
                }
            }
            else if (item.IsAir && moveItem) {
                //Put item down
                item = Main.mouseItem.Clone();
                Main.mouseItem = new Item();
            }

            return valid;
        }
        public bool ItemChanged(Item current, Item previous, bool weapon = false) {
            if (current != null && !current.IsAir) {
                if (previous == null) {
                    return true;
                }

                if (previous.IsAir) {
                    return true;
                }
                else if (current.TryGetEnchantedItem(out EnchantedItem cGlobal) && (weapon && !cGlobal.trackedWeapon || !weapon && !cGlobal.equippedInArmorSlot)) {
                    return true;
                }
            }
            else if (previous != null && !previous.IsAir) {
                return true;
            }

            return false;
        }
        public void UpdatePotionBuffs(ref Item newItem, ref Item oldItem) {

            #region Debug

            if (LogMethods.debugging) ($"\\/UpdatePotionBuffs(" + newItem.S() + ", " + oldItem.S() + ")").Log();

            #endregion

            UpdatePotionBuff(ref newItem);
            UpdatePotionBuff(ref oldItem, true);

            #region Debug

            if (LogMethods.debugging) ($"/\\UpdatePotionBuffs(" + newItem.S() + ", " + oldItem.S() + ")").Log();

            #endregion
        }
        private void UpdatePotionBuff(ref Item item, bool remove = false) {

            #region Debug

            if (LogMethods.debugging) ($"\\/UpdatePotionBuff(" + item.S() + ", remove: " + remove + ")").Log();

            #endregion

            if (!item.TryGetEnchantedItem(out EnchantedItem iGlobal))
                return;

            WEPlayer wePlayer = Player.GetModPlayer<WEPlayer>();

            #region Debug

            if (LogMethods.debugging) ($"potionBuffs.Count: {buffs.Count}, iGlobal.potionBuffs.Count: {iGlobal.buffs.Count}").Log();

            #endregion

            foreach (int key in iGlobal.buffs.Keys) {

                #region Debug

                if (LogMethods.debugging) ($"player: {buffs.S(key)}, item: {iGlobal.buffs.S(key)}").Log();

                #endregion

                if (wePlayer.buffs.ContainsKey(key)) {
                    wePlayer.buffs[key] += iGlobal.buffs[key] * (remove ? -1 : 1);
                }
                else {
                    wePlayer.buffs.Add(key, iGlobal.buffs[key]);
                }

                if (remove && wePlayer.buffs[key] < 1)
                    wePlayer.buffs.Remove(key);

                #region Debug

                if (LogMethods.debugging) ($"player: {buffs.S(key)}, item: {iGlobal.buffs.S(key)}").Log();

                #endregion
            }

            #region Debug

            if (LogMethods.debugging) ($"/\\UpdatePotionBuff(" + item.S() + ", remove: " + remove + ")").Log();

            #endregion
        }
        public static StatModifier CombineStatModifier(StatModifier baseStatModifier, StatModifier newStatModifier, bool remove) {

            #region Debug

            if (LogMethods.debugging) ($"\\/CombineStatModifier(baseStatModifier: " + baseStatModifier.S() + ", newStatModifier: " + newStatModifier.S() + ", remove: " + remove + ") StatModifier").Log();

            #endregion

            StatModifier finalModifier;
            if (remove) {
                finalModifier = new StatModifier(baseStatModifier.Additive / newStatModifier.Additive, baseStatModifier.Multiplicative / newStatModifier.Multiplicative, baseStatModifier.Flat - newStatModifier.Flat, baseStatModifier.Base - newStatModifier.Base);
            }
            else {
                finalModifier = new StatModifier(baseStatModifier.Additive * newStatModifier.Additive, baseStatModifier.Multiplicative * newStatModifier.Multiplicative, baseStatModifier.Flat + newStatModifier.Flat, baseStatModifier.Base + newStatModifier.Base);
            }

            #region Debug

            if (LogMethods.debugging) ($"/\\CombineStatModifier(baseStatModifier: " + baseStatModifier.S() + ", newStatModifier: " + newStatModifier.S() + ", remove: " + remove + ") return " + finalModifier.S()).Log();

            #endregion

            return finalModifier;
        }
        public static StatModifier InverseCombineWith(StatModifier baseStatModifier, StatModifier newStatModifier, bool remove) {

            #region Debug

            if (LogMethods.debugging) ($"\\/InverseCombineWith(baseStatModifier: " + baseStatModifier.S() + ", newStatModifier: " + newStatModifier.S() + ", remove: " + remove + ") StatModifier").Log();

            #endregion

            StatModifier newInvertedStatModifier;
            if (remove) {
                newInvertedStatModifier = new StatModifier(2f - newStatModifier.Additive, 1f / newStatModifier.Multiplicative, -newStatModifier.Flat, -newStatModifier.Base);
            }
            else {
                newInvertedStatModifier = newStatModifier;
            }

            #region Debug

            if (LogMethods.debugging) ($"newInvertedStatModifier: " + newInvertedStatModifier.S()).Log();

            #endregion

            StatModifier finalModifier = baseStatModifier.CombineWith(newInvertedStatModifier);

            #region Debug

            if (LogMethods.debugging) ($"/\\InverseCombineWith(baseStatModifier: " + baseStatModifier.S() + ", newStatModifier: " + newStatModifier.S() + ", remove: " + remove + ") return " + finalModifier.S()).Log();

            #endregion

            return finalModifier;
        }
        public static void TryRemoveStat(ref Dictionary<string, StatModifier> dictionary, string key) {

            #region Debug

            if (LogMethods.debugging) ($"\\/TryRemoveStat( dictionary, key: " + key + ") dictionary: " + dictionary.S(key)).Log();

            #endregion

            if (dictionary.ContainsKey(key)) {
                if ((float)Math.Abs(Math.Abs(Math.Round(dictionary[key].Additive, 4)) - 1f) < 1E-4 && (float)Math.Abs(Math.Abs(Math.Round(dictionary[key].Multiplicative, 4)) - 1f) < 1E-4 && Math.Abs(Math.Round(dictionary[key].Flat, 4)) < 1E-4 && Math.Abs(Math.Round(dictionary[key].Base, 4)) < 1E-4) {
                    dictionary.Remove(key);
                }
            }

            #region Debug

            if (LogMethods.debugging) ($"/\\TryRemoveStat( dictionary, key: " + key + ") dictionary: " + dictionary.S(key)).Log();

            #endregion
        }
        public void UpdatePlayerStats(ref Item newItem, ref Item oldItem) {

            #region Debug

            if (LogMethods.debugging) ($"\\/UpdatePlayerStats(" + newItem.S() + ", " + oldItem.S() + ")").Log();

            #endregion

            if (IsEnchantable(newItem))
                UpdatePlayerDictionaries(newItem);

            if (IsEnchantable(oldItem))
                UpdatePlayerDictionaries(oldItem, true);

            if (IsEnchantable(newItem))
                UpdateItemStats(ref newItem);

            if (!IsWeaponItem(newItem)) {
                Item weapon = null;
                if (Player.HeldItem.TryGetEnchantedItem(out EnchantedItem iGlobal) && iGlobal.trackedWeapon) {
                    weapon = Player.HeldItem;
                }
                else if (Main.mouseItem.TryGetEnchantedItem(out EnchantedItem mGlobal) && mGlobal.trackedWeapon) {
                    weapon = Main.mouseItem;
                }

                if (weapon != null)
                    UpdateItemStats(ref weapon);
            }

            UpdatePlayerStat();

            #region Debug

            if (LogMethods.debugging) ($"/\\UpdatePlayerStats(" + newItem.S() + ", " + oldItem.S() + ")").Log();

            #endregion
        }
        private void UpdatePlayerStat() {

            #region Debug

            if (LogMethods.debugging) ($"\\/UpdatePlayerStat()").Log();

            #endregion

            foreach (string key in statModifiers.Keys) {
                string statName = key.RemoveInvert();
                bool statsNeedUpdate = true;
                if (appliedStatModifiers.ContainsKey(key))
                    statsNeedUpdate = statModifiers[key] != appliedStatModifiers[key];

                if (statsNeedUpdate) {
                    Type playerType = Player.GetType();
                    FieldInfo field = playerType.GetField(statName);
                    PropertyInfo property = playerType.GetProperty(statName);
                    if (field != null || property != null) {
                        if (!appliedStatModifiers.ContainsKey(key))
                            appliedStatModifiers.Add(key, StatModifier.Default);

                        StatModifier lastAppliedStatModifier = appliedStatModifiers[key];
                        appliedStatModifiers[key] = statModifiers[key];
                        StatModifier staticStat = CombineStatModifier(statModifiers[key], lastAppliedStatModifier, true);
                        if (field != null) {
                            Type fieldType = field.FieldType;
                            //float (field)
                            if (fieldType == typeof(float)) {
                                float finalValue = staticStat.ApplyTo((float)field.GetValue(Player));
                                field.SetValue(Player, finalValue);
                            }

                            //int (field)
                            if (fieldType == typeof(int)) {
                                float finalValue = staticStat.ApplyTo((float)(int)field.GetValue(Player));
                                field.SetValue(Player, (int)Math.Round(finalValue + 5E-6));
                            }

                            //bool (field)
                            if (fieldType == typeof(bool)) {
                                bool baseValue = (bool)field.GetValue(new Player());
                                bool finalValue = statModifiers[key].Additive != 1f;
                                field.SetValue(Player, !statModifiers.ContainsKey("P_" + key) && (baseValue || finalValue));
                            }
                        }
                        else if (property != null) {
                            Type propertyType = property.PropertyType;
                            //float (property)
                            if (propertyType == typeof(float)) {
                                float finalValue = staticStat.ApplyTo((float)property.GetValue(Player));
                                property.SetValue(Player, finalValue);
                            }

                            //int (property)
                            if (propertyType == typeof(int)) {
                                float finalValue = staticStat.ApplyTo((float)(int)property.GetValue(Player));
                                property.SetValue(Player, (int)Math.Round(finalValue + 5E-6));
                            }

                            //bool (property)
                            if (propertyType == typeof(bool)) {
                                bool baseValue = (bool)property.GetValue(new Player());
                                bool finalValue = statModifiers[key].Additive != 1f;
                                property.SetValue(Player, !statModifiers.ContainsKey("P_" + key) && (baseValue || finalValue));
                            }
                        }
                    }
                }
            }

            #region Debug

            if (LogMethods.debugging) ($"/\\UpdatePlayerStat()").Log();

            #endregion
        }
        private void UpdatePlayerDictionaries(Item item, bool remove = false) {
            if (!item.TryGetEnchantedItem(out EnchantedItem iGlobal))
                return;

            #region Debug

            if (LogMethods.debugging) ($"\\/UpdatePlayerDictionaries(" + item.S() + ", remove: " + remove + ") statModifiers.Count: " + iGlobal.statModifiers.Count).Log();

            #endregion

            foreach (string key in iGlobal.statModifiers.Keys) {
                string statName = key.RemoveInvert();
                if (!IsWeaponItem(item) || item.GetType().GetField(statName) == null && item.GetType().GetProperty(statName) == null) {
                    if (!statModifiers.ContainsKey(key))
                        statModifiers.Add(key, StatModifier.Default);

                    statModifiers[key] = InverseCombineWith(statModifiers[key], iGlobal.statModifiers[key], remove);
                    TryRemoveStat(ref statModifiers, key);
                }
            }

            if (!IsWeaponItem(item)) {
                foreach (string key in iGlobal.eStats.Keys) {
                    if (!eStats.ContainsKey(key))
                        eStats.Add(key, StatModifier.Default);

                    eStats[key] = InverseCombineWith(eStats[key], iGlobal.eStats[key], remove);
                    TryRemoveStat(ref eStats, key);
                }
            }

            #region Debug

            if (LogMethods.debugging) ($"/\\UpdatePlayerDictionaries(" + item.S() + ", remove: " + remove + ")").Log();

            #endregion
        }
        public void UpdateItemStats(ref Item item) {
            if (!item.TryGetEnchantedItem(out EnchantedItem iGlobal))
                return;

            #region Debug

            if (LogMethods.debugging) ($"\\/UpdateItemStats(" + item.S() + ")").Log();

            #endregion

            //Prefix
            int trackedPrefix = iGlobal.prefix;
            if (trackedPrefix != item.prefix) {
                iGlobal.appliedStatModifiers.Clear();
                iGlobal.appliedEStats.Clear();
                iGlobal.prefix = item.prefix;
                int damageType = iGlobal.damageType;
                if (damageType > -1)
                    item.UpdateDamageType(damageType);
            }

            int infusedArmorSlot = iGlobal.infusedArmorSlot;
            int armorSlot = item.GetInfusionArmorSlot(false, true);
            if (infusedArmorSlot != -1 && armorSlot != infusedArmorSlot)
                item.UpdateArmorSlot(iGlobal.infusedArmorSlot);

            //Populate itemStatModifiers
            Dictionary<string, StatModifier> combinedStatModifiers = new Dictionary<string, StatModifier>();
            foreach (string itemKey in iGlobal.statModifiers.Keys) {
                string riItemKey = itemKey.RemoveInvert().RemovePrevent();
                if (item.GetType().GetField(riItemKey) != null || item.GetType().GetProperty(riItemKey) != null) {
                    StatModifier riStatModifier = itemKey.ContainsInvert() ? CombineStatModifier(StatModifier.Default, iGlobal.statModifiers[itemKey], true) : iGlobal.statModifiers[itemKey];
                    if (combinedStatModifiers.ContainsKey(riItemKey))
                        combinedStatModifiers[riItemKey] = combinedStatModifiers[riItemKey].CombineWith(riStatModifier);
                    else
                        combinedStatModifiers.Add(riItemKey, riStatModifier);

                    #region Debug

                    if (LogMethods.debugging) ($"combinedStatModifiers.Add(itemKey: " + itemKey + ", " + iGlobal.statModifiers.S(itemKey) + ")").Log();

                    #endregion
                }
            }

            //Populate playerStatModifiers if item is a weapon
            if (IsWeaponItem(item)) {
                foreach (string playerKey in statModifiers.Keys) {
                    string riPlayerKey = playerKey.RemoveInvert().RemovePrevent();
                    if (item.GetType().GetField(riPlayerKey) != null || item.GetType().GetProperty(riPlayerKey) != null) {
                        StatModifier riStatModifier = playerKey.ContainsInvert() ? CombineStatModifier(StatModifier.Default, statModifiers[playerKey], true) : statModifiers[playerKey];
                        if (combinedStatModifiers.ContainsKey(riPlayerKey)) {
                            combinedStatModifiers[riPlayerKey] = combinedStatModifiers[riPlayerKey].CombineWith(riStatModifier);
                        }
                        else {
                            combinedStatModifiers.Add(riPlayerKey, riStatModifier);
                        }

                        #region Debug

                        if (LogMethods.debugging) ($"combinedStatModifiers.Add(playerKey: " + playerKey + ", " + iGlobal.statModifiers.S(playerKey) + ")").Log();

                        #endregion
                    }
                }
            }

            foreach (string key in iGlobal.appliedStatModifiers.Keys) {
                if (!combinedStatModifiers.ContainsKey(key))
                    combinedStatModifiers.Add(key, StatModifier.Default);
            }

            foreach (string key in combinedStatModifiers.Keys) {
                bool statsNeedUpdate = true;
                if (iGlobal.appliedStatModifiers.ContainsKey(key))
                    statsNeedUpdate = combinedStatModifiers[key] != iGlobal.appliedStatModifiers[key];

                #region Debug

                if (LogMethods.debugging) ($"statsNeedUpdate: " + statsNeedUpdate + " combinedStatModifiers[" + key + "]: " + combinedStatModifiers.S(key) + " != item.G().appliedStatModifiers[" + key + "]: " + iGlobal.appliedStatModifiers.S(key)).Log();

                #endregion

                if (statsNeedUpdate) {
                    FieldInfo field = item.GetType().GetField(key);
                    PropertyInfo property = item.GetType().GetProperty(key);
                    if (field != null || property != null) {
                        if (!iGlobal.appliedStatModifiers.ContainsKey(key))
                            iGlobal.appliedStatModifiers.Add(key, StatModifier.Default);

                        StatModifier lastAppliedStatModifier = iGlobal.appliedStatModifiers[key];
                        iGlobal.appliedStatModifiers[key] = combinedStatModifiers[key];
                        StatModifier staticStat = CombineStatModifier(combinedStatModifiers[key], lastAppliedStatModifier, true);
                        if (field != null) {
                            Type fieldType = field.FieldType;
                            //float (field)
                            if (fieldType == typeof(float)) {
                                float finalValue = staticStat.ApplyTo((float)field.GetValue(item));
                                field.SetValue(item, finalValue);
                            }

                            //int (field)
                            if (fieldType == typeof(int)) {
                                float finalValue = staticStat.ApplyTo((float)(int)field.GetValue(item));
                                staticStat.RoundCheck(ref finalValue, (int)field.GetValue(item), iGlobal.appliedStatModifiers[key], (int)field.GetValue(ContentSamples.ItemsByType[item.type]));
                                field.SetValue(item, (int)Math.Round(finalValue + 5E-6));
                            }

                            //bool (field)
                            if (fieldType == typeof(bool)) {
                                bool baseValue = (bool)field.GetValue(ContentSamples.ItemsByType[item.type]);
                                bool finalValue = combinedStatModifiers[key].Additive > 1.001f;
                                bool containtPrevent = iGlobal.statModifiers.ContainsKey("P_" + key) && iGlobal.statModifiers["P_" + key].Additive > 1.001f || statModifiers.ContainsKey("P_" + key) && statModifiers["P_" + key].Additive > 1.001f;
                                bool setValue = !containtPrevent && (baseValue || finalValue);
                                field.SetValue(item, setValue);
                            }
                        }
                        else if (property != null) {
                            Type propertyType = property.PropertyType;
                            //float (property)
                            if (propertyType == typeof(float)) {
                                float finalValue = staticStat.ApplyTo((float)property.GetValue(item));
                                property.SetValue(item, finalValue);
                            }

                            //int (property)
                            if (propertyType == typeof(int)) {
                                float finalValue = staticStat.ApplyTo((float)(int)property.GetValue(item));
                                staticStat.RoundCheck(ref finalValue, (int)property.GetValue(item), iGlobal.appliedStatModifiers[key], (int)property.GetValue(ContentSamples.ItemsByType[item.type]));
                                property.SetValue(item, (int)Math.Round(finalValue + 5E-6));
                            }

                            //bool (property)
                            if (propertyType == typeof(bool)) {
                                bool baseValue = (bool)property.GetValue(ContentSamples.ItemsByType[item.type]);
                                bool finalValue = combinedStatModifiers[key].Additive != 1f;
                                property.SetValue(item, !combinedStatModifiers.ContainsKey("P_" + key) && (baseValue || finalValue));
                            }
                        }
                    }
                }
            }

            //Populate itemeStats
            Dictionary<string, StatModifier> combinedEStats = new Dictionary<string, StatModifier>();
            foreach (string itemKey in iGlobal.eStats.Keys) {
                string riItemKey = itemKey.RemoveInvert();
                StatModifier riEStat = itemKey.ContainsInvert() ? CombineStatModifier(StatModifier.Default, iGlobal.eStats[itemKey], true) : iGlobal.eStats[itemKey];
                if (combinedEStats.ContainsKey(riItemKey)) {
                    combinedEStats[riItemKey] = combinedEStats[riItemKey].CombineWith(riEStat);
                }
                else {
                    combinedEStats.Add(riItemKey, riEStat);
                }

                #region Debug

                if (LogMethods.debugging) ($"combinedEStats.Add(itemKey: " + itemKey + ", " + iGlobal.eStats.S(itemKey) + ")").Log();

                #endregion
            }

            //Populate playereStats if item is a weapon
            if (IsWeaponItem(item)) {
                foreach (string playerKey in eStats.Keys) {
                    string riPlayerKey = playerKey.RemoveInvert();
                    StatModifier riEStat = playerKey.ContainsInvert() ? CombineStatModifier(StatModifier.Default, eStats[playerKey], true) : eStats[playerKey];
                    if (combinedEStats.ContainsKey(riPlayerKey)) {
                        combinedEStats[riPlayerKey] = combinedEStats[riPlayerKey].CombineWith(riEStat);
                    }
                    else {
                        combinedEStats.Add(riPlayerKey, riEStat);
                    }

                    #region Debug

                    if (LogMethods.debugging) ($"combinedEStats.Add(riPlayerKey: " + riPlayerKey + ", " + iGlobal.eStats.S(playerKey) + ")").Log();

                    #endregion
                }
            }

            foreach (string key in iGlobal.appliedEStats.Keys) {
                if (!combinedEStats.ContainsKey(key))
                    combinedEStats.Add(key, StatModifier.Default);
            }

            foreach (string key in combinedEStats.Keys) {
                bool statsNeedUpdate = true;
                if (iGlobal.appliedEStats.ContainsKey(key))
                    statsNeedUpdate = combinedEStats[key] != iGlobal.appliedEStats[key];

                if (statsNeedUpdate) {
                    if (!iGlobal.appliedEStats.ContainsKey(key)) {
                        iGlobal.appliedEStats.Add(key, combinedEStats[key]);
                    }
                    else {
                        iGlobal.appliedEStats[key] = combinedEStats[key];
                    }
                }
            }

            foreach (string key in iGlobal.statModifiers.Keys)
                TryRemoveStat(ref iGlobal.statModifiers, key);

            foreach (string key in iGlobal.appliedStatModifiers.Keys)
                TryRemoveStat(ref iGlobal.appliedStatModifiers, key);

            foreach (string key in iGlobal.eStats.Keys)
                TryRemoveStat(ref iGlobal.eStats, key);

            foreach (string key in iGlobal.appliedEStats.Keys)
                TryRemoveStat(ref iGlobal.appliedEStats, key);

            #region Debug

            if (LogMethods.debugging) ($"/\\UpdateItemStats(" + item.S() + ")").Log();

            #endregion
        }
    }
    public static class PlayerFunctions {
        public static void CheckWeapon(this Item newItem, ref Item oldItem, Player player, int slot) {
            WEPlayer wePlayer = player.GetModPlayer<WEPlayer>();
            bool checkWeapon = wePlayer.ItemChanged(newItem, oldItem, true);
            //Check HeldItem
            if (checkWeapon) {

                #region Debug

                if (LogMethods.debugging) ($"\\/CheckWeapon({newItem.S()}, {oldItem.S()}, player: {player.S()}, slot: {slot}) ").Log();

                #endregion

                if (!newItem.IsAir && newItem.TryGetEnchantedItem(out EnchantedItem newGlobal))
                    newGlobal.trackedWeapon = true;

                if (!oldItem.IsAir && oldItem.TryGetEnchantedItem(out EnchantedItem oldGlobal))
                    oldGlobal.trackedWeapon = false;

                Item newCheckItem = IsWeaponItem(newItem) ? newItem : new Item();
                Item oldCheckItem = IsWeaponItem(oldItem) ? oldItem : new Item();
                wePlayer.UpdatePotionBuffs(ref newCheckItem, ref oldCheckItem);
                wePlayer.UpdatePlayerStats(ref newCheckItem, ref oldCheckItem);

                oldItem = newItem;

                #region Debug

                if (LogMethods.debugging) ($"/\\CheckWeapon({newItem.S()}, {oldItem.S()}, player: {player.S()}, slot: {slot}) ").Log();

                #endregion
            }
        }
    }
}
