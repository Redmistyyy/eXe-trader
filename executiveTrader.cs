using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils.Cloners;
using System.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Models.Eft.Hideout;

namespace executive
{
    [Injectable(TypePriority = OnLoadOrder.PostSptModLoader + 1)]
    public class AddLocales(ISptLogger<AddLocales> logger, DatabaseService databaseService, ModHelper modHelper, LocaleService localeService)
    {
        private string ModPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());

        public void AddLocalesMultipleLang()
        {
           var sptLocales = databaseService.GetTables().Locales.Global;
           var myLangNames = ExeHelper.GetFileNamesWithoutExtension(ModPath + "/db/locales/");

            foreach (var myLangName in myLangNames)
            {
                var myLangFile = modHelper.GetJsonDataFromFile<Dictionary<string, string>>(ModPath, $"db/locales/{myLangName}.json");
                if (databaseService.GetTables().Locales.Global.TryGetValue(myLangName, out var lazyLoadedValue))
                {
                    lazyLoadedValue.AddTransformer(lazyLoadedValueData =>
                    {
                        foreach (var kvp in myLangFile)
                        {
                            lazyLoadedValueData.Add(kvp.Key, kvp.Value);
                        }
                        return lazyLoadedValueData;
                    });
                }
            }
        }
    }


    [Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1)]
    public class AddExecutiveTrader(ISptLogger<AddExecutiveTrader> logger, ICloner cloner, DatabaseService databaseService, DatabaseServer databaseServer, ConfigServer configServer, ModHelper modHelper, ItemHelper itemHelper, ImageRouter imageRouter)
    {
        private readonly TraderConfig _traderConfig = configServer.GetConfig<TraderConfig>();
        private readonly RagfairConfig _ragfairConfig = configServer.GetConfig<RagfairConfig>();
        private string ModPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        public readonly string _traderName = "Executive";
        public readonly string _modName = "eXe";
        public readonly string _traderId = "66e708118c4fb55f239384cd";
        
        private void SetTraderUpdateTime()
        {
            var traderRefreshRecord = new UpdateTime
            {
                TraderId = _traderId,
                Seconds = new MinMax<int>(1800, 3600)
            };

            _traderConfig.UpdateTime.Add(traderRefreshRecord);
            _ragfairConfig.Traders.Add(_traderId, true);
        }
        private void CreateEmptyTraderAssort()
        {
            var traderBase = modHelper.GetJsonDataFromFile<TraderBase>(ModPath, "db/trader/base.json");
            
            // SPT method
            // Create an empty assort ready for our items
            var emptyTraderItemAssortObject = new TraderAssort
            {
                Items = [],
                BarterScheme = new Dictionary<MongoId, List<List<BarterScheme>>>(),
                LoyalLevelItems = new Dictionary<MongoId, int>()
            };

            var traderDataToAdd = new Trader
            {
                Assort = emptyTraderItemAssortObject,
                Base = cloner.Clone(traderBase),
                QuestAssort = new Dictionary<string, Dictionary<string, string>> 
                // quest assort is empty as trader has no assorts unlocked by quests
            {
                // We create 3 empty arrays, one for each of the main statuses that are possible
                { "Started", new Dictionary<string, string>() },
                { "Success", new Dictionary<string, string>() },
                { "Fail", new Dictionary<string, string>() }
            }
            };

            // Add the new trader id and data to the server
            if (!databaseService.GetTables().Traders.TryAdd(traderBase.Id, traderDataToAdd))
            {
                //Failed to add trader!
                logger.Error($"[{_modName}] Trader Executive failed to load! Please report issue to mod author.");
            }
        }
        private void AddTraderImageRouter()
        {
            var traderBase = modHelper.GetJsonDataFromFile<TraderBase>(ModPath, "db/trader/base.json");
            var avatarRouterPath = System.IO.Path.Combine(ModPath, $"res/avatars/{_traderId}.png");
            imageRouter.AddRoute(traderBase.Avatar.Replace(".png", ""), avatarRouterPath);
        }
        private void AddBarterSchemesAndLoyality(MongoId assortId, TraderAssort source, TraderAssort target)
        {
            if (source.BarterScheme.TryGetValue(assortId, out var barterScheme))
            {
                target.BarterScheme[assortId] = barterScheme;
            }
            if (source.LoyalLevelItems.TryGetValue(assortId, out var loyaltyLevel))
            {
                target.LoyalLevelItems[assortId] = loyaltyLevel;
            }
        }
        private void AddTraderAssort()
        {
            // Initialization
            var traderAssort = modHelper.GetJsonDataFromFile<TraderAssort>(ModPath, "db/trader/assort.json");
            var itemdb = databaseServer.GetTables().Templates.Items;

            // logger.Warning(JsonConvert.SerializeObject(traderAssort, Formatting.Indented));

            // Assort stage
            var assortItem = traderAssort.Items;
            var assortBarterSchemes = traderAssort.BarterScheme;
            var assortLoyality = traderAssort.LoyalLevelItems;

            var assortDataToAdd = new TraderAssort
            {
                Items = [],
                BarterScheme = new Dictionary<MongoId, List<List<BarterScheme>>>(),
                LoyalLevelItems = new Dictionary<MongoId, int>()
            };

            // An 'assort' is the term used to describe the offers a trader sells, it has 3 parts to an assort
            foreach (var item in assortItem)
            {
                var assortId = item.Id;
                var itemTpl = item.Template;
                

                // Check if the item existed or not
                if (itemdb.ContainsKey(itemTpl))
                {
                    // 1: The item
                    // Check if the item is ammobox or not
                    if (itemdb[itemTpl].Parent.Equals("543be5cb4bdc2deb348b4568"))
                    {
                        var cartridge = new List<Item> { item };
                        itemHelper.AddCartridgesToAmmoBox(cartridge, itemdb[itemTpl]);
                        assortDataToAdd.Items.AddRange(cartridge);
                        AddBarterSchemesAndLoyality(assortId, traderAssort, assortDataToAdd);
                    }
                    else
                    {
                        assortDataToAdd.Items.Add(item);
                        AddBarterSchemesAndLoyality(assortId, traderAssort, assortDataToAdd);
                    }
                }
                else
                {
                    logger.Warning($"[{_modName}] Item {itemTpl} in assort doesn't exist in item database, please check if it is a custom item.");
                }
            }

            if (!databaseService.GetTables().Traders.TryGetValue(_traderId, out var traderToEdit))
            {
                logger.Warning($"Unable to update assorts for trader: {_traderId}, they couldn't be found on the server");
                return;
            }

            // Override the traders assorts with the ones we passed in
            traderToEdit.Assort = assortDataToAdd;
        }
        private void AddCustomRecipes()
        {
            var sptRecipes = databaseServer.GetTables().Hideout.Production.Recipes;
            var myRecipes = modHelper.GetJsonDataFromFile<Dictionary<string, HideoutProduction>>(ModPath, "db/templates/hideout/customProduction.json");

            if (myRecipes?.Count == 0)
            {
                logger.Warning($"[{_modName}] No custom recipes found in the specified path.");
                return;
            }
            sptRecipes.AddRange(myRecipes.Values);
        }

        private void AddCustomQuests()
        {
            var sptQuests = databaseServer.GetTables().Templates.Quests;
            var myQuestsDirectory = System.IO.Path.Combine(ModPath, "db/templates/quests");
            var myQuestsImagesDirectory = System.IO.Path.Combine(ModPath, "res/quests");
            var myQuestFiles = ExeHelper.GetFileNamesWithoutExtension(myQuestsDirectory);

            foreach (var myQuestFile in myQuestFiles)
            {
                var myQuest = modHelper.GetJsonDataFromFile<Quest>(ModPath, $"db/templates/quests/{myQuestFile}.json");
                if (myQuest is not null)
                {
                    sptQuests.Add(myQuest.Id, myQuest);
                    var questsImagePath = System.IO.Path.Combine(myQuestsImagesDirectory, $"{myQuest.Id}.png");
                    imageRouter.AddRoute(myQuest.Image.Replace(".png", ""), questsImagePath);
                }
                else
                {
                    logger.Warning($"[{_modName}] Quest file {myQuestFile} could not be loaded, please check the file format.");
                }
            }
        }

        public void AddTrader()
        {
            SetTraderUpdateTime();
            AddTraderImageRouter();
            CreateEmptyTraderAssort();
            AddTraderAssort();
            AddCustomRecipes();
            AddCustomQuests();
            
            try
            {
                
            }
            catch (Exception ex)
            {
                logger.Error($"An error occurred: {ex.Message}", ex);
            }
        }
    }
}


