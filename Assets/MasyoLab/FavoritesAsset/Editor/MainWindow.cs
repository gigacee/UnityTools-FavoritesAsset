#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

//=========================================================
//
//  developer : MasyoLab
//  github    : https://github.com/MasyoLab/UnityTools-FavoritesAsset
//
//=========================================================

namespace MasyoLab.Editor.FavoritesAsset {

    public class MainWindow : EditorWindow {

        private class Pipeline : IPipeline {

            private FavoritesManager m_favorites = null;
            private SettingManager m_setting = null;
            private GroupManager m_group = null;

            public FavoritesManager Favorites {
                get {
                    if (m_favorites == null) {
                        m_favorites = new FavoritesManager(this);
                    }
                    return m_favorites;
                }
            }

            public SettingManager Setting {
                get {
                    if (m_setting == null) {
                        m_setting = new SettingManager(this);
                    }
                    return m_setting;
                }
            }

            public GroupManager Group {
                get {
                    if (m_group == null) {
                        m_group = new GroupManager(this);
                    }
                    return m_group;
                }
            }

            public EditorWindow Root { get; set; } = null;
            public Rect WindowSize { get; set; } = Rect.zero;
        }

        private static MainWindow Inst = null;
        private List<BaseWindow> m_windows = new List<BaseWindow>((int)WindowEnum.Max);
        private BaseWindow m_guiWindow = null;
        private Pipeline m_pipeline = new Pipeline();

        /// <summary>
        /// ウィンドウを追加
        /// </summary>
        [MenuItem(CONST.MENU_ITEM)]
        private static void Init() {
            var window = GetWindow<MainWindow>(CONST.EDITOR_WINDOW_NAME);
            window.titleContent.image = EditorGUIUtility.IconContent(CONST.FAVORITE_ICON).image;
            Inst = window;
        }

        /// <summary>
        /// エディタ上で選択しているアセットを登録
        /// </summary>
        [MenuItem(CONST.ADD_TO_FAVORITES_ASSET_WINDOW, false, 10001)]
        private static void RegisterSelection() {
            if (Selection.activeObject == null) {
                return;
            }

            Init();

            var window = Inst.GetWindowClass<FavoritesWindow>();
            foreach (var item in Selection.objects) {
                window.AddAssetToObject(item);
            }
            window.Save();
        }

        /// <summary>
        /// RegisterSelectionのValidateメソッド
        /// </summary>
        [MenuItem(CONST.ADD_TO_FAVORITES_ASSET_WINDOW, true)]
        private static bool ValidateRegisterSelection() {
            return Selection.activeObject != null;
        }

        private void OnEnable() {
            m_pipeline.Root = this;
            foreach (var baseWindow in m_windows) {
                baseWindow.OnEnable();
            }
        }

        private void OnDestroy() {
            foreach (var baseWindow in m_windows) {
                baseWindow.OnDestroy();
            }
        }

        private void OnDisable() {
            foreach (var baseWindow in m_windows) {
                baseWindow.OnDisable();
            }
        }

        /// <summary>
        /// GUI 描画
        /// </summary>
        private void OnGUI() {
            DrawToolbar();
            UpdateGUIAction();
        }

        private void OnFocus() {
            m_pipeline.Favorites.CheckFavoritesAsset();
        }

        private void UpdateGUIAction() {
            if (m_guiWindow == null) {
                GetWindowClass<FavoritesWindow>();
            }

            m_pipeline.WindowSize = new Rect(0, EditorStyles.toolbar.fixedHeight, position.width, position.height - EditorStyles.toolbar.fixedHeight);
            m_guiWindow.OnGUI();
        }

        private _Ty GetWindowClass<_Ty>() where _Ty : BaseWindow, new() {
            if (m_guiWindow as _Ty != null) {
                return m_guiWindow as _Ty;
            }

            foreach (var item in m_windows) {
                _Ty win = item as _Ty;
                if (win == null) {
                    continue;
                }
                win.Init(m_pipeline);
                m_guiWindow?.Close();
                m_guiWindow = win;
                return win;
            }

            var newWin = new _Ty();
            newWin.Init(m_pipeline);
            m_windows.Add(newWin);
            m_guiWindow?.Close();
            m_guiWindow = newWin;
            return newWin;
        }

