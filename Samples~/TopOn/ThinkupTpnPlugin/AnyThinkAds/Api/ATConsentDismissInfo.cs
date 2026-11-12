namespace AnyThinkAds.Api
{
    /// <summary>
    /// GDPR / UMP consent dialog dismiss payload; mirrors Android <c>ATGDPRConsentDismissListener.ConsentDismissInfo</c>.
    /// </summary>
    public class ATConsentDismissInfo
    {
        public string infoMsg;
        public int dismissType;

        public ATConsentDismissInfo() { }

        public ATConsentDismissInfo(string infoMsg, int dismissType)
        {
            this.infoMsg = infoMsg ?? "";
            this.dismissType = dismissType;
        }
        
        public static readonly ATConsentDismissInfo Empty = new ATConsentDismissInfo("", 0);
    }
}
