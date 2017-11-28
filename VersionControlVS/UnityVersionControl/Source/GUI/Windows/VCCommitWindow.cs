// Copyright (c) <2017> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
//using MultiColumnState = MultiColumnState<string, UnityEngine.GUIContent>;

namespace VersionControl.UserInterface
{
    using Logging;
    using ComposedString = ComposedSet<string, FilesAndFoldersComposedStringDatabase>;
    internal class VCCommitWindow : EditorWindow
    {
        // Const
        const float minimumControlHeight = 50;
        const int maxProgressSize = 65536;

        // State
        public IEnumerable<string> commitedFiles = new List<string>();

        private IEnumerable<ComposedString> assetPaths = new List<ComposedString>();
        private IEnumerable<ComposedString> depedencyAssetPaths = new List<ComposedString>();
        private bool firstTime = true;
        private bool commitInProgress = false;
        private bool commitCompleted = false;
        private string commitProgress = "";
        private float commitMessageHeight;
        private string commitMessage = null;
        private string CommitMessage
        {
            get { return commitMessage ?? (commitMessage = EditorPrefs.GetString("VCCommitWindow/CommitMessage", "")); }
            set { commitMessage = value; EditorPrefs.SetString("VCCommitWindow/CommitMessage", commitMessage); }
        }

        // Cache
        private Vector2 scrollViewVectorLog = Vector2.zero;
        private Vector2 statusScroll = Vector2.zero;
        private Rect rect;

        VCMultiColumnAssetList vcMultiColumnAssetList;

        public static void Init()
        {
            GetWindow<VCCommitWindow>("Commit");
        }

        public void SetAssetPaths(IEnumerable<string> assets, IEnumerable<string> dependencies)
        {
            D.Log("VCCommitWindow:SetAssetPaths");
            ProfilerUtilities.BeginSample("CommitWindow::SetAssetPaths");
            assetPaths = assets.Select(s => new ComposedString(s)).ToList();
            depedencyAssetPaths = dependencies.Select(s => new ComposedString(s)).ToList();
            vcMultiColumnAssetList.SetBaseFilter(BaseFilter);
            RefreshSelection();
            ProfilerUtilities.EndSample();
        }

        private bool BaseFilter(VersionControlStatus vcStatus)
        {
            using (PushStateUtility.Profiler("CommitWindow::BaseFilter"))
            {
                var metaStatus = vcStatus.MetaStatus();
                bool interresting = (vcStatus.fileStatus != VCFileStatus.None &&
                                    (vcStatus.fileStatus != VCFileStatus.Normal || (metaStatus != null && metaStatus.fileStatus != VCFileStatus.Normal))) ||
                                    vcStatus.lockStatus == VCLockStatus.LockedHere;

                if (!interresting) return false;
                ComposedString key = vcStatus.assetPath.TrimEnd(VCCAddMetaFiles.meta);
                return (assetPaths.Contains(key) || depedencyAssetPaths.Contains(key));
            }
        }

        private void RefreshSelection()
        {
            vcMultiColumnAssetList.refreshSelection(assetPaths);
            Repaint();
        }

        private void OnEnable()
        {
            minSize  = new Vector2(1000, 400);
            commitMessageHeight = EditorPrefs.GetFloat("VCCommitWindow/commitMessageHeight", 140.0f);
            rect = new Rect(0, commitMessageHeight, position.width, 10.0f);
            vcMultiColumnAssetList = new VCMultiColumnAssetList(Repaint, /*VCSettings.SelectiveCommit*/ true);
            VCCommands.Instance.StatusCompleted += RefreshSelection;
        }

        private void OnDisable()
        {
            EditorPrefs.SetFloat("VCCommitWindow/commitMessageHeight", commitMessageHeight);
            vcMultiColumnAssetList.Dispose();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginVertical();
            if (commitInProgress) CommitProgressGUI();
            else CommitMessageGUI();
            EditorGUILayout.EndVertical();
        }

        private void CommitProgressGUI()
        {
            scrollViewVectorLog = EditorGUILayout.BeginScrollView(scrollViewVectorLog, false, false);
            GUILayout.TextArea(commitProgress);
            EditorGUILayout.EndScrollView();
            if (commitCompleted)
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Close"))
                {
                    Close();
                }
            }
        }

        private void CommitMessageGUI()
        {
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeVertical);
            rect = GUIControls.DragButton(rect, GUIContent.none, null);
            rect.y = position.height - rect.y;
            rect.x = 0.0f;
            rect.width = position.width;
            commitMessageHeight = rect.y = Mathf.Clamp(rect.y, minimumControlHeight, position.height - minimumControlHeight);

            GUILayout.BeginArea(new Rect(0, 0, position.width, rect.y));
            vcMultiColumnAssetList.DrawGUI();
            GUILayout.EndArea();

