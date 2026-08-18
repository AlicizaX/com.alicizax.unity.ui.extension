#if TEXTMESHPRO_SUPPORT
using System;
using AlicizaX;
using AlicizaX.Localization;
using Cysharp.Text;
using TMPro;

namespace UnityEngine.UI
{
    public class UXTextMeshPro : TextMeshProUGUI
    {
#pragma warning disable CS0414
        [SerializeField] private int m_localizationID;
#pragma warning restore CS0414
        [SerializeField] private string m_localizationKey = "";
        [SerializeField] private string[] m_localizationFormatArgs = Array.Empty<string>();
        private EventRuntimeHandle m_localizationChangeHandle;
        private bool m_localizationSubscribed;

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            if (!Application.isPlaying && !string.IsNullOrEmpty(m_localizationKey))
            {
                string previewLabel = LocalizationRefreshHelper.GetPreviewLabel(m_localizationKey);
                LocalizationRefreshHelper.ResizeFormatArgs(
                    ref m_localizationFormatArgs,
                    LocalizationRefreshHelper.GetFormatArgumentCount(previewLabel));
                SetLocalizedText(previewLabel);
            }
        }
#endif

        protected override void Awake()
        {
            base.Awake();
            if (!Application.isPlaying) return;
            SyncLocalizationSubscription();
            ChangeLanguage();
        }

        protected override void OnDestroy()
        {
            if (m_localizationSubscribed)
            {
                m_localizationChangeHandle.Dispose();
            }
            base.OnDestroy();
        }

        protected void OnLocalizationChanged(in LocalizationChangeEvent e)
        {
            ChangeLanguage();
        }

        protected void ChangeLanguage()
        {
            if (!string.IsNullOrEmpty(m_localizationKey))
            {
                SetLocalizedText(UXComponentExtensionsHelper.GetString(m_localizationKey));
            }
        }

        private void SyncLocalizationSubscription()
        {
            if (string.IsNullOrEmpty(m_localizationKey))
            {
                UnsubscribeLocalization();
                return;
            }

            SubscribeLocalization();
        }

        private void SubscribeLocalization()
        {
            if (m_localizationSubscribed) return;
            m_localizationChangeHandle = EventBus.Subscribe<LocalizationChangeEvent>(OnLocalizationChanged);
            m_localizationSubscribed = true;
        }

        private void UnsubscribeLocalization()
        {
            if (!m_localizationSubscribed) return;
            m_localizationChangeHandle.Dispose();
            m_localizationSubscribed = false;
        }

        /// <summary>
        /// 设置本地化 Key，并使用当前 Inspector 中配置的格式化参数刷新文本。
        /// </summary>
        /// <param name="localizationID">本地化 Key，例如："UI.Shop.Currency"。</param>
        /// <example>
        /// <code>
        /// currencyText.SetLocalization("UI.Shop.Currency");
        /// </code>
        /// </example>
        public void SetLocalization(string localizationID)
        {
            m_localizationKey = localizationID;
            if (Application.isPlaying)
            {
                SyncLocalizationSubscription();
            }
            ChangeLanguage();
        }

        /// <summary>
        /// 设置本地化 Key，并覆盖 Inspector 中配置的全部格式化参数。
        /// </summary>
        /// <param name="localizationID">本地化 Key，例如："UI.Player.Info"。</param>
        /// <param name="formatArgs">用于替换 {0}、{1}、{2} 等占位符的格式化参数。</param>
        /// <example>
        /// <code>
        /// // 本地化文本："{0}你好，等级 {1}"
        /// playerInfoText.SetLocalization("UI.Player.Info", "Alice", "10");
        /// </code>
        /// </example>
        public void SetLocalization(string localizationID, params string[] formatArgs)
        {
            m_localizationKey = localizationID;
            m_localizationFormatArgs = formatArgs;
            if (Application.isPlaying)
            {
                SyncLocalizationSubscription();
            }
            ChangeLanguage();
        }

