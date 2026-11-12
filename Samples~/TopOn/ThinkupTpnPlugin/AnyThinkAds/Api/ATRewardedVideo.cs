using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;
using System;

using AnyThinkAds.Common;
using AnyThinkAds.ThirdParty.LitJson;


namespace AnyThinkAds.Api
{
    public class ATRewardedVideo
    {
        private static readonly ATRewardedVideo instance = new ATRewardedVideo();
        public IATRewardedVideoAdClient client;

        private ATRewardedVideo()
        {
            client = GetATRewardedClient();
        }

        public static ATRewardedVideo Instance
        {
            get
            {
                return instance;
            }
        }


		/***
		 * 
		 */
        public void loadVideoAd(string placementId, Dictionary<string,string> pairs)
        {
            client.loadVideoAd(placementId, JsonMapper.ToJson(pairs));
        }

        /// <summary>Load rewarded video. <paramref name="loadPayload"/> may contain a nested <c>atAdRequest</c> map (<c>channelSource</c>, <c>adxBidFloorInfo</c>, <c>preLoadInfo</c>, ...) plus existing keys such as <c>UserId</c>.</summary>
        public void loadVideoAd(string placementId, Dictionary<string, object> loadPayload)
        {
            client.loadVideoAd(placementId, JsonMapper.ToJson(loadPayload));
        }


        public bool hasAdReady(string placementId)
        {
            return client.hasAdReady(placementId);
        }

        public void entryScenarioWithPlacementID(string placementId, string scenarioID)
        {
            client.entryScenarioWithPlacementID(placementId,scenarioID);
        }

        public void entryScenarioWithPlacementID(string placementId, string scenarioID, Dictionary<string, object> tkExtra)
        {
            string j = (tkExtra != null) ? JsonMapper.ToJson(tkExtra) : null;
            client.entryScenarioWithPlacementID(placementId, scenarioID, j);
        }

        public void setAdRevenueListener(string placementId, IATAdRevenueListener listener)
        {
            client.setAdRevenueListener(placementId, listener);
        }

        public void setAutoAdRevenueListener(IATAdRevenueListener listener)
        {
            client.setAutoAdRevenueListener(listener);
        }
        
        public string checkAdStatus(string placementId)
        {
            return client.checkAdStatus(placementId);
        }

        public string getValidAdCaches(string placementId)
        {
            return client.getValidAdCaches(placementId);
        }

        public void showAd(string placementId)
        {
            client.showAd(placementId, JsonMapper.ToJson(new Dictionary<string, string>()));
        }

        public void showAd(string placementId, Dictionary<string, string> pairs)
        {
            client.showAd(placementId, JsonMapper.ToJson(pairs));
        }
                
        public IATRewardedVideoAdClient GetATRewardedClient()
        {
            return AnyThinkAds.ATAdsClientFactory.BuildRewardedVideoAdClient();
        }



    }
}