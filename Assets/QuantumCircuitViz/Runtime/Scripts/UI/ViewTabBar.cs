using UnityEngine;
using UnityEngine.UI;
using System;

namespace QuantumCircuitViz.UI
{
    /// <summary>
    /// Top tab bar for switching between the three main views:
    /// Circuit, State, and Bloch.
    /// </summary>
    public class ViewTabBar : MonoBehaviour
    {
        public event Action<int> OnTabSelected;

        private Button[] _tabs;
        private Image[] _tabBgs;
        private Text[] _tabTexts;
        private int _selectedIndex;

        private static readonly Color ActiveBg   = new Color(0.00f, 0.45f, 0.65f, 0.95f);
        private static readonly Color InactiveBg = new Color(0.08f, 0.08f, 0.14f, 0.90f);
        private static readonly Color ActiveText  = Color.white;
        private static readonly Color InactiveText = new Color(0.45f, 0.55f, 0.65f);

        public int SelectedIndex => _selectedIndex;

        public void Initialise(RectTransform parent, string[] tabNames)
        {
            gameObject.name = "TabBar";
            var rt = GetComponent<RectTransform>();
            if (rt == null) rt = gameObject.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0f, 0.96f);
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0.03f, 0.03f, 0.06f, 0.98f);

            int count = tabNames.Length;
            _tabs = new Button[count];
            _tabBgs = new Image[count];
            _tabTexts = new Text[count];

            float w = 1f / count;
            for (int i = 0; i < count; i++)
            {
                var go = UIGo($"Tab_{tabNames[i]}");
                var trt = go.GetComponent<RectTransform>();
                trt.SetParent(rt, false);
                trt.anchorMin = new Vector2(i * w, 0f);
                trt.anchorMax = new Vector2((i + 1) * w, 1f);
                trt.offsetMin = new Vector2(2, 2);
                trt.offsetMax = new Vector2(-2, -2);

                _tabBgs[i] = go.AddComponent<Image>();
                _tabs[i] = go.AddComponent<Button>();

                var txtGo = UIGo("Label");
                var txtRt = txtGo.GetComponent<RectTransform>();
                txtRt.SetParent(trt, false);
                txtRt.anchorMin = Vector2.zero;
                txtRt.anchorMax = Vector2.one;
                txtRt.offsetMin = txtRt.offsetMax = Vector2.zero;

                _tabTexts[i] = txtGo.AddComponent<Text>();
                _tabTexts[i].text = tabNames[i];
                _tabTexts[i].font = Font.CreateDynamicFontFromOSFont("Consolas", 14);
                _tabTexts[i].fontSize = 14;
                _tabTexts[i].alignment = TextAnchor.MiddleCenter;

                int idx = i;
                _tabs[i].onClick.AddListener(() => Select(idx));
            }

            Select(0);
        }

        public void Select(int index)
        {
            if (index < 0 || index >= _tabs.Length) return;
            _selectedIndex = index;
            for (int i = 0; i < _tabs.Length; i++)
            {
                bool active = i == index;
                _tabBgs[i].color = active ? ActiveBg : InactiveBg;
                _tabTexts[i].color = active ? ActiveText : InactiveText;
            }
            OnTabSelected?.Invoke(index);
        }

        /// <summary>Cycle to next/prev tab.</summary>
        public void Cycle(int direction)
        {
            int count = _tabs.Length;
            int next = ((_selectedIndex + direction) % count + count) % count;
            Select(next);
        }

        private static GameObject UIGo(string name) => new GameObject(name, typeof(RectTransform));
    }
}
