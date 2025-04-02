using System;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.FPS.Game;
using Unity.FPS.UI;
using Unity.Tutorials.Core.Editor;
using UnityEditorInternal;
using Object = UnityEngine.Object;

// 에셋 노드 구조체 정의
public class AssetNode
{
    public string Path;                        // 에셋 경로
    public int Depth;                          // 루트 노드로부터의 깊이
    public List<string> Dependencies;          // 이 노드가 의존하는 에셋들
    public List<AssetNode> Children;           // 이 노드의 자식 노드들
    public AssetNode Parent;                   // 부모 노드
    public bool InCycle = false;               // 순환 참조에 포함되었는지 여부
    
    public AssetNode(string path)
    {
        Path = path;
        Depth = 0;
        Dependencies = new List<string>();
        Children = new List<AssetNode>();
        Parent = null;
    }
    
    // 자식 노드 추가 및 깊이 설정
    public void AddChild(AssetNode child)
    {
        child.Parent = this;
        child.Depth = this.Depth + 1;
        Children.Add(child);
    }
}

public class DependencyGraphTool : EditorWindow 
{
    #region Node

    private Dictionary<string, AssetNode> assetNodes = new Dictionary<string, AssetNode>();
    private Dictionary<string, Rect> nodePositions = new Dictionary<string, Rect>();
    private HashSet<Tuple<string, string>> cyclicEdges = new HashSet<Tuple<string, string>>();
    #endregion
    
    #region Rect
    private Rect mainRect;
    private Rect leftPanelRect;
    private Rect rightPanelRect;
    private Rect leftContentRect;
    private Rect rightContentRect;

    private float leftPanelWidth = 240f;
    private float panelSpacing = 10f;
    private float panelPadding = 10f;
    private float contentPadding = 10f;
    #endregion
    
    #region Graph
    private bool analysisCompleted = false; // 분석이 완료되었는지 여부
    private string highlightedAsset = null; // 현재 하이라이트된 에셋
    private Rect virtualCanvasRect = new Rect(0, 0, 10000, 10000); // 가상 캔버스 크기
    private Vector2 canvasCenter = new Vector2(5000, 5000); // 가상 캔버스 중심점
    #endregion

    #region Filtering
    private bool showHierarchyOnly = false;      // 계층 구조만 표시
    private bool showDirectDependencies = true;  // 직접 종속성 표시
    private bool showCyclicDependencies = true;  // 순환 종속성 표시
    private int maxDepthToShow = 5;              // 표시할 최대 깊이
    #endregion
    
    #region Analysis Settings
    private int maxAnalysisDepth = 3; // 분석 최대 깊이
    #endregion
    
    private CycleDetector cycleDetector = new CycleDetector();
    private NodePositionCalculator nodePositionCalculator = new NodePositionCalculator();
    private DependencyAnalyzer dependencyAnalyzer = new DependencyAnalyzer();
    private string selectedAsset;

    private Vector2 scrollPosition;
    private Vector2 assetListScrollPosition;

    private ReorderableList assetList;
    
    [MenuItem("Tools/Dependency Graph")]
    public static void ShowWindow()
    {
        GetWindow<DependencyGraphTool>("Dependency Graph");
    }

    private void OnEnable()
    {
        wantsMouseMove = true; // 마우스 이벤트를 받을 수 있도록 설정
    }

    private void OnGUI()
    {
        // 매 프레임마다 그래프가 있으면 그리도록 설정
        if (Event.current.type == EventType.MouseMove || Event.current.type == EventType.Layout)
        {
            Repaint();
        }

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
        
        // 현재 선택된 에셋 필드 표시
        EditorGUILayout.LabelField("선택된 에셋", EditorStyles.boldLabel);
        
        // 에셋 경로 표시 (읽기 전용)
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextField(string.IsNullOrEmpty(selectedAsset) ? 
                "에셋을 선택하세요" : System.IO.Path.GetFileName(selectedAsset));
        }
        
        GUILayout.Space(5);
        
