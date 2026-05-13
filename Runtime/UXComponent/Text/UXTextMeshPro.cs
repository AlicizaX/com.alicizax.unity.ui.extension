#if TEXTMESHPRO_SUPPORT
using AlicizaX;
using AlicizaX.Localization;
using TMPro;

namespace UnityEngine.UI
{
    public class UXTextMeshPro : TextMeshProUGUI
    {
        [SerializeField] private int m_localizationID;
        [SerializeField] private string m_localizationKey = "";

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            if (!Application.isPlaying && !string.IsNullOrEmpty(m_localizationKey))
            {
                text = LocalizationRefreshHelper.GetPreviewLabel(m_localizationKey);
            }
        }
#endif
        protected override void Start()
        {
            base.Start();
            if (!Application.isPlaying) return;
            EventBus.Subscribe<LocalizationChangeEvent>(OnLocalizationChanged);
            ChangeLanguage();
        }

        protected void OnLocalizationChanged(in LocalizationChangeEvent e)
        {
            ChangeLanguage();
        }

        protected void ChangeLanguage()
        {
            if (!string.IsNullOrEmpty(m_localizationKey) && !"None".Equals(m_localizationKey) && UXComponentExtensionsHelper.LocalizationHelper != null)
            {
                text = UXComponentExtensionsHelper.LocalizationHelper.GetString(m_localizationKey);
            }
        }

        /// <summary>
        /// 重新动态设置多语言
        /// </summary>
        /// <param name="localizationID"></param>
        public void SetLocalization(string localizationID)
        {
            m_localizationKey = localizationID;
            ChangeLanguage();
        }
    }
}

#endif
