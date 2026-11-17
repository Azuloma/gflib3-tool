// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using Nxa.GFlib3.Core.FlatBuffers;

namespace Nxa.GFlib3.Core.Formats.Trinity;

/// <summary>TRMTR: material definitions, shader parameters, and texture references.</summary>
public sealed class TrmtrFile
{
    private TrmtrFile(TrmtrMaterial[] materials)
    {
        Materials = materials;
    }

    public IReadOnlyList<TrmtrMaterial> Materials { get; }

    public static TrmtrFile Parse(FlatBufferTable root)
    {
        return new TrmtrFile(TrinityParse.TableVector(root, 1, ParseMaterial));
    }

    private static TrmtrMaterial ParseMaterial(FlatBufferTable table)
    {
        FlatBufferTable? byteExtra = table.GetTable(13);
        FlatBufferTable? intExtra = table.GetTable(14);

        return new TrmtrMaterial(
            table.GetString(0),
            TrinityParse.TableVector(table, 1, ParseShader),
            TrinityParse.TableVector(table, 2, ParseTexture),
            TrinityParse.TableVector(table, 3, ParseSampler),
            TrinityParse.TableVector(table, 4, ParseFloatParameter),
            table.GetString(5),
            TrinityParse.TableVector(table, 6, ParseVec4Parameter),
            TrinityParse.TableVector(table, 7, ParseVec4Parameter),
            table.GetString(8),
            TrinityParse.TableVector(table, 9, ParseIntParameter),
            table.GetString(10),
            table.GetString(11),
            table.GetString(12),
            byteExtra is null ? null : new TrmtrByteExtra(byteExtra.Value.GetSByte(0, -1)),
            intExtra is null
                ? null
                : new TrmtrIntExtra(intExtra.Value.GetUInt32(0), intExtra.Value.GetInt32(1, -1)),
            table.GetString(15));
    }

    private static TrmtrShader ParseShader(FlatBufferTable table)
    {
        return new TrmtrShader(
            table.GetString(0),
            TrinityParse.TableVector(table, 1, ParseStringParameter));
    }

    private static TrmtrStringParameter ParseStringParameter(FlatBufferTable table)
    {
        return new TrmtrStringParameter(table.GetString(0), table.GetString(1));
    }

    private static TrmtrTexture ParseTexture(FlatBufferTable table)
    {
        return new TrmtrTexture(table.GetString(0), table.GetString(1), table.GetUInt32(2));
    }

    private static TrmtrSampler ParseSampler(FlatBufferTable table)
    {
        return new TrmtrSampler(
            table.GetUInt32(0),
            table.GetUInt32(1),
            table.GetUInt32(2),
            table.GetUInt32(3),
            table.GetFloat(4),
            table.GetFloat(5),
            table.GetFloat(6),
            table.GetFloat(7),
            table.GetFloat(8),
            (UVWrapMode)table.GetUInt32(9),
            (UVWrapMode)table.GetUInt32(10),
            (UVWrapMode)table.GetUInt32(11),
            table.GetVector4f(12));
    }

    private static TrmtrFloatParameter ParseFloatParameter(FlatBufferTable table)
    {
        return new TrmtrFloatParameter(table.GetString(0), table.GetFloat(1));
    }

    private static TrmtrVec4Parameter ParseVec4Parameter(FlatBufferTable table)
    {
        return new TrmtrVec4Parameter(table.GetString(0), table.GetVector4f(1));
    }

    private static TrmtrIntParameter ParseIntParameter(FlatBufferTable table)
    {
        return new TrmtrIntParameter(table.GetString(0), table.GetInt32(1, -1));
    }
}

public sealed class TrmtrMaterial
{
    internal TrmtrMaterial(
        string? name,
        IReadOnlyList<TrmtrShader> shaders,
        IReadOnlyList<TrmtrTexture> textures,
        IReadOnlyList<TrmtrSampler> samplers,
        IReadOnlyList<TrmtrFloatParameter> floatParameters,
        string? field05,
        IReadOnlyList<TrmtrVec4Parameter> lightParameters,
        IReadOnlyList<TrmtrVec4Parameter> vec4Parameters,
        string? field08,
        IReadOnlyList<TrmtrIntParameter> intParameters,
        string? field10,
        string? field11,
        string? field12,
        TrmtrByteExtra? byteExtra,
        TrmtrIntExtra? intExtra,
        string? alphaType)
    {
        Name = name;
        Shaders = shaders;
        Textures = textures;
        Samplers = samplers;
        FloatParameters = floatParameters;
        Field05 = field05;
        LightParameters = lightParameters;
        Vec4Parameters = vec4Parameters;
        Field08 = field08;
        IntParameters = intParameters;
        Field10 = field10;
        Field11 = field11;
        Field12 = field12;
        ByteExtra = byteExtra;
        IntExtra = intExtra;
        AlphaType = alphaType;
    }

