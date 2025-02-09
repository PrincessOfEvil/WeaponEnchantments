﻿using System.Collections.Generic;
using WeaponEnchantments.Effects;

namespace WeaponEnchantments.Items.Enchantments
{
	public abstract class DefenseEnchantment : Enchantment
	{
		public override int StrengthGroup => 3;
		public override string MyDisplayName => "Defence";
		public override int LowestCraftableTier => 0;
		public override Dictionary<EItemType, float> AllowedList => new Dictionary<EItemType, float>() {
			{ EItemType.Weapon, 0.5f },
			{ EItemType.Armor, 1f },
			{ EItemType.Accessory, 1f }
		};
		public override void GetMyStats() {
			Effects = new EnchantmentEffect[] {
				new DefenseEffect(@base: EnchantmentStrength),
			};
		}

		public override string Artist => "Zorutan";
		public override string Designer => "andro951";
	}
	public class DefenseEnchantmentBasic : DefenseEnchantment { }
	public class DefenseEnchantmentCommon : DefenseEnchantment { }
	public class DefenseEnchantmentRare : DefenseEnchantment { }
	public class DefenseEnchantmentSuperRare : DefenseEnchantment { }
	public class DefenseEnchantmentUltraRare : DefenseEnchantment { }

}