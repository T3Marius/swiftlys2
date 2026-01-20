/************************************************************************************************
 *  SwiftlyS2 is a scripting framework for Source2-based games.
 *  Copyright (C) 2023-2026 Swiftly Solution SRL via Sava Andrei-Sebastian and it's contributors
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with this program.  If not, see <https://www.gnu.org/licenses/>.
 ************************************************************************************************/

#include "schema.h"
#include "datamap.h"

#include <fmt/format.h>

static bool IsFieldNetworked(SchemaClassFieldData_t& field)
{
    static auto networkEnabled = hash_32_fnv1a_const("MNetworkEnable");
    for (int i = 0; i < field.m_nStaticMetadataCount; i++)
        if (networkEnabled == hash_32_fnv1a_const(field.m_pStaticMetadata[i].m_pszName))
            return true;

    return false;
}

bool IsStandardLayoutClass(SchemaClassInfoData_t* classData) {
    {
        auto pClass = classData;
        int classesWithFields = 0;
        do {
            classesWithFields += ((pClass->m_nSize > 1) || (pClass->m_nFieldCount != 0)) ? 1 : 0;

            if (classesWithFields > 1) return false;

            pClass = (pClass->m_pBaseClasses == nullptr) ? nullptr : pClass->m_pBaseClasses->m_pClass;
        } while (pClass != nullptr);
    }

    auto fields = classData->m_pFields;
    auto fieldsCount = classData->m_nFieldCount;
    for (uint16_t i = 0; i < fieldsCount; i++) {
        auto fieldType = fields[i].m_pType;
        if (fieldType->m_eTypeCategory == SchemaTypeCategory_t::SCHEMA_TYPE_DECLARED_CLASS) {
            CSchemaType_DeclaredClass* fClass = reinterpret_cast<CSchemaType_DeclaredClass*>(fieldType);
            if (fClass->m_pClassInfo && !IsStandardLayoutClass(fClass->m_pClassInfo)) return false;
        }
    }

    return true;
}

std::string GetBuiltinTypeName(CSchemaType_Builtin* pType)
{
    switch (pType->m_eBuiltinType)
    {
    case SCHEMA_BUILTIN_TYPE_VOID: return "void";
    case SCHEMA_BUILTIN_TYPE_CHAR: return "char";
    case SCHEMA_BUILTIN_TYPE_INT8: return "int8";
    case SCHEMA_BUILTIN_TYPE_UINT8: return "uint8";
    case SCHEMA_BUILTIN_TYPE_INT16: return "int16";
    case SCHEMA_BUILTIN_TYPE_UINT16: return "uint16";
    case SCHEMA_BUILTIN_TYPE_INT32: return "int32";
    case SCHEMA_BUILTIN_TYPE_UINT32: return "uint32";
    case SCHEMA_BUILTIN_TYPE_INT64: return "int64";
    case SCHEMA_BUILTIN_TYPE_UINT64: return "uint64";
    case SCHEMA_BUILTIN_TYPE_FLOAT32: return "float32";
    case SCHEMA_BUILTIN_TYPE_FLOAT64: return "float64";
    case SCHEMA_BUILTIN_TYPE_BOOL: return "bool";
    default: return "";
    }
}

std::string ReadFieldType(CSchemaType* field)
{

    if (field->IsA<CSchemaType_Builtin>())
    {
        return GetBuiltinTypeName(field->ReinterpretAs<CSchemaType_Builtin>());
    }
    else if (field->IsA<CSchemaType_DeclaredClass>())
    {
        auto classInfo = field->ReinterpretAs<CSchemaType_DeclaredClass>()->m_pClassInfo;
        if (classInfo)
        {
            return classInfo->m_pszName;
        }
        else
        {
            return field->m_sTypeName.Get();
        }
    }
    else if (field->IsA<CSchemaType_DeclaredEnum>())
    {
        return field->ReinterpretAs<CSchemaType_DeclaredEnum>()->m_pEnumInfo->m_pszName;
    }
    else if (field->IsA<CSchemaType_Ptr>())
    {
        return ReadFieldType(field->ReinterpretAs<CSchemaType_Ptr>()->GetInnerType().Get());
    }
    else if (field->IsA<CSchemaType_Bitfield>())
    {
        return "bitfield";
    }
    else if (field->IsA<CSchemaType_FixedArray>())
    {
        auto fixed_array = field->ReinterpretAs<CSchemaType_FixedArray>();
        return fmt::format("{}[{}]", ReadFieldType(fixed_array->m_pElementType), fixed_array->m_nElementCount);
    }
    else if (field->IsA<CSchemaType_Atomic>())
    {
        return field->m_sTypeName.Get();
    }
    else return field->m_sTypeName.Get();
}

