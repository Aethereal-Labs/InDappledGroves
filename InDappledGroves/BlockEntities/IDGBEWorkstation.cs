using InDappledGroves.CollectibleBehaviors;
using InDappledGroves.Util.Handlers;
using System;
using System.Numerics;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;
using static InDappledGroves.Util.RecipeTools.IDGRecipeNames;
using static OpenTK.Graphics.OpenGL.GL;

namespace InDappledGroves.BlockEntities
{
    public class IDGBEWorkstation : BlockEntityDisplay
    {
		public override InventoryBase Inventory { get; }

        protected InventoryGeneric inventory;

        public override string InventoryClassName => Block==null?"workstation":Block.Attributes["inventoryclass"].AsString();
        public override string AttributeTransformCode => Block.Attributes["attributetransformcode"].AsString();

        public string workstationtype => Block.Attributes["workstationproperties"]["workstationtype"].ToString();

        public string processmodifier => Block.Attributes["workstationproperties"]["processmodifiername"].ToString();

        public bool recipecomplete { get; set; } = false;

        public RecipeHandler recipeHandler { get; set; }

        public float currentMiningDamage { get; set; }
        public ItemSlot InputSlot { get { return Inventory[Block.Attributes["workstationproperties"]["slottypes"]["inputslot"].AsInt()];} }

        public ItemSlot ProcessModifierSlot { get { return Block.Attributes["workstationproperties"]["workstationtype"].ToString() == "complex" ? Inventory[Block.Attributes["workstationproperties"]["slottypes"]["processmodifier0"].AsInt()] : null; } }



        public IDGBEWorkstation()
		{
            //Must initialize inventory in derived classes
            Inventory = new InventoryDisplayed(this, 2, InventoryClassName + "-slot", null, null);

        }

		public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            if (recipeHandler == null)
            {
                recipeHandler = new RecipeHandler(api, this);
            }
            this.capi = (api as ICoreClientAPI);
        }

        public virtual bool OnInteract(IPlayer byPlayer)
        {
            
            ItemSlot activeHotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
            bool result = false;
            //If The Players Hand Is Empty
            if (workstationtype == "basic")
            {
                return OnBasicInteract(byPlayer);
            }
            else if (workstationtype == "complex")
            {
                return OnComplexInteract(byPlayer);
            }

            if (result)
            {
                updateMeshes();
                MarkDirty(true);
            }
			return false;
		}


        public virtual bool OnBasicInteract(IPlayer byPlayer)
        {
            ItemSlot activeHotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

                if (activeHotbarSlot.Empty)
                {
                    return this.TryTake(byPlayer, InputSlot);
                }
                else if (recipeHandler.GetMatchingIngredient(Api.World, activeHotbarSlot, workstationtype, Block.Code.FirstCodePart()))
                {
                    return this.TryPut(byPlayer, activeHotbarSlot, InputSlot);
                }
            this.updateMeshes();
            this.MarkDirty(true);
            return false;
        }

        public virtual bool OnComplexInteract(IPlayer byPlayer)
        {
            ItemSlot activeHotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (activeHotbarSlot.Empty)
            {
                if (!InputSlot.Empty)
                {
                    return this.TryTake(byPlayer, InputSlot);
                }
                else
                {
                    return this.TryTake(byPlayer, ProcessModifierSlot);
                }
            }
            else if (recipeHandler.GetMatchingIngredient(Api.World, activeHotbarSlot, workstationtype, Block.Code.FirstCodePart()))
            {
                return this.TryPut(byPlayer, activeHotbarSlot, InputSlot);
            }
            else if (recipeHandler.GetMatchingProcessModifier(Api.World, activeHotbarSlot, workstationtype))
            {
                return this.TryPut(byPlayer, activeHotbarSlot, ProcessModifierSlot);
            }
            this.updateMeshes();
            this.MarkDirty(true);
            return false;
        }

        public virtual bool TryPut(IPlayer byPlayer, ItemSlot slot, ItemSlot targetSlot)
        {
            if (targetSlot.Empty)
            {
                Block block = slot.Itemstack.Block;
                int num3 = slot.TryPutInto(this.Api.World, targetSlot, 1);
                if (num3 > 0)
                {

                    AssetLocation assetLocation;
                    if (block == null)
                    {
                        assetLocation = null;
                    }
                    else
                    {
                        BlockSounds sounds = block.Sounds;
                        assetLocation = (sounds?.Place);
                    }
                    AssetLocation assetLocation2 = assetLocation;
                    this.Api.World.PlaySoundAt(assetLocation2 ?? new AssetLocation("sounds/player/build"), byPlayer.Entity, byPlayer, true, 16f, 1f);
                };
                updateMeshes();
                MarkDirty(true);
            }
            return false;
        }

