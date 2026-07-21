namespace NuclearOptionCommander;

internal sealed class CommanderPersistentOperations
{
    private readonly CommanderSpawnService spawnService;
    private readonly CommanderSupplyHeliService supplyHeliService;
    private readonly CommanderAirCommandService airCommandService;
    private readonly CommanderMobileEmplacementService mobileEmplacementService;

    internal CommanderPersistentOperations(
        CommanderSpawnService spawnService,
        CommanderSupplyHeliService supplyHeliService,
        CommanderAirCommandService airCommandService,
        CommanderMobileEmplacementService mobileEmplacementService)
    {
        this.spawnService = spawnService;
        this.supplyHeliService = supplyHeliService;
        this.airCommandService = airCommandService;
        this.mobileEmplacementService = mobileEmplacementService;
    }

    internal void Tick()
    {
        spawnService.TickPersistent();
        supplyHeliService.TickPersistent();
        airCommandService.TickPersistent();
        mobileEmplacementService.TickPersistent();
    }
}