void FindChainer(bool& has_chainer, int& chainer_offset, CSchemaClassInfo* classInfo)
{
    for (int i = 0; i < classInfo->m_nBaseClassCount; i++)
    {
        auto baseClass = classInfo->m_pBaseClasses[i].m_pClass;
        if (baseClass)
        {
            for (int j = 0; j < baseClass->m_nFieldCount; j++)
            {
                if (baseClass->m_pFields[j].m_pszName == std::string("__m_pChainEntity"))
                {
                    has_chainer = true;
                    chainer_offset = baseClass->m_pFields[j].m_nSingleInheritanceOffset;
                    break;
                }
            }
        }
        if (has_chainer) break;
    }

    if (!has_chainer)
    {
        for (int i = 0; i < classInfo->m_nBaseClassCount; i++)
        {
            auto baseClass = classInfo->m_pBaseClasses[i].m_pClass;
            if (baseClass)
            {
                FindChainer(has_chainer, chainer_offset, baseClass);
                if (has_chainer) break;
            }
        }
    }
}

std::string GetDatamapFieldTypeName(fieldtype_t type)
{
    switch (type)
    {
    case FIELD_VOID: return "FIELD_VOID";
    case FIELD_FLOAT32: return "FIELD_FLOAT32";
    case FIELD_STRING: return "FIELD_STRING";
    case FIELD_VECTOR: return "FIELD_VECTOR";
    case FIELD_QUATERNION: return "FIELD_QUATERNION";
    case FIELD_INT32: return "FIELD_INT32";
    case FIELD_BOOLEAN: return "FIELD_BOOLEAN";
    case FIELD_INT16: return "FIELD_INT16";
    case FIELD_CHARACTER: return "FIELD_CHARACTER";
    case FIELD_COLOR32: return "FIELD_COLOR32";
    case FIELD_EMBEDDED: return "FIELD_EMBEDDED";
    case FIELD_CUSTOM: return "FIELD_CUSTOM";
    case FIELD_CLASSPTR: return "FIELD_CLASSPTR";
    case FIELD_EHANDLE: return "FIELD_EHANDLE";
    case FIELD_POSITION_VECTOR: return "FIELD_POSITION_VECTOR";
    case FIELD_TIME: return "FIELD_TIME";
    case FIELD_TICK: return "FIELD_TICK";
    case FIELD_SOUNDNAME: return "FIELD_SOUNDNAME";
    case FIELD_INPUT: return "FIELD_INPUT";
    case FIELD_FUNCTION: return "FIELD_FUNCTION";
    case FIELD_VMATRIX: return "FIELD_VMATRIX";
    case FIELD_VMATRIX_WORLDSPACE: return "FIELD_VMATRIX_WORLDSPACE";
    case FIELD_MATRIX3X4_WORLDSPACE: return "FIELD_MATRIX3X4_WORLDSPACE";
    case FIELD_INTERVAL: return "FIELD_INTERVAL";
    case FIELD_UNUSED: return "FIELD_UNUSED";
    case FIELD_VECTOR2D: return "FIELD_VECTOR2D";
    case FIELD_INT64: return "FIELD_INT64";
    case FIELD_VECTOR4D: return "FIELD_VECTOR4D";
    case FIELD_RESOURCE: return "FIELD_RESOURCE";
    case FIELD_TYPEUNKNOWN: return "FIELD_TYPEUNKNOWN";
    case FIELD_CSTRING: return "FIELD_CSTRING";
    case FIELD_HSCRIPT: return "FIELD_HSCRIPT";
    case FIELD_VARIANT: return "FIELD_VARIANT";
    case FIELD_UINT64: return "FIELD_UINT64";
    case FIELD_FLOAT64: return "FIELD_FLOAT64";
    case FIELD_POSITIVEINTEGER_OR_NULL: return "FIELD_POSITIVEINTEGER_OR_NULL";
    case FIELD_HSCRIPT_NEW_INSTANCE: return "FIELD_HSCRIPT_NEW_INSTANCE";
    case FIELD_UINT32: return "FIELD_UINT32";
    case FIELD_UTLSTRINGTOKEN: return "FIELD_UTLSTRINGTOKEN";
    case FIELD_QANGLE: return "FIELD_QANGLE";
    case FIELD_NETWORK_ORIGIN_CELL_QUANTIZED_VECTOR: return "FIELD_NETWORK_ORIGIN_CELL_QUANTIZED_VECTOR";
    case FIELD_HMATERIAL: return "FIELD_HMATERIAL";
    case FIELD_HMODEL: return "FIELD_HMODEL";
    case FIELD_NETWORK_QUANTIZED_VECTOR: return "FIELD_NETWORK_QUANTIZED_VECTOR";
    case FIELD_NETWORK_QUANTIZED_FLOAT: return "FIELD_NETWORK_QUANTIZED_FLOAT";
    case FIELD_DIRECTION_VECTOR_WORLDSPACE: return "FIELD_DIRECTION_VECTOR_WORLDSPACE";
    case FIELD_QANGLE_WORLDSPACE: return "FIELD_QANGLE_WORLDSPACE";
    case FIELD_QUATERNION_WORLDSPACE: return "FIELD_QUATERNION_WORLDSPACE";
    case FIELD_HSCRIPT_LIGHTBINDING: return "FIELD_HSCRIPT_LIGHTBINDING";
    case FIELD_V8_VALUE: return "FIELD_V8_VALUE";
    case FIELD_V8_OBJECT: return "FIELD_V8_OBJECT";
    case FIELD_V8_ARRAY: return "FIELD_V8_ARRAY";
    case FIELD_V8_CALLBACK_INFO: return "FIELD_V8_CALLBACK_INFO";
    case FIELD_UTLSTRING: return "FIELD_UTLSTRING";
    case FIELD_NETWORK_ORIGIN_CELL_QUANTIZED_POSITION_VECTOR: return "FIELD_NETWORK_ORIGIN_CELL_QUANTIZED_POSITION_VECTOR";
    case FIELD_HRENDERTEXTURE: return "FIELD_HRENDERTEXTURE";
    case FIELD_HPARTICLESYSTEMDEFINITION: return "FIELD_HPARTICLESYSTEMDEFINITION";
    case FIELD_UINT8: return "FIELD_UINT8";
    case FIELD_UINT16: return "FIELD_UINT16";
    case FIELD_CTRANSFORM: return "FIELD_CTRANSFORM";
    case FIELD_CTRANSFORM_WORLDSPACE: return "FIELD_CTRANSFORM_WORLDSPACE";
    case FIELD_HPOSTPROCESSING: return "FIELD_HPOSTPROCESSING";
    case FIELD_MATRIX3X4: return "FIELD_MATRIX3X4";
    case FIELD_SHIM: return "FIELD_SHIM";
    case FIELD_CMOTIONTRANSFORM: return "FIELD_CMOTIONTRANSFORM";
    case FIELD_CMOTIONTRANSFORM_WORLDSPACE: return "FIELD_CMOTIONTRANSFORM_WORLDSPACE";
    case FIELD_ATTACHMENT_HANDLE: return "FIELD_ATTACHMENT_HANDLE";
    case FIELD_AMMO_INDEX: return "FIELD_AMMO_INDEX";
    case FIELD_CONDITION_ID: return "FIELD_CONDITION_ID";
    case FIELD_AI_SCHEDULE_BITS: return "FIELD_AI_SCHEDULE_BITS";
    case FIELD_MODIFIER_HANDLE: return "FIELD_MODIFIER_HANDLE";
    case FIELD_ROTATION_VECTOR: return "FIELD_ROTATION_VECTOR";
    case FIELD_ROTATION_VECTOR_WORLDSPACE: return "FIELD_ROTATION_VECTOR_WORLDSPACE";
    case FIELD_HVDATA: return "FIELD_HVDATA";
    case FIELD_SCALE32: return "FIELD_SCALE32";
    case FIELD_STRING_AND_TOKEN: return "FIELD_STRING_AND_TOKEN";
    case FIELD_ENGINE_TIME: return "FIELD_ENGINE_TIME";
    case FIELD_ENGINE_TICK: return "FIELD_ENGINE_TICK";
    case FIELD_WORLD_GROUP_ID: return "FIELD_WORLD_GROUP_ID";
    case FIELD_GLOBALSYMBOL: return "FIELD_GLOBALSYMBOL";
    case FIELD_HNMGRAPHDEFINITION: return "FIELD_HNMGRAPHDEFINITION";
    case FIELD_TYPECOUNT: return "FIELD_TYPECOUNT";
    }

    return "FIELD_TYPEUNKNOWN";
}

