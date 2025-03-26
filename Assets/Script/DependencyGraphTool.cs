using System;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditorInternal;
using Object = UnityEngine.Object;

public class DependencyGraphTool : EditorWindow 
{
    #region Node
    private float nodeWidth = 200f;
    private float nodeHeight = 50f;
    private Dictionary<string, Rect> nodePositions = new Dictionary<string, Rect>();
    private Dictionary<string, List<string>> dependencyDict = new Dictionary<string, List<string>>();
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

    // 노드 배치를 위한 변수
    private float horizontalSpacing = 250f;
    private float verticalSpacing = 100f;
    private float startX = 50f;
    private float startY = 50f;

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
            Rect canvasRect = new Rect(0, 0, 2000, 2000);
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
                return true;
                
            // 2. 인터페이스 구현 확인
            foreach (Type interfaceType in sourceType.GetInterfaces())
            {
                if (interfaceType == targetType || interfaceType.Name == targetTypeName)
                    return true;
            }
            
            // 3. 필드 타입 확인
            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | 
                                BindingFlags.Instance | BindingFlags.Static;
                                
            foreach (FieldInfo field in sourceType.GetFields(flags))
            {
                Type fieldType = field.FieldType;
                
                // 배열인 경우 요소 타입 확인
                if (fieldType.IsArray && fieldType.GetElementType() == targetType)
                    return true;
                    
                // 제네릭 타입인 경우 인자 타입 확인
                if (fieldType.IsGenericType)
                {
                    foreach (Type argType in fieldType.GetGenericArguments())
                    {
                        if (argType == targetType || argType.Name == targetTypeName)
                            return true;
                    }
                }
                
                // 직접 타입 비교
                if (fieldType == targetType || fieldType.Name == targetTypeName)
                    return true;
            }
            
