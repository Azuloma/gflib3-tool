// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Numerics;

namespace Nxa.GFlib3.Render.Camera;

/// <summary>Y-up free-flight camera with WASD and mouse look.</summary>
public sealed class FreeFlyCamera
{
    private const float MinPitch = -1.55f;
    private const float MaxPitch = 1.55f;
    private const float LookSensitivity = 0.0032f;

    public const float FieldOfViewRadians = MathF.PI / 3f;

    public Vector3 Position { get; set; } = new(0, 10, 30);

    public float Yaw { get; set; } = MathF.PI;

    public float Pitch { get; set; }

    public float BaseSpeed { get; set; } = 18f;

    public float SprintMultiplier { get; set; } = 4f;

    public Vector3 Forward => new(
        MathF.Sin(Yaw) * MathF.Cos(Pitch),
        MathF.Sin(Pitch),
        MathF.Cos(Yaw) * MathF.Cos(Pitch));

    public Vector3 Right => Vector3.Normalize(Vector3.Cross(Forward, Vector3.UnitY));

    public Matrix4x4 View => Matrix4x4.CreateLookAt(Position, Position + Forward, Vector3.UnitY);

    public static Matrix4x4 Projection(float aspect, float farPlane)
    {
        return Matrix4x4.CreatePerspectiveFieldOfView(
            FieldOfViewRadians,
            MathF.Max(aspect, 0.0001f),
            0.1f,
            farPlane);
    }

    public void Look(float deltaX, float deltaY)
    {
        Yaw -= deltaX * LookSensitivity;
        Pitch = Math.Clamp(Pitch - deltaY * LookSensitivity, MinPitch, MaxPitch);
    }

    public void Move(Vector3 localDirection, float deltaSeconds, bool sprint)
    {
        if (localDirection.LengthSquared() < 0.0001f)
        {
            return;
        }

        Vector3 direction = Right * localDirection.X
            + Vector3.UnitY * localDirection.Y
            + Forward * localDirection.Z;
        float speed = BaseSpeed * (sprint ? SprintMultiplier : 1f);
        Position += Vector3.Normalize(direction) * speed * deltaSeconds;
    }

    public void Frame(Vector3 center, float radius)
    {
        float distance = MathF.Max(radius * 0.45f, 12f);
        Position = center + new Vector3(0, radius * 0.35f + 4f, distance);
        Yaw = MathF.PI;
        Pitch = -0.35f;
    }
}