        private void DrawToolbar() {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)) {
                GUIContent content = new GUIContent(LanguageData.GetText(m_pipeline.Setting.Language, TextEnum.Menu));
                if (GUILayout.Button(content, EditorStyles.toolbarDropDown)) {
                    OpenMenu();
                }

                content = new GUIContent(LanguageData.GetText(m_pipeline.Setting.Language, TextEnum.Favorites));
                if (GUILayout.Button(content, EditorStyles.toolbarButton)) {
                    GetWindowClass<FavoritesWindow>();
                }

                var selectIndex = EditorGUILayout.Popup(m_pipeline.Group.Index, m_pipeline.Group.GroupNames);
                switch (m_pipeline.Group.SelectGroupByIndex(selectIndex)) {
                    case GroupSelectEventEnum.Unselect:
                        break;
                    case GroupSelectEventEnum.Select:
                        Reload();
                        break;
                    case GroupSelectEventEnum.Open:
                        GetWindowClass<GroupWindow>();
                        break;
                    default:
                        break;
                }

                GUILayout.FlexibleSpace();
            }
        }

        private void OpenMenu() {
            // Now create the menu, add items and show it
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent(LanguageData.GetText(m_pipeline.Setting.Language, TextEnum.Import)), false, (call) => {
                var importJson = SaveLoad.LoadFile(m_pipeline.Setting.IOTarget);
                var importData = FavoritesJsonExportData.FromJson(importJson);
                m_pipeline.Favorites.SetImportData(importData);
                m_pipeline.Group.SetImportData(importData);
                Reload();
                GetWindowClass<FavoritesWindow>();
            }, TextEnum.Import);

            menu.AddItem(new GUIContent(LanguageData.GetText(m_pipeline.Setting.Language, TextEnum.Export)), false, (call) => {
                var exportJson = FavoritesJsonExportData.ToJson(m_pipeline.Favorites.AssetInfoList, m_pipeline.Group.GroupDB, m_pipeline.Favorites.GetFavoriteList());
                SaveLoad.SaveFile(exportJson, m_pipeline.Setting.IOTarget, m_pipeline.Setting.IOFileName, result => {
                    m_pipeline.Setting.IOTarget = result.FolderDirectory;
                    m_pipeline.Setting.IOFileName = result.Filename;
                });
            }, TextEnum.Export);

            menu.AddSeparator("");
            menu.AddItem(new GUIContent(LanguageData.GetText(m_pipeline.Setting.Language, TextEnum.FavoriteGroup)), false, (call) => {
                GetWindowClass<GroupWindow>();
            }, TextEnum.FavoriteGroup);
            menu.AddSeparator("");

            menu.AddItem(new GUIContent(LanguageData.GetText(m_pipeline.Setting.Language, TextEnum.Setting)), false, (call) => {
                GetWindowClass<SettingWindow>();
            }, TextEnum.Setting);

            menu.AddItem(new GUIContent(LanguageData.GetText(m_pipeline.Setting.Language, TextEnum.Help)), false, (call) => {
                GetWindowClass<HelpWindow>();
            }, TextEnum.Help);

            menu.DropDown(new Rect(0, EditorStyles.toolbar.fixedHeight, 0f, 0f));
        }

        private void OpenMenuB() {
            // Now create the menu, add items and show it
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("Favorites"), false, (call) => {
                GetWindowClass<FavoritesWindow>();
            }, "item 1");

            menu.ShowAsContext();

            menu.AddItem(new GUIContent("SubMenu/MenuItem3"), false, call => { }, "item 3");

            menu.DropDown(new Rect(0, EditorStyles.toolbar.fixedHeight, 0f, 0f));
        }

        public void Reload() {
            foreach (var item in m_windows) {
                item.Reload();
            }
        }
    }
}
#endif
