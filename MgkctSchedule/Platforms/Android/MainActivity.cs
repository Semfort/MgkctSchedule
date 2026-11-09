using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;

namespace MgkctSchedule
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        public override bool DispatchTouchEvent(MotionEvent? e)
        {
            MgkctSchedule.Gestures.SwipeDetector.Observe(e, Resources?.DisplayMetrics?.Density ?? 2f);
            return base.DispatchTouchEvent(e);
        }
    }
}
