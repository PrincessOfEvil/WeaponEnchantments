﻿using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using WeaponEnchantments.Common;
using WeaponEnchantments.Common.Configs;
using System.Reflection;
using Terraria.GameContent.Creative;
using static WeaponEnchantments.Common.Configs.ConfigValues;
using static WeaponEnchantments.Common.Utility.LogModSystem;
using WeaponEnchantments.Common.Utility;
using static WeaponEnchantments.Common.EnchantingRarity;
using Terraria.Localization;
using System.Linq;
using WeaponEnchantments.Effects;

namespace WeaponEnchantments.Items {
    public enum DamageTypeSpecificID
	{
		Default,
		Generic,
		Melee,
		MeleeNoSpeed,
		Ranged,
		Magic,
		Summon,
		SummonMeleeSpeed,
		MagicSummonHybrid,
		Throwing
	} // Located in DamageClassLoader.cs

	public enum ArmorSlotSpecificID
	{
		Head,
		Body,
		Legs
	}
	public abstract class Enchantment : ModItem {

		#region Static

		public struct EnchantmentStrengths {
			public EnchantmentStrengths(float[] strengths) {
				enchantmentTierStrength = strengths;
			}
			public float[] enchantmentTierStrength = new float[tierNames.Length];
		}
		public static readonly EnchantmentStrengths[] defaultEnchantmentStrengths = new EnchantmentStrengths[] {
			new EnchantmentStrengths(new float[] { 0.03f, 0.08f, 0.16f, 0.25f, 0.40f }),
			new EnchantmentStrengths(new float[] { 0.4f, 0.8f, 1.2f, 1.6f, 2f }),//Not used yet
			new EnchantmentStrengths(new float[] { 1.2f, 1.4f, 1.6f, 1.8f, 2f }),
			new EnchantmentStrengths(new float[] { 1f, 2f, 3f, 5f, 10f }),
			new EnchantmentStrengths(new float[] { 2f, 4f, 6f, 10f, 20f }),
			new EnchantmentStrengths(new float[] { 0.005f, 0.01f, 0.015f, 0.02f, 0.025f }),
			new EnchantmentStrengths(new float[] { 2f, 3f, 5f, 8f, 10f }),
			new EnchantmentStrengths(new float[] { 0.02f, 0.04f, 0.06f, 0.08f, 0.10f }),
			new EnchantmentStrengths(new float[] { 0.5f, 0.6f, 0.75f, 0.85f, 1f }),
			new EnchantmentStrengths(new float[] { 0.6f, 0.65f, 0.7f, 0.8f, 0.9f }),
			new EnchantmentStrengths(new float[] { 0.2f, 0.4f, 0.6f, 0.8f, 1f }),
			new EnchantmentStrengths(new float[] { 0.04f, 0.08f, 0.12f, 0.16f, 0.20f }),
			new EnchantmentStrengths(new float[] { 0.14f, 0.18f, 0.22f, 0.26f, 0.30f })
		};//Need to manually update the StrengthGroup <summary> when changing defaultEnchantmentStrengths

		public static readonly int defaultBuffDuration = 60;

		#endregion

		#region Strength

		public float EnchantmentStrength { private set; get; }

		/// <summary>
		/// Default 0<br/>
		/// Sets the EnchantmentStrengths for the enchantment.<br/>
		/// Example: LifeSteal is StrengthGroup 5.  Tier 1 Lifesteal's Enchantment strength is 0.01f.<br/><br/>
		/// <list>
		/// <term>0</term><description>{ 0.03f, 0.08f, 0.16f, 0.25f, 0.40f }</description><br/>
		/// <term>1</term><description>{ 0.4f, 0.8f, 1.2f, 1.6f, 2f } Not used Yet</description><br/>
		/// <term>2</term><description>{ 1.2f, 1.4f, 1.6f, 1.8f, 2f }</description><br/>
		/// <term>3</term><description>{ 1f, 2f, 3f, 5f, 10f }</description><br/>
		/// <term>4</term><description>{ 2f, 4f, 6f, 10f, 20f }</description><br/>
		/// <term>5</term><description>{ 0.005f, 0.01f, 0.015f, 0.02f, 0.025f }</description><br/>
		/// <term>6</term><description>{ 2f, 3f, 5f, 8f, 10f }</description><br/>
		/// <term>7</term><description>{ 0.02f, 0.04f, 0.06f, 0.08f, 0.10f }</description><br/>
		/// <term>8</term><description>{ 0.5f, 0.6f, 0.75f, 0.85f, 1f }</description><br/>
		/// <term>9</term><description>{ 0.6f, 0.65f, 0.7f, 0.8f, 0.9f }</description><br/>
		/// <term>10</term><description>{ 0.2f, 0.4f, 0.6f, 0.8f, 1f }</description><br/>
		/// <term>11</term><description>{ 0.04f, 0.08f, 0.12f, 0.16f, 0.20f }</description><br/>
		/// <term>12</term><description>{ 0.14f, 0.18f, 0.22f, 0.26f, 0.30f }</description><br/>
		/// </list>
		/// </summary>
		public virtual int StrengthGroup { private set; get; } = 0;

