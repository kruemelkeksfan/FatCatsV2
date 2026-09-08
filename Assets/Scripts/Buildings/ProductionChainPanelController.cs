using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ProductionChainPanelController : PanelObject
{
    [SerializeField] private RectTransform productionChainEntryPrefab = null;
    private List<Tuple<string, string>> productionChains = null;

    private void Awake()
    {
        productionChains = new List<Tuple<string, string>>();
    }

    public override void UpdatePanel(RectTransform panel)
    {
        RectTransform contentPanel = (RectTransform)panel.GetChild(2).GetChild(0).GetChild(0);
        if(contentPanel.childCount <= 0)
        {
            int i = 0;
            float totalHeight = 0.0f;
            foreach(Tuple<string, string> productionChain in productionChains)
            {
                RectTransform productionChainEntry = GameObject.Instantiate<RectTransform>(productionChainEntryPrefab, contentPanel);
                productionChainEntry.anchoredPosition = new Vector2(productionChainEntry.anchoredPosition.x, -totalHeight);

                productionChainEntry.GetChild(0).GetComponent<TMP_Text>().text = productionChain.Item1;
                productionChainEntry.GetChild(1).GetComponent<TMP_Text>().text = productionChain.Item2;

                if(i % 2 != 0)
			    {
				    productionChainEntry.GetComponent<Image>().enabled = false;
			    }

                ++i;
                totalHeight += productionChainEntry.sizeDelta.y;
            }

            contentPanel.sizeDelta = new Vector2(contentPanel.sizeDelta.x, totalHeight);
        }
    }

    public void SetProductionChains(Dictionary<string, ProductionChain> productionChains)
    {
        // Sort Production Chains by Building ID
        List<KeyValuePair<string, ProductionChain>> sortedProductionChains = new List<KeyValuePair<string, ProductionChain>>(productionChains);
        sortedProductionChains.Sort(delegate (KeyValuePair<string, ProductionChain> lho, KeyValuePair<string, ProductionChain> rho)
        {
            return lho.Value.finalBuilding.id.CompareTo(rho.Value.finalBuilding.id);
        });

        // Generate Production Chain Strings
        GoodManager goodManager = GoodManager.GetInstance();
        foreach(KeyValuePair<string, ProductionChain> productionChain in sortedProductionChains)
        {
            if(goodManager.GetGoodData(productionChain.Key).needCategory != NeedCategory.None)
            {
                StringBuilder buildingStringBuilder = new StringBuilder();
                List<KeyValuePair<BuildingData, int>> sortedBuildingCountKeys = new List<KeyValuePair<BuildingData, int>>(productionChain.Value.buildingCounts);
                foreach(KeyValuePair<BuildingData, int> buildingCount in sortedBuildingCountKeys)
                {
                    buildingStringBuilder.Append(buildingCount.Key.buildingName + " x " + buildingCount.Value + "    ");
                }

                this.productionChains.Add(new Tuple<string, string>(productionChain.Key, buildingStringBuilder.ToString()));
            }
        }
    }
}
