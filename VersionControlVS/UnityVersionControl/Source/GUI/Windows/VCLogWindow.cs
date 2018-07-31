using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UnityVersionControl.Source.GUI.Windows
{
    public class VCLogWindow: EditorWindow
    {
        enum Month {Jan = 1, Feb, Mar, Apr, May, Jun, Jul, Aug, Sep, Oct, Nov, Dec }

        public static void showLogWindow(IEnumerable<string> _ieAssetPaths = null)
        {
            VCLogWindow window = CreateInstance<VCLogWindow>();
            if (_ieAssetPaths != null)
                window.m_liSelectedPaths = _ieAssetPaths.ToList();
            window.init();
            window.Show();
        }


        private List<string> m_liSelectedPaths = null;

        private bool m_bVerbose = false;
        private int m_iDayStart, m_iDayEnd, m_iYearStart, m_iYearEnd;
        private Month m_iMonthStart, m_iMonthEnd;

        private List<log> m_liLogs = new List<log>();

        private Vector2 m_v2ScrollPos = Vector2.zero;

        private string strArgument
        {
            get
            {
                string strRet = " -r ";

                strRet += "{" + m_iYearStart.ToString("0000") + "-" + ((int)m_iMonthStart).ToString("00") + "-" + m_iDayStart.ToString("00") + "}:";
                strRet += "{" + m_iYearEnd.ToString("0000") + "-" + ((int)m_iMonthEnd).ToString("00") + "-" + m_iDayEnd.ToString("00") + "}";

                if (m_bVerbose)
                    strRet += " -v";

                return strRet;
            }
        }

        private void OnGUI()
        {
            GUILayout.BeginHorizontal();

            GUILayout.Label("From: ", GUILayout.Width(50));
            float fOldWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 50;

            m_iDayStart = Mathf.Clamp(EditorGUILayout.IntField("Day: ", m_iDayStart, GUILayout.Width(100)), 1, 31);
            m_iMonthStart = (Month)EditorGUILayout.EnumPopup("Month: ", m_iMonthStart, GUILayout.Width(100));
            m_iYearStart = Mathf.Clamp(EditorGUILayout.IntField("Year: ", m_iYearStart, GUILayout.Width(100)), 2005, System.DateTime.Today.Year);

            GUILayout.Space(100);
            GUILayout.Label("To: ", GUILayout.Width(50));

            m_iDayEnd = Mathf.Clamp(EditorGUILayout.IntField("Day: ", m_iDayEnd, GUILayout.Width(100)), 1, 31);
            m_iMonthEnd = (Month)EditorGUILayout.EnumPopup("Month: ", m_iMonthEnd, GUILayout.Width(100));
            m_iYearEnd = Mathf.Clamp(EditorGUILayout.IntField("Year: ", m_iYearEnd, GUILayout.Width(100)), 2005, System.DateTime.Today.Year);

            EditorGUIUtility.labelWidth = fOldWidth;
            GUILayout.EndHorizontal();

            m_bVerbose = EditorGUILayout.Toggle("Verbose: ", m_bVerbose);

            if (GUILayout.Button("Refresh"))
                refreshLog();

            GUILayout.Space(15);

            m_v2ScrollPos = EditorGUILayout.BeginScrollView(m_v2ScrollPos);
            for (int i = 0; i < m_liLogs.Count; i++)
            {
                m_liLogs[i].OnGUI();
                if (i + 1 < m_liLogs.Count)
                {
                    EditorGUILayout.Separator();
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void refreshLog()
        {
            m_v2ScrollPos = Vector2.zero;

            m_liLogs.Clear();
            if (m_liSelectedPaths == null)
            {
                m_liLogs.Add(new log("", strArgument));
            }
            else
            {
                for (int i = 0; i < m_liSelectedPaths.Count; i++)
                {
                    m_liLogs.Add(new log(m_liSelectedPaths[i], strArgument));
                }
            }
        }

        private void init()
        {
            System.DateTime dateEnd = System.DateTime.Now;
            System.DateTime dateStart = dateEnd.Subtract(new TimeSpan(7, 0, 0, 0));

            m_iDayStart = dateStart.Day;
            m_iDayEnd = dateEnd.Day;
            m_iYearStart = dateStart.Year;
            m_iYearEnd = dateEnd.Year;
            m_iMonthStart = (Month)dateStart.Month;
            m_iMonthEnd = (Month)dateEnd.Month;

            refreshLog();
        }
    }

    class log
    {
        private const string c_strLogSeparator = "------------------------------------------------------------------------";
        private static readonly char[] c_arHeaderContentSeparator =  new char[] { '\n', '\r' };


        private bool m_bExpanded;
        private List<logEntry> m_liLogEntries;
        private string m_strPath;

        public log(string _strAssetPath, string _strArgument)
        {
            m_liLogEntries = new List<logEntry>();


            m_strPath = _strAssetPath;

            string strLog = VersionControl.VCCommands.Instance.Log(m_strPath, _strArgument);
            if (strLog == null || strLog == "")
                return;

            string[] arEntries = strLog.Split(new string[] { c_strLogSeparator}, StringSplitOptions.RemoveEmptyEntries);


            if (arEntries == null)
                return;
            for (int i = 0; i < arEntries.Length; i++)
            {
                string strCurrentEntry = arEntries[i].Trim(new char[] { ' ', '\n', '\r' });
                if (strCurrentEntry != null && strCurrentEntry != "")
                {

                    string[] arCurrentEntry = strCurrentEntry.Split(c_arHeaderContentSeparator, StringSplitOptions.RemoveEmptyEntries);

                    m_liLogEntries.Add(
                        new logEntry(arCurrentEntry[0].Trim(new char[] { ' ', '\n', '\r' }),
                                arCurrentEntry.Length > 1 ? 
                                String.Join(new string(c_arHeaderContentSeparator), arCurrentEntry, 1, arCurrentEntry.Length-1)
                                : ""
                            ));
                }
            }
        }

        public void OnGUI()
        {
            m_bExpanded = EditorGUILayout.Foldout(m_bExpanded, m_strPath == ""? "Log" : m_strPath);
            EditorGUILayout.Space();

            if (m_bExpanded)
            {
                EditorGUI.indentLevel++;
                for (int i = 0; i < m_liLogEntries.Count; i++)
                {
                    m_liLogEntries[i].OnGUI();
                }
                EditorGUI.indentLevel--;
            }
        }
    }

    class logEntry
    {
        private string m_strHeader;
        private string m_strContent;
        private bool m_bExpanded;

        public logEntry(string _strHeader, string _strContent)
        {
            m_bExpanded = false;
            m_strHeader = _strHeader;
            m_strContent = _strContent;
        }

        public void OnGUI()
        {
            GUILayout.BeginHorizontal();
            Rect rect = EditorGUILayout.GetControlRect(GUILayout.Width(30));
            m_bExpanded = EditorGUI.Foldout(rect, m_bExpanded, "");
            EditorGUILayout.SelectableLabel(m_strHeader, EditorStyles.boldLabel);
            GUILayout.EndHorizontal();
            EditorGUILayout.Space();

            if (m_bExpanded)
            {
                GUILayout.BeginHorizontal();
                EditorGUILayout.SelectableLabel("", GUILayout.Width(20));
                EditorGUILayout.SelectableLabel(m_strContent);
                GUILayout.EndHorizontal();
            }
        }
    }
}