            // 4. 메서드 파라미터 및 반환 타입 확인
            foreach (MethodInfo method in sourceType.GetMethods(flags))
            {
                // 반환 타입 확인
                if (method.ReturnType == targetType || method.ReturnType.Name == targetTypeName)
                    return true;
                    
                // 파라미터 타입 확인
                foreach (ParameterInfo param in method.GetParameters())
                {
                    Type paramType = param.ParameterType;
                    
                    if (paramType == targetType || paramType.Name == targetTypeName)
                        return true;
                        
                    // 제네릭 파라미터 확인
                    if (paramType.IsGenericType)
                    {
                        foreach (Type argType in paramType.GetGenericArguments())
                        {
                            if (argType == targetType || argType.Name == targetTypeName)
                                return true;
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
        
        // 역참조 추가
        if (!assetReferences.ContainsKey(targetAsset))
        {
            assetReferences[targetAsset] = new List<string>();
        }
        
        if (!assetReferences[targetAsset].Contains(sourceAsset))
        {
            assetReferences[targetAsset].Add(sourceAsset);
        }
    }
    
    private void ArrangeNodes()
    {
        nodePositions.Clear();
        
        // 모든 에셋 경로 수집 (선택된 에셋 + 종속성)
        HashSet<string> allAssetPaths = new HashSet<string>(selectedAssets);
        
        // 종속성 추가
        foreach (var kvp in assetDependencies)
        {
            allAssetPaths.Add(kvp.Key);
            foreach (string dep in kvp.Value)
            {
                allAssetPaths.Add(dep);
            }
        }
        
        // 역참조 추가 
        foreach (var kvp in assetReferences)
        {
            allAssetPaths.Add(kvp.Key);
            foreach (string ref_ in kvp.Value)
            {
                allAssetPaths.Add(ref_);
            }
        }
    
        // 계층별로 노드 그룹화
        Dictionary<int, List<string>> nodeLayers = new Dictionary<int, List<string>>();
    
        // 먼저 최상위 노드 (선택된 에셋)을 첫 번째 계층에 배치
        nodeLayers[0] = new List<string>(selectedAssets);
    
        // 나머지 노드를 계층으로 배치
        int currentLayer = 1;
        HashSet<string> processedNodes = new HashSet<string>(selectedAssets);
    
        while (true)
        {
            List<string> layerNodes = new List<string>();
        
            // 이전 계층의 모든 노드에 대해 종속성 검사
            foreach (string node in nodeLayers[currentLayer - 1])
            {
                if (assetDependencies.ContainsKey(node))
                {
                    foreach (string dep in assetDependencies[node])
                    {
                        if (!processedNodes.Contains(dep))
                        {
                            layerNodes.Add(dep);
                            processedNodes.Add(dep);
                        }
                    }
                }
            }
        
            // 새 노드가 없으면 중단
            if (layerNodes.Count == 0)
                break;
            
            nodeLayers[currentLayer] = layerNodes;
            currentLayer++;
        }
    
        // 각 계층마다 노드 위치 설정
        for (int layer = 0; layer < nodeLayers.Count; layer++)
        {
            List<string> layerNodes = nodeLayers[layer];
            float layerY = startY + layer * verticalSpacing;
        
            if (layerNodes.Count == 0)
                continue;
                
            // 계층 내에서 노드 간격 계산
            float nodeSpacing = horizontalSpacing;
            float layerWidth = nodeSpacing * (layerNodes.Count - 1);
            float startNodeX = startX + (1000 - layerWidth) / 2; // 중앙 정렬
        
            for (int i = 0; i < layerNodes.Count; i++)
            {
                string nodePath = layerNodes[i];
                float nodeX = startNodeX + i * nodeSpacing;
                nodePositions[nodePath] = new Rect(nodeX, layerY, nodeWidth, nodeHeight);
            }
        }
    }
    
    private void DrawConnectionLines()
    {
        if (Event.current.type != EventType.Repaint)
            return;
            
        Handles.BeginGUI();
    
        foreach (var kvp in assetDependencies)
        {
            string sourceAsset = kvp.Key;
        
            if (!nodePositions.ContainsKey(sourceAsset))
                continue;
            
            Rect sourceRect = nodePositions[sourceAsset];
            Vector2 sourcePos = new Vector2(sourceRect.x + sourceRect.width / 2, sourceRect.y + sourceRect.height);
        
            foreach (string targetAsset in kvp.Value)
            {
                if (!nodePositions.ContainsKey(targetAsset))
                    continue;
                
                Rect targetRect = nodePositions[targetAsset];
                Vector2 targetPos = new Vector2(targetRect.x + targetRect.width / 2, targetRect.y);
            
                // 단순 라인 그리기
                Handles.color = new Color(0.5f, 0.5f, 0.5f, 0.8f);
                Handles.DrawLine(sourcePos, targetPos);
            
                // 화살표 그리기
                DrawArrow(targetPos, Vector2.up, 10);
            }
        }
    
        Handles.EndGUI();
    }
    
    private void DrawArrow(Vector2 position, Vector2 direction, float size)
    {
        Vector2 right = new Vector2(-direction.y, direction.x).normalized * size * 0.5f;
        Vector2 tip = position + direction * size;
        Vector2 baseLeft = position - right;
        Vector2 baseRight = position + right;
    
        Handles.color = new Color(0.5f, 0.5f, 0.5f, 0.8f);
        Handles.DrawAAConvexPolygon(new Vector3[] { tip, baseLeft, baseRight });
    }

    private void DrawSingleNode(Rect position, string title, string type, Color color, bool isSelected = false)
    {
        // 그림자 효과
        GUI.color = new Color(0, 0, 0, 0.2f);
        GUI.Box(new Rect(position.x + 3, position.y + 3, position.width, position.height), "", "flow node 0");
        GUI.color = Color.white;

        // 노드 배경
        EditorGUI.DrawRect(position, color);

        // 테두리
        Color borderColor = isSelected ? Color.white : new Color(1, 1, 1, 0.3f);
        float borderWidth = isSelected ? 2f : 1f;
        var borderRect = new Rect(position.x - borderWidth, position.y - borderWidth, 
            position.width + borderWidth * 2, position.height + borderWidth * 2);
        EditorGUI.DrawRect(borderRect, borderColor);

        // 노드 내용
        GUI.Label(new Rect(position.x + 5, position.y + 5, position.width - 10, 20), title, EditorStyles.whiteBoldLabel);
        GUI.Label(new Rect(position.x + 5, position.y + 25, position.width - 10, 20), type, EditorStyles.whiteLabel);
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
}