        // 에셋 선택 버튼
        if (GUILayout.Button("에셋 선택", GUILayout.Height(25)))
        {
            string path = EditorUtility.OpenFilePanel("에셋 선택", "Assets", "");
            if (!string.IsNullOrEmpty(path))
            {
                // 전체 경로를 Unity 상대 경로로 변환
                string relativePath = path.Replace(Application.dataPath, "Assets");
                selectedAsset = relativePath;
                Repaint();
            }
        }
        
        // 현재 에디터에서 선택된 에셋 가져오기
        if (GUILayout.Button("현재 선택된 에셋 가져오기"))
        {
            if (Selection.activeObject != null)
            {
                string path = AssetDatabase.GetAssetPath(Selection.activeObject);
                if (!string.IsNullOrEmpty(path))
                {
                    selectedAsset = path;
                    Repaint();
                }
            }
        }
        
        // 선택된 에셋 정보 표시
        if (!string.IsNullOrEmpty(selectedAsset))
        {
            GUILayout.Space(10);
            EditorGUILayout.LabelField("에셋 정보", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            
            // 에셋 아이콘 표시
            Texture icon = AssetDatabase.GetCachedIcon(selectedAsset);
            if (icon != null)
            {
                GUILayout.Box(icon, GUILayout.Width(32), GUILayout.Height(32));
            }
            
            EditorGUILayout.BeginVertical();
            
            // 에셋 이름
            EditorGUILayout.LabelField("이름:", System.IO.Path.GetFileName(selectedAsset));
            
            // 에셋 타입
            string extension = System.IO.Path.GetExtension(selectedAsset).ToLower();
            string typeLabel = "알 수 없음";
            
            switch (extension)
            {
                case ".cs":
                    typeLabel = "C# 스크립트";
                    break;
                case ".prefab":
                    typeLabel = "프리팹";
                    break;
                case ".asset":
                    typeLabel = "에셋";
                    break;
                default:
                    typeLabel = extension.TrimStart('.');
                    break;
            }
            
            EditorGUILayout.LabelField("타입:", typeLabel);
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndHorizontal();
            
            // 에셋 찾기 버튼
            if (GUILayout.Button("에셋 찾기", GUILayout.Height(20)))
            {
                var asset = AssetDatabase.LoadAssetAtPath<Object>(selectedAsset);
                if (asset != null)
                {
                    EditorGUIUtility.PingObject(asset);
                    Selection.activeObject = asset;
                }
            }
        }
        
        EditorGUILayout.EndVertical();
        
        GUILayout.Space(10);
        
        // 분석 설정
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("분석 설정", EditorStyles.boldLabel);
        
        // 탐색 깊이 설정
        GUILayout.BeginHorizontal();
        GUILayout.Label("탐색 깊이:", GUILayout.Width(80));
        maxAnalysisDepth = EditorGUILayout.IntSlider(maxAnalysisDepth, 1, 10);
        GUILayout.EndHorizontal();
        
        GUILayout.Space(5);
        
        // 도움말 텍스트
        EditorGUILayout.HelpBox("탐색 깊이가 클수록 더 많은 종속성을 발견하지만 분석 시간이 오래 걸립니다.", MessageType.Info);
        
        EditorGUILayout.EndVertical();
        
        GUILayout.Space(10);
        
        // 분석 도구
        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("분석 도구", EditorStyles.boldLabel);
        
        EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(selectedAsset));
        if (GUILayout.Button("선택한 에셋 분석", GUILayout.Height(30)))
        {
            OnAnalyzeButtonClicked();
        }
        EditorGUI.EndDisabledGroup();
        
        GUILayout.EndVertical();
        