		/// <summary>
		/// <para>
		/// Default 1f (All of the strength will be modified by the Recomended Strength Multiplier)<br/>
		/// Acceptable range: -1f to 1f<br/>
		/// Not required.  (Feel free to skip this and ask andro951 about it)<br/>
		/// Allows you to affect how much an enchantment is affected by the Recomended Strength multiplier from the config.<br/>
		/// </para><br/>
		/// <para>
		/// When would I need this? (This example is the Cold Steel damage multiplier at tier 4.  It desperatly needs a ScalePercent)<br/>
		/// The ScalePercent of Cold Steel is 2/9 = 0.22222.... for this example, I'm using 0.22f to make it a bit easier to read.<br/>
		/// <br/>
		/// Example: ScalePercent value of 0.2f, EnchantmentStrength of 0.9 (90%), RecomendedStrengthMultiplier of 0.25 (25%).<br/>
		///		Actual EnchantmentStrength value = ScalePercent * EnchantmentStrength * RecomendedStrengthMultiplier + (1f - ScalePercent) * EnchantmentStrength<br/>
		///		Actual EnchantmentStrength value = 0.22f * 0.9 * 0.25 + (1f - 0.22f) * 0.9f => 0.05f + 0.7f => 0.75f<br/>
		///	</para><br/>
		///	<para>
		///	If you did not set ScalePercent:<br/>
		///	Example: ScalePercent value of 1f, EnchantmentStrength of 0.9 (90%), RecomendedStrengthMultiplier of 0.25 (25%).<br/>
		///		Actual EnchantmentStrength value = ScalePercent * EnchantmentStrength * RecomendedStrengthMultiplier + (1f - ScalePercent) * EnchantmentStrength<br/>
		///		Actual EnchantmentStrength value = 1f * 0.9 * 0.25 + (1f - 1f) * 0.9f => 0.225f + 0f * 0.9f => 0.225f<br/>
		///	</para><br/>
		///	<para>
		/// Having a damage multiplier less than 1 on the Cold Steel enchantment is meant as a small nerf to offset the benefits of the enchantment.<br/>
		/// This number going from 0.9 (Normal config preset) => 0.75 (Master config preset) is a reasonable downgrade.<br/>
		/// If no ScalePercent was used, going from 0.9 (Normal config preset) => 0.225 (Master config preset) is a massive downgrade, making the enchantment worthless.<br/>
		/// <para><br/>
		/// </para>
		/// Note: To fix an issue with War and Peace, the ScalePercent can be set to a negative value.  Normally it should be 0f to 1f.<br/>
		/// Negative values are reverted to positive when calculating the Enchantment Strengths.  The only diference is it uses a different calculation.<br/>
		/// Negative values will cause 1f to be subtracted before calculating then add the 1f back after calculating.<br/>
		/// </para>
		/// </summary>
		public virtual float ScalePercent { private set; get; } = 1f;

		/// <summary>
		/// Allows you to manually adjust affect the cost of enchantments.
		/// Utility are 1f by default -> 1, 2, 3, 4, 5
		/// Normal are 2f by defualt -> 2, 4, 6, 8, 10
		/// Unique and Max1 are 3f by default -> 3, 6, 9, 12, 15
		/// Note: The null value I chose for this is -13.13f  That value will cause the defaults above to occur.
		/// </summary>
		public virtual float CapacityCostMultiplier { private set; get; } = -13.13f;

		public enum EItemType {
			None,
			Weapon,
			Armor,
			Accessory,
		}

		/// <summary>
		/// Default is { EItemType.Weapon, 1f }, { EItemType.Armor, 0.25f }, { EItemType.Accessory, 0.25f }<br/>
		/// (100% effective on weapons, 25% effective on armor and accessories)<br/>
		/// You must include ALL of the item types the enchantment can be applied on.  The above defaults are only set if you do not set the AllowedList.<br/>
		/// Example: Having just { EItemType.Weapon, 1f } will prevent the item being used on armor and accessories.<br/>
		/// </summary>
		public virtual Dictionary<EItemType, float> AllowedList { private set; get; } = new Dictionary<EItemType, float>() {
			{ EItemType.Weapon, 1f },
			{ EItemType.Armor, 0.33f },
			{ EItemType.Accessory, 0.2f }
		};

		#endregion

		#region Identifiers and names

		/// <summary>
		/// Not required.  Only include additional information to explain a complex enchantment.<br/>
		/// Static Stat, buff and debuff tooltips are all automatically generated.<br/>
		/// </summary>
		public virtual string CustomTooltip { private set; get; } = "";
		/// <summary>
		/// A value 0 - 4 representing the enchantment's tier.
		/// </summary>
		public virtual int EnchantmentTier => GetEnchantmentTier(Name);
		public string EnchantmentTypeName { get => Name.Substring(0, Name.IndexOf("Enchantment")); }

		/// <summary>
		/// DO NOT CHANGE THIS UNLESS YOU ARE POSITIVE YOU ARE SUPPOSED TO!!!<br/>
		/// This is a temporary fix for enchantments that have less than 5 tiers (0 through 4).<br/>
		/// This reduces the tier of the enchantment for the purposes of calculating the enchantment value from the essence used to craft it only.<br/>
		/// Only the potion buff enchantments such as Spelunker currently set this.<br/>
		/// Default 0<br/>
		/// Acceptable Range -4 to 0 (Potion buffs set to -2).<br/>
		/// -4 would cause the value of the enchantment to be almost zero.<br/>
		/// </summary>
		public virtual int EnchantmentValueTierReduction { private set; get; } = 0;

		/// <summary>
		/// Default 1<br/>
		/// Acceptable Range 1 to 5<br/><br/>
		/// <list>
		/// <term>0</term><description>All tiers are craftable.</description><br/>
		/// <term>1</term><description>Tier 1 and above are craftable.</description><br/>
		/// <term>2</term><description>Tier 2 and above are craftable.</description><br/>
		/// <term>3</term><description>Tier 3 and above are craftable.</description><br/>
		/// <term>4</term><description>Tier 4 is craftable.</description><br/>
		/// <term>5</term><description>No tiers are craftable.</description><br/>
		/// </list>
		/// </summary>
		public virtual int LowestCraftableTier { private set; get; } = 1;

		/// <summary>
		/// Default value will the the class name with spaces added.<br/>
		/// Not required.  Only override this if the class name is different than the desired in game name.<br/>
		/// Do not include the word "Enchantment" or the rarity words.  They are added automatically.<br/>
		/// Example: class name - ScaleEnchantmentBasic, desired display name - Size Enchantment Basic.<br/>
		///	MyDisplayName => "Size"<br/>
		/// </summary>
		public virtual string MyDisplayName { private set; get; } = "";

