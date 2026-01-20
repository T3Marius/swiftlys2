using System.Reflection;
using System.Runtime.InteropServices;
using SwiftlyS2.Shared.Datamaps;
using SwiftlyS2.Shared.Misc;

namespace SwiftlyS2.Core.AttributeParsers;

internal static class DatamapHookAttributeParser
{
    public static void ParseFromObject( this IDatamapFunctionService self, object instance )
    {
        var type = instance.GetType();
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (var method in methods)
        {
            var datamapHookAttribute = method.GetCustomAttribute<DatamapHookAttribute>();
            if (datamapHookAttribute != null)
            {
                var contextType = method.GetParameters()[0].ParameterType;
                var hookContextInterface = contextType.GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType &&
                    i.GetGenericTypeDefinition() == typeof(IDatamapFunctionHookContext<>));
                if (hookContextInterface != null)
                {
                    foreach (var datamapFunction in self.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    {
                        if (datamapFunction.PropertyType.GenericTypeArguments[1] == contextType)
                        {
                            var mode = datamapHookAttribute.HookMode == HookMode.Pre ? "HookPre" : "HookPost";
                            var datamapFuncObject = datamapFunction.GetValue(self);
                            _ = typeof(IDatamapFunctionOperator<,>)
                                    .MakeGenericType(datamapFunction.PropertyType.GenericTypeArguments[0], contextType)
                                    .GetMethod(mode)!
                                    .Invoke(datamapFuncObject, [
                                        Delegate.CreateDelegate(typeof(Action<>).MakeGenericType(contextType), instance, method)
                                    ]);
                        }
                    }
                }
                else
                {
                    throw new InvalidOperationException($"Method {method.Name} has no IDatamapFunctionHookContext parameter");
                }
            }
        }
    }
}