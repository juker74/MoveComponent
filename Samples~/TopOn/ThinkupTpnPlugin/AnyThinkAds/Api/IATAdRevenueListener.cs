namespace AnyThinkAds.Api
{
    /// <summary>
    /// Ad revenue callback. Mirrors Android <c>com.thinkup.core.api.ATAdRevenueListener</c>; payload is an <see cref="ATCallbackInfo"/> serialized from native <c>ATAdInfo</c>.
    /// </summary>
    public interface IATAdRevenueListener
    {
        void onAdRevenuePaid(string placementId, ATCallbackInfo adInfo);
    }
}
