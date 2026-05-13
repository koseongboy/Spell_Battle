using DA_Assets.DAI;
using DA_Assets.FCU.Extensions;
using DA_Assets.Shared;
using DA_Assets.Singleton;
using DA_Assets.UpdateChecker;
using DA_Assets.UpdateChecker.Models;
using System;
using UnityEngine;
using UnityEngine.UIElements;
using static DA_Assets.DAI.DAInspectorUITK;

namespace DA_Assets.FCU
{
    public partial class FcuEditor
    {
        public VisualElement BuildBottomExtraInfo()
        {
            var root = new VisualElement();

            root.Add(new DeveloperMessagesElement(AssetType.FCU, FcuConfig.Instance.ProductVersion, uitk));

            if (monoBeh.IsJsonNetExists() == false)
            {
                root.Add(uitk.Space10());
                root.Add(BuildJsonNetHelpBox());
            }

            if (monoBeh.AssetTools.NeedShowRateMe)
                root.Add(BuildRateMe());

            return root;
        }

        private VisualElement BuildJsonNetHelpBox()
        {
            var helpBoxData = new HelpBoxData
            {
                Message = FcuLocKey.helpbox_install_json_net.Localize(),
                MessageType = UnityEditor.MessageType.Error,
                OnClick = () => Application.OpenURL("https://da-assets.gitbook.io/docs/fcu-for-developers/json.net"),
            };

            return new CustomHelpBox(uitk, helpBoxData);
        }

        private VisualElement BuildRateMe()
        {
            string packageLink = GetRateMePackageLink();

            Func<string> descriptionProvider = () =>
            {
                int dc = UpdateService.GetFirstVersionDaysCount(AssetType.FCU);
                return FcuLocKey.label_rateme_desc.Localize(dc);
            };

            return uitk.BuildRateMe(
                packageLink,
                descriptionProvider,
                FcuConfig.RATEME_PREFS_KEY,
                () => FcuLocKey.tooltip_rateme_desc.Localize());
        }

        private string GetRateMePackageLink()
        {
            if (FcuConfig.Instance.Localizator.Language == DALanguage.zh)
                return "";

            int packageId = monoBeh.IsUGUI() ? 198134 : 272042;
            return "https://assetstore.unity.com/packages/tools/utilities/" + packageId + "#reviews";
        }
    }
}
