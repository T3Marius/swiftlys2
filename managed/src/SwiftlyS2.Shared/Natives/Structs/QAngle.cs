using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Diagnostics.CodeAnalysis;

namespace SwiftlyS2.Shared.Natives;

/// <summary>
/// QAngle is a type that contains 3 float, representing an angle.
/// Degree Euler. Pitch, Yaw, Roll
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4, Size = 12)]
public struct QAngle
{
    public float Pitch;
    public float Yaw;
    public float Roll;

    public QAngle( float pitch, float yaw, float roll )
    {
        Pitch = pitch;
        Yaw = yaw;
        Roll = roll;
    }

    public QAngle( QAngle other )
    {
        Pitch = other.Pitch;
        Yaw = other.Yaw;
        Roll = other.Roll;
    }

    /// <summary>
    /// X-axis accessor for Pitch rotation (up/down).
    /// </summary>
    /// <remarks>
    /// This is just a mapped accessor to the Pitch field.
    /// </remarks>
    public float X {
        readonly get => Pitch;
        set => Pitch = value;
    }

    /// <summary>
    /// Y-axis accessor for Yaw rotation (left/right).
    /// </summary>
    /// <remarks>
    /// This is just a mapped accessor to the Yaw field.
    /// </remarks>
    public float Y {
        readonly get => Yaw;
        set => Yaw = value;
    }

    /// <summary>
    /// Z-axis accessor for Roll rotation (roll/tilt).
    /// </summary>
    /// <remarks>
    /// This is just a mapped accessor to the Roll field.
    /// </remarks>
    public float Z {
        readonly get => Roll;
        set => Roll = value;
    }

    public readonly RadianEuler ToRadianEuler() => new(Pitch * MathF.PI / 180.0f, Yaw * MathF.PI / 180.0f, Roll * MathF.PI / 180.0f);
    public override readonly bool Equals( object? obj ) => obj is QAngle angle && this == angle;
    public override readonly int GetHashCode() => HashCode.Combine(Pitch, Yaw, Roll);
    public override readonly string ToString() => $"QAngle({Pitch}, {Yaw}, {Roll})";

    public static QAngle Zero => new(0, 0, 0);


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static QAngle operator +( QAngle a, QAngle b ) => new(a.Pitch + b.Pitch, a.Yaw + b.Yaw, a.Roll + b.Roll);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static QAngle operator -( QAngle a, QAngle b ) => new(a.Pitch - b.Pitch, a.Yaw - b.Yaw, a.Roll - b.Roll);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static QAngle operator *( QAngle a, QAngle b ) => new(a.Pitch * b.Pitch, a.Yaw * b.Yaw, a.Roll * b.Roll);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static QAngle operator /( QAngle a, QAngle b ) => new(a.Pitch / b.Pitch, a.Yaw / b.Yaw, a.Roll / b.Roll);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static QAngle operator *( QAngle a, float b ) => new(a.Pitch * b, a.Yaw * b, a.Roll * b);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static QAngle operator /( QAngle a, float b )
    {
        if (b == 0)
        {
            throw new DivideByZeroException();
        }
        var oofl = 1.0f / b;
        return new(a.Pitch * oofl, a.Yaw * oofl, a.Roll * oofl);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static QAngle operator -( QAngle a ) => new(-a.Pitch, -a.Yaw, -a.Roll);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==( QAngle a, QAngle b ) => a.Pitch == b.Pitch && a.Yaw == b.Yaw && a.Roll == b.Roll;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=( QAngle a, QAngle b ) => a.Pitch != b.Pitch || a.Yaw != b.Yaw || a.Roll != b.Roll;

    private const float Deg2Rad = MathF.PI / 180.0f;

    /// <summary>
    /// Calculates forward, right, and up basis vectors that correspond to this angle.
    /// Usage: <c>angle.ToDirectionVectors(out var forward, out var right, out var up);</c>
    /// </summary>
    /// <param name="forward">Forward direction (X: north, Z: up).</param>
    /// <param name="right">Right direction.</param>
    /// <param name="up">Up direction.</param>
    public readonly void ToDirectionVectors( out Vector forward, out Vector right, out Vector up )
    {
        var yawRad = Yaw * Deg2Rad;
        var pitchRad = Pitch * Deg2Rad;
        var rollRad = Roll * Deg2Rad;

        var sy = MathF.Sin(yawRad);
        var cy = MathF.Cos(yawRad);
        var sp = MathF.Sin(pitchRad);
        var cp = MathF.Cos(pitchRad);
        var sr = MathF.Sin(rollRad);
        var cr = MathF.Cos(rollRad);

        forward = new Vector(cp * cy, cp * sy, -sp);
        right = new Vector((-sr * sp * cy) + (cr * sy), (-sr * sp * sy) - (cr * cy), -sr * cp);
        up = new Vector((cr * sp * cy) + (sr * sy), (cr * sp * sy) - (sr * cy), cr * cp);
    }

    /// <summary>
    /// Serializes the qangle to a string.
    /// Example return: "100 200 300"
    /// </summary>
    /// <param name="formatProvider">Format provider to use for the string. Null for default provider.</param>
    /// <returns>Serialized qangle in string.</returns>
    public readonly string Serialize( IFormatProvider? formatProvider = null )
    {
        return $"{X.ToString(formatProvider)} {Y.ToString(formatProvider)} {Z.ToString(formatProvider)}";
    }

    /// <summary>
    /// Deserializes the qangle from a string.
    /// Example input: "100 200 300"
    /// </summary>
    /// <exception cref="FormatException">Thrown when the input string is not in the correct format.</exception>
    /// <param name="input">Serialized qangle in string.</param>
    /// <param name="formatProvider">Format provider to use for the string. Null for default provider.</param>
    /// <returns>Deserialized qangle.</returns>
    public static QAngle Deserialize( string input, IFormatProvider? formatProvider = null )
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new FormatException("Input string is null or whitespace.");

        var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3)
        {
            throw new FormatException("Invalid qangle string format. Expected format: 'x y z'");
        }
        return new QAngle(float.Parse(parts[0], formatProvider), float.Parse(parts[1], formatProvider), float.Parse(parts[2], formatProvider));
    }

    /// <summary>
    /// Tries to deserialize the qangle from a string.
    /// Example input: "100 200 300"
    /// </summary>
    /// <param name="input">Serialized qangle in string.</param>
    /// <param name="qangle">Deserialized qangle.</param>
    /// <returns>True if the deserialization was successful, false otherwise.</returns>
    public static bool TryDeserialize( [NotNullWhen(true)] string? input, out QAngle qangle )
    {
        return TryDeserialize(input, null, out qangle);
    }

    /// <summary>
    /// Tries to deserialize the qangle from a string.
    /// Example input: "100 200 300"
    /// </summary>
    /// <param name="input">Serialized qangle in string.</param>
    /// <param name="provider">Format provider to use for the string. Null for default provider.</param>
    /// <param name="qangle">Deserialized qangle.</param>
    /// <returns>True if the deserialization was successful, false otherwise.</returns>
    public static bool TryDeserialize( [NotNullWhen(true)] string? input, IFormatProvider? provider, out QAngle qangle )
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            qangle = Zero;
            return false;
        }
        var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3)
        {
            qangle = Zero;
            return false;
        }
        if (float.TryParse(parts[0], provider, out var x) && float.TryParse(parts[1], provider, out var y) && float.TryParse(parts[2], provider, out var z))
        {
            qangle = new QAngle(x, y, z);
            return true;
        }
        qangle = Zero;
        return false;
    }
}