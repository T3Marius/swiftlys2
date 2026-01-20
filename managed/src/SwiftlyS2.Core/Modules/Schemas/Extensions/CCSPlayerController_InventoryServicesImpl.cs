using SwiftlyS2.Core.Extensions;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.ProtobufDefinitions;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace SwiftlyS2.Core.SchemaDefinitions;

internal partial class CCSPlayerController_InventoryServicesImpl
{

    public CSOPersonaDataPublic? SOPersonaDataPublic {
        get {
            var ptr = EquippedPlayerSprayIDs.Address.Read<nint>(-8);
            return ptr.IsValidPtr() ? Helper.AsProtobuf<CSOPersonaDataPublic>(ptr + 8, false) : null;
        }
    }

}