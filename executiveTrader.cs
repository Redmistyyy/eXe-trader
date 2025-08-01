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

namespace executive
{
    [Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1)]
    public class AddExecutiveTrader(ISptLogger<AddExecutiveTrader> logger, ICloner cloner, DatabaseService databaseService, DatabaseServer databaseServer, ConfigServer configServer, ModHelper modHelper, ItemHelper itemHelper)
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
                        // logger.Debug($"[{_modName}] Item {itemTpl} is ammobox, filled it with Nikita's love.");
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

        /// <summary>
        public void AddTraderToLocales(string firstName, string description)
        {
            // For each language, add locale for the new trader
            var locales = databaseService.GetTables().Locales.Global;
            var traderBase = modHelper.GetJsonDataFromFile<TraderBase>(ModPath, "db/trader/base.json");
            var newTraderId = traderBase.Id;
            var fullName = traderBase.Name;
            var nickName = traderBase.Nickname;
            var location = traderBase.Location;

            foreach (var (localeKey, localeKvP) in locales)
            {
                // We have to add a transformer here, because locales are lazy loaded due to them taking up huge space in memory
                // The transformer will make sure that each time the locales are requested, the ones added below are included
                localeKvP.AddTransformer(lazyloadedLocaleData =>
                {
                    lazyloadedLocaleData.Add($"{newTraderId} FullName", fullName);
                    lazyloadedLocaleData.Add($"{newTraderId} FirstName", firstName);
                    lazyloadedLocaleData.Add($"{newTraderId} Nickname", nickName);
                    lazyloadedLocaleData.Add($"{newTraderId} Location", location);
                    lazyloadedLocaleData.Add($"{newTraderId} Description", description);
                    return lazyloadedLocaleData;
                });
            }
        }

        public void AddTrader()
        {
            try
            {
                SetTraderUpdateTime();
                CreateEmptyTraderAssort();
                AddTraderAssort();
            }
            catch (Exception ex)
            {
                logger.Error($"An error occurred: {ex.Message}", ex);
            }
        }
    }
}