		/// <summary>
		/// Default true<br/>
		/// Set to false to prevent showing a "%" after the EnchantmentStrength in the tooltip.<br/>
		/// DO NOT set this if you add more than one stat if they shouldn't all be changed.<br/>
		/// If you need to change one specifically, do it in the GetPercentageMult100() method.<br/><br/>
		/// <list>
		/// <term>True</term><description> +40%</description><br/>
		/// <term>False</term><description> +40</description><br/>
		/// </list>
		/// </summary>
		//public virtual bool? ShowPercentSignInTooltip { private set; get; } = null;

		/// <summary>
		/// Default true<br/>
		/// Set to false to prevent multiplying the EnchantmentStrength by 100 in the tooltip.<br/>
		/// DO NOT set this if you add more than one stat if they shouldn't all be changed.<br/>
		/// If you need to change one specifically, do it in the GetPercentageMult100() method.<br/><br/>
		/// <list>
		/// <term>True</term><description> x40%</description><br/>
		/// <term>False</term><description> x0.4%</description><br/>
		/// </list>
		/// </summary>
		//public virtual bool? MultiplyBy100InTooltip { private set; get; } = null;

		/// <summary>
		/// Default true for Static Stats and false for all others.<br/>
		/// Set to true to show a "x" in the tooltip after the EnchantmentStrength.<br/>
		/// DO NOT set this if you add more than one stat if they shouldn't all be changed.<br/>
		/// If you need to change one specifically, do it in the GetPercentageMult100() method.<br/><br/>
		/// <list>
		/// <term>True</term><description> +40%</description><br/>
		/// <term>False</term><description> 40x</description><br/>
		/// </list>
		/// </summary>
		//public virtual bool? ShowPlusSignInTooltip { private set; get; } = null;
		//public string FullToolTip { private set; get; }
		//public Dictionary<EItemType, string> AllowedListTooltips { private set; get; } = new Dictionary<EItemType, string>();

		public abstract string Artist { get; }
		public abstract string Designer { get; }

		#endregion

		#region Restrictions and enchantment types

		/// <summary>
		/// Default false<br/>
		/// Automatically set to true if in the Utility folder.<br/>
		/// Note: It is actually based on the namespace, but the default namespace is the folder it's in.<br/>
		/// Manually changing the namespace will prevent this from being set.<br/>
		/// </summary>
		public bool Utility { private set; get; } = false;

		/// <summary>
		/// Default false<br/>
		/// Automatically set to true if in the Unique folder.<br/>
		/// Note: It is actually based on the namespace, but the default namespace is the folder it's in.<br/>
		/// Manually changing the namespace will prevent this from being set.<br/>
		/// </summary>
		public bool Unique { private set; get; } = false;

		/// <summary>
		/// Default false<br/>
		/// True will prevent more than 1 of this enchantment from being applied to an item.<br/>
		/// </summary>
		public virtual bool Max1 { private set; get; } = false;

		/// <summary>
		/// Default 0<br/>
		/// Makes an enchantment only allowed on weapons with the specified damage type.<br/>
		/// Please use the DamageTypeSpecificID enum for this.<br/>
		/// Example: DamageClassSpecific => (int)DamageTypeSpecificID.Melee<br/><br/>
		/// <list>
		/// <term>0</term><description>Generic</description><br/>
		/// <term>1</term><description>Melee</description><br/>
		/// <term>2</term><description>MeleeNoSpeed</description><br/>
		/// <term>3</term><description>Ranged</description><br/>
		/// <term>4</term><description>Magic</description><br/>
		/// <term>5</term><description>Summon</description><br/>
		/// <term>6</term><description>SummonMeleeSpeed</description><br/>
		/// <term>7</term><description>MagicSummonHybrid</description><br/>
		/// <term>8</term><description>Throwing</description><br/>
		/// </list>
		/// </summary>
		public virtual int DamageClassSpecific { private set; get; } = 0;

		/// <summary>
		/// Default -1<br/>
		/// Makes an enchantment only allowed on armor of the specified type.<br/>
		/// Please use the ArmorSlotSpecificID enum for this.<br/>
		/// Example: ArmorSlotSpecific => (int)ArmorSlotSpecificID.Head<br/><br/>
		/// <list>
		/// <term>0</term><description>Head</description><br/>
		/// <term>1</term><description>Body</description><br/>
		/// <term>2</term><description>Legs</description><br/>
		/// </list>
		/// </summary>
		public virtual int ArmorSlotSpecific { private set; get; } = -1;

		/// <summary>
		/// Default -1<br/>
		/// Prevents an enchantment from being applied on weapons with the specified damage type.<br/>
		/// Please use the DamageTypeSpecificID enum for this.<br/>
		/// Example: RestrictedClass => (int)DamageTypeSpecificID.Melee<br/><br/>
		/// <list>
		/// <term>0</term><description>Generic</description><br/>
		/// <term>1</term><description>Melee</description><br/>
		/// <term>2</term><description>MeleeNoSpeed</description><br/>
		/// <term>3</term><description>Ranged</description><br/>
		/// <term>4</term><description>Magic</description><br/>
		/// <term>5</term><description>Summon</description><br/>
		/// <term>6</term><description>SummonMeleeSpeed</description><br/>
		/// <term>7</term><description>MagicSummonHybrid</description><br/>
		/// <term>8</term><description>Throwing</description><br/>
		/// </list>
		/// </summary>
		public virtual int RestrictedClass { private set; get; } = -1;

		#endregion

		#region Stats and buffs

		private bool finishedOneTimeSetup = false;

