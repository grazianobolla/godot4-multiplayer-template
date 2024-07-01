using Godot;
using NetMessage;

public static class PlayerMovement
{
    public static readonly float FrameDelta = 1.0f / Engine.PhysicsTicksPerSecond;

    public static Vector3 ComputeMotion(Transform3D from, Vector3 velocity, Vector2 input)
    {
        Vector3 direction = new Vector3(input.X, 0, input.Y).Normalized();

        if (direction != Vector3.Zero)
        {
            velocity.X = direction.X * 5;
            velocity.Z = direction.Z * 5;
        }
        else
        {
            velocity *= 0.99f;
        }

        return velocity;
    }

    public static Vector2 InputToDirection(byte input)
    {
        Vector2 direction = Vector2.Zero;

        if ((input & (byte)InputFlags.Right) > 0) direction.X += 1;
        if ((input & (byte)InputFlags.Left) > 0) direction.X -= 1;
        if ((input & (byte)InputFlags.Backward) > 0) direction.Y += 1;
        if ((input & (byte)InputFlags.Forward) > 0) direction.Y -= 1;

        return direction.Normalized();
    }
}