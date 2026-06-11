using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;

namespace BnlCommunityFixes
{
    public static class TimeAssaultRuntime
    {
        public static void NormalizeSceneRestart(Protocol.SceneZone scene)
        {
            if (scene == null || !scene.Restart)
            {
                return;
            }

            CardMatch matchCard = Singleton<Catalogue>.Instance.GetCard<CardMatch>(scene.MatchKey);
            if (matchCard != null && matchCard.Data is MatchDataTimeTrial)
            {
                scene.Restart = false;
                Debug.Log("[BNL TimeAssault] Disabled fast restart path for Time Assault.");
            }
        }

        public static void EnsureMenuTab(UiMenuPlay playMenu)
        {
            if (playMenu == null)
            {
                return;
            }

            TimeAssaultPlayTabInstaller installer = playMenu.GetComponent<TimeAssaultPlayTabInstaller>();
            if (installer == null)
            {
                installer = playMenu.gameObject.AddComponent<TimeAssaultPlayTabInstaller>();
            }

            installer.Initialize(playMenu);
            installer.TryInstall();
        }

        private sealed class TimeAssaultPlayTabInstaller : MonoBehaviour
        {
            private static readonly FieldInfo TabsField = typeof(UiMenuBase).GetField("Tabs", BindingFlags.Instance | BindingFlags.NonPublic);
            private static readonly MethodInfo AttemptMenuChangeMethod = typeof(UiMenuBase).GetMethod("AttemptMenuChange", BindingFlags.Instance | BindingFlags.NonPublic);

            private UiMenuPlay playMenu;
            private bool installed;

            public void Initialize(UiMenuPlay menu)
            {
                playMenu = menu;
            }

            private void OnEnable()
            {
                TryInstall();
            }

            private void Update()
            {
                if (!installed)
                {
                    TryInstall();
                }
            }

            public void TryInstall()
            {
                if (installed || playMenu == null || TabsField == null || AttemptMenuChangeMethod == null)
                {
                    return;
                }

                UiMenuTimeTrial timeTrialMenu = FindTimeTrialMenu();
                if (timeTrialMenu == null)
                {
                    return;
                }

                System.Collections.IList tabs = TabsField.GetValue(playMenu) as System.Collections.IList;
                if (tabs == null)
                {
                    return;
                }

                for (int i = 0; i < tabs.Count; i++)
                {
                    UiMenuBase.TabData existing = (UiMenuBase.TabData)tabs[i];
                    if (existing.Content == timeTrialMenu)
                    {
                        ConfigureTab(existing.Tab, timeTrialMenu);
                        installed = true;
                        Debug.Log("[BNL TimeAssault] Re-enabled existing Time Assault tab.");
                        return;
                    }
                }

                UiTabDataComponent template = FindTemplateTab();
                if (template == null || template.TabData.Tab == null)
                {
                    return;
                }

                GameObject clone = UnityEngine.Object.Instantiate(template.TabData.Tab.gameObject) as GameObject;
                if (clone == null)
                {
                    return;
                }

                clone.name = "TimeAssaultTab";
                clone.transform.SetParent(template.TabData.Tab.transform.parent, false);
                clone.transform.SetSiblingIndex(template.TabData.Tab.transform.GetSiblingIndex() + 1);

                UiTab tab = clone.GetComponent<UiTab>();
                if (tab == null)
                {
                    return;
                }

                UiMenuBase.TabData tabData = new UiMenuBase.TabData(tab, timeTrialMenu);
                tabs.Add(tabData);
                ConfigureTab(tab, timeTrialMenu);
                installed = true;
                Debug.Log("[BNL TimeAssault] Added Time Assault tab next to Custom Games.");
            }

            private UiMenuTimeTrial FindTimeTrialMenu()
            {
                UiMenuTimeTrial[] menus = playMenu.GetComponentsInChildren<UiMenuTimeTrial>(true);
                return menus.Length > 0 ? menus[0] : null;
            }

            private UiTabDataComponent FindTemplateTab()
            {
                UiTabDataComponent[] components = playMenu.GetComponents<UiTabDataComponent>();
                for (int i = 0; i < components.Length; i++)
                {
                    if (components[i].TabData.Content == playMenu.MatchBrowser)
                    {
                        return components[i];
                    }
                }

                return components.Length > 0 ? components[components.Length - 1] : null;
            }

            private void ConfigureTab(UiTab tab, UiMenuTimeTrial timeTrialMenu)
            {
                if (tab == null || timeTrialMenu == null)
                {
                    return;
                }

                if (tab.Text != null)
                {
                    string label = Singleton<Localizer>.Instance.Get("game_mode_time_trial");
                    tab.Text.text = string.IsNullOrEmpty(label) ? "TIME ASSAULT" : label.ToUpper();
                }

                tab.gameObject.SetActive(true);
                timeTrialMenu.gameObject.SetActive(false);
                tab.Button.onClick.RemoveAllListeners();
                tab.Button.onClick.AddListener(new UnityEngine.Events.UnityAction(OpenTimeAssault));
            }

            private void OpenTimeAssault()
            {
                UiMenuTimeTrial timeTrialMenu = FindTimeTrialMenu();
                if (playMenu == null || timeTrialMenu == null)
                {
                    return;
                }

                AttemptMenuChangeMethod.Invoke(playMenu, new object[] { timeTrialMenu });
            }
        }
    }
}
