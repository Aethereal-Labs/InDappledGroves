using InDappledGroves.BlockEntities;
using InDappledGroves.CollectibleBehaviors;
using System.Globalization;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using static InDappledGroves.Util.RecipeTools.IDGRecipeNames;

namespace InDappledGroves.Blocks
{
    class IDGWorkstation : Block
    {

        /*TODO: Implement InUseCheck.  If UserUID is not "workstationfree", then 
         * UserUID gets set on BlockEntity when user reaches OnHeldInteractStep method
         * if UserUID == "workstationfree". After that step the UserUID is checked against the
         * interacting characters UID. If the UIDs match, the process is allowed to continue. If not,
         * the interacting users attempt returns false. (Will this affect the original user? Testing will tell)
         * Upon HeldInteractCancel or HeldInteractStop, the UserUID get cleared back to "workstationfree."
         * This should nerf the "Power of Friendship" as Stew calls it.
        */

        

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is IDGBEWorkstation beworkstation)
            {
                return true;
            }
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            bool result = false;
            if (blockSel != null && world.BlockAccessor.GetBlockEntity(blockSel.Position) is IDGBEWorkstation beworkstation)
            {
                    CollectibleObject heldCollectible = byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack?.Collectible;

                    if ((heldCollectible == null || !heldCollectible.HasBehavior<BehaviorIDGTool>()))
                    {
                        result = beworkstation.OnInteract(byPlayer);
                    }
                    else if (!beworkstation.InputSlot.Empty && heldCollectible != null && heldCollectible.HasBehavior<BehaviorIDGTool>())
                    {
                        result = beworkstation.handleRecipe(heldCollectible, secondsUsed, world, byPlayer, blockSel);
                        //byPlayer.Entity.StartAnimation(beworkstation.recipeHandler.recipe?.Animation);
                    }
            }
            return result;
        }

        public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            bool result = false;
            if (blockSel != null && world.BlockAccessor.GetBlockEntity(blockSel.Position) is IDGBEWorkstation beworkstation)
            {
                beworkstation.recipeHandler.playNextSound = 0.5f;
                if (beworkstation.recipeHandler.recipe != null)
                {
                    byPlayer.Entity.StopAnimation(beworkstation.recipeHandler.recipe.Animation);
                }
                if (beworkstation.recipecomplete) beworkstation.recipeHandler.clearRecipe();

                beworkstation.MarkDirty(true);
                beworkstation.updateMeshes();
            }
        }

        public override bool OnBlockInteractCancel(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, EnumItemUseCancelReason cancelReason)
        {
            if (blockSel != null && world.BlockAccessor.GetBlockEntity(blockSel.Position) is IDGBEWorkstation beworkstation)
            {
                if (beworkstation.recipeHandler.recipe != null)
                {
                    byPlayer.Entity.StopAnimation(beworkstation.recipeHandler.recipe.Animation);
                }
            }
            return base.OnBlockInteractCancel(secondsUsed, world, byPlayer, blockSel, cancelReason);
        }

        public override void OnEntityCollide(IWorldAccessor world, Entity entity, BlockPos pos, BlockFacing facing, Vec3d collideSpeed, bool isImpact)
        {
            
            EntityItem inWorldItem = entity as EntityItem;
            IDGBEWorkstation ws;
            if (world.Rand.NextDouble() < 0.9)
            {
                return;
            }
            if (inWorldItem != null && world.Side == EnumAppSide.Server)
            {
                ws = api.World.BlockAccessor.GetBlockEntity(pos) as IDGBEWorkstation;
                WorkstationRecipe recipe;
                if (ws != null)
                {
                    ws.recipeHandler.GetMatchingRecipes(entity.Api.World, inWorldItem.Slot, "any", this.Attributes["inventoryclass"].ToString(), ws.workstationtype, out recipe);
                } else
                {
                    return;
                }   
                if (recipe != null && inWorldItem.Alive)
                {
                        ItemSlot wslot = ws.Inventory.GetAutoPushIntoSlot(facing, inWorldItem.Slot);
                        if (wslot != null)
                        {
                            inWorldItem.Slot.TryPutInto(this.api.World, wslot, 1);
                            if (inWorldItem.Slot.StackSize <= 0)
                            {
                                inWorldItem.Itemstack = null;
                                inWorldItem.Alive = false;
                            }
                        }
                    ws.updateMeshes();
                    ws.MarkDirty(true);
                }
            }
        }
        public override string GetHeldItemName(ItemStack stack)
        {
            base.GetHeldItemName(stack);
            string primary = Variant["primary"];
            string secondary = Variant["secondary"];
            string materials = Lang.Get("material-" + $"{primary}") + (secondary != null ? " and " + Lang.Get("material-" + $"{secondary}") : "");
            string blockid = Lang.HasTranslation("indappledgroves:block-" + this.FirstCodePart()) ? "indappledgroves:block-" + this.FirstCodePart() : this.Code.Domain + ":block-" + this.FirstCodePart();
            return string.Format($"{CultureInfo.CurrentCulture.TextInfo.ToTitleCase(materials.ToLower())} " + Lang.GetMatching(blockid));
            
        }

        private float playNextSound;
        private float resistance;
        private float lastSecondsUsed;
        private float curDmgFromMiningSpeed;
    }

}