		/// <summary>
		/// Default -1<br/>
		/// Converts a weapon's damage type to the specified type.<br/>
		/// Please use the DamageTypeSpecificID enum for this.<br/>
		/// Example: NewDamageType => (int)DamageTypeSpecificID.Melee<br/><br/>
		/// <list>
		/// <term>0</term><description>Generic</description><br/>
		/// <term>1</term><description>Melee</description><br/>
		/// <term>2</term><description>MeleeNoSpeed</description><br/>
		/// <term>3</term><description>Ranged</description><br/>
		/// <term>4</term><description>Magic</description><br/>
		/// <term>5</term><description>Summon</description><br/>
		/// <term>6</term><description>SummonMeleeSpeed</description><br/>
		/// <term>7</term><description>MagicSummonHybrid</description><br/>
		/// <term>8</term><description>Throwing</description><br/>
		/// </list>
		/// </summary>
		public virtual int NewDamageType { private set; get; } = -1;
		public int BuffDuration => GetBuffDuration();
		public List<int> Buff { private set; get; } = new List<int>();
		public Dictionary<int, int> OnHitBuff { private set; get; } = new Dictionary<int, int>();
		public Dictionary<int, int> Debuff { private set; get; } = new Dictionary<int, int>();
		public List<EnchantmentStaticStat> StaticStats { private set; get; } = new List<EnchantmentStaticStat>();
		public List<EStat> EStats { private set; get; } = new List<EStat>();
		public EnchantmentEffect[] Effects { protected set; get; } = new EnchantmentEffect[] { };

		#endregion

		public override string Texture => $"WeaponEnchantments/Items/Sprites/{Name}";

		/// <summary>
		/// Add to the stat Lists and Dictionaries here.<br/>
		/// If no changes are made here, the default stat will be: AddEStat(EnchantmentTypeName, 0f, 1f, 0f, EnchantmentStrength)<br/>
		/// <br/>
		/// Buff.Add(int)<br/>
		/// OnHitBuff.Add(int, int)<br/>
		/// Debuff.Add(int, int)<br/>
		/// AddStaticStat(string name, float additive = 0f, float multiplicative = 1f, float flat = 0f, float @base = 0f)<br/>
		/// AddEstat(string name, float additive = 0f, float multiplicative = 1f, float flat = 0f, float @base = 0f)<br/>
		/// <br/>
		/// Automated methods to add a stat based on the enchantment name:<br/>
		/// CheckStaticStatByName()<br/>
		/// CheckBuffByName()<br/>
		/// </summary>
		public virtual void GetMyStats() { } //Meant to be overriden in the specific Enchantment class.


        public override void ModifyTooltips(List<TooltipLine> tooltips) {
			var tooltipTuples = GenerateFullTooltip();
            foreach (var tooltipTuple in tooltipTuples) {
				tooltips.Add(new TooltipLine(Mod, "enchantment:base", tooltipTuple.Item1) { OverrideColor = tooltipTuple.Item2});
			}
		}

