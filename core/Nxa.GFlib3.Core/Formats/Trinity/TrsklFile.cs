// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using Nxa.GFlib3.Core.FlatBuffers;

namespace Nxa.GFlib3.Core.Formats.Trinity;

/// <summary>TRSKL: transform-node hierarchy, joints, and helper bones.</summary>
public sealed class TrsklFile
{
    private TrsklFile(
        uint version,
        TrTransformNode[] transformNodes,
        TrJointInfo[] jointInfos,
        TrHelperBoneInfo[] helperBones,
        uint skinningPaletteOffset,
        bool isInteriorMap)
    {
        Version = version;
        TransformNodes = transformNodes;
        JointInfos = jointInfos;
        HelperBones = helperBones;
        SkinningPaletteOffset = skinningPaletteOffset;
        IsInteriorMap = isInteriorMap;
    }

    public uint Version { get; }

    public IReadOnlyList<TrTransformNode> TransformNodes { get; }

    public IReadOnlyList<TrJointInfo> JointInfos { get; }

    public IReadOnlyList<TrHelperBoneInfo> HelperBones { get; }

    public uint SkinningPaletteOffset { get; }

    public bool IsInteriorMap { get; }

    public static TrsklFile Parse(FlatBufferTable root)
    {
        return new TrsklFile(
            root.GetUInt32(0),
            TrinityParse.TableVector(root, 1, ParseTransformNode),
            TrinityParse.TableVector(root, 2, ParseJointInfo),
            TrinityParse.TableVector(root, 3, ParseHelperBone),
            root.GetUInt32(4),
            root.GetBool(5));
    }

    private static TrTransformNode ParseTransformNode(FlatBufferTable table)
    {
        FlatBufferTable? transform = table.GetTable(1);

        return new TrTransformNode(
            table.GetString(0),
            transform is null ? default : TrinityParse.Srt(transform.Value),
            table.GetVector3f(2),
            table.GetVector3f(3),
            table.GetInt32(4, -1),
            table.GetInt32(5, -1),
            table.GetString(6),
            (TrNodeType)table.GetUInt32(7),
            table.GetBool(8),
            table.GetBool(9));
    }

    private static TrJointInfo ParseJointInfo(FlatBufferTable table)
    {
        FlatBufferTable? inverseBind = table.GetTable(2);

        return new TrJointInfo(
            table.GetBool(0),
            table.GetBool(1),
            inverseBind is null ? default : ParseMatrix4x3(inverseBind.Value));
    }

    private static Matrix4x3 ParseMatrix4x3(FlatBufferTable table)
    {
        return new Matrix4x3(
            table.GetVector3f(0),
            table.GetVector3f(1),
            table.GetVector3f(2),
            table.GetVector3f(3));
    }

    private static TrHelperBoneInfo ParseHelperBone(FlatBufferTable table)
    {
        return new TrHelperBoneInfo(
            table.GetString(0),
            table.GetString(1),
            table.GetString(2),
            table.GetString(3),
            table.GetUInt32(4),
            table.GetVector3f(5),
            table.GetVector4f(6));
    }
}

public sealed class TrTransformNode
{
    internal TrTransformNode(
        string? name,
        Srt transform,
        Vector3f scalePivot,
        Vector3f rotatePivot,
        int parentNodeIndex,
        int jointInfoIndex,
        string? parentNodeName,
        TrNodeType nodeType,
        bool priorityPass,
        bool ignoreParentRotation)
    {
        Name = name;
        Transform = transform;
        ScalePivot = scalePivot;
        RotatePivot = rotatePivot;
        ParentNodeIndex = parentNodeIndex;
        JointInfoIndex = jointInfoIndex;
        ParentNodeName = parentNodeName;
        NodeType = nodeType;
        PriorityPass = priorityPass;
        IgnoreParentRotation = ignoreParentRotation;
    }

    public string? Name { get; }

    public Srt Transform { get; }

    public Vector3f ScalePivot { get; }

    public Vector3f RotatePivot { get; }

    public int ParentNodeIndex { get; }

    public int JointInfoIndex { get; }

    public string? ParentNodeName { get; }

    public TrNodeType NodeType { get; }

    public bool PriorityPass { get; }

    public bool IgnoreParentRotation { get; }
}

public sealed class TrJointInfo
{
    internal TrJointInfo(bool segmentScaleCompensate, bool influenceSkinning, Matrix4x3 inverseBindPose)
    {
        SegmentScaleCompensate = segmentScaleCompensate;
        InfluenceSkinning = influenceSkinning;
        InverseBindPose = inverseBindPose;
    }

    public bool SegmentScaleCompensate { get; }

    public bool InfluenceSkinning { get; }

    public Matrix4x3 InverseBindPose { get; }
}

public sealed class TrHelperBoneInfo
{
    internal TrHelperBoneInfo(
        string? output,
        string? target,
        string? reference,
        string? type,
        uint reserved,
        Vector3f weight,
        Vector4f adjust)
    {
        Output = output;
        Target = target;
        Reference = reference;
        Type = type;
        Reserved = reserved;
        Weight = weight;
        Adjust = adjust;
    }

    public string? Output { get; }

    public string? Target { get; }

    public string? Reference { get; }

    public string? Type { get; }

    public uint Reserved { get; }

    public Vector3f Weight { get; }

    public Vector4f Adjust { get; }
}
