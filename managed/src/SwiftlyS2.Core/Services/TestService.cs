using System.Reflection;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Core.Natives;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Core.Extensions;
using SwiftlyS2.Shared.EntitySystem;
using SwiftlyS2.Shared.SchemaDefinitions;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Core.Datamaps;
using SwiftlyS2.Shared.Datamaps;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.StringTable;
using SwiftlyS2.Shared.ProtobufDefinitions;

namespace SwiftlyS2.Core.Services;

internal class TestService
{
    // [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    // internal delegate nint DispatchSpawnHook( nint entity, nint kv );

    private readonly ILogger<TestService> logger;
    private readonly ProfileService profile;
    private readonly ISwiftlyCore core;

    public unsafe TestService( ILogger<TestService> logger, ProfileService profileService, ISwiftlyCore core )
    {
        this.profile = profileService;
        this.core = core;
        this.logger = logger;

        logger.LogWarning("TestService created");
        logger.LogWarning("TestService created");
        logger.LogWarning("TestService created");
        logger.LogWarning("TestService created");
        logger.LogWarning("TestService created");
        logger.LogWarning("TestService created");
        logger.LogWarning("TestService created");
        logger.LogWarning("TestService created");
        logger.LogWarning("TestService created");

        core.Registrator.Register(this);
        Test2();
    }

    private static void PrintStructFields<T>( T obj ) where T : struct
    {
        var fields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance);

        foreach (var field in fields)
        {
            var value = field.GetValue(obj);
            Console.WriteLine($"{field.Name}: {value}");
        }
    }

    public void Test()
    {
        _ = core.Scheduler.RepeatBySeconds(1.0f, () =>
        {
            var gameServer = NativeEngineHelpers.GetNetworkGameServer();
            unsafe
            {
                ref var array = ref gameServer.AsRef<CUtlVector<nint>>(624);
                foreach (var client in array)
                {
                    if (client == 0)
                    {
                        continue;
                    }
                    ref var serversideClient = ref client.AsRef<CServerSideClient>();
                    PrintStructFields(serversideClient.Base);
                }
            }
        });
    }

    public void Test2()
    {
        core.Event.OnStartupServer += () =>
        {
            var table = core.StringTable.FindTable("InfoPanel")!;
            _ = table.GetOrAddString("motd");
            _ = table.SetStringUserData("motd", StringTableUserData.FromString("https://swiftlys2.net"));
        };
        core.Event.OnClientPutInServer += (@event) =>
        {
            var table = core.StringTable.FindTable("InfoPanel")!;
            CRecipientFilter filter = new();
            filter.AddAllPlayers();
            table.ReplicateUserData("motd", StringTableUserData.FromString("https://google.com"), filter);

        };

        
    }

    [DatamapHook(HookMode.Pre)]
    public void Test2( IDHookCCSPlayerControllerInventoryUpdateThink ctx )
    {
        Console.WriteLine($"IDHookCCSPlayerControllerInventoryUpdateThink -> ctx: {ctx.SchemaObject.DesignerName}");

    }

    // [EntityOutputHandler("*", "*")]
    // public void Test3( IOnEntityFireOutputHookEvent @event )
    // {
    //     Console.WriteLine("MFMFMFMFMFMFMFMFMF");
    //     Console.WriteLine($"HookEntityOutput -> designerName: {@event.DesignerName} output: {@event.OutputName}, activator: {@event.Activator?.As<CBaseEntity>()?.DesignerName}, caller: {@event.Caller?.As<CBaseEntity>()?.DesignerName}, value: {@event.VariantValue}, delay: {@event.Delay}");
    // }

    // [EntityInputHandler("*", "*")]
    // public void Test4( IOnEntityIdentityAcceptInputHookEvent @event )
    // {
    //     Console.WriteLine("FMFMFMFMFMFMFMFMFM");
    //     Console.WriteLine($"HookEntityInput -> designerName: {@event.DesignerName} output: {@event.InputName}, activator: {@event.Activator?.As<CBaseEntity>()?.DesignerName}, caller: {@event.Caller?.As<CBaseEntity>()?.DesignerName}, value: {@event.VariantValue}");
    // }

    [EntityOutputHandler<CPropDoorRotating>("*")]
    public void Test3( IOnEntityFireOutputHookEvent @event )
    {
        Console.WriteLine($"HookEntityOutput -> designerName: {@event.DesignerName} output: {@event.OutputName}, activator: {@event.Activator?.As<CBaseEntity>()?.DesignerName}, caller: {@event.Caller?.As<CBaseEntity>()?.DesignerName}, value: {@event.VariantValue}, delay: {@event.Delay}");
    }

    [EntityInputHandler<CCSPlayerPawn>("*")]
    public void Test4( IOnEntityIdentityAcceptInputHookEvent @event )
    {
        Console.WriteLine($"HookEntityInput -> designerName: {@event.DesignerName} output: {@event.InputName}, activator: {@event.Activator?.As<CBaseEntity>()?.DesignerName}, caller: {@event.Caller?.As<CBaseEntity>()?.DesignerName}, value: {@event.VariantValue}");
    }
}