        public override void SetStaticDefaults() {
			//Get values needed to generate tooltips
			GetDefaults();// true);//Change this to have arguments to only get the needed info for setting up tooltips.

			//Journy mode item sacrifice
			CreativeItemSacrificesCatalog.Instance.SacrificeCountNeededByItemId[Type] = 1;

			//DisplayName
			//string typeNameString = "Mods.WeaponEnchantments.EnchantmentTypeNames." + EnchantmentTypeName;
			//typeNameString.Log();
			//string displayName = Language.GetTextValue(typeNameString) + " " + Language.GetTextValue("Mods.WeaponEnchantments.Enchantment");
			if (WEMod.clientConfig.UseOldTierNames) {
				//Old rarity names, "Basic", "Common", "Rare", "SuperRare", "UltraRare"
				//string rarityString = "Mods.WeaponEnchantments.TierNames." + displayTierNames[EnchantmentTier];
				//rarityString.Log();
				//DisplayName.SetDefault(displayName + " " + Language.GetTextValue(rarityString));
				DisplayName.SetDefault(StringManipulation.AddSpaces(MyDisplayName + Name.Substring(Name.IndexOf("Enchantment"))));
			}
			else {
				//Current rarity names, "Basic", "Common", "Rare", "Epic", "Legendary"
				//string rarityString = "Mods.WeaponEnchantments.DisplayTierNames." + displayTierNames[EnchantmentTier];
				//rarityString.Log();
				//DisplayName.SetDefault(displayName + " " + Language.GetTextValue(rarityString));
				DisplayName.SetDefault(StringManipulation.AddSpaces(MyDisplayName + "Enchantment" + displayTierNames[EnchantmentTier]));
			}

			//Only used to print the full list of enchantment tooltips in WEPlayer OnEnterWorld()
			if(printListOfEnchantmentTooltips)
				listOfAllEnchantmentTooltips += $"{Name}\n{Tooltip.GetDefault()}\n\n";

			if(printListOfContributors && (EnchantmentTier == 1 || EnchantmentTypeName == "AllForOne")) {
				//All for one is allowed to pass every sprite
				bool allForOne = EnchantmentTypeName == "AllForOne";

				UpdateContributorsList(this, allForOne ? null : EnchantmentTypeName);
			}

			if(printLocalization && EnchantmentTier == 1) {
				UpdateEnchantmentLocalization(this);
			}
		}
		private void GetDefaults() { // bool tooltipSetupOnly = false) {
			//Item rarity
			Item.rare = GetRarityFromTier(EnchantmentTier);

			//Width and Height
			switch (EnchantmentTier) {
				case 3:
					Item.width = 44;
					Item.height = 40;
					break;
				case 4:
					Item.width = 40;
					Item.height = 40;
					break;
				default:
					Item.width = 28 + 4 * (EnchantmentTier);
					Item.height = 28 + 4 * (EnchantmentTier);
					break;
			}

			//Value - Essence
			for (int i = 0; i <= EnchantmentTier + EnchantmentValueTierReduction; i++) {
				int quantity = Utility ? 5 : 10;
				int value = (int)EnchantmentEssence.values[i];
				Item.value += value * quantity;
			}

			//Value - Containment/SuperiorStaibalizers
			switch (EnchantmentTier) {
				case 3:
					Item.value += ContainmentItem.Values[2];
					break;
				case 4:
					Item.value += ContentSamples.ItemsByType[999].value;
					break;
				default:
					Item.value += ContainmentItem.Values[EnchantmentTier];
					break;
			}

			//Check Utility
			if (GetType().Namespace.GetFolderName() == "Utility")
				Utility = true;

			//Check Unique
			if (GetType().Namespace.GetFolderName() == "Unique")
				Unique = true;

			//Config - Individual Strength
			bool foundIndividualStrength = false;
			if (WEMod.serverConfig.individualStrengthsEnabled && WEMod.serverConfig.individualStrengths.Count > 0) {
				foreach (Pair pair in WEMod.serverConfig.individualStrengths) {
					if (pair.itemDefinition.Name == Name) {
						EnchantmentStrength = ((float)pair.Strength / 1000f);
						foundIndividualStrength = true;
					}
				}
			}

			//Config - Global Enchantment Strength Multipliers
			if (!foundIndividualStrength) {
				//Recomended
				float multiplier = RecomendedStrengthMultiplier;
				float defaultStrength = defaultEnchantmentStrengths[StrengthGroup].enchantmentTierStrength[EnchantmentTier];
				float scale = Math.Abs(ScalePercent);

				//Apply Scale Percent
				if (ScalePercent < 0f && multiplier < 1f) {
					EnchantmentStrength = 1f + (1f - scale) * (defaultStrength - 1f) + (defaultStrength - 1f) * multiplier * scale;
				}
				else {
					EnchantmentStrength = (1f - scale) * defaultStrength + defaultStrength * multiplier * scale;
				}
			}

			//Round Enchantment Strength
			EnchantmentStrength = (float)Math.Round(EnchantmentStrength, 4);

			//Default My Display Name
			if (MyDisplayName == "")
				MyDisplayName = EnchantmentTypeName;

			//Only check once
			if (finishedOneTimeSetup)
				return;

			GetMyStats();

			//Default Stat
			if (StaticStats.Count < 1 && EStats.Count < 1 && Buff.Count < 1 && Debuff.Count < 1 && OnHitBuff.Count < 1 && NewDamageType == -1) {
				AddEStat(EnchantmentTypeName, 0f, 1f, 0f, EnchantmentStrength);
			}

			finishedOneTimeSetup = true;
		}
		public override void SetDefaults() {
			Item.maxStack = 99;
			GetDefaults();
		}
		/*private void GetPercentageMult100(string s, out bool percentage, out bool multiply100, out bool plus, bool staticStat = false) {
			percentage = ShowPercentSignInTooltip != null ? (bool)ShowPercentSignInTooltip : true;
			multiply100 = MultiplyBy100InTooltip != null ? (bool)MultiplyBy100InTooltip : true;
			plus = ShowPlusSignInTooltip != null ? (bool)ShowPlusSignInTooltip : staticStat;
			switch (s) {
				//case "ArmorPenetration":
				//case "Defense":
				//case "maxMinions":
				//	percentage = false;
				//	multiply100 = false;
				//	plus = true;
				//	break;
				//case "crit":
				//	multiply100 = false;
				//	plus = true;
				//	break;
				case "Damage":
				case "NPCHitCooldown":
					plus = true;
					break;
			}//percentage, multiply100
		}*/
		/*private string CheckStatAlteredName(string name) {
			switch (name) {
				case "crit":
				case "Defense":
				case "scale":
					return MyDisplayName.AddSpaces();
				case "Damage":
					return "Damage bonus is applied after defenses (Not visible in weapon tooltip)";
				case "mana":
					return "Mana Cost";
				default:
					return name.CapitalizeFirst().AddSpaces();
			}
		}*/
		protected bool CheckStaticStatByName(string checkName = "", bool checkBoolOnly = false) {
			if (checkName == "")
				checkName = EnchantmentTypeName;

			foreach (FieldInfo field in Item.GetType().GetFields()) {
				string fieldName = field.Name;
				if (fieldName.Length <= checkName.Length) {
					if (fieldName.Length < checkName.Length) {
						switch (checkName) {
							case "CriticalStrikeChance":
								//Do nothing.  Allow only a segment of the name to be matched (such as crit to criticalStrikeChance)
								break;
							default:
								//Prevent name checking if not correct length.
								continue;
						}
					}

					string name = StringManipulation.ToFieldName(checkName.Substring(0, fieldName.Length));
					if (fieldName == name) {
						if (checkBoolOnly) {
							return field.FieldType == typeof(bool);
						}
						else {
							switch (name) {
								case "crit":
									AddStaticStat(fieldName, 0f, 1f, 0f, EnchantmentStrength * 100);
									break;
								default:
									AddStaticStat(fieldName, EnchantmentStrength);
									break;
							}
						}
						return true;
					}
				}
			}
			foreach (PropertyInfo property in Item.GetType().GetProperties()) {
				string name = property.Name;
				if (name.Length <= checkName.Length) {
					if (name == checkName.Substring(0, name.Length)) {
						if (checkBoolOnly) {
							return property.PropertyType == typeof(bool);
						}
						else {
							switch (name) {
								case "ArmorPenetration":
									AddStaticStat(name, 0f, 1f, 0f, EnchantmentStrength);
									break;
								default:
									AddStaticStat(name, EnchantmentStrength);
									break;
							}
						}
						return true;
					}
				}
			}
			Player player = new();
			foreach (FieldInfo field in player.GetType().GetFields()) {
				string fieldName = field.Name;
				if (fieldName.Length <= checkName.Length) {
					string name = StringManipulation.ToFieldName(checkName.Substring(0, fieldName.Length));
					if (fieldName == name) {
						if (checkBoolOnly) {
							return field.FieldType == typeof(bool);
						}
						else {
							switch (name) {
								case "Defense":
								case "maxMinions":
									AddStaticStat(fieldName, 0f, 1f, 0f, EnchantmentStrength);
									break;
								default:
									AddStaticStat(fieldName, EnchantmentStrength);
									break;
							}
						}
						return true;
					}
				}
			}
			foreach (PropertyInfo property in player.GetType().GetProperties()) {
				string name = property.Name;
				if (name.Length <= checkName.Length) {
					if (name == checkName.Substring(0, name.Length)) {
						if (checkBoolOnly) {
							return property.PropertyType == typeof(bool);
						}
						else {
							AddStaticStat(name, EnchantmentStrength);
						}
						return true;
					}
				}
			}
			return false;
		}
		protected bool AddStaticStat(string name, float additive = 0f, float multiplicative = 1f, float flat = 0f, float @base = 0f) {
			StaticStats.Add(new EnchantmentStaticStat(name, additive, multiplicative, flat, @base));

			return true;
		}
		protected bool AddEStat(string name, float additive = 0f, float multiplicative = 1f, float flat = 0f, float @base = 0f) {
			EStats.Add(new EStat(name, additive, multiplicative, flat, @base));

			return true;
		}
		/*protected bool CheckBuffByName(bool debuff = false, string baseName = "") {
			if (baseName == "")
				baseName = Name;

			BuffID buffID = new();
			foreach (FieldInfo field in buffID.GetType().GetFields()) {
				string fieldName = field.Name;
				if (fieldName.Length <= baseName.Length) {
					string name = baseName.Substring(0, fieldName.Length);
					if (fieldName.ToLower() == name.ToLower()) {
						if (debuff) {
							Debuff.Add((int)buffID.GetType().GetField(fieldName).GetValue(buffID), GetBuffDuration());
						}
						else {
							Buff.Add((int)buffID.GetType().GetField(fieldName).GetValue(buffID));
						}
						return true;
					}
				}
			}
			return false;
		}*/

