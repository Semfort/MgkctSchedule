namespace MgkctSchedule.Services;

/// <summary>Горизонтальный свайп по экрану: 1 = влево (следующий), -1 = вправо (предыдущий).</summary>
public static class SwipeBridge
{
    public static event Action<int>? Swiped;
    public static void Raise(int direction) => Swiped?.Invoke(direction);
}
