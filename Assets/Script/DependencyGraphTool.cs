using System;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.FPS.Game;
using Unity.FPS.UI;
using UnityEditorInternal;
using Object = UnityEngine.Object;

public class DependencyGraphTool : EditorWindow 
{
    #region Node
    private float nodeWidth = 200f;
    private float nodeHeight = 50f;
    private Dictionary<string, Rect> nodePositions = new Dictionary<string, Rect>();
    public HashSet<Tuple<string, string>> cyclicEdges = new HashSet<Tuple<string, string>>();
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
    private Dictionary<string, List<string>> assetDependencies = new Dictionary<string, List<string>>();
    private Dictionary<string, List<string>> assetReferences = new Dictionary<string, List<string>>();

    // 상태 변수 추가
    private bool analysisCompleted = false; // 분석이 완료되었는지 여부
    #endregion

    private List<string> selectedAssets = new List<string>();

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
        InitializeAssetList();
        wantsMouseMove = true; // 마우스 이벤트를 받을 수 있도록 설정
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
            if (index < selectedAssets.Count)
            {
                string assetPath = selectedAssets[index];
                string assetName = System.IO.Path.GetFileName(assetPath);
                
                // 아이콘 표시
                Texture icon = AssetDatabase.GetCachedIcon(assetPath);
                if (icon != null)
                {
                    GUI.DrawTexture(new Rect(rect.x, rect.y, 16, 16), icon);
                }
                
                // 에셋 이름 표시
                EditorGUI.LabelField(new Rect(rect.x + 20, rect.y, rect.width - 20, rect.height), assetName);
            }
        };
        
        // 추가 버튼 이벤트
        assetList.onAddCallback = (ReorderableList list) => {
            string path = EditorUtility.OpenFilePanel("에셋 선택", "Assets", "");
            if (!string.IsNullOrEmpty(path))
            {
                string relativePath = path.Replace(Application.dataPath, "Assets");
                if (!selectedAssets.Contains(relativePath))
                {
                    selectedAssets.Add(relativePath);
                    Repaint();
                }
            }
        };
        
        // 항목 제거 버튼 이벤트
        assetList.onRemoveCallback = (ReorderableList list) => {
            if (list.index >= 0 && list.index < selectedAssets.Count)
            {
                selectedAssets.RemoveAt(list.index);
                Repaint();
            }
        };
        
        // 선택 이벤트
        assetList.onSelectCallback = (ReorderableList list) => {
            if (list.index >= 0 && list.index < selectedAssets.Count)
            {
                string assetPath = selectedAssets[list.index];
                Object asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
                if (asset != null)
                {
                    EditorGUIUtility.PingObject(asset);
                }
            }
        };
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
    
    private void LogDependencyDetails(string sourceAsset, List<string> dependencies)
    {
        Debug.Log($"Analyzing dependencies for: {sourceAsset}");
        foreach (var dep in dependencies)
        {
            Debug.Log($"  - Detected dependency: {dep}");
        }
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
            if (Selection.activeObject != null)
            {
                string path = AssetDatabase.GetAssetPath(Selection.activeObject);
                if (!string.IsNullOrEmpty(path) && !selectedAssets.Contains(path))
                {
                    selectedAssets.Add(path);
                    Repaint();
                }
            }
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
            OnAnalyzeButtonClicked();
        }
        
        EditorGUI.EndDisabledGroup();
        
        GUILayout.EndVertical();
        
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
    
        GUILayout.Button(EditorGUIUtility.IconContent("d_ToolHandleLocal"), EditorStyles.toolbarButton, GUILayout.Width(30));
        GUILayout.Button(EditorGUIUtility.IconContent("d_GridAndSnap"), EditorStyles.toolbarButton, GUILayout.Width(30));
        GUILayout.Button(EditorGUIUtility.IconContent("d_ToolHandlePivot"), EditorStyles.toolbarButton, GUILayout.Width(30));
    
        GUILayout.FlexibleSpace();
    
        EditorGUILayout.LabelField("확대/축소:", GUILayout.Width(60));
        float zoom = GUILayout.HorizontalSlider(1.0f, 0.1f, 2.0f, GUILayout.Width(100));
    
        GUILayout.Button("100%", EditorStyles.toolbarButton, GUILayout.Width(50));
    
        EditorGUILayout.EndHorizontal();

        // 그래프 영역
        Rect graphScrollArea = EditorGUILayout.GetControlRect(false, rightContentRect.height - 40);
        GUI.Box(graphScrollArea, "", EditorStyles.helpBox);
    
        // 분석이 완료된 경우에만 그래프 표시
        if (analysisCompleted && nodePositions.Count > 0)
        {
            // 스크롤 뷰 (가상 캔버스 영역)
            Rect canvasRect = new Rect(0, 0, 5000, 5000);
            scrollPosition = GUI.BeginScrollView(graphScrollArea, scrollPosition, canvasRect);

            // 노드 그리기
            DrawNodes();
            
            // 연결 라인 그리기
            DrawConnectionLines();
            
            GUI.EndScrollView();
        }
        else
        {
            // 분석 전이면 안내 메시지 표시
            GUIStyle centeredStyle = new GUIStyle(GUI.skin.label);
            centeredStyle.alignment = TextAnchor.MiddleCenter;
            centeredStyle.fontSize = 14;
            GUI.Label(graphScrollArea, "왼쪽 패널에서 에셋을 선택하고 '선택한 에셋 분석' 버튼을 클릭하세요.", centeredStyle);
        }

        // 하단 상태 표시줄
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        int connectionCount = CountConnections();
        GUILayout.Label($"노드: {nodePositions.Count}개", EditorStyles.miniLabel);
        GUILayout.Label("|", EditorStyles.miniLabel);
        GUILayout.Label($"연결: {connectionCount}개", EditorStyles.miniLabel);

        GUILayout.FlexibleSpace();
    
        EditorGUILayout.EndHorizontal();

        GUILayout.EndArea();
    }
    
    private void DrawNodes()
    {
        if (nodePositions.Count == 0)
            return;
            
        foreach (var node in nodePositions)
        {
            string assetPath = node.Key;
            Rect position = node.Value;
            
            string fileName = System.IO.Path.GetFileName(assetPath);
            string fileExt = System.IO.Path.GetExtension(assetPath).ToLower();

            // 노드 유형에 따라 색상 결정
            Color nodeColor = Color.gray;
            string nodeType = "Unknown";

            if (fileExt == ".cs")
            {
                nodeColor = new Color(0.3f, 0.5f, 0.8f); // 파란색 (스크립트)
                nodeType = "Script";
            }
            else if (fileExt == ".prefab")
            {
                nodeColor = new Color(0.8f, 0.5f, 0.3f); // 주황색 (프리팹)
                nodeType = "Prefab";
            }
            else if (fileExt == ".unity")
            {
                nodeColor = new Color(0.3f, 0.8f, 0.5f); // 녹색 (씬)
                nodeType = "Scene";
            }
            else if (fileExt == ".mat")
            {
                nodeColor = new Color(0.8f, 0.3f, 0.5f); // 분홍색 (머티리얼)
                nodeType = "Material";
            }
            else if (fileExt == ".shader")
            {
                nodeColor = new Color(0.5f, 0.3f, 0.8f); // 보라색 (셰이더)
                nodeType = "Shader";
            }
            else if (fileExt == ".asset")
            {
                nodeColor = new Color(0.5f, 0.8f, 0.3f); // 연두색 (에셋)
                nodeType = "Asset";
            }
            else if (fileExt == ".anim")
            {
                nodeColor = new Color(0.8f, 0.8f, 0.3f); // 노란색 (애니메이션)
                nodeType = "Animation";
            }
            else if (fileExt == ".controller")
            {
                nodeColor = new Color(0.8f, 0.6f, 0.8f); // 연보라색 (애니메이터 컨트롤러)
                nodeType = "Animator Controller";
            }
            else if (fileExt == ".png" || fileExt == ".jpg" || fileExt == ".jpeg" || fileExt == ".tga")
            {
                nodeColor = new Color(0.3f, 0.8f, 0.8f); // 청록색 (텍스쳐)
                nodeType = "Texture";
            }
        
            // 선택된 에셋 강조
            bool isSelected = selectedAssets.Contains(assetPath);
            if (isSelected)
            {
                nodeColor = Color.Lerp(nodeColor, Color.white, 0.3f);
            }
        
            // 노드 그리기
            DrawSingleNode(position, fileName, nodeType, nodeColor, isSelected);
        }
    }
    
    // 분석 버튼 클릭 핸들러
    private void OnAnalyzeButtonClicked()
    {
        CalculateDependency();
        ArrangeNodes();
        
        analysisCompleted = true;
        Repaint(); // UI 갱신 요청
    }
    
    private void CalculateDependency()
    {
        // 기존 데이터 초기화
        assetDependencies.Clear();
        assetReferences.Clear();
        nodePositions.Clear();
        analysisCompleted = false;
    
        // 처리된 에셋을 추적하기 위한 집합
        HashSet<string> processedAssets = new HashSet<string>();
    
        // 모든 선택된 에셋에 대해 재귀적으로 종속성 분석
        foreach (string assetPath in selectedAssets)
        {
            AnalyzeAssetRecursively(assetPath, processedAssets, 0);
        }
    
        analysisCompleted = true;
    }

    private void AnalyzeAssetRecursively(string assetPath, HashSet<string> processedAssets, int depth, int maxDepth = 5)
    {
        // 재귀 깊이 제한 또는 이미 처리된 에셋이면 중단
        if (depth >= maxDepth || processedAssets.Contains(assetPath))
            return;
        
        // 에셋 처리 표시
        processedAssets.Add(assetPath);
    
        // 기본 종속성 맵 초기화
        if (!assetDependencies.ContainsKey(assetPath))
        {
            assetDependencies[assetPath] = new List<string>();
        }
    
        // 에셋 타입에 따른 분석
        string ext = System.IO.Path.GetExtension(assetPath).ToLower();
        List<string> dependencies = new List<string>();
    
        if (ext == ".cs")
        {
            // 스크립트 분석
            dependencies = AnalyzeScript(assetPath);
        }
        else
        {
            // 일반 에셋 분석
            dependencies = AnalyzeAsset(assetPath);
        }
    
        // 발견된 모든 종속성에 대해
        foreach (string dependency in dependencies)
        {
            // 종속성 추가
            AddDependency(assetPath, dependency);
        
            // 재귀적으로 종속성 분석
            AnalyzeAssetRecursively(dependency, processedAssets, depth + 1, maxDepth);
        }
    }

    private List<string> AnalyzeAsset(string assetPath)
    {
        List<string> result = new List<string>();
        string[] dependencies = AssetDatabase.GetDependencies(assetPath, false);
    
        foreach (string dep in dependencies)
        {
            // 자기 자신, 패키지, 라이브러리 파일 제외
            if (dep != assetPath && !dep.StartsWith("Packages/") && !dep.StartsWith("Library/"))
            {
                result.Add(dep);
            }
        }
    
        return result;
    }

    private List<string> AnalyzeScript(string scriptPath)
    {
        List<string> result = new List<string>();
        
        try
        {
            // 스크립트에서 타입 가져오기
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
            if (script == null) return result;
            
            Type scriptType = script.GetClass();
            if (scriptType == null) 
            {
                return result;
            }
            
            // 프로젝트 내 모든 C# 스크립트 가져오기
            string[] allScriptGuids = AssetDatabase.FindAssets("t:MonoScript");
            
            foreach (string guid in allScriptGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path == scriptPath || !path.EndsWith(".cs")) continue;
                
                MonoScript otherScript = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (otherScript == null) continue;
                
                Type otherType = otherScript.GetClass();
                if (otherType == null) continue;
                
                // 스크립트 타입 간의 참조 관계 확인
                if (HasTypeReference(scriptType, otherType))
                {
                    result.Add(path);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"어셈블리 분석 오류: {e.Message}");
        }
        
        return result;
    }

    // 한 타입이 다른 타입을 참조하는지 확인
    private bool HasTypeReference(Type sourceType, Type targetType)
    {
        try
        {
            string targetTypeName = targetType.Name;
            
            // 1. 상속 관계 확인
            if (sourceType.BaseType != null &&
                (sourceType.BaseType == targetType || sourceType.BaseType.Name == targetTypeName))
            {
                return true;
            }
                
            // 2. 인터페이스 구현 확인
            foreach (Type interfaceType in sourceType.GetInterfaces())
            {
                if (interfaceType == targetType || interfaceType.Name == targetTypeName)
                {
                    return true;
                }
            }
            
            // 3. 필드 타입 확인
            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | 
                                BindingFlags.Instance | BindingFlags.Static;
                                
            foreach (FieldInfo field in sourceType.GetFields(flags))
            {
                Type fieldType = field.FieldType;
                
                // 배열인 경우 요소 타입 확인
                if (fieldType.IsArray && fieldType.GetElementType() == targetType)
                {
                    return true;
                }
                    
                // 제네릭 타입인 경우 인자 타입 확인
                if (fieldType.IsGenericType)
                {
                    foreach (Type argType in fieldType.GetGenericArguments())
                    {
                        if (argType == targetType || argType.Name == targetTypeName)
                        {
                            return true;
                        }
                    }
                }
                
                // 직접 타입 비교
                if (fieldType == targetType || fieldType.Name == targetTypeName)
                {
                    return true;
                }
            }
            
            // 4. 메서드 파라미터 및 반환 타입 확인
            foreach (MethodInfo method in sourceType.GetMethods(flags))
            {
                // 반환 타입 확인
                if (method.ReturnType == targetType || method.ReturnType.Name == targetTypeName)
                {
                    return true;
                }
                    
                // 파라미터 타입 확인
                foreach (ParameterInfo param in method.GetParameters())
                {
                    Type paramType = param.ParameterType;
                    
                    if (paramType == targetType || paramType.Name == targetTypeName)
                    {
                        return true;
                    }
                        
                    // 제네릭 파라미터 확인
                    if (paramType.IsGenericType)
                    {
                        foreach (Type argType in paramType.GetGenericArguments())
                        {
                            if (argType == targetType || argType.Name == targetTypeName)
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            
            return false;
        }
        catch
        {
            // 오류 발생 시 안전하게 처리
            return false;
        }
    }

    // 의존성 추가 헬퍼 메서드
    private void AddDependency(string sourceAsset, string targetAsset)
    {
        // 종속성 추가
        if (!assetDependencies.ContainsKey(sourceAsset))
        {
            assetDependencies[sourceAsset] = new List<string>();
        }
        
        if (!assetDependencies[sourceAsset].Contains(targetAsset))
        {
            assetDependencies[sourceAsset].Add(targetAsset);
        }
    }
    
    private void DetectCycles()
    {
        cyclicEdges = new HashSet<Tuple<string, string>>();
        HashSet<string> visited = new HashSet<string>();
        HashSet<string> inStack = new HashSet<string>();
        
        foreach (var node in assetDependencies.Keys)
        {
            if (!visited.Contains(node))
            {
                DFSForCycleDetection(node, visited, inStack);
            }
        }
    }
    
    private void DFSForCycleDetection(string node, HashSet<string> visited, HashSet<string> inStack)
    {
        visited.Add(node);
        inStack.Add(node);
        
        if (assetDependencies.ContainsKey(node))
        {
            foreach (var dep in assetDependencies[node])
            {
                if (!visited.Contains(dep))
                {
                    DFSForCycleDetection(dep, visited, inStack);
                }
                else if (inStack.Contains(dep))
                {
                    // 사이클 발견
                    cyclicEdges.Add(new Tuple<string, string>(node, dep));
                }
            }
        }
        
        inStack.Remove(node);
    }
    
    private void ArrangeNodes()
    {
        nodePositions.Clear();
        cyclicEdges = new HashSet<Tuple<string, string>>();
        
        // 모든 에셋 경로 수집
        HashSet<string> allAssetPaths = new HashSet<string>(selectedAssets);
        
        foreach (var kvp in assetDependencies)
        {
            allAssetPaths.Add(kvp.Key);
            foreach (string dep in kvp.Value)
            {
                allAssetPaths.Add(dep);
            }
        }
        
        foreach (var kvp in assetReferences)
        {
            allAssetPaths.Add(kvp.Key);
            foreach (string ref_ in kvp.Value)
            {
                allAssetPaths.Add(ref_);
            }
        }

        // 사이클 감지
        DetectCycles();
        
        // 의존성 깊이에 따라 노드 카테고리 구성
        Dictionary<string, int> nodeDepths = CalculateNodeDepths(allAssetPaths);
        
        // 사이클에 포함된 노드 식별
        HashSet<string> nodesInCycles = IdentifyNodesInCycles();
        
        // 파일 타입별로 y 좌표 구성 (수직 정렬용)
        Dictionary<string, int> fileTypeYPositions = new Dictionary<string, int>
        {
            { ".cs", 0 },      // 스크립트 파일 (맨 위에 배치)
            { ".prefab", 1 },  // 프리팹
            { ".png", 2 },     // 이미지
            { ".wav", 3 },     // 오디오
            { ".fbx", 4 },     // 모델
            { ".mat", 5 },     // 재질
            { ".controller", 6 }, // 애니메이터 컨트롤러
            { "default", 7 }   // 기타
        };
        
        // 각 깊이별 노드 그룹화
        Dictionary<int, List<string>> depthTypeNodes = new Dictionary<int, List<string>>();
        
        foreach (var nodePath in allAssetPaths)
        {
            int depth = nodeDepths.ContainsKey(nodePath) ? nodeDepths[nodePath] : 0;
            
            string ext = System.IO.Path.GetExtension(nodePath).ToLower();
            
            if (!depthTypeNodes.ContainsKey(depth))
            {
                depthTypeNodes[depth] = new List<string>();
            }
            
            depthTypeNodes[depth].Add(nodePath);
        }
        
        // 각 노드의 위치 설정
        float startX = 80f;
        float startY = 80f;
        float depthSpacing = 380f;  // 깊이에 따른 x 간격 (더 늘림)
        float nodeSpacingX = 50f;   // 같은 깊이 내의 노드 간 x 간격
        float nodeSpacingY = 30f;   // 같은 깊이 내의 노드 간 y 간격
        
        // 각 깊이별로 처리
        for (int depth = 0; depth < depthTypeNodes.Count; depth++)
        {
            float depthX = startX + depth * depthSpacing;
            
            List<string> nodes = depthTypeNodes[depth];
            
            // 같은 타입 내의 노드 간 간격 조정
            float maxWidth = Mathf.Max(nodes.Count * (nodeWidth + nodeSpacingX) - nodeSpacingX, 0);
            float startNodeX = depthX;
            float startNodeY = startY;
            
            // 첫 번째 깊이(루트 노드)면 노드 간 간격을 더 벌림
            if (depth == 0 && nodes.Count > 1)
            {
                nodeSpacingX = 80f;  // 루트 노드 간 간격 더 크게
                maxWidth = nodes.Count * (nodeWidth + nodeSpacingX) - nodeSpacingX;
                startNodeX = Math.Max(startX, depthX - maxWidth / 2 + nodeWidth / 2);
            }
            
            // 각 노드 배치
            for (int i = 0; i < nodes.Count; i++)
            {
                string nodePath = nodes[i];
                float nodeX = startNodeX;
                float nodeY = startNodeY + i * (nodeHeight + nodeSpacingY);
                
                // 사이클 노드는 약간 아래로 오프셋
                if (nodesInCycles.Contains(nodePath))
                {
                    nodeY += 15f;
                }
                
                nodePositions[nodePath] = new Rect(nodeX, nodeY, nodeWidth, nodeHeight);
            }
        }
    }

    // 의존성 깊이 계산 메서드
    private Dictionary<string, int> CalculateNodeDepths(HashSet<string> nodes)
    {
        Dictionary<string, int> nodeDepths = new Dictionary<string, int>();
        
        // 루트 노드는 깊이 0
        foreach (var node in selectedAssets)
        {
            nodeDepths[node] = 0;
        }
        
        // BFS로 의존성 깊이 계산
        Queue<string> queue = new Queue<string>(selectedAssets);
        HashSet<string> visited = new HashSet<string>(selectedAssets);
        int currentDepth = 1;
        
        while (queue.Count > 0)
        {
            int levelSize = queue.Count;
            
            for (int i = 0; i < levelSize; i++)
            {
                string currentNode = queue.Dequeue();
                
                if (assetDependencies.ContainsKey(currentNode))
                {
                    foreach (string dep in assetDependencies[currentNode])
                    {
                        if (!visited.Contains(dep) && nodes.Contains(dep))
                        {
                            nodeDepths[dep] = currentDepth;
                            queue.Enqueue(dep);
                            visited.Add(dep);
                        }
                    }
                }
            }
            
            currentDepth++;
        }
        
        return nodeDepths;
    }

    // 화살표 그리기 메서드 수정 (색상 파라미터 추가)
    private void DrawArrow(Vector2 position, Vector2 direction, float size, Color color)
    {
        Vector2 right = new Vector2(-direction.y, direction.x).normalized * size * 0.5f;
        Vector2 tip = position + direction * size;
        Vector2 baseLeft = position - right;
        Vector2 baseRight = position + right;

        Handles.color = color;
        Handles.DrawAAConvexPolygon(new Vector3[] { tip, baseLeft, baseRight });
    }
    
    // 사이클에 포함된 노드들을 식별하는 메서드
    private HashSet<string> IdentifyNodesInCycles()
    {
        HashSet<string> nodesInCycles = new HashSet<string>();
        
        foreach (var edge in cyclicEdges)
        {
            nodesInCycles.Add(edge.Item1);
            nodesInCycles.Add(edge.Item2);
        }
        
        // 사이클에 속한 노드들의 모든 연결을 확인하여 완전한 사이클 찾기
        bool changed = true;
        while (changed)
        {
            changed = false;
            HashSet<string> newNodes = new HashSet<string>();
            
            foreach (var node in nodesInCycles)
            {
                if (assetDependencies.ContainsKey(node))
                {
                    foreach (var dep in assetDependencies[node])
                    {
                        if (nodesInCycles.Contains(dep) && !cyclicEdges.Contains(new Tuple<string, string>(node, dep)))
                        {
                            newNodes.Add(dep);
                            cyclicEdges.Add(new Tuple<string, string>(node, dep));
                            changed = true;
                        }
                    }
                }
            }
            
            nodesInCycles.UnionWith(newNodes);
        }
        
        return nodesInCycles;
    }
    
    // DrawSingleNode 메서드 수정 - 노드 테두리 개선
    private void DrawSingleNode(Rect position, string title, string type, Color color, bool isSelected = false)
    {
        // 파일 이름만 추출
        string displayName = System.IO.Path.GetFileName(title);
        bool isInCycle = cyclicEdges != null && 
                        cyclicEdges.Any(e => 
                            e.Item1 == title || e.Item2 == title || 
                            (System.IO.Path.GetFileName(e.Item1) == displayName || System.IO.Path.GetFileName(e.Item2) == displayName));
        
        // 그림자 효과
        GUI.color = new Color(0, 0, 0, 0.3f);
        GUI.Box(new Rect(position.x + 3, position.y + 3, position.width, position.height), "", "flow node 0");
        GUI.color = Color.white;

        // 노드 배경
        EditorGUI.DrawRect(position, color);

        // 테두리 - 사이클이면 빨간색, 선택됐으면 흰색, 아니면 반투명 흰색
        Color borderColor = isSelected ? Color.white : (isInCycle ? Color.red : new Color(1, 1, 1, 0.5f));
        float borderWidth = isSelected ? 2f : (isInCycle ? 1.5f : 1f);
        
        // 위/아래 테두리
        EditorGUI.DrawRect(new Rect(position.x, position.y - borderWidth, position.width, borderWidth), borderColor);
        EditorGUI.DrawRect(new Rect(position.x, position.y + position.height, position.width, borderWidth), borderColor);
        
        // 좌/우 테두리
        EditorGUI.DrawRect(new Rect(position.x - borderWidth, position.y - borderWidth, borderWidth, position.height + borderWidth * 2), borderColor);
        EditorGUI.DrawRect(new Rect(position.x + position.width, position.y - borderWidth, borderWidth, position.height + borderWidth * 2), borderColor);
        
        // 제목 줄 배경 (약간 어두운 그라데이션)
        EditorGUI.DrawRect(new Rect(position.x, position.y, position.width, 22), new Color(0, 0, 0, 0.1f));

        // 노드 내용 - 파일 이름과 타입 표시
        GUIStyle titleStyle = new GUIStyle(EditorStyles.whiteBoldLabel);
        titleStyle.alignment = TextAnchor.MiddleLeft;
        GUI.Label(new Rect(position.x + 8, position.y, position.width - 16, 22), displayName, titleStyle);
        
        GUIStyle typeStyle = new GUIStyle(EditorStyles.whiteLabel);
        typeStyle.alignment = TextAnchor.MiddleLeft;
        typeStyle.fontSize = 10;
        GUI.Label(new Rect(position.x + 8, position.y + 24, position.width - 16, 20), type, typeStyle);
    }

    // 연결 수 계산 메서드
    private int CountConnections()
    {
        int count = 0;
        foreach (var kvp in assetDependencies)
        {
            count += kvp.Value.Count;
        }
        return count;
    }
    
    // 수정된 연결점 계산 메서드 - 역참조 사이클을 위한 특별 처리 추가
    private Vector2 GetCorrectConnectionPoint(Rect rect, Rect targetRect, bool isSource)
    {
        Vector2 center = new Vector2(rect.x + rect.width / 2, rect.y + rect.height / 2);
        Vector2 targetCenter = new Vector2(targetRect.x + targetRect.width / 2, targetRect.y + targetRect.height / 2);
        
        // 일반적인 방향 판단 (왼쪽->오른쪽)
        bool isTargetOnRight = targetCenter.x > center.x;
        
        // 역참조 사이클 판단 (깊이가 더 낮은 노드로 돌아가는 경우)
        bool isBackReference = targetCenter.x < center.x;
        
        if (isBackReference)
        {
            // 역참조의 경우 위/아래 연결점 사용
            if (isSource)
            {
                // 소스 노드 - 아래쪽 가장자리에서 시작
                return new Vector2(
                    rect.x + rect.width / 2,
                    rect.y + rect.height
                );
            }
            else
            {
                // 타겟 노드 - 위쪽 가장자리로
                return new Vector2(
                    targetRect.x + targetRect.width / 2,
                    targetRect.y
                );
            }
        }
        else
        {
            // 일반적인 참조는 왼쪽/오른쪽 연결점 사용
            if (isSource) 
            {
                // 소스 노드(A가 B를 참조함)
                return new Vector2(
                    isTargetOnRight ? rect.x + rect.width : rect.x,
                    rect.y + rect.height / 2
                );
            }
            else 
            {
                // 타겟 노드
                return new Vector2(
                    isTargetOnRight ? targetRect.x : targetRect.x + targetRect.width,
                    targetRect.y + targetRect.height / 2
                );
            }
        }
    }

    // 연결선 그리기 메서드 수정
    private void DrawConnectionLines()
    {
        if (Event.current.type != EventType.Repaint)
            return;
            
        Handles.BeginGUI();

        // 일반 연결선 먼저 그리기 (사이클 제외)
        foreach (var kvp in assetDependencies)
        {
            string sourceAsset = kvp.Key;
        
            if (!nodePositions.ContainsKey(sourceAsset))
                continue;
            
            Rect sourceRect = nodePositions[sourceAsset];

            foreach (string targetAsset in kvp.Value)
            {
                // 자기 자신을 참조하는 경우 건너뛰기
                if (sourceAsset == targetAsset)
                    continue;
                    
                if (!nodePositions.ContainsKey(targetAsset))
                    continue;
                    
                // 사이클 엣지는 별도로 처리
                if (cyclicEdges.Contains(new Tuple<string, string>(sourceAsset, targetAsset)))
                    continue;
                    
                Rect targetRect = nodePositions[targetAsset];
                
                // 왼쪽에서 오른쪽으로 그리기 (정방향 참조)
                Vector2 start = new Vector2(sourceRect.x + sourceRect.width, sourceRect.y + sourceRect.height / 2);
                Vector2 end = new Vector2(targetRect.x, targetRect.y + targetRect.height / 2);
                
                // 정방향 화살표
                Handles.color = new Color(0.4f, 0.6f, 1.0f, 0.8f);
                Handles.DrawLine(start, end);
                DrawArrow(end, (end - start).normalized, 8, Handles.color);
            }
        }
        
        // 사이클 연결선 별도로 그리기
        foreach (var edge in cyclicEdges)
        {
            string sourceAsset = edge.Item1;
            string targetAsset = edge.Item2;
            
            // 자기 자신을 참조하는 경우 건너뛰기
            if (sourceAsset == targetAsset)
                continue;
                
            if (!nodePositions.ContainsKey(sourceAsset) || !nodePositions.ContainsKey(targetAsset))
                continue;
                
            Rect sourceRect = nodePositions[sourceAsset];
            Rect targetRect = nodePositions[targetAsset];
            
            // 역방향 참조 (오른쪽에서 왼쪽으로) - 아래로 휘어지는 곡선
            // 역방향 참조 (오른쪽에서 왼쪽으로) - 아래로 휘어지는 곡선으로 수정
            if (targetRect.x < sourceRect.x)
            {
                // 아래쪽에서 출발해서 아래쪽으로 도착
                Vector2 start = new Vector2(sourceRect.x + sourceRect.width / 2, sourceRect.y + sourceRect.height);
                Vector2 end = new Vector2(targetRect.x + targetRect.width / 2, targetRect.y + targetRect.height);
    
                float distance = Vector2.Distance(start, end);
                Vector2 midPoint = (start + end) / 2;
    
                // 더 크게 아래로 휘어짐
                Vector2 controlPoint = midPoint + new Vector2(0, distance * 0.5f);
    
                Handles.color = Color.red;
                Handles.DrawBezier(
                    start, end,
                    Vector2.Lerp(start, controlPoint, 0.5f),
                    Vector2.Lerp(controlPoint, end, 0.5f),
                    Color.red, null, 2f);
    
                // 화살표 방향 계산 (위쪽 방향)
                Vector2 arrowTip = end;
                Vector2 arrowDirection = Vector2.up; // 아래에서 위로 향하는 화살표
                DrawArrow(arrowTip, arrowDirection, 10, Color.red);
            }
            else
            {
                // 일반 사이클 (같은 방향인데 사이클인 경우) - 곡선으로 그리기
                Vector2 start = new Vector2(sourceRect.x + sourceRect.width, sourceRect.y + sourceRect.height / 2);
                Vector2 end = new Vector2(targetRect.x, targetRect.y + targetRect.height / 2);
                
                float distance = Vector2.Distance(start, end);
                Vector2 midPoint = (start + end) / 2;
                Vector2 perpendicular = new Vector2(0, -distance * 0.2f); // 위로 휘어짐
                
                Handles.color = Color.red;
                Handles.DrawBezier(
                    start, end, 
                    start + (midPoint - start) * 0.5f + perpendicular,
                    end + (midPoint - end) * 0.5f + perpendicular,
                    Color.red, null, 2f);
                
                DrawArrow(end, (end - (end + (midPoint - end) * 0.5f + perpendicular)).normalized, 10, Color.red);
            }
        }

        Handles.EndGUI();
    }
}