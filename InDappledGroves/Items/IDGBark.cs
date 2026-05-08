using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.Client;
using Vintagestory.GameContent;

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

        /// <summary>Called when the player right clicks while holding this block/item in his hands</summary>
        /// <param name="slot">Players activehotbar slot</param>
        /// <param name="byEntity"></param>
        /// <param name="blockSel"></param>
        /// <param name="entitySel"></param>
        /// <param name="firstEvent">
        /// True when the player pressed the right mouse button on this block. Every subsequent call, while the player holds right mouse down will be false, it gets called every second while right mouse is down
        /// </param>
        /// <param name="handling">Whether or not to do any subsequent actions. If not set or set to NotHandled, the action will not called on the server.</param>
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            BlockPos pos = blockSel?.Position;

            if (pos != null && api.World.BlockAccessor.GetBlockEntity(pos) is BlockEntityGroundStorage bebgs && byEntity is EntityPlayer player)
            {
                if (bebgs.GetContentStacks()[0]?.Collectible.Code == slot.Itemstack.Collectible.Code && player.Player.InventoryManager.OffhandHotbarSlot?.Itemstack?.Collectible is ItemHammer)
                    handling = EnumHandHandling.PreventDefault;
                //return;
            }
            else
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            }
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            BlockPos pos = blockSel?.Position;


            if (pos != null && api.World.BlockAccessor.GetBlockEntity(pos) is BlockEntityGroundStorage bebgs && byEntity is EntityPlayer player)
            {

                if (bebgs.Inventory[0].StackSize >= 4)
                {
                    if (api.Side.IsServer())
                    {
                        ItemStack stack = new ItemStack(api.World.BlockAccessor.GetBlock(
                                new AssetLocation("indappledgroves:barkbundle-" +
                                bebgs.Inventory[0].Itemstack.Collectible.Code.SecondCodePart() +
                                "-dry")));
                        ItemStack removedStack = bebgs.Inventory.FirstNonEmptySlot.TakeOut(4);
                        if (stack != null && player.Player.InventoryManager.TryGiveItemstack(stack))
                        {
                            slot.TakeOut(1);
                            if (stack.StackSize > 0)
                            {
                                api.World.SpawnItemEntity(stack, bebgs.Pos);
                            }
                            bebgs.updateMeshes();
                            bebgs.MarkDirty(true);
                            api.World.Logger.Audit("{0} Took {1}x{2} from Ground storage at {3}.", player.Player.PlayerName, 4, removedStack.Collectible.Code, bebgs.Pos);
                            if (bebgs.TotalStackSize == 0)
                            {
                                api.World.BlockAccessor.SetBlock(0, bebgs.Pos);
                            }

                            return false;
                        }
                    }
                }
                else if (bebgs.Inventory[0].MaxSlotStackSize <= 4)
                {
                    if(api is ICoreClientAPI capi)
                    {
                        capi.TriggerIngameError(this, "toofewbarkinstack", "There's not enough bark in the stack to create a bundle.");
                    }
                }
                
            }
            return base.OnHeldInteractStep(secondsUsed, slot, byEntity, blockSel, entitySel);
        }
    }
}
