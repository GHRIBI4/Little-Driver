/// <summary>
/// Side of the intersection a car ENTERS from.
/// Top-down view with North up: a car coming from North drives south.
/// </summary>
public enum Direction { North, East, South, West }

public enum Intent { Straight, Left, Right }

/// <summary>A = North/South green, B = East/West green.</summary>
public enum Phase { A, B }

public static class DirectionExtensions
{
    public static Direction Opposite(this Direction d)
    {
        return (Direction)(((int)d + 2) % 4);
    }

    public static bool IsNorthSouth(this Direction d)
    {
        return d == Direction.North || d == Direction.South;
    }

    /// <summary>Which road a car leaves on, given where it came from and where it wants to go.</summary>
    public static Direction ExitFor(this Direction from, Intent intent)
    {
        switch (intent)
        {
            case Intent.Left:  return (Direction)(((int)from + 1) % 4); // from North, turning left -> East
            case Intent.Right: return (Direction)(((int)from + 3) % 4); // from North, turning right -> West
            default:           return from.Opposite();                  // straight
        }
    }
}
