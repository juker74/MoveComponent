using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AnyThinkAds.Api;

namespace AnyThinkAds.Android
{
    public class ATGDPRConsentDismissListener : AndroidJavaProxy
    {
        ATConsentDismissListener mListener;
        public ATGDPRConsentDismissListener(ATConsentDismissListener listener): base("com.thinkup.unitybridge.sdkinit.SDKConsentDismissListener")
        {
            mListener = listener;
        }

        public void onConsentDismiss(AndroidJavaObject consentDismissInfo)
        {
            if (mListener == null)
            {
                return;
            }
            ATConsentDismissInfo info;
            if (consentDismissInfo == null)
            {
                info = ATConsentDismissInfo.Empty;
            }
            else
            {
                string msg = consentDismissInfo.Call<string>("getInfoMsg");
                if (msg == null)
                {
                    msg = "";
                }
                int type = consentDismissInfo.Call<int>("getDismissType");
                info = new ATConsentDismissInfo(msg, type);
            }
            mListener.onConsentDismiss(info);
        }

    }
}
