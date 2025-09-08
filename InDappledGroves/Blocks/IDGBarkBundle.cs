using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace InDappledGroves.Blocks
{
    class IDGBarkBundle : Block
    {
        public override void OnBlockPlaced(IWorldAccessor world, BlockPos blockPos, ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(world, blockPos, byItemStack);
        }
        
        public override string GetHeldItemName(ItemStack stack) => GetName();
        public override string GetPlacedBlockName(IWorldAccessor world, BlockPos pos) => GetName();

        public string GetName()
        {
            var barktype = Lang.Get("material-" + $"{Variant["bark"]}");
            var barkstate = Lang.Get($"{Variant["stage"]}");
            barktype = $"{barktype[0].ToString().ToUpper()}{barktype.Substring(1)}";
            barkstate = $"{barkstate[0].ToString().ToUpper()}{barkstate.Substring(1)}";
            return string.Format($"{barkstate} {Lang.Get("indappledgroves:block-barkbundle")} ({barktype})");
        }
    }
}