        // 분석이 완료된 경우에만 필터링 옵션 표시
        if (analysisCompleted)
        {
            GUILayout.Space(10);
            
            // 필터링 옵션
            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("그래프 필터링", EditorStyles.boldLabel);
            
            showHierarchyOnly = EditorGUILayout.ToggleLeft("계층 구조만 표시", showHierarchyOnly);
            
            if (!showHierarchyOnly)
            {
                EditorGUI.indentLevel++;
                showDirectDependencies = EditorGUILayout.ToggleLeft("직접 종속성 표시", showDirectDependencies);
                showCyclicDependencies = EditorGUILayout.ToggleLeft("순환 종속성 표시 (빨간색)", showCyclicDependencies);
                EditorGUI.indentLevel--;
            }
            
            GUILayout.Space(5);
            
            // 최대 깊이 슬라이더
            GUILayout.BeginHorizontal();
            GUILayout.Label("표시 깊이:", GUILayout.Width(70));
            maxDepthToShow = EditorGUILayout.IntSlider(maxDepthToShow, 1, 10);
            GUILayout.EndHorizontal();
            
            if (GUILayout.Button("적용", GUILayout.Height(24)))
            {
                // 필터 적용 후 그래프 다시 그리기
                Repaint();
            }
            
            GUILayout.EndVertical();
        }
        