        public virtual bool TryTake(IPlayer byPlayer,ItemSlot targetSlot)
		{
				if (!targetSlot.Empty)
				{
					ItemStack itemStack = targetSlot.TakeOut(1);
					if (byPlayer.InventoryManager.TryGiveItemstack(itemStack, false))
					{
						Block block = itemStack.Block;
						AssetLocation assetLocation;
						if (block == null)
						{
							assetLocation = null;
						}
						else
						{
							BlockSounds sounds = block.Sounds;
							assetLocation = (sounds?.Place);
						}
						AssetLocation assetLocation2 = assetLocation;
						this.Api.World.PlaySoundAt(assetLocation2 ?? new AssetLocation("sounds/player/build"), byPlayer.Entity, byPlayer, true, 16f, 1f);
                        recipeHandler.clearRecipe();
                }
					if (itemStack.StackSize > 0)
					{
						this.Api.World.SpawnItemEntity(itemStack, this.Pos.ToVec3d().Add(0.5, 0.5, 0.5), null);
					}
                    base.MarkDirty(true, null);
					return false;
				}

			return false;
		}

        internal bool handleRecipe(CollectibleObject heldCollectible, float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
            
            recipecomplete = recipeHandler.processRecipe(heldCollectible, slot, byPlayer, blockSel.Position, this, secondsUsed);
            WeatherSystemBase modSystem = this.Api.ModLoader.GetModSystem<WeatherSystemBase>(true);
            double windspeed = (modSystem != null) ? modSystem.WeatherDataSlowAccess.GetWindSpeed(byPlayer.Entity.SidedPos.XYZ) : 0.0;

            
            if (recipecomplete) recipeHandler.clearRecipe();
            updateMeshes();
            base.MarkDirty(true, null);

            return !recipecomplete;

        }
       

        private string GetWorkStationType()
        {
            JsonObject attributes = Block.Attributes["workstationproperties"];
            if (attributes.Exists && attributes["workstationtype"].Exists)
            {
                return attributes["workstationtype"].ToString();
            } else
            {
                if (Api.Side.IsClient())
                {
                    capi.Logger.Debug(Lang.GetMatching("WorkstationTypeNotDesignated", Block.Class));
                }
                return null;
            }
        }