        /// <summary>
        /// 覆盖当前本地化格式化参数，并使用已有本地化 Key 刷新文本。
        /// </summary>
        /// <param name="formatArgs">用于替换 {0}、{1}、{2} 等占位符的格式化参数。</param>
        /// <example>
        /// <code>
        /// // 当前本地化文本："信用点：{0}"
        /// currencyText.SetLocalizationArgs("100");
        ///
        /// // 当前本地化文本："{0}你好，等级 {1}，段位 {2}"
        /// playerInfoText.SetLocalizationArgs("Alice", "10", "Gold");
        /// </code>
        /// </example>
        public void SetLocalizationArgs(params string[] formatArgs)
        {
            m_localizationFormatArgs = formatArgs;
            ChangeLanguage();
        }

        /// <summary>
        /// 清除本地化绑定，并直接设置最终显示字符串。
        /// </summary>
        /// <param name="value">最终显示文本。</param>
        /// <remarks>
        /// 此方法会清空本地化 Key 和 ID，使该组件不再响应本地化自动刷新。
        /// </remarks>
        /// <example>
        /// <code>
        /// currencyText.SetTextValue("信用点：999");
        /// </code>
        /// </example>
        public void SetTextValue(string value)
        {
            m_localizationKey = string.Empty;
            m_localizationID = 0;
            if (Application.isPlaying)
            {
                UnsubscribeLocalization();
            }
            SetText(value);
        }

        /// <summary>
        /// 清除本地化绑定，并通过 ZString 直接设置最终显示值。
        /// </summary>
        /// <typeparam name="T">写入文本缓冲区的值类型。</typeparam>
        /// <param name="value">最终显示值。</param>
        /// <remarks>
        /// 此方法会清空本地化 Key 和 ID，使该组件不再响应本地化自动刷新。
        /// </remarks>
        /// <example>
        /// <code>
        /// scoreText.SetTextValue(999);
        /// timerText.SetTextValue(12.5f);
        /// </code>
        /// </example>
        public void SetTextValue<T>(T value)
        {
            m_localizationKey = string.Empty;
            m_localizationID = 0;
            if (Application.isPlaying)
            {
                UnsubscribeLocalization();
            }
            this.SetText(value);
        }

        private void SetLocalizedText(string format)
        {
            if (m_localizationFormatArgs.Length == 0)
            {
                SetText(format);
                return;
            }

            var builder = ZString.CreateStringBuilder();
            try
            {
                AppendLocalizationFormat(ref builder, format, m_localizationFormatArgs);
                var segment = builder.AsArraySegment();
                SetCharArray(segment.Array, segment.Offset, segment.Count);
            }
            finally
            {
                builder.Dispose();
            }
        }

        private static void AppendLocalizationFormat(ref Utf16ValueStringBuilder builder, string format, string[] args)
        {
            for (int i = 0; i < format.Length; i++)
            {
                char character = format[i];
                if (character == '{')
                {
                    if (i + 1 < format.Length && format[i + 1] == '{')
                    {
                        builder.Append('{');
                        i++;
                        continue;
                    }

                    if (TryParseFormatArgument(format, i, out int index, out int endIndex) && index < args.Length)
                    {
                        builder.Append(args[index]);
                        i = endIndex;
                        continue;
                    }
                }
                else if (character == '}' && i + 1 < format.Length && format[i + 1] == '}')
                {
                    builder.Append('}');
                    i++;
                    continue;
                }

                builder.Append(character);
            }
        }

        private static bool TryParseFormatArgument(string format, int startIndex, out int index, out int endIndex)
        {
            index = 0;
            endIndex = startIndex;

            int currentIndex = startIndex + 1;
            bool hasIndex = false;
            while (currentIndex < format.Length && char.IsDigit(format[currentIndex]))
            {
                hasIndex = true;
                index = index * 10 + format[currentIndex] - '0';
                currentIndex++;
            }

            if (!hasIndex || currentIndex >= format.Length)
            {
                return false;
            }

            char next = format[currentIndex];
            if (next != '}' && next != ':' && next != ',')
            {
                return false;
            }

            while (currentIndex < format.Length && format[currentIndex] != '}')
            {
                currentIndex++;
            }

            if (currentIndex >= format.Length)
            {
                return false;
            }

            endIndex = currentIndex;
            return true;
        }
    }
}

#endif
