using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace InDappledGroves.Util.Properties
{
    public class ProcessableProperties
    {
        public string Name = "unnamed";
        public string FromModID = "modidnotprovided";
        public float ProcessTime = 4;
        public BlockDropItemStack[] ProcessedStacks = null;
        public AssetLocation ProcessingSound = "";
        public string ProcessingAnimationCode = "";
        public float ProcessingAnimationTargetFrame = 0f;
        public JsonItemStack RemainingItem = null;
        public int ConsumedGroundStorageStackQty = 1;
        public bool ExactGroundStorageStackQtyRequired = false;
        public bool ExactOrGreaterThanStorageQtyRequired = false;
        public bool PlaceRemainingItemAsBlock = false; //Replaces ground storage with output block.
                                                       //public bool ReplaceSurfaceBlock = false; //Replaces block under ground storage with output block, PlaceRemainingItemAsBlock must be true,
                                                       //and RequiredSurfaceMaterials or RequiredCraftingSurfaceByTag must be set.
        public bool OffHandMustBeEmpty = false;
        public bool MainHandMustBeEmpty = false;
        public string[] RequiredClassTraits = null;
        public EnumTool? Tool { get; set; } = null;
        public int toolDamage = 1;
        public EnumTool? ToolOffhand { get; set; } = null;
        public int toolOffhandDamage = 1;
        public bool transferFreshness = false;
        public String[] RequiredCraftingSurfaceByTag = null;
        public string craftingSurfaceErrorLangCode = "";
        public EnumBlockMaterial[] RequiredSurfaceMaterials = null;
        public EnumBlockMaterial[] RequiredImmersedInMaterials = null;
        public EnumBlockMaterial[] RestrictedImmersedInMaterials = new EnumBlockMaterial[] { EnumBlockMaterial.Water, EnumBlockMaterial.Lava };
        public string surfaceErrorLangCode = "";
        public AssetLocation CompletionSound = "";
        public JsonItemStack[] MainHandProcessingItemsByCode = null;
        public JsonObject[] MainHandProcessingItemsByKey = null;
        public JsonItemStack[] OffHandProcessingItemsByCode = null;
        public JsonObject[] OffHandProcessingItemsByKey = null;
        //public JsonObject[] GroundStorageProcessItems = null;
        public string interactionHelpCode = "";
        public string handbookProcessIntoTitle = "";
        public string handbookCreatedByTitle = "";
        public EnumEntityAction[] RequiredActions = null;

        public ProcessableProperties(JsonObject newProcess)
        {
            if (newProcess == null)
            {

                return;
            }
            try
            {
                //The Nmae of the Recipe - Required for clarity in logging, processes without Name and ModID set will autoreject.
                //Both must be set manually to ensure compliance.
                Name = newProcess["name"]?.ToString() ?? Name;
                FromModID = newProcess["fromModID"]?.ToString() ?? FromModID;

                //Time in seconds for process to complete
                ProcessTime = newProcess["processTime"].Exists ? newProcess["processTime"].AsFloat(1) : ProcessTime;
                //BlockDropItemStacks that will be produced every time the process is completed.
                ProcessedStacks = newProcess["processedStacks"].Exists ? newProcess["processedStacks"].AsObject<BlockDropItemStack[]>() : ProcessedStacks;
                //The sound that will play during processing
                ProcessingSound = newProcess["processingSound"].Exists ? new AssetLocation(newProcess["processingSound"].ToString()) : ProcessingSound;
                //The animation that will play during processing
                ProcessingAnimationCode = newProcess["processingAnimationCode"]?.ToString() ?? ProcessingAnimationCode;
                //The target frame in the animation when the sound will play and particles will be produced.
                ProcessingAnimationTargetFrame = newProcess["processingAnimationTargetFrame"]?.AsFloat() ?? ProcessingAnimationTargetFrame;
                //This item will attemept to be placed in the GroundStorage or (if a block) will replace the ground storage based on PlaceRemainingItemAsBlock.
                //Will TryGiveItemStack otherwise.
                RemainingItem = newProcess["remainingItem"].Exists ? newProcess["remainingItem"].AsObject<JsonItemStack>() : null;
                //The number of items that will be consumed from the groundStorage if GroundStorageProcessItems[] is null.
                ConsumedGroundStorageStackQty = newProcess["consumedGroundStorageStackQty"].Exists ? newProcess["consumedGroundStorageStackQty"].AsInt(1) : ConsumedGroundStorageStackQty;
                //If the stacksize of the GroundStorage slot (or slots if multislot) must match GroundStorageProcessItems or consumedGroundStorageStackQty exactly.
                //Primarily used for PlaceItemAsBlock
                ExactGroundStorageStackQtyRequired = newProcess["ExactGroundStorageStackQtyRequired"].Exists ? newProcess["ExactGroundStorageStackQtyRequired"].AsBool(false) : ExactGroundStorageStackQtyRequired;
                //If true, the RemainingItem will be placed as a block in the world at the position of the ground storage instead of being given to the player. Will require an item that resolves to a block.
                ExactOrGreaterThanStorageQtyRequired = newProcess["ExactOrGreaterThanStorageQtyRequired"].Exists ? newProcess["ExactOrGreaterThanStorageQtyRequired"].AsBool(false) : ExactOrGreaterThanStorageQtyRequired;
                PlaceRemainingItemAsBlock = newProcess["placeRemainingItemAsBlock"].Exists ? newProcess["placeRemainingItemAsBlock"].AsBool(false) : PlaceRemainingItemAsBlock;

                //ReplaceSurfaceBlock 

                //If the players offhand must be empty.
                OffHandMustBeEmpty = newProcess["offHandMustBeEmpty"].Exists ? newProcess["offHandMustBeEmpty"].AsBool(false) : OffHandMustBeEmpty;
                //If the players activeHotBarSlot must be empty.
                MainHandMustBeEmpty = newProcess["mainHandMustBeEmpty"].Exists ? newProcess["mainHandMustBeEmpty"].AsBool(false) : MainHandMustBeEmpty;

                //Any class trait or traits the player must have to perform this process. Only one trait must be met, but multiple can be supplied. Can easily be altered
                //To require multiple traits at once if we should wish it.
                RequiredClassTraits = newProcess["requiredTraits"].Exists ? newProcess["requiredTraits"].AsArray<string>(null) : RequiredClassTraits;

                //Tool required to be held in the main hand and the damage it will receive upon completion.
                string toolCode = newProcess["tool"]?.ToString();
                EnumTool tools = newProcess["tool"].AsObject<EnumTool>();
                if (!string.IsNullOrWhiteSpace(toolCode))
                {
                    if (Enum.TryParse(toolCode, true, out EnumTool parsedTool))
                    {
                        Tool = parsedTool;
                    }
                    else
                    {
                        // Optional: log or throw a clearer error
                        throw new Exception($"Invalid tool value '{toolCode}' in process JSON.");
                    }
                }
                toolDamage = newProcess["toolDamage"].Exists ? newProcess["toolDamage"].AsInt(1) : toolDamage;


                //Tool that must be held in the offhand and the damage it will receive upon completion.
                string toolOffHandCode = newProcess["toolOffhand"]?.ToString();
                if (!string.IsNullOrWhiteSpace(toolOffHandCode))
                {
                    if (Enum.TryParse(toolOffHandCode, true, out EnumTool parsedTool))
                    {
                        ToolOffhand = parsedTool;
                    }
                    else
                    {
                        // Optional: log or throw a clearer error
                        throw new Exception($"Invalid tooloffhand value '{toolCode}' in process JSON.");
                    }
                }

                //Transfer freshness of input to output.
                transferFreshness = newProcess["transferFreshness"].Exists ? newProcess["transferFreshness"].AsBool(false) : transferFreshness;

                //Required tag that must exist on crafting surface for this process to be used.
                RequiredCraftingSurfaceByTag = newProcess["requiredCraftingSurfaceByTag"].Exists ? newProcess["requiredCraftingSurfaceByTag"].AsArray<string>(null) : RequiredCraftingSurfaceByTag;

                //The error code that will show up if the crafting surface is wrong. Error codes are not currently working as a non-matching recipe will not show up at all.
                craftingSurfaceErrorLangCode = newProcess["craftingSurfaceErrorLangCode"]?.ToString() ?? craftingSurfaceErrorLangCode;

                //Any valid materials for the block the GroundStorage is resting on for the process.
                RequiredSurfaceMaterials = newProcess["requiredSurfaceMaterials"].Exists ? newProcess["requiredSurfaceMaterials"].AsArray<EnumBlockMaterial>(null) : RequiredSurfaceMaterials;
                //Any materials the groundstorage must be immersed in for the process to work, defaults to null.
                RequiredImmersedInMaterials = newProcess["requiredImmersedInMaterials"].Exists ? newProcess["requiredImmersedInMaterials"].AsArray<EnumBlockMaterial>(null) : RequiredImmersedInMaterials;
                //Any materials the groundstorage must not be immersed in for the process to work, defaults to Water and Lava
                RestrictedImmersedInMaterials = newProcess["RestrictedImmersedInMaterials"].AsArray<EnumBlockMaterial>(new EnumBlockMaterial[] { EnumBlockMaterial.Water, EnumBlockMaterial.Lava });
                //The error code that will show up if the surface material lacks the correct tag or material. Error codes are not currently working as a non-matching recipe will not show up at all.
                surfaceErrorLangCode = newProcess["surfaceErrorLangCode"]?.ToString() ?? surfaceErrorLangCode;
                //The sound that plays upon completion of the process. No sound by default.
                CompletionSound = newProcess["completionSound"].Exists ? new AssetLocation(newProcess["completionSound"].ToString()) : CompletionSound;
                //A list of itemstacks and stacksizes that can be held in the main hand, if listed, one must be in the activeHotbarSlot in the appropriate quantities to process.
                MainHandProcessingItemsByCode = newProcess["mainHandProcessingItemsByCode"].Exists ? newProcess["mainHandProcessingItemsByCode"].AsObject<JsonItemStack[]>() : MainHandProcessingItemsByCode;
                //A list of tags and stacksizes that indicate valid processing items for the main hand and the quantities desired.
                MainHandProcessingItemsByKey = newProcess["mainHandProcessingItemsByKey"].Exists ? newProcess["mainHandProcessingItemsByKey"].AsObject<JsonObject[]>() : MainHandProcessingItemsByKey;
                //A list of itemstacks and stacksizes that can be held in the offhand, if listed, one must be in the offhandHotbarSlot in the appropriate quantities to process.
                OffHandProcessingItemsByCode = newProcess["offHandProcessingItemsByCode"].Exists ? newProcess["offHandProcessingItemsByCode"].AsObject<JsonItemStack[]>() : OffHandProcessingItemsByCode;
                //A list of tags and stacksizes that indicate valid processing items for the offhand and the quantities desired.
                OffHandProcessingItemsByKey = newProcess["offHandProcessingItemsByKey"].Exists ? newProcess["offHandProcessingItemsByKey"].AsObject<JsonObject[]>() : OffHandProcessingItemsByKey;
                //This number is set once the itemstack held in the offhand has been determined. It is not set as part of describing the process.

                //(Unimplemented) GroundStorageProcessItems is A list of up to four itemstacks that will be looked for in a target groundstorage.
                //If all items are present in the indicated quantities, will produce a desired output. This can require an exact quantity per itemstack or simply a minimum quantity,
                //depending on the value of ExactGroundStorageStackQtyRequired. Should only produce one group of processed stacks. Mass production will require ongoing interaction.
                //Optionally, at the whim of the gods, it can be set to produce as many groups of processed stacks as the quantity of input stacks allows for.  (Vinter: It's my preference that
                //Only one group of processed stacks is output at a time.)

                //The language code for the interaction help text that appears when looking at the ground storage with this process available.                   
                interactionHelpCode = newProcess["interactionHelpCode"]?.ToString() ?? interactionHelpCode;
                //If all items are present in the indicated quantities, will produce a desired output. This can require an exact quantity per itemstack or simply a minimum quantity,
                //depending on the value of ExactGroundStorageStackQtyRequired. Should only produce one group of processed stacks. Mass production will require ongoing interaction.
                //Optionally, at the whim of the gods, it can be set to produce as many groups of processed stacks as the quantity of input stacks allows for.  (Vinter: It's my preference that
                //Only one group of processed stacks is output at a time.)
                handbookProcessIntoTitle = newProcess["handbookProcessIntoTitle"]?.ToString() ?? handbookProcessIntoTitle;
                handbookCreatedByTitle = newProcess["handbookCreatedByTitle"]?.ToString() ?? handbookCreatedByTitle;

                string[] actionCodes = newProcess["requiredActions"]?.AsArray<String>();
                if (actionCodes != null)
                {
                    List<EnumEntityAction> actions = new();
                    for (int i = 0; i < actionCodes.Length; i++)
                    {
                        if (Enum.TryParse(actionCodes[i], true, out EnumEntityAction parsedAction))
                        {
                            actions.Add(parsedAction);
                        }
                        else
                        {
                            // Optional: log or throw a clearer error
                            throw new Exception($"Invalid action code '{actionCodes[i]}' in process JSON.");
                        }
                        RequiredActions = actions.ToArray();
                    }
                }
            }
            catch (Exception e)
            {
                // Catch and log any exceptions during deserialization to avoid crashing the game due to mod errors.
                // This also helps identify issues with process definitions that may be causing valid processes to be ignored.
                Console.WriteLine($"Error deserializing ProcessableProperties: {e}");
            }
        }     
    }
}
