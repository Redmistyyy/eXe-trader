using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;

namespace executive;

public record ModMetadata : AbstractModMetadata
    {
        // Basic mod information
        public override string ModGuid { get; init; } = "com.redmisty.executive";
        public override string Name { get; init; } = "Executive";
        public override string Author { get; init; } = "Redmisty";
        public override string SptVersion { get; init; } = "4.0.0";
        public override string Version { get; init; } = "0.0.2";
        public override List<string>? Contributors { get; set; }
        public override List<string>? LoadBefore { get; set; }
        public override List<string>? LoadAfter { get; set; }
        public override List<string>? Incompatibilities { get; set; }
        public override Dictionary<string, string>? ModDependencies { get; set; }
        public override string? Url { get; set; } = "https://github.com/sp-tarkov/server-mod-examples";
        public override bool? IsBundleMod { get; set; } = false;
        public override string? License { get; init; } = "MIT";
    }

[Injectable(TypePriority = OnLoadOrder.PostSptModLoader + 1)]

public class PostSptMlContents(ISptLogger<PostSptMlContents> logger, AddLocales addLocales) : IOnLoad
{ 
    public Task OnLoad()
    {
        try 
        {
            addLocales.AddLocalesMultipleLang();
        }
        catch (Exception ex)
        {
            logger.Error($"An error occurred: {ex.Message}", ex);
        }
        
        return Task.CompletedTask;
    }
}

[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1)]

public class PostDBContents (ISptLogger<PostDBContents> logger, AddExecutiveTrader addExecutiveTrader) : IOnLoad
{
    public Task OnLoad()
    {
        // Trader load Stage
        addExecutiveTrader.AddTrader();

        logger.LogWithColor($"[{addExecutiveTrader._modName}] {addExecutiveTrader._traderName} advent!", LogTextColor.Magenta);
        return Task.CompletedTask;
    }
}