using System.Security.Cryptography.X509Certificates;
using SwiftlyS2.Core.Natives.NativeObjects;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Schemas;

namespace SwiftlyS2.Core.Schemas;

internal abstract class SchemaClass : NativeHandle {
  public SchemaClass(nint handle) : base(handle) {
  }

}