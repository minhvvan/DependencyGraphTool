using System;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEditorInternal;
using Object = UnityEngine.Object;

public class DependencyGraphTool : EditorWindow 
{
    private Vector2 scrollPosition;
    private Vector2 assetListScrollPosition;
    private List<string> selectedAssets = new List<string>();
    
    private Rect mainRect;
    private Rect leftPanelRect;
    private Rect rightPanelRect;
    private Rect leftContentRect;
    private Rect rightContentRect;
    
    private float leftPanelWidth = 240f;
    private float panelSpacing = 10f;
    private float panelPadding = 10f;
    private float contentPadding = 10f;
    
    private ReorderableList assetList;
    
    [MenuItem("Tools/Dependency Graph")]
    public static void ShowWindow()
    {
        GetWindow<DependencyGraphTool>("Dependency Graph");
    }

    private void OnEnable()
    {
        InitializeAssetList();
    }

    private void InitializeAssetList()
    {
        assetList = new ReorderableList(selectedAssets, typeof(string), true, true, true, true);
        
        // 헤더 설정
        assetList.drawHeaderCallback = (Rect rect) => {
            EditorGUI.LabelField(rect, "선택된 에셋");
        };
        
        // 요소 그리기 
        assetList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => {
        };
        
        // 추가 버튼 이벤트
        assetList.onAddCallback = (ReorderableList list) => {
        };
        
        // 항목 제거 버튼 이벤트
        assetList.onRemoveCallback = (ReorderableList list) => {
        };
        
        // 선택 이벤트
        assetList.onSelectCallback = (ReorderableList list) => {
        };
    }

    private void OnGUI()
    {
        mainRect = new Rect(panelPadding, panelPadding, position.width - panelPadding * 2, position.height - panelPadding * 2);
        
        GUILayout.BeginHorizontal();
        
        // 왼쪽 패널
        GUILayout.BeginVertical(GUILayout.Width(300));
        DrawControlPanel();
        GUILayout.EndVertical();
        
        // 오른쪽 패널
        GUILayout.BeginVertical();
        DrawGraphView();
        GUILayout.EndVertical();
        
        GUILayout.EndHorizontal();
    }
    
    private void DrawControlPanel()
    {
        leftPanelRect = new Rect(mainRect.x, mainRect.y, leftPanelWidth, mainRect.height);
        leftContentRect = new Rect(leftPanelRect.x + contentPadding, leftPanelRect.y + contentPadding, leftPanelWidth - contentPadding, mainRect.height - contentPadding);
        GUI.Box(leftPanelRect, "");
    
        GUILayout.BeginArea(leftContentRect);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        GUILayout.Label("에셋 종속성 분석", EditorStyles.boldLabel);
        
        GUILayout.Space(10);
        
        // 현재 선택된 에셋 추가 버튼
        if (GUILayout.Button("현재 선택된 에셋 추가"))
        {
            
        }
        
        GUILayout.Space(10);
        
        // 에셋 목록
        EditorGUILayout.LabelField("에셋 목록", EditorStyles.boldLabel);
        assetListScrollPosition = EditorGUILayout.BeginScrollView(assetListScrollPosition, GUILayout.Height(150));
        
        if (assetList != null) assetList.DoLayoutList();
        
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
        
        GUILayout.Space(10);
        
        GUILayout.BeginVertical(EditorStyles.helpBox);

        GUILayout.Label("분석 도구", EditorStyles.boldLabel);
        
        EditorGUI.BeginDisabledGroup(selectedAssets.Count == 0);
        if (GUILayout.Button("선택한 에셋 분석", GUILayout.Height(30)))
        {
            Debug.Log("에셋 분석 시작 - 총 " + selectedAssets.Count + "개 에셋");
        }
        EditorGUI.EndDisabledGroup();
        
        GUILayout.EndVertical();
        
        GUILayout.EndArea();
    }

    private void DrawGraphView()
    {
        rightPanelRect = new Rect( leftPanelRect.x + leftPanelWidth + panelSpacing, mainRect.y, mainRect.width - leftPanelWidth - panelSpacing, mainRect.height);
        rightContentRect = new Rect(rightPanelRect.x + contentPadding, rightPanelRect.y + contentPadding, rightPanelRect.width - contentPadding, rightPanelRect.height - contentPadding);

        GUI.Box(rightPanelRect, "");
        
        GUILayout.BeginArea(rightContentRect);

        GUILayout.Label("종속성 그래프", EditorStyles.boldLabel);
    
        // 그래프 제어 도구 모음 (상단)
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
    
        GUILayout.Button(EditorGUIUtility.IconContent("d_ToolHandleLocal"), EditorStyles.toolbarButton, GUILayout.Width(30));
        GUILayout.Button(EditorGUIUtility.IconContent("d_GridAndSnap"), EditorStyles.toolbarButton, GUILayout.Width(30));
        GUILayout.Button(EditorGUIUtility.IconContent("d_ToolHandlePivot"), EditorStyles.toolbarButton, GUILayout.Width(30));
    
        GUILayout.FlexibleSpace();
    
        EditorGUILayout.LabelField("확대/축소:", GUILayout.Width(60));
        float zoom = GUILayout.HorizontalSlider(1.0f, 0.1f, 2.0f, GUILayout.Width(100));
    
        GUILayout.Button("100%", EditorStyles.toolbarButton, GUILayout.Width(50));
    
        EditorGUILayout.EndHorizontal();
    
        // 그래프 영역
        Rect graphScrollArea = EditorGUILayout.GetControlRect(false, rightContentRect.height - 60);
        GUI.Box(graphScrollArea, "", EditorStyles.helpBox);
    
        // 스크롤 뷰 (가상 캔버스 영역)
        scrollPosition = GUI.BeginScrollView(graphScrollArea, scrollPosition, new Rect(0, 0, 2000, 2000));
        GUI.EndScrollView();
    
        // 하단 상태 표시줄
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
    
        GUILayout.Label("노드: 6개", EditorStyles.miniLabel);
        GUILayout.Label("|", EditorStyles.miniLabel);
        GUILayout.Label("연결: 5개", EditorStyles.miniLabel);
    
        GUILayout.FlexibleSpace();
        
        EditorGUILayout.EndHorizontal();

        GUILayout.EndArea();
    }
}