            GUILayout.BeginArea(new Rect(0, rect.y, position.width, position.height - rect.y));
            DrawButtons();
            GUILayout.EndArea();
        }

        private void DrawButtons()
        {
            EditorGUILayout.BeginHorizontal();

            using (GUILayoutHelper.BackgroundColor(CommitMessage.Length < 10 ? new Color(1, 0, 0) : new Color(0, 1, 0)))
            {
                statusScroll = EditorGUILayout.BeginScrollView(statusScroll, false, false);
                string strCommitMessageOld = CommitMessage;

                    CommitMessage = EditorGUILayout.TextArea(CommitMessage, GUILayout.MinWidth(100), GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

                if (CommitMessage.Length > strCommitMessageOld.Length)
                {
                    if (!string.IsNullOrEmpty(VCSettings.strCommitAutoComplete))
                    {

                        string[] arSuggestions = VCSettings.strCommitAutoComplete.Split(';');
                        for (int i = 0; i < arSuggestions.Length; i++)
                        {
                            string strSuggestion = arSuggestions[i].Trim(new[] { ' ' });
                            string[] strSuggestionParts = strSuggestion.Split(new string[] { "->" }, System.StringSplitOptions.RemoveEmptyEntries);

                            if (CommitMessage.EndsWith(strSuggestionParts[0]))
                            {
                                TextEditor editor = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);

                                CommitAutoCompleteSuggestion suggestion = new CommitAutoCompleteSuggestion(strSuggestionParts[0].Length, strSuggestionParts[1], this);

                                editor.DetectFocusChange();
                                editor.OnLostFocus();

                                PopupWindow.Show(new Rect((editor.graphicalSelectCursorPos), suggestion.GetWindowSize()), suggestion);
                            }
                        }

                    }
                }
                EditorGUILayout.EndScrollView();
            }
            if (firstTime)
            {
                EditorGUI.FocusTextInControl("CommitMessage");
                firstTime = false;
            }


            using (new PushState<bool>(GUI.enabled, VCCommands.Instance.Ready, v => GUI.enabled = v))
            {
                if (GUILayout.Button(Terminology.commit, GUILayout.Width(100)))
                {
                    var selection = //VCSettings.SelectiveCommit ? vcMultiColumnAssetList.GetMasterSelection() : vcMultiColumnAssetList.GetSelection();
                        vcMultiColumnAssetList.GetCommitSelection(VCSettings.SelectiveCommit);
                    if (selection.Count() != 0)
                    {                        
                        var selectedAssets = selection.Select(status => status.assetPath).Select(cstr => cstr.Compose()).ToList();
                        VCCommands.Instance.ProgressInformation += s =>
                        {
                            commitProgress = commitProgress + s;
                            if (commitProgress.Length > maxProgressSize)
                            {
                                commitProgress = commitProgress.Substring(commitProgress.Length - maxProgressSize);
                            }
                            statusScroll.y = Mathf.Infinity;
                            Repaint();
                        };
                        var commitTask = VCCommands.Instance.CommitTask(selectedAssets, CommitMessage);
                        commitTask.ContinueWithOnNextUpdate(result =>
                        {
                            if (result)
                            {
                                commitedFiles = selectedAssets;
                                CommitMessage = "";
                                Repaint();
                                if (VCSettings.AutoCloseAfterSuccess) Close();
                            }
                            commitCompleted = true;
                        });
                        commitInProgress = true;
                    }
                    else
                    {
                        ShowNotification(new GUIContent("No files selected"));
                    }
                }
                if (GUILayout.Button("Cancel", GUILayout.Width(100)))
                {
                    Close();
                }
            }
            EditorGUILayout.EndHorizontal();
            if (vcMultiColumnAssetList.GetSelection().Any())
            {
                RemoveNotification();
            }

            GUI.SetNextControlName("unfocus");
        }

        public void suggesitonInput(int _iOriginalLength = 0, string _strSuggestion = "")
        {
            if (_strSuggestion != "")
            {
                CommitMessage = CommitMessage.Substring(0, CommitMessage.Length - _iOriginalLength) + _strSuggestion;

                GUIUtility.hotControl = 0;
                GUIUtility.keyboardControl = 0;
                GUI.FocusControl("unfocus");
                EditorGUI.FocusTextInControl("unfocus");

                Repaint();
            }
        }
    }

    internal class CommitAutoCompleteSuggestion : PopupWindowContent
    {
        public const string c_strWarning = "de/resel!";

        private string m_strSuggestion;
        private int m_iOriginalLength;
        private VCCommitWindow m_window;


        private bool bAccepted;
        public CommitAutoCompleteSuggestion(int _iOriginalLength, string _strSuggestion, VCCommitWindow _window)
        { m_strSuggestion = _strSuggestion; m_iOriginalLength = _iOriginalLength; m_window = _window; }

        public override void OnGUI(Rect rect)
        {
            if (bAccepted)
            {
                GUI.Label(rect, c_strWarning);
            }
            else
            {
                if ((GUI.Button(rect, m_strSuggestion, GUI.skin.label)) || (Event.current.type == EventType.keyDown && (Event.current.keyCode == KeyCode.KeypadEnter || Event.current.keyCode == KeyCode.Return)))
                { m_window.suggesitonInput(m_iOriginalLength, m_strSuggestion); bAccepted = true; editorWindow.Repaint(); }
                else if (Event.current.type == EventType.mouseDown || Event.current.type == EventType.mouseUp || Event.current.type == EventType.KeyDown)
                { m_window.suggesitonInput(); if (m_window) m_window.Focus(); else this.editorWindow.Close(); }
            }
        }

        public override Vector2 GetWindowSize()
        {
            return new Vector2(8.5f * Mathf.Max(m_strSuggestion.Length, c_strWarning.Length), EditorGUIUtility.singleLineHeight);
        }
    }
}

