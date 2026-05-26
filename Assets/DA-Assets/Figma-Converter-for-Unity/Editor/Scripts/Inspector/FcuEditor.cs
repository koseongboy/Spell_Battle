using DA_Assets.DAI;
using DA_Assets.Extensions;
using DA_Assets.FCU.Extensions;
using DA_Assets.Singleton;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static DA_Assets.DAI.DAInspectorUITK;

#pragma warning disable IDE0003
#pragma warning disable CS0649

namespace DA_Assets.FCU
{
    [CustomEditor(typeof(FigmaConverterUnity)), CanEditMultipleObjects]
    public partial class FcuEditor : DAEditor<FcuEditor, FigmaConverterUnity>
    {
        [SerializeField] Texture2D _logoDarkTheme;
        public Texture2D LogoDarkTheme => _logoDarkTheme;

        [SerializeField] Texture2D _logoLightTheme;
        public Texture2D LogoLightTheme => _logoLightTheme;

        [SerializeField] Texture2D _topBanner;

        [SerializeField] Texture2D _gradientBg;

        [SerializeField] Texture2D _noise;

        private HeaderSection _headerSection;
        internal HeaderSection Header => monoBeh.Link(ref _headerSection, this);

        private FramesSection _frameListSection;
        internal FramesSection FrameList => monoBeh.Link(ref _frameListSection, this);

        private ScrollView _framesScroll;
        private VisualElement _framesHost;
        private SquareIconButton _btnRecent;
        private VisualElement _root;
        private TextField _projectUrlField;

        public override VisualElement CreateInspectorGUI()
        {
            if (monoBeh.Settings.MainSettings.WindowMode)
            {
                return DrawWindowedGUI();
            }
            else
            {
                return DrawGUI();
            }
        }

        public VisualElement DrawGUI()
        {
            _root = uitk.CreateRoot(uitk.ColorScheme.FCU_BG);
            BuildContent();
            return _root;
        }

        internal void RebuildUI()
        {
            _root.Clear();
            BuildContent();
        }

        void ForceRebuild() => ActiveEditorTracker.sharedTracker.ForceRebuild();
    }
}