    public string? Name { get; }

    public IReadOnlyList<TrmtrShader> Shaders { get; }

    public IReadOnlyList<TrmtrTexture> Textures { get; }

    public IReadOnlyList<TrmtrSampler> Samplers { get; }

    public IReadOnlyList<TrmtrFloatParameter> FloatParameters { get; }

    public string? Field05 { get; }

    public IReadOnlyList<TrmtrVec4Parameter> LightParameters { get; }

    public IReadOnlyList<TrmtrVec4Parameter> Vec4Parameters { get; }

    public string? Field08 { get; }

    public IReadOnlyList<TrmtrIntParameter> IntParameters { get; }

    public string? Field10 { get; }

    public string? Field11 { get; }

    public string? Field12 { get; }

    public TrmtrByteExtra? ByteExtra { get; }

    public TrmtrIntExtra? IntExtra { get; }

    public string? AlphaType { get; }
}

public sealed class TrmtrShader
{
    internal TrmtrShader(string? name, IReadOnlyList<TrmtrStringParameter> values)
    {
        Name = name;
        Values = values;
    }

    public string? Name { get; }

    public IReadOnlyList<TrmtrStringParameter> Values { get; }
}

public sealed class TrmtrStringParameter
{
    internal TrmtrStringParameter(string? name, string? value)
    {
        Name = name;
        Value = value;
    }

    public string? Name { get; }

    public string? Value { get; }
}

public sealed class TrmtrTexture
{
    internal TrmtrTexture(string? name, string? file, uint slot)
    {
        Name = name;
        File = file;
        Slot = slot;
    }

    public string? Name { get; }

    public string? File { get; }

    public uint Slot { get; }
}

public sealed class TrmtrSampler
{
    internal TrmtrSampler(
        uint state0,
        uint state1,
        uint state2,
        uint state3,
        float state4,
        float state5,
        float state6,
        float state7,
        float state8,
        UVWrapMode repeatU,
        UVWrapMode repeatV,
        UVWrapMode repeatW,
        Vector4f borderColor)
    {
        State0 = state0;
        State1 = state1;
        State2 = state2;
        State3 = state3;
        State4 = state4;
        State5 = state5;
        State6 = state6;
        State7 = state7;
        State8 = state8;
        RepeatU = repeatU;
        RepeatV = repeatV;
        RepeatW = repeatW;
        BorderColor = borderColor;
    }

    public uint State0 { get; }

    public uint State1 { get; }

    public uint State2 { get; }

    public uint State3 { get; }

    public float State4 { get; }

    public float State5 { get; }

    public float State6 { get; }

    public float State7 { get; }

    public float State8 { get; }

    public UVWrapMode RepeatU { get; }

    public UVWrapMode RepeatV { get; }

    public UVWrapMode RepeatW { get; }

    public Vector4f BorderColor { get; }
}

public sealed class TrmtrFloatParameter
{
    internal TrmtrFloatParameter(string? name, float value)
    {
        Name = name;
        Value = value;
    }

    public string? Name { get; }

    public float Value { get; }
}

public sealed class TrmtrVec4Parameter
{
    internal TrmtrVec4Parameter(string? name, Vector4f value)
    {
        Name = name;
        Value = value;
    }

    public string? Name { get; }

    public Vector4f Value { get; }
}

public sealed class TrmtrIntParameter
{
    internal TrmtrIntParameter(string? name, int value)
    {
        Name = name;
        Value = value;
    }

    public string? Name { get; }

    public int Value { get; }
}

public sealed class TrmtrByteExtra
{
    internal TrmtrByteExtra(sbyte value)
    {
        Value = value;
    }

    public sbyte Value { get; }
}

public sealed class TrmtrIntExtra
{
    internal TrmtrIntExtra(uint reserved, int value)
    {
        Reserved = reserved;
        Value = value;
    }

    public uint Reserved { get; }

    public int Value { get; }
}