void CollectDatamapFields(datamap_t* map, json& fields)
{
    if (!map) return;

    // CollectDatamapFields(map->baseMap, fields);

    if (!map->dataDesc || map->dataNumFields <= 0) return;

    for (int i = 0; i < map->dataNumFields; i++)
    {
        auto& desc = map->dataDesc[i];

        std::string fieldName = desc.fieldName ? desc.fieldName : "";
        std::string externalName = (desc.externalName && desc.externalName[0]) ? desc.externalName : "";

        bool isFunction = (desc.flags & FTYPEDESC_FUNCTIONTABLE) != 0;
        bool isInput = (desc.flags & FTYPEDESC_WAS_INPUT) != 0;
        bool isOutput = (desc.flags & FTYPEDESC_WAS_OUTPUT) != 0;

        uint32_t function_hash = 0;

        if (isFunction && fieldName != "") {
            function_hash = hash_32_fnv1a_const(fieldName.c_str());
            datamapFunctions.insert({ function_hash , &desc.inputFunc });
        }

        if (
            !isFunction && !isInput && !isOutput
            && (fieldName == "" || externalName == "")
        ) {
            continue;
        }

        fields.push_back({
            {"fieldType", GetDatamapFieldTypeName(desc.fieldType)},
            {"fieldName", fieldName},
            {"externalName", externalName},
            {"isFunction", isFunction},
            {"isInput", isInput},
            {"isOutput", isOutput},
            {"functionHash", function_hash},
        });
    }
}

