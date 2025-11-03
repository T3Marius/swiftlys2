using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SwiftlyS2.Core.Natives;
using SwiftlyS2.Core.Services;
using SwiftlyS2.Shared.Services;
using Tomlyn;
using Tomlyn.Model;

namespace SwiftlyS2.Core.Services;

internal static class TomlModelHelper
{
  [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Configuration models are preserved at runtime")]
  public static TomlTable ObjectToTomlTable(object obj)
  {
    var table = new TomlTable();
    var properties = obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

    foreach (var prop in properties)
    {
      var value = prop.GetValue(obj);
      if (value == null)
        continue;

      var propName = prop.Name;

      // Convert property value to TOML-compatible type
      if (IsSimpleType(prop.PropertyType))
      {
        table[propName] = value;
      }
      else if (IsGenericDictionary(prop.PropertyType))
      {
        // Handle Dictionary<string, TValue>
        var dictTable = new TomlTable();
        if (value is System.Collections.IDictionary dictionary)
        {
          foreach (System.Collections.DictionaryEntry entry in dictionary)
          {
            if (entry.Key != null && entry.Value != null)
            {
              var key = entry.Key.ToString();
              if (key != null)
              {
                if (IsSimpleType(entry.Value.GetType()))
                {
                  dictTable[key] = entry.Value;
                }
                else
                {
                  dictTable[key] = ObjectToTomlTable(entry.Value);
                }
              }
            }
          }
        }
        table[propName] = dictTable;
      }
      else if (prop.PropertyType.IsArray || IsGenericList(prop.PropertyType))
      {
        // Arrays or lists
        var array = new TomlArray();
        if (value is System.Collections.IEnumerable enumerable)
        {
          foreach (var item in enumerable)
          {
            if (item != null)
            {
              if (IsSimpleType(item.GetType()))
              {
                array.Add(item);
              }
              else
              {
                array.Add(ObjectToTomlTable(item));
              }
            }
          }
        }
        table[propName] = array;
      }
      else if (prop.PropertyType.IsClass && !prop.PropertyType.IsArray)
      {
        // Nested object
        table[propName] = ObjectToTomlTable(value);
      }
    }

    return table;
  }

  private static bool IsSimpleType(Type type)
  {
    return type.IsPrimitive
           || type.IsEnum
           || type == typeof(string)
           || type == typeof(decimal)
           || type == typeof(DateTime)
           || type == typeof(DateTimeOffset)
           || type == typeof(TimeSpan)
           || type == typeof(Guid);
  }

  private static bool IsGenericList(Type type)
  {
    if (!type.IsGenericType)
      return false;

    var genericTypeDef = type.GetGenericTypeDefinition();
    return genericTypeDef == typeof(List<>)
           || genericTypeDef == typeof(IList<>)
           || genericTypeDef == typeof(ICollection<>)
           || genericTypeDef == typeof(IEnumerable<>);
  }

  private static bool IsGenericDictionary(Type type)
  {
    if (!type.IsGenericType)
      return false;

    var genericTypeDef = type.GetGenericTypeDefinition();
    return genericTypeDef == typeof(Dictionary<,>)
           || genericTypeDef == typeof(IDictionary<,>);
  }
}

internal class PluginConfigurationService : IPluginConfigurationService
{

  private ConfigurationService _ConfigurationService { get; init; }
  private CoreContext _Id { get; init; }
  private IConfigurationManager? _Manager { get; set; }

  public bool BasePathExists
  {
    get => Path.Exists(BasePath);
  }

  public PluginConfigurationService(CoreContext id, ConfigurationService configurationService)
  {
    _Id = id;
    _ConfigurationService = configurationService;
  }

  public string BasePath => Path.Combine(_ConfigurationService.GetConfigRoot(), "plugins", _Id.Name);

  public string GetRoot()
  {
    var dir = Path.Combine(_ConfigurationService.GetConfigRoot(), "plugins", _Id.Name);
    if (!Directory.Exists(dir))
    {
      Directory.CreateDirectory(dir);
    }
    return dir;
  }

  public string GetConfigPath(string name)
  {
    return Path.Combine(GetRoot(), name);
  }

  public IPluginConfigurationService InitializeWithTemplate(string name, string templatePath)
  {

    var configPath = GetConfigPath(name);

    if (File.Exists(configPath))
    {
      return this;
    }

    var dir = Path.GetDirectoryName(configPath);
    if (dir is not null)
    {
      Directory.CreateDirectory(dir);
    }
    File.Create(configPath).Close();

    var templateAbsPath = Path.Combine(_Id.BaseDirectory, "resources", "templates", templatePath);

    if (!File.Exists(templateAbsPath))
    {
      throw new FileNotFoundException($"Template file not found: {templateAbsPath}");
    }

    File.Copy(templateAbsPath, configPath);
    return this;
  }

  public IPluginConfigurationService InitializeJsonWithModel<T>(string name, string sectionName) where T : class, new()
  {

    var configPath = GetConfigPath(name);

    if (File.Exists(configPath))
    {
      return this;
    }

    var dir = Path.GetDirectoryName(configPath);
    if (dir is not null)
    {
      Directory.CreateDirectory(dir);
    }

    var config = new T();

    var wrapped = new Dictionary<string, object?>
    {
      [sectionName] = config
    };

    var options = new JsonSerializerOptions
    {
      WriteIndented = true,
      IncludeFields = true,
      PropertyNamingPolicy = null
    };

    var configJson = JsonSerializer.Serialize(wrapped, options);
    File.WriteAllText(configPath, configJson);

    return this;
  }

  public IPluginConfigurationService InitializeTomlWithModel<T>(string name, string sectionName) where T : class, new()
  {

    var configPath = GetConfigPath(name);

    if (File.Exists(configPath))
    {
      return this;
    }

    var dir = Path.GetDirectoryName(configPath);
    if (dir is not null)
    {
      Directory.CreateDirectory(dir);
    }

    var config = new T();

    // Create a TomlTable and add the section
    var tomlTable = new TomlTable();
    var sectionTable = TomlModelHelper.ObjectToTomlTable(config);
    tomlTable[sectionName] = sectionTable;

    var tomlString = Toml.FromModel(tomlTable);
    File.WriteAllText(configPath, tomlString);

    return this;
  }

  public IPluginConfigurationService Configure(Action<IConfigurationBuilder> configure)
  {
    configure(Manager);
    return this;
  }

  public IConfigurationManager Manager
  {
    get
    {
      if (!BasePathExists)
      {
        throw new Exception("Base path does not exist in file system. Please call InitializeWithTemplate, InitializeJsonWithModel or InitializeTomlWithModel before using the Manager.");
      }
      if (_Manager is null)
      {
        _Manager = new ConfigurationManager();
        _Manager.SetBasePath(BasePath);
      }
      return _Manager;
    }
  }
}