		public IEnumerable<Tuple<string, Color>> GetEnchantmentTooltips() {
			List<Tuple<string, Color>> tooltips = new List<Tuple<string, Color>>();
			return tooltips;
        }

		public IEnumerable<Tuple<string, Color>> GetAllowedListTooltips() {
			List<Tuple<string, Color>> tooltips = new List<Tuple<string, Color>>();
			return AllowedList.Keys.Select(i => new Tuple<string, Color>($"{i} ({Math.Round(AllowedList[i] * 100, 1)}%)", Color.Gray));
		}

		public IEnumerable<Tuple<string, Color>> GetEffectsTooltips() {
			List<Tuple<string, Color>> tooltips = new List<Tuple<string, Color>>();
			foreach (var effect in Effects) {
				if (!effect.showTooltip)
					continue;

				tooltips.Add(new Tuple<string, Color>(effect.Tooltip, effect.TooltipColor));
			}

			return tooltips;
		}

		private string GetItemRestrictionTooltip(IEnumerable<EItemType> itemTypes) {
			return string.Join("\nAllowed on ", itemTypes.Select(i => $"{i} ({Math.Round(AllowedList[i]*100, 1)})"));
		}

		private IEnumerable<Tuple<string, Color>> GenerateFullTooltip() {
			List<Tuple<string, Color>> fullTooltip = new List<Tuple<string, Color>>();

			if (CustomTooltip != "") 
				fullTooltip.Add(new Tuple<string, Color>(CustomTooltip, Color.DarkGray));
			
			fullTooltip.Add(new Tuple<string, Color>("Effectiveness:", Color.Violet));
			fullTooltip.AddRange(GetAllowedListTooltips());

			fullTooltip.Add(new Tuple<string, Color>("Effects:", Color.Violet));
			fullTooltip.AddRange(GetEffectsTooltips());


			fullTooltip.Add(new Tuple<string, Color>($"Level cost: {GetCapacityCost()}", Color.Blue));

			if (Max1)
				fullTooltip.Add(new Tuple<string, Color>($"♦ Max of 1 per item", Color.DarkRed));

			if (Utility)
				fullTooltip.Add(new Tuple<string, Color>($"♪ Utility", Color.DarkGreen));

			//string shortTooltip = GenerateShortTooltip();

			//string toolTip = $"{shortTooltip}{(uniqueTooltip != "" ? "\n" : "")}{uniqueTooltip}";
			//if (NewDamageType > -1)
			//	toolTip += $"\nConverts weapon damage type to {((DamageTypeSpecificID)GetDamageClass(NewDamageType)).ToString().AddSpaces()}";

			////Estats
			//if (EStats.Count > 0) {
			//	foreach (EStat eStat in EStats) {
			//		string eStatToolTip = GetEStatToolTip(eStat, true);
			//		if (eStatToolTip != shortTooltip)
			//			toolTip += $"\n{eStatToolTip}";
			//	}
			//}

			////StaticStats
			//if (StaticStats.Count > 0) {
			//	foreach (EnchantmentStaticStat staticStat in StaticStats) {
			//		string staticStatToolTip = GetStaticStatToolTip(staticStat, true);
			//		if (staticStatToolTip != shortTooltip)
			//			toolTip += $"\n{staticStatToolTip}";
			//	}
			//}

			////OnHitBuffs
			//if (OnHitBuff.Count > 0) {
			//	int i = 0;
			//	bool first = true;
			//	foreach (int onHitBuff in OnHitBuff.Keys) {
			//		string buffName = GetBuffName(onHitBuff).AddSpaces();
			//		if (first) {
			//			toolTip += $"\nOn Hit Buffs: {buffName}";
			//			first = false;
			//		}
			//		else if (i == OnHitBuff.Count - 1) {
			//			toolTip += $" and {buffName}";
			//		}
			//		else {
			//			toolTip += $", {buffName}";
			//		}
			//		i++;
			//	}
			//}

			////Debuffs
			//if (Debuff.Count > 0) {
			//	int i = 0;
			//	bool first = true;
			//	foreach (int debuff in Debuff.Keys) {
			//		string buffName = GetBuffName(debuff).AddSpaces();
			//		if (first) {
			//			toolTip += $"\nOn Hit Debuffs: {buffName}";
			//			first = false;
			//		}
			//		else if (i == Debuff.Count - 1) {
			//			toolTip += $" and {buffName}";
			//		}
			//		else {
			//			toolTip += $", {buffName}";
			//		}
			//		i++;
			//	}
			//}

			//Level Cost

			//Unique, DamageClassSpecific, RestrictedClass, ArmorSlotSpecific
			//if (DamageClassSpecific > 0 || Unique || RestrictedClass > -1 || ArmorSlotSpecific > -1) {
			//	string limmitationToolTip = "";
			//	if (Unique && !Max1 && DamageClassSpecific == 0 && ArmorSlotSpecific == -1 && RestrictedClass == -1  && Utility == false) {
			//		//Unique (Specific Item)
			//		limmitationToolTip += $"\n   *{StringManipulation.AddSpaces(EnchantmentTypeName)} Only*";
			//	}
			//	else if (DamageClassSpecific > 0) {
			//		//DamageClassSpecific
			//		limmitationToolTip += $"\n   *{((DamageTypeSpecificID)GetDamageClass(DamageClassSpecific)).ToString().AddSpaces()} Only*";
			//	}
			//	else if (ArmorSlotSpecific > -1) {
			//		//ArmorSlotSpecific
			//		limmitationToolTip += $"\n   *{(ArmorSlotSpecificID)ArmorSlotSpecific} armor slot Only*";
			//	}

			//	//RestrictedClass
			//	if (RestrictedClass > -1) {
			//		limmitationToolTip += $"\n   *Not allowed on {((DamageTypeSpecificID)GetDamageClass(RestrictedClass)).ToString().AddSpaces()} weapons*";
			//	}

			//	//Unique
			//	if(Unique)
			//		limmitationToolTip += "\n   *Unique* (Limited to 1 Unique Enchantment)";


			//	toolTip += limmitationToolTip;
			//}

			////AllowedList
			//if (AllowedList.Count > 0) {
			//	int i = 0;
			//	bool first = true;
			//	foreach (string key in AllowedList.Keys) {
			//		if (first) {
			//			toolTip += $"\n   *Allowed on {key}: {AllowedList[key] * 100}%{(AllowedList.Count == 1 ? " Only*" : "")}";
			//			first = false;
			//		}
			//		else if (i == AllowedList.Count - 1) {
			//			toolTip += $" and {key}: {AllowedList[key] * 100}%{(AllowedList.Count < 3 ? " Only*" : "")}";
			//		}
			//		else {
			//			toolTip += $", {key}: {AllowedList[key] * 100}%";
			//		}
			//		i++;
			//	}
			//}

			return fullTooltip;
		}
		/*private string GetEStatToolTip(EStat eStat, bool forFullToolTip = false, bool firstToolTip = false, EItemType allowedListKey = EItemType.None) {
			string toolTip = "";

			//percentage, multiply100, plus
			GetPercentageMult100(eStat.StatName, out bool percentage, out bool multiply100, out bool plus);

			//Stat Name
			string statName;
			string eStatFirst2 = eStat.StatName.Substring(0, 2);
			bool invert = forFullToolTip && !firstToolTip && eStatFirst2 == "I_";
			if (invert) {
				//Remove "I_" from the start of the Stat Name
				statName = eStat.StatName.Substring(2);
			}
			else {
				statName = eStat.StatName;
			}

			//Flat value of 13.13f will prevent any number from being displayed in the tooltip.
			if (eStat.Flat != 13.13f) {
				float allowedListMultiplier = allowedListKey != EItemType.None ? AllowedList[allowedListKey] : 1f;
				float invertMultiplier = invert ? -1f : 1f;
				float additive = eStat.Additive * invertMultiplier * allowedListMultiplier;
				float multiplicative = invert ? 1f / (eStat.Multiplicative * allowedListMultiplier) : eStat.Multiplicative * allowedListMultiplier;
				float flat = eStat.Flat * invertMultiplier * allowedListMultiplier;
				float @base = eStat.Base * invertMultiplier * allowedListMultiplier;
				EStat enchantmentStat = new EStat(statName, additive, multiplicative, flat, @base);

				if (enchantmentStat.Additive != 0f || enchantmentStat.Multiplicative != 1f) {
					if (enchantmentStat.Additive != 0f) {
						toolTip += (plus ? (enchantmentStat.Additive > 0f ? "+" : "") : "") + 
							$"{enchantmentStat.Additive * (multiply100 ? 100 : 1)}{(percentage ? "%" : "")} ";
					}
					else if (enchantmentStat.Multiplicative != 1f) {
						toolTip += $"{enchantmentStat.Multiplicative}x ";
					}
				}
				else {
					float num = enchantmentStat.Base != 0f ? enchantmentStat.Base : enchantmentStat.Flat;
					toolTip += (plus ? (num > 0f ? "+" : "") : "") + $"{num * (multiply100 ? 100 : 1)}{(percentage ? "%" : "")} ";
				}
			}

			toolTip += $"{(forFullToolTip ? CheckStatAlteredName(firstToolTip ? MyDisplayName : statName) : MyDisplayName)}";

			return toolTip;
		}
		private string GetStaticStatToolTip(EnchantmentStaticStat staticStat, bool forFullToolTip = false, bool firstToolTip = false, EItemType allowedListKey = EItemType.None) {
			string toolTip = "";
			string statName;
			bool invert = staticStat.Name.Substring(0, 2) == "I_";
			bool prevent = staticStat.Name.Substring(0, 2) == "P_";
			if (invert || prevent) {
				statName = staticStat.Name.Substring(2);
			}
			else {
				statName = staticStat.Name;
			}

			bool statIsBool = CheckStaticStatByName(statName, true);
			if (statIsBool) {
				statName = statName.CapitalizeFirst().AddSpaces();
				if (prevent)
					statName = $"Prevent {statName}";

				toolTip = statName;
			}
			else {
				float allowedListMultiplier = allowedListKey != EItemType.None ? AllowedList[allowedListKey] : 1f;
				float invertMultiplier = invert ? -1f : 1f;
				float additive = staticStat.Additive * invertMultiplier * allowedListMultiplier;
				float multiplicative = invert ? 1f / (staticStat.Multiplicative * allowedListMultiplier) : staticStat.Multiplicative * allowedListMultiplier;
				float flat = staticStat.Flat * invertMultiplier * allowedListMultiplier;
				float @base = staticStat.Base * invertMultiplier * allowedListMultiplier;
				EnchantmentStaticStat enchantmentStaticStat = new EnchantmentStaticStat(statName, additive, multiplicative, flat, @base);

				GetPercentageMult100(enchantmentStaticStat.Name, out bool percentage, out bool multiply100, out bool plus, true);

				if (enchantmentStaticStat.Additive != 0f || enchantmentStaticStat.Multiplicative != 1f) {
					if (enchantmentStaticStat.Additive != 0f) {
						toolTip += (plus ? (enchantmentStaticStat.Additive > 0f ? "+" : "") : "") + $"{enchantmentStaticStat.Additive * (multiply100 ? 100 : 1)}{(percentage ? "%" : "")} ";
					}
					else if (enchantmentStaticStat.Multiplicative != 1f) {
						toolTip += $"{enchantmentStaticStat.Multiplicative}x ";
					}
				}
				else {
					float num = enchantmentStaticStat.Base != 0f ? enchantmentStaticStat.Base : enchantmentStaticStat.Flat;
					toolTip += (plus ? (num > 0f ? "+" : "") : "") + $"{num * (multiply100 ? 100 : 1)}{(percentage ? "%" : "")} ";// " + (enchantmentStaticStat.Base != 0f ? "base" : "");
				}
				toolTip += $"{(forFullToolTip ? CheckStatAlteredName(firstToolTip ? MyDisplayName : enchantmentStaticStat.Name) : MyDisplayName)}";
			}

			return toolTip;
		}*/
		public static int GetDamageClass(int damageType) {
			switch ((DamageTypeSpecificID)damageType) {
				case DamageTypeSpecificID.Melee:
				case DamageTypeSpecificID.MeleeNoSpeed:
					return (int)DamageTypeSpecificID.Melee;
				case DamageTypeSpecificID.Ranged:
					return (int)DamageTypeSpecificID.Ranged;
				case DamageTypeSpecificID.Magic:
					return (int)DamageTypeSpecificID.Magic;
				case DamageTypeSpecificID.Summon:
				case DamageTypeSpecificID.MagicSummonHybrid:
				case DamageTypeSpecificID.SummonMeleeSpeed:
					return (int)DamageTypeSpecificID.Summon;
				case DamageTypeSpecificID.Throwing:
					return (int)DamageTypeSpecificID.Throwing;
				default:
					return (int)DamageTypeSpecificID.Generic;
			}
		}
		private int GetBuffDuration() {
			return defaultBuffDuration * (EnchantmentTier + 1);
		}
		public static int GetEnchantmentTier(string name) {
			for (int i = 0; i < tierNames.Length; i++) {
				if (tierNames[i] == name.Substring(name.IndexOf("Enchantment") + 11)) {
					return i;
				}
			}//Get EnchantmentSize

			return -1;
		}
		public override void AddRecipes() {
			for (int i = EnchantmentTier; i < tierNames.Length; i++) {
				//Lowest Craftable Tier
				if (EnchantmentTier < LowestCraftableTier)
					continue;

				Recipe recipe;

				for (int j = LowestCraftableTier; j <= EnchantmentTier; j++) {
					recipe = CreateRecipe();

					//Essence
					for (int k = j; k <= EnchantmentTier; k++) {
						int essenceNumber = Utility ? 5 : 10;
						recipe.AddIngredient(Mod, "EnchantmentEssence" + tierNames[k], essenceNumber);
					}

					//Enchantment
					if (j > 0) {
						recipe.AddIngredient(Mod, EnchantmentTypeName + "Enchantment" + tierNames[j - 1], 1);
					}
						
					//Containment
					if (EnchantmentTier < 3) {
						recipe.AddIngredient(Mod, ContainmentItem.sizes[EnchantmentTier] + "Containment", 1);
					}
					else if (j < 3) {
						recipe.AddIngredient(Mod, ContainmentItem.sizes[2] + "Containment", 1);
					}

					//Gems
					if (EnchantmentTier == 3) {
						recipe.AddRecipeGroup("WeaponEnchantments:CommonGems", 2);
					}
					if (EnchantmentTier == 4) {
						recipe.AddRecipeGroup("WeaponEnchantments:RareGems");
					}

					//Enchanting Table
					recipe.AddTile(Mod, EnchantingTableItem.enchantingTableNames[i] + "EnchantingTable");

					recipe.Register();
				}
			}
		}
		public int GetCapacityCost() {
			if (CapacityCostMultiplier != -13.13f) {
				//multiplier is being manually set by this enchantment
				return (int)Math.Round((1 + EnchantmentTier) * CapacityCostMultiplier);
			}

			int multiplier = 2;

			if (Utility)
				multiplier = 1;

			if (Unique || Max1)
				multiplier = 3;

			return (1 + EnchantmentTier) * multiplier;
		}
	}
}