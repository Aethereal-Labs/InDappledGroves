using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace InDappledGroves.Items
{
    class IDGBark : Item
    {
        public override string GetHeldItemName(ItemStack stack) => GetName();

        public string GetName()
        {

            var barktype = Lang.Get($"material-{Variant["bark"]}");
            var barkstate = Lang.Get($"{Variant["state"]}");
            barktype = $"{barktype[0].ToString().ToUpper()}{barktype.Substring(1)}";
            barkstate = $"{barkstate[0].ToString().ToUpper()}{barkstate.Substring(1)}";
            return $"{barkstate} {Lang.Get("indappledgroves:item-bark")} ({barktype})";
        }
    }
}
