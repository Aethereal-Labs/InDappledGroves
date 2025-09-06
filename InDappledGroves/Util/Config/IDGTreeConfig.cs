using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace InDappledGroves.Util.Config
{
    [ProtoBuf.ProtoContract()]
    public class IDGTreeConfig
    {

        //Multiplier applied when trees are being chopped, higher numbers reduces chopping speed of trees, lower numbers reduce time to chop trees.
        //Default is 1
        [ProtoMember(1)]
        public float TreeFellingMultiplier { get; set; }
        //Rate at which Tree Hollows Update

        public bool SaplingSpacingEnabled { get; set; }

        [ProtoMember(2)]
        public int MinHorizontalSaplingDistance { get; set; }
        
        [ProtoMember(3)]
        public int MinVerticalSaplingDistance { get; set; }

        [ProtoMember(4)]
        public float ConfigVersion { get; set; }


        public IDGTreeConfig()
        { }

        public static IDGTreeConfig Current { get; set; }

        public static IDGTreeConfig GetDefault()
        {
            IDGTreeConfig defaultConfig = new();

            defaultConfig.TreeFellingMultiplier = 1;
            defaultConfig.SaplingSpacingEnabled = true;
            defaultConfig.MinHorizontalSaplingDistance = 3;
            defaultConfig.MinVerticalSaplingDistance = 10;
            defaultConfig.ConfigVersion = 1.1f;

            return defaultConfig;
        }

        public static void CreateConfigFile(ICoreAPI api)
        {
            //Tree Config
            try
            {
                var Config = api.LoadModConfig<IDGTreeConfig>("indappledgroves/treeconfig.json");
                if (Config != null)
                {
                    if (Config.ConfigVersion == GetDefault().ConfigVersion)
                    {
                        api.Logger.Notification("Mod Config successfully loaded.");
                        IDGTreeConfig.Current = Config;
                    }
                    else
                    {
                        api.Logger.Notification("Tree Config Versions Do Not Match, Updating To Current Default");
                        IDGTreeConfig.Current = IDGTreeConfig.GetDefault();
                    }
                }
                else
                {
                    api.Logger.Notification("No Mod Config specified. Falling back to default settings");
                    IDGTreeConfig.Current = IDGTreeConfig.GetDefault();
                }
            }
            catch
            {
                IDGTreeConfig.Current = IDGTreeConfig.GetDefault();
                api.Logger.Error("IDG Tree Config Failed to load custom mod configuration. Falling back to default settings!");
            }
            finally
            {
                api.StoreModConfig(IDGTreeConfig.Current, "indappledgroves/treeconfig.json");
            }
        }
    }
}