        GUILayout.EndArea();
    }

    private void DrawGraphView()
    {
        rightPanelRect = new Rect(leftPanelRect.x + leftPanelWidth + panelSpacing, mainRect.y, mainRect.width - leftPanelWidth - panelSpacing, mainRect.height);
        rightContentRect = new Rect(rightPanelRect.x + contentPadding, rightPanelRect.y + contentPadding, rightPanelRect.width - contentPadding * 2, rightPanelRect.height - contentPadding * 2);

        GUI.Box(rightPanelRect, "");
        
        GUILayout.BeginArea(rightContentRect);

        GUILayout.Label("종속성 그래프", EditorStyles.boldLabel);
        
        // 그래프 제어 도구 모음 (상단)
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        
        // 중앙으로 이동 버튼 (캔버스 재정렬)
        if (GUILayout.Button(EditorGUIUtility.IconContent("RotateTool"), EditorStyles.toolbarButton, GUILayout.Width(30)))
        {
            // 노드 재배치 (중앙 기준)
            ArrangeNodesWithHierarchy();
            Repaint();
        }
        
        GUILayout.FlexibleSpace();
        
        EditorGUILayout.EndHorizontal();

        // 그래프 영역
        Rect graphScrollArea = EditorGUILayout.GetControlRect(false, rightContentRect.height - 40);
        GUI.Box(graphScrollArea, "", EditorStyles.helpBox);
        
        // 분석이 완료된 경우에만 그래프 표시
        if (analysisCompleted && nodePositions.Count > 0)
        {
            // 가상 캔버스 영역 (고정 크기)
            scrollPosition = GUI.BeginScrollView(graphScrollArea, scrollPosition, virtualCanvasRect);

            // 노드 그리기
            DrawNodes();
            
            // 연결 라인 그리기
            DrawConnectionLines();
            
            // 마우스 클릭 이벤트 처리 (노드 선택)
            HandleMouseEvents();
            
            GUI.EndScrollView();
        }
        else
        {
            // 분석 전이면 안내 메시지 표시
            GUIStyle centeredStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14
            };
            GUI.Label(graphScrollArea, "왼쪽 패널에서 에셋을 선택하고 '선택한 에셋 분석' 버튼을 클릭하세요.", centeredStyle);
        }

        // 하단 상태 표시줄
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        int connectionCount = CountConnections();
        GUILayout.Label($"노드: {nodePositions.Count}개", EditorStyles.miniLabel);
        GUILayout.Label("|", EditorStyles.miniLabel);
        GUILayout.Label($"연결: {connectionCount}개", EditorStyles.miniLabel);
        
        // 선택된 에셋 정보 표시
        if (!string.IsNullOrEmpty(highlightedAsset))
        {
            GUILayout.Label("|", EditorStyles.miniLabel);
            GUILayout.Label($"선택됨: {System.IO.Path.GetFileName(highlightedAsset)}", EditorStyles.miniLabel);
        }

        GUILayout.FlexibleSpace();
        
        EditorGUILayout.EndHorizontal();

        GUILayout.EndArea();
    }
    
    private void HandleMouseEvents()
    {
        Event e = Event.current;
    
        if (e.type == EventType.MouseDown && e.button == 0)
        {
            // 클릭한 위치에 있는 노드 찾기
            Vector2 mousePos = e.mousePosition;
            string clickedAsset = null;
        
            foreach (var kvp in nodePositions)
            {
                if (kvp.Value.Contains(mousePos))
                {
                    clickedAsset = kvp.Key;
                    break;
                }
            }
        
            if (!string.IsNullOrEmpty(clickedAsset))
            {
                // 클릭한 에셋이 있으면 하이라이트 및 Unity 에디터에서 Ping
                highlightedAsset = clickedAsset;
            
                // Unity 에디터에서 에셋 찾아 하이라이트
                var asset = AssetDatabase.LoadAssetAtPath<Object>(clickedAsset);
                if (asset != null)
                {
                    EditorGUIUtility.PingObject(asset);
                    Selection.activeObject = asset;
                }
            
                e.Use(); // 이벤트 소비
                Repaint();
            }
        }
    }
    
    private void DrawNodes()
    {
        if (Event.current.type != EventType.Repaint)
            return;

        if (nodePositions.Count == 0)
            return;
        
        // 최대 깊이 찾기
        int maxDepth = assetNodes.Values.Any() ? assetNodes.Values.Max(n => n.Depth) : 1;
        
        // nodePositions에 있는 모든 키를 순회
        foreach (var assetPath in nodePositions.Keys)
        {
            // 깊이 필터링 적용
            if (assetNodes.ContainsKey(assetPath) && assetNodes[assetPath].Depth > maxDepthToShow)
                continue;
                
            Rect pos = nodePositions[assetPath];
                    
            string fileName = System.IO.Path.GetFileName(assetPath);
            string fileExt = System.IO.Path.GetExtension(assetPath).ToLower();

            // 노드 색상 팔레트 개선
            Color[] scriptColors = {
                new Color(0.3f, 0.5f, 0.8f),   // 파란색 계열
                new Color(0.4f, 0.6f, 0.9f),
                new Color(0.2f, 0.4f, 0.7f)
            };

            Color[] prefabColors = {
                new Color(0.8f, 0.5f, 0.3f),   // 주황색 계열
                new Color(0.9f, 0.6f, 0.4f),
                new Color(0.7f, 0.4f, 0.2f)
            };
            
            Color nodeColor = Color.gray;
            string nodeType = "Unknown";

            switch (fileExt)
            {
                case ".cs":
                    nodeColor = scriptColors[Math.Abs(assetPath.GetHashCode()) % scriptColors.Length];
                    nodeType = "Script";
                    break;
                case ".prefab":
                    nodeColor = prefabColors[Math.Abs(assetPath.GetHashCode()) % prefabColors.Length];
                    nodeType = "Prefab";
                    break;
                case ".asset":
                    nodeColor = new Color(0.5f, 0.8f, 0.3f); // 연두색 (에셋)
                    nodeType = "Asset";
                    break;
            }
            
            // 깊이에 따른 색상 조정 (깊이가 깊을수록 밝아짐)
            int depth = assetNodes[assetPath].Depth;
            float depthRatio = maxDepth > 0 ? (float)depth / maxDepth : 0;
            nodeColor = Color.Lerp(nodeColor, Color.white, depthRatio * 0.4f);
            
            // 선택된 에셋 강조 (초기 선택된 에셋 또는 클릭된 에셋)
            bool isSelected = selectedAsset.Equals(assetPath);
            bool isHighlighted = assetPath == highlightedAsset;
            
            if (isHighlighted)
            {
                // 하이라이트된 노드는 밝게 강조
                nodeColor = Color.Lerp(nodeColor, Color.yellow, 0.5f);
            }
            else if (isSelected)
            {
                // 선택된 에셋은 약간 밝게
                nodeColor = Color.Lerp(nodeColor, Color.white, 0.3f);
            }
            
            // 순환 참조를 포함하는 노드 표시
            if (assetNodes[assetPath].InCycle)
            {
                // 약간 빨간색 추가
                nodeColor = Color.Lerp(nodeColor, Color.red, 0.3f);
            }
            
            // 노드 그리기
            DrawSingleNode(pos, fileName, nodeType, nodeColor, isSelected || isHighlighted);

            // 깊이 정보 표시 (노드 오른쪽 아래에 작게 표시)
            GUI.Label(
                new Rect(pos.x + pos.width - 25, pos.y + pos.height - 15, 20, 15),
                $"D{depth}",
                new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight }
            );
        }
    }
    
    private void DrawConnectionLines()
    {
        if (Event.current.type != EventType.Repaint)
            return;

        Handles.BeginGUI();

        // 연결선 스타일 정의
        float normalLineWidth = 1.5f;
        float highlightLineWidth = 2.0f;
        Color hierarchyLineColor = new Color(0.1f, 0.8f, 0.3f, 0.9f); // 녹색 (정방향 계층)
        Color reverseLineColor = new Color(0.4f, 0.6f, 1.0f, 0.7f);   // 파란색 (역방향)
        Color cyclicLineColor = new Color(1.0f, 0.3f, 0.3f, 0.8f);    // 빨간색 (사이클)
        
        HashSet<Tuple<string, string>> drawnConnections = new HashSet<Tuple<string, string>>();
        
        // 1. 먼저 계층 구조 그리기 (부모 -> 자식, 정방향)
        foreach (var node in assetNodes.Values)
        {
            if (node.Parent == null) continue;
            
            // 깊이 필터링
            if (node.Depth > maxDepthToShow) continue;
            
            string childPath = node.Path;
            string parentPath = node.Parent.Path;
            
            if (!nodePositions.ContainsKey(childPath) || !nodePositions.ContainsKey(parentPath))
                continue;
                
            Rect childRect = nodePositions[childPath];
            Rect parentRect = nodePositions[parentPath];
            
            // 연결 포인트 계산
            Vector2 childCenter = new Vector2(childRect.x + childRect.width/2, childRect.y + childRect.height/2);
            Vector2 parentCenter = new Vector2(parentRect.x + parentRect.width/2, parentRect.y + parentRect.height/2);
            
            Vector2 start = GetConnectionPoint(parentRect, childCenter);
            Vector2 end = GetConnectionPoint(childRect, parentCenter);
            
            // 계층 구조는 녹색 실선 직선으로 그리기 (부모 -> 자식 방향)
            DrawStraightLine(start, end, hierarchyLineColor, highlightLineWidth);
            
            // 그려진 연결 표시
            drawnConnections.Add(new Tuple<string, string>(parentPath, childPath));
        }
        
        // 계층 구조만 표시하는 경우 여기서 종료
        if (showHierarchyOnly) 
        {
            Handles.EndGUI();
            return;
        }
        
        // 2. 종속성 관계 그리기
        if (showDirectDependencies)
        {
            foreach (var sourceNode in assetNodes.Values)
            {
                string sourceAsset = sourceNode.Path;
                
                // 깊이 필터링
                if (sourceNode.Depth > maxDepthToShow) continue;
                    
                if (!nodePositions.TryGetValue(sourceAsset, out var sourceRect)) continue;

                foreach (string targetAsset in sourceNode.Dependencies)
                {
                    // 타겟 노드가 없으면 건너뛰기
                    if (!assetNodes.TryGetValue(targetAsset, out var targetNode)) continue;

                    // 깊이 필터링
                    if (targetNode.Depth > maxDepthToShow) continue;
                        
                    if (sourceAsset == targetAsset || !nodePositions.ContainsKey(targetAsset)) continue;
                        
                    // 이미 그려진 연결은 건너뛰기
                    var connectionKey = new Tuple<string, string>(sourceAsset, targetAsset);
                    if (!drawnConnections.Add(connectionKey)) continue;
                        

                    // 종속성 연결
                    Rect targetRect = nodePositions[targetAsset];
                    
                    // 연결 포인트 계산
                    Vector2 sourceCenter = new Vector2(sourceRect.x + sourceRect.width/2, sourceRect.y + sourceRect.height/2);
                    Vector2 targetCenter = new Vector2(targetRect.x + targetRect.width/2, targetRect.y + targetRect.height/2);
                    
                    Vector2 start = GetConnectionPoint(sourceRect, targetCenter);
                    Vector2 end = GetConnectionPoint(targetRect, sourceCenter);
                    
                    // 사이클 여부 확인
                    bool isCyclic = cyclicEdges.Contains(new Tuple<string, string>(sourceAsset, targetAsset));
                    
                    // 사이클 표시 여부 확인
                    if (isCyclic && !showCyclicDependencies)
                        continue;
                    
                    // 3가지 케이스로 나누어 처리
                    if (isCyclic)
                    {
                        // 1. 사이클: 빨간색 실선 곡선
                        DrawCurvedLine(start, end, cyclicLineColor, highlightLineWidth, 0.3f, false);
                    }
                    else if (sourceNode.Depth >= targetNode.Depth)
                    {
                        // 2. 역방향 (자신보다 낮은 깊이를 가리키는 경우): 파란색 점선 곡선
                        DrawCurvedLine(start, end, reverseLineColor, normalLineWidth, 0.3f, true);
                    }
                    else
                    {
                        // 3. 정방향 (깊이가 증가하는 방향): 녹색 실선 직선
                        DrawStraightLine(start, end, hierarchyLineColor, normalLineWidth);
                    }
                }
            }
        }

        Handles.EndGUI();
    }
   
    // 분석 버튼 클릭 핸들러
    private void OnAnalyzeButtonClicked()
    {
        CalculateDependency();
        
        analysisCompleted = true;
        Repaint(); // UI 갱신 요청
    }
    
    private void CalculateDependency()
    {
        // 데이터 초기화
        assetNodes.Clear();
        nodePositions.Clear();
        cyclicEdges.Clear();
        analysisCompleted = false;

        // 루트 노드 생성
        AssetNode rootNode = new AssetNode(selectedAsset) { Depth = 0 };
        assetNodes[selectedAsset] = rootNode;

        // 처리된 에셋을 추적하기 위한 집합
        HashSet<string> processedAssets = new HashSet<string>();

        // 재귀적으로 종속성 분석 및 AssetNode 맵 구축
        AnalyzeAssetRecursively(selectedAsset, processedAssets, 0, maxAnalysisDepth);

        // 순환 참조 감지
        DetectCycles();

        // 노드 배치
        ArrangeNodesWithHierarchy();

        analysisCompleted = true;
    }
    
    // 재귀적 종속성 분석 메서드 개선
    private void AnalyzeAssetRecursively(string assetPath, HashSet<string> processedAssets, int depth, int maxDepth = 5)
    {
        dependencyAnalyzer.AnalyzeAssetRecursively(assetNodes, assetPath, processedAssets, depth, maxDepth);
    }

    private void DetectCycles()
    {
        cyclicEdges.Clear();
        cycleDetector.DetectCycle(assetNodes, cyclicEdges);
    }
    
    // 360도 분포를 가진 개선된 노드 배치 메서드
    private void ArrangeNodesWithHierarchy()
    {
        nodePositions.Clear();
        nodePositionCalculator.ArrangeNodesWithHierarchy(nodePositions, canvasCenter, selectedAsset, assetNodes);
        
        // 초기 스크롤 위치 설정
        InitializeScrollPosition();
    }
    
    private void InitializeScrollPosition()
    {
        // 루트 노드가 있으면 중앙에 보이도록 스크롤 위치 설정
        if (!selectedAsset.IsNullOrEmpty() && nodePositions.ContainsKey(selectedAsset))
        {
            string rootPath = selectedAsset;
            Rect rootRect = nodePositions[rootPath];
        
            // 가상 캔버스 중앙에 루트 노드 위치
            scrollPosition = new Vector2(
                rootRect.x - (position.width - rightPanelRect.x - leftPanelWidth - panelSpacing) / 2 + nodePositionCalculator.nodeWidth / 2,
                rootRect.y - (position.height - rightPanelRect.y) / 2 + nodePositionCalculator.nodeHeight / 2
            );
        }
        else
        {
            // 아니면 가상 캔버스 중앙으로
            scrollPosition = new Vector2(
                canvasCenter.x - (position.width - rightPanelRect.x - leftPanelWidth - panelSpacing) / 2,
                canvasCenter.y - (position.height - rightPanelRect.y) / 2
            );
        }
    }
    
    private void DrawArrow(Vector2 pos, Vector2 direction, float size, Color color)
    {
        // 명확한 삼각형 화살표 그리기
        Vector2 right = new Vector2(-direction.y, direction.x).normalized * (size * 0.5f);
        Vector2 tip = pos;
        Vector2 baseLeft = pos - direction * size - right;
        Vector2 baseRight = pos - direction * size + right;

        Handles.color = color;
        Handles.DrawAAConvexPolygon(new Vector3[] { tip, baseLeft, baseRight });
    }
    
    // DrawSingleNode 메서드 수정 - 노드 테두리 개선
    private void DrawSingleNode(Rect pos, string nodeTitle, string type, Color color, bool isSelected = false)
    {
        // 파일 이름만 추출
        string displayName = System.IO.Path.GetFileName(nodeTitle);
        bool isInCycle = cyclicEdges != null && 
                        cyclicEdges.Any(e => 
                            e.Item1 == nodeTitle || e.Item2 == nodeTitle || 
                            (System.IO.Path.GetFileName(e.Item1) == displayName || System.IO.Path.GetFileName(e.Item2) == displayName));
        
        // 그림자 효과
        GUI.color = new Color(0, 0, 0, 0.3f);
        GUI.Box(new Rect(pos.x + 3, pos.y + 3, pos.width, pos.height), "", "flow node 0");
        GUI.color = Color.white;

        // 노드 배경
        EditorGUI.DrawRect(pos, color);

        // 테두리 색상 선택
        Color borderColor;
        float borderWidth;
        
        if (nodeTitle == highlightedAsset)
        {
            // 하이라이트된 노드 - 노란색 굵은 테두리
            borderColor = Color.yellow;
            borderWidth = 3f;
        }
        else if (isSelected)
        {
            // 선택된 노드 - 흰색 테두리
            borderColor = Color.white;
            borderWidth = 2f;
        }
        else if (isInCycle)
        {
            // 사이클 포함 노드 - 빨간색 테두리
            borderColor = Color.red;
            borderWidth = 1.5f;
        }
        else
        {
            // 일반 노드 - 반투명 테두리
            borderColor = new Color(1, 1, 1, 0.5f);
            borderWidth = 1f;
        }
        
        // 위/아래 테두리
        EditorGUI.DrawRect(new Rect(pos.x, pos.y - borderWidth, pos.width, borderWidth), borderColor);
        EditorGUI.DrawRect(new Rect(pos.x, pos.y + pos.height, pos.width, borderWidth), borderColor);
        
        // 좌/우 테두리
        EditorGUI.DrawRect(new Rect(pos.x - borderWidth, pos.y - borderWidth, borderWidth, pos.height + borderWidth * 2), borderColor);
        EditorGUI.DrawRect(new Rect(pos.x + pos.width, pos.y - borderWidth, borderWidth, pos.height + borderWidth * 2), borderColor);
        
        // 제목 줄 배경 (약간 어두운 그라데이션)
        EditorGUI.DrawRect(new Rect(pos.x, pos.y, pos.width, 22), new Color(0, 0, 0, 0.1f));

        // 노드 내용 - 파일 이름과 타입 표시
        GUIStyle titleStyle = new GUIStyle(EditorStyles.whiteBoldLabel) { alignment = TextAnchor.MiddleLeft };
        GUI.Label(new Rect(pos.x + 8, pos.y, pos.width - 16, 22), displayName, titleStyle);
        
        GUIStyle typeStyle = new GUIStyle(EditorStyles.whiteLabel) 
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 10
        };
        GUI.Label(new Rect(pos.x + 8, pos.y + 24, pos.width - 16, 20), type, typeStyle);
    }

    // 연결 수 계산 메서드
    private int CountConnections()
    {
        int count = 0;
        foreach (var node in assetNodes.Values)
        {
            count += node.Dependencies.Count;
        }
        return count;
    }
    
    // 점선 곡선 관련 문제 수정
    private void DrawCurvedLine(Vector2 start, Vector2 end, Color color, float width, float curvatureMultiplier = 0.3f, bool dashed = false)
    {
        // 두 점 사이의 거리 계산
        Vector2 diff = end - start;
        float distance = diff.magnitude;
    
        // 거리에 비례하는 커브 강도 계산
        float curvature = Mathf.Min(distance * curvatureMultiplier, 100f);
    
        // 커브 방향 (선에 수직)
        Vector2 perpendicular = new Vector2(-diff.y, diff.x).normalized;
    
        // 제어점 계산
        Vector2 midPoint = (start + end) * 0.5f;
        Vector2 controlPoint = midPoint + perpendicular * curvature;
    
        // 핸들 색상 설정
        Handles.color = color;
    
        if (dashed)
        {
            // 점선 그리기 개선
            Vector2[] dashPoints = new Vector2[40]; // 충분한 포인트 사용
        
            for (int i = 0; i < dashPoints.Length; i++)
            {
                float t = (float)i / (dashPoints.Length - 1);
            
                // 베지어 곡선 공식으로 점 계산
                float u = 1f - t;
                dashPoints[i] = u*u*u * start + 
                                3f*u*u*t * Vector2.Lerp(start, controlPoint, 0.5f) + 
                                3f*u*t*t * Vector2.Lerp(controlPoint, end, 0.5f) + 
                                t*t*t * end;
            }
        
            // 점선 그리기
            for (int i = 0; i < dashPoints.Length - 1; i += 2)
            {
                if (i + 1 < dashPoints.Length)
                {
                    Handles.DrawAAPolyLine(width, dashPoints[i], dashPoints[i+1]);
                }
            }
        }
        else
        {
            // 실선 베지어 곡선
            Handles.DrawBezier(
                start, end,
                Vector2.Lerp(start, controlPoint, 0.5f),
                Vector2.Lerp(controlPoint, end, 0.5f),
                color, null, width
            );
        }
    
        // 화살표 그리기
        Vector2 arrowDirection = (end - Vector2.Lerp(controlPoint, end, 0.8f)).normalized;
        DrawArrow(end, arrowDirection, 10, color);
    }

    // 직선 연결선
    // 선 렌더링 문제 수정
    private void DrawStraightLine(Vector2 start, Vector2 end, Color color, float width)
    {
        // 완전한 실선으로 그리기 위해 핸들 색상 및 너비 명확하게 설정
        Handles.color = color;
    
        // 실선으로 명확하게 그리기
        Handles.DrawAAPolyLine(width, start, end);
    
        // 화살표 그리기
        Vector2 direction = (end - start).normalized;
        DrawArrow(end, direction, 10, color);
    }
    
    // 노드의 경계에서 연결 지점 계산 (보다 정확한 위치 계산)
    private Vector2 GetConnectionPoint(Rect rect, Vector2 targetPoint)
    {
        Vector2 center = new Vector2(rect.x + rect.width / 2, rect.y + rect.height / 2);
        Vector2 direction = (targetPoint - center).normalized;

        // 직사각형 모양의 노드에 맞는 연결 지점 계산
        float dx = rect.width / 2;
        float dy = rect.height / 2;
        
        // 선이 교차하는 지점 계산
        float vx = direction.x;
        float vy = direction.y;
        
        // x, y 비율을 이용한 교차점 계산
        float tx = (vx == 0) ? float.MaxValue : Mathf.Abs(dx / vx);
        float ty = (vy == 0) ? float.MaxValue : Mathf.Abs(dy / vy);
        
        // 더 가까운 교차점 사용
        float t = Mathf.Min(tx, ty);
        
        return center + direction * t;
    }
}