void ReadClassDatamap(CSchemaType_DeclaredClass* declClass, json& outJson)
{
    auto classInfo = declClass->m_pClassInfo;
    if (!classInfo) return;

    auto map = classInfo->m_pDataDescMap;
    if (!map) return;

    std::string baseMapName = "";
    if (map->baseMap) {
        baseMapName = map->baseMap->dataClassName ? map->baseMap->dataClassName : "";
    }

    if (!outJson.contains("datamaps"))
        outJson["datamaps"] = json::array();

    outJson["datamaps"].push_back({
        {"class_name", classInfo->m_pszName},
        {"data_class_name", map->dataClassName ? map->dataClassName : classInfo->m_pszName},
        {"base_data_class_name", baseMapName},
        });

    auto& datamap = outJson["datamaps"].back();
    datamap["fields"] = json::array();

    CollectDatamapFields(map, datamap["fields"]);
}

void ReadClasses(CSchemaType_DeclaredClass* declClass, json& outJson)
{
    auto classInfo = declClass->m_pClassInfo;

    if (!classInfo)
    {
        return;
    }
    uint32_t class_hash = hash_32_fnv1a_const(classInfo->m_pszName);
    bool isStruct = IsStandardLayoutClass(classInfo);

    outJson["classes"].push_back({
        {"name", classInfo->m_pszName},
        {"name_hash", class_hash},
        {"is_struct", isStruct},
        {"project", classInfo->m_pszProjectName ? classInfo->m_pszProjectName : "default"},
        {"alignment", classInfo->m_nAlignment},
        {"size", classInfo->m_nSize},
        {"fields_count", classInfo->m_nFieldCount},
        });

    classes.insert({ class_hash, {isStruct, (uint32_t)classInfo->m_nSize, (uint32_t)classInfo->m_nAlignment, class_hash} });

    auto& cls = outJson["classes"].back();

    if (classInfo->m_nBaseClassCount) {
        cls["base_classes_count"] = classInfo->m_nBaseClassCount;
        for (int i = 0; i < classInfo->m_nBaseClassCount; i++) {
            cls["base_classes"].push_back(classInfo->m_pBaseClasses[i].m_pClass->m_pszName);
        }
    }

    auto field_size = classInfo->m_nFieldCount;
    auto fields = classInfo->m_pFields;

    bool has_chainer = false;
    int chainer_offset = 0;

    for (int i = 0; i < field_size; i++)
    {
        if (fields[i].m_pszName == std::string("__m_pChainEntity"))
        {
            has_chainer = true;
            chainer_offset = fields[i].m_nSingleInheritanceOffset;
            break;
        }
    }

    if (!has_chainer)
    {
        FindChainer(has_chainer, chainer_offset, classInfo);
    }

    cls["has_chainer"] = has_chainer;

    for (int i = 0; i < field_size; i++)
    {
        auto field = fields[i];
        uint64_t fieldHash = ((uint64_t)(class_hash) << 32 | hash_32_fnv1a_const(field.m_pszName));

        offsets.insert({ fieldHash, { IsFieldNetworked(field), has_chainer, isStruct, (uint32_t)field.m_nSingleInheritanceOffset, chainer_offset } });

        int size;
        uint8_t alignment;

        field.m_pType->GetSizeAndAlignment(size, alignment);

        cls["fields"].push_back({
            {"name", field.m_pszName},
            {"name_hash", fieldHash},
            {"networked", IsFieldNetworked(field)},
            {"offset", field.m_nSingleInheritanceOffset},
            {"size", size},
            {"alignment", alignment},
            });

        auto& lastField = cls["fields"].back();

        switch (field.m_pType->m_eTypeCategory)
        {
        case SCHEMA_TYPE_BUILTIN:
        case SCHEMA_TYPE_DECLARED_ENUM:
        case SCHEMA_TYPE_DECLARED_CLASS:
        {
            lastField["kind"] = "ref";
            lastField["type"] = ReadFieldType(field.m_pType);
            break;
        }
        case SCHEMA_TYPE_ATOMIC:
        {
            lastField["kind"] = "atomic";
            lastField["type"] = explode(ReadFieldType(field.m_pType), "<")[0];
            lastField["templated"] = ReadFieldType(field.m_pType);

            switch (field.m_pType->m_eAtomicCategory)
            {
            case SCHEMA_ATOMIC_T:
            {
                auto atomic = field.m_pType->ReinterpretAs<CSchemaType_Atomic_T>();
                lastField["template"].push_back(ReadFieldType(atomic->m_pTemplateType));
                break;
            }
            case SCHEMA_ATOMIC_TT:
            {
                auto atomic = field.m_pType->ReinterpretAs<CSchemaType_Atomic_TT>();
                lastField["template"].push_back(ReadFieldType(atomic->m_pTemplateType));
                lastField["template"].push_back(ReadFieldType(atomic->m_pTemplateType2));
                break;
            }
            case SCHEMA_ATOMIC_COLLECTION_OF_T:
            {
                auto atomic = field.m_pType->ReinterpretAs<CSchemaType_Atomic_CollectionOfT>();
                lastField["template"].push_back(ReadFieldType(atomic->m_pTemplateType));
                if (atomic->m_nFixedBufferCount > 0) {
                    lastField["template"].push_back({
                        {"type", "literal"},
                        {"value", atomic->m_nFixedBufferCount}
                        });
                }
                break;
            }
            case SCHEMA_ATOMIC_I:
            {
                auto atomic = field.m_pType->ReinterpretAs<CSchemaType_Atomic_I>();
                lastField["template"].push_back({
                    {"type", "literal"},
                    {"value", atomic->m_nInteger}
                    });
                break;
            }
            }

            break;
        }
        case SCHEMA_TYPE_POINTER:
        {
            lastField["kind"] = "ptr";
            lastField["type"] = ReadFieldType(field.m_pType);

            break;
        }
        case SCHEMA_TYPE_BITFIELD:
        {
            auto bitfield = field.m_pType->ReinterpretAs<CSchemaType_Bitfield>();
            lastField["kind"] = "bitfield";
            lastField["type"] = ReadFieldType(field.m_pType);
            lastField["count"] = bitfield->m_nBitfieldCount;
            break;
        }
        case SCHEMA_TYPE_FIXED_ARRAY:
        {
            auto fixed_array = field.m_pType->ReinterpretAs<CSchemaType_FixedArray>();
            lastField["kind"] = "fixed_array";
            lastField["type"] = ReadFieldType(fixed_array->m_pElementType);
            lastField["element_size"] = fixed_array->m_nElementSize;
            lastField["element_count"] = fixed_array->m_nElementCount;
            lastField["element_alignment"] = fixed_array->m_nElementAlignment;
            break;
        }
        }
    }
}

void ReadEnums(CSchemaType_DeclaredEnum* declClass, json& outJson)
{
    auto enumInfo = declClass->m_pEnumInfo;

    outJson["enums"].push_back({
        {"name", enumInfo->m_pszName},
        {"project", enumInfo->m_pszProjectName ? enumInfo->m_pszProjectName : "default"},
        {"alignment", enumInfo->m_nAlignment},
        {"size", enumInfo->m_nSize},
        {"fields_count", enumInfo->m_nEnumeratorCount},
        });

    auto& enm = outJson["enums"].back();

    auto field_size = enumInfo->m_nEnumeratorCount;
    auto fields = enumInfo->m_pEnumerators;

    for (int i = 0; i < field_size; i++)
    {
        auto field = fields[i];
        enm["fields"].push_back({
            {"name", field.m_pszName},
            {"value", field.m_nValue},
            });
    }
}