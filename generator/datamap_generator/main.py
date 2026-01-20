import os
import json
from pathlib import Path
import shutil

SCRIPT_DIR = os.path.pardir
DEFAULT_DATAMAP_PATH = os.path.join(SCRIPT_DIR, "datamaps.json")
FOLDER = Path("../../managed/src/SwiftlyS2.Generated/Datamaps")


MANAGER_TEMPLATE = """using SwiftlyS2.Shared.SchemaDefinitions;
using SwiftlyS2.Core.Hooks;

namespace SwiftlyS2.Core.Datamaps;

internal partial class DatamapFunctionManager
{
    public HookManager HookManager { get; }

$fields$

    public DatamapFunctionManager(HookManager hookManager)
    {
        HookManager = hookManager;
$constructors$
    }

}
"""


MANAGER_FUNCTION_FIELDS_TEMPLATE = """    public BaseDatamapFunction<$className$, DHook$functionName$> $functionName$ { get; init; }
"""

MANAGER_FUNCTION_CONSTRUCTOR_TEMPLATE = """        $functionName$ = new(this, $functionHash$);"""

SERVICE_TEMPLATE = """using SwiftlyS2.Shared.Datamaps;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace SwiftlyS2.Core.Datamaps;

internal partial class DatamapFunctionService : IDatamapFunctionService
{
    $functions$
}
"""

SERVICE_FUNCTION_TEMPLATE = """
    public IDatamapFunctionOperator<$className$, DHook$functionName$> $functionName$ { get; } = manager.$functionName$.Get(ctx.Name, profiler);

    IDatamapFunctionOperator<$className$, IDHook$functionName$> IDatamapFunctionService.$functionName$ => $functionName$;
"""

SERVICE_INTERFACE_TEMPLATE = """using SwiftlyS2.Shared.Datamaps;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace SwiftlyS2.Shared.Datamaps;

public partial interface IDatamapFunctionService
{
$functions$
}
"""

SERVICE_INTERFACE_FUNCTION_TEMPLATE = """
    public IDatamapFunctionOperator<$className$, IDHook$functionName$> $functionName$ { get; }
"""

HOOK_CONTEXT_TEMPLATE = """using SwiftlyS2.Shared.Datamaps;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace SwiftlyS2.Core.Datamaps;

internal class DHook$functionName$ : BaseDatamapFunctionHookContext<$className$>, IDHook$functionName$
{
}
"""

HOOK_CONTEXT_INTERFACE_TEMPLATE = """using SwiftlyS2.Shared.SchemaDefinitions;

namespace SwiftlyS2.Shared.Datamaps;

public interface IDHook$functionName$ : IDatamapFunctionHookContext<$className$>
{
}
"""

def format_template(template, **params):
    for key, value in params.items():
        template = template.replace(f"${key}$", str(value))
    return template

def main():
    if FOLDER.exists():
        shutil.rmtree(str(FOLDER))
    os.makedirs(FOLDER)
    os.makedirs(FOLDER / "Interfaces")
    os.makedirs(FOLDER / "Classes")

    f_service_interface = open(FOLDER / "Interfaces" / "IDatamapFunctionService.cs", "w", encoding="utf-8")
    f_manager = open(FOLDER / "Classes" / "DatamapFunctionManager.cs", "w", encoding="utf-8")
    f_service = open(FOLDER / "Classes" / "DatamapFunctionService.cs", "w", encoding="utf-8")

    with open(DEFAULT_DATAMAP_PATH, "r", encoding="utf-8") as f:
        data = json.load(f)


    datamaps = data.get("datamaps", [])

    manager_functions = []
    manager_constructors = []
    service_functions = []
    service_interface_functions = []

    for clazz in datamaps:
        class_name = clazz['class_name']
        for field in clazz["fields"]:
            if field['isFunction']: 
                name = field['fieldName']
                name = name.replace("::", "_")
                hash = field['functionHash']
                manager_functions.append(format_template(MANAGER_FUNCTION_FIELDS_TEMPLATE, className=class_name, functionName=name, functionHash=hash))
                manager_constructors.append(format_template(MANAGER_FUNCTION_CONSTRUCTOR_TEMPLATE, functionName=name, functionHash=hash))
                service_functions.append(format_template(SERVICE_FUNCTION_TEMPLATE, className=class_name, functionName=name))
                service_interface_functions.append(format_template(SERVICE_INTERFACE_FUNCTION_TEMPLATE, className=class_name, functionName=name))
                hook_context_template = format_template(HOOK_CONTEXT_TEMPLATE, className=class_name, functionName=name)
                hook_context_interface_template = format_template(HOOK_CONTEXT_INTERFACE_TEMPLATE, className=class_name, functionName=name)

                f_hook_context = open(FOLDER / "Classes" / f"DHook{name}.cs", "w", encoding="utf-8")
                f_hook_context_interface = open(FOLDER / "Interfaces" / f"IDHook{name}.cs", "w", encoding="utf-8")
                f_hook_context.write(hook_context_template)
                f_hook_context_interface.write(hook_context_interface_template)
                f_hook_context.close()
                f_hook_context_interface.close()

    f_manager.write(format_template(MANAGER_TEMPLATE, fields="\n".join(manager_functions), constructors="\n".join(manager_constructors)))
    f_service.write(format_template(SERVICE_TEMPLATE, functions="\n".join(service_functions)))
    f_service_interface.write(format_template(SERVICE_INTERFACE_TEMPLATE, functions="\n".join(service_interface_functions)))

    f_manager.close()
    f_service.close()
    f_service_interface.close()

if __name__ == "__main__":
    main()