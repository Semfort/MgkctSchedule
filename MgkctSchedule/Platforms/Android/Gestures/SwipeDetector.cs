using Android.Views;
using MgkctSchedule.Services;

namespace MgkctSchedule.Gestures;

/// <summary>
/// Только наблюдает за касаниями (ничего не перехватывает), поэтому вертикальная прокрутка списка
/// работает как обычно. Считает свайпом быстрое движение, где по горизонтали заметно больше, чем по вертикали.
/// </summary>
public static class SwipeDetector
{
    const float MinDistanceDp = 64f;     // минимальная длина свайпа
    const float HorizontalRatio = 1.8f;  // по X должно быть минимум в 1.8 раза больше, чем по Y
    const long MaxDurationMs = 700;

    static float _x, _y;
    static long _t;
    static bool _tracking;

    public static void Observe(MotionEvent? e, float density)
    {
        if (e == null) return;
        switch (e.ActionMasked)
        {
            case MotionEventActions.Down:
                _x = e.GetX(); _y = e.GetY(); _t = e.EventTime; _tracking = true;
                break;

            case MotionEventActions.PointerDown:   // второй палец: это не свайп (например, масштабирование)
            case MotionEventActions.Cancel:
                _tracking = false;
                break;

            case MotionEventActions.Up:
                if (!_tracking) break;
                _tracking = false;
                float dx = e.GetX() - _x, dy = e.GetY() - _y;
                if (e.EventTime - _t <= MaxDurationMs &&
                    Math.Abs(dx) >= MinDistanceDp * density &&
                    Math.Abs(dx) >= HorizontalRatio * Math.Abs(dy))
                {
                    SwipeBridge.Raise(dx < 0 ? 1 : -1);
                }
                break;
        }
    }
}