        protected override float[][] genTransformationMatrices()
        {
            float[][] tfMatrices = new float[Inventory.Count][];
            for (int index = 0; index < Inventory.Count; index++)
            {

                ItemSlot itemSlot = this.Inventory[index];
                JsonObject jsonObject;
               if (itemSlot == null)
                {
                    jsonObject = null;
                }
                else
                {
                    ItemStack itemstack = itemSlot.Itemstack;
                    if (itemstack == null)
                    {
                        jsonObject = null;
                    }
                    else
                    {
                        if (index == Block.Attributes["workstationproperties"]["slottypes"]["inputslot"].AsInt())
                        {
                            string blocktype = "specialadjust" + Block.Code.FirstCodePart().ToString();

                            Matrixf matrix = new Matrixf();
                            if (itemstack.Block != null && itemstack.Block.Attributes[blocktype].Exists)
                            {
                                JsonObject specialadjust = itemstack.Collectible.Attributes[blocktype];
                                switch (Block.Variant["side"])
                                {
                                    case "east":
                                        matrix.Translate(specialadjust["east"].AsFloat(), 0, 0);
                                        matrix.Translate(specialadjust["eastrotateX"].AsFloat(), specialadjust["eastrotateY"].AsFloat(), specialadjust["eastrotateZ"].AsFloat());
                                        break;
                                    case "west":
                                        matrix.Translate(specialadjust["west"].AsFloat(), 0, 0);
                                        matrix.Translate(specialadjust["westrotateX"].AsFloat(), specialadjust["westrotateY"].AsFloat(), specialadjust["westrotateZ"].AsFloat());
                                        break;
                                    case "north":
                                        matrix.Translate(0, 0, specialadjust["north"].AsFloat());
                                        matrix.Translate(specialadjust["northrotateX"].AsFloat(), specialadjust["northrotateY"].AsFloat(), specialadjust["northrotateZ"].AsFloat());
                                        break;
                                    case "south":
                                        matrix.Translate(0, 0, specialadjust["south"].AsFloat());
                                        matrix.Translate(specialadjust["southrotateX"].AsFloat(), specialadjust["southrotateY"].AsFloat(), specialadjust["southrotateZ"].AsFloat());
                                        break;
                                }
                            }
                            tfMatrices[index] = matrix.Translate(0, 0.0, 0).Translate(0.5, 0.5, 0.5)
                                .RotateYDeg(this.Block.Shape.rotateY)
                                .Translate(-0.5, -0.5, -0.5).Values;
                        } else {
                            tfMatrices[index] = new Matrixf().Translate(0.5, 0.5, 0.5).RotateYDeg(this.Block.Shape.rotateY).Translate(-0.5, -0.5, -0.5).Values;
                        }
                    }
                }
            }
            return tfMatrices;
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {

            if (recipeHandler != null)
            {
                tree.SetString("currenttoolmode", recipeHandler.curtMode);
                tree.SetFloat("lastsecondsused", recipeHandler.lastSecondsUsed);
                tree.SetFloat("recipeprogress", recipeHandler.recipeProgress);
                tree.SetFloat("playnextsound", recipeHandler.playNextSound);
                tree.SetFloat("currentminingdamage", recipeHandler.currentMiningDamage);
                tree.SetFloat("curdmgfromminingspeed", recipeHandler.curDmgFromMiningSpeed);
                tree.SetFloat("totalsecondsused", recipeHandler.totalSecondsUsed);
                tree.SetBool("recipecomplete", recipecomplete);
            }
            base.ToTreeAttributes(tree);
            updateMeshes();
            MarkDirty(true);

        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            if (recipeHandler == null) recipeHandler = new RecipeHandler(Api, this);
            if (recipeHandler != null)
            {
                recipeHandler.curtMode = tree.GetString("currenttoolmode");
                recipeHandler.lastSecondsUsed = tree.GetFloat("lastsecondsused");
                recipeHandler.recipeProgress = tree.GetFloat("recipeprogress");
                recipeHandler.currentMiningDamage = tree.GetFloat("currentminingdamage");
                recipeHandler.curDmgFromMiningSpeed = tree.GetFloat("curdmgfromminingspeed");
                recipeHandler.totalSecondsUsed = tree.GetFloat("totalsecondsused");
                recipeHandler.playNextSound = tree.GetFloat("playnextsound");
                recipecomplete = tree.GetBool("recipecomplete");
                recipeHandler.api = Api;
            }
            //TODO: Check and see if this fixes a given problem.
            updateMeshes();
            MarkDirty(true);
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            var primary = Block.Variant["primary"];
            var secondary = Block.Variant["secondary"];
            var materials = (Lang.Get("material-" + $"{primary}") + (secondary != null ? " and " + Lang.Get("material-" + $"{secondary}") : ""));
            ItemStack stack = forPlayer.InventoryManager.ActiveHotbarSlot.Itemstack;
            string curToolMode = stack?.Collectible.GetBehavior<BehaviorIDGTool>()?.GetToolModeName(stack).ToString();
            dsc.AppendLine(Lang.GetMatching("indappledgroves:workstationholding") + ": " + (InputSlot.Empty ? Lang.GetMatching("indappledgroves:Empty") : InputSlot.Itemstack.Collectible.GetHeldItemName(InputSlot.Itemstack)));
            
            if (workstationtype == "complex" && !ProcessModifierSlot.Empty)
            {
                dsc.AppendLine(Lang.GetMatching(this.processmodifier!=null?processmodifier:"indappledgroves:defaultprocessmodifier") + ProcessModifierSlot.Itemstack.GetName());
                dsc.AppendLine(Lang.GetMatching("indappledgroves:remainingdurability") + ": " + ProcessModifierSlot.Itemstack.Attributes["durability"]);
            }
            WorkstationRecipe retrRecipe;
            

            string curTMode = forPlayer.InventoryManager.ActiveHotbarSlot.Itemstack?.Collectible.GetBehavior<BehaviorIDGTool>()?.GetToolModeName(forPlayer.InventoryManager?.ActiveHotbarSlot.Itemstack);
            recipeHandler.GetMatchingRecipes(forPlayer.Entity.Api.World, InputSlot, curTMode, Block.Attributes["inventoryclass"].ToString(), Block.Attributes["workstationproperties"]["workstationtype"].ToString(), out retrRecipe);
            
            if (retrRecipe != null)
            {
                ItemStack resolvedItemStack = retrRecipe.Output.ResolvedItemstack;
                ItemStack resolvedReturnStack = retrRecipe.ReturnStack.ResolvedItemstack ?? null;
                dsc.AppendLine(Lang.GetMatching("indappledgroves:recipeoutputstack") + " " + resolvedItemStack.StackSize + " " + resolvedItemStack.Collectible.GetHeldItemName(resolvedItemStack));
                if(resolvedReturnStack.Id != 0) 
                { dsc.AppendLine("& " + resolvedReturnStack.StackSize + " " + resolvedReturnStack.Collectible.GetHeldItemName(resolvedReturnStack)); }
                if (recipeHandler.recipe != null)
                {
                    dsc.AppendLine(Lang.GetMatching("indappledgroves:recipeprogress") + " " + Math.Round((recipeHandler.recipeProgress) * 100) + "%");
                }
            }
            if (ClientSettings.ExtendedDebugInfo) { 
                
                    
                dsc.AppendLine(string.Format($"{materials}"));
                dsc.AppendLine (Lang.GetMatching("indappledgroves:attributetransformcode") + ": " + AttributeTransformCode);
                dsc.AppendLine(Lang.GetMatching("indappledgroves:inventoryclassname") + ": " + InventoryClassName);
                dsc.AppendLine(Lang.GetMatching("indappledgroves:currenttoolmode") + ": " + curToolMode);
                
                if (recipeHandler.recipe != null)
                {
                    dsc.AppendLine(Lang.GetMatching("indappledgroves:recipetoolmode") + ": " + recipeHandler.recipe.ToolMode.ToString());
                    dsc.AppendLine("indappledgroves:recipeworkstation" + ": " + recipeHandler.recipe.RequiredWorkstation.ToString());
                }
            }
           
        }

	}


}

