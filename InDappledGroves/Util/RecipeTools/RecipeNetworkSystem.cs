using ProtoBuf;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using static InDappledGroves.Util.RecipeTools.IDGRecipeNames;

namespace InDappledGroves.Util.RecipeTools
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class RecipeUpload
    {
        public List<string> bwsvalues; //Basic Workstation Recipe Values
        public List<string> cwsvalues;  //Complex Workstation Recipe Values
        public List<string> gvalues;  //Ground Recipe Values
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class RecipeSyncRequest
    {
        public string reason;
    }

    public class RecipeUploadSystem : ModSystem
    {
        #region Client
        IClientNetworkChannel clientChannel;
        ICoreClientAPI clientApi;
        long recipeSyncListenerId;

        public override void StartClientSide(ICoreClientAPI api)
        {
            clientApi = api;

            clientChannel =
                api.Network.RegisterChannel("idgrecipechannel")
                .RegisterMessageType(typeof(RecipeUpload))
                .RegisterMessageType(typeof(RecipeSyncRequest))
                .SetMessageHandler<RecipeUpload>(OnServerMessage);

            // Do not depend only on the server's PlayerNowPlaying push.
            // Request sync once the client world/player is available.
            recipeSyncListenerId = api.Event.RegisterGameTickListener(dt =>
            {
                if (clientApi.World == null || clientApi.World.Player == null) return;

                clientChannel.SendPacket(new RecipeSyncRequest()
                {
                    reason = "initial-client-recipe-sync"
                });

                clientApi.Event.UnregisterGameTickListener(recipeSyncListenerId);
            }, 250);
        }


        private void OnServerMessage(RecipeUpload networkMessage)
        {

            List<BasicWorkstationRecipe> bwsrecipes = new();
            List<ComplexWorkstationRecipe> cwsrecipes = new();
            List<GroundRecipe> grecipes = new();
            #endregion


            #region Register Ground Recipes
            if (networkMessage.gvalues != null)
            {
                foreach (string grec in networkMessage.gvalues)
                {
                    using (MemoryStream ms = new(Ascii85.Decode(grec)))
                    {
                        BinaryReader reader = new BinaryReader(ms);

                        GroundRecipe retr = new GroundRecipe();
                        retr.FromBytes(reader, clientApi.World);

                        grecipes.Add(retr);
                    }
                }
            }

            IDGRecipeRegistry.Loaded.GroundRecipes = grecipes;
            #endregion

            #region Register Basic Workstation Recipes
            if (networkMessage.bwsvalues != null)
            {
                foreach (string bwsrec in networkMessage.bwsvalues)
                {
                    using (MemoryStream ms = new(Ascii85.Decode(bwsrec)))
                    {
                        BinaryReader reader = new BinaryReader(ms);

                        BasicWorkstationRecipe retr = new BasicWorkstationRecipe();
                        retr.FromBytes(reader, clientApi.World);

                        bwsrecipes.Add(retr);
                    }
                }
            }
            IDGRecipeRegistry.Loaded.BasicWorkstationRecipes = bwsrecipes;
            #endregion

            #region Register Complex Workstation Recipes
            if (networkMessage.cwsvalues != null)
            {
                foreach (string cwsrec in networkMessage.cwsvalues)
                {
                    using (MemoryStream ms = new(Ascii85.Decode(cwsrec)))
                    {
                        BinaryReader reader = new BinaryReader(ms);

                        ComplexWorkstationRecipe retr = new ComplexWorkstationRecipe();
                        retr.FromBytes(reader, clientApi.World);

                        cwsrecipes.Add(retr);
                    }
                }
            }
            IDGRecipeRegistry.Loaded.ComplexWorkstationRecipes = cwsrecipes;
            #endregion

        }

        #region Server
        IServerNetworkChannel serverChannel;
        ICoreServerAPI serverApi;

        public override void StartServerSide(ICoreServerAPI api)
        {
            serverApi = api;

            serverChannel =
                api.Network.RegisterChannel("idgrecipechannel")
                .RegisterMessageType(typeof(RecipeUpload))
                .RegisterMessageType(typeof(RecipeSyncRequest))
                .SetMessageHandler<RecipeSyncRequest>(OnClientRecipeSyncRequest);

            api.RegisterCommand("recipeupload", "Resync recipes", "", OnRecipeUploadCmd, Privilege.chat);

            api.Event.PlayerNowPlaying += player =>
            {
                SendRecipesTo(player);
            };
        }

        private void OnClientRecipeSyncRequest(IPlayer fromPlayer, RecipeSyncRequest networkMessage)
        {
            if (fromPlayer is IServerPlayer serverPlayer)
            {
                SendRecipesTo(serverPlayer);
            }
        }

        private RecipeUpload BuildRecipeUpload()
        {
            List<string> bwsrecipes = new();
            List<string> cwsrecipes = new();
            List<string> grecipes = new();

            foreach (BasicWorkstationRecipe bwsrec in IDGRecipeRegistry.Loaded.BasicWorkstationRecipes ?? new())
            {
                using MemoryStream ms = new();
                using BinaryWriter writer = new(ms);

                bwsrec.ToBytes(writer);
                writer.Flush();

                bwsrecipes.Add(Ascii85.Encode(ms.ToArray()));
            }

            foreach (ComplexWorkstationRecipe cwsrec in IDGRecipeRegistry.Loaded.ComplexWorkstationRecipes ?? new())
            {
                using MemoryStream ms = new();
                using BinaryWriter writer = new(ms);

                cwsrec.ToBytes(writer);
                writer.Flush();

                cwsrecipes.Add(Ascii85.Encode(ms.ToArray()));
            }

            foreach (GroundRecipe grec in IDGRecipeRegistry.Loaded.GroundRecipes ?? new())
            {
                using MemoryStream ms = new();
                using BinaryWriter writer = new(ms);

                grec.ToBytes(writer);
                writer.Flush();

                grecipes.Add(Ascii85.Encode(ms.ToArray()));
            }

            return new RecipeUpload()
            {
                bwsvalues = bwsrecipes,
                cwsvalues = cwsrecipes,
                gvalues = grecipes,
            };
        }

        private void SendRecipesTo(IServerPlayer player)
        {
            serverChannel.SendPacket(BuildRecipeUpload(), player);
        }

        private void BroadcastRecipes()
        {
            serverChannel.BroadcastPacket(BuildRecipeUpload());
        }

        private void OnRecipeUploadCmd(IServerPlayer player = null, int groupId = 0, CmdArgs args = null)
        {
            if (player != null) SendRecipesTo(player);
            else BroadcastRecipes();
        }

        private void OnClientMessage(IPlayer fromPlayer, RecipeSyncRequest networkMessage)
        {
            OnRecipeUploadCmd();
        }


        #endregion
    }
}
