using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.DataStructures;

namespace TerrariaWiringVisualCopy.Items
{
    public class FoundWiring : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 40;
            Item.height = 40;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTime = 15;
            Item.useAnimation = 15;
            Item.useTurn = true;
            Item.autoReuse = false;
            Item.rare = ItemRarityID.Master;
            Item.value = Item.buyPrice(platinum: 100);
            Item.mana = 0;
        }

        public override bool? UseItem(Player player)
        {
            VisualizerWorld.AddAllLogicGate(new Point16(Player.tileTargetX, Player.tileTargetY));
            return true;
        }
    }